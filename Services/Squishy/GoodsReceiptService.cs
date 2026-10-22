using System;
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
            return new RestData<GoodsReceiptDto> { IsSuccess = false, Message = "Phiếu nhập phải có ít nhất 1 sản phẩm." };

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Sinh mã phiếu nhập PN-YYYYMMDD-XXXX
            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var countToday = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM GoodsReceipt WHERE Code LIKE @Prefix",
                new { Prefix = $"PN-{dateStr}-%" }, transaction);

            var receiptCode = $"PN-{dateStr}-{(countToday + 1):D3}";
            var receiptId = Guid.NewGuid();
            var totalAmount = dto.Items.Sum(x => x.Quantity * x.UnitPrice);

            var receipt = new GoodsReceipt
            {
                Id = receiptId,
                Code = receiptCode,
                SupplierId = dto.SupplierId,
                ReceiptDate = dto.ReceiptDate ?? DateTime.Now,
                TotalAmount = totalAmount,
                Status = "COMPLETED",
                Note = dto.Note,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDeleted = false
            };

            var insertReceiptSql = @"
                INSERT INTO GoodsReceipt (Id, Code, SupplierId, ReceiptDate, TotalAmount, Status, Note, CreatedAt, UpdatedAt, IsDeleted)
                VALUES (@Id, @Code, @SupplierId, @ReceiptDate, @TotalAmount, @Status, @Note, @CreatedAt, @UpdatedAt, @IsDeleted)";
            await connection.ExecuteAsync(insertReceiptSql, receipt, transaction);

            foreach (var item in dto.Items)
            {
                var detailId = Guid.NewGuid();
                var itemTotal = item.Quantity * item.UnitPrice;

                var insertDetailSql = @"
                    INSERT INTO GoodsReceiptDetail (Id, GoodsReceiptId, ProductId, Quantity, UnitPrice, TotalPrice, CreatedAt)
                    VALUES (@Id, @GoodsReceiptId, @ProductId, @Quantity, @UnitPrice, @TotalPrice, GETDATE())";
                await connection.ExecuteAsync(insertDetailSql, new
                {
                    Id = detailId,
                    GoodsReceiptId = receiptId,
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice,
                    TotalPrice = itemTotal
                }, transaction);

                // Lấy thông tin sản phẩm hiện tại để cập nhật kho và tính giá vốn bình quân
                var product = await connection.QueryFirstOrDefaultAsync<Product>(
                    "SELECT * FROM Product WHERE Id = @Id", new { Id = item.ProductId }, transaction);

                if (product != null)
                {
                    var qtyBefore = product.CurrentStock;
                    var qtyChange = item.Quantity;
                    var qtyAfter = qtyBefore + qtyChange;

                    // Tính giá vốn bình quân gia quyền: (tồn_cũ * giá_vốn_cũ + nhập_mới * giá_nhập) / tổng_tồn_mới
                    decimal newCostPrice = product.CostPrice;
                    if (qtyAfter > 0)
                    {
                        var totalVal = (qtyBefore * product.CostPrice) + (qtyChange * item.UnitPrice);
                        newCostPrice = Math.Round(totalVal / qtyAfter, 2);
                    }

                    // Cập nhật Product
                    await connection.ExecuteAsync(@"
                        UPDATE Product 
                        SET CurrentStock = @QtyAfter, CostPrice = @NewCostPrice, UpdatedAt = GETDATE()
                        WHERE Id = @Id",
                        new { QtyAfter = qtyAfter, NewCostPrice = newCostPrice, Id = product.Id }, transaction);

                    // Ghi thẻ kho StockTransaction (IMPORT)
                    var stockTransSql = @"
                        INSERT INTO StockTransaction (Id, ProductId, TransactionType, ReferenceCode, QuantityBefore, QuantityChange, QuantityAfter, Note, CreatedAt)
                        VALUES (@Id, @ProductId, 'IMPORT', @ReferenceCode, @QuantityBefore, @QuantityChange, @QuantityAfter, @Note, GETDATE())";
                    await connection.ExecuteAsync(stockTransSql, new
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        ReferenceCode = receiptCode,
                        QuantityBefore = qtyBefore,
                        QuantityChange = qtyChange,
                        QuantityAfter = qtyAfter,
                        Note = $"Nhập hàng theo phiếu {receiptCode}"
                    }, transaction);
                }
            }

            transaction.Commit();
            return await GetByIdAsync(receiptId);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new RestData<GoodsReceiptDto> { IsSuccess = false, Message = $"Lỗi khi tạo phiếu nhập: {ex.Message}" };
        }
    }

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
