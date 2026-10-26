using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

[Table("GoodsReceiptDetail")]
public class GoodsReceiptDetail
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoodsReceiptId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? LandedUnitCost { get; set; }    // giá nhập + phí ship phân bổ cho 1 chiếc
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
