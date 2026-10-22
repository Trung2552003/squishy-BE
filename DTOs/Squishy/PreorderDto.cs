using System;
using System.Collections.Generic;

namespace SystemBase.BE.DTOs.Squishy;

public class PreorderItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string Tag { get; set; } = "HOT";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public class SavePreorderItemRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string Tag { get; set; } = "HOT";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PreorderLineDto
{
    public Guid Id { get; set; }
    public Guid PreorderId { get; set; }
    public Guid PreorderItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class PreorderDto
{
    public Guid Id { get; set; }
    public Guid? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? SocialContact { get; set; }
    public string? InstagramContact { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "NEW";
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalQuantity { get; set; }
    public List<PreorderLineDto> Lines { get; set; } = new();
}

public class CreatePreorderLineRequest
{
    public Guid PreorderItemId { get; set; }
    public int Quantity { get; set; }
}

public class CreatePreorderRequest
{
    public string CampaignSlug { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? SocialContact { get; set; }
    public string? InstagramContact { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Note { get; set; }
    public List<CreatePreorderLineRequest> Lines { get; set; } = new();
}

/// <summary>Kết quả trả cho khách sau khi gửi: chỉ mã đơn và tổng tiền, không lộ dữ liệu khác.</summary>
public class PreorderReceiptDto
{
    public string Code { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
}

public class UpdatePreorderStatusRequest
{
    public string Status { get; set; } = "CONTACTED";
    public string? AdminNote { get; set; }
}

/// <summary>Tổng số lượng khách đặt cho từng mẫu (để chốt số lượng nhập hàng).</summary>
public class PreorderSummaryDto
{
    public Guid PreorderItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public int TotalQuantity { get; set; }
    public int ConfirmedQuantity { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
}

/// <summary>Trạng thái đợt: OPEN (đang nhận đơn), UPCOMING (chưa mở), CLOSED (đã đóng).</summary>
public class PreorderCampaignDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? OpenAt { get; set; }
    public DateTime? CloseAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "OPEN";
    public List<Guid> ItemIds { get; set; } = new();
    public int OrderCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
}

public class SavePreorderCampaignRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public DateTime? OpenAt { get; set; }
    public DateTime? CloseAt { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Guid> ItemIds { get; set; } = new();
}

/// <summary>Dữ liệu trang công khai của một đợt: thông tin đợt + các mẫu thuộc đợt.</summary>
public class PreorderCampaignPageDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? OpenAt { get; set; }
    public DateTime? CloseAt { get; set; }
    public string Status { get; set; } = "OPEN";
    public List<PreorderItemDto> Items { get; set; } = new();
}
