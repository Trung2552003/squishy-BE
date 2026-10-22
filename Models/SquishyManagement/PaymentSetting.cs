using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

[Table("PaymentSetting")]
public class PaymentSetting
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BankCode { get; set; } = "MB"; // MB, VCB, ACB, TPB, ICB
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? SePayApiKey { get; set; }        // xác thực webhook kiểu "API Key"
    public string? SePayWebhookSecret { get; set; } // xác thực webhook kiểu "HMAC-SHA256"
    public string QrTemplate { get; set; } = "compact";
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
