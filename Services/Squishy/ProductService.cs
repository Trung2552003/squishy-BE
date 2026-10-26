using System;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
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

        await LoadVariantsAsync(connection, products);

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

        await LoadVariantsAsync(connection, list);
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

        await LoadVariantsAsync(connection, new[] { product });
        return new RestData<ProductDto> { IsSuccess = true, Data = product };
    }

    public async Task<RestData<ProductDto>> CreateAsync(CreateUpdateProductDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Product WHERE Sku = @Sku AND IsDeleted = 0", new { dto.Sku });
        if (exists > 0)
            return new RestData<ProductDto> { IsSuccess = false, Message = $"Mã SKU '{dto.Sku}' đã tồn tại." };

        var hasVariants = dto.Variants != null && dto.Variants.Count > 0;
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
            // Có phân loại màu: tồn kho = tổng tồn các màu (tính sau khi lưu màu)
            CurrentStock = hasVariants ? 0 : dto.CurrentStock,
            MinStockAlert = dto.MinStockAlert,
            SquishyType = dto.SquishyType,
            MainImageUrl = dto.MainImageUrl,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsDeleted = false
        };

        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(@"INSERT INTO Product (Id, CategoryId, Sku, Name, Description, Unit, CostPrice, SalePrice, CurrentStock, MinStockAlert, SquishyType, MainImageUrl, IsActive, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES (@Id, @CategoryId, @Sku, @Name, @Description, @Unit, @CostPrice, @SalePrice, @CurrentStock, @MinStockAlert, @SquishyType, @MainImageUrl, @IsActive, @CreatedAt, @UpdatedAt, @IsDeleted)",
                product, transaction);

            // Gắn ảnh/video đã tải lên vào sản phẩm (không tạo bản ghi mới để tránh trùng trong thư viện)
            await SyncProductMediaAsync(connection, transaction, product.Id, dto.ImageUrls, dto.VideoUrls);

            var variantError = await SyncVariantsAsync(connection, transaction, product.Id, dto.Variants, 0, dto.CostPrice);
            if (variantError != null)
            {
                transaction.Rollback();
                return new RestData<ProductDto> { IsSuccess = false, Message = variantError };
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
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

        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var current = await connection.QueryFirstOrDefaultAsync<Product>(
                "SELECT * FROM Product WITH (UPDLOCK) WHERE Id = @Id AND IsDeleted = 0", new { Id = id }, transaction);
            if (current == null)
            {
                transaction.Rollback();
                return new RestData<ProductDto> { IsSuccess = false, Message = "Không tìm thấy sản phẩm." };
            }

            // Đã / sẽ có phân loại màu: tồn kho và giá vốn do các màu quyết định, không ghi đè từ form
            var hadVariants = await Inventory.HasVariantsAsync(connection, transaction, id);
            var keepStock = hadVariants || (dto.Variants != null && dto.Variants.Count > 0);

            await connection.ExecuteAsync(@"UPDATE Product
                    SET CategoryId = @CategoryId, Sku = @Sku, Name = @Name, Description = @Description,
                        Unit = @Unit, SalePrice = @SalePrice, MinStockAlert = @MinStockAlert, SquishyType = @SquishyType,
                        MainImageUrl = @MainImageUrl, IsActive = @IsActive, UpdatedAt = GETDATE(),
                        CostPrice = CASE WHEN @KeepStock = 1 THEN CostPrice ELSE @CostPrice END,
                        CurrentStock = CASE WHEN @KeepStock = 1 THEN CurrentStock ELSE @CurrentStock END
                    WHERE Id = @Id",
                new
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
                    dto.IsActive,
                    KeepStock = keepStock
                }, transaction);

            // Cập nhật lại media nếu truyền vào danh sách
            await SyncProductMediaAsync(connection, transaction, id, dto.ImageUrls, dto.VideoUrls);

            var variantError = await SyncVariantsAsync(connection, transaction, id, dto.Variants,
                hadVariants ? 0 : current.CurrentStock, current.CostPrice);
            if (variantError != null)
            {
                transaction.Rollback();
                return new RestData<ProductDto> { IsSuccess = false, Message = variantError };
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return await GetByIdAsync(id);
    }

    /// <summary>
    /// Lưu danh sách màu của sản phẩm. Trả về lỗi hoặc null.
    /// - Màu mới: tạo với tồn đầu kỳ (ghi thẻ kho "OPENING").
    /// - Sản phẩm đang có hàng mà chưa chia màu: tồn hiện có chuyển sang màu đầu tiên ("VARIANT_SPLIT").
    /// - Màu bị bỏ khỏi danh sách: chỉ xóa được khi đã hết hàng.
    /// </summary>
    private static async Task<string?> SyncVariantsAsync(IDbConnection connection, IDbTransaction transaction, Guid productId,
        List<SaveProductVariantDto>? incoming, int stockToMove, decimal productCost)
    {
        if (incoming == null) return null;

        foreach (var v in incoming)
        {
            v.Name = (v.Name ?? "").Trim();
            v.ColorHex = string.IsNullOrWhiteSpace(v.ColorHex) ? null : v.ColorHex.Trim();
            v.ImageUrl = string.IsNullOrWhiteSpace(v.ImageUrl) ? null : v.ImageUrl.Trim();
            if (v.Name.Length == 0 || v.Name.Length > 50) return "Tên màu không được để trống (tối đa 50 ký tự).";
            if (v.ColorHex != null && !Regex.IsMatch(v.ColorHex, "^#[0-9A-Fa-f]{6}$"))
                return $"Mã màu của \"{v.Name}\" không hợp lệ.";
            if (v.InitialStock < 0) return $"Tồn đầu kỳ của \"{v.Name}\" không được âm.";
        }
        var dup = incoming.GroupBy(v => v.Name.ToLowerInvariant()).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) return $"Màu \"{dup.First().Name}\" bị trùng tên.";

        var existing = (await connection.QueryAsync<ProductVariant>(
            "SELECT * FROM ProductVariant WITH (UPDLOCK) WHERE ProductId = @Id AND IsDeleted = 0", new { Id = productId }, transaction)).ToList();

        var keepIds = incoming.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
        foreach (var removed in existing.Where(e => !keepIds.Contains(e.Id)))
        {
            if (removed.CurrentStock > 0)
                return $"Màu \"{removed.Name}\" còn {removed.CurrentStock} chiếc nên không xóa được. Hãy tắt \"Đang bán\" cho màu này.";
            await connection.ExecuteAsync("UPDATE ProductVariant SET IsDeleted = 1, IsActive = 0, UpdatedAt = GETDATE() WHERE Id = @Id",
                new { removed.Id }, transaction);
        }

        var moveStock = existing.Count == 0 && stockToMove > 0;
        var order = 1;
        foreach (var v in incoming)
        {
            Guid variantId;
            if (v.Id.HasValue)
            {
                if (!existing.Any(e => e.Id == v.Id)) return $"Màu \"{v.Name}\" không còn tồn tại, vui lòng tải lại trang.";
                variantId = v.Id.Value;
                await connection.ExecuteAsync(@"UPDATE ProductVariant SET Name = @Name, ColorHex = @ColorHex, ImageUrl = @ImageUrl,
                        DisplayOrder = @Order, IsActive = @IsActive, UpdatedAt = GETDATE() WHERE Id = @Id",
                    new { Id = variantId, v.Name, v.ColorHex, v.ImageUrl, Order = order, v.IsActive }, transaction);
            }
            else
            {
                variantId = Guid.NewGuid();
                await connection.ExecuteAsync(@"INSERT INTO ProductVariant (Id, ProductId, Name, ColorHex, ImageUrl, CurrentStock, CostPrice, DisplayOrder, IsActive, CreatedAt, UpdatedAt, IsDeleted)
                        VALUES (@Id, @ProductId, @Name, @ColorHex, @ImageUrl, 0, @Cost, @Order, @IsActive, GETDATE(), GETDATE(), 0)",
                    new { Id = variantId, ProductId = productId, v.Name, v.ColorHex, v.ImageUrl, Cost = productCost, Order = order, v.IsActive }, transaction);

                if (moveStock)
                {
                    await Inventory.ChangeAsync(connection, transaction, productId, variantId, stockToMove, "VARIANT_SPLIT", null,
                        $"Chuyển {stockToMove} chiếc tồn kho hiện có sang màu {v.Name}", Inventory.CostMode.Import, productCost);
                    moveStock = false;
                }
                if (v.InitialStock > 0)
                {
                    await Inventory.ChangeAsync(connection, transaction, productId, variantId, v.InitialStock, "OPENING", null,
                        $"Tồn đầu kỳ màu {v.Name}", Inventory.CostMode.Import, productCost);
                }
            }

            // Ảnh của màu: gắn vào màu để không hiện lẻ loi trong thư viện
            if (v.ImageUrl != null)
            {
                await connection.ExecuteAsync(@"UPDATE MediaImage SET EntityId = @Id, EntityType = 'PRODUCT_VARIANT', UpdatedAt = GETDATE()
                        WHERE Url = @Url AND IsDeleted = 0 AND EntityId IS NULL", new { Id = variantId, Url = v.ImageUrl }, transaction);
            }
            order++;
        }

        if (incoming.Count > 0)
            await Inventory.RecalcProductAsync(connection, transaction, productId);
        else if (existing.Count > 0)
            // Bỏ hết màu (đều đã hết hàng): sản phẩm quay về quản lý tồn kho chung, bắt đầu từ 0
            await connection.ExecuteAsync("UPDATE Product SET CurrentStock = 0, UpdatedAt = GETDATE() WHERE Id = @Id", new { Id = productId }, transaction);

        return null;
    }

    private static async Task LoadVariantsAsync(IDbConnection connection, IEnumerable<ProductDto> products)
    {
        var list = products.ToList();
        if (list.Count == 0) return;
        var variants = (await connection.QueryAsync<ProductVariantDto>(
            "SELECT * FROM ProductVariant WHERE ProductId IN @Ids AND IsDeleted = 0 ORDER BY DisplayOrder, Name",
            new { Ids = list.Select(p => p.Id).ToList() })).ToList();
        foreach (var p in list)
            p.Variants = variants.Where(v => v.ProductId == p.Id).ToList();
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
    private static async Task SyncProductMediaAsync(IDbConnection connection, IDbTransaction? transaction, Guid productId, List<string>? imageUrls, List<string>? videoUrls)
    {
        if (imageUrls != null)
            await SyncMediaTableAsync(connection, transaction, "MediaImage", productId, imageUrls, withPrimary: true);
        if (videoUrls != null)
            await SyncMediaTableAsync(connection, transaction, "MediaVideo", productId, videoUrls, withPrimary: false);
    }

    private static async Task SyncMediaTableAsync(IDbConnection connection, IDbTransaction? transaction, string table, Guid productId, List<string> urls, bool withPrimary)
    {
        var list = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList();

        // Gỡ những ảnh không còn trong danh sách
        await connection.ExecuteAsync(
            $"UPDATE {table} SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE EntityId = @Id AND EntityType = 'PRODUCT' AND IsDeleted = 0" +
            (list.Count > 0 ? " AND Url NOT IN @Urls" : ""),
            new { Id = productId, Urls = list }, transaction);

        var order = 1;
        foreach (var url in list)
        {
            var primarySet = withPrimary ? ", IsPrimary = @IsPrimary" : "";
            // Ưu tiên bản ghi đang gắn sản phẩm này, sau đó tới ảnh vừa tải lên chưa gắn vào đâu
            var existingId = await connection.ExecuteScalarAsync<Guid?>($@"
                SELECT TOP 1 Id FROM {table}
                WHERE Url = @Url AND EntityType = 'PRODUCT' AND IsDeleted = 0 AND (EntityId = @Id OR EntityId IS NULL)
                ORDER BY CASE WHEN EntityId = @Id THEN 0 ELSE 1 END, CreatedAt",
                new { Url = url, Id = productId }, transaction);

            if (existingId.HasValue)
            {
                await connection.ExecuteAsync(
                    $"UPDATE {table} SET EntityId = @Id, DisplayOrder = @Order{primarySet}, UpdatedAt = GETDATE() WHERE Id = @MediaId",
                    new { Id = productId, Order = order, IsPrimary = order == 1, MediaId = existingId.Value }, transaction);
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
                    new { Id = productId, Url = url, FileName = fileName, IsPrimary = order == 1, Order = order }, transaction);
            }
            order++;
        }
    }
}
