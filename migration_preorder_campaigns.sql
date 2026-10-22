-- =========================================================================================
-- MIGRATION: ĐỢT ĐẶT TRƯỚC (mỗi đợt một link riêng, chỉ hiện mẫu của đợt đó)
-- Chạy SAU migration_preorders.sql. Chạy được nhiều lần (idempotent).
-- =========================================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.PreorderCampaign', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PreorderCampaign] (
        [Id]          UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PreorderCampaign] PRIMARY KEY DEFAULT NEWID(),
        [Name]        NVARCHAR(200)    NOT NULL,
        [Slug]        NVARCHAR(80)     NOT NULL, -- phần cuối của link: /dat-truoc/{Slug}
        [Description] NVARCHAR(1000)   NULL,     -- lời nhắn hiện đầu trang cho khách
        [OpenAt]      DATETIME2        NULL,     -- NULL = mở ngay
        [CloseAt]     DATETIME2        NULL,     -- NULL = không tự đóng
        [IsActive]    BIT              NOT NULL CONSTRAINT [DF_PreorderCampaign_Active] DEFAULT 1,
        [CreatedAt]   DATETIME2        NOT NULL CONSTRAINT [DF_PreorderCampaign_CreatedAt] DEFAULT GETDATE(),
        [UpdatedAt]   DATETIME2        NOT NULL CONSTRAINT [DF_PreorderCampaign_UpdatedAt] DEFAULT GETDATE(),
        [IsDeleted]   BIT              NOT NULL CONSTRAINT [DF_PreorderCampaign_IsDeleted] DEFAULT 0
    );
    CREATE UNIQUE INDEX [IX_PreorderCampaign_Slug] ON [dbo].[PreorderCampaign]([Slug]);
    PRINT N'Đã tạo bảng PreorderCampaign.';
END
GO

IF OBJECT_ID('dbo.PreorderCampaignItem', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PreorderCampaignItem] (
        [CampaignId]     UNIQUEIDENTIFIER NOT NULL,
        [PreorderItemId] UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT [PK_PreorderCampaignItem] PRIMARY KEY ([CampaignId], [PreorderItemId]),
        CONSTRAINT [FK_PreorderCampaignItem_Campaign] FOREIGN KEY ([CampaignId]) REFERENCES [dbo].[PreorderCampaign]([Id]),
        CONSTRAINT [FK_PreorderCampaignItem_Item] FOREIGN KEY ([PreorderItemId]) REFERENCES [dbo].[PreorderItem]([Id])
    );
    PRINT N'Đã tạo bảng PreorderCampaignItem.';
END
GO

IF COL_LENGTH('dbo.Preorder', 'CampaignId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Preorder] ADD [CampaignId] UNIQUEIDENTIFIER NULL;
    PRINT N'Đã thêm cột Preorder.CampaignId.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Preorder_Campaign' AND object_id = OBJECT_ID('dbo.Preorder'))
    CREATE INDEX [IX_Preorder_Campaign] ON [dbo].[Preorder]([CampaignId], [CreatedAt]);
GO

-- Đợt đầu tiên: gom các mẫu đang mở và các đơn đã có từ trước
IF NOT EXISTS (SELECT 1 FROM [dbo].[PreorderCampaign])
BEGIN
    DECLARE @CampaignId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO [dbo].[PreorderCampaign] ([Id], [Name], [Slug], [Description], [IsActive])
    VALUES (@CampaignId, N'Đợt hàng sắp về', 'dot-1',
            N'Các bé squishy dưới đây đang trên đường về shop. Đặt trước để shop giữ hàng cho bạn nhé!', 1);

    INSERT INTO [dbo].[PreorderCampaignItem] ([CampaignId], [PreorderItemId])
    SELECT @CampaignId, [Id] FROM [dbo].[PreorderItem] WHERE [IsDeleted] = 0 AND [IsActive] = 1;

    UPDATE [dbo].[Preorder] SET [CampaignId] = @CampaignId WHERE [CampaignId] IS NULL;
    PRINT N'Đã tạo đợt đầu tiên (link /dat-truoc/dot-1).';
END
GO

PRINT N'Hoàn tất migration Đợt đặt trước.';
GO
