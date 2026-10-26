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
            SELECT d.*, p.Name AS ProductName, p.Sku
            FROM GoodsReceiptDetail d
            INNER JOIN Product p ON d.ProductId = p.Id
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
            var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
            var existing = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Product WHERE Id IN @Ids AND IsDeleted = 0", new { Ids = productIds }, transaction);
            if (existing != productIds.Count)
                return Fail("Có sản phẩm không còn tồn tại. Vui lòng tải lại trang.");

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
                    INSERT INTO GoodsReceiptDetail (Id, GoodsReceiptId, ProductId, Quantity, UnitPrice, TotalPrice, LandedUnitCost, CreatedAt)
                    VALUES (@Id, @GoodsReceiptId, @ProductId, @Quantity, @UnitPrice, @TotalPrice, @LandedUnitCost, GETDATE())",
                    new
                    {
                        Id = Guid.NewGuid(),
                        GoodsReceiptId = receiptId,
                        item.ProductId,
                        item.Quantity,
                        item.UnitPrice,
                        TotalPrice = lineValue,
                        LandedUnitCost = landedUnitCost
                    }, transaction);

                var product = await connection.QueryFirstAsync<Product>(
                    "SELECT * FROM Product WITH (UPDLOCK) WHERE Id = @Id", new { Id = item.ProductId }, transaction);
                var qtyBefore = product.CurrentStock;
                var qtyAfter = qtyBefore + item.Quantity;

                // Giá vốn bình quân gia quyền, tính theo giá đã gồm phí ship:
                // (tồn cũ × giá vốn cũ + số nhập × giá vốn thực) / tồn mới
                var newCostPrice = Math.Round((qtyBefore * product.CostPrice + item.Quantity * landedUnitCost) / qtyAfter, 2);

                await connection.ExecuteAsync(
                    "UPDATE Product SET CurrentStock = @QtyAfter, CostPrice = @NewCostPrice, UpdatedAt = GETDATE() WHERE Id = @Id",
                    new { QtyAfter = qtyAfter, NewCostPrice = newCostPrice, Id = product.Id }, transaction);

                await InsertStockTransactionAsync(connection, transaction, product.Id, "IMPORT", receiptCode, qtyBefore, item.Quantity, qtyAfter,
                    $"Nhập hàng theo phiếu {receiptCode}");
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

            // Gộp theo sản phẩm (1 phiếu có thể có nhiều dòng cùng sản phẩm)
            var perProduct = lines.GroupBy(l => l.ProductId).Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Value = g.Sum(x => x.Quantity * (x.LandedUnitCost ?? x.UnitPrice))
            }).ToList();

            var products = (await connection.QueryAsync<Product>(
                "SELECT * FROM Product WITH (UPDLOCK) WHERE Id IN @Ids", new { Ids = perProduct.Select(p => p.ProductId).ToList() }, transaction))
                .ToDictionary(p => p.Id);

            var shortages = perProduct
                .Where(p => !products.ContainsKey(p.ProductId) || products[p.ProductId].CurrentStock < p.Quantity)
                .Select(p => products.TryGetValue(p.ProductId, out var pr)
                    ? $"\"{pr.Name}\" chỉ còn {pr.CurrentStock}/{p.Quantity} chiếc"
                    : "một sản phẩm đã bị xóa")
                .ToList();
            if (shortages.Count > 0)
                return Fail($"Không hủy được vì hàng của phiếu đã bán bớt: {string.Join("; ", shortages)}. " +
                            "Hãy dùng phiếu nhập mới để điều chỉnh phần chênh lệch.");

            foreach (var p in perProduct)
            {
                var product = products[p.ProductId];
                var qtyBefore = product.CurrentStock;
                var qtyAfter = qtyBefore - p.Quantity;

                // Tính ngược bình quân gia quyền: bỏ phần giá trị của phiếu bị hủy ra khỏi tồn kho
                var newCostPrice = product.CostPrice;
                if (qtyAfter > 0)
                {
                    var remaining = (qtyBefore * product.CostPrice - p.Value) / qtyAfter;
                    if (remaining > 0) newCostPrice = Math.Round(remaining, 2);
                }

                await connection.ExecuteAsync(
                    "UPDATE Product SET CurrentStock = @QtyAfter, CostPrice = @NewCostPrice, UpdatedAt = GETDATE() WHERE Id = @Id",
                    new { QtyAfter = qtyAfter, NewCostPrice = newCostPrice, Id = product.Id }, transaction);

                await InsertStockTransactionAsync(connection, transaction, product.Id, "IMPORT_CANCEL", receipt.Code, qtyBefore, -p.Quantity, qtyAfter,
                    $"Hủy phiếu nhập {receipt.Code}" + (string.IsNullOrWhiteSpace(reason) ? "" : $": {reason.Trim()}"));
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

    private static Task InsertStockTransactionAsync(IDbConnection connection, IDbTransaction transaction, Guid productId,
        string type, string reference, int before, int change, int after, string note) =>
        connection.ExecuteAsync(@"
            INSERT INTO StockTransaction (Id, ProductId, TransactionType, ReferenceCode, QuantityBefore, QuantityChange, QuantityAfter, Note, CreatedAt)
            VALUES (NEWID(), @ProductId, @Type, @Reference, @Before, @Change, @After, @Note, GETDATE())",
            new { ProductId = productId, Type = type, Reference = reference, Before = before, Change = change, After = after, Note = note }, transaction);

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
            SELECT st.*, p.Name AS ProductName, p.Sku
            FROM StockTransaction st
            INNER JOIN Product p ON st.ProductId = p.Id
            WHERE {whereClause}
            ORDER BY st.CreatedAt DESC";

        var list = (await connection.QueryAsync<StockMovementReportDto>(sql, parameters)).ToList();
        return new RestData<List<StockMovementReportDto>> { IsSuccess = true, Data = list };
    }
}
