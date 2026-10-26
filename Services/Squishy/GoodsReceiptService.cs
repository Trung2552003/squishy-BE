using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SystemBase.BE.DTOs.Shared;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Infrastructure.Dapper;
using SystemBase.BE.Models.SquishyManagement;

namespace SystemBase.BE.Services.Squishy;

public interface IGoodsReceiptService
{
    Task<RestData<List<GoodsReceiptDto>>> GetAllAsync();
    Task<RestData<GoodsReceiptDto>> GetByIdAsync(Guid id);
    Task<RestData<GoodsReceiptDto>> CreateReceiptAsync(CreateGoodsReceiptDto dto);
    Task<RestData<GoodsReceiptDto>> CancelReceiptAsync(Guid id, string? reason);
    Task<RestData<List<SupplierDto>>> GetSuppliersAsync();
    Task<RestData<SupplierDto>> CreateSupplierAsync(SupplierDto dto);
    Task<RestData<List<StockMovementReportDto>>> GetStockTransactionsAsync(Guid? productId = null);
}

public class GoodsReceiptService : IGoodsReceiptService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public GoodsReceiptService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RestData<List<GoodsReceiptDto>>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT r.*, s.Name AS SupplierName
            FROM GoodsReceipt r
            LEFT JOIN Supplier s ON r.SupplierId = s.Id
            WHERE r.IsDeleted = 0
            ORDER BY r.ReceiptDate DESC, r.CreatedAt DESC";

        var list = (await connection.QueryAsync<GoodsReceiptDto>(sql)).ToList();
        return new RestData<List<GoodsReceiptDto>> { IsSuccess = true, Data = list };
    }

    public async Task<RestData<GoodsReceiptDto>> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var receiptSql = @"
            SELECT r.*, s.Name AS SupplierName
            FROM GoodsReceipt r
            LEFT JOIN Supplier s ON r.SupplierId = s.Id
            WHERE r.Id = @Id AND r.IsDeleted = 0";

        var receipt = await connection.QueryFirstOrDefaultAsync<GoodsReceiptDto>(receiptSql, new { Id = id });
        if (receipt == null)
            return new RestData<GoodsReceiptDto> { IsSuccess = false, Message = "Không tìm thấy phiếu nhập." };

        var detailSql = @"
            SELECT d.*, p.Name + ISNULL(' - ' + v.Name, '') AS ProductName, v.Name AS VariantName, p.Sku
            FROM GoodsReceiptDetail d
            INNER JOIN Product p ON d.ProductId = p.Id
            LEFT JOIN ProductVariant v ON v.Id = d.VariantId
            WHERE d.GoodsReceiptId = @Id";

        var details = (await connection.QueryAsync<GoodsReceiptItemDto>(detailSql, new { Id = id })).ToList();
        receipt.Details = details;

        return new RestData<GoodsReceiptDto> { IsSuccess = true, Data = receipt };
    }

    public async Task<RestData<GoodsReceiptDto>> CreateReceiptAsync(CreateGoodsReceiptDto dto)
    {
        if (dto.Items == null || !dto.Items.Any())
            return Fail("Phiếu nhập phải có ít nhất 1 sản phẩm.");
        if (dto.Items.Any(i => i.Quantity <= 0))
            return Fail("Số lượng nhập phải lớn hơn 0.");
        if (dto.Items.Any(i => i.UnitPrice < 0) || dto.ShippingFee < 0)
            return Fail("Giá nhập và phí vận chuyển không được âm.");

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var item in dto.Items)
            {
                var name = await connection.QueryFirstOrDefaultAsync<string?>(
                    "SELECT Name FROM Product WHERE Id = @Id AND IsDeleted = 0", new { Id = item.ProductId }, transaction);
                if (name == null)
                    return Fail("Có sản phẩm không còn tồn tại. Vui lòng tải lại trang.");
                // Sản phẩm có màu thì phải nhập theo từng màu (cho phép nhập cả màu đang tạm ngừng bán)
                var variantError = await Inventory.ValidateVariantAsync(connection, transaction, item.ProductId, item.VariantId, name, requireActive: false);
                if (variantError != null)
                    return Fail(variantError);
            }

            // Sinh mã phiếu nhập PN-yyyyMMdd-###
            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var countToday = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM GoodsReceipt WITH (UPDLOCK, HOLDLOCK) WHERE Code LIKE @Prefix",
                new { Prefix = $"PN-{dateStr}-%" }, transaction);

            var receiptCode = $"PN-{dateStr}-{(countToday + 1):D3}";
            var receiptId = Guid.NewGuid();
            var goodsTotal = dto.Items.Sum(x => x.Quantity * x.UnitPrice);
            var totalQty = dto.Items.Sum(x => x.Quantity);

            await connection.ExecuteAsync(@"
                INSERT INTO GoodsReceipt (Id, Code, SupplierId, ReceiptDate, TotalAmount, ShippingFee, Status, Note, CreatedAt, UpdatedAt, IsDeleted)
                VALUES (@Id, @Code, @SupplierId, @ReceiptDate, @TotalAmount, @ShippingFee, 'COMPLETED', @Note, GETDATE(), GETDATE(), 0)",
                new
                {
                    Id = receiptId,
                    Code = receiptCode,
                    dto.SupplierId,
                    ReceiptDate = dto.ReceiptDate ?? DateTime.Now,
                    TotalAmount = goodsTotal + dto.ShippingFee,
                    dto.ShippingFee,
                    dto.Note
                }, transaction);

            foreach (var item in dto.Items)
            {
                // Phân bổ phí ship theo giá trị dòng hàng (hàng đắt chịu nhiều hơn); toàn hàng 0đ thì chia theo số lượng
                var lineValue = item.Quantity * item.UnitPrice;
                var allocated = goodsTotal > 0 ? dto.ShippingFee * lineValue / goodsTotal : dto.ShippingFee * item.Quantity / totalQty;
                var landedUnitCost = Math.Round(item.UnitPrice + allocated / item.Quantity, 2);

                await connection.ExecuteAsync(@"
                    INSERT INTO GoodsReceiptDetail (Id, GoodsReceiptId, ProductId, VariantId, Quantity, UnitPrice, TotalPrice, LandedUnitCost, CreatedAt)
                    VALUES (@Id, @GoodsReceiptId, @ProductId, @VariantId, @Quantity, @UnitPrice, @TotalPrice, @LandedUnitCost, GETDATE())",
                    new
                    {
                        Id = Guid.NewGuid(),
                        GoodsReceiptId = receiptId,
                        item.ProductId,
                        item.VariantId,
                        item.Quantity,
                        item.UnitPrice,
                        TotalPrice = lineValue,
                        LandedUnitCost = landedUnitCost
                    }, transaction);

                // Cộng kho, giá vốn bình quân gia quyền theo giá đã gồm phí ship (tính riêng cho từng màu)
                await Inventory.ChangeAsync(connection, transaction, item.ProductId, item.VariantId, item.Quantity, "IMPORT", receiptCode,
                    $"Nhập hàng theo phiếu {receiptCode}", Inventory.CostMode.Import, landedUnitCost);
            }

            transaction.Commit();
            return await GetByIdAsync(receiptId);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return Fail($"Lỗi khi tạo phiếu nhập: {ex.Message}");
        }
    }

    /// <summary>
    /// Hủy phiếu nhập nhầm: trừ lại tồn kho, tính ngược giá vốn bình quân, ghi thẻ kho.
    /// Không hủy được nếu hàng của phiếu đã bán bớt (tồn hiện tại ít hơn số đã nhập).
    /// </summary>
    public async Task<RestData<GoodsReceiptDto>> CancelReceiptAsync(Guid id, string? reason)
    {
        if ((reason?.Length ?? 0) > 500)
            return Fail("Lý do hủy tối đa 500 ký tự.");

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var receipt = await connection.QueryFirstOrDefaultAsync<GoodsReceipt>(
                "SELECT * FROM GoodsReceipt WITH (UPDLOCK) WHERE Id = @Id AND IsDeleted = 0", new { Id = id }, transaction);
            if (receipt == null) return Fail("Không tìm thấy phiếu nhập.");
            if (receipt.Status == "CANCELLED") return Fail("Phiếu nhập này đã được hủy trước đó.");

            var lines = (await connection.QueryAsync<GoodsReceiptDetail>(
                "SELECT * FROM GoodsReceiptDetail WHERE GoodsReceiptId = @Id", new { Id = id }, transaction)).ToList();

            // Gộp theo sản phẩm + màu (1 phiếu có thể có nhiều dòng cùng loại)
            var perLine = lines.GroupBy(l => new { l.ProductId, l.VariantId }).Select(g => new
            {
                g.Key.ProductId,
                g.Key.VariantId,
                Quantity = g.Sum(x => x.Quantity),
                Value = g.Sum(x => x.Quantity * (x.LandedUnitCost ?? x.UnitPrice))
            }).ToList();

            var shortages = new List<string>();
            foreach (var l in perLine)
            {
                var stock = await Inventory.GetStockAsync(connection, transaction, l.ProductId, l.VariantId);
                if (stock < l.Quantity)
                {
                    var name = await connection.QueryFirstOrDefaultAsync<string?>(@"
                        SELECT p.Name + ISNULL(' - ' + v.Name, '') FROM Product p LEFT JOIN ProductVariant v ON v.Id = @VariantId WHERE p.Id = @Id",
                        new { Id = l.ProductId, l.VariantId }, transaction) ?? "Sản phẩm đã xóa";
                    shortages.Add($"\"{name}\" chỉ còn {stock}/{l.Quantity} chiếc");
                }
            }
            if (shortages.Count > 0)
                return Fail($"Không hủy được vì hàng của phiếu đã bán bớt: {string.Join("; ", shortages)}. " +
                            "Hãy dùng phiếu nhập mới để điều chỉnh phần chênh lệch.");

            foreach (var l in perLine)
            {
                // Trừ lại kho và bỏ giá trị lô nhập ra khỏi giá vốn bình quân
                await Inventory.ChangeAsync(connection, transaction, l.ProductId, l.VariantId, -l.Quantity, "IMPORT_CANCEL", receipt.Code,
                    $"Hủy phiếu nhập {receipt.Code}" + (string.IsNullOrWhiteSpace(reason) ? "" : $": {reason.Trim()}"),
                    Inventory.CostMode.CancelImport, l.Value);
            }

            await connection.ExecuteAsync(@"
                UPDATE GoodsReceipt SET Status = 'CANCELLED', CancelledAt = GETDATE(), CancelReason = @Reason, UpdatedAt = GETDATE()
                WHERE Id = @Id", new { Id = id, Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim() }, transaction);

            transaction.Commit();
            var result = await GetByIdAsync(id);
            result.Message = $"Đã hủy phiếu {receipt.Code} và trừ lại tồn kho.";
            return result;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return Fail($"Lỗi khi hủy phiếu nhập: {ex.Message}");
        }
    }

    private static RestData<GoodsReceiptDto> Fail(string message) => new() { IsSuccess = false, Message = message };

    public async Task<RestData<List<SupplierDto>>> GetSuppliersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT * FROM Supplier WHERE IsDeleted = 0 ORDER BY Name ASC";
        var list = (await connection.QueryAsync<SupplierDto>(sql)).ToList();
        return new RestData<List<SupplierDto>> { IsSuccess = true, Data = list };
    }

    public async Task<RestData<SupplierDto>> CreateSupplierAsync(SupplierDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Code = string.IsNullOrWhiteSpace(dto.Code) ? $"NCC-{DateTime.Now:yyyyMMddHHmm}" : dto.Code.Trim().ToUpper(),
            Name = dto.Name.Trim(),
            Phone = dto.Phone,
            Email = dto.Email,
            Address = dto.Address,
            Note = dto.Note,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsDeleted = false
        };

        var sql = @"INSERT INTO Supplier (Id, Code, Name, Phone, Email, Address, Note, IsActive, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES (@Id, @Code, @Name, @Phone, @Email, @Address, @Note, @IsActive, @CreatedAt, @UpdatedAt, @IsDeleted)";
        await connection.ExecuteAsync(sql, supplier);

        dto.Id = supplier.Id;
        dto.Code = supplier.Code;
        return new RestData<SupplierDto> { IsSuccess = true, Data = dto };
    }

    public async Task<RestData<List<StockMovementReportDto>>> GetStockTransactionsAsync(Guid? productId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var whereClause = "1=1";
        var parameters = new DynamicParameters();

        if (productId.HasValue && productId != Guid.Empty)
        {
            whereClause += " AND st.ProductId = @ProductId";
            parameters.Add("ProductId", productId.Value);
        }

        var sql = $@"
            SELECT st.*, p.Name + ISNULL(' - ' + v.Name, '') AS ProductName, p.Sku
            FROM StockTransaction st
            INNER JOIN Product p ON st.ProductId = p.Id
            LEFT JOIN ProductVariant v ON v.Id = st.VariantId
            WHERE {whereClause}
            ORDER BY st.CreatedAt DESC";

        var list = (await connection.QueryAsync<StockMovementReportDto>(sql, parameters)).ToList();
        return new RestData<List<StockMovementReportDto>> { IsSuccess = true, Data = list };
    }
}
