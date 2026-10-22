-- =========================================================================================
-- CẤU TRÚC DATABASE ĐẦY ĐỦ (sinh tự động từ database đang chạy ngày 22/10/2026).
-- Tạo mọi bảng, chỉ mục, khóa ngoại nếu chưa có. Chạy lại nhiều lần không ảnh hưởng dữ liệu.
-- =========================================================================================
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.ActionLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ActionLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [UserName] nvarchar(100) NOT NULL CONSTRAINT [DF_ActionLogs_UserName] DEFAULT (''),
        [Action] nvarchar(50) NOT NULL CONSTRAINT [DF_ActionLogs_Action] DEFAULT (''),
        [Module] nvarchar(100) NOT NULL CONSTRAINT [DF_ActionLogs_Module] DEFAULT (''),
        [Description] nvarchar(500) NOT NULL CONSTRAINT [DF_ActionLogs_Description] DEFAULT (''),
        [IpAddress] nvarchar(50) NOT NULL CONSTRAINT [DF_ActionLogs_IpAddress] DEFAULT (''),
        [CreatedAt] datetime2(7) NOT NULL,
        CONSTRAINT [PK_ActionLogs] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng ActionLogs';
END
GO

IF OBJECT_ID(N'dbo.CashTransaction', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CashTransaction] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [Code] nvarchar(30) NOT NULL,
        [Type] nvarchar(10) NOT NULL,
        [Category] nvarchar(30) NOT NULL,
        [Amount] decimal(18, 2) NOT NULL,
        [TransactionDate] datetime2(7) NOT NULL,
        [PaymentMethod] nvarchar(20) NOT NULL CONSTRAINT [DF_CashTransaction_Method] DEFAULT ('CASH'),
        [PartyName] nvarchar(200) NULL,
        [Description] nvarchar(1000) NULL,
        [CreatedBy] nvarchar(100) NULL,
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_CashTransaction_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_CashTransaction_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_CashTransaction_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_CashTransaction] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_CashTransaction_Amount] CHECK ([Amount]>(0)),
        CONSTRAINT [CK_CashTransaction_Type] CHECK ([Type]='INCOME' OR [Type]='EXPENSE')
    );
    PRINT N'Đã tạo bảng CashTransaction';
END
GO

IF OBJECT_ID(N'dbo.Category', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Category] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [Code] nvarchar(100) NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Slug] nvarchar(255) NULL,
        [Description] nvarchar(1000) NULL,
        [ImageUrl] nvarchar(MAX) NULL,
        [DisplayOrder] int NOT NULL CONSTRAINT [DF_Category_DisplayOrder] DEFAULT ((0)),
        [IsActive] bit NOT NULL CONSTRAINT [DF_Category_IsActive] DEFAULT ((1)),
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Category_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Category_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_Category_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_Category] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng Category';
END
GO

IF OBJECT_ID(N'dbo.ErrorLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ErrorLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [UserName] nvarchar(100) NOT NULL CONSTRAINT [DF_ErrorLogs_UserName] DEFAULT (''),
        [Message] nvarchar(1000) NOT NULL CONSTRAINT [DF_ErrorLogs_Message] DEFAULT (''),
        [StackTrace] nvarchar(MAX) NOT NULL CONSTRAINT [DF_ErrorLogs_StackTrace] DEFAULT (''),
        [Path] nvarchar(500) NOT NULL CONSTRAINT [DF_ErrorLogs_Path] DEFAULT (''),
        [IpAddress] nvarchar(50) NOT NULL CONSTRAINT [DF_ErrorLogs_IpAddress] DEFAULT (''),
        [CreatedAt] datetime2(7) NOT NULL,
        CONSTRAINT [PK_ErrorLogs] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng ErrorLogs';
END
GO

IF OBJECT_ID(N'dbo.GoodsReceipt', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GoodsReceipt] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [Code] nvarchar(100) NOT NULL,
        [SupplierId] uniqueidentifier NULL,
        [ReceiptDate] datetime2(7) NOT NULL CONSTRAINT [DF_GoodsReceipt_Date] DEFAULT (getdate()),
        [TotalAmount] decimal(18, 2) NOT NULL CONSTRAINT [DF_GoodsReceipt_Total] DEFAULT ((0)),
        [Status] nvarchar(50) NOT NULL CONSTRAINT [DF_GoodsReceipt_Status] DEFAULT ('COMPLETED'),
        [Note] nvarchar(MAX) NULL,
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_GoodsReceipt_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_GoodsReceipt_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_GoodsReceipt_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_GoodsReceipt] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng GoodsReceipt';
END
GO

IF OBJECT_ID(N'dbo.GoodsReceiptDetail', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[GoodsReceiptDetail] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [GoodsReceiptId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18, 2) NOT NULL,
        [TotalPrice] decimal(18, 2) NOT NULL,
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_GoodsReceiptDetail_CreatedAt] DEFAULT (getdate()),
        CONSTRAINT [PK_GoodsReceiptDetail] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng GoodsReceiptDetail';
END
GO

IF OBJECT_ID(N'dbo.LoginLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LoginLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [UserName] nvarchar(100) NOT NULL CONSTRAINT [DF_LoginLogs_UserName] DEFAULT (''),
        [IpAddress] nvarchar(50) NOT NULL CONSTRAINT [DF_LoginLogs_IpAddress] DEFAULT (''),
        [UserAgent] nvarchar(500) NOT NULL CONSTRAINT [DF_LoginLogs_UserAgent] DEFAULT (''),
        [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_LoginLogs_Status] DEFAULT (''),
        [Message] nvarchar(255) NOT NULL CONSTRAINT [DF_LoginLogs_Message] DEFAULT (''),
        [CreatedAt] datetime2(7) NOT NULL,
        CONSTRAINT [PK_LoginLogs] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng LoginLogs';
END
GO

IF OBJECT_ID(N'dbo.MediaImage', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MediaImage] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [EntityId] uniqueidentifier NULL,
        [EntityType] nvarchar(50) NOT NULL,
        [Url] nvarchar(MAX) NOT NULL,
        [FileName] nvarchar(500) NULL,
        [FileSize] bigint NULL,
        [MimeType] nvarchar(100) NULL,
        [IsPrimary] bit NOT NULL CONSTRAINT [DF_MediaImage_IsPrimary] DEFAULT ((0)),
        [DisplayOrder] int NOT NULL CONSTRAINT [DF_MediaImage_DisplayOrder] DEFAULT ((0)),
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MediaImage_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MediaImage_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_MediaImage_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_MediaImage] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng MediaImage';
END
GO

IF OBJECT_ID(N'dbo.MediaVideo', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MediaVideo] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [EntityId] uniqueidentifier NULL,
        [EntityType] nvarchar(50) NOT NULL,
        [Url] nvarchar(MAX) NOT NULL,
        [ThumbnailUrl] nvarchar(MAX) NULL,
        [FileName] nvarchar(500) NULL,
        [FileSize] bigint NULL,
        [DurationSeconds] int NULL,
        [DisplayOrder] int NOT NULL CONSTRAINT [DF_MediaVideo_DisplayOrder] DEFAULT ((0)),
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MediaVideo_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MediaVideo_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_MediaVideo_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_MediaVideo] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng MediaVideo';
END
GO

IF OBJECT_ID(N'dbo.Order', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Order] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [OrderCode] nvarchar(100) NOT NULL,
        [CustomerName] nvarchar(255) NULL,
        [CustomerPhone] nvarchar(50) NULL,
        [OrderType] nvarchar(50) NOT NULL CONSTRAINT [DF_Order_Type] DEFAULT ('POS'),
        [SubTotal] decimal(18, 2) NOT NULL CONSTRAINT [DF_Order_SubTotal] DEFAULT ((0)),
        [DiscountAmount] decimal(18, 2) NOT NULL CONSTRAINT [DF_Order_Discount] DEFAULT ((0)),
        [TotalAmount] decimal(18, 2) NOT NULL CONSTRAINT [DF_Order_Total] DEFAULT ((0)),
        [PaymentMethod] nvarchar(50) NOT NULL CONSTRAINT [DF_Order_PayMethod] DEFAULT ('BANK_TRANSFER_QR'),
        [PaymentStatus] nvarchar(50) NOT NULL CONSTRAINT [DF_Order_PayStatus] DEFAULT ('PENDING'),
        [OrderStatus] nvarchar(50) NOT NULL CONSTRAINT [DF_Order_Status] DEFAULT ('PENDING'),
        [TransferContent] nvarchar(100) NULL,
        [PaidAt] datetime2(7) NULL,
        [PaymentProofImageUrl] nvarchar(MAX) NULL,
        [Note] nvarchar(MAX) NULL,
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Order_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Order_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_Order_IsDeleted] DEFAULT ((0)),
        [ShippingAddress] nvarchar(500) NULL,
        [ShippingProvider] nvarchar(100) NULL,
        [TrackingCode] nvarchar(100) NULL,
        [ShippingFee] decimal(18, 2) NOT NULL CONSTRAINT [DF_Order_ShippingFee] DEFAULT ((0)),
        [PreparationStatus] nvarchar(50) NOT NULL CONSTRAINT [DF_Order_PrepStatus] DEFAULT ('PENDING'),
        [PreparationNote] nvarchar(MAX) NULL,
        [PackingChecklistJson] nvarchar(MAX) NULL,
        CONSTRAINT [PK_Order] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng Order';
END
GO

IF OBJECT_ID(N'dbo.OrderDetail', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderDetail] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [OrderId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductName] nvarchar(255) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18, 2) NOT NULL,
        [CostPrice] decimal(18, 2) NOT NULL CONSTRAINT [DF_OrderDetail_CostPrice] DEFAULT ((0)),
        [TotalPrice] decimal(18, 2) NOT NULL,
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_OrderDetail_CreatedAt] DEFAULT (getdate()),
        CONSTRAINT [PK_OrderDetail] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng OrderDetail';
END
GO

IF OBJECT_ID(N'dbo.PaymentSetting', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PaymentSetting] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [BankCode] nvarchar(50) NOT NULL CONSTRAINT [DF_PaymentSetting_Bank] DEFAULT ('MB'),
        [AccountNumber] nvarchar(100) NOT NULL CONSTRAINT [DF_PaymentSetting_AccNum] DEFAULT ('0987654321'),
        [AccountName] nvarchar(255) NOT NULL CONSTRAINT [DF_PaymentSetting_AccName] DEFAULT ('SQUISHY STORE'),
        [SePayApiKey] nvarchar(255) NULL,
        [QrTemplate] nvarchar(50) NOT NULL CONSTRAINT [DF_PaymentSetting_Template] DEFAULT ('compact'),
        [IsActive] bit NOT NULL CONSTRAINT [DF_PaymentSetting_Active] DEFAULT ((1)),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_PaymentSetting_Updated] DEFAULT (getdate()),
        [SePayWebhookSecret] nvarchar(200) NULL,
        CONSTRAINT [PK_PaymentSetting] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng PaymentSetting';
END
GO

IF OBJECT_ID(N'dbo.PaymentWebhookLog', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PaymentWebhookLog] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [Gateway] nvarchar(100) NULL,
        [TransactionDate] nvarchar(100) NULL,
        [AccountNumber] nvarchar(100) NULL,
        [AmountIn] decimal(18, 2) NULL,
        [TransactionContent] nvarchar(MAX) NULL,
        [ReferenceNumber] nvarchar(255) NULL,
        [RawPayload] nvarchar(MAX) NULL,
        [OrderId] uniqueidentifier NULL,
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_PaymentWebhookLog_CreatedAt] DEFAULT (getdate()),
        [SePayTransactionId] bigint NULL,
        [TransferType] nvarchar(10) NULL,
        [Status] nvarchar(30) NULL,
        [Message] nvarchar(500) NULL,
        CONSTRAINT [PK_PaymentWebhookLog] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng PaymentWebhookLog';
END
GO

IF OBJECT_ID(N'dbo.Preorder', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Preorder] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [Code] nvarchar(50) NOT NULL,
        [CustomerName] nvarchar(100) NOT NULL,
        [CustomerPhone] nvarchar(20) NOT NULL,
        [SocialContact] nvarchar(200) NULL,
        [ShippingAddress] nvarchar(500) NULL,
        [Note] nvarchar(500) NULL,
        [TotalAmount] decimal(18, 2) NOT NULL CONSTRAINT [DF_Preorder_Total] DEFAULT ((0)),
        [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_Preorder_Status] DEFAULT ('NEW'),
        [AdminNote] nvarchar(1000) NULL,
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Preorder_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Preorder_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_Preorder_IsDeleted] DEFAULT ((0)),
        [CampaignId] uniqueidentifier NULL,
        [InstagramContact] nvarchar(100) NULL,
        CONSTRAINT [PK_Preorder] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng Preorder';
END
GO

IF OBJECT_ID(N'dbo.PreorderCampaign', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PreorderCampaign] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(80) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [OpenAt] datetime2(7) NULL,
        [CloseAt] datetime2(7) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_PreorderCampaign_Active] DEFAULT ((1)),
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_PreorderCampaign_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_PreorderCampaign_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_PreorderCampaign_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_PreorderCampaign] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng PreorderCampaign';
END
GO

IF OBJECT_ID(N'dbo.PreorderCampaignItem', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PreorderCampaignItem] (
        [CampaignId] uniqueidentifier NOT NULL,
        [PreorderItemId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PreorderCampaignItem] PRIMARY KEY CLUSTERED ([CampaignId], [PreorderItemId])
    );
    PRINT N'Đã tạo bảng PreorderCampaignItem';
END
GO

IF OBJECT_ID(N'dbo.PreorderItem', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PreorderItem] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [Name] nvarchar(200) NOT NULL,
        [Price] decimal(18, 2) NOT NULL CONSTRAINT [DF_PreorderItem_Price] DEFAULT ((0)),
        [ImageUrl] nvarchar(500) NULL,
        [Description] nvarchar(1000) NULL,
        [Tag] nvarchar(20) NOT NULL CONSTRAINT [DF_PreorderItem_Tag] DEFAULT ('HOT'),
        [DisplayOrder] int NOT NULL CONSTRAINT [DF_PreorderItem_Order] DEFAULT ((0)),
        [IsActive] bit NOT NULL CONSTRAINT [DF_PreorderItem_Active] DEFAULT ((1)),
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_PreorderItem_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_PreorderItem_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_PreorderItem_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_PreorderItem] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng PreorderItem';
END
GO

IF OBJECT_ID(N'dbo.PreorderLine', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PreorderLine] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [PreorderId] uniqueidentifier NOT NULL,
        [PreorderItemId] uniqueidentifier NOT NULL,
        [ItemName] nvarchar(200) NOT NULL,
        [UnitPrice] decimal(18, 2) NOT NULL,
        [Quantity] int NOT NULL,
        CONSTRAINT [PK_PreorderLine] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_PreorderLine_Quantity] CHECK ([Quantity]>(0))
    );
    PRINT N'Đã tạo bảng PreorderLine';
END
GO

IF OBJECT_ID(N'dbo.Product', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Product] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [CategoryId] uniqueidentifier NULL,
        [Sku] nvarchar(100) NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Description] nvarchar(MAX) NULL,
        [Unit] nvarchar(50) NOT NULL CONSTRAINT [DF_Product_Unit] DEFAULT (N'Chiếc'),
        [CostPrice] decimal(18, 2) NOT NULL CONSTRAINT [DF_Product_CostPrice] DEFAULT ((0)),
        [SalePrice] decimal(18, 2) NOT NULL CONSTRAINT [DF_Product_SalePrice] DEFAULT ((0)),
        [CurrentStock] int NOT NULL CONSTRAINT [DF_Product_CurrentStock] DEFAULT ((0)),
        [MinStockAlert] int NOT NULL CONSTRAINT [DF_Product_MinStockAlert] DEFAULT ((5)),
        [SquishyType] nvarchar(100) NULL,
        [MainImageUrl] nvarchar(MAX) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_Product_IsActive] DEFAULT ((1)),
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Product_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Product_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_Product_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_Product] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng Product';
END
GO

IF OBJECT_ID(N'dbo.ReturnRequest', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ReturnRequest] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [Code] nvarchar(50) NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [OrderCode] nvarchar(50) NOT NULL,
        [CustomerName] nvarchar(255) NULL,
        [CustomerPhone] nvarchar(50) NULL,
        [RequestType] nvarchar(30) NOT NULL,
        [ReasonCategory] nvarchar(30) NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [Status] nvarchar(30) NOT NULL CONSTRAINT [DF_ReturnRequest_Status] DEFAULT ('PENDING'),
        [RefundAmount] decimal(18, 2) NOT NULL CONSTRAINT [DF_ReturnRequest_Refund] DEFAULT ((0)),
        [RefundMethod] nvarchar(30) NULL,
        [ExtraCharge] decimal(18, 2) NOT NULL CONSTRAINT [DF_ReturnRequest_Extra] DEFAULT ((0)),
        [RepairCost] decimal(18, 2) NOT NULL CONSTRAINT [DF_ReturnRequest_Repair] DEFAULT ((0)),
        [StockRestored] bit NOT NULL CONSTRAINT [DF_ReturnRequest_StockRestored] DEFAULT ((0)),
        [ResolutionNote] nvarchar(1000) NULL,
        [CompletedAt] datetime2(7) NULL,
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_ReturnRequest_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_ReturnRequest_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_ReturnRequest_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_ReturnRequest] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng ReturnRequest';
END
GO

IF OBJECT_ID(N'dbo.ReturnRequestItem', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ReturnRequestItem] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [ReturnRequestId] uniqueidentifier NOT NULL,
        [OrderDetailId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductName] nvarchar(255) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18, 2) NOT NULL,
        [CostPrice] decimal(18, 2) NOT NULL CONSTRAINT [DF_ReturnRequestItem_Cost] DEFAULT ((0)),
        [ItemCondition] nvarchar(20) NOT NULL CONSTRAINT [DF_ReturnRequestItem_Condition] DEFAULT ('DEFECTIVE'),
        [ExchangeProductId] uniqueidentifier NULL,
        [ExchangeProductName] nvarchar(255) NULL,
        [ExchangeQuantity] int NOT NULL CONSTRAINT [DF_ReturnRequestItem_ExQty] DEFAULT ((0)),
        [ExchangeUnitPrice] decimal(18, 2) NOT NULL CONSTRAINT [DF_ReturnRequestItem_ExPrice] DEFAULT ((0)),
        [ExchangeCostPrice] decimal(18, 2) NOT NULL CONSTRAINT [DF_ReturnRequestItem_ExCost] DEFAULT ((0)),
        CONSTRAINT [PK_ReturnRequestItem] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [CK_ReturnRequestItem_Quantity] CHECK ([Quantity]>(0))
    );
    PRINT N'Đã tạo bảng ReturnRequestItem';
END
GO

IF OBJECT_ID(N'dbo.StockTransaction', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StockTransaction] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [ProductId] uniqueidentifier NOT NULL,
        [TransactionType] nvarchar(50) NOT NULL,
        [ReferenceCode] nvarchar(100) NULL,
        [QuantityBefore] int NOT NULL,
        [QuantityChange] int NOT NULL,
        [QuantityAfter] int NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_StockTransaction_CreatedAt] DEFAULT (getdate()),
        CONSTRAINT [PK_StockTransaction] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng StockTransaction';
END
GO

IF OBJECT_ID(N'dbo.Supplier', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Supplier] (
        [Id] uniqueidentifier NOT NULL DEFAULT (newid()),
        [Code] nvarchar(100) NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Phone] nvarchar(50) NULL,
        [Email] nvarchar(255) NULL,
        [Address] nvarchar(500) NULL,
        [Note] nvarchar(MAX) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_Supplier_IsActive] DEFAULT ((1)),
        [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Supplier_CreatedAt] DEFAULT (getdate()),
        [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Supplier_UpdatedAt] DEFAULT (getdate()),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_Supplier_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_Supplier] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng Supplier';
END
GO

IF OBJECT_ID(N'dbo.SystemFunction', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SystemFunction] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(7) NOT NULL,
        [UpdatedAt] datetime2(7) NOT NULL,
        [UserCreated] uniqueidentifier NOT NULL,
        [UserModified] uniqueidentifier NOT NULL,
        [Code] nvarchar(255) NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Url] nvarchar(500) NOT NULL,
        [Order] int NOT NULL CONSTRAINT [DF_SystemFunction_Order] DEFAULT ((0)),
        [Type] nvarchar(50) NOT NULL,
        [Icon] nvarchar(100) NOT NULL,
        [IsShow] bit NOT NULL CONSTRAINT [DF_SystemFunction_IsShow] DEFAULT ((1)),
        [ParentId] uniqueidentifier NOT NULL CONSTRAINT [DF_SystemFunction_ParentId] DEFAULT ('00000000-0000-0000-0000-000000000000'),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_SystemFunction_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_SystemFunction] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng SystemFunction';
END
GO

IF OBJECT_ID(N'dbo.SystemRole', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SystemRole] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(7) NOT NULL,
        [UpdatedAt] datetime2(7) NOT NULL,
        [UserCreated] uniqueidentifier NOT NULL,
        [UserModified] uniqueidentifier NOT NULL,
        [Code] nvarchar(255) NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_SystemRole_IsDeleted] DEFAULT ((0)),
        CONSTRAINT [PK_SystemRole] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng SystemRole';
END
GO

IF OBJECT_ID(N'dbo.SystemRoleFunctions', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SystemRoleFunctions] (
        [RoleId] uniqueidentifier NOT NULL,
        [FunctionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SystemRoleFunctions] PRIMARY KEY CLUSTERED ([RoleId], [FunctionId])
    );
    PRINT N'Đã tạo bảng SystemRoleFunctions';
END
GO

IF OBJECT_ID(N'dbo.SystemSecuritySettings', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SystemSecuritySettings] (
        [Id] uniqueidentifier NOT NULL,
        [MinPasswordLength] int NOT NULL CONSTRAINT [DF_Sec_MinPass] DEFAULT ((8)),
        [RequireUppercase] bit NOT NULL CONSTRAINT [DF_Sec_ReqUpper] DEFAULT ((1)),
        [RequireLowercase] bit NOT NULL CONSTRAINT [DF_Sec_ReqLower] DEFAULT ((1)),
        [RequireNumber] bit NOT NULL CONSTRAINT [DF_Sec_ReqNumber] DEFAULT ((1)),
        [RequireSpecialCharacter] bit NOT NULL CONSTRAINT [DF_Sec_ReqSpecial] DEFAULT ((1)),
        [PasswordExpiryDays] int NOT NULL CONSTRAINT [DF_Sec_PassExpiry] DEFAULT ((90)),
        [MaxFailedAccessAttempts] int NOT NULL CONSTRAINT [DF_Sec_MaxFailed] DEFAULT ((5)),
        [LockoutDurationMinutes] int NOT NULL CONSTRAINT [DF_Sec_LockoutMin] DEFAULT ((15)),
        [AllowedAdminIPs] nvarchar(MAX) NULL,
        CONSTRAINT [PK_SystemSecuritySettings] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng SystemSecuritySettings';
END
GO

IF OBJECT_ID(N'dbo.User', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[User] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2(7) NOT NULL,
        [UpdatedAt] datetime2(7) NOT NULL,
        [UserCreated] uniqueidentifier NOT NULL,
        [UserModified] uniqueidentifier NOT NULL,
        [UserName] nvarchar(500) NOT NULL,
        [PasswordHashed] nvarchar(MAX) NOT NULL,
        [Email] nvarchar(500) NULL,
        [RefreshToken] nvarchar(MAX) NULL,
        [RefreshTokenExpired] datetime2(7) NULL,
        [Name] nvarchar(500) NULL,
        [PhoneNumber] nvarchar(50) NULL,
        [AvatarPath] nvarchar(MAX) NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_User_IsActive] DEFAULT ((1)),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_User_IsDeleted] DEFAULT ((0)),
        [LastPasswordChangedAt] datetime2(7) NULL,
        [FailedAccessAttempts] int NOT NULL CONSTRAINT [DF_User_FailedAttempts] DEFAULT ((0)),
        [LockoutEnd] datetime2(7) NULL,
        [RequiresPasswordChange] bit NOT NULL CONSTRAINT [DF_User_ReqPassChange] DEFAULT ((0)),
        [ResetPasswordCode] nvarchar(MAX) NULL,
        [ResetPasswordCodeExpiredAt] datetime2(7) NULL,
        CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([Id])
    );
    PRINT N'Đã tạo bảng User';
END
GO

IF OBJECT_ID(N'dbo.UserRoles', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserId], [RoleId])
    );
    PRINT N'Đã tạo bảng UserRoles';
END
GO

-- ===== Chỉ mục =====

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashTransaction_Code' AND object_id = OBJECT_ID(N'dbo.CashTransaction'))
    CREATE UNIQUE NONCLUSTERED INDEX [IX_CashTransaction_Code] ON [dbo].[CashTransaction] ([Code]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashTransaction_Date' AND object_id = OBJECT_ID(N'dbo.CashTransaction'))
    CREATE NONCLUSTERED INDEX [IX_CashTransaction_Date] ON [dbo].[CashTransaction] ([TransactionDate], [Type]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentWebhookLog_SePayId' AND object_id = OBJECT_ID(N'dbo.PaymentWebhookLog'))
    CREATE NONCLUSTERED INDEX [IX_PaymentWebhookLog_SePayId] ON [dbo].[PaymentWebhookLog] ([SePayTransactionId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Preorder_Campaign' AND object_id = OBJECT_ID(N'dbo.Preorder'))
    CREATE NONCLUSTERED INDEX [IX_Preorder_Campaign] ON [dbo].[Preorder] ([CampaignId], [CreatedAt]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Preorder_Code' AND object_id = OBJECT_ID(N'dbo.Preorder'))
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Preorder_Code] ON [dbo].[Preorder] ([Code]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Preorder_Phone' AND object_id = OBJECT_ID(N'dbo.Preorder'))
    CREATE NONCLUSTERED INDEX [IX_Preorder_Phone] ON [dbo].[Preorder] ([CustomerPhone], [CreatedAt]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PreorderCampaign_Slug' AND object_id = OBJECT_ID(N'dbo.PreorderCampaign'))
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PreorderCampaign_Slug] ON [dbo].[PreorderCampaign] ([Slug]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PreorderLine_Item' AND object_id = OBJECT_ID(N'dbo.PreorderLine'))
    CREATE NONCLUSTERED INDEX [IX_PreorderLine_Item] ON [dbo].[PreorderLine] ([PreorderItemId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PreorderLine_Preorder' AND object_id = OBJECT_ID(N'dbo.PreorderLine'))
    CREATE NONCLUSTERED INDEX [IX_PreorderLine_Preorder] ON [dbo].[PreorderLine] ([PreorderId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReturnRequest_Code' AND object_id = OBJECT_ID(N'dbo.ReturnRequest'))
    CREATE UNIQUE NONCLUSTERED INDEX [IX_ReturnRequest_Code] ON [dbo].[ReturnRequest] ([Code]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReturnRequest_OrderId' AND object_id = OBJECT_ID(N'dbo.ReturnRequest'))
    CREATE NONCLUSTERED INDEX [IX_ReturnRequest_OrderId] ON [dbo].[ReturnRequest] ([OrderId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReturnRequest_Status' AND object_id = OBJECT_ID(N'dbo.ReturnRequest'))
    CREATE NONCLUSTERED INDEX [IX_ReturnRequest_Status] ON [dbo].[ReturnRequest] ([Status], [CompletedAt]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReturnRequestItem_OrderDetail' AND object_id = OBJECT_ID(N'dbo.ReturnRequestItem'))
    CREATE NONCLUSTERED INDEX [IX_ReturnRequestItem_OrderDetail] ON [dbo].[ReturnRequestItem] ([OrderDetailId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReturnRequestItem_Request' AND object_id = OBJECT_ID(N'dbo.ReturnRequestItem'))
    CREATE NONCLUSTERED INDEX [IX_ReturnRequestItem_Request] ON [dbo].[ReturnRequestItem] ([ReturnRequestId]);

GO

-- ===== Khóa ngoại =====

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PreorderCampaignItem_Campaign')
    ALTER TABLE [dbo].[PreorderCampaignItem] ADD CONSTRAINT [FK_PreorderCampaignItem_Campaign] FOREIGN KEY ([CampaignId]) REFERENCES [dbo].[PreorderCampaign] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PreorderCampaignItem_Item')
    ALTER TABLE [dbo].[PreorderCampaignItem] ADD CONSTRAINT [FK_PreorderCampaignItem_Item] FOREIGN KEY ([PreorderItemId]) REFERENCES [dbo].[PreorderItem] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PreorderLine_Item')
    ALTER TABLE [dbo].[PreorderLine] ADD CONSTRAINT [FK_PreorderLine_Item] FOREIGN KEY ([PreorderItemId]) REFERENCES [dbo].[PreorderItem] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PreorderLine_Preorder')
    ALTER TABLE [dbo].[PreorderLine] ADD CONSTRAINT [FK_PreorderLine_Preorder] FOREIGN KEY ([PreorderId]) REFERENCES [dbo].[Preorder] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ReturnRequest_Order')
    ALTER TABLE [dbo].[ReturnRequest] ADD CONSTRAINT [FK_ReturnRequest_Order] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Order] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ReturnRequestItem_OrderDetail')
    ALTER TABLE [dbo].[ReturnRequestItem] ADD CONSTRAINT [FK_ReturnRequestItem_OrderDetail] FOREIGN KEY ([OrderDetailId]) REFERENCES [dbo].[OrderDetail] ([Id]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ReturnRequestItem_Request')
    ALTER TABLE [dbo].[ReturnRequestItem] ADD CONSTRAINT [FK_ReturnRequestItem_Request] FOREIGN KEY ([ReturnRequestId]) REFERENCES [dbo].[ReturnRequest] ([Id]);

GO
