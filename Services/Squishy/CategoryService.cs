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

public interface ICategoryService
{
    Task<RestData<List<CategoryDto>>> GetAllAsync();
    Task<RestData<CategoryDto>> GetByIdAsync(Guid id);
    Task<RestData<CategoryDto>> CreateAsync(CreateUpdateCategoryDto dto);
    Task<RestData<CategoryDto>> UpdateAsync(Guid id, CreateUpdateCategoryDto dto);
    Task<RestData<bool>> DeleteAsync(Guid id);
}

public class CategoryService : ICategoryService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CategoryService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RestData<List<CategoryDto>>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT c.*, 
                   (SELECT COUNT(1) FROM Product p WHERE p.CategoryId = c.Id AND p.IsDeleted = 0) AS ProductCount
            FROM Category c
            WHERE c.IsDeleted = 0
            ORDER BY c.DisplayOrder ASC, c.CreatedAt DESC";

        var list = (await connection.QueryAsync<CategoryDto>(sql)).ToList();
        return new RestData<List<CategoryDto>> { IsSuccess = true, Data = list };
    }

    public async Task<RestData<CategoryDto>> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = @"
            SELECT c.*, 
                   (SELECT COUNT(1) FROM Product p WHERE p.CategoryId = c.Id AND p.IsDeleted = 0) AS ProductCount
            FROM Category c
            WHERE c.Id = @Id AND c.IsDeleted = 0";

        var category = await connection.QueryFirstOrDefaultAsync<CategoryDto>(sql, new { Id = id });
        if (category == null)
            return new RestData<CategoryDto> { IsSuccess = false, Message = "Không tìm thấy danh mục." };

        return new RestData<CategoryDto> { IsSuccess = true, Data = category };
    }

    public async Task<RestData<CategoryDto>> CreateAsync(CreateUpdateCategoryDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Category WHERE Code = @Code AND IsDeleted = 0", new { dto.Code });
        if (exists > 0)
            return new RestData<CategoryDto> { IsSuccess = false, Message = $"Mã danh mục '{dto.Code}' đã tồn tại." };

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Code = dto.Code.Trim().ToUpper(),
            Name = dto.Name.Trim(),
            Slug = dto.Slug ?? dto.Name.ToLower().Replace(" ", "-"),
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsDeleted = false
        };

        var sql = @"INSERT INTO Category (Id, Code, Name, Slug, Description, ImageUrl, DisplayOrder, IsActive, CreatedAt, UpdatedAt, IsDeleted)
                    VALUES (@Id, @Code, @Name, @Slug, @Description, @ImageUrl, @DisplayOrder, @IsActive, @CreatedAt, @UpdatedAt, @IsDeleted)";
        await connection.ExecuteAsync(sql, category);

        return await GetByIdAsync(category.Id);
    }

    public async Task<RestData<CategoryDto>> UpdateAsync(Guid id, CreateUpdateCategoryDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Category WHERE Code = @Code AND Id <> @Id AND IsDeleted = 0", new { dto.Code, Id = id });
        if (exists > 0)
            return new RestData<CategoryDto> { IsSuccess = false, Message = $"Mã danh mục '{dto.Code}' đã được sử dụng." };

        var sql = @"UPDATE Category 
                    SET Code = @Code, Name = @Name, Slug = @Slug, Description = @Description,
                        ImageUrl = @ImageUrl, DisplayOrder = @DisplayOrder, IsActive = @IsActive, UpdatedAt = GETDATE()
                    WHERE Id = @Id AND IsDeleted = 0";

        var affected = await connection.ExecuteAsync(sql, new
        {
            Id = id,
            Code = dto.Code.Trim().ToUpper(),
            Name = dto.Name.Trim(),
            Slug = dto.Slug,
            dto.Description,
            dto.ImageUrl,
            dto.DisplayOrder,
            dto.IsActive
        });

        if (affected == 0)
            return new RestData<CategoryDto> { IsSuccess = false, Message = "Không tìm thấy danh mục để cập nhật." };

        return await GetByIdAsync(id);
    }

    public async Task<RestData<bool>> DeleteAsync(Guid id)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE Category SET IsDeleted = 1, UpdatedAt = GETDATE() WHERE Id = @Id", new { Id = id });
        return new RestData<bool> { IsSuccess = true, Data = true, Message = "Xóa danh mục thành công." };
    }
}
