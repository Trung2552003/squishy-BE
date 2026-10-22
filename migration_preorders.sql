-- =========================================================================================
-- MIGRATION: ĐẶT TRƯỚC HÀNG MỚI (PRE-ORDER)
-- Chạy được nhiều lần (idempotent). Không xóa hay sửa dữ liệu cũ.
-- Ảnh mẫu nằm tại wwwroot/uploads/preorder/*.jpg
-- =========================================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.PreorderItem', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PreorderItem] (
        [Id]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PreorderItem] PRIMARY KEY DEFAULT NEWID(),
        [Name]         NVARCHAR(200)    NOT NULL,
        [Price]        DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_PreorderItem_Price] DEFAULT 0,
        [ImageUrl]     NVARCHAR(500)    NULL,
        [Description]  NVARCHAR(1000)   NULL,
        [Tag]          NVARCHAR(20)     NOT NULL CONSTRAINT [DF_PreorderItem_Tag] DEFAULT 'HOT', -- HOT, NEW, NONE
        [DisplayOrder] INT              NOT NULL CONSTRAINT [DF_PreorderItem_Order] DEFAULT 0,
        [IsActive]     BIT              NOT NULL CONSTRAINT [DF_PreorderItem_Active] DEFAULT 1,
        [CreatedAt]    DATETIME2        NOT NULL CONSTRAINT [DF_PreorderItem_CreatedAt] DEFAULT GETDATE(),
        [UpdatedAt]    DATETIME2        NOT NULL CONSTRAINT [DF_PreorderItem_UpdatedAt] DEFAULT GETDATE(),
        [IsDeleted]    BIT              NOT NULL CONSTRAINT [DF_PreorderItem_IsDeleted] DEFAULT 0
    );
    PRINT N'Đã tạo bảng PreorderItem.';
END
GO

IF OBJECT_ID('dbo.Preorder', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Preorder] (
        [Id]              UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Preorder] PRIMARY KEY DEFAULT NEWID(),
        [Code]            NVARCHAR(50)     NOT NULL,
        [CustomerName]    NVARCHAR(100)    NOT NULL,
        [CustomerPhone]   NVARCHAR(20)     NOT NULL,
        [SocialContact]   NVARCHAR(200)    NULL,
        [ShippingAddress] NVARCHAR(500)    NULL,
        [Note]            NVARCHAR(500)    NULL,
        [TotalAmount]     DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_Preorder_Total] DEFAULT 0,
        [Status]          NVARCHAR(20)     NOT NULL CONSTRAINT [DF_Preorder_Status] DEFAULT 'NEW', -- NEW, CONTACTED, CONFIRMED, CANCELLED
        [AdminNote]       NVARCHAR(1000)   NULL,
        [CreatedAt]       DATETIME2        NOT NULL CONSTRAINT [DF_Preorder_CreatedAt] DEFAULT GETDATE(),
        [UpdatedAt]       DATETIME2        NOT NULL CONSTRAINT [DF_Preorder_UpdatedAt] DEFAULT GETDATE(),
        [IsDeleted]       BIT              NOT NULL CONSTRAINT [DF_Preorder_IsDeleted] DEFAULT 0
    );
    CREATE UNIQUE INDEX [IX_Preorder_Code] ON [dbo].[Preorder]([Code]);
    CREATE INDEX [IX_Preorder_Phone] ON [dbo].[Preorder]([CustomerPhone], [CreatedAt]);
    PRINT N'Đã tạo bảng Preorder.';
END
GO

IF OBJECT_ID('dbo.PreorderLine', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PreorderLine] (
        [Id]             UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_PreorderLine] PRIMARY KEY DEFAULT NEWID(),
        [PreorderId]     UNIQUEIDENTIFIER NOT NULL,
        [PreorderItemId] UNIQUEIDENTIFIER NOT NULL,
        [ItemName]       NVARCHAR(200)    NOT NULL,
        [UnitPrice]      DECIMAL(18, 2)   NOT NULL,
        [Quantity]       INT              NOT NULL,
        CONSTRAINT [FK_PreorderLine_Preorder] FOREIGN KEY ([PreorderId]) REFERENCES [dbo].[Preorder]([Id]),
        CONSTRAINT [FK_PreorderLine_Item] FOREIGN KEY ([PreorderItemId]) REFERENCES [dbo].[PreorderItem]([Id]),
        CONSTRAINT [CK_PreorderLine_Quantity] CHECK ([Quantity] > 0)
    );
    CREATE INDEX [IX_PreorderLine_Preorder] ON [dbo].[PreorderLine]([PreorderId]);
    CREATE INDEX [IX_PreorderLine_Item] ON [dbo].[PreorderLine]([PreorderItemId]);
    PRINT N'Đã tạo bảng PreorderLine.';
END
GO

-- Danh sách mẫu sắp về (từ file squishy.docx). "Đá bào" chưa có giá nên để ẩn.
IF NOT EXISTS (SELECT 1 FROM [dbo].[PreorderItem])
BEGIN
    INSERT INTO [dbo].[PreorderItem] ([Name], [Price], [ImageUrl], [Tag], [DisplayOrder], [IsActive]) VALUES
    (N'Phô mai 7cm',          60000, '/uploads/preorder/pho-mai-7cm.jpg',        'HOT',  1, 1),
    (N'Phô mai 6cm',          50000, '/uploads/preorder/pho-mai-6cm.jpg',        'HOT',  2, 1),
    (N'Phô mai trong suốt',   60000, '/uploads/preorder/pho-mai-trong-suot.jpg', 'NEW',  3, 1),
    (N'Phô mai hồng',         60000, '/uploads/preorder/pho-mai-hong.jpg',       'NEW',  4, 1),
    (N'Lạc rộp',              55000, '/uploads/preorder/lac-rop.jpg',            'HOT',  5, 1),
    (N'Hello Kitty bơ dẻo',   55000, '/uploads/preorder/hello-kitty-bo-deo.jpg', 'NEW',  6, 1),
    (N'Trứng múi',            60000, '/uploads/preorder/trung-mui.jpg',          'NEW',  7, 1),
    (N'Đá bào',                   0, '/uploads/preorder/da-bao.jpg',             'NONE', 8, 0),
    (N'Xà phòng rộp',         45000, '/uploads/preorder/xa-phong-rop.jpg',       'HOT',  9, 1),
    (N'Khoai dẻo',            50000, '/uploads/preorder/khoai-deo.jpg',          'HOT', 10, 1),
    (N'Khoai rộp',            45000, '/uploads/preorder/khoai-rop.jpg',          'HOT', 11, 1),
    (N'Xoài rộp',             45000, '/uploads/preorder/xoai-rop.jpg',           'HOT', 12, 1),
    (N'Stressball',           35000, '/uploads/preorder/stressball.jpg',         'HOT', 13, 1),
    (N'Măng cụt',             45000, '/uploads/preorder/mang-cut.jpg',           'HOT', 14, 1);
    PRINT N'Đã thêm 14 mẫu đặt trước (Đá bào đang ẩn).';
END
GO

-- Menu "Đặt Trước Hàng Mới" trong nhóm Bán Hàng & Giao Hàng
DECLARE @Now DATETIME2 = DATEADD(HOUR, 7, GETUTCDATE());
DECLARE @EmptyGuid UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @SalesGroupId UNIQUEIDENTIFIER = (
    SELECT TOP 1 [Id] FROM [dbo].[SystemFunction] WHERE [Code] = 'GROUP_SALES' AND [IsDeleted] = 0
);
IF @SalesGroupId IS NULL SET @SalesGroupId = @EmptyGuid;

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemFunction] WHERE [Code] = 'PREORDER_MANAGE')
    INSERT INTO [dbo].[SystemFunction]
        ([Id], [CreatedAt], [UpdatedAt], [UserCreated], [UserModified], [Code], [Name], [Url], [Order], [Type], [Icon], [IsShow], [ParentId], [IsDeleted])
    VALUES
        (NEWID(), @Now, @Now, @EmptyGuid, @EmptyGuid, 'PREORDER_MANAGE', N'Đặt Trước Hàng Mới', '/cms/preorders', 5, 'menu', 'ClipboardTextIcon', 1, @SalesGroupId, 0);
ELSE
    UPDATE [dbo].[SystemFunction] SET [Url] = '/cms/preorders', [IsDeleted] = 0, [IsShow] = 1, [UpdatedAt] = @Now
    WHERE [Code] = 'PREORDER_MANAGE';

-- Cấp quyền cho ADMIN và các vai trò đang xem được "Đơn hàng"
DECLARE @FuncId UNIQUEIDENTIFIER = (SELECT TOP 1 [Id] FROM [dbo].[SystemFunction] WHERE [Code] = 'PREORDER_MANAGE');
INSERT INTO [dbo].[SystemRoleFunctions] ([RoleId], [FunctionId])
SELECT DISTINCT r.[Id], @FuncId
FROM [dbo].[SystemRole] r
WHERE r.[IsDeleted] = 0
  AND (
      r.[Code] = 'ADMIN'
      OR EXISTS (
          SELECT 1 FROM [dbo].[SystemRoleFunctions] srf
          JOIN [dbo].[SystemFunction] f ON f.[Id] = srf.[FunctionId]
          WHERE srf.[RoleId] = r.[Id] AND f.[Url] = '/cms/orders' AND f.[IsDeleted] = 0
      )
  )
  AND NOT EXISTS (SELECT 1 FROM [dbo].[SystemRoleFunctions] x WHERE x.[RoleId] = r.[Id] AND x.[FunctionId] = @FuncId);

PRINT N'Hoàn tất migration Đặt trước.';
GO
