using System;
using System.Collections.Generic;

namespace SystemBase.BE.DTOs.Squishy;

public class CashTransactionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = "EXPENSE";
    public string Category { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    /// <summary>Có tính vào lãi/lỗ không (vốn được cấp thì không, chỉ làm tăng tiền mặt).</summary>
    public bool AffectsProfit { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public string? PartyName { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ImageCount { get; set; }
}

public class SaveCashTransactionRequest
{
    public string Type { get; set; } = "EXPENSE";
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? TransactionDate { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public string? PartyName { get; set; }
    public string? Description { get; set; }
}

public class CashCategoryDto
{
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = "EXPENSE";
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public bool AffectsProfit { get; set; }
}

public class CashTransactionListDto
{
    public List<CashTransactionDto> Items { get; set; } = new();
    public decimal TotalExpense { get; set; }
    /// <summary>Thu khác tính vào lãi (tài trợ, thanh lý...).</summary>
    public decimal TotalOtherIncome { get; set; }
    /// <summary>Vốn được cấp / góp vốn: tăng tiền nhưng không phải lãi.</summary>
    public decimal TotalCapital { get; set; }
}
