using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

/// <summary>Mẫu hàng sắp về, mở cho khách đặt trước (chưa có trong kho).</summary>
[Table("PreorderItem")]
public class PreorderItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string Tag { get; set; } = "HOT"; // HOT, NEW, NONE
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}

/// <summary>Một đợt đặt trước: có link riêng /dat-truoc/{Slug}, chỉ hiện các mẫu của đợt.</summary>
[Table("PreorderCampaign")]
public class PreorderCampaign
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? OpenAt { get; set; }
    public DateTime? CloseAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}

[Table("PreorderCampaignItem")]
public class PreorderCampaignItem
{
    public Guid CampaignId { get; set; }
    public Guid PreorderItemId { get; set; }
}

/// <summary>Đơn đặt trước của khách.</summary>
[Table("Preorder")]
public class Preorder
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CampaignId { get; set; }
    public string Code { get; set; } = string.Empty; // DTR-yyyyMMdd-###
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? SocialContact { get; set; } // Zalo / Facebook
    public string? InstagramContact { get; set; } // Nick Instagram / Threads
    public string? ShippingAddress { get; set; }
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "NEW"; // NEW, CONTACTED, CONFIRMED, CANCELLED
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}

[Table("PreorderLine")]
public class PreorderLine
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PreorderId { get; set; }
    public Guid PreorderItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
