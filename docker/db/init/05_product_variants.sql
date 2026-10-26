-- =========================================================================================
-- Phân loại sản phẩm theo màu: mỗi màu có tồn kho, giá vốn và ảnh riêng (cùng giá bán).
-- Tồn kho của Product = tổng tồn các màu (được tính lại mỗi lần kho thay đổi).
-- Chạy lại được.
-- =========================================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.ProductVariant', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductVariant] (
        [Id]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ProductVariant] PRIMARY KEY DEFAULT NEWID(),
        [ProductId]    UNIQUEIDENTIFIER NOT NULL,
        [Name]         NVARCHAR(50)     NOT NULL,                       -- VD: Hồng, Xanh mint
        [ColorHex]     NVARCHAR(9)      NULL,                           -- VD: #F9A8D4 (ô màu hiển thị)
        [ImageUrl]     NVARCHAR(500)    NULL,
        [CurrentStock] INT              NOT NULL CONSTRAINT [DF_ProductVariant_Stock] DEFAULT 0,
        [CostPrice]    DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_ProductVariant_Cost] DEFAULT 0,
        [DisplayOrder] INT              NOT NULL CONSTRAINT [DF_ProductVariant_Order] DEFAULT 0,
        [IsActive]     BIT              NOT NULL CONSTRAINT [DF_ProductVariant_Active] DEFAULT 1,
        [CreatedAt]    DATETIME2        NOT NULL CONSTRAINT [DF_ProductVariant_CreatedAt] DEFAULT GETDATE(),
        [UpdatedAt]    DATETIME2        NOT NULL CONSTRAINT [DF_ProductVariant_UpdatedAt] DEFAULT GETDATE(),
        [IsDeleted]    BIT              NOT NULL CONSTRAINT [DF_ProductVariant_IsDeleted] DEFAULT 0,
        CONSTRAINT [FK_ProductVariant_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product]([Id]),
        CONSTRAINT [CK_ProductVariant_Stock] CHECK ([CurrentStock] >= 0)
    );
    CREATE INDEX [IX_ProductVariant_Product] ON [dbo].[ProductVariant]([ProductId], [IsDeleted]);
    PRINT N'Đã tạo bảng ProductVariant.';
END
GO

-- Màu của từng dòng hàng trong đơn / phiếu nhập / đổi trả / thẻ kho
IF COL_LENGTH('dbo.OrderDetail', 'VariantId') IS NULL
    ALTER TABLE [dbo].[OrderDetail] ADD [VariantId] UNIQUEIDENTIFIER NULL, [VariantName] NVARCHAR(50) NULL;
GO
IF COL_LENGTH('dbo.GoodsReceiptDetail', 'VariantId') IS NULL
    ALTER TABLE [dbo].[GoodsReceiptDetail] ADD [VariantId] UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH('dbo.ReturnRequestItem', 'VariantId') IS NULL
    ALTER TABLE [dbo].[ReturnRequestItem] ADD [VariantId] UNIQUEIDENTIFIER NULL, [ExchangeVariantId] UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH('dbo.StockTransaction', 'VariantId') IS NULL
    ALTER TABLE [dbo].[StockTransaction] ADD [VariantId] UNIQUEIDENTIFIER NULL;
GO
