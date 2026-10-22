using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SystemBase.BE.Models.SquishyManagement;

[Table("MediaVideo")]
public class MediaVideo
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty; // 'PRODUCT', 'ORDER_PAYMENT', 'OTHER'
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public int? DurationSeconds { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}
