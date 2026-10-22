-- =========================================================================================
-- MIGRATION: THU CHI KHÁC (chi tiêu phát sinh, khoản tiền được cấp / thu khác)
-- Chạy được nhiều lần.
-- =========================================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.CashTransaction', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CashTransaction] (
        [Id]              UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_CashTransaction] PRIMARY KEY DEFAULT NEWID(),
        [Code]            NVARCHAR(30)     NOT NULL,                 -- PC-yyyyMMdd-### (phiếu chi), PT-yyyyMMdd-### (phiếu thu)
        [Type]            NVARCHAR(10)     NOT NULL,                 -- EXPENSE, INCOME
        [Category]        NVARCHAR(30)     NOT NULL,                 -- xem danh mục trong CashTransactionService
        [Amount]          DECIMAL(18, 2)   NOT NULL,
        [TransactionDate] DATETIME2        NOT NULL,                 -- ngày phát sinh (dùng để tính báo cáo theo kỳ)
        [PaymentMethod]   NVARCHAR(20)     NOT NULL CONSTRAINT [DF_CashTransaction_Method] DEFAULT 'CASH', -- CASH, BANK
        [PartyName]       NVARCHAR(200)    NULL,                     -- người nhận tiền / người nộp tiền
        [Description]     NVARCHAR(1000)   NULL,
        [CreatedBy]       NVARCHAR(100)    NULL,
        [CreatedAt]       DATETIME2        NOT NULL CONSTRAINT [DF_CashTransaction_CreatedAt] DEFAULT GETDATE(),
        [UpdatedAt]       DATETIME2        NOT NULL CONSTRAINT [DF_CashTransaction_UpdatedAt] DEFAULT GETDATE(),
        [IsDeleted]       BIT              NOT NULL CONSTRAINT [DF_CashTransaction_IsDeleted] DEFAULT 0,
        CONSTRAINT [CK_CashTransaction_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_CashTransaction_Type] CHECK ([Type] IN ('EXPENSE', 'INCOME'))
    );
    CREATE UNIQUE INDEX [IX_CashTransaction_Code] ON [dbo].[CashTransaction]([Code]);
    CREATE INDEX [IX_CashTransaction_Date] ON [dbo].[CashTransaction]([TransactionDate], [Type]);
    PRINT N'Đã tạo bảng CashTransaction.';
END
GO

-- Menu "Thu Chi Khác" trong nhóm Báo Cáo & Cài Đặt
DECLARE @Now DATETIME2 = DATEADD(HOUR, 7, GETUTCDATE());
DECLARE @EmptyGuid UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @ReportGroupId UNIQUEIDENTIFIER = (SELECT TOP 1 [Id] FROM [dbo].[SystemFunction] WHERE [Code] = 'GROUP_REPORT' AND [IsDeleted] = 0);
IF @ReportGroupId IS NULL SET @ReportGroupId = @EmptyGuid;

IF NOT EXISTS (SELECT 1 FROM [dbo].[SystemFunction] WHERE [Code] = 'CASHBOOK')
    INSERT INTO [dbo].[SystemFunction]
        ([Id], [CreatedAt], [UpdatedAt], [UserCreated], [UserModified], [Code], [Name], [Url], [Order], [Type], [Icon], [IsShow], [ParentId], [IsDeleted])
    VALUES
        (NEWID(), @Now, @Now, @EmptyGuid, @EmptyGuid, 'CASHBOOK', N'Thu Chi Khác', '/cms/cashbook', 2, 'menu', 'WalletIcon', 1, @ReportGroupId, 0);
ELSE
    UPDATE [dbo].[SystemFunction] SET [Url] = '/cms/cashbook', [IsDeleted] = 0, [IsShow] = 1, [UpdatedAt] = @Now WHERE [Code] = 'CASHBOOK';

UPDATE [dbo].[SystemFunction] SET [Order] = 3 WHERE [Code] = 'PAYMENT_CONFIG' AND [Order] = 2;

-- Cấp quyền cho ADMIN và các vai trò đang xem được Báo cáo
DECLARE @FuncId UNIQUEIDENTIFIER = (SELECT TOP 1 [Id] FROM [dbo].[SystemFunction] WHERE [Code] = 'CASHBOOK');
INSERT INTO [dbo].[SystemRoleFunctions] ([RoleId], [FunctionId])
SELECT DISTINCT r.[Id], @FuncId
FROM [dbo].[SystemRole] r
WHERE r.[IsDeleted] = 0
  AND (
      r.[Code] = 'ADMIN'
      OR EXISTS (
          SELECT 1 FROM [dbo].[SystemRoleFunctions] srf
          JOIN [dbo].[SystemFunction] f ON f.[Id] = srf.[FunctionId]
          WHERE srf.[RoleId] = r.[Id] AND f.[Url] = '/cms/reports' AND f.[IsDeleted] = 0
      )
  )
  AND NOT EXISTS (SELECT 1 FROM [dbo].[SystemRoleFunctions] x WHERE x.[RoleId] = r.[Id] AND x.[FunctionId] = @FuncId);

PRINT N'Hoàn tất migration Thu chi khác.';
GO
