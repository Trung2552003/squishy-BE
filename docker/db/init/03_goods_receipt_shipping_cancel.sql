-- =========================================================================================
-- Phiếu nhập: phí vận chuyển (phân bổ vào giá vốn) + hủy phiếu nhập nhầm.
-- Chạy lại được.
-- =========================================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.GoodsReceipt', 'ShippingFee') IS NULL
    ALTER TABLE [dbo].[GoodsReceipt] ADD [ShippingFee] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_GoodsReceipt_ShippingFee] DEFAULT 0;
GO
IF COL_LENGTH('dbo.GoodsReceipt', 'CancelledAt') IS NULL
    ALTER TABLE [dbo].[GoodsReceipt] ADD [CancelledAt] DATETIME2 NULL, [CancelReason] NVARCHAR(500) NULL;
GO

-- Giá vốn thực của 1 chiếc = giá nhập + phần phí ship phân bổ. Dùng để hủy phiếu chính xác.
IF COL_LENGTH('dbo.GoodsReceiptDetail', 'LandedUnitCost') IS NULL
BEGIN
    ALTER TABLE [dbo].[GoodsReceiptDetail] ADD [LandedUnitCost] DECIMAL(18, 2) NULL;
END
GO
UPDATE [dbo].[GoodsReceiptDetail] SET [LandedUnitCost] = [UnitPrice] WHERE [LandedUnitCost] IS NULL;
GO
