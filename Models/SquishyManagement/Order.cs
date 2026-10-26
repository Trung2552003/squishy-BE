using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

[Table("Order")]
public class Order
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderCode { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string OrderType { get; set; } = "POS"; // POS, DELIVERY, ONLINE
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "BANK_TRANSFER_QR"; // CASH, BANK_TRANSFER_QR, COD
    public string PaymentStatus { get; set; } = "PENDING"; // PENDING, PAID, FAILED, REFUNDED
    public string OrderStatus { get; set; } = "PENDING"; // PENDING, PROCESSING, COMPLETED, CANCELLED

    // Thông tin giao hàng
    public string? ShippingAddress { get; set; }
    public string? ShippingProvider { get; set; } // GHTK, GHN, ViettelPost, GrabExpress
    public string? TrackingCode { get; set; }
    public decimal ShippingFee { get; set; }        // phí trả cho đơn vị vận chuyển
    public string ShippingPayer { get; set; } = "RECEIVER"; // RECEIVER: khách trả (cộng vào tổng) | SENDER: shop trả (chi phí của shop)

    // Khâu chuẩn bị & đóng hàng (Fulfillment)
    public string PreparationStatus { get; set; } = "PENDING"; // PENDING, PREPARING, PACKED, SHIPPED
    public string? PreparationNote { get; set; }
    public string? PackingChecklistJson { get; set; }

    public string? TransferContent { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentProofImageUrl { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}
