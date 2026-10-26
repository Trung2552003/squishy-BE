-- =========================================================================================
-- Đơn giao hàng: ai trả phí ship.
--   RECEIVER: người nhận (khách) trả → cộng vào tiền khách trả, là tiền thu hộ cho đơn vị vận chuyển (không phải doanh thu)
--   SENDER  : người gửi (shop) trả  → khách chỉ trả tiền hàng, phí ship là chi phí của shop
-- Đơn cũ: phí ship đã cộng vào tiền khách trả nên mặc định RECEIVER. Chạy lại được.
-- =========================================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.Order', 'ShippingPayer') IS NULL
    ALTER TABLE [dbo].[Order] ADD [ShippingPayer] NVARCHAR(10) NOT NULL CONSTRAINT [DF_Order_ShippingPayer] DEFAULT 'RECEIVER';
GO
