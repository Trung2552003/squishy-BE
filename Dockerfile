# ===== Build =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Khôi phục package trước để tận dụng cache khi chỉ sửa code
COPY SystemBase.BE.csproj ./
RUN dotnet restore SystemBase.BE.csproj

COPY . .
RUN dotnet publish SystemBase.BE.csproj -c Release -o /app/publish /p:UseAppHost=false

# ===== Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# tzdata: để DateTime.Now theo giờ Việt Nam (đơn hàng, báo cáo theo ngày đều dùng giờ địa phương)
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=Asia/Ho_Chi_Minh

COPY --from=build /app/publish .

# Thư mục ảnh/video tải lên (gắn volume để không mất khi cập nhật)
RUN mkdir -p /app/wwwroot/uploads && chown -R $APP_UID /app/wwwroot
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "SystemBase.BE.dll"]
