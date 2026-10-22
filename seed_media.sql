-- Xóa dữ liệu media cũ không liên kết
DELETE FROM MediaVideo WHERE EntityType = 'PRODUCT';
DELETE FROM MediaImage WHERE EntityType = 'PRODUCT';

-- 1. Squishy Bánh Kem Dâu Tây Hoàng Gia (8eea8678-246f-4f02-9d33-951c0f75c504)
INSERT INTO MediaImage (Id, EntityId, EntityType, Url, FileName, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), '8eea8678-246f-4f02-9d33-951c0f75c504', 'PRODUCT', 'https://images.unsplash.com/photo-1578985545062-69928b1d9587?w=800&auto=format&fit=crop&q=80', N'banh-kem-dau-chinh.jpg', 1, 1, GETDATE(), GETDATE(), 0),
(NEWID(), '8eea8678-246f-4f02-9d33-951c0f75c504', 'PRODUCT', 'https://images.unsplash.com/photo-1588195538326-c5b1e9f80a1b?w=800&auto=format&fit=crop&q=80', N'banh-kem-dau-goc-nghieng.jpg', 0, 2, GETDATE(), GETDATE(), 0),
(NEWID(), '8eea8678-246f-4f02-9d33-951c0f75c504', 'PRODUCT', 'https://images.unsplash.com/photo-1565958011703-44f9829ba187?w=800&auto=format&fit=crop&q=80', N'banh-kem-dau-can-canh.jpg', 0, 3, GETDATE(), GETDATE(), 0);

INSERT INTO MediaVideo (Id, EntityId, EntityType, Url, ThumbnailUrl, FileName, DurationSeconds, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), '8eea8678-246f-4f02-9d33-951c0f75c504', 'PRODUCT', 'https://www.w3schools.com/html/mov_bbb.mp4', 'https://images.unsplash.com/photo-1578985545062-69928b1d9587?w=500&auto=format&fit=crop&q=60', N'video-review-banh-kem-dau.mp4', 15, 1, GETDATE(), GETDATE(), 0);

-- 2. Squishy Quả Đào Tiên Mọng Nước (b6c65c75-4e07-4b2c-a5d7-ebcd6ae29155)
INSERT INTO MediaImage (Id, EntityId, EntityType, Url, FileName, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), 'b6c65c75-4e07-4b2c-a5d7-ebcd6ae29155', 'PRODUCT', 'https://images.unsplash.com/photo-1595126731181-4998b6543b92?w=800&auto=format&fit=crop&q=80', N'qua-dao-tien-chinh.jpg', 1, 1, GETDATE(), GETDATE(), 0),
(NEWID(), 'b6c65c75-4e07-4b2c-a5d7-ebcd6ae29155', 'PRODUCT', 'https://images.unsplash.com/photo-1615485500704-8e990f9900f7?w=800&auto=format&fit=crop&q=80', N'qua-dao-tien-chi-tiet.jpg', 0, 2, GETDATE(), GETDATE(), 0);

INSERT INTO MediaVideo (Id, EntityId, EntityType, Url, ThumbnailUrl, FileName, DurationSeconds, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), 'b6c65c75-4e07-4b2c-a5d7-ebcd6ae29155', 'PRODUCT', 'https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerEscapes.mp4', 'https://images.unsplash.com/photo-1595126731181-4998b6543b92?w=500&auto=format&fit=crop&q=60', N'video-review-dao-tien.mp4', 15, 1, GETDATE(), GETDATE(), 0);

-- 3. Squishy Gấu Dâu Jumbo Khổng Lồ 20cm (70b1ae40-d93a-4bd3-b284-8681a053ff1b)
INSERT INTO MediaImage (Id, EntityId, EntityType, Url, FileName, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), '70b1ae40-d93a-4bd3-b284-8681a053ff1b', 'PRODUCT', 'https://images.unsplash.com/photo-1559454403-b8fb88521f11?w=800&auto=format&fit=crop&q=80', N'gau-dau-jumbo-chinh.jpg', 1, 1, GETDATE(), GETDATE(), 0),
(NEWID(), '70b1ae40-d93a-4bd3-b284-8681a053ff1b', 'PRODUCT', 'https://images.unsplash.com/photo-1533738363-b7f9aef128ce?w=800&auto=format&fit=crop&q=80', N'gau-dau-jumbo-om-tay.jpg', 0, 2, GETDATE(), GETDATE(), 0);

INSERT INTO MediaVideo (Id, EntityId, EntityType, Url, ThumbnailUrl, FileName, DurationSeconds, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), '70b1ae40-d93a-4bd3-b284-8681a053ff1b', 'PRODUCT', 'https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerFun.mp4', 'https://images.unsplash.com/photo-1559454403-b8fb88521f11?w=500&auto=format&fit=crop&q=60', N'video-review-gau-jumbo.mp4', 15, 1, GETDATE(), GETDATE(), 0);

-- 4. Squishy Mèo Lười Béo Bụng Trắng (0baf090d-e2c4-4b9b-b8c1-c16e456dbb3d)
INSERT INTO MediaImage (Id, EntityId, EntityType, Url, FileName, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), '0baf090d-e2c4-4b9b-b8c1-c16e456dbb3d', 'PRODUCT', 'https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=800&auto=format&fit=crop&q=80', N'meo-luoi-beo-chinh.jpg', 1, 1, GETDATE(), GETDATE(), 0),
(NEWID(), '0baf090d-e2c4-4b9b-b8c1-c16e456dbb3d', 'PRODUCT', 'https://images.unsplash.com/photo-1574158622682-e40e69881006?w=800&auto=format&fit=crop&q=80', N'meo-luoi-bung-trang.jpg', 0, 2, GETDATE(), GETDATE(), 0);

INSERT INTO MediaVideo (Id, EntityId, EntityType, Url, ThumbnailUrl, FileName, DurationSeconds, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), '0baf090d-e2c4-4b9b-b8c1-c16e456dbb3d', 'PRODUCT', 'https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4', 'https://images.unsplash.com/photo-1514888286974-6c03e2ca1dba?w=500&auto=format&fit=crop&q=60', N'video-review-meo-luoi.mp4', 15, 1, GETDATE(), GETDATE(), 0);

-- 5. Bộ 5 Mochi Squishy Thú Cưng Mini (2415ca8a-0c0d-4c80-830b-3e733c2f2e05)
INSERT INTO MediaImage (Id, EntityId, EntityType, Url, FileName, IsPrimary, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), '2415ca8a-0c0d-4c80-830b-3e733c2f2e05', 'PRODUCT', 'https://images.unsplash.com/photo-1563245372-f21724e3856d?w=800&auto=format&fit=crop&q=80', N'mochi-mini-chinh.jpg', 1, 1, GETDATE(), GETDATE(), 0),
(NEWID(), '2415ca8a-0c0d-4c80-830b-3e733c2f2e05', 'PRODUCT', 'https://images.unsplash.com/photo-1558769132-cb1aea458c5e?w=800&auto=format&fit=crop&q=80', N'mochi-mini-hop.jpg', 0, 2, GETDATE(), GETDATE(), 0);

INSERT INTO MediaVideo (Id, EntityId, EntityType, Url, ThumbnailUrl, FileName, DurationSeconds, DisplayOrder, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
(NEWID(), '2415ca8a-0c0d-4c80-830b-3e733c2f2e05', 'PRODUCT', 'https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerJoyBlazes.mp4', 'https://images.unsplash.com/photo-1563245372-f21724e3856d?w=500&auto=format&fit=crop&q=60', N'video-review-mochi.mp4', 15, 1, GETDATE(), GETDATE(), 0);
