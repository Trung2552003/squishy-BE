using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SystemBase.BE.DTOs.Shared;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Infrastructure.Dapper;

namespace SystemBase.BE.Services.Squishy;

public interface IReportService
{
    Task<RestData<DashboardSummaryDto>> GetSummaryAsync(DateTime? from = null, DateTime? to = null);
    Task<RestData<List<ChartPointDto>>> GetChartAsync(int days = 7, DateTime? from = null, DateTime? to = null);
    Task<RestData<List<TopSellingProductDto>>> GetTopProductsAsync(int top = 5, DateTime? from = null, DateTime? to = null);
}

public class ReportService : IReportService
{
    private const int MaxChartDays = 92;

    // Đơn được tính doanh thu: đã thanh toán và không bị hủy. Ghi nhận theo ngày thanh toán.
    private const string PaidOrderFilter = @"o.IsDeleted = 0 AND o.PaymentStatus = 'PAID' AND o.OrderStatus <> 'CANCELLED'
        AND (@From IS NULL OR ISNULL(o.PaidAt, o.CreatedAt) >= @From) AND (@To IS NULL OR ISNULL(o.PaidAt, o.CreatedAt) < @To)";

    private readonly IDbConnectionFactory _connectionFactory;

    public ReportService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RestData<DashboardSummaryDto>> GetSummaryAsync(DateTime? from = null, DateTime? to = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dto = await ComputePeriodAsync(connection, from, to, withBreakdown: true);

        var counts = await connection.QueryFirstAsync<(int Total, int Completed, int Paid, int Cancelled)>(@"
            SELECT COUNT(1),
                   ISNULL(SUM(CASE WHEN OrderStatus = 'COMPLETED' THEN 1 ELSE 0 END), 0),
                   ISNULL(SUM(CASE WHEN PaymentStatus = 'PAID' AND OrderStatus <> 'CANCELLED' THEN 1 ELSE 0 END), 0),
                   ISNULL(SUM(CASE WHEN OrderStatus = 'CANCELLED' THEN 1 ELSE 0 END), 0)
            FROM [Order]
            WHERE IsDeleted = 0 AND (@From IS NULL OR CreatedAt >= @From) AND (@To IS NULL OR CreatedAt < @To)",
            new { From = from, To = to });
        dto.TotalOrders = counts.Total;
        dto.CompletedOrders = counts.Completed;
        dto.PaidOrders = counts.Paid;
        dto.CancelledOrders = counts.Cancelled;

        var stock = await connection.QueryFirstAsync<(int Products, int LowStock, decimal Value)>(@"
            SELECT COUNT(1),
                   ISNULL(SUM(CASE WHEN CurrentStock <= MinStockAlert THEN 1 ELSE 0 END), 0),
                   ISNULL(SUM(CurrentStock * CostPrice), 0)
            FROM Product WHERE IsDeleted = 0");
        dto.TotalProducts = stock.Products;
        dto.LowStockCount = stock.LowStock;
        dto.TotalInventoryValue = stock.Value;

        return new RestData<DashboardSummaryDto> { IsSuccess = true, Data = dto };
    }

    public async Task<RestData<List<ChartPointDto>>> GetChartAsync(int days = 7, DateTime? from = null, DateTime? to = null)
    {
        // Mặc định: N ngày gần nhất tính cả hôm nay. Có from/to thì vẽ đúng khoảng đó (tối đa 92 ngày).
        var start = (from ?? DateTime.Today.AddDays(-(Math.Clamp(days, 1, MaxChartDays) - 1))).Date;
        var end = (to ?? DateTime.Today.AddDays(1)).Date;
        if (end <= start) end = start.AddDays(1);
        if ((end - start).TotalDays > MaxChartDays) start = end.AddDays(-MaxChartDays);

        using var connection = _connectionFactory.CreateConnection();
        var result = new List<ChartPointDto>();
        for (var date = start; date < end; date = date.AddDays(1))
        {
            var next = date.AddDays(1);
            // Dùng chung công thức với thẻ tổng để tổng các cột luôn bằng số liệu tổng của kỳ
            var m = await ComputePeriodAsync(connection, date, next, withBreakdown: false);
            var orders = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM [Order] WHERE IsDeleted = 0 AND CreatedAt >= @Date AND CreatedAt < @Next",
                new { Date = date, Next = next });

            result.Add(new ChartPointDto
            {
                Label = date.ToString("dd/MM"),
                Date = date,
                Revenue = m.TotalRevenue,
                Cost = m.CashOut,
                Expense = m.OperatingExpense,
                Profit = m.NetProfit,
                Orders = orders
            });
        }

        return new RestData<List<ChartPointDto>> { IsSuccess = true, Data = result };
    }

    public async Task<RestData<List<TopSellingProductDto>>> GetTopProductsAsync(int top = 5, DateTime? from = null, DateTime? to = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        // Chỉ đếm hàng trong đơn đã thanh toán và chưa hủy (trước đây đếm cả đơn chưa trả tiền / đã hủy)
        var list = (await connection.QueryAsync<TopSellingProductDto>($@"
            SELECT TOP (@Top)
                p.Id AS ProductId, p.Name AS ProductName, p.MainImageUrl, p.CurrentStock,
                SUM(d.Quantity) AS TotalQuantitySold,
                SUM(d.TotalPrice) AS TotalRevenue
            FROM OrderDetail d
            JOIN [Order] o ON o.Id = d.OrderId
            JOIN Product p ON p.Id = d.ProductId
            WHERE {PaidOrderFilter}
            GROUP BY p.Id, p.Name, p.MainImageUrl, p.CurrentStock
            ORDER BY TotalQuantitySold DESC, TotalRevenue DESC",
            new { Top = Math.Clamp(top, 1, 50), From = from, To = to })).ToList();
        return new RestData<List<TopSellingProductDto>> { IsSuccess = true, Data = list };
    }

    /// <summary>Tính toàn bộ số liệu tiền của một kỳ. Xem công thức ở <see cref="DashboardSummaryDto"/>.</summary>
    private static async Task<DashboardSummaryDto> ComputePeriodAsync(IDbConnection connection, DateTime? from, DateTime? to, bool withBreakdown)
    {
        var args = new { From = from, To = to };

        // 1) Bán hàng
        // Ship người nhận trả: nằm trong tiền khách trả nhưng là tiền thu hộ cho đơn vị vận chuyển → tách ra khỏi doanh thu.
        // Ship người gửi (shop) trả: không nằm trong tiền khách trả, là chi phí của shop.
        var sales = await connection.QueryFirstAsync<(decimal Revenue, decimal Collected, decimal Expense)>($@"
            SELECT
                ISNULL(SUM(o.TotalAmount - CASE WHEN o.ShippingPayer = 'RECEIVER' THEN o.ShippingFee ELSE 0 END), 0),
                ISNULL(SUM(CASE WHEN o.ShippingPayer = 'RECEIVER' THEN o.ShippingFee ELSE 0 END), 0),
                ISNULL(SUM(CASE WHEN o.ShippingPayer = 'SENDER' THEN o.ShippingFee ELSE 0 END), 0)
            FROM [Order] o WHERE {PaidOrderFilter}", args);
        var cogs = await connection.ExecuteScalarAsync<decimal>($@"
            SELECT ISNULL(SUM(d.Quantity * d.CostPrice), 0)
            FROM OrderDetail d JOIN [Order] o ON o.Id = d.OrderId
            WHERE {PaidOrderFilter}", args);

        // 2) Đổi trả / bảo hành hoàn tất trong kỳ
        var ret = await GetReturnFiguresAsync(connection, from, to);

        // 3) Nhập hàng (tiền ra, không trừ thẳng vào lợi nhuận)
        var purchases = await connection.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(TotalAmount), 0) FROM GoodsReceipt
            WHERE IsDeleted = 0 AND Status = 'COMPLETED'
              AND (@From IS NULL OR ReceiptDate >= @From) AND (@To IS NULL OR ReceiptDate < @To)", args);

        // 4) Thu chi khác
        var cash = await GetCashFiguresAsync(connection, from, to);

        var dto = new DashboardSummaryDto
        {
            From = from,
            To = to,
            SalesRevenue = sales.Revenue,
            ShippingFeeCollected = sales.Collected,
            ShippingExpense = sales.Expense,
            ReturnRefunds = ret.Refund,
            ReturnExtraCharges = ret.Extra,
            RepairCost = ret.Repair,
            TotalRevenue = sales.Revenue - ret.Refund + ret.Extra,
            Cogs = cogs - ret.RestockedCost + ret.ExchangeCost + ret.Repair,
            OperatingExpense = cash.Sum(c => c.Type == "EXPENSE" ? c.Amount : 0),
            OtherIncome = cash.Sum(c => c.Type == "INCOME" && c.AffectsProfit ? c.Amount : 0),
            CapitalIn = cash.Sum(c => c.Type == "INCOME" && !c.AffectsProfit ? c.Amount : 0),
            TotalCost = purchases
        };
        dto.GrossProfit = dto.TotalRevenue - dto.Cogs;
        dto.NetProfit = dto.GrossProfit - dto.ShippingExpense - dto.OperatingExpense + dto.OtherIncome;
        dto.CashIn = dto.SalesRevenue + dto.ReturnExtraCharges + dto.OtherIncome + dto.CapitalIn;
        dto.CashOut = dto.TotalCost + dto.ShippingExpense + dto.OperatingExpense + dto.ReturnRefunds + dto.RepairCost;
        dto.NetCashFlow = dto.CashIn - dto.CashOut;

        if (withBreakdown)
        {
            dto.ExpenseBreakdown = BuildBreakdown(cash, "EXPENSE");
            dto.IncomeBreakdown = BuildBreakdown(cash, "INCOME");
        }
        return dto;
    }

    private record CashRow(string Type, string Category, decimal Amount, int Count, bool AffectsProfit);

    private static async Task<List<CashRow>> GetCashFiguresAsync(IDbConnection connection, DateTime? from, DateTime? to)
    {
        var tableExists = await connection.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID('dbo.CashTransaction') IS NULL THEN 0 ELSE 1 END");
        if (tableExists == 0) return new List<CashRow>();

        var rows = await connection.QueryAsync<(string Type, string Category, decimal Amount, int Count)>(@"
            SELECT Type, Category, SUM(Amount), COUNT(1)
            FROM CashTransaction
            WHERE IsDeleted = 0 AND (@From IS NULL OR TransactionDate >= @From) AND (@To IS NULL OR TransactionDate < @To)
            GROUP BY Type, Category", new { From = from, To = to });

        return rows.Select(r =>
        {
            var cat = CashTransactionService.Categories.FirstOrDefault(c => c.Code == r.Category);
            return new CashRow(r.Type, r.Category, r.Amount, r.Count, cat?.AffectsProfit ?? true);
        }).ToList();
    }

    private static List<CategoryAmountDto> BuildBreakdown(List<CashRow> rows, string type) =>
        rows.Where(r => r.Type == type)
            .Select(r =>
            {
                var cat = CashTransactionService.Categories.FirstOrDefault(c => c.Code == r.Category);
                return new CategoryAmountDto
                {
                    Category = r.Category,
                    Name = cat?.Name ?? r.Category,
                    Emoji = cat?.Emoji ?? "",
                    AffectsProfit = r.AffectsProfit,
                    Amount = r.Amount,
                    Count = r.Count
                };
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

    /// <summary>
    /// Số liệu các phiếu đổi trả đã hoàn tất (theo ngày hoàn tất):
    /// hoàn tiền / thu thêm của khách, chi phí sửa shop chịu, giá vốn hàng tốt nhập lại kho và hàng xuất đổi.
    /// Hàng lỗi không nhập lại kho nên giá vốn của nó vẫn nằm trong giá vốn (là khoản lỗ).
    /// </summary>
    private static async Task<(decimal Refund, decimal Extra, decimal Repair, decimal RestockedCost, decimal ExchangeCost)> GetReturnFiguresAsync(
        IDbConnection connection, DateTime? from, DateTime? to)
    {
        var tableExists = await connection.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID('dbo.ReturnRequest') IS NULL THEN 0 ELSE 1 END");
        if (tableExists == 0) return (0, 0, 0, 0, 0);

        const string dateFilter = "(@From IS NULL OR r.CompletedAt >= @From) AND (@To IS NULL OR r.CompletedAt < @To)";
        var args = new { From = from, To = to };

        var money = await connection.QueryFirstAsync<(decimal Refund, decimal Extra, decimal Repair)>($@"
            SELECT ISNULL(SUM(r.RefundAmount), 0), ISNULL(SUM(r.ExtraCharge), 0), ISNULL(SUM(r.RepairCost), 0)
            FROM ReturnRequest r
            WHERE r.IsDeleted = 0 AND r.Status = 'COMPLETED' AND {dateFilter}", args);

        var goods = await connection.QueryFirstAsync<(decimal RestockedCost, decimal ExchangeCost)>($@"
            SELECT
                ISNULL(SUM(CASE WHEN r.StockRestored = 1 AND i.ItemCondition = 'GOOD' AND r.RequestType <> 'REPAIR'
                                THEN i.Quantity * i.CostPrice ELSE 0 END), 0),
                ISNULL(SUM(CASE WHEN r.RequestType = 'EXCHANGE' THEN i.ExchangeQuantity * i.ExchangeCostPrice ELSE 0 END), 0)
            FROM ReturnRequestItem i
            JOIN ReturnRequest r ON r.Id = i.ReturnRequestId
            WHERE r.IsDeleted = 0 AND r.Status = 'COMPLETED' AND {dateFilter}", args);

        return (money.Refund, money.Extra, money.Repair, goods.RestockedCost, goods.ExchangeCost);
    }
}
