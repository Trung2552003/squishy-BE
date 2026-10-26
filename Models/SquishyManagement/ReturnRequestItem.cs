using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

[Table("ReturnRequestItem")]
public class ReturnRequestItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReturnRequestId { get; set; }
    public Guid OrderDetailId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }

    // GOOD: còn bán được → nhập lại kho; DEFECTIVE: hàng lỗi → không nhập lại kho
    public string ItemCondition { get; set; } = "DEFECTIVE";

    // Chỉ dùng cho phiếu đổi hàng: sản phẩm giao lại cho khách
    public Guid? ExchangeProductId { get; set; }
    public Guid? ExchangeVariantId { get; set; }
    public string? ExchangeProductName { get; set; }
    public int ExchangeQuantity { get; set; }
    public decimal ExchangeUnitPrice { get; set; }
    public decimal ExchangeCostPrice { get; set; }
}
