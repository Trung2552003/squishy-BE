using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

[Table("MediaImage")]
public class MediaImage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty; // 'PRODUCT', 'ORDER_PAYMENT', 'GOODS_RECEIPT', 'OTHER'
    public string Url { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}
