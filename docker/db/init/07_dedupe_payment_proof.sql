-- =========================================================================================
-- Dọn ảnh xác nhận thanh toán bị nhân đôi.
-- Nguyên nhân cũ: ảnh tải lên tạo 1 bản ghi gắn đơn, sau đó bước "gắn ảnh bill" lại thêm 1 bản ghi cùng đường dẫn.
-- Cách dọn: mỗi (đơn, đường dẫn) giữ bản tải lên đầu tiên (có tên file gốc), ẩn các bản thừa. Chạy lại được.
-- =========================================================================================
SET NOCOUNT ON;
GO

;WITH d AS (
    SELECT Id, ROW_NUMBER() OVER (
        PARTITION BY EntityId, Url
        ORDER BY CASE WHEN FileName = 'payment_proof.jpg' THEN 1 ELSE 0 END, CreatedAt, Id) AS rn
    FROM [dbo].[MediaImage]
    WHERE EntityType = 'ORDER_PAYMENT' AND EntityId IS NOT NULL AND IsDeleted = 0
)
UPDATE m SET m.IsDeleted = 1, m.UpdatedAt = GETDATE()
FROM [dbo].[MediaImage] m
JOIN d ON d.Id = m.Id
WHERE d.rn > 1;
GO
