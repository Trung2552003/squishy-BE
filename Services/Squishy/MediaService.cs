using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SystemBase.BE.DTOs.Shared;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Infrastructure.Dapper;
using SystemBase.BE.Models.SquishyManagement;

namespace SystemBase.BE.Services.Squishy;

public interface IMediaService
{
    Task<RestData<MediaItemDto>> UploadImageAsync(IFormFile file, string entityType = "PRODUCT", Guid? entityId = null, bool isPrimary = false);
    Task<RestData<MediaItemDto>> UploadVideoAsync(IFormFile file, string entityType = "PRODUCT", Guid? entityId = null);
    Task<RestData<List<MediaItemDto>>> GetAllMediaAsync(string? entityType = null, string? mediaType = null);
    Task<RestData<List<MediaItemDto>>> GetMediaByEntityAsync(Guid entityId, string entityType);
    Task<RestData<bool>> DeleteMediaAsync(Guid id, bool isVideo = false);
    Task<RestData<bool>> AttachMediaAsync(AttachMediaRequest request);
}

public class MediaService : IMediaService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IWebHostEnvironment _env;

    public MediaService(IDbConnectionFactory connectionFactory, IWebHostEnvironment env)
    {
        _connectionFactory = connectionFactory;
        _env = env;
    }

    public async Task<RestData<MediaItemDto>> UploadImageAsync(IFormFile file, string entityType = "PRODUCT", Guid? entityId = null, bool isPrimary = false)
    {
        if (file == null || file.Length == 0)
            return new RestData<MediaItemDto> { IsSuccess = false, Message = "File ảnh không hợp lệ." };

        if (file.Length > 20 * 1024 * 1024)
            return new RestData<MediaItemDto> { IsSuccess = false, Message = "Dung lượng ảnh tối đa là 20MB." };

        var ext = Path.GetExtension(file.FileName).ToLower();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
        if (!allowedExts.Contains(ext))
            return new RestData<MediaItemDto> { IsSuccess = false, Message = "Định dạng ảnh không được hỗ trợ." };

        var folderPath = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "images");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(folderPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativeUrl = $"/uploads/images/{fileName}";
        var image = new MediaImage
        {
            Id = Guid.NewGuid(),
            EntityId = entityId,
            EntityType = entityType.ToUpper(),
            Url = relativeUrl,
            FileName = file.FileName,
            FileSize = file.Length,
            MimeType = file.ContentType,
            IsPrimary = isPrimary,
            DisplayOrder = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsDeleted = false
        };

        using var connection = _connectionFactory.CreateConnection();
        var sql = @"INSERT INTO MediaImage (Id, EntityId, EntityType, Url, FileName, FileSize, MimeType, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES (@Id, @EntityId, @EntityType, @Url, @FileName, @FileSize, @MimeType, @IsPrimary, @DisplayOrder, @CreatedAt, @UpdatedAt, @IsDeleted)";
        await connection.ExecuteAsync(sql, image);

        return new RestData<MediaItemDto>
        {
            IsSuccess = true,
            Message = "Tải ảnh lên thành công.",
            Data = new MediaItemDto
            {
                Id = image.Id,
                EntityId = image.EntityId,
                EntityType = image.EntityType,
                Url = image.Url,
                FileName = image.FileName,
                FileSize = image.FileSize,
                MimeType = image.MimeType,
                IsPrimary = image.IsPrimary,
                DisplayOrder = image.DisplayOrder,
                CreatedAt = image.CreatedAt
            }
        };
    }

    public async Task<RestData<MediaItemDto>> UploadVideoAsync(IFormFile file, string entityType = "PRODUCT", Guid? entityId = null)
    {
        if (file == null || file.Length == 0)
            return new RestData<MediaItemDto> { IsSuccess = false, Message = "File video không hợp lệ." };

        if (file.Length > 100 * 1024 * 1024)
            return new RestData<MediaItemDto> { IsSuccess = false, Message = "Dung lượng video tối đa là 100MB." };

        var ext = Path.GetExtension(file.FileName).ToLower();
        var allowedExts = new[] { ".mp4", ".webm", ".mov", ".avi", ".mkv" };
        if (!allowedExts.Contains(ext))
            return new RestData<MediaItemDto> { IsSuccess = false, Message = "Định dạng video không được hỗ trợ." };

        var folderPath = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "videos");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(folderPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativeUrl = $"/uploads/videos/{fileName}";
        var video = new MediaVideo
        {
            Id = Guid.NewGuid(),
            EntityId = entityId,
            EntityType = entityType.ToUpper(),
            Url = relativeUrl,
            FileName = file.FileName,
            FileSize = file.Length,
            DurationSeconds = 0,
            DisplayOrder = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsDeleted = false
        };

        using var connection = _connectionFactory.CreateConnection();
        var sql = @"INSERT INTO MediaVideo (Id, EntityId, EntityType, Url, ThumbnailUrl, FileName, FileSize, DurationSeconds, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES (@Id, @EntityId, @EntityType, @Url, @ThumbnailUrl, @FileName, @FileSize, @DurationSeconds, @DisplayOrder, @CreatedAt, @UpdatedAt, @IsDeleted)";
        await connection.ExecuteAsync(sql, video);

        return new RestData<MediaItemDto>
        {
            IsSuccess = true,
            Message = "Tải video lên thành công.",
            Data = new MediaItemDto
            {
                Id = video.Id,
                EntityId = video.EntityId,
                EntityType = video.EntityType,
                Url = video.Url,
                FileName = video.FileName,
                FileSize = video.FileSize,
                DurationSeconds = video.DurationSeconds,
                DisplayOrder = video.DisplayOrder,
                CreatedAt = video.CreatedAt
            }
        };
    }

    public async Task<RestData<List<MediaItemDto>>> GetAllMediaAsync(string? entityType = null, string? mediaType = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = new List<MediaItemDto>();

        bool loadImages = string.IsNullOrWhiteSpace(mediaType) || mediaType.Equals("IMAGE", StringComparison.OrdinalIgnoreCase);
        bool loadVideos = string.IsNullOrWhiteSpace(mediaType) || mediaType.Equals("VIDEO", StringComparison.OrdinalIgnoreCase);

        if (loadImages)
        {
            var imgWhere = "IsDeleted = 0";
            var p = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(entityType))
            {
                imgWhere += " AND EntityType = @EntityType";
                p.Add("EntityType", entityType.ToUpper());
            }

            var images = await connection.QueryAsync<MediaImage>($"SELECT * FROM MediaImage WHERE {imgWhere} ORDER BY CreatedAt DESC", p);
            result.AddRange(images.Select(img => new MediaItemDto
            {
                Id = img.Id,
                EntityId = img.EntityId,
                EntityType = img.EntityType,
                Url = img.Url,
                FileName = img.FileName,
                FileSize = img.FileSize,
                MimeType = img.MimeType,
                IsPrimary = img.IsPrimary,
                DisplayOrder = img.DisplayOrder,
                CreatedAt = img.CreatedAt
            }));
        }

        if (loadVideos)
        {
            var vidWhere = "IsDeleted = 0";
            var p = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(entityType))
            {
                vidWhere += " AND EntityType = @EntityType";
                p.Add("EntityType", entityType.ToUpper());
            }

            var videos = await connection.QueryAsync<MediaVideo>($"SELECT * FROM MediaVideo WHERE {vidWhere} ORDER BY CreatedAt DESC", p);
            result.AddRange(videos.Select(vid => new MediaItemDto
            {
                Id = vid.Id,
                EntityId = vid.EntityId,
                EntityType = vid.EntityType,
                Url = vid.Url,
                ThumbnailUrl = vid.ThumbnailUrl,
                FileName = vid.FileName,
                FileSize = vid.FileSize,
                DurationSeconds = vid.DurationSeconds,
                DisplayOrder = vid.DisplayOrder,
                CreatedAt = vid.CreatedAt
            }));
        }

        result = result.OrderByDescending(x => x.CreatedAt).ToList();
        return new RestData<List<MediaItemDto>> { IsSuccess = true, Data = result };
    }

    public async Task<RestData<List<MediaItemDto>>> GetMediaByEntityAsync(Guid entityId, string entityType)
    {
        using var connection = _connectionFactory.CreateConnection();
        var images = await connection.QueryAsync<MediaImage>(
            "SELECT * FROM MediaImage WHERE EntityId = @EntityId AND EntityType = @EntityType AND IsDeleted = 0 ORDER BY DisplayOrder ASC, CreatedAt DESC",
            new { EntityId = entityId, EntityType = entityType.ToUpper() });

        var videos = await connection.QueryAsync<MediaVideo>(
            "SELECT * FROM MediaVideo WHERE EntityId = @EntityId AND EntityType = @EntityType AND IsDeleted = 0 ORDER BY DisplayOrder ASC, CreatedAt DESC",
            new { EntityId = entityId, EntityType = entityType.ToUpper() });

        var result = new List<MediaItemDto>();
        result.AddRange(images.Select(img => new MediaItemDto
        {
            Id = img.Id,
            EntityId = img.EntityId,
            EntityType = img.EntityType,
            Url = img.Url,
            FileName = img.FileName,
            FileSize = img.FileSize,
            MimeType = img.MimeType,
            IsPrimary = img.IsPrimary,
            DisplayOrder = img.DisplayOrder,
            CreatedAt = img.CreatedAt
        }));

        result.AddRange(videos.Select(vid => new MediaItemDto
        {
            Id = vid.Id,
            EntityId = vid.EntityId,
            EntityType = vid.EntityType,
            Url = vid.Url,
            ThumbnailUrl = vid.ThumbnailUrl,
            FileName = vid.FileName,
            FileSize = vid.FileSize,
            DurationSeconds = vid.DurationSeconds,
            DisplayOrder = vid.DisplayOrder,
            CreatedAt = vid.CreatedAt
        }));

        return new RestData<List<MediaItemDto>> { IsSuccess = true, Data = result };
    }

    public async Task<RestData<bool>> DeleteMediaAsync(Guid id, bool isVideo = false)
    {
        using var connection = _connectionFactory.CreateConnection();
        var table = isVideo ? "MediaVideo" : "MediaImage";
        await connection.ExecuteAsync($"UPDATE {table} SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE Id = @Id", new { Id = id });
        return new RestData<bool> { IsSuccess = true, Data = true, Message = "Xóa media thành công." };
    }

    public async Task<RestData<bool>> AttachMediaAsync(AttachMediaRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (request.ImageIds != null && request.ImageIds.Any())
        {
            await connection.ExecuteAsync(
                "UPDATE MediaImage SET EntityId = @EntityId, EntityType = @EntityType, UpdatedAt = GETDATE() WHERE Id IN @Ids",
                new { EntityId = request.EntityId, EntityType = request.EntityType.ToUpper(), Ids = request.ImageIds });
        }

        if (request.VideoIds != null && request.VideoIds.Any())
        {
            await connection.ExecuteAsync(
                "UPDATE MediaVideo SET EntityId = @EntityId, EntityType = @EntityType, UpdatedAt = GETDATE() WHERE Id IN @Ids",
                new { EntityId = request.EntityId, EntityType = request.EntityType.ToUpper(), Ids = request.VideoIds });
        }

        return new RestData<bool> { IsSuccess = true, Data = true };
    }
}
