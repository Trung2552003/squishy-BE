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
}

public class ProductFilterRequest
{
    public string? Keyword { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? LowStockOnly { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
