using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

/// <summary>Phiếu thu / phiếu chi ngoài bán hàng và nhập hàng (chi tiêu phát sinh, khoản tiền được cấp...).</summary>
[Table("CashTransaction")]
public class CashTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;        // PC-yyyyMMdd-### / PT-yyyyMMdd-###
    public string Type { get; set; } = "EXPENSE";           // EXPENSE, INCOME
    public string Category { get; set; } = "OTHER_EXPENSE";
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Now;
    public string PaymentMethod { get; set; } = "CASH";     // CASH, BANK
    public string? PartyName { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}
