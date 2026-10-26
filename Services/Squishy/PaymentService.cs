using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using SystemBase.BE.DTOs.Shared;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Hubs;
using SystemBase.BE.Infrastructure.Dapper;
using SystemBase.BE.Models.SquishyManagement;

namespace SystemBase.BE.Services.Squishy;

public interface IPaymentService
{
    Task<RestData<PaymentSettingDto>> GetSettingAsync();
    Task<RestData<PaymentSettingDto>> UpdateSettingAsync(UpdatePaymentSettingDto dto);
    Task<string> GenerateQrUrlAsync(string orderCode, decimal amount, string? transferContent = null);
    Task<WebhookResult> ProcessSePayWebhookAsync(string rawBody, IHeaderDictionary headers);
    Task<RestData<List<PaymentWebhookLogDto>>> GetRecentWebhookLogsAsync(int take = 20);
}

public class PaymentService : IPaymentService
{
    // SePay cho phép lệch giờ tối đa 5 phút giữa X-SePay-Timestamp và giờ máy chủ (chống gửi lại gói cũ)
    private const int MaxTimestampSkewSeconds = 300;
    private static readonly Regex TransferCodeRegex = new(@"SQS\d+", RegexOptions.IgnoreCase);
    private static readonly Regex OrderCodeRegex = new(@"HD-?(\d{8})-?(\d{3})", RegexOptions.IgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IHubContext<AppHub> _hubContext;

    public PaymentService(IDbConnectionFactory connectionFactory, IHubContext<AppHub> hubContext)
    {
        _connectionFactory = connectionFactory;
        _hubContext = hubContext;
    }

    // ===================================================================================
    // CÀI ĐẶT
    // ===================================================================================

    private async Task<PaymentSetting?> LoadSettingAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<PaymentSetting>("SELECT TOP 1 * FROM PaymentSetting ORDER BY UpdatedAt DESC");
    }

    public async Task<RestData<PaymentSettingDto>> GetSettingAsync()
    {
        var s = await LoadSettingAsync();
        var dto = s == null
            // Chưa cấu hình: để trống thay vì số tài khoản mẫu, tránh khách chuyển nhầm vào tài khoản lạ
            ? new PaymentSettingDto { BankCode = "MB", QrTemplate = "compact", IsActive = true }
            : new PaymentSettingDto
            {
                Id = s.Id,
                BankCode = s.BankCode,
                AccountNumber = s.AccountNumber,
                AccountName = s.AccountName,
                QrTemplate = s.QrTemplate,
                IsActive = s.IsActive,
                HasApiKey = !string.IsNullOrEmpty(s.SePayApiKey),
                ApiKeyHint = Hint(s.SePayApiKey),
                HasWebhookSecret = !string.IsNullOrEmpty(s.SePayWebhookSecret),
                WebhookSecretHint = Hint(s.SePayWebhookSecret)
            };
        return new RestData<PaymentSettingDto> { IsSuccess = true, Data = dto };
    }

    public async Task<RestData<PaymentSettingDto>> UpdateSettingAsync(UpdatePaymentSettingDto dto)
    {
        var accountNumber = (dto.AccountNumber ?? "").Trim().ToUpperInvariant();
        var accountName = (dto.AccountName ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(dto.BankCode) || accountNumber.Length == 0 || accountName.Length == 0)
            return new RestData<PaymentSettingDto> { IsSuccess = false, Message = "Vui lòng nhập đủ ngân hàng, số tài khoản và tên chủ tài khoản." };
        if (!Regex.IsMatch(accountNumber, "^[A-Z0-9]{4,30}$"))
            return new RestData<PaymentSettingDto> { IsSuccess = false, Message = "Số tài khoản chỉ gồm chữ và số (tài khoản ảo của SePay có dạng VQR...)." };

        using var connection = _connectionFactory.CreateConnection();
        var existing = await connection.QueryFirstOrDefaultAsync<PaymentSetting>("SELECT TOP 1 * FROM PaymentSetting ORDER BY UpdatedAt DESC");

        // null = giữ khóa cũ (trang quản trị không bao giờ nhận lại khóa thật nên không thể gửi lại)
        var apiKey = dto.SePayApiKey == null ? existing?.SePayApiKey : NullIfBlank(dto.SePayApiKey);
        var secret = dto.SePayWebhookSecret == null ? existing?.SePayWebhookSecret : NullIfBlank(dto.SePayWebhookSecret);
        var args = new
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            BankCode = dto.BankCode.Trim().ToUpperInvariant(),
            AccountNumber = accountNumber,
            AccountName = accountName,
            SePayApiKey = apiKey,
            SePayWebhookSecret = secret,
            QrTemplate = string.IsNullOrWhiteSpace(dto.QrTemplate) ? "compact" : dto.QrTemplate
        };

        if (existing == null)
        {
            await connection.ExecuteAsync(@"
                INSERT INTO PaymentSetting (Id, BankCode, AccountNumber, AccountName, SePayApiKey, SePayWebhookSecret, QrTemplate, IsActive, UpdatedAt)
                VALUES (@Id, @BankCode, @AccountNumber, @AccountName, @SePayApiKey, @SePayWebhookSecret, @QrTemplate, 1, GETDATE())", args);
        }
        else
        {
            await connection.ExecuteAsync(@"
                UPDATE PaymentSetting
                SET BankCode = @BankCode, AccountNumber = @AccountNumber, AccountName = @AccountName,
                    SePayApiKey = @SePayApiKey, SePayWebhookSecret = @SePayWebhookSecret, QrTemplate = @QrTemplate, UpdatedAt = GETDATE()
                WHERE Id = @Id", args);
        }

        var result = await GetSettingAsync();
        result.Message = "Đã lưu cài đặt thanh toán.";
        return result;
    }

    public async Task<string> GenerateQrUrlAsync(string orderCode, decimal amount, string? transferContent = null)
    {
        var setting = await LoadSettingAsync();
        if (setting == null || string.IsNullOrWhiteSpace(setting.AccountNumber))
            return string.Empty; // Chưa cấu hình tài khoản nhận tiền thì không sinh QR

        var content = Uri.EscapeDataString(transferContent ?? orderCode);
        var roundedAmount = (long)Math.Round(amount, 0);
        var acc = Uri.EscapeDataString(setting.AccountNumber);
        var bank = Uri.EscapeDataString(setting.BankCode);
        var template = Uri.EscapeDataString(setting.QrTemplate ?? "compact");
        return $"https://qr.sepay.vn/img?acc={acc}&bank={bank}&amount={roundedAmount}&des={content}&template={template}";
    }

    public async Task<RestData<List<PaymentWebhookLogDto>>> GetRecentWebhookLogsAsync(int take = 20)
    {
        using var connection = _connectionFactory.CreateConnection();
        var logs = (await connection.QueryAsync<PaymentWebhookLogDto>(@"
            SELECT TOP (@Take) l.Id, l.CreatedAt, l.Gateway, l.TransactionDate, l.AmountIn, l.TransactionContent,
                   l.TransferType, l.Status, l.Message, o.OrderCode
            FROM PaymentWebhookLog l
            LEFT JOIN [Order] o ON o.Id = l.OrderId
            ORDER BY l.CreatedAt DESC", new { Take = Math.Clamp(take, 1, 100) })).ToList();
        return new RestData<List<PaymentWebhookLogDto>> { IsSuccess = true, Data = logs };
    }

    // ===================================================================================
    // WEBHOOK SEPAY
    // ===================================================================================

    public async Task<WebhookResult> ProcessSePayWebhookAsync(string rawBody, IHeaderDictionary headers)
    {
        var setting = await LoadSettingAsync();
        using var connection = _connectionFactory.CreateConnection();

        // 1) Xác thực: chỉ tin dữ liệu thật sự do SePay gửi
        var authError = VerifySignature(setting, rawBody, headers);
        if (authError != null)
        {
            await InsertLogAsync(connection, null, rawBody, "REJECTED", authError, null);
            return new WebhookResult { Authorized = false, Status = "REJECTED", Message = authError };
        }

        SePayWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SePayWebhookPayload>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            payload = null;
        }
        if (payload == null)
        {
            await InsertLogAsync(connection, null, rawBody, "IGNORED", "Dữ liệu không phải JSON hợp lệ.", null);
            return new WebhookResult { Status = "IGNORED", Message = "Dữ liệu không hợp lệ." };
        }

        // 2) SePay có thể gửi lại cùng một giao dịch (khi lần trước không nhận được phản hồi): bỏ qua lần sau
        if (payload.id.HasValue)
        {
            var seen = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PaymentWebhookLog WHERE SePayTransactionId = @Id AND ISNULL(Status, '') NOT IN ('REJECTED', 'DUPLICATE')",
                new { Id = payload.id });
            if (seen > 0)
            {
                await InsertLogAsync(connection, payload, rawBody, "DUPLICATE", "Giao dịch đã nhận trước đó, bỏ qua.", null);
                return new WebhookResult { Status = "DUPLICATE", Message = "Giao dịch đã được xử lý trước đó." };
            }
        }

        // 3) Chỉ xử lý tiền VÀO đúng tài khoản của shop
        if (!string.Equals(payload.transferType, "in", StringComparison.OrdinalIgnoreCase))
            return await FinishAsync(connection, payload, rawBody, "IGNORED", "Giao dịch tiền ra, không xử lý.", null);
        if ((payload.transferAmount ?? 0) <= 0 || string.IsNullOrWhiteSpace(payload.content) && string.IsNullOrWhiteSpace(payload.code))
            return await FinishAsync(connection, payload, rawBody, "IGNORED", "Thiếu số tiền hoặc nội dung chuyển khoản.", null);
        if (setting != null && !string.IsNullOrWhiteSpace(setting.AccountNumber) && !IsOwnAccount(setting.AccountNumber, payload))
            return await FinishAsync(connection, payload, rawBody, "IGNORED",
                $"Tài khoản nhận ({payload.accountNumber}{(string.IsNullOrEmpty(payload.subAccount) ? "" : " / " + payload.subAccount)}) không khớp tài khoản đã cài đặt.", null);

        // 4) Tìm đơn theo mã chuyển khoản (SQS...) hoặc mã đơn (HD-yyyyMMdd-###) — so khớp chính xác, không dùng LIKE
        var text = $"{payload.code} {payload.content}".ToUpperInvariant();
        var transferCodes = TransferCodeRegex.Matches(text).Select(m => m.Value).Distinct().ToList();
        var orderCodes = OrderCodeRegex.Matches(text).Select(m => $"HD-{m.Groups[1].Value}-{m.Groups[2].Value}").Distinct().ToList();
        if (transferCodes.Count == 0 && orderCodes.Count == 0)
            return await FinishAsync(connection, payload, rawBody, "UNMATCHED", "Nội dung không có mã đơn hàng.", null);

        var order = await connection.QueryFirstOrDefaultAsync<Order>(@"
            SELECT TOP 1 * FROM [Order]
            WHERE IsDeleted = 0
              AND (UPPER(TransferContent) IN @TransferCodes OR OrderCode IN @OrderCodes)
            ORDER BY CreatedAt DESC",
            new { TransferCodes = transferCodes.DefaultIfEmpty("-").ToList(), OrderCodes = orderCodes.DefaultIfEmpty("-").ToList() });

        if (order == null)
            return await FinishAsync(connection, payload, rawBody, "UNMATCHED", $"Không tìm thấy đơn có mã {string.Join(", ", transferCodes.Concat(orderCodes))}.", null);
        if (order.PaymentStatus == "PAID")
            return await FinishAsync(connection, payload, rawBody, "UNMATCHED", $"Đơn {order.OrderCode} đã thanh toán trước đó — kiểm tra khách có chuyển 2 lần không.", order.Id);
        if (order.OrderStatus is "CANCELLED" or "RETURNED")
            return await FinishAsync(connection, payload, rawBody, "UNMATCHED", $"Đơn {order.OrderCode} đã hủy/hoàn — cần hoàn tiền cho khách thủ công.", order.Id);
        if ((payload.transferAmount ?? 0) < order.TotalAmount)
            return await FinishAsync(connection, payload, rawBody, "UNMATCHED",
                $"Khách chuyển thiếu cho đơn {order.OrderCode}: nhận {Money(payload.transferAmount)} / cần {Money(order.TotalAmount)}.", order.Id);

        // 5) Ghi nhận đã thanh toán + trừ kho trong cùng một giao dịch DB
        connection.Open();
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                // Điều kiện PaymentStatus <> 'PAID' chặn trường hợp 2 webhook đến cùng lúc làm trừ kho 2 lần
                var updated = await connection.ExecuteAsync(@"
                    UPDATE [Order]
                    SET PaymentStatus = 'PAID', OrderStatus = 'COMPLETED', PaidAt = GETDATE(), UpdatedAt = GETDATE()
                    WHERE Id = @Id AND PaymentStatus <> 'PAID'", new { order.Id }, transaction);
                if (updated == 0)
                {
                    transaction.Rollback();
                    return await FinishAsync(connection, payload, rawBody, "DUPLICATE", $"Đơn {order.OrderCode} vừa được xác nhận bởi yêu cầu khác.", order.Id);
                }

                var details = await connection.QueryAsync<OrderDetail>(
                    "SELECT * FROM OrderDetail WHERE OrderId = @OrderId", new { OrderId = order.Id }, transaction);
                foreach (var item in details)
                {
                    await Inventory.ChangeAsync(connection, transaction, item.ProductId, item.VariantId, -item.Quantity, "EXPORT", order.OrderCode,
                        $"Xuất bán đơn hàng {order.OrderCode} qua SePay");
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        var extra = (payload.transferAmount ?? 0) > order.TotalAmount ? $" (khách chuyển dư {Money(payload.transferAmount - order.TotalAmount)})" : "";
        var result = await FinishAsync(connection, payload, rawBody, "MATCHED", $"Đã khớp đơn {order.OrderCode}{extra}.", order.Id);

        // Báo realtime cho POS và màn hình quản trị
        await _hubContext.Clients.All.SendAsync("OrderPaid", new
        {
            OrderId = order.Id,
            OrderCode = order.OrderCode,
            TransferContent = order.TransferContent,
            TotalAmount = order.TotalAmount,
            PaidAt = DateTime.Now,
            CustomerName = order.CustomerName
        });
        return result;
    }

    /// <summary>Trả về null nếu hợp lệ, ngược lại là lý do từ chối. Ưu tiên HMAC-SHA256 nếu đã nhập Secret Key.</summary>
    private static string? VerifySignature(PaymentSetting? setting, string rawBody, IHeaderDictionary headers)
    {
        if (!string.IsNullOrEmpty(setting?.SePayWebhookSecret))
        {
            var signature = headers["X-SePay-Signature"].ToString();
            var timestampText = headers["X-SePay-Timestamp"].ToString();
            if (string.IsNullOrEmpty(signature) || !long.TryParse(timestampText, out var timestamp))
                return "Thiếu chữ ký HMAC (X-SePay-Signature / X-SePay-Timestamp).";
            if (Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp) > MaxTimestampSkewSeconds)
                return "Thời gian ký lệch quá 5 phút (kiểm tra đồng hồ máy chủ).";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(setting.SePayWebhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"));
            var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
            return FixedEquals(expected, signature.Trim().ToLowerInvariant()) ? null : "Chữ ký HMAC không đúng (kiểm tra Secret Key).";
        }

        if (!string.IsNullOrEmpty(setting?.SePayApiKey))
        {
            var auth = headers.Authorization.ToString();
            const string prefix = "Apikey ";
            if (!auth.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return "Thiếu header Authorization: Apikey ...";
            return FixedEquals(setting.SePayApiKey, auth[prefix.Length..].Trim()) ? null : "API Key không đúng.";
        }

        return "Chưa cài đặt Secret Key / API Key cho webhook nên từ chối để tránh giả mạo.";
    }

    private static bool IsOwnAccount(string configured, SePayWebhookPayload payload)
    {
        static string Norm(string? v) => (v ?? "").Replace(" ", "").ToUpperInvariant();
        var acc = Norm(configured);
        return acc == Norm(payload.accountNumber) || acc == Norm(payload.subAccount);
    }

    private static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private async Task<WebhookResult> FinishAsync(IDbConnection connection, SePayWebhookPayload payload, string rawBody, string status, string message, Guid? orderId)
    {
        await InsertLogAsync(connection, payload, rawBody, status, message, orderId);
        return new WebhookResult { Status = status, Message = message };
    }

    private static Task InsertLogAsync(IDbConnection connection, SePayWebhookPayload? payload, string rawBody, string status, string message, Guid? orderId) =>
        connection.ExecuteAsync(@"
            INSERT INTO PaymentWebhookLog (Id, Gateway, TransactionDate, AccountNumber, AmountIn, TransactionContent, ReferenceNumber, RawPayload, OrderId, CreatedAt,
                                           SePayTransactionId, TransferType, Status, Message)
            VALUES (@Id, @Gateway, @TransactionDate, @AccountNumber, @AmountIn, @TransactionContent, @ReferenceNumber, @RawPayload, @OrderId, GETDATE(),
                    @SePayTransactionId, @TransferType, @Status, @Message)",
            new
            {
                Id = Guid.NewGuid(),
                Gateway = payload?.gateway,
                TransactionDate = payload?.transactionDate,
                AccountNumber = payload?.accountNumber,
                AmountIn = payload?.transferAmount,
                TransactionContent = payload?.content,
                ReferenceNumber = payload?.referenceCode,
                RawPayload = rawBody.Length > 4000 ? rawBody[..4000] : rawBody,
                OrderId = orderId,
                SePayTransactionId = payload?.id,
                TransferType = payload?.transferType,
                Status = status,
                Message = message.Length > 500 ? message[..500] : message
            });

    private static string? Hint(string? secret) =>
        string.IsNullOrEmpty(secret) ? null : "••••" + (secret.Length > 4 ? secret[^4..] : "");

    private static string Money(decimal? value) =>
        (value ?? 0).ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
