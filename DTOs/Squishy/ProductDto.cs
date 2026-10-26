using System;
using System.Collections.Generic;

namespace SystemBase.BE.DTOs.Squishy;

public class ProductDto
{
    public Guid Id { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = "Chiếc";
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public int CurrentStock { get; set; }
    public int MinStockAlert { get; set; }
    public string? SquishyType { get; set; }
    public string? MainImageUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<MediaItemDto> Images { get; set; } = new();
    public List<MediaItemDto> Videos { get; set; } = new();
    public List<ProductVariantDto> Variants { get; set; } = new();
}

public class ProductVariantDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public string? ImageUrl { get; set; }
    public int CurrentStock { get; set; }
    public decimal CostPrice { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public class SaveProductVariantDto
{
    /// <summary>null = màu mới</summary>
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Tồn đầu kỳ, chỉ dùng khi tạo màu mới (sau đó tồn kho thay đổi qua phiếu nhập / bán hàng)</summary>
    public int InitialStock { get; set; }
}

public class CreateUpdateProductDto
{
    public Guid? CategoryId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = "Chiếc";
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public int CurrentStock { get; set; }
    public int MinStockAlert { get; set; } = 5;
    public string? SquishyType { get; set; }
    public string? MainImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string>? ImageUrls { get; set; }
    public List<string>? VideoUrls { get; set; }
    /// <summary>null = không đổi phân loại; danh sách rỗng = bỏ hết màu (chỉ khi các màu đã hết hàng)</summary>
    public List<SaveProductVariantDto>? Variants { get; set; }
}

public class ProductFilterRequest
{
    public string? Keyword { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? LowStockOnly { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
