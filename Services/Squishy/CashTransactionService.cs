using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SystemBase.BE.DTOs.Shared;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Infrastructure.Dapper;

namespace SystemBase.BE.Services.Squishy;

public interface ICashTransactionService
{
    RestData<List<CashCategoryDto>> GetCategories();
    Task<RestData<CashTransactionListDto>> GetAllAsync(string? type, string? category, DateTime? from, DateTime? to, string? keyword);
    Task<RestData<CashTransactionDto>> SaveAsync(Guid? id, SaveCashTransactionRequest request, string? userName);
    Task<RestData<bool>> DeleteAsync(Guid id);
}

public class CashTransactionService : ICashTransactionService
{
    /// <summary>
    /// Danh mục thu chi. AffectsProfit = false nghĩa là khoản đó không phải lãi/lỗ
    /// (VD vốn được cấp chỉ làm tăng tiền mặt, không được cộng vào lợi nhuận).
    /// </summary>
    public static readonly IReadOnlyList<CashCategoryDto> Categories = new List<CashCategoryDto>
    {
        new() { Code = "SHIPPING",      Type = "EXPENSE", Emoji = "🚚", Name = "Phí vận chuyển, ship",        AffectsProfit = true },
        new() { Code = "PACKAGING",     Type = "EXPENSE", Emoji = "📦", Name = "Bao bì, đóng gói, quà tặng", AffectsProfit = true },
        new() { Code = "MARKETING",     Type = "EXPENSE", Emoji = "📣", Name = "Quảng cáo, marketing",        AffectsProfit = true },
        new() { Code = "RENT",          Type = "EXPENSE", Emoji = "🏠", Name = "Mặt bằng, gian hàng",         AffectsProfit = true },
        new() { Code = "UTILITIES",     Type = "EXPENSE", Emoji = "💡", Name = "Điện, nước, internet",        AffectsProfit = true },
        new() { Code = "SALARY",        Type = "EXPENSE", Emoji = "👩‍💼", Name = "Lương, thưởng, công",         AffectsProfit = true },
        new() { Code = "EQUIPMENT",     Type = "EXPENSE", Emoji = "🛠️", Name = "Dụng cụ, thiết bị",           AffectsProfit = true },
        new() { Code = "FEES",          Type = "EXPENSE", Emoji = "🏦", Name = "Phí ngân hàng, sàn, dịch vụ", AffectsProfit = true },
        new() { Code = "OTHER_EXPENSE", Type = "EXPENSE", Emoji = "🧾", Name = "Chi phí khác",                AffectsProfit = true },
        new() { Code = "CAPITAL",       Type = "INCOME",  Emoji = "💰", Name = "Vốn được cấp, góp vốn",       AffectsProfit = false },
        new() { Code = "FUNDING",       Type = "INCOME",  Emoji = "🎁", Name = "Tài trợ, hỗ trợ",             AffectsProfit = true },
        new() { Code = "OTHER_INCOME",  Type = "INCOME",  Emoji = "➕", Name = "Thu khác (thanh lý...)",      AffectsProfit = true },
    };

    private static readonly string[] Methods = { "CASH", "BANK" };
    private const decimal MaxAmount = 10_000_000_000m;

    private readonly IDbConnectionFactory _connectionFactory;

    public CashTransactionService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public RestData<List<CashCategoryDto>> GetCategories() =>
        new() { IsSuccess = true, Data = Categories.ToList() };

    public async Task<RestData<CashTransactionListDto>> GetAllAsync(string? type, string? category, DateTime? from, DateTime? to, string? keyword)
    {
        using var connection = _connectionFactory.CreateConnection();
        var where = "t.IsDeleted = 0";
        var p = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(type)) { where += " AND t.Type = @Type"; p.Add("Type", type); }
        if (!string.IsNullOrWhiteSpace(category)) { where += " AND t.Category = @Category"; p.Add("Category", category); }
        if (from.HasValue) { where += " AND t.TransactionDate >= @From"; p.Add("From", from); }
        if (to.HasValue) { where += " AND t.TransactionDate < @To"; p.Add("To", to); }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where += " AND (t.Code LIKE @Kw OR t.Description LIKE @Kw OR t.PartyName LIKE @Kw)";
            p.Add("Kw", $"%{keyword.Trim()}%");
        }

        var items = (await connection.QueryAsync<CashTransactionDto>($@"
            SELECT t.*,
                   (SELECT COUNT(1) FROM MediaImage m WHERE m.EntityId = t.Id AND m.EntityType = 'CASH_TXN' AND m.IsDeleted = 0) AS ImageCount
            FROM CashTransaction t
            WHERE {where}
            ORDER BY t.TransactionDate DESC, t.CreatedAt DESC", p)).ToList();
        items.ForEach(Decorate);

        var result = new CashTransactionListDto
        {
            Items = items,
            TotalExpense = items.Where(i => i.Type == "EXPENSE").Sum(i => i.Amount),
            TotalOtherIncome = items.Where(i => i.Type == "INCOME" && i.AffectsProfit).Sum(i => i.Amount),
            TotalCapital = items.Where(i => i.Type == "INCOME" && !i.AffectsProfit).Sum(i => i.Amount)
        };
        return new RestData<CashTransactionListDto> { IsSuccess = true, Data = result };
    }

    public async Task<RestData<CashTransactionDto>> SaveAsync(Guid? id, SaveCashTransactionRequest request, string? userName)
    {
        var category = Categories.FirstOrDefault(c => c.Code == request.Category);
        if (request.Type is not ("EXPENSE" or "INCOME"))
            return Fail("Loại phiếu không hợp lệ.");
        if (category == null || category.Type != request.Type)
            return Fail("Vui lòng chọn danh mục phù hợp.");
        if (request.Amount <= 0 || request.Amount > MaxAmount)
            return Fail("Số tiền phải lớn hơn 0.");
        if (decimal.Truncate(request.Amount) != request.Amount)
            return Fail("Số tiền không có phần lẻ.");
        if (!Methods.Contains(request.PaymentMethod))
            return Fail("Hình thức thanh toán không hợp lệ.");
        if ((request.PartyName?.Length ?? 0) > 200 || (request.Description?.Length ?? 0) > 1000)
            return Fail("Thông tin nhập vào quá dài.");

        var date = request.TransactionDate ?? DateTime.Now;
        if (date > DateTime.Now.AddDays(1))
            return Fail("Ngày phát sinh không được ở tương lai.");

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var txId = id ?? Guid.NewGuid();
            var args = new
            {
                Id = txId,
                request.Type,
                request.Category,
                request.Amount,
                TransactionDate = date,
                request.PaymentMethod,
                PartyName = NullIfBlank(request.PartyName),
                Description = NullIfBlank(request.Description),
                CreatedBy = userName
            };

            if (id.HasValue)
            {
                // Không cho đổi Thu <-> Chi để mã phiếu (PT/PC) luôn đúng loại
                var existingType = await connection.ExecuteScalarAsync<string?>(
                    "SELECT Type FROM CashTransaction WHERE Id = @Id AND IsDeleted = 0", new { Id = txId }, transaction);
                if (existingType == null) return Fail("Không tìm thấy phiếu.");
                if (existingType != request.Type) return Fail("Không thể đổi phiếu chi thành phiếu thu (và ngược lại). Hãy xóa và tạo phiếu mới.");

                await connection.ExecuteAsync(@"
                    UPDATE CashTransaction
                    SET Category = @Category, Amount = @Amount, TransactionDate = @TransactionDate, PaymentMethod = @PaymentMethod,
                        PartyName = @PartyName, Description = @Description, UpdatedAt = GETDATE()
                    WHERE Id = @Id", args, transaction);
            }
            else
            {
                var prefix = request.Type == "EXPENSE" ? "PC" : "PT";
                var dateStr = DateTime.Now.ToString("yyyyMMdd");
                var count = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM CashTransaction WITH (UPDLOCK, HOLDLOCK) WHERE Code LIKE @P",
                    new { P = $"{prefix}-{dateStr}-%" }, transaction);
                await connection.ExecuteAsync(@"
                    INSERT INTO CashTransaction (Id, Code, Type, Category, Amount, TransactionDate, PaymentMethod, PartyName, Description, CreatedBy, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES (@Id, @Code, @Type, @Category, @Amount, @TransactionDate, @PaymentMethod, @PartyName, @Description, @CreatedBy, GETDATE(), GETDATE(), 0)",
                    new
                    {
                        args.Id, Code = $"{prefix}-{dateStr}-{count + 1:D3}", args.Type, args.Category, args.Amount, args.TransactionDate,
                        args.PaymentMethod, args.PartyName, args.Description, args.CreatedBy
                    }, transaction);
            }

            transaction.Commit();

            var dto = await connection.QueryFirstAsync<CashTransactionDto>("SELECT * FROM CashTransaction WHERE Id = @Id", new { Id = txId });
            Decorate(dto);
            return new RestData<CashTransactionDto> { IsSuccess = true, Data = dto, Message = id.HasValue ? "Đã lưu phiếu." : $"Đã tạo phiếu {dto.Code}." };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<RestData<bool>> DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(
            "UPDATE CashTransaction SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE Id = @Id AND IsDeleted = 0", new { Id = id });
        return affected > 0
            ? new RestData<bool> { IsSuccess = true, Data = true, Message = "Đã xóa phiếu." }
            : new RestData<bool> { IsSuccess = false, Message = "Không tìm thấy phiếu." };
    }

    private static void Decorate(CashTransactionDto dto)
    {
        var cat = Categories.FirstOrDefault(c => c.Code == dto.Category);
        dto.CategoryName = cat?.Name ?? dto.Category;
        dto.AffectsProfit = cat?.AffectsProfit ?? true;
    }

    private static RestData<CashTransactionDto> Fail(string message) => new() { IsSuccess = false, Message = message };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
