using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using SystemBase.BE.DTOs.Shared;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Hubs;
using SystemBase.BE.Infrastructure.Dapper;
using SystemBase.BE.Models.SquishyManagement;

namespace SystemBase.BE.Services.Squishy;

public interface IOrderService
{
    Task<RestData<List<OrderDto>>> GetAllAsync(string? status = null, string? keyword = null, string? orderType = null);
    Task<RestData<List<OrderDto>>> GetFulfillmentOrdersAsync(string? prepStatus = null);
    Task<RestData<OrderDto>> GetByIdAsync(Guid id);
    Task<RestData<OrderDto>> CreateOrderAsync(CreateOrderRequest request);
    Task<RestData<OrderDto>> UpdateStatusAsync(Guid id, UpdateOrderStatusRequest request);
    Task<RestData<OrderDto>> UpdateFulfillmentAsync(Guid id, UpdateFulfillmentRequest request);
    Task<RestData<OrderDto>> UpdateInfoAsync(Guid id, UpdateOrderInfoRequest request);
    Task<RestData<bool>> UploadPaymentProofAsync(Guid orderId, string imageUrl);
}

public class OrderService : IOrderService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPaymentService _paymentService;
    private readonly IHubContext<AppHub> _hubContext;

    public OrderService(IDbConnectionFactory connectionFactory, IPaymentService paymentService, IHubContext<AppHub> hubContext)
    {
        _connectionFactory = connectionFactory;
        _paymentService = paymentService;
        _hubContext = hubContext;
    }

    public async Task<RestData<List<OrderDto>>> GetAllAsync(string? status = null, string? keyword = null, string? orderType = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var whereClause = "IsDeleted = 0";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(status))
        {
            whereClause += " AND (OrderStatus = @Status OR PaymentStatus = @Status)";
            parameters.Add("Status", status);
        }

        if (!string.IsNullOrWhiteSpace(orderType))
        {
            whereClause += " AND OrderType = @OrderType";
            parameters.Add("OrderType", orderType);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            whereClause += " AND (OrderCode LIKE @Keyword OR CustomerName LIKE @Keyword OR CustomerPhone LIKE @Keyword OR TransferContent LIKE @Keyword OR TrackingCode LIKE @Keyword)";
            parameters.Add("Keyword", $"%{keyword}%");
        }

        var sql = $"SELECT * FROM [Order] WHERE {whereClause} ORDER BY CreatedAt DESC";
        var orders = (await connection.QueryAsync<OrderDto>(sql, parameters)).ToList();

        return new RestData<List<OrderDto>> { IsSuccess = true, Data = orders };
    }

    public async Task<RestData<List<OrderDto>>> GetFulfillmentOrdersAsync(string? prepStatus = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        // Chỉ đơn giao hàng (khách đặt trên web) mới cần đóng gói / giao shipper; đơn tại quầy đã giao tay ngay
        var whereClause = "IsDeleted = 0 AND OrderStatus <> 'CANCELLED' AND OrderType <> 'POS'";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(prepStatus))
        {
            whereClause += " AND PreparationStatus = @PrepStatus";
            parameters.Add("PrepStatus", prepStatus);
        }

        var sql = $"SELECT * FROM [Order] WHERE {whereClause} ORDER BY CreatedAt DESC";
        var orders = (await connection.QueryAsync<OrderDto>(sql, parameters)).ToList();

        // Load details for each order to show items in packing checklist
        if (orders.Any())
        {
            var orderIds = orders.Select(o => o.Id).ToList();
            var details = (await connection.QueryAsync<OrderItemDto>(@"
                SELECT d.*, COALESCE(v.ImageUrl, p.MainImageUrl) AS MainImageUrl
                FROM OrderDetail d
                LEFT JOIN Product p ON d.ProductId = p.Id
                LEFT JOIN ProductVariant v ON v.Id = d.VariantId
                WHERE d.OrderId IN @Ids", new { Ids = orderIds })).ToList();

            foreach (var o in orders)
            {
                o.Details = details.Where(d => d.OrderId == o.Id).ToList();
            }
        }

        return new RestData<List<OrderDto>> { IsSuccess = true, Data = orders };
    }

    public async Task<RestData<OrderDto>> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var order = await connection.QueryFirstOrDefaultAsync<OrderDto>(
            "SELECT * FROM [Order] WHERE Id = @Id AND IsDeleted = 0", new { Id = id });

        if (order == null)
            return new RestData<OrderDto> { IsSuccess = false, Message = "Không tìm thấy đơn hàng." };

        var details = (await connection.QueryAsync<OrderItemDto>(@"
            SELECT d.*, COALESCE(v.ImageUrl, p.MainImageUrl) AS MainImageUrl
            FROM OrderDetail d
            LEFT JOIN Product p ON d.ProductId = p.Id
                LEFT JOIN ProductVariant v ON v.Id = d.VariantId
            WHERE d.OrderId = @Id", new { Id = id })).ToList();

        order.Details = details;

        // Ảnh xác nhận thanh toán và ảnh bàn giao shipper lưu trong MediaImage
        var orderImages = (await connection.QueryAsync<MediaImage>(
            "SELECT * FROM MediaImage WHERE EntityId = @Id AND EntityType IN ('ORDER_PAYMENT', 'ORDER_SHIPPING') AND IsDeleted = 0 ORDER BY CreatedAt",
            new { Id = id })).ToList();

        List<MediaItemDto> ToDtos(string entityType) => orderImages.Where(img => img.EntityType == entityType).Select(img => new MediaItemDto
        {
            Id = img.Id,
            EntityId = img.EntityId,
            EntityType = img.EntityType,
            Url = img.Url,
            FileName = img.FileName,
            CreatedAt = img.CreatedAt
        }).ToList();
        order.PaymentProofImages = ToDtos("ORDER_PAYMENT");
        order.ShippingProofImages = ToDtos("ORDER_SHIPPING");

        // Sinh mã QR nếu đơn chưa thanh toán và dùng QR
        if (order.PaymentStatus != "PAID")
        {
            order.QrCodeUrl = await _paymentService.GenerateQrUrlAsync(order.OrderCode, order.TotalAmount, order.TransferContent);
        }

        return new RestData<OrderDto> { IsSuccess = true, Data = order };
    }

    public async Task<RestData<OrderDto>> CreateOrderAsync(CreateOrderRequest request)
    {
        if (request.Items == null || !request.Items.Any())
            return new RestData<OrderDto> { IsSuccess = false, Message = "Đơn hàng phải có ít nhất 1 sản phẩm." };

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var countToday = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM [Order] WHERE OrderCode LIKE @Prefix",
                new { Prefix = $"HD-{dateStr}-%" }, transaction);

            var orderCode = $"HD-{dateStr}-{(countToday + 1):D3}";
            // Mã chuyển khoản cố định 9 ký tự (SQS + 6 số ngẫu nhiên) và không trùng đơn nào,
            // để webhook SePay so khớp chính xác, không nhận nhầm mã này là phần đầu của mã khác
            string transferContent;
            do
            {
                var digits = global::System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000);
                transferContent = $"SQS{digits}";
            } while (await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM [Order] WHERE TransferContent = @Code", new { Code = transferContent }, transaction) > 0);

            decimal subTotal = 0;
            var orderId = Guid.NewGuid();
            var isCash = request.PaymentMethod == "CASH";

            var orderDetailsToInsert = new List<OrderDetail>();
            foreach (var item in request.Items)
            {
                var product = await connection.QueryFirstOrDefaultAsync<Product>(
                    "SELECT * FROM Product WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = item.ProductId }, transaction);

                if (product == null)
                    throw new Exception($"Sản phẩm không tồn tại (ID: {item.ProductId}).");
                if (item.Quantity <= 0)
                    throw new Exception($"Số lượng của \"{product.Name}\" phải lớn hơn 0.");

                // Sản phẩm có phân loại màu thì bắt buộc chọn màu đang bán
                var variantError = await Inventory.ValidateVariantAsync(connection, transaction, product.Id, item.VariantId, product.Name, requireActive: true);
                if (variantError != null) throw new Exception(variantError);
                var variantName = await Inventory.GetVariantNameAsync(connection, transaction, item.VariantId);
                var lineName = variantName == null ? product.Name : $"{product.Name} - {variantName}";

                var stock = await Inventory.GetStockAsync(connection, transaction, product.Id, item.VariantId);
                if (stock < item.Quantity)
                    throw new Exception($"\"{lineName}\" chỉ còn {stock} chiếc.");

                var itemTotal = item.Quantity * item.UnitPrice;
                subTotal += itemTotal;

                orderDetailsToInsert.Add(new OrderDetail
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = product.Id,
                    VariantId = item.VariantId,
                    ProductName = lineName,
                    VariantName = variantName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    CostPrice = await Inventory.GetCostAsync(connection, transaction, product.Id, item.VariantId),
                    TotalPrice = itemTotal,
                    CreatedAt = DateTime.Now
                });

                // Nếu là tiền mặt tại quầy: Trừ kho ngay lập tức
                if (isCash)
                {
                    await Inventory.ChangeAsync(connection, transaction, product.Id, item.VariantId, -item.Quantity, "EXPORT", orderCode,
                        $"Xuất bán tại quầy đơn {orderCode}");
                }
            }

            var discount = Math.Max(0, request.DiscountAmount);
            var isDelivery = !string.Equals(request.OrderType, "POS", StringComparison.OrdinalIgnoreCase);
            var shippingFee = isDelivery ? Math.Max(0, request.ShippingFee) : 0;
            var shippingPayer = NormalizePayer(request.ShippingPayer);
            if (shippingPayer == null)
                throw new Exception("Người trả phí ship không hợp lệ.");
            var totalAmount = CalcTotal(subTotal, discount, shippingFee, shippingPayer);

            var order = new Order
            {
                Id = orderId,
                OrderCode = orderCode,
                CustomerName = request.CustomerName ?? "Khách lẻ",
                CustomerPhone = request.CustomerPhone,
                OrderType = request.OrderType ?? "POS",
                SubTotal = subTotal,
                DiscountAmount = discount,
                TotalAmount = totalAmount,
                PaymentMethod = request.PaymentMethod,
                PaymentStatus = isCash ? "PAID" : "PENDING",
                OrderStatus = isCash ? "COMPLETED" : "PENDING",
                ShippingAddress = request.ShippingAddress,
                ShippingProvider = request.ShippingProvider,
                TrackingCode = request.TrackingCode,
                ShippingFee = shippingFee,
                ShippingPayer = shippingPayer,
                PreparationStatus = "PENDING",
                TransferContent = transferContent,
                PaidAt = isCash ? DateTime.Now : null,
                PaymentProofImageUrl = request.PaymentProofImageUrl,
                Note = request.Note,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDeleted = false
            };

            var insertOrderSql = @"
                INSERT INTO [Order] (Id, OrderCode, CustomerName, CustomerPhone, OrderType, SubTotal, DiscountAmount, TotalAmount, PaymentMethod, PaymentStatus, OrderStatus, ShippingAddress, ShippingProvider, TrackingCode, ShippingFee, ShippingPayer, PreparationStatus, TransferContent, PaidAt, PaymentProofImageUrl, Note, CreatedAt, UpdatedAt, IsDeleted)
                VALUES (@Id, @OrderCode, @CustomerName, @CustomerPhone, @OrderType, @SubTotal, @DiscountAmount, @TotalAmount, @PaymentMethod, @PaymentStatus, @OrderStatus, @ShippingAddress, @ShippingProvider, @TrackingCode, @ShippingFee, @ShippingPayer, @PreparationStatus, @TransferContent, @PaidAt, @PaymentProofImageUrl, @Note, @CreatedAt, @UpdatedAt, @IsDeleted)";
            await connection.ExecuteAsync(insertOrderSql, order, transaction);

            var insertDetailSql = @"
                INSERT INTO OrderDetail (Id, OrderId, ProductId, VariantId, ProductName, VariantName, Quantity, UnitPrice, CostPrice, TotalPrice, CreatedAt)
                VALUES (@Id, @OrderId, @ProductId, @VariantId, @ProductName, @VariantName, @Quantity, @UnitPrice, @CostPrice, @TotalPrice, @CreatedAt)";
            foreach (var detail in orderDetailsToInsert)
            {
                await connection.ExecuteAsync(insertDetailSql, detail, transaction);
            }

            if (!string.IsNullOrWhiteSpace(request.PaymentProofImageUrl))
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO MediaImage (Id, EntityId, EntityType, Url, FileName, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES (@Id, @EntityId, 'ORDER_PAYMENT', @Url, @FileName, 1, 1, GETDATE(), GETDATE(), 0)",
                    new
                    {
                        Id = Guid.NewGuid(),
                        EntityId = orderId,
                        Url = request.PaymentProofImageUrl,
                        FileName = "payment_proof.jpg"
                    }, transaction);
            }

            transaction.Commit();

            return await GetByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new RestData<OrderDto> { IsSuccess = false, Message = $"Lỗi khi tạo đơn hàng: {ex.Message}" };
        }
    }

    public async Task<RestData<OrderDto>> UpdateStatusAsync(Guid id, UpdateOrderStatusRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        var order = await connection.QueryFirstOrDefaultAsync<Order>(
            "SELECT * FROM [Order] WHERE Id = @Id AND IsDeleted = 0", new { Id = id });

        if (order == null)
            return new RestData<OrderDto> { IsSuccess = false, Message = "Không tìm thấy đơn hàng." };

        var newOrderStatus = request.Status;
        var newPaymentStatus = request.PaymentStatus ?? order.PaymentStatus;

        // Đơn đã có hàng đổi trả hoàn tất: kho đã được xử lý theo phiếu đổi trả, không cho đổi trạng thái để tránh cộng/trừ kho lặp
        if (order.OrderStatus == "RETURNED")
            return new RestData<OrderDto> { IsSuccess = false, Message = "Đơn đã hoàn hàng, không thể đổi trạng thái." };

        if (newOrderStatus == "CANCELLED")
        {
            var hasCompletedReturn = await connection.ExecuteScalarAsync<int>(@"
                SELECT CASE WHEN OBJECT_ID('dbo.ReturnRequest') IS NULL THEN 0 ELSE 1 END");
            if (hasCompletedReturn == 1)
            {
                var completedReturns = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(1) FROM ReturnRequest
                    WHERE OrderId = @Id AND IsDeleted = 0 AND Status = 'COMPLETED' AND RequestType <> 'REPAIR'", new { Id = id });
                if (completedReturns > 0)
                    return new RestData<OrderDto> { IsSuccess = false, Message = "Đơn đã có phiếu đổi trả hoàn tất. Hãy tạo phiếu đổi trả cho các món còn lại thay vì hủy đơn." };
            }
        }

        if (newOrderStatus == "COMPLETED" && order.OrderStatus != "COMPLETED")
        {
            var details = await connection.QueryAsync<OrderDetail>("SELECT * FROM OrderDetail WHERE OrderId = @Id", new { Id = id });
            foreach (var item in details)
            {
                await Inventory.ChangeAsync(connection, null, item.ProductId, item.VariantId, -item.Quantity, "EXPORT", order.OrderCode,
                    $"Xuất bán đơn hàng {order.OrderCode}");
            }
        }
        else if (newOrderStatus == "CANCELLED" && (order.OrderStatus == "COMPLETED" || order.PaymentStatus == "PAID"))
        {
            var details = await connection.QueryAsync<OrderDetail>("SELECT * FROM OrderDetail WHERE OrderId = @Id", new { Id = id });
            foreach (var item in details)
            {
                await Inventory.ChangeAsync(connection, null, item.ProductId, item.VariantId, item.Quantity, "ORDER_CANCEL", order.OrderCode,
                    $"Hoàn kho do hủy đơn {order.OrderCode}");
            }
        }

        DateTime? paidAt = (newPaymentStatus == "PAID" && order.PaidAt == null) ? DateTime.Now : order.PaidAt;

        await connection.ExecuteAsync(@"
            UPDATE [Order] 
            SET OrderStatus = @OrderStatus, PaymentStatus = @PaymentStatus, PaidAt = @PaidAt, UpdatedAt = GETDATE()
            WHERE Id = @Id",
            new { Id = id, OrderStatus = newOrderStatus, PaymentStatus = newPaymentStatus, PaidAt = paidAt });

        return await GetByIdAsync(id);
    }

    public async Task<RestData<OrderDto>> UpdateFulfillmentAsync(Guid id, UpdateFulfillmentRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        var orderType = await connection.ExecuteScalarAsync<string?>(
            "SELECT OrderType FROM [Order] WHERE Id = @Id AND IsDeleted = 0", new { Id = id });
        if (orderType == null)
            return FailOrder("Không tìm thấy đơn hàng.");
        if (orderType == "POS")
            return FailOrder("Đơn bán tại quầy đã giao tận tay khách, không cần đóng gói / giao shipper.");
        var sql = @"
            UPDATE [Order] 
            SET PreparationStatus = @PreparationStatus,
                PreparationNote = @PreparationNote,
                TrackingCode = @TrackingCode,
                ShippingProvider = @ShippingProvider,
                PackingChecklistJson = @PackingChecklistJson,
                UpdatedAt = GETDATE()
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            request.PreparationStatus,
            request.PreparationNote,
            request.TrackingCode,
            request.ShippingProvider,
            request.PackingChecklistJson
        });

        return await GetByIdAsync(id);
    }

    public async Task<RestData<bool>> UploadPaymentProofAsync(Guid orderId, string imageUrl)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            UPDATE [Order] SET PaymentProofImageUrl = @Url, UpdatedAt = GETDATE() WHERE Id = @Id",
            new { Id = orderId, Url = imageUrl });

        await connection.ExecuteAsync(@"
            INSERT INTO MediaImage (Id, EntityId, EntityType, Url, FileName, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
            VALUES (NEWID(), @EntityId, 'ORDER_PAYMENT', @Url, 'payment_proof.jpg', 1, 1, GETDATE(), GETDATE(), 0)",
            new { EntityId = orderId, Url = imageUrl });

        return new RestData<bool> { IsSuccess = true, Data = true, Message = "Đã cập nhật ảnh chứng minh thanh toán." };
    }

    /// <summary>
    /// Sửa thông tin đơn khi nhập sai. Tổng tiền khách trả = tiền hàng − giảm giá + (phí ship nếu người nhận trả).
    /// Đơn đã thanh toán thì không cho đổi các khoản làm thay đổi số tiền khách trả (tránh lệch với tiền đã nhận).
    /// </summary>
    public async Task<RestData<OrderDto>> UpdateInfoAsync(Guid id, UpdateOrderInfoRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        var order = await connection.QueryFirstOrDefaultAsync<Order>(
            "SELECT * FROM [Order] WHERE Id = @Id AND IsDeleted = 0", new { Id = id });
        if (order == null)
            return FailOrder("Không tìm thấy đơn hàng.");
        if (order.OrderStatus is "CANCELLED" or "RETURNED")
            return FailOrder("Đơn đã hủy / đã hoàn hàng nên không sửa được.");

        var payer = NormalizePayer(request.ShippingPayer);
        if (payer == null)
            return FailOrder("Người trả phí ship không hợp lệ.");
        if (request.ShippingFee < 0 || request.DiscountAmount < 0)
            return FailOrder("Phí ship và giảm giá không được âm.");
        if (request.DiscountAmount > order.SubTotal)
            return FailOrder("Giảm giá không được lớn hơn tiền hàng.");

        var name = string.IsNullOrWhiteSpace(request.CustomerName) ? (order.OrderType == "POS" ? "Khách lẻ" : null) : request.CustomerName.Trim();
        if (name == null)
            return FailOrder("Vui lòng nhập tên người nhận.");
        var phone = string.IsNullOrWhiteSpace(request.CustomerPhone) ? null : Regex.Replace(request.CustomerPhone, @"[\s.\-]", "");
        if (phone != null && !Regex.IsMatch(phone, @"^(0|\+84)\d{9}$"))
            return FailOrder("Số điện thoại chưa đúng (VD: 0912345678).");

        var isDelivery = order.OrderType != "POS";
        if (isDelivery && string.IsNullOrWhiteSpace(request.ShippingAddress))
            return FailOrder("Đơn giao hàng cần có địa chỉ nhận hàng.");
        var shippingFee = isDelivery ? request.ShippingFee : 0;
        var newTotal = CalcTotal(order.SubTotal, request.DiscountAmount, shippingFee, payer);

        if (order.PaymentStatus == "PAID" && newTotal != order.TotalAmount)
            return FailOrder($"Đơn đã thanh toán {Vnd(order.TotalAmount)}, thay đổi này làm tổng thành {Vnd(newTotal)}. " +
                             "Chỉ sửa được phí ship khi shop trả, hoặc thông tin người nhận / vận chuyển. Khoản chênh lệch hãy ghi ở Thu chi khác.");

        await connection.ExecuteAsync(@"
            UPDATE [Order]
            SET CustomerName = @CustomerName, CustomerPhone = @CustomerPhone, ShippingAddress = @ShippingAddress, Note = @Note,
                ShippingProvider = @ShippingProvider, TrackingCode = @TrackingCode, ShippingFee = @ShippingFee, ShippingPayer = @ShippingPayer,
                DiscountAmount = @DiscountAmount, TotalAmount = @TotalAmount, UpdatedAt = GETDATE()
            WHERE Id = @Id",
            new
            {
                Id = id,
                CustomerName = name,
                CustomerPhone = phone,
                ShippingAddress = isDelivery ? request.ShippingAddress!.Trim() : null,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                ShippingProvider = string.IsNullOrWhiteSpace(request.ShippingProvider) ? null : request.ShippingProvider.Trim(),
                TrackingCode = string.IsNullOrWhiteSpace(request.TrackingCode) ? null : request.TrackingCode.Trim().ToUpperInvariant(),
                ShippingFee = shippingFee,
                ShippingPayer = payer,
                request.DiscountAmount,
                TotalAmount = newTotal
            });

        var result = await GetByIdAsync(id);
        result.Message = "Đã cập nhật đơn hàng.";
        return result;
    }

    /// <summary>Tổng khách trả: phí ship chỉ cộng vào khi người nhận trả; shop trả thì là chi phí của shop.</summary>
    private static decimal CalcTotal(decimal subTotal, decimal discount, decimal shippingFee, string payer) =>
        Math.Max(0, subTotal - discount + (payer == "RECEIVER" ? shippingFee : 0));

    private static string? NormalizePayer(string? payer)
    {
        var p = string.IsNullOrWhiteSpace(payer) ? "RECEIVER" : payer.Trim().ToUpperInvariant();
        return p is "RECEIVER" or "SENDER" ? p : null;
    }

    private static string Vnd(decimal value) =>
        value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";

    private static RestData<OrderDto> FailOrder(string message) => new() { IsSuccess = false, Message = message };
}
