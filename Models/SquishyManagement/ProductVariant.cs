using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

/// <summary>Phân loại (màu) của sản phẩm: tồn kho và giá vốn riêng, giá bán dùng chung của sản phẩm.</summary>
[Table("ProductVariant")]
public class ProductVariant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public string? ImageUrl { get; set; }
    public int CurrentStock { get; set; }
    public decimal CostPrice { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}
