using System;
using System.Collections.Generic;

namespace SystemBase.BE.DTOs.Squishy;

/// <summary>
/// Số liệu tài chính của một kỳ (from..to). Công thức:
///   Doanh thu thuần   = Tiền đơn đã thanh toán (trừ đơn hủy) − Hoàn tiền đổi trả + Thu thêm đổi trả
///   Giá vốn           = Giá vốn hàng đã bán − Hàng tốt nhập lại kho + Hàng xuất đổi + Chi phí sửa
///   Lợi nhuận gộp     = Doanh thu thuần − Giá vốn
///   Lợi nhuận ròng    = Lợi nhuận gộp − Chi phí hoạt động + Thu nhập khác
///   Tiền vào          = Tiền đơn đã thanh toán + Thu thêm đổi trả + Thu khác + Vốn được cấp
///   Tiền ra           = Nhập hàng + Chi phí hoạt động + Hoàn tiền khách + Chi phí sửa
/// Tiền nhập hàng KHÔNG trừ thẳng vào lợi nhuận (đã tính qua giá vốn khi bán ra), chỉ nằm trong dòng tiền.
/// </summary>
public class DashboardSummaryDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    // Kết quả kinh doanh
    public decimal SalesRevenue { get; set; }
    public decimal ShippingFeeCollected { get; set; }
    public decimal ReturnRefunds { get; set; }
    public decimal ReturnExtraCharges { get; set; }
    public decimal TotalRevenue { get; set; }          // doanh thu thuần
    public decimal Cogs { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal OperatingExpense { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal NetProfit { get; set; }

    // Dòng tiền
    public decimal TotalCost { get; set; }             // tiền nhập hàng (phiếu nhập hoàn tất)
    public decimal RepairCost { get; set; }
    public decimal CapitalIn { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal NetCashFlow { get; set; }

    public List<CategoryAmountDto> ExpenseBreakdown { get; set; } = new();
    public List<CategoryAmountDto> IncomeBreakdown { get; set; } = new();

    // Đơn hàng trong kỳ (theo ngày tạo đơn)
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int PaidOrders { get; set; }
    public int CancelledOrders { get; set; }

    // Ảnh chụp hiện tại (không theo kỳ)
    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    public decimal TotalInventoryValue { get; set; }
}

public class CategoryAmountDto
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public bool AffectsProfit { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class ChartPointDto
{
    public string Label { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }   // doanh thu thuần
    public decimal Cost { get; set; }      // tổng tiền chi ra
    public decimal Expense { get; set; }   // chi phí hoạt động
    public decimal Profit { get; set; }    // lợi nhuận ròng
    public int Orders { get; set; }
}

public class TopSellingProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? MainImageUrl { get; set; }
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public int CurrentStock { get; set; }
}

public class StockMovementReportDto
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string? ReferenceCode { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityChange { get; set; }
    public int QuantityAfter { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
