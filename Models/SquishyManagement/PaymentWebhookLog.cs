using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

[Table("PaymentWebhookLog")]
public class PaymentWebhookLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Gateway { get; set; }
    public string? TransactionDate { get; set; }
    public string? AccountNumber { get; set; }
    public decimal? AmountIn { get; set; }
    public string? TransactionContent { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? RawPayload { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
