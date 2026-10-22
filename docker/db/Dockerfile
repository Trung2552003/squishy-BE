# Image chạy 1 lần để tạo database: đóng gói sẵn script thay vì gắn thư mục (bind mount),
# vì Portainer deploy từ Git không gắn được đường dẫn tương đối như ./docker/db.
FROM mcr.microsoft.com/mssql/server:2025-latest

COPY run-init.sh /docker-db/run-init.sh
COPY init/ /docker-db/init/

ENTRYPOINT ["/bin/bash", "/docker-db/run-init.sh"]
