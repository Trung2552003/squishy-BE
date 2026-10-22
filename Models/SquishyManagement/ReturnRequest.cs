using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

/// <summary>
/// Phiếu đổi trả / bảo hành cho một đơn hàng.
/// Kho và tiền chỉ thay đổi khi phiếu chuyển sang COMPLETED.
/// </summary>
[Table("ReturnRequest")]
public class ReturnRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // DT-yyyyMMdd-###
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }

    public string RequestType { get; set; } = "RETURN"; // RETURN, EXCHANGE, REPAIR, FAILED_DELIVERY
    public string ReasonCategory { get; set; } = "DEFECTIVE"; // DEFECTIVE, WRONG_ITEM, CHANGED_MIND, DELIVERY_FAILED, OTHER
    public string? Reason { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, PROCESSING, REPAIRING, REPAIRED, COMPLETED, REJECTED

    // Tiền: hoàn cho khách / thu thêm của khách (chênh lệch đổi hàng, phí sửa) / chi phí sửa shop chịu
    public decimal RefundAmount { get; set; }
    public string? RefundMethod { get; set; } // CASH, BANK_TRANSFER
    public decimal ExtraCharge { get; set; }
    public decimal RepairCost { get; set; }

    // Đã thực sự cộng lại kho hàng tốt khi hoàn tất (đơn chưa bị trừ kho thì không cộng)
    public bool StockRestored { get; set; }

    public string? ResolutionNote { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}
