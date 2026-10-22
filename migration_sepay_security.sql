-- =========================================================================================
-- MIGRATION: Bảo mật webhook SePay (HMAC-SHA256 / API Key) + chống xử lý trùng giao dịch.
-- Chạy được nhiều lần.
-- =========================================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.PaymentSetting', 'SePayWebhookSecret') IS NULL
BEGIN
    ALTER TABLE [dbo].[PaymentSetting] ADD [SePayWebhookSecret] NVARCHAR(200) NULL; -- Secret Key khi chọn xác thực HMAC-SHA256
    PRINT N'Đã thêm cột PaymentSetting.SePayWebhookSecret.';
END
GO

-- Giá trị mẫu lúc khởi tạo không phải khóa thật: xóa để buộc nhập khóa từ my.sepay.vn
UPDATE [dbo].[PaymentSetting] SET [SePayApiKey] = NULL WHERE [SePayApiKey] = 'SEPAY_KEY_DEMO';
GO

IF COL_LENGTH('dbo.PaymentWebhookLog', 'SePayTransactionId') IS NULL
BEGIN
    ALTER TABLE [dbo].[PaymentWebhookLog] ADD
        [SePayTransactionId] BIGINT NULL,          -- trường "id" SePay gửi, dùng để bỏ qua khi SePay gửi lại
        [TransferType] NVARCHAR(10) NULL,          -- in / out
        [Status] NVARCHAR(30) NULL,                -- MATCHED, UNMATCHED, DUPLICATE, IGNORED, REJECTED
        [Message] NVARCHAR(500) NULL;
    PRINT N'Đã thêm cột theo dõi vào PaymentWebhookLog.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaymentWebhookLog_SePayId' AND object_id = OBJECT_ID('dbo.PaymentWebhookLog'))
    CREATE INDEX [IX_PaymentWebhookLog_SePayId] ON [dbo].[PaymentWebhookLog]([SePayTransactionId]);
GO

PRINT N'Hoàn tất migration bảo mật SePay.';
GO
