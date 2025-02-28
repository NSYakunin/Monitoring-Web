using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Monitoring.Application.Interfaces;
using Monitoring.Application.Services;
using Monitoring.Infrastructure.Services;
using QuestPDF.Infrastructure;

namespace Monitoring.UI.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Метод расширения для регистрации всех необходимых сервисов приложения.
        /// </summary>
        public static IServiceCollection AddMonitoringServices(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            // Например, сразу укажем лицензию QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;

            // Подключаем Razor Pages и отключаем Antiforgery
            services.AddRazorPages(options => {
                options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
            });

            // Кэш
            services.AddMemoryCache();

            // Наши сервисы
            services.AddScoped<IWorkItemService, WorkItemService>();
            services.AddScoped<ILoginService, LoginService>();
            services.AddScoped<IUserSettingsService, UserSettingsService>();
            services.AddTransient<INotificationService, NotificationService>();
            services.AddTransient<IWorkRequestService, WorkRequestService>();

            return services;
        }
    }
}