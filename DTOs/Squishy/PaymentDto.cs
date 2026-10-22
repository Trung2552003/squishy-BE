using System;

namespace SystemBase.BE.DTOs.Squishy;

/// <summary>Cài đặt trả về cho trang quản trị: không bao giờ trả khóa bí mật, chỉ cho biết đã nhập và 4 ký tự cuối.</summary>
public class PaymentSettingDto
{
    public Guid Id { get; set; }
    public string BankCode { get; set; } = "MB";
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string QrTemplate { get; set; } = "compact";
    public bool IsActive { get; set; }
    public bool HasApiKey { get; set; }
    public string? ApiKeyHint { get; set; }
    public bool HasWebhookSecret { get; set; }
    public string? WebhookSecretHint { get; set; }
}

public class UpdatePaymentSettingDto
{
    public string BankCode { get; set; } = "MB";
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string QrTemplate { get; set; } = "compact";
    /// <summary>null = giữ nguyên khóa cũ, "" = xóa khóa, giá trị khác = thay khóa mới.</summary>
    public string? SePayApiKey { get; set; }
    /// <summary>Secret Key khi chọn HMAC-SHA256 trên SePay. null = giữ nguyên, "" = xóa.</summary>
    public string? SePayWebhookSecret { get; set; }
}

public class PaymentWebhookLogDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Gateway { get; set; }
    public string? TransactionDate { get; set; }
    public decimal? AmountIn { get; set; }
    public string? TransactionContent { get; set; }
    public string? TransferType { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
    public string? OrderCode { get; set; }
}

public class WebhookResult
{
    public bool Authorized { get; set; } = true;
    public string Status { get; set; } = "UNMATCHED";
    public string Message { get; set; } = string.Empty;
}

public class SePayWebhookPayload
{
    public long? id { get; set; }
    public string? gateway { get; set; }
    public string? transactionDate { get; set; }
    public string? accountNumber { get; set; }
    public string? code { get; set; }
    public string? content { get; set; }
    public string? transferType { get; set; }
    public decimal? transferAmount { get; set; }
    public decimal? accumulated { get; set; }
    public string? subAccount { get; set; }
    public string? referenceCode { get; set; }
    public string? description { get; set; }
}
