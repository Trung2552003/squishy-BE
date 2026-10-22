using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using SystemBase.BE.DTOs.Shared;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Infrastructure.Dapper;

namespace SystemBase.BE.Services.Squishy;

public interface IPreorderService
{
    // Dành cho khách (trang đặt trước công khai)
    Task<RestData<List<PreorderCampaignDto>>> GetOpenCampaignsAsync();
    Task<RestData<PreorderCampaignPageDto>> GetCampaignPageAsync(string slug);
    Task<RestData<PreorderReceiptDto>> CreateAsync(CreatePreorderRequest request);

    // Dành cho quản trị
    Task<RestData<List<PreorderCampaignDto>>> GetCampaignsAsync();
    Task<RestData<PreorderCampaignDto>> SaveCampaignAsync(Guid? id, SavePreorderCampaignRequest request);
    Task<RestData<bool>> DeleteCampaignAsync(Guid id);
    Task<RestData<List<PreorderItemDto>>> GetAllItemsAsync();
    Task<RestData<PreorderItemDto>> SaveItemAsync(Guid? id, SavePreorderItemRequest request);
    Task<RestData<bool>> DeleteItemAsync(Guid id);
    Task<RestData<List<PreorderDto>>> GetAllAsync(string? status = null, string? keyword = null, Guid? campaignId = null);
    Task<RestData<PreorderDto>> UpdateStatusAsync(Guid id, UpdatePreorderStatusRequest request);
    Task<RestData<List<PreorderSummaryDto>>> GetSummaryAsync(Guid? campaignId = null);
}

public class PreorderService : IPreorderService
{
    private static readonly string[] Statuses = { "NEW", "CONTACTED", "CONFIRMED", "CANCELLED" };
    private static readonly string[] Tags = { "HOT", "NEW", "NONE" };
    private static readonly Regex PhoneRegex = new(@"^(0|\+84)\d{9}$");
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(-[a-z0-9]+)*$");
    private const int MaxQuantityPerLine = 99;
    private const int MaxSlugLength = 60;

    private readonly IDbConnectionFactory _connectionFactory;

    public PreorderService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ===================================================================================
    // ĐỢT ĐẶT TRƯỚC
    // ===================================================================================

    public async Task<RestData<List<PreorderCampaignDto>>> GetOpenCampaignsAsync()
    {
        var all = await LoadCampaignsAsync(includeStats: false);
        // Trang công khai chỉ cần tên, link và thời hạn, không lộ số liệu đơn
        var open = all.Where(c => c.Status == "OPEN").Select(c => new PreorderCampaignDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            OpenAt = c.OpenAt,
            CloseAt = c.CloseAt,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            Status = c.Status,
            ItemIds = c.ItemIds
        }).ToList();
        return new RestData<List<PreorderCampaignDto>> { IsSuccess = true, Data = open };
    }

    public async Task<RestData<PreorderCampaignPageDto>> GetCampaignPageAsync(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();
        var campaign = await connection.QueryFirstOrDefaultAsync<PreorderCampaignDto>(
            "SELECT * FROM PreorderCampaign WHERE Slug = @Slug AND IsDeleted = 0", new { Slug = (slug ?? "").Trim().ToLowerInvariant() });
        if (campaign == null)
            return Fail<PreorderCampaignPageDto>("Không tìm thấy đợt đặt trước này. Link có thể đã bị đổi hoặc xóa.");

        var items = (await connection.QueryAsync<PreorderItemDto>(@"
            SELECT i.* FROM PreorderItem i
            JOIN PreorderCampaignItem ci ON ci.PreorderItemId = i.Id
            WHERE ci.CampaignId = @Id AND i.IsDeleted = 0 AND i.IsActive = 1
            ORDER BY i.DisplayOrder, i.Name", new { campaign.Id })).ToList();

        return new RestData<PreorderCampaignPageDto>
        {
            IsSuccess = true,
            Data = new PreorderCampaignPageDto
            {
                Name = campaign.Name,
                Slug = campaign.Slug,
                Description = campaign.Description,
                OpenAt = campaign.OpenAt,
                CloseAt = campaign.CloseAt,
                Status = ComputeStatus(campaign),
                Items = items
            }
        };
    }

    public async Task<RestData<List<PreorderCampaignDto>>> GetCampaignsAsync()
    {
        return new RestData<List<PreorderCampaignDto>> { IsSuccess = true, Data = await LoadCampaignsAsync(includeStats: true) };
    }

    public async Task<RestData<PreorderCampaignDto>> SaveCampaignAsync(Guid? id, SavePreorderCampaignRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 200)
            return Fail<PreorderCampaignDto>("Vui lòng nhập tên đợt (tối đa 200 ký tự).");
        if ((request.Description?.Length ?? 0) > 1000)
            return Fail<PreorderCampaignDto>("Lời nhắn cho khách tối đa 1000 ký tự.");
        if (request.OpenAt.HasValue && request.CloseAt.HasValue && request.CloseAt <= request.OpenAt)
            return Fail<PreorderCampaignDto>("Thời gian đóng phải sau thời gian mở.");

        var itemIds = (request.ItemIds ?? new()).Distinct().ToList();
        if (itemIds.Count == 0)
            return Fail<PreorderCampaignDto>("Hãy chọn ít nhất 1 mẫu cho đợt này.");

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slugify(name) : request.Slug.Trim().ToLowerInvariant();
        if (slug.Length < 2 || slug.Length > MaxSlugLength || !SlugRegex.IsMatch(slug))
            return Fail<PreorderCampaignDto>("Đuôi link chỉ gồm chữ thường không dấu, số và dấu gạch ngang (VD: thang-11).");

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var validItems = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PreorderItem WHERE Id IN @Ids AND IsDeleted = 0", new { Ids = itemIds }, transaction);
            if (validItems != itemIds.Count)
                return Fail<PreorderCampaignDto>("Có mẫu không còn tồn tại. Vui lòng tải lại và chọn lại.");

            // Đuôi link không được trùng với đợt khác (kể cả đợt đã xóa, để link cũ không trỏ nhầm sang đợt mới)
            var campaignId = id ?? Guid.NewGuid();
            var taken = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PreorderCampaign WHERE Slug = @Slug AND Id <> @Id", new { Slug = slug, Id = campaignId }, transaction);
            if (taken > 0)
            {
                if (!string.IsNullOrWhiteSpace(request.Slug))
                    return Fail<PreorderCampaignDto>($"Đuôi link \"{slug}\" đã được dùng cho đợt khác. Hãy đặt đuôi khác.");
                slug = await NextFreeSlugAsync(connection, transaction, slug, campaignId);
            }

            var args = new
            {
                Id = campaignId,
                Name = name,
                Slug = slug,
                Description = NullIfBlank(request.Description),
                request.OpenAt,
                request.CloseAt,
                request.IsActive
            };

            if (id.HasValue)
            {
                var affected = await connection.ExecuteAsync(@"
                    UPDATE PreorderCampaign
                    SET Name = @Name, Slug = @Slug, Description = @Description, OpenAt = @OpenAt, CloseAt = @CloseAt,
                        IsActive = @IsActive, UpdatedAt = GETDATE()
                    WHERE Id = @Id AND IsDeleted = 0", args, transaction);
                if (affected == 0)
                    return Fail<PreorderCampaignDto>("Không tìm thấy đợt.");
                await connection.ExecuteAsync("DELETE FROM PreorderCampaignItem WHERE CampaignId = @Id", new { Id = campaignId }, transaction);
            }
            else
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO PreorderCampaign (Id, Name, Slug, Description, OpenAt, CloseAt, IsActive, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES (@Id, @Name, @Slug, @Description, @OpenAt, @CloseAt, @IsActive, GETDATE(), GETDATE(), 0)", args, transaction);
            }

            await connection.ExecuteAsync(
                "INSERT INTO PreorderCampaignItem (CampaignId, PreorderItemId) VALUES (@CampaignId, @PreorderItemId)",
                itemIds.Select(i => new { CampaignId = campaignId, PreorderItemId = i }), transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        var saved = (await LoadCampaignsAsync(includeStats: true)).First(c => c.Slug == slug);
        return new RestData<PreorderCampaignDto> { IsSuccess = true, Data = saved };
    }

    public async Task<RestData<bool>> DeleteCampaignAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        // Xóa mềm: đơn của đợt vẫn được giữ, link của đợt ngừng hoạt động
        await connection.ExecuteAsync(
            "UPDATE PreorderCampaign SET IsDeleted = 1, IsActive = 0, UpdatedAt = GETDATE() WHERE Id = @Id", new { Id = id });
        return new RestData<bool> { IsSuccess = true, Data = true };
    }

    private async Task<List<PreorderCampaignDto>> LoadCampaignsAsync(bool includeStats)
    {
        using var connection = _connectionFactory.CreateConnection();
        var campaigns = (await connection.QueryAsync<PreorderCampaignDto>(includeStats
            ? @"SELECT c.*,
                       (SELECT COUNT(1) FROM Preorder p WHERE p.CampaignId = c.Id AND p.IsDeleted = 0 AND p.Status <> 'CANCELLED') AS OrderCount,
                       (SELECT ISNULL(SUM(p.TotalAmount), 0) FROM Preorder p WHERE p.CampaignId = c.Id AND p.IsDeleted = 0 AND p.Status <> 'CANCELLED') AS TotalAmount,
                       (SELECT ISNULL(SUM(l.Quantity), 0) FROM PreorderLine l JOIN Preorder p ON p.Id = l.PreorderId
                        WHERE p.CampaignId = c.Id AND p.IsDeleted = 0 AND p.Status <> 'CANCELLED') AS TotalQuantity
                FROM PreorderCampaign c WHERE c.IsDeleted = 0 ORDER BY c.CreatedAt DESC"
            : "SELECT * FROM PreorderCampaign WHERE IsDeleted = 0 ORDER BY CreatedAt DESC")).ToList();

        if (campaigns.Count > 0)
        {
            var links = (await connection.QueryAsync<(Guid CampaignId, Guid PreorderItemId)>(@"
                SELECT ci.CampaignId, ci.PreorderItemId FROM PreorderCampaignItem ci
                JOIN PreorderItem i ON i.Id = ci.PreorderItemId
                WHERE ci.CampaignId IN @Ids AND i.IsDeleted = 0", new { Ids = campaigns.Select(c => c.Id).ToList() })).ToList();
            foreach (var c in campaigns)
            {
                c.Status = ComputeStatus(c);
                c.ItemIds = links.Where(l => l.CampaignId == c.Id).Select(l => l.PreorderItemId).ToList();
            }
        }
        return campaigns;
    }

    private static string ComputeStatus(PreorderCampaignDto c)
    {
        var now = DateTime.Now;
        if (!c.IsActive) return "CLOSED";
        if (c.OpenAt.HasValue && c.OpenAt > now) return "UPCOMING";
        if (c.CloseAt.HasValue && c.CloseAt <= now) return "CLOSED";
        return "OPEN";
    }

    private static async Task<string> NextFreeSlugAsync(IDbConnection connection, IDbTransaction transaction, string baseSlug, Guid campaignId)
    {
        for (var n = 2; ; n++)
        {
            var suffix = $"-{n}";
            var candidate = baseSlug[..Math.Min(baseSlug.Length, MaxSlugLength - suffix.Length)].TrimEnd('-') + suffix;
            var taken = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM PreorderCampaign WHERE Slug = @Slug AND Id <> @Id", new { Slug = candidate, Id = campaignId }, transaction);
            if (taken == 0) return candidate;
        }
    }

    /// <summary>"Đợt tháng 11 – Phô mai" → "dot-thang-11-pho-mai"</summary>
    private static string Slugify(string text)
    {
        var normalized = text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch is >= 'a' and <= 'z' or >= '0' and <= '9' ? ch : '-');
        }
        var slug = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        if (slug.Length > MaxSlugLength) slug = slug[..MaxSlugLength].TrimEnd('-');
        return slug.Length >= 2 ? slug : $"dot-{DateTime.Now:yyyyMMdd}";
    }

    // ===================================================================================
    // ĐƠN ĐẶT TRƯỚC
    // ===================================================================================

    public async Task<RestData<PreorderReceiptDto>> CreateAsync(CreatePreorderRequest request)
    {
        var name = request.CustomerName?.Trim() ?? "";
        var phone = Regex.Replace(request.CustomerPhone ?? "", @"[\s.\-]", "");

        if (name.Length < 2 || name.Length > 100)
            return Fail<PreorderReceiptDto>("Vui lòng nhập họ tên (2–100 ký tự).");
        if (!PhoneRegex.IsMatch(phone))
            return Fail<PreorderReceiptDto>("Số điện thoại chưa đúng, ví dụ: 0912345678.");
        var instagram = NullIfBlank(request.InstagramContact)?.TrimStart('@');
        if ((request.SocialContact?.Length ?? 0) > 200 || (instagram?.Length ?? 0) > 100 || (request.ShippingAddress?.Length ?? 0) > 500 || (request.Note?.Length ?? 0) > 500)
            return Fail<PreorderReceiptDto>("Thông tin nhập vào quá dài.");

        var lines = (request.Lines ?? new())
            .Where(l => l.Quantity > 0)
            .GroupBy(l => l.PreorderItemId)
            .Select(g => new { ItemId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();
        if (lines.Count == 0)
            return Fail<PreorderReceiptDto>("Vui lòng chọn ít nhất 1 mẫu.");
        if (lines.Any(l => l.Quantity > MaxQuantityPerLine))
            return Fail<PreorderReceiptDto>($"Mỗi mẫu đặt tối đa {MaxQuantityPerLine} chiếc. Cần số lượng lớn hơn hãy nhắn trực tiếp cho shop.");

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var campaign = await connection.QueryFirstOrDefaultAsync<PreorderCampaignDto>(
                "SELECT * FROM PreorderCampaign WHERE Slug = @Slug AND IsDeleted = 0",
                new { Slug = (request.CampaignSlug ?? "").Trim().ToLowerInvariant() }, transaction);
            if (campaign == null)
                return Fail<PreorderReceiptDto>("Không tìm thấy đợt đặt trước này.");
            var campaignStatus = ComputeStatus(campaign);
            if (campaignStatus == "UPCOMING")
                return Fail<PreorderReceiptDto>("Đợt này chưa mở nhận đơn. Bạn quay lại sau nhé!");
            if (campaignStatus == "CLOSED")
                return Fail<PreorderReceiptDto>("Đợt này đã đóng, shop không nhận thêm đơn nữa.");

            // Chặn bấm gửi liên tục: cùng số điện thoại trong 30 giây
            var recent = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Preorder WHERE CustomerPhone = @Phone AND CreatedAt > DATEADD(SECOND, -30, GETDATE())",
                new { Phone = phone }, transaction);
            if (recent > 0)
                return Fail<PreorderReceiptDto>("Bạn vừa gửi đơn xong. Nếu muốn đặt thêm, vui lòng đợi ít giây rồi gửi lại.");

            // Luôn lấy giá từ máy chủ, không tin giá gửi lên; chỉ nhận mẫu thuộc đợt
            var items = (await connection.QueryAsync<PreorderItemDto>(@"
                SELECT i.* FROM PreorderItem i
                JOIN PreorderCampaignItem ci ON ci.PreorderItemId = i.Id AND ci.CampaignId = @CampaignId
                WHERE i.Id IN @Ids AND i.IsDeleted = 0 AND i.IsActive = 1",
                new { CampaignId = campaign.Id, Ids = lines.Select(l => l.ItemId).ToList() }, transaction)).ToDictionary(i => i.Id);
            if (items.Count != lines.Count)
                return Fail<PreorderReceiptDto>("Có mẫu đã ngừng nhận đặt trước. Vui lòng tải lại trang và chọn lại.");

            var dateStr = DateTime.Now.ToString("yyyyMMdd");
            var countToday = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Preorder WHERE Code LIKE @Prefix", new { Prefix = $"DTR-{dateStr}-%" }, transaction);

            var preorderId = Guid.NewGuid();
            var code = $"DTR-{dateStr}-{(countToday + 1):D3}";
            var lineRows = lines.Select(l => new
            {
                Id = Guid.NewGuid(),
                PreorderId = preorderId,
                PreorderItemId = l.ItemId,
                ItemName = items[l.ItemId].Name,
                UnitPrice = items[l.ItemId].Price,
                l.Quantity
            }).ToList();
            var total = lineRows.Sum(l => l.UnitPrice * l.Quantity);

            await connection.ExecuteAsync(@"
                INSERT INTO Preorder (Id, CampaignId, Code, CustomerName, CustomerPhone, SocialContact, InstagramContact, ShippingAddress, Note, TotalAmount, Status, CreatedAt, UpdatedAt, IsDeleted)
                VALUES (@Id, @CampaignId, @Code, @CustomerName, @CustomerPhone, @SocialContact, @InstagramContact, @ShippingAddress, @Note, @TotalAmount, 'NEW', GETDATE(), GETDATE(), 0)",
                new
                {
                    Id = preorderId,
                    CampaignId = campaign.Id,
                    Code = code,
                    CustomerName = name,
                    CustomerPhone = phone,
                    SocialContact = NullIfBlank(request.SocialContact),
                    InstagramContact = string.IsNullOrEmpty(instagram) ? null : instagram,
                    ShippingAddress = NullIfBlank(request.ShippingAddress),
                    Note = NullIfBlank(request.Note),
                    TotalAmount = total
                }, transaction);

            await connection.ExecuteAsync(@"
                INSERT INTO PreorderLine (Id, PreorderId, PreorderItemId, ItemName, UnitPrice, Quantity)
                VALUES (@Id, @PreorderId, @PreorderItemId, @ItemName, @UnitPrice, @Quantity)", lineRows, transaction);

            transaction.Commit();

            return new RestData<PreorderReceiptDto>
            {
                IsSuccess = true,
                Message = "Đặt trước thành công!",
                Data = new PreorderReceiptDto { Code = code, TotalAmount = total, TotalQuantity = lineRows.Sum(l => l.Quantity) }
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // ===================================================================================
    // KHO MẪU
    // ===================================================================================

    public async Task<RestData<List<PreorderItemDto>>> GetAllItemsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var items = (await connection.QueryAsync<PreorderItemDto>(
            "SELECT * FROM PreorderItem WHERE IsDeleted = 0 ORDER BY DisplayOrder, Name")).ToList();
        return new RestData<List<PreorderItemDto>> { IsSuccess = true, Data = items };
    }

    public async Task<RestData<PreorderItemDto>> SaveItemAsync(Guid? id, SavePreorderItemRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 200)
            return Fail<PreorderItemDto>("Vui lòng nhập tên mẫu (tối đa 200 ký tự).");
        if (request.Price < 0)
            return Fail<PreorderItemDto>("Giá không được âm.");
        if (!Tags.Contains(request.Tag))
            return Fail<PreorderItemDto>("Nhãn không hợp lệ.");
        if (request.IsActive && request.Price <= 0)
            return Fail<PreorderItemDto>("Hãy nhập giá trước khi mở bán mẫu này.");

        using var connection = _connectionFactory.CreateConnection();
        var args = new
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            request.Price,
            ImageUrl = NullIfBlank(request.ImageUrl),
            Description = NullIfBlank(request.Description),
            request.Tag,
            request.DisplayOrder,
            request.IsActive
        };

        if (id.HasValue)
        {
            var affected = await connection.ExecuteAsync(@"
                UPDATE PreorderItem
                SET Name = @Name, Price = @Price, ImageUrl = @ImageUrl, Description = @Description, Tag = @Tag,
                    DisplayOrder = @DisplayOrder, IsActive = @IsActive, UpdatedAt = GETDATE()
                WHERE Id = @Id AND IsDeleted = 0", args);
            if (affected == 0)
                return Fail<PreorderItemDto>("Không tìm thấy mẫu.");
        }
        else
        {
            await connection.ExecuteAsync(@"
                INSERT INTO PreorderItem (Id, Name, Price, ImageUrl, Description, Tag, DisplayOrder, IsActive, CreatedAt, UpdatedAt, IsDeleted)
                VALUES (@Id, @Name, @Price, @ImageUrl, @Description, @Tag, @DisplayOrder, @IsActive, GETDATE(), GETDATE(), 0)", args);
        }

        var saved = await connection.QueryFirstAsync<PreorderItemDto>("SELECT * FROM PreorderItem WHERE Id = @Id", new { args.Id });
        return new RestData<PreorderItemDto> { IsSuccess = true, Data = saved };
    }

    public async Task<RestData<bool>> DeleteItemAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        // Xóa mềm: các đơn cũ vẫn giữ tên và giá mẫu đã lưu
        await connection.ExecuteAsync(
            "UPDATE PreorderItem SET IsDeleted = 1, IsActive = 0, UpdatedAt = GETDATE() WHERE Id = @Id", new { Id = id });
        return new RestData<bool> { IsSuccess = true, Data = true };
    }

    // ===================================================================================
    // QUẢN LÝ ĐƠN
    // ===================================================================================

    public async Task<RestData<List<PreorderDto>>> GetAllAsync(string? status = null, string? keyword = null, Guid? campaignId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var where = "p.IsDeleted = 0";
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(status))
        {
            where += " AND p.Status = @Status";
            parameters.Add("Status", status);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            where += " AND (p.Code LIKE @Keyword OR p.CustomerName LIKE @Keyword OR p.CustomerPhone LIKE @Keyword OR p.SocialContact LIKE @Keyword OR p.InstagramContact LIKE @Keyword)";
            parameters.Add("Keyword", $"%{keyword.Trim()}%");
        }
        if (campaignId.HasValue)
        {
            where += " AND p.CampaignId = @CampaignId";
            parameters.Add("CampaignId", campaignId);
        }

        var orders = (await connection.QueryAsync<PreorderDto>($@"
            SELECT p.*, c.Name AS CampaignName
            FROM Preorder p
            LEFT JOIN PreorderCampaign c ON c.Id = p.CampaignId
            WHERE {where} ORDER BY p.CreatedAt DESC", parameters)).ToList();

        if (orders.Count > 0)
        {
            var lines = (await connection.QueryAsync<PreorderLineDto>(@"
                SELECT l.*, i.ImageUrl
                FROM PreorderLine l
                LEFT JOIN PreorderItem i ON i.Id = l.PreorderItemId
                WHERE l.PreorderId IN @Ids", new { Ids = orders.Select(o => o.Id).ToList() })).ToList();
            foreach (var o in orders)
            {
                o.Lines = lines.Where(l => l.PreorderId == o.Id).OrderBy(l => l.ItemName).ToList();
                o.TotalQuantity = o.Lines.Sum(l => l.Quantity);
            }
        }

        return new RestData<List<PreorderDto>> { IsSuccess = true, Data = orders };
    }

    public async Task<RestData<PreorderDto>> UpdateStatusAsync(Guid id, UpdatePreorderStatusRequest request)
    {
        if (!Statuses.Contains(request.Status))
            return Fail<PreorderDto>("Trạng thái không hợp lệ.");

        using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(@"
            UPDATE Preorder SET Status = @Status, AdminNote = @AdminNote, UpdatedAt = GETDATE()
            WHERE Id = @Id AND IsDeleted = 0",
            new { Id = id, request.Status, AdminNote = NullIfBlank(request.AdminNote) });
        if (affected == 0)
            return Fail<PreorderDto>("Không tìm thấy đơn đặt trước.");

        var dto = await connection.QueryFirstAsync<PreorderDto>("SELECT * FROM Preorder WHERE Id = @Id", new { Id = id });
        return new RestData<PreorderDto> { IsSuccess = true, Data = dto };
    }

    public async Task<RestData<List<PreorderSummaryDto>>> GetSummaryAsync(Guid? campaignId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        // Có chọn đợt: hiện mọi mẫu của đợt (kể cả chưa ai đặt) và mẫu có đơn trong đợt.
        // Không chọn đợt: hiện mọi mẫu còn trong kho mẫu và mẫu đã có đơn.
        var rows = (await connection.QueryAsync<PreorderSummaryDto>(@"
            SELECT i.Id AS PreorderItemId, i.Name AS ItemName, i.ImageUrl, i.Price,
                   ISNULL(SUM(CASE WHEN p.Id IS NOT NULL THEN l.Quantity END), 0) AS TotalQuantity,
                   ISNULL(SUM(CASE WHEN p.Status = 'CONFIRMED' THEN l.Quantity END), 0) AS ConfirmedQuantity,
                   COUNT(DISTINCT p.Id) AS OrderCount,
                   ISNULL(SUM(CASE WHEN p.Id IS NOT NULL THEN l.Quantity * l.UnitPrice END), 0) AS TotalAmount
            FROM PreorderItem i
            LEFT JOIN PreorderLine l ON l.PreorderItemId = i.Id
            LEFT JOIN Preorder p ON p.Id = l.PreorderId AND p.IsDeleted = 0 AND p.Status <> 'CANCELLED'
                                 AND (@CampaignId IS NULL OR p.CampaignId = @CampaignId)
            WHERE p.Id IS NOT NULL
               OR (@CampaignId IS NULL AND i.IsDeleted = 0)
               OR (i.IsDeleted = 0 AND EXISTS (SELECT 1 FROM PreorderCampaignItem ci WHERE ci.CampaignId = @CampaignId AND ci.PreorderItemId = i.Id))
            GROUP BY i.Id, i.Name, i.ImageUrl, i.Price, i.DisplayOrder
            ORDER BY TotalQuantity DESC, i.DisplayOrder", new { CampaignId = campaignId })).ToList();
        return new RestData<List<PreorderSummaryDto>> { IsSuccess = true, Data = rows };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RestData<T> Fail<T>(string message) => new() { IsSuccess = false, Message = message };
}
