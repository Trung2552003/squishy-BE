using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace SystemBase.BE.Services.Squishy;

/// <summary>
/// Mọi thay đổi tồn kho đi qua đây để trừ/cộng đúng màu (nếu sản phẩm có phân loại),
/// tự tính lại tồn kho + giá vốn tổng của sản phẩm và ghi thẻ kho.
/// </summary>
public static class Inventory
{
    public enum CostMode
    {
        /// <summary>Không đổi giá vốn (bán, trả hàng, đổi hàng...).</summary>
        None,
        /// <summary>Nhập hàng: tính bình quân gia quyền với giá vốn thực của lô nhập.</summary>
        Import,
        /// <summary>Hủy phiếu nhập: bỏ giá trị lô nhập ra khỏi bình quân.</summary>
        CancelImport
    }

    /// <summary>Sản phẩm có ít nhất 1 màu (chưa xóa) thì mọi giao dịch kho phải chỉ rõ màu.</summary>
    public static Task<bool> HasVariantsAsync(IDbConnection connection, IDbTransaction? transaction, Guid productId) =>
        connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM ProductVariant WHERE ProductId = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = productId }, transaction);

    /// <summary>
    /// Kiểm tra màu hợp lệ cho sản phẩm. Trả về lỗi (tiếng Việt) hoặc null nếu hợp lệ.
    /// Sản phẩm có màu thì bắt buộc chọn màu; sản phẩm không màu thì không được gửi màu.
    /// </summary>
    public static async Task<string?> ValidateVariantAsync(IDbConnection connection, IDbTransaction? transaction,
        Guid productId, Guid? variantId, string productName, bool requireActive)
    {
        var hasVariants = await HasVariantsAsync(connection, transaction, productId);
        if (!hasVariants)
            return variantId.HasValue ? $"\"{productName}\" không có phân loại màu." : null;
        if (!variantId.HasValue)
            return $"Vui lòng chọn màu cho \"{productName}\".";

        var active = await connection.QueryFirstOrDefaultAsync<bool?>(
            "SELECT IsActive FROM ProductVariant WHERE Id = @Id AND ProductId = @ProductId AND IsDeleted = 0",
            new { Id = variantId, ProductId = productId }, transaction);
        if (active == null) return $"Màu đã chọn của \"{productName}\" không còn tồn tại.";
        if (requireActive && active == false) return $"Màu đã chọn của \"{productName}\" đang ngừng bán.";
        return null;
    }

    public static Task<string?> GetVariantNameAsync(IDbConnection connection, IDbTransaction? transaction, Guid? variantId) =>
        variantId.HasValue
            ? connection.QueryFirstOrDefaultAsync<string?>("SELECT Name FROM ProductVariant WHERE Id = @Id", new { Id = variantId }, transaction)
            : Task.FromResult<string?>(null);

    /// <summary>Tồn kho hiện tại của sản phẩm hoặc của màu.</summary>
    public static Task<int> GetStockAsync(IDbConnection connection, IDbTransaction? transaction, Guid productId, Guid? variantId) =>
        variantId.HasValue
            ? connection.ExecuteScalarAsync<int>("SELECT ISNULL((SELECT CurrentStock FROM ProductVariant WHERE Id = @Id), 0)", new { Id = variantId }, transaction)
            : connection.ExecuteScalarAsync<int>("SELECT ISNULL((SELECT CurrentStock FROM Product WHERE Id = @Id), 0)", new { Id = productId }, transaction);

    /// <summary>Giá vốn hiện tại của sản phẩm hoặc của màu (dùng làm giá vốn khi bán).</summary>
    public static Task<decimal> GetCostAsync(IDbConnection connection, IDbTransaction? transaction, Guid productId, Guid? variantId) =>
        variantId.HasValue
            ? connection.ExecuteScalarAsync<decimal>("SELECT ISNULL((SELECT CostPrice FROM ProductVariant WHERE Id = @Id), 0)", new { Id = variantId }, transaction)
            : connection.ExecuteScalarAsync<decimal>("SELECT ISNULL((SELECT CostPrice FROM Product WHERE Id = @Id), 0)", new { Id = productId }, transaction);

    /// <summary>
    /// Cộng/trừ kho 1 dòng hàng và ghi thẻ kho. Tồn kho không xuống dưới 0.
    /// costValue: với Import là giá vốn thực 1 chiếc; với CancelImport là tổng giá trị lô bị hủy.
    /// </summary>
    public static async Task<(int Before, int After)> ChangeAsync(IDbConnection connection, IDbTransaction? transaction,
        Guid productId, Guid? variantId, int delta, string type, string? reference, string note,
        CostMode costMode = CostMode.None, decimal costValue = 0)
    {
        var table = variantId.HasValue ? "ProductVariant" : "Product";
        var rowId = variantId ?? productId;

        var row = await connection.QueryFirstAsync<(int Stock, decimal Cost)>(
            $"SELECT CurrentStock, CostPrice FROM {table} WITH (UPDLOCK) WHERE Id = @Id", new { Id = rowId }, transaction);
        var before = row.Stock;
        var after = Math.Max(0, before + delta);

        var newCost = row.Cost;
        if (costMode == CostMode.Import && after > 0)
            newCost = Math.Round((before * row.Cost + delta * costValue) / after, 2);
        else if (costMode == CostMode.CancelImport && after > 0)
        {
            var remaining = (before * row.Cost - costValue) / after;
            if (remaining > 0) newCost = Math.Round(remaining, 2);
        }

        await connection.ExecuteAsync(
            $"UPDATE {table} SET CurrentStock = @After, CostPrice = @Cost, UpdatedAt = GETDATE() WHERE Id = @Id",
            new { After = after, Cost = newCost, Id = rowId }, transaction);

        if (variantId.HasValue)
            await RecalcProductAsync(connection, transaction, productId);

        await connection.ExecuteAsync(@"
            INSERT INTO StockTransaction (Id, ProductId, VariantId, TransactionType, ReferenceCode, QuantityBefore, QuantityChange, QuantityAfter, Note, CreatedAt)
            VALUES (NEWID(), @ProductId, @VariantId, @Type, @Reference, @Before, @Change, @After, @Note, GETDATE())",
            new { ProductId = productId, VariantId = variantId, Type = type, Reference = reference, Before = before, Change = after - before, After = after, Note = note },
            transaction);

        return (before, after);
    }

    /// <summary>Ghi thẻ kho không làm đổi số lượng (VD nhận hàng lỗi khách trả).</summary>
    public static async Task LogAsync(IDbConnection connection, IDbTransaction? transaction,
        Guid productId, Guid? variantId, string type, string? reference, string note)
    {
        var stock = await GetStockAsync(connection, transaction, productId, variantId);
        await connection.ExecuteAsync(@"
            INSERT INTO StockTransaction (Id, ProductId, VariantId, TransactionType, ReferenceCode, QuantityBefore, QuantityChange, QuantityAfter, Note, CreatedAt)
            VALUES (NEWID(), @ProductId, @VariantId, @Type, @Reference, @Stock, 0, @Stock, @Note, GETDATE())",
            new { ProductId = productId, VariantId = variantId, Type = type, Reference = reference, Stock = stock, Note = note }, transaction);
    }

    /// <summary>Tồn kho sản phẩm = tổng các màu; giá vốn sản phẩm = bình quân theo tồn của các màu.</summary>
    public static Task RecalcProductAsync(IDbConnection connection, IDbTransaction? transaction, Guid productId) =>
        connection.ExecuteAsync(@"
            UPDATE p SET
                p.CurrentStock = v.Stock,
                p.CostPrice = CASE WHEN v.Stock > 0 THEN ROUND(v.Value / v.Stock, 2) ELSE p.CostPrice END,
                p.UpdatedAt = GETDATE()
            FROM Product p
            CROSS APPLY (
                SELECT ISNULL(SUM(CurrentStock), 0) AS Stock, ISNULL(SUM(CurrentStock * CostPrice), 0) AS Value, COUNT(1) AS Cnt
                FROM ProductVariant WHERE ProductId = p.Id AND IsDeleted = 0
            ) v
            WHERE p.Id = @Id AND v.Cnt > 0", new { Id = productId }, transaction);
}
