using System;
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

        // Lưu ảnh nếu có
        if (dto.ImageUrls != null && dto.ImageUrls.Any())
        {
            int order = 1;
            foreach (var url in dto.ImageUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    await connection.ExecuteAsync(
                        @"INSERT INTO MediaImage (Id, EntityId, EntityType, Url, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
                          VALUES (@Id, @EntityId, 'PRODUCT', @Url, @IsPrimary, @DisplayOrder, GETDATE(), GETDATE(), 0)",
                        new { Id = Guid.NewGuid(), EntityId = product.Id, Url = url, IsPrimary = (order == 1), DisplayOrder = order++ });
                }
            }
        }

        // Lưu video nếu có
        if (dto.VideoUrls != null && dto.VideoUrls.Any())
        {
            int order = 1;
            foreach (var url in dto.VideoUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    await connection.ExecuteAsync(
                        @"INSERT INTO MediaVideo (Id, EntityId, EntityType, Url, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
                          VALUES (@Id, @EntityId, 'PRODUCT', @Url, @DisplayOrder, GETDATE(), GETDATE(), 0)",
                        new { Id = Guid.NewGuid(), EntityId = product.Id, Url = url, DisplayOrder = order++ });
                }
            }
        }

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
        if (dto.ImageUrls != null)
        {
            await connection.ExecuteAsync("UPDATE MediaImage SET IsDeleted = 1 WHERE EntityId = @Id AND EntityType = 'PRODUCT'", new { Id = id });
            int order = 1;
            foreach (var url in dto.ImageUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    await connection.ExecuteAsync(
                        @"INSERT INTO MediaImage (Id, EntityId, EntityType, Url, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
                          VALUES (@Id, @EntityId, 'PRODUCT', @Url, @IsPrimary, @DisplayOrder, GETDATE(), GETDATE(), 0)",
                        new { Id = Guid.NewGuid(), EntityId = id, Url = url, IsPrimary = (order == 1), DisplayOrder = order++ });
                }
            }
        }

        if (dto.VideoUrls != null)
        {
            await connection.ExecuteAsync("UPDATE MediaVideo SET IsDeleted = 1 WHERE EntityId = @Id AND EntityType = 'PRODUCT'", new { Id = id });
            int order = 1;
            foreach (var url in dto.VideoUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    await connection.ExecuteAsync(
                        @"INSERT INTO MediaVideo (Id, EntityId, EntityType, Url, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
                          VALUES (@Id, @EntityId, 'PRODUCT', @Url, @DisplayOrder, GETDATE(), GETDATE(), 0)",
                        new { Id = Guid.NewGuid(), EntityId = id, Url = url, DisplayOrder = order++ });
                }
            }
        }

        return await GetByIdAsync(id);
    }

    public async Task<RestData<bool>> DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE Product SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE Id = @Id", new { Id = id });
        return new RestData<bool> { IsSuccess = true, Data = true, Message = "Xóa sản phẩm thành công." };
    }
}
