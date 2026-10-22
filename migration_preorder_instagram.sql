-- =========================================================================================
-- MIGRATION: Thêm nick Instagram / Threads vào đơn đặt trước. Chạy được nhiều lần.
-- =========================================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.Preorder', 'InstagramContact') IS NULL
BEGIN
    ALTER TABLE [dbo].[Preorder] ADD [InstagramContact] NVARCHAR(100) NULL;
    PRINT N'Đã thêm cột Preorder.InstagramContact.';
END
GO
