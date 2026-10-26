using System;
using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SystemBase.BE.DTOs.Shared;
using SystemBase.BE.DTOs.Squishy;
using SystemBase.BE.Infrastructure.Dapper;
using SystemBase.BE.Models.SquishyManagement;

namespace SystemBase.BE.Services.Squishy;

public interface IProductService
{
    Task<RestData<PagedResult<ProductDto>>> GetPagedAsync(ProductFilterRequest request);
    Task<RestData<List<ProductDto>>> GetAllActiveAsync(Guid? categoryId = null);
    Task<RestData<ProductDto>> GetByIdAsync(Guid id);
    Task<RestData<ProductDto>> CreateAsync(CreateUpdateProductDto dto);
    Task<RestData<ProductDto>> UpdateAsync(Guid id, CreateUpdateProductDto dto);
    Task<RestData<bool>> DeleteAsync(Guid id);
}

public class ProductService : IProductService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RestData<PagedResult<ProductDto>>> GetPagedAsync(ProductFilterRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        var whereClause = "p.IsDeleted = 0";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            whereClause += " AND (LOWER(p.Name) LIKE @Keyword OR LOWER(p.Sku) LIKE @Keyword OR LOWER(p.SquishyType) LIKE @Keyword)";
            parameters.Add("Keyword", $"%{request.Keyword.ToLower()}%");
        }

        if (request.CategoryId.HasValue && request.CategoryId != Guid.Empty)
        {
            whereClause += " AND p.CategoryId = @CategoryId";
            parameters.Add("CategoryId", request.CategoryId.Value);
        }

        if (request.LowStockOnly == true)
        {
            whereClause += " AND p.CurrentStock <= p.MinStockAlert";
        }

        var countSql = $"SELECT COUNT(1) FROM Product p WHERE {whereClause}";
        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var offset = (request.PageIndex - 1) * request.PageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", request.PageSize);

        var dataSql = $@"
            SELECT p.*, c.Name AS CategoryName
            FROM Product p
            LEFT JOIN Category c ON p.CategoryId = c.Id
            WHERE {whereClause}
            ORDER BY p.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var products = (await connection.QueryAsync<ProductDto>(dataSql, parameters)).ToList();

        // Load media (images, videos) for each product
        if (products.Any())
        {
            var productIds = products.Select(p => p.Id).ToList();
            var images = (await connection.QueryAsync<MediaImage>(
                "SELECT * FROM MediaImage WHERE EntityId IN @Ids AND EntityType = 'PRODUCT' AND IsDeleted = 0 ORDER BY DisplayOrder ASC",
                new { Ids = productIds })).ToList();

            var videos = (await connection.QueryAsync<MediaVideo>(
                "SELECT * FROM MediaVideo WHERE EntityId IN @Ids AND EntityType = 'PRODUCT' AND IsDeleted = 0 ORDER BY DisplayOrder ASC",
                new { Ids = productIds })).ToList();

            foreach (var p in products)
            {
                p.Images = images.Where(img => img.EntityId == p.Id).Select(img => new MediaItemDto
                {
                    Id = img.Id,
                    EntityId = img.EntityId,
                    EntityType = img.EntityType,
                    Url = img.Url,
                    FileName = img.FileName,
                    IsPrimary = img.IsPrimary,
                    DisplayOrder = img.DisplayOrder,
                    CreatedAt = img.CreatedAt
                }).ToList();

                p.Videos = videos.Where(vid => vid.EntityId == p.Id).Select(vid => new MediaItemDto
                {
                    Id = vid.Id,
                    EntityId = vid.EntityId,
                    EntityType = vid.EntityType,
                    Url = vid.Url,
                    ThumbnailUrl = vid.ThumbnailUrl,
                    FileName = vid.FileName,
                    DurationSeconds = vid.DurationSeconds,
                    DisplayOrder = vid.DisplayOrder,
                    CreatedAt = vid.CreatedAt
                }).ToList();
            }
        }

        var result = new PagedResult<ProductDto>
        {
            Items = products,
            TotalCount = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };

        return new RestData<PagedResult<ProductDto>> { IsSuccess = true, Data = result };
    }

    public async Task<RestData<List<ProductDto>>> GetAllActiveAsync(Guid? categoryId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var whereClause = "p.IsDeleted = 0 AND p.IsActive = 1";
        var parameters = new DynamicParameters();

        if (categoryId.HasValue && categoryId != Guid.Empty)
        {
            whereClause += " AND p.CategoryId = @CategoryId";
            parameters.Add("CategoryId", categoryId.Value);
        }

        var sql = $@"
            SELECT p.*, c.Name AS CategoryName
            FROM Product p
            LEFT JOIN Category c ON p.CategoryId = c.Id
            WHERE {whereClause}
            ORDER BY p.Name ASC";

        var list = (await connection.QueryAsync<ProductDto>(sql, parameters)).ToList();

        if (list.Any())
        {
            var productIds = list.Select(p => p.Id).ToList();
            var images = (await connection.QueryAsync<MediaImage>(
                "SELECT * FROM MediaImage WHERE EntityId IN @Ids AND EntityType = 'PRODUCT' AND IsDeleted = 0 ORDER BY DisplayOrder ASC",
                new { Ids = productIds })).ToList();

            var videos = (await connection.QueryAsync<MediaVideo>(
                "SELECT * FROM MediaVideo WHERE EntityId IN @Ids AND EntityType = 'PRODUCT' AND IsDeleted = 0 ORDER BY DisplayOrder ASC",
                new { Ids = productIds })).ToList();

            foreach (var p in list)
            {
                p.Images = images.Where(img => img.EntityId == p.Id).Select(img => new MediaItemDto
                {
                    Id = img.Id,
                    EntityId = img.EntityId,
                    EntityType = img.EntityType,
                    Url = img.Url,
                    FileName = img.FileName,
                    IsPrimary = img.IsPrimary,
                    DisplayOrder = img.DisplayOrder,
                    CreatedAt = img.CreatedAt
                }).ToList();

                p.Videos = videos.Where(vid => vid.EntityId == p.Id).Select(vid => new MediaItemDto
                {
                    Id = vid.Id,
                    EntityId = vid.EntityId,
                    EntityType = vid.EntityType,
                    Url = vid.Url,
                    ThumbnailUrl = vid.ThumbnailUrl,
                    FileName = vid.FileName,
                    DurationSeconds = vid.DurationSeconds,
                    DisplayOrder = vid.DisplayOrder,
                    CreatedAt = vid.CreatedAt
                }).ToList();
            }
        }

        return new RestData<List<ProductDto>> { IsSuccess = true, Data = list };
    }

    public async Task<RestData<ProductDto>> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT p.*, c.Name AS CategoryName
            FROM Product p
            LEFT JOIN Category c ON p.CategoryId = c.Id
            WHERE p.Id = @Id AND p.IsDeleted = 0";

        var product = await connection.QueryFirstOrDefaultAsync<ProductDto>(sql, new { Id = id });
        if (product == null)
            return new RestData<ProductDto> { IsSuccess = false, Message = "Không tìm thấy sản phẩm." };

        var images = await connection.QueryAsync<MediaImage>(
            "SELECT * FROM MediaImage WHERE EntityId = @Id AND EntityType = 'PRODUCT' AND IsDeleted = 0 ORDER BY DisplayOrder ASC",
            new { Id = id });

        var videos = await connection.QueryAsync<MediaVideo>(
            "SELECT * FROM MediaVideo WHERE EntityId = @Id AND EntityType = 'PRODUCT' AND IsDeleted = 0 ORDER BY DisplayOrder ASC",
            new { Id = id });

        product.Images = images.Select(img => new MediaItemDto
        {
            Id = img.Id,
            EntityId = img.EntityId,
            EntityType = img.EntityType,
            Url = img.Url,
            FileName = img.FileName,
            IsPrimary = img.IsPrimary,
            DisplayOrder = img.DisplayOrder,
            CreatedAt = img.CreatedAt
        }).ToList();

        product.Videos = videos.Select(vid => new MediaItemDto
        {
            Id = vid.Id,
            EntityId = vid.EntityId,
            EntityType = vid.EntityType,
            Url = vid.Url,
            ThumbnailUrl = vid.ThumbnailUrl,
            FileName = vid.FileName,
            DurationSeconds = vid.DurationSeconds,
            DisplayOrder = vid.DisplayOrder,
            CreatedAt = vid.CreatedAt
        }).ToList();

        return new RestData<ProductDto> { IsSuccess = true, Data = product };
    }

    public async Task<RestData<ProductDto>> CreateAsync(CreateUpdateProductDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Product WHERE Sku = @Sku AND IsDeleted = 0", new { dto.Sku });
        if (exists > 0)
            return new RestData<ProductDto> { IsSuccess = false, Message = $"Mã SKU '{dto.Sku}' đã tồn tại." };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = dto.CategoryId,
            Sku = dto.Sku.Trim().ToUpper(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "Chiếc" : dto.Unit.Trim(),
            CostPrice = dto.CostPrice,
            SalePrice = dto.SalePrice,
            CurrentStock = dto.CurrentStock,
            MinStockAlert = dto.MinStockAlert,
            SquishyType = dto.SquishyType,
            MainImageUrl = dto.MainImageUrl,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsDeleted = false
        };

        var sql = @"INSERT INTO Product (Id, CategoryId, Sku, Name, Description, Unit, CostPrice, SalePrice, CurrentStock, MinStockAlert, SquishyType, MainImageUrl, IsActive, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES (@Id, @CategoryId, @Sku, @Name, @Description, @Unit, @CostPrice, @SalePrice, @CurrentStock, @MinStockAlert, @SquishyType, @MainImageUrl, @IsActive, @CreatedAt, @UpdatedAt, @IsDeleted)";
        await connection.ExecuteAsync(sql, product);

        // Gắn ảnh/video đã tải lên vào sản phẩm (không tạo bản ghi mới để tránh trùng trong thư viện)
        await SyncProductMediaAsync(connection, product.Id, dto.ImageUrls, dto.VideoUrls);

        return await GetByIdAsync(product.Id);
    }

    public async Task<RestData<ProductDto>> UpdateAsync(Guid id, CreateUpdateProductDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Product WHERE Sku = @Sku AND Id <> @Id AND IsDeleted = 0", new { dto.Sku, Id = id });
        if (exists > 0)
            return new RestData<ProductDto> { IsSuccess = false, Message = $"Mã SKU '{dto.Sku}' đã thuộc về sản phẩm khác." };

        var sql = @"UPDATE Product 
                    SET CategoryId = @CategoryId, Sku = @Sku, Name = @Name, Description = @Description,
                        Unit = @Unit, CostPrice = @CostPrice, SalePrice = @SalePrice, CurrentStock = @CurrentStock,
                        MinStockAlert = @MinStockAlert, SquishyType = @SquishyType, MainImageUrl = @MainImageUrl,
                        IsActive = @IsActive, UpdatedAt = GETDATE()
                    WHERE Id = @Id AND IsDeleted = 0";

        var affected = await connection.ExecuteAsync(sql, new
        {
            Id = id,
            dto.CategoryId,
            Sku = dto.Sku.Trim().ToUpper(),
            Name = dto.Name.Trim(),
            dto.Description,
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "Chiếc" : dto.Unit.Trim(),
            dto.CostPrice,
            dto.SalePrice,
            dto.CurrentStock,
            dto.MinStockAlert,
            dto.SquishyType,
            dto.MainImageUrl,
            dto.IsActive
        });

        if (affected == 0)
            return new RestData<ProductDto> { IsSuccess = false, Message = "Không tìm thấy sản phẩm." };

        // Cập nhật lại media nếu truyền vào danh sách
        await SyncProductMediaAsync(connection, id, dto.ImageUrls, dto.VideoUrls);

        return await GetByIdAsync(id);
    }

    public async Task<RestData<bool>> DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE Product SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE Id = @Id", new { Id = id });
        return new RestData<bool> { IsSuccess = true, Data = true, Message = "Xóa sản phẩm thành công." };
    }

    /// <summary>
    /// Đồng bộ ảnh/video của sản phẩm theo danh sách URL (thứ tự = thứ tự hiển thị, ảnh đầu là ảnh chính).
    /// Ảnh tải lên từ form đã có sẵn 1 bản ghi trong thư viện: gắn lại chính bản ghi đó thay vì thêm bản mới,
    /// nên thư viện không bị nhân đôi. Ảnh không còn trong danh sách thì gỡ khỏi sản phẩm.
    /// null = không đổi media (giữ nguyên).
    /// </summary>
    private static async Task SyncProductMediaAsync(IDbConnection connection, Guid productId, List<string>? imageUrls, List<string>? videoUrls)
    {
        if (imageUrls != null)
            await SyncMediaTableAsync(connection, "MediaImage", productId, imageUrls, withPrimary: true);
        if (videoUrls != null)
            await SyncMediaTableAsync(connection, "MediaVideo", productId, videoUrls, withPrimary: false);
    }

    private static async Task SyncMediaTableAsync(IDbConnection connection, string table, Guid productId, List<string> urls, bool withPrimary)
    {
        var list = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList();

        // Gỡ những ảnh không còn trong danh sách
        await connection.ExecuteAsync(
            $"UPDATE {table} SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE EntityId = @Id AND EntityType = 'PRODUCT' AND IsDeleted = 0" +
            (list.Count > 0 ? " AND Url NOT IN @Urls" : ""),
            new { Id = productId, Urls = list });

        var order = 1;
        foreach (var url in list)
        {
            var primarySet = withPrimary ? ", IsPrimary = @IsPrimary" : "";
            // Ưu tiên bản ghi đang gắn sản phẩm này, sau đó tới ảnh vừa tải lên chưa gắn vào đâu
            var existingId = await connection.ExecuteScalarAsync<Guid?>($@"
                SELECT TOP 1 Id FROM {table}
                WHERE Url = @Url AND EntityType = 'PRODUCT' AND IsDeleted = 0 AND (EntityId = @Id OR EntityId IS NULL)
                ORDER BY CASE WHEN EntityId = @Id THEN 0 ELSE 1 END, CreatedAt",
                new { Url = url, Id = productId });

            if (existingId.HasValue)
            {
                await connection.ExecuteAsync(
                    $"UPDATE {table} SET EntityId = @Id, DisplayOrder = @Order{primarySet}, UpdatedAt = GETDATE() WHERE Id = @MediaId",
                    new { Id = productId, Order = order, IsPrimary = order == 1, MediaId = existingId.Value });
            }
            else
            {
                // URL nhập tay / ảnh cũ không có bản ghi: tạo mới, lấy tên file từ đường dẫn
                var fileName = Path.GetFileName(url.Split('?')[0]);
                await connection.ExecuteAsync(withPrimary
                    ? @"INSERT INTO MediaImage (Id, EntityId, EntityType, Url, FileName, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
                        VALUES (NEWID(), @Id, 'PRODUCT', @Url, @FileName, @IsPrimary, @Order, GETDATE(), GETDATE(), 0)"
                    : @"INSERT INTO MediaVideo (Id, EntityId, EntityType, Url, FileName, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
                        VALUES (NEWID(), @Id, 'PRODUCT', @Url, @FileName, @Order, GETDATE(), GETDATE(), 0)",
                    new { Id = productId, Url = url, FileName = fileName, IsPrimary = order == 1, Order = order });
            }
            order++;
        }
    }
}
