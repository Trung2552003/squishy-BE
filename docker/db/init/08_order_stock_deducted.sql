-- =========================================================================================
-- Cờ "đơn đã trừ kho" để trừ kho đúng 1 lần dù đơn đi qua nhiều bước:
--   bán tiền mặt tại quầy, bấm hoàn thành, SePay báo tiền về (đóng gói / giao shipper không trừ kho).
-- Hủy đơn chỉ hoàn kho khi cờ đang bật.
-- Đơn cũ: suy ra từ thẻ kho (tổng xuất bán + hoàn kho do hủy theo mã đơn < 0 nghĩa là đang trừ). Chạy lại được.
-- =========================================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.Order', 'IsStockDeducted') IS NULL
    ALTER TABLE [dbo].[Order] ADD [IsStockDeducted] BIT NULL;
GO

-- Chỉ điền cho dòng chưa có giá trị (lần đầu chạy)
UPDATE o SET o.IsStockDeducted = CASE WHEN ISNULL(t.Net, 0) < 0 THEN 1 ELSE 0 END
FROM [dbo].[Order] o
OUTER APPLY (
    SELECT SUM(st.QuantityChange) AS Net FROM [dbo].[StockTransaction] st
    WHERE st.ReferenceCode = o.OrderCode AND st.TransactionType IN ('EXPORT', 'ORDER_CANCEL')
) t
WHERE o.IsStockDeducted IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Order') AND name = 'IsStockDeducted' AND is_nullable = 1)
    ALTER TABLE [dbo].[Order] ALTER COLUMN [IsStockDeducted] BIT NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Order_IsStockDeducted')
    ALTER TABLE [dbo].[Order] ADD CONSTRAINT [DF_Order_IsStockDeducted] DEFAULT 0 FOR [IsStockDeducted];
GO
