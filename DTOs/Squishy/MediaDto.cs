using System;
using System.Collections.Generic;

namespace SystemBase.BE.DTOs.Squishy;

public class MediaItemDto
{
    public Guid Id { get; set; }
    public Guid? EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public bool IsPrimary { get; set; }
    public int? DurationSeconds { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AttachMediaRequest
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = "PRODUCT";
    public List<Guid>? ImageIds { get; set; }
    public List<Guid>? VideoIds { get; set; }
}
