-- =========================================================================================
-- MIGRATION: ĐỔI TRẢ / HOÀN HÀNG / SỬA - BẢO HÀNH
-- Chạy được nhiều lần (idempotent). Không xóa hay sửa dữ liệu cũ.
-- =========================================================================================
SET NOCOUNT ON;
GO

-- 1. Bảng phiếu đổi trả
IF OBJECT_ID('dbo.ReturnRequest', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ReturnRequest] (
        [Id]             UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ReturnRequest] PRIMARY KEY DEFAULT NEWID(),
        [Code]           NVARCHAR(50)     NOT NULL,
        [OrderId]        UNIQUEIDENTIFIER NOT NULL,
        [OrderCode]      NVARCHAR(50)     NOT NULL,
        [CustomerName]   NVARCHAR(255)    NULL,
        [CustomerPhone]  NVARCHAR(50)     NULL,
        [RequestType]    NVARCHAR(30)     NOT NULL,  -- RETURN, EXCHANGE, REPAIR, FAILED_DELIVERY
        [ReasonCategory] NVARCHAR(30)     NOT NULL,  -- DEFECTIVE, WRONG_ITEM, CHANGED_MIND, DELIVERY_FAILED, OTHER
        [Reason]         NVARCHAR(1000)   NULL,
        [Status]         NVARCHAR(30)     NOT NULL CONSTRAINT [DF_ReturnRequest_Status] DEFAULT 'PENDING',
        [RefundAmount]   DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_ReturnRequest_Refund] DEFAULT 0,
        [RefundMethod]   NVARCHAR(30)     NULL,      -- CASH, BANK_TRANSFER
        [ExtraCharge]    DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_ReturnRequest_Extra] DEFAULT 0,
        [RepairCost]     DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_ReturnRequest_Repair] DEFAULT 0,
        [StockRestored]  BIT              NOT NULL CONSTRAINT [DF_ReturnRequest_StockRestored] DEFAULT 0,
        [ResolutionNote] NVARCHAR(1000)   NULL,
        [CompletedAt]    DATETIME2        NULL,
        [CreatedAt]      DATETIME2        NOT NULL CONSTRAINT [DF_ReturnRequest_CreatedAt] DEFAULT GETDATE(),
        [UpdatedAt]      DATETIME2        NOT NULL CONSTRAINT [DF_ReturnRequest_UpdatedAt] DEFAULT GETDATE(),
        [IsDeleted]      BIT              NOT NULL CONSTRAINT [DF_ReturnRequest_IsDeleted] DEFAULT 0,
        CONSTRAINT [FK_ReturnRequest_Order] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Order]([Id])
    );
    CREATE UNIQUE INDEX [IX_ReturnRequest_Code] ON [dbo].[ReturnRequest]([Code]);
    CREATE INDEX [IX_ReturnRequest_OrderId] ON [dbo].[ReturnRequest]([OrderId]);
    CREATE INDEX [IX_ReturnRequest_Status] ON [dbo].[ReturnRequest]([Status], [CompletedAt]);
    PRINT N'Đã tạo bảng ReturnRequest.';
END
GO

-- 2. Bảng chi tiết sản phẩm trong phiếu
IF OBJECT_ID('dbo.ReturnRequestItem', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ReturnRequestItem] (
        [Id]                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ReturnRequestItem] PRIMARY KEY DEFAULT NEWID(),
        [ReturnRequestId]     UNIQUEIDENTIFIER NOT NULL,
        [OrderDetailId]       UNIQUEIDENTIFIER NOT NULL,
        [ProductId]           UNIQUEIDENTIFIER NOT NULL,
        [ProductName]         NVARCHAR(255)    NOT NULL,
        [Quantity]            INT              NOT NULL,
        [UnitPrice]           DECIMAL(18, 2)   NOT NULL,
        [CostPrice]           DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_ReturnRequestItem_Cost] DEFAULT 0,
        [ItemCondition]       NVARCHAR(20)     NOT NULL CONSTRAINT [DF_ReturnRequestItem_Condition] DEFAULT 'DEFECTIVE', -- GOOD, DEFECTIVE
        [ExchangeProductId]   UNIQUEIDENTIFIER NULL,
        [ExchangeProductName] NVARCHAR(255)    NULL,
        [ExchangeQuantity]    INT              NOT NULL CONSTRAINT [DF_ReturnRequestItem_ExQty] DEFAULT 0,
        [ExchangeUnitPrice]   DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_ReturnRequestItem_ExPrice] DEFAULT 0,
        [ExchangeCostPrice]   DECIMAL(18, 2)   NOT NULL CONSTRAINT [DF_ReturnRequestItem_ExCost] DEFAULT 0,
        CONSTRAINT [FK_ReturnRequestItem_Request] FOREIGN KEY ([ReturnRequestId]) REFERENCES [dbo].[ReturnRequest]([Id]),
        CONSTRAINT [FK_ReturnRequestItem_OrderDetail] FOREIGN KEY ([OrderDetailId]) REFERENCES [dbo].[OrderDetail]([Id]),
        CONSTRAINT [CK_ReturnRequestItem_Quantity] CHECK ([Quantity] > 0)
    );
    CREATE INDEX [IX_ReturnRequestItem_Request] ON [dbo].[ReturnRequestItem]([ReturnRequestId]);
    CREATE INDEX [IX_ReturnRequestItem_OrderDetail] ON [dbo].[ReturnRequestItem]([OrderDetailId]);
    PRINT N'Đã tạo bảng ReturnRequestItem.';
END
GO

-- 3. Menu "Đổi Trả & Bảo Hành" trong nhóm Bán Hàng & Giao Hàng
DECLARE @Now DATETIME2 = DATEADD(HOUR, 7, GETUTCDATE());
DECLARE @EmptyGuid UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @SalesGroupId UNIQUEIDENTIFIER = (
    SELECT TOP 1 [Id] FROM [dbo].[SystemFunction] WHERE [Code] = 'GROUP_SALES' AND [IsDeleted] = 0
);

IF @SalesGroupId IS NULL
    -- Không có nhóm bán hàng: đặt ở cấp gốc
    SET @SalesGroupId = @EmptyGuid;

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemFunction] WHERE [Code] = 'ORDER_RETURN')
BEGIN
    INSERT INTO [dbo].[SystemFunction]
        ([Id], [CreatedAt], [UpdatedAt], [UserCreated], [UserModified], [Code], [Name], [Url], [Order], [Type], [Icon], [IsShow], [ParentId], [IsDeleted])
    VALUES
        (NEWID(), @Now, @Now, @EmptyGuid, @EmptyGuid, 'ORDER_RETURN', N'Đổi Trả & Bảo Hành', '/cms/returns', 4, 'menu', 'ArrowUUpLeftIcon', 1, @SalesGroupId, 0);
    PRINT N'Đã thêm menu Đổi Trả & Bảo Hành.';
END
ELSE
BEGIN
    UPDATE [dbo].[SystemFunction]
    SET [Url] = '/cms/returns', [IsDeleted] = 0, [IsShow] = 1, [UpdatedAt] = @Now
    WHERE [Code] = 'ORDER_RETURN';
END

-- 4. Cấp quyền cho các vai trò đang được xem "Đơn hàng" (và luôn cấp cho ADMIN)
DECLARE @ReturnFuncId UNIQUEIDENTIFIER = (SELECT TOP 1 [Id] FROM [dbo].[SystemFunction] WHERE [Code] = 'ORDER_RETURN');

INSERT INTO [dbo].[SystemRoleFunctions] ([RoleId], [FunctionId])
SELECT DISTINCT r.[Id], @ReturnFuncId
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
  AND NOT EXISTS (
      SELECT 1 FROM [dbo].[SystemRoleFunctions] x WHERE x.[RoleId] = r.[Id] AND x.[FunctionId] = @ReturnFuncId
  );

PRINT N'Hoàn tất migration Đổi trả & Bảo hành.';
GO
