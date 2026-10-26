using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

[Table("GoodsReceipt")]
public class GoodsReceipt
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public DateTime ReceiptDate { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }        // tiền hàng + phí vận chuyển
    public decimal ShippingFee { get; set; }        // phí ship nhập hàng, phân bổ vào giá vốn từng sản phẩm
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public string Status { get; set; } = "COMPLETED"; // DRAFT, COMPLETED, CANCELLED
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}
