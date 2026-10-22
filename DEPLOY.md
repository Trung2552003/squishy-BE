# Deploy Squishy Pop bằng Portainer (Git)

Stack gồm 4 container: **mssql** (SQL Server 2025), **db-init** (tạo database rồi tự thoát), **backend** (.NET 10), **frontend** (nginx).
Người dùng chỉ truy cập **frontend**; nginx chuyển `/api`, `/hubs`, `/uploads` sang backend nên web và API chung một địa chỉ.

```
Trình duyệt ──► frontend:80 (nginx) ──► backend:8080 ──► mssql:1433
                     │ web tĩnh            │ ảnh tải lên (volume uploads)
                                           └ dữ liệu (volume mssql-data)
```

## 1. Chuẩn bị

- Server Linux có Docker, Portainer, **git** (Docker cần git để build frontend từ repo) và **RAM tối thiểu 4GB** (SQL Server cần ~2GB).
- Commit và push cả hai repo lên nhánh `main`:
  - `squishy-BE` (có `Dockerfile`, `docker-compose.yml`, `docker/db/`)
  - `Squishy-FE` (có `Dockerfile`, `nginx.conf`)

## 2. Tạo stack

Portainer → **Stacks → Add stack**

| Mục | Giá trị |
|---|---|
| Build method | **Repository** |
| Repository URL | `https://github.com/Trung2552003/squishy-BE` |
| Repository reference | `refs/heads/main` |
| Compose path | `docker-compose.yml` |
| Authentication | Bật nếu repo private (GitHub user + Personal Access Token) |

**Environment variables** (xem `.env.example`):

| Biến | Bắt buộc | Ghi chú |
|---|---|---|
| `MSSQL_SA_PASSWORD` | ✅ | ≥ 8 ký tự, có hoa, thường, số, ký tự đặc biệt |
| `JWT_SECRET` | ✅ | Chuỗi ngẫu nhiên ≥ 64 ký tự |
| `WEB_PORT` | | Mặc định `8080` |
| `SMTP_USERNAME`, `SMTP_PASSWORD` | | Gmail + App Password, để gửi mail quên mật khẩu |
| `FE_GIT_CONTEXT` | | Repo FE private: `https://<user>:<token>@github.com/Trung2552003/Squishy-FE.git#main` |
| `DB_NAME`, `MSSQL_PID` | | Mặc định `SquishyPop`, `Express` |
| `MSSQL_BIND_IP`, `MSSQL_PUBLIC_PORT` | | Mặc định `127.0.0.1:1433`: chỉ kết nối qua đường hầm SSH. `0.0.0.0` = mở ra Internet |

Bấm **Deploy the stack**. Lần đầu mất vài phút (tải image + build). Xem log container `db-init`: thấy dòng `Database SquishyPop đã sẵn sàng.` là xong.

Mở `http://<ip-server>:8080` → đăng nhập bằng tài khoản cũ (dữ liệu người dùng, vai trò, phân quyền được giữ; sản phẩm, đơn hàng… bắt đầu trống).

## 3. Tên miền + HTTPS

Đặt reverse proxy (Nginx Proxy Manager, Traefik, Caddy, Cloudflare Tunnel…) trỏ tên miền về `http://<server>:8080`.
Proxy cần gửi header `X-Forwarded-Proto: https` (các công cụ trên đều tự gửi) để cookie đăng nhập bật chế độ Secure.
Nếu proxy có tùy chọn **WebSockets support**, hãy bật (dùng cho thông báo thanh toán realtime).

Sau đó vào **Cài đặt SePay** trong CMS: URL webhook hiển thị sẽ là `https://<tên-miền>/api/Payment/sepay-webhook`.

## 4. Cập nhật phiên bản mới

Push code lên `main` → Portainer → stack → **Pull and redeploy** (bật *Re-pull image and redeploy*).
Có thể bật **GitOps updates** để Portainer tự kiểm tra repo định kỳ.
Dữ liệu nằm trong volume `mssql-data` và `uploads` nên không mất khi redeploy.

**Thay đổi cấu trúc database:** thêm file mới vào `docker/db/init/` (VD `03_them_cot_abc.sql`), viết dạng chạy lại được
(`IF COL_LENGTH(...) IS NULL ALTER TABLE ...`). `db-init` chạy lại toàn bộ thư mục này theo thứ tự tên mỗi lần deploy.

## 5. Sao lưu

```bash
# Tạo file backup bên trong volume mssql-data
docker exec -it squishypop-mssql-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<MSSQL_SA_PASSWORD>' -C \
  -Q "BACKUP DATABASE [SquishyPop] TO DISK = N'/var/opt/mssql/data/SquishyPop.bak' WITH INIT, COMPRESSION"
docker cp squishypop-mssql-1:/var/opt/mssql/data/SquishyPop.bak ./SquishyPop-$(date +%F).bak
```

Ảnh tải lên nằm trong volume `squishypop_uploads` — sao lưu cùng lúc.

## 6. Lưu ý bảo mật

- `appsettings.json` trong repo đang chứa JWT secret và mật khẩu Gmail dùng lúc phát triển. Bản Docker đã ghi đè bằng biến môi trường,
  nhưng nên **đổi App Password Gmail** và xóa các giá trị đó khỏi repo nếu repo từng public.
- `docker/db/init/02_seed.sql` chứa mã băm mật khẩu của tài khoản hiện có → giữ repo ở chế độ **private**.
- SQL Server mặc định chỉ mở cho chính server; kết nối từ máy bạn qua đường hầm SSH (mục 7). Nếu đặt `MSSQL_BIND_IP=0.0.0.0`
  thì dùng mật khẩu `sa` thật mạnh và chặn firewall chỉ cho IP của bạn (VD `ufw allow from <IP-của-bạn> to any port 1433`).

## 7. Kết nối database trên server từ máy bạn (đường hầm SSH)

```bash
# Giữ cửa sổ này mở trong lúc làm việc. 14330 là cổng trên máy bạn (tránh trùng SQL Server cài sẵn ở 1433)
ssh -N -L 14330:127.0.0.1:1433 <user>@<ip-server>
```

- **SSMS / Azure Data Studio:** Server `localhost,14330`, SQL Login `sa`, mật khẩu `MSSQL_SA_PASSWORD`, tích *Trust server certificate*.
- **Chạy backend trên máy bạn với database của server:** copy `appsettings.Local.example.json` thành `appsettings.Local.json`,
  điền mật khẩu. File này nằm trong `.gitignore` nên mật khẩu không lên GitHub; xóa file đi là backend quay lại dùng database trên máy.
