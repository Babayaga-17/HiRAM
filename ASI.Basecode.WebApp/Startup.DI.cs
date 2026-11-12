using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Authentication;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ASI.Basecode.WebApp
{
    // Other services configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Configures the other services.
        /// </summary>
        private void ConfigureOtherServices()
        {
            // Framework
            this._services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            this._services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();

            // Common
            this._services.AddScoped<TokenProvider>();
            this._services.TryAddSingleton<TokenProviderOptionsFactory>();
            this._services.TryAddSingleton<TokenValidationParametersFactory>();
            this._services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Services
            this._services.TryAddSingleton<TokenValidationParametersFactory>();
            this._services.AddScoped<IAccountService, AccountService>();
            this._services.AddScoped<IEquipmentCategoryService, EquipmentCategoryService>();
            this._services.AddScoped<IEquipmentService, EquipmentService>();
            this._services.AddScoped<IEquipmentRepository, EquipmentRepository>();
            this._services.AddScoped<IPasswordResetService, PasswordResetService>();
            this._services.AddScoped<IBorrowerProfileService, BorrowerProfileService>();
            this._services.AddScoped<IUserAccountService, UserAccountService>();
            this._services.AddScoped<IReservationService, ReservationService>();
            this._services.AddScoped<IReportService, ReportService>();
            this._services.AddScoped<IDashboardService, DashboardService>();
            this._services.AddHttpClient<IEquipmentPhotoService, EquipmentPhotoService>();
            this._services.AddHttpClient<IProfilePhotoService, ProfilePhotoService>();

            this._services.Configure<SupabaseStorageOptions>(this.Configuration.GetSection(SupabaseStorageOptions.SectionName));

            this._services.AddScoped<IMaintenanceService, MaintenanceService>();
            this._services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();

            // Repositories
            this._services.AddScoped<INotificationRepository, NotificationRepository>();
            this._services.AddScoped<INotificationService, NotificationService>();
            this._services.AddScoped<INotificationEmailDispatcher, NotificationEmailDispatcher>();
            this._services.AddHostedService<Workers.NotificationEmailWorker>();
            this._services.AddScoped<IBorrowerProfileRepository, BorrowerProfileRepository>();
            this._services.AddScoped<IUserRepository, UserRepository>();
            this._services.AddScoped<IAccountRepository, AccountRepository>();
            this._services.AddScoped<IEquipmentCategoryRepository, EquipmentCategoryRepository>();
            this._services.AddScoped<IEquipmentRepository, EquipmentRepository>();
            this._services.AddScoped<IReservationRepository, ReservationRepository>();
            this._services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
            this._services.AddScoped<IRoleRepository, RoleRepository>();
            this._services.AddScoped<IReportRepository, ReportRepository>();

            // Manager Class
            this._services.AddScoped<SignInManager>();
            this._services.AddScoped<IEmailManager, EmailManager>();


            this._services.AddHttpClient();
        }
    }
}

