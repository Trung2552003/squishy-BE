using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace SystemBase.BE.Middlewares
{
    /// <summary>
    /// Ảnh thu nhỏ theo yêu cầu: GET /uploads/images/abc.jpg?w=320 trả về bản WebP rộng tối đa 320px.
    /// Bản thu nhỏ tạo 1 lần rồi lưu ở uploads/_thumbs/{w}/..., các lần sau đọc thẳng từ đĩa.
    /// Ảnh gốc chụp điện thoại vài MB chỉ để hiện ô nhỏ trong danh sách là nguyên nhân chính khiến trang tải chậm.
    /// </summary>
    public class ThumbnailMiddleware
    {
        private static readonly int[] Widths = { 160, 320, 640, 1280 };
        private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        // Giải mã ảnh lớn tốn RAM: giới hạn số ảnh xử lý cùng lúc
        private static readonly SemaphoreSlim Gate = new(2);
        private static readonly WebpEncoder Encoder = new() { Quality = 80 };

        private readonly RequestDelegate _next;
        private readonly string _uploadsPath;
        private readonly ILogger<ThumbnailMiddleware> _logger;

        public ThumbnailMiddleware(RequestDelegate next, string uploadsPath, ILogger<ThumbnailMiddleware> logger)
        {
            _next = next;
            _uploadsPath = Path.GetFullPath(uploadsPath);
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            if (!(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
                || !request.Path.StartsWithSegments("/uploads", out var rest)
                || !int.TryParse(request.Query["w"], out var requested) || requested <= 0)
            {
                await _next(context);
                return;
            }

            var relative = rest.Value?.TrimStart('/') ?? "";
            var source = Path.GetFullPath(Path.Combine(_uploadsPath, relative));
            if (!source.StartsWith(_uploadsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !Extensions.Contains(Path.GetExtension(source).ToLowerInvariant())
                || !File.Exists(source))
            {
                await _next(context);
                return;
            }

            var width = Widths.FirstOrDefault(x => x >= requested, Widths[^1]);
            var thumb = Path.Combine(_uploadsPath, "_thumbs", width.ToString(), relative + ".webp");

            try
            {
                if (!IsFresh(thumb, source))
                    await CreateAsync(source, thumb, width, context.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Ảnh lỗi / định dạng lạ: trả ảnh gốc
                _logger.LogWarning(ex, "Không tạo được ảnh thu nhỏ cho {Path}", relative);
                await _next(context);
                return;
            }

            context.Response.ContentType = "image/webp";
            context.Response.Headers.CacheControl = "public, max-age=604800";
            context.Response.ContentLength = new FileInfo(thumb).Length;
            if (HttpMethods.IsGet(request.Method))
                await context.Response.SendFileAsync(thumb, context.RequestAborted);
        }

        private static bool IsFresh(string thumb, string source) =>
            File.Exists(thumb) && File.GetLastWriteTimeUtc(thumb) >= File.GetLastWriteTimeUtc(source);

        private static async Task CreateAsync(string source, string thumb, int width, CancellationToken ct)
        {
            await Gate.WaitAsync(ct);
            try
            {
                if (IsFresh(thumb, source)) return; // yêu cầu khác vừa tạo xong

                // Giải mã sẵn ở cỡ nhỏ (cạnh dài ≤ 2w) để không phải bung cả ảnh 12MP vào RAM
                var options = new DecoderOptions { TargetSize = new Size(width * 2, width * 2) };
                using var image = await Image.LoadAsync(options, source, ct);
                image.Mutate(x => x.AutoOrient()); // ảnh điện thoại xoay theo EXIF
                if (image.Width > width)
                    image.Mutate(x => x.Resize(width, 0)); // 0: giữ tỉ lệ, không phóng to ảnh nhỏ
                image.Metadata.ExifProfile = null;

                Directory.CreateDirectory(Path.GetDirectoryName(thumb)!);
                var temp = thumb + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await image.SaveAsync(temp, Encoder, ct);
                File.Move(temp, thumb, overwrite: true);
            }
            finally
            {
                Gate.Release();
            }
        }
    }
}
