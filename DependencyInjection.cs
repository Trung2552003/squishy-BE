using Microsoft.Extensions.DependencyInjection;
using SystemBase.BE.Services.Auth;
using SystemBase.BE.Services.SystemFunction;
using SystemBase.BE.Services.SystemRole;
using SystemBase.BE.Services.LoginLog;
using SystemBase.BE.Services.ActionLog;
using SystemBase.BE.Services.System.SystemSecuritySetting;
using SystemBase.BE.Services.User;
using SystemBase.BE.Services.ErrorLog;
using SystemBase.BE.Services.System.Email;
using SystemBase.BE.Infrastructure.Dapper;

using SystemBase.BE.Services.Squishy;

namespace SystemBase.BE
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Cấu hình Dapper Connection Factory
            services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

            // Đăng ký Service vào DI container
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<ISystemFunctionService, SystemFunctionService>();
            services.AddScoped<ILoginLogService, LoginLogService>();
            services.AddScoped<ISystemRoleService, SystemRoleService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IActionLogService, ActionLogService>();
            services.AddScoped<IErrorLogService, ErrorLogService>();
            services.AddScoped<ISystemSecuritySettingService, SystemSecuritySettingService>();

            // Đăng ký Squishy Store Services
            services.AddScoped<IMediaService, MediaService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<IReturnService, ReturnService>();
            services.AddScoped<IPreorderService, PreorderService>();
            services.AddScoped<ICashTransactionService, CashTransactionService>();

            services.AddHttpContextAccessor();

            return services;
        }
    }
}
