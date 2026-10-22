using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SystemBase.BE.DTOs.Shared;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Infrastructure.Dapper;
using SystemBase.BE.Models.SquishyManagement;

namespace SystemBase.BE.Services.Squishy;

public interface IReturnService
{
    Task<RestData<List<ReturnRequestDto>>> GetAllAsync(string? status = null, string? requestType = null, string? keyword = null);
    Task<RestData<ReturnRequestDto>> GetByIdAsync(Guid id);
    Task<RestData<ReturnableOrderDto>> GetReturnableOrderAsync(string orderCode);
    Task<RestData<ReturnRequestDto>> CreateAsync(CreateReturnRequest request);
    Task<RestData<ReturnRequestDto>> UpdateStatusAsync(Guid id, UpdateReturnStatusRequest request);
}

public class ReturnService : IReturnService
{
    private static readonly string[] RequestTypes = { "RETURN", "EXCHANGE", "REPAIR", "FAILED_DELIVERY" };
    private static readonly string[] ReasonCategories = { "DEFECTIVE", "WRONG_ITEM", "CHANGED_MIND", "DELIVERY_FAILED", "OTHER" };
    private static readonly string[] ItemConditions = { "GOOD", "DEFECTIVE" };

    // Trạng thái được phép chuyển tới từ mỗi trạng thái hiện tại
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["PENDING"] = new[] { "PROCESSING", "REPAIRING", "COMPLETED", "REJECTED" },
        ["PROCESSING"] = new[] { "REPAIRING", "COMPLETED", "REJECTED" },
        ["REPAIRING"] = new[] { "REPAIRED", "COMPLETED", "REJECTED" },
        ["REPAIRED"] = new[] { "COMPLETED" },
        ["COMPLETED"] = Array.Empty<string>(),
        ["REJECTED"] = Array.Empty<string>(),
    };

    private readonly IDbConnectionFactory _connectionFactory;

    public ReturnService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RestData<List<ReturnRequestDto>>> GetAllAsync(string? status = null, string? requestType = null, string? keyword = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var where = "r.IsDeleted = 0";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(status))
        {
            where += " AND r.Status = @Status";
            parameters.Add("Status", status);
        }
        if (!string.IsNullOrWhiteSpace(requestType))
        {
            where += " AND r.RequestType = @RequestType";
            parameters.Add("RequestType", requestType);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where += " AND (r.Code LIKE @Keyword OR r.OrderCode LIKE @Keyword OR r.CustomerName LIKE @Keyword OR r.CustomerPhone LIKE @Keyword)";
            parameters.Add("Keyword", $"%{keyword.Trim()}%");
        }

        var sql = $@"
            SELECT r.*,
                   ISNULL((SELECT SUM(i.Quantity) FROM ReturnRequestItem i WHERE i.ReturnRequestId = r.Id), 0) AS TotalQuantity
            FROM ReturnRequest r
            WHERE {where}
            ORDER BY r.CreatedAt DESC";

        var list = (await connection.QueryAsync<ReturnRequestDto>(sql, parameters)).ToList();
        return new RestData<List<ReturnRequestDto>> { IsSuccess = true, Data = list };
    }

    public async Task<RestData<ReturnRequestDto>> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dto = await connection.QueryFirstOrDefaultAsync<ReturnRequestDto>(
            "SELECT * FROM ReturnRequest WHERE Id = @Id AND IsDeleted = 0", new { Id = id });

        if (dto == null)
            return new RestData<ReturnRequestDto> { IsSuccess = false, Message = "Không tìm thấy phiếu đổi trả." };

        dto.Items = (await connection.QueryAsync<ReturnItemDto>(@"
            SELECT i.*, p.MainImageUrl
            FROM ReturnRequestItem i
            LEFT JOIN Product p ON i.ProductId = p.Id
            WHERE i.ReturnRequestId = @Id", new { Id = id })).ToList();
        dto.TotalQuantity = dto.Items.Sum(i => i.Quantity);

        var images = await connection.QueryAsync<MediaImage>(
            "SELECT * FROM MediaImage WHERE EntityId = @Id AND EntityType = 'RETURN_REQUEST' AND IsDeleted = 0 ORDER BY CreatedAt",
            new { Id = id });
        dto.Images = images.Select(img => new MediaItemDto
        {
            Id = img.Id,
            EntityId = img.EntityId,
            EntityType = img.EntityType,
            Url = img.Url,
            FileName = img.FileName,
            CreatedAt = img.CreatedAt
        }).ToList();

        return new RestData<ReturnRequestDto> { IsSuccess = true, Data = dto };
    }

    public async Task<RestData<ReturnableOrderDto>> GetReturnableOrderAsync(string orderCode)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
            return new RestData<ReturnableOrderDto> { IsSuccess = false, Message = "Vui lòng nhập mã đơn hàng." };

        using var connection = _connectionFactory.CreateConnection();
        var order = await connection.QueryFirstOrDefaultAsync<Order>(
            "SELECT * FROM [Order] WHERE OrderCode = @Code AND IsDeleted = 0", new { Code = orderCode.Trim() });

        if (order == null)
            return new RestData<ReturnableOrderDto> { IsSuccess = false, Message = $"Không tìm thấy đơn hàng {orderCode.Trim()}." };

        var items = await LoadReturnableItemsAsync(connection, null, order.Id, null);

        return new RestData<ReturnableOrderDto>
        {
            IsSuccess = true,
            Data = new ReturnableOrderDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                OrderType = order.OrderType,
                OrderStatus = order.OrderStatus,
                PaymentStatus = order.PaymentStatus,
                PaymentMethod = order.PaymentMethod,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                Items = items
            }
        };
    }

    public async Task<RestData<ReturnRequestDto>> CreateAsync(CreateReturnRequest request)
    {
        if (!RequestTypes.Contains(request.RequestType))
            return Fail("Loại yêu cầu không hợp lệ.");
        if (!ReasonCategories.Contains(request.ReasonCategory))
            return Fail("Lý do không hợp lệ.");
        if (request.Items == null || !request.Items.Any(i => i.Quantity > 0))
            return Fail("Vui lòng chọn ít nhất 1 sản phẩm.");
        if (request.Items.Any(i => !ItemConditions.Contains(i.ItemCondition)))
            return Fail("Tình trạng hàng không hợp lệ.");
        if (request.Items.Any(i => i.Quantity < 0) || request.RepairCost < 0
            || request.RefundAmount < 0 || request.ExtraCharge < 0)
            return Fail("Số lượng và số tiền không được âm.");

        var requestId = Guid.NewGuid();

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var order = await connection.QueryFirstOrDefaultAsync<Order>(
                "SELECT * FROM [Order] WHERE Id = @Id AND IsDeleted = 0", new { Id = request.OrderId }, transaction);
            if (order == null)
                return Fail("Không tìm thấy đơn hàng.");
            if (order.OrderStatus == "CANCELLED")
                return Fail("Đơn hàng đã bị hủy, không thể tạo phiếu đổi trả.");
            if (request.RequestType != "FAILED_DELIVERY" && order.OrderStatus != "COMPLETED" && order.OrderStatus != "RETURNED")
                return Fail("Chỉ đơn đã hoàn tất mới được đổi trả / bảo hành. Đơn chưa giao xong hãy dùng \"Hoàn hàng do giao thất bại\" hoặc hủy đơn.");

            var returnable = await LoadReturnableItemsAsync(connection, transaction, order.Id, null);
            var lines = request.Items.Where(i => i.Quantity > 0).ToList();
            var itemsToInsert = new List<ReturnRequestItem>();

            foreach (var line in lines)
            {
                var detail = returnable.FirstOrDefault(r => r.OrderDetailId == line.OrderDetailId);
                if (detail == null)
                    return Fail("Sản phẩm không thuộc đơn hàng này.");

                // Sửa/bảo hành không làm giảm số lượng có thể trả; các loại khác thì có
                var maxQty = request.RequestType == "REPAIR" ? detail.Quantity : detail.ReturnableQuantity;
                if (line.Quantity > maxQty)
                    return Fail($"\"{detail.ProductName}\" chỉ còn {maxQty} sản phẩm có thể {(request.RequestType == "REPAIR" ? "bảo hành" : "đổi trả")}.");

                var costPrice = await connection.ExecuteScalarAsync<decimal>(
                    "SELECT CostPrice FROM OrderDetail WHERE Id = @Id", new { Id = detail.OrderDetailId }, transaction);

                var item = new ReturnRequestItem
                {
                    ReturnRequestId = requestId,
                    OrderDetailId = detail.OrderDetailId,
                    ProductId = detail.ProductId,
                    ProductName = detail.ProductName,
                    Quantity = line.Quantity,
                    UnitPrice = detail.UnitPrice,
                    CostPrice = costPrice,
                    // Bảo hành: hàng vẫn là của khách, đánh dấu lỗi để không bao giờ nhập kho
                    ItemCondition = request.RequestType == "REPAIR" ? "DEFECTIVE" : line.ItemCondition,
                };

                if (request.RequestType == "EXCHANGE")
                {
                    var exchangeId = line.ExchangeProductId ?? detail.ProductId;
                    var exchangeProduct = await connection.QueryFirstOrDefaultAsync<Product>(
                        "SELECT * FROM Product WHERE Id = @Id AND IsDeleted = 0", new { Id = exchangeId }, transaction);
                    if (exchangeProduct == null)
                        return Fail("Sản phẩm đổi không tồn tại.");

                    item.ExchangeProductId = exchangeProduct.Id;
                    item.ExchangeProductName = exchangeProduct.Name;
                    item.ExchangeQuantity = line.ExchangeQuantity > 0 ? line.ExchangeQuantity : line.Quantity;
                    // Đổi cùng mẫu thì giữ giá khách đã mua, đổi mẫu khác thì theo giá bán hiện tại
                    item.ExchangeUnitPrice = exchangeProduct.Id == detail.ProductId ? detail.UnitPrice : exchangeProduct.SalePrice;
                    item.ExchangeCostPrice = exchangeProduct.CostPrice;
                }

                itemsToInsert.Add(item);
            }

            // Số tiền mặc định, nhân viên có thể sửa lại khi tạo hoặc khi hoàn tất
            var returnedValue = itemsToInsert.Sum(i => i.Quantity * i.UnitPrice);
            decimal refund = 0, extra = 0;
            switch (request.RequestType)
            {
                case "RETURN":
                    refund = returnedValue;
                    break;
                case "FAILED_DELIVERY":
                    refund = order.PaymentStatus == "PAID" ? returnedValue : 0;
                    break;
                case "EXCHANGE":
                    var diff = itemsToInsert.Sum(i => i.ExchangeQuantity * i.ExchangeUnitPrice) - returnedValue;
                    if (diff > 0) extra = diff; else refund = -diff;
                    break;
            }
            refund = request.RefundAmount ?? refund;
            extra = request.ExtraCharge ?? extra;

            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var countToday = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM ReturnRequest WHERE Code LIKE @Prefix", new { Prefix = $"DT-{dateStr}-%" }, transaction);

            var entity = new ReturnRequest
            {
                Id = requestId,
                Code = $"DT-{dateStr}-{(countToday + 1):D3}",
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                RequestType = request.RequestType,
                ReasonCategory = request.ReasonCategory,
                Reason = request.Reason?.Trim(),
                Status = "PENDING",
                RefundAmount = refund,
                RefundMethod = refund > 0 ? (request.RefundMethod ?? "CASH") : null,
                ExtraCharge = extra,
                RepairCost = request.RequestType == "REPAIR" ? request.RepairCost : 0,
            };

            await connection.ExecuteAsync(@"
                INSERT INTO ReturnRequest (Id, Code, OrderId, OrderCode, CustomerName, CustomerPhone, RequestType, ReasonCategory, Reason, Status,
                                           RefundAmount, RefundMethod, ExtraCharge, RepairCost, StockRestored, CreatedAt, UpdatedAt, IsDeleted)
                VALUES (@Id, @Code, @OrderId, @OrderCode, @CustomerName, @CustomerPhone, @RequestType, @ReasonCategory, @Reason, @Status,
                        @RefundAmount, @RefundMethod, @ExtraCharge, @RepairCost, 0, GETDATE(), GETDATE(), 0)",
                entity, transaction);

            await connection.ExecuteAsync(@"
                INSERT INTO ReturnRequestItem (Id, ReturnRequestId, OrderDetailId, ProductId, ProductName, Quantity, UnitPrice, CostPrice, ItemCondition,
                                               ExchangeProductId, ExchangeProductName, ExchangeQuantity, ExchangeUnitPrice, ExchangeCostPrice)
                VALUES (@Id, @ReturnRequestId, @OrderDetailId, @ProductId, @ProductName, @Quantity, @UnitPrice, @CostPrice, @ItemCondition,
                        @ExchangeProductId, @ExchangeProductName, @ExchangeQuantity, @ExchangeUnitPrice, @ExchangeCostPrice)",
                itemsToInsert, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return await GetByIdAsync(requestId);
    }

    public async Task<RestData<ReturnRequestDto>> UpdateStatusAsync(Guid id, UpdateReturnStatusRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var entity = await connection.QueryFirstOrDefaultAsync<ReturnRequest>(
                "SELECT * FROM ReturnRequest WITH (UPDLOCK) WHERE Id = @Id AND IsDeleted = 0", new { Id = id }, transaction);
            if (entity == null)
                return Fail("Không tìm thấy phiếu đổi trả.");

            var newStatus = request.Status;
            if (!Transitions.TryGetValue(entity.Status, out var allowed) || !allowed.Contains(newStatus))
                return Fail("Không thể chuyển phiếu sang trạng thái này.");
            if ((newStatus == "REPAIRING" || newStatus == "REPAIRED") && entity.RequestType != "REPAIR")
                return Fail("Chỉ phiếu sửa / bảo hành mới có bước sửa chữa.");
            if (entity.RequestType == "REPAIR" && newStatus == "COMPLETED" && entity.Status != "REPAIRED" && entity.Status != "REPAIRING")
                return Fail("Hãy chuyển phiếu sang \"Đang sửa\" trước khi hoàn tất.");
            if (request.RefundAmount < 0 || request.ExtraCharge < 0 || request.RepairCost < 0)
                return Fail("Số tiền không được âm.");

            // Cập nhật số tiền / tình trạng hàng nếu nhân viên chỉnh lại
            if (request.RefundAmount.HasValue) entity.RefundAmount = request.RefundAmount.Value;
            if (request.ExtraCharge.HasValue) entity.ExtraCharge = request.ExtraCharge.Value;
            if (request.RepairCost.HasValue && entity.RequestType == "REPAIR") entity.RepairCost = request.RepairCost.Value;
            if (!string.IsNullOrWhiteSpace(request.RefundMethod)) entity.RefundMethod = request.RefundMethod;
            if (entity.RefundAmount <= 0) entity.RefundMethod = null;
            else entity.RefundMethod ??= "CASH";
            if (request.ResolutionNote != null) entity.ResolutionNote = request.ResolutionNote.Trim();

            if (request.Items != null && entity.RequestType != "REPAIR")
            {
                foreach (var change in request.Items.Where(c => ItemConditions.Contains(c.ItemCondition)))
                {
                    await connection.ExecuteAsync(
                        "UPDATE ReturnRequestItem SET ItemCondition = @ItemCondition WHERE Id = @Id AND ReturnRequestId = @RequestId",
                        new { change.ItemCondition, change.Id, RequestId = id }, transaction);
                }
            }

            if (newStatus == "COMPLETED")
            {
                var error = await ApplyCompletionAsync(connection, transaction, entity);
                if (error != null)
                {
                    transaction.Rollback();
                    return Fail(error);
                }
                entity.CompletedAt = DateTime.Now;
            }

            entity.Status = newStatus;

            await connection.ExecuteAsync(@"
                UPDATE ReturnRequest
                SET Status = @Status, RefundAmount = @RefundAmount, RefundMethod = @RefundMethod, ExtraCharge = @ExtraCharge,
                    RepairCost = @RepairCost, StockRestored = @StockRestored, ResolutionNote = @ResolutionNote,
                    CompletedAt = @CompletedAt, UpdatedAt = GETDATE()
                WHERE Id = @Id", entity, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return await GetByIdAsync(id);
    }

    /// <summary>
    /// Áp dụng thay đổi kho và trạng thái đơn khi phiếu hoàn tất. Trả về thông báo lỗi nếu không thực hiện được.
    /// </summary>
    private async Task<string?> ApplyCompletionAsync(IDbConnection connection, IDbTransaction transaction, ReturnRequest entity)
    {
        if (entity.RequestType == "REPAIR")
            return null; // Sửa xong trả khách: không ảnh hưởng kho

        var order = await connection.QueryFirstAsync<Order>(
            "SELECT * FROM [Order] WHERE Id = @Id", new { Id = entity.OrderId }, transaction);
        var items = (await connection.QueryAsync<ReturnRequestItem>(
            "SELECT * FROM ReturnRequestItem WHERE ReturnRequestId = @Id", new { Id = entity.Id }, transaction)).ToList();

        // Kiểm tra lại số lượng có thể trả (phòng trường hợp có phiếu khác hoàn tất trước)
        var returnable = await LoadReturnableItemsAsync(connection, transaction, entity.OrderId, entity.Id);
        foreach (var item in items)
        {
            var r = returnable.FirstOrDefault(x => x.OrderDetailId == item.OrderDetailId);
            if (r == null || item.Quantity > r.ReturnableQuantity)
                return $"\"{item.ProductName}\" vượt quá số lượng còn có thể đổi trả.";
        }

        // Kho chỉ bị trừ khi đơn đã hoàn tất (thanh toán tiền mặt / đã xác nhận thanh toán)
        var stockWasDeducted = order.OrderStatus == "COMPLETED" || order.OrderStatus == "RETURNED";
        var reference = entity.Code;

        foreach (var item in items)
        {
            var product = await connection.QueryFirstOrDefaultAsync<Product>(
                "SELECT * FROM Product WITH (UPDLOCK) WHERE Id = @Id", new { Id = item.ProductId }, transaction);
            if (product == null) continue;

            if (item.ItemCondition == "GOOD" && stockWasDeducted)
            {
                await ChangeStockAsync(connection, transaction, product, item.Quantity, "RETURN_IN", reference,
                    $"Nhập lại kho hàng khách trả (đơn {order.OrderCode})");
                entity.StockRestored = true;
            }
            else if (item.ItemCondition == "DEFECTIVE" && stockWasDeducted)
            {
                // Hàng lỗi không quay lại kho bán, chỉ ghi nhận vào thẻ kho để theo dõi
                await connection.ExecuteAsync(@"
                    INSERT INTO StockTransaction (Id, ProductId, TransactionType, ReferenceCode, QuantityBefore, QuantityChange, QuantityAfter, Note, CreatedAt)
                    VALUES (NEWID(), @ProductId, 'DEFECTIVE', @ReferenceCode, @Qty, 0, @Qty, @Note, GETDATE())",
                    new
                    {
                        ProductId = product.Id,
                        ReferenceCode = reference,
                        Qty = product.CurrentStock,
                        Note = $"Nhận {item.Quantity} hàng lỗi khách trả (đơn {order.OrderCode}) - không nhập lại kho"
                    }, transaction);
            }

            if (entity.RequestType == "EXCHANGE" && item.ExchangeProductId.HasValue && item.ExchangeQuantity > 0)
            {
                var exchangeProduct = item.ExchangeProductId == product.Id
                    ? await connection.QueryFirstAsync<Product>("SELECT * FROM Product WHERE Id = @Id", new { Id = product.Id }, transaction)
                    : await connection.QueryFirstOrDefaultAsync<Product>(
                        "SELECT * FROM Product WITH (UPDLOCK) WHERE Id = @Id", new { Id = item.ExchangeProductId }, transaction);
                if (exchangeProduct == null)
                    return $"Sản phẩm đổi \"{item.ExchangeProductName}\" không còn tồn tại.";
                if (exchangeProduct.CurrentStock < item.ExchangeQuantity)
                    return $"Không đủ hàng để đổi: \"{exchangeProduct.Name}\" chỉ còn {exchangeProduct.CurrentStock} sản phẩm.";

                await ChangeStockAsync(connection, transaction, exchangeProduct, -item.ExchangeQuantity, "EXCHANGE_OUT", reference,
                    $"Xuất hàng đổi cho khách (đơn {order.OrderCode})");
            }
        }

        // Cập nhật trạng thái đơn
        if (entity.RequestType == "FAILED_DELIVERY" && !stockWasDeducted)
        {
            // Đơn chưa hoàn tất (thường là COD) bị hoàn về: hủy đơn, kho chưa từng bị trừ nên không cộng lại
            await connection.ExecuteAsync(
                "UPDATE [Order] SET OrderStatus = 'CANCELLED', UpdatedAt = GETDATE() WHERE Id = @Id", new { Id = order.Id }, transaction);
        }
        else
        {
            // Chỉ tính các phiếu đã hoàn tất: hết món để trả thì đơn chuyển sang "Đã hoàn hàng"
            var orderedQty = returnable.Sum(r => r.Quantity);
            var completedReturnedQty = await connection.ExecuteScalarAsync<int>(@"
                SELECT ISNULL(SUM(ri.Quantity), 0)
                FROM ReturnRequestItem ri
                JOIN ReturnRequest r ON r.Id = ri.ReturnRequestId
                WHERE r.OrderId = @OrderId AND r.IsDeleted = 0 AND r.Status = 'COMPLETED'
                  AND r.RequestType <> 'REPAIR' AND r.Id <> @Id", new { OrderId = order.Id, entity.Id }, transaction);
            if (completedReturnedQty + items.Sum(i => i.Quantity) >= orderedQty)
            {
                await connection.ExecuteAsync(
                    "UPDATE [Order] SET OrderStatus = 'RETURNED', UpdatedAt = GETDATE() WHERE Id = @Id", new { Id = order.Id }, transaction);
            }
        }

        return null;
    }

    private static async Task ChangeStockAsync(IDbConnection connection, IDbTransaction transaction, Product product, int change,
        string type, string reference, string note)
    {
        var before = product.CurrentStock;
        var after = Math.Max(0, before + change);
        await connection.ExecuteAsync("UPDATE Product SET CurrentStock = @After, UpdatedAt = GETDATE() WHERE Id = @Id",
            new { After = after, product.Id }, transaction);
        await connection.ExecuteAsync(@"
            INSERT INTO StockTransaction (Id, ProductId, TransactionType, ReferenceCode, QuantityBefore, QuantityChange, QuantityAfter, Note, CreatedAt)
            VALUES (NEWID(), @ProductId, @Type, @ReferenceCode, @Before, @Change, @After, @Note, GETDATE())",
            new { ProductId = product.Id, Type = type, ReferenceCode = reference, Before = before, Change = change, After = after, Note = note },
            transaction);
        product.CurrentStock = after;
    }

    /// <summary>
    /// Các món trong đơn kèm số lượng đã đổi trả (phiếu chưa bị từ chối, không tính bảo hành).
    /// </summary>
    private static async Task<List<ReturnableOrderItemDto>> LoadReturnableItemsAsync(IDbConnection connection, IDbTransaction? transaction,
        Guid orderId, Guid? excludeRequestId)
    {
        var items = (await connection.QueryAsync<ReturnableOrderItemDto>(@"
            SELECT d.Id AS OrderDetailId, d.ProductId, d.ProductName, p.MainImageUrl, d.Quantity, d.UnitPrice,
                   ISNULL((
                       SELECT SUM(ri.Quantity)
                       FROM ReturnRequestItem ri
                       JOIN ReturnRequest r ON r.Id = ri.ReturnRequestId
                       WHERE ri.OrderDetailId = d.Id AND r.IsDeleted = 0 AND r.Status <> 'REJECTED'
                         AND r.RequestType <> 'REPAIR' AND (@ExcludeId IS NULL OR r.Id <> @ExcludeId)
                   ), 0) AS ReturnedQuantity
            FROM OrderDetail d
            LEFT JOIN Product p ON d.ProductId = p.Id
            WHERE d.OrderId = @OrderId
            ORDER BY d.CreatedAt", new { OrderId = orderId, ExcludeId = excludeRequestId }, transaction)).ToList();

        foreach (var item in items)
            item.ReturnableQuantity = Math.Max(0, item.Quantity - item.ReturnedQuantity);

        return items;
    }

    private static RestData<ReturnRequestDto> Fail(string message) => new() { IsSuccess = false, Message = message };
}
