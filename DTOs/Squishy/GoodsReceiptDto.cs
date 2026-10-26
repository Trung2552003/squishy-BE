using System;
using System.Collections.Generic;

namespace SystemBase.BE.DTOs.Squishy;

public class GoodsReceiptItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string? VariantName { get; set; }
    public string? ProductName { get; set; }
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? LandedUnitCost { get; set; }
}

public class GoodsReceiptDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime ReceiptDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public string Status { get; set; } = "COMPLETED";
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GoodsReceiptItemDto> Details { get; set; } = new();
}

public class CreateGoodsReceiptItemDto
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class CreateGoodsReceiptDto
{
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public DateTime? ReceiptDate { get; set; }
    public string? Note { get; set; }
    public decimal ShippingFee { get; set; }
    public List<CreateGoodsReceiptItemDto> Items { get; set; } = new();
}

public class CancelGoodsReceiptDto
{
    public string? Reason { get; set; }
}

public class SupplierDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }
}
