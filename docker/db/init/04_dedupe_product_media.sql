-- =========================================================================================
-- Dọn ảnh/video sản phẩm bị nhân đôi trong Thư viện.
-- Nguyên nhân cũ: ảnh tải lên ở form sản phẩm tạo 1 bản ghi (có tên file, chưa gắn sản phẩm),
-- khi lưu sản phẩm lại tạo thêm 1 bản ghi khác cùng đường dẫn (không tên file → hiện "media-item").
-- Cách dọn: giữ bản đang gắn sản phẩm, chép tên file gốc sang, ẩn bản thừa chưa gắn. Chạy lại được.
-- =========================================================================================
SET NOCOUNT ON;
GO

-- Ảnh: chép tên file / dung lượng / định dạng từ bản tải lên sang bản đang gắn sản phẩm
UPDATE l
SET l.FileName = o.FileName, l.FileSize = ISNULL(l.FileSize, o.FileSize), l.MimeType = ISNULL(l.MimeType, o.MimeType)
FROM [dbo].[MediaImage] l
CROSS APPLY (
    SELECT TOP 1 x.FileName, x.FileSize, x.MimeType FROM [dbo].[MediaImage] x
    WHERE x.Url = l.Url AND x.Id <> l.Id AND x.FileName IS NOT NULL
    ORDER BY x.CreatedAt
) o
WHERE l.EntityType = 'PRODUCT' AND l.EntityId IS NOT NULL AND l.IsDeleted = 0 AND l.FileName IS NULL;
GO

-- Ảnh: ẩn bản chưa gắn sản phẩm khi đã có bản cùng đường dẫn đang gắn sản phẩm
UPDATE o SET o.IsDeleted = 1, o.UpdatedAt = GETDATE()
FROM [dbo].[MediaImage] o
WHERE o.EntityType = 'PRODUCT' AND o.EntityId IS NULL AND o.IsDeleted = 0
  AND EXISTS (SELECT 1 FROM [dbo].[MediaImage] l
              WHERE l.Url = o.Url AND l.Id <> o.Id AND l.EntityType = 'PRODUCT' AND l.EntityId IS NOT NULL AND l.IsDeleted = 0);
GO

-- Video: tương tự
UPDATE l
SET l.FileName = o.FileName, l.FileSize = ISNULL(l.FileSize, o.FileSize)
FROM [dbo].[MediaVideo] l
CROSS APPLY (
    SELECT TOP 1 x.FileName, x.FileSize FROM [dbo].[MediaVideo] x
    WHERE x.Url = l.Url AND x.Id <> l.Id AND x.FileName IS NOT NULL
    ORDER BY x.CreatedAt
) o
WHERE l.EntityType = 'PRODUCT' AND l.EntityId IS NOT NULL AND l.IsDeleted = 0 AND l.FileName IS NULL;
GO

UPDATE o SET o.IsDeleted = 1, o.UpdatedAt = GETDATE()
FROM [dbo].[MediaVideo] o
WHERE o.EntityType = 'PRODUCT' AND o.EntityId IS NULL AND o.IsDeleted = 0
  AND EXISTS (SELECT 1 FROM [dbo].[MediaVideo] l
              WHERE l.Url = o.Url AND l.Id <> o.Id AND l.EntityType = 'PRODUCT' AND l.EntityId IS NOT NULL AND l.IsDeleted = 0);
GO
