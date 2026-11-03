using NTSW_CENOPS.Application.Files;
using NTSW_CENOPS.Application.Stories;
using NTSW_CENOPS.Infrastructure.Stories;
using NTSW_CENOPS.Infrastructure.Files;
using NTSW_CENOPS.FileStreaming;
using NTSW_CENOPS.Application.Administration;
using NTSW_CENOPS.Application.Access;
using NTSW_CENOPS.Infrastructure.Authorization;
using NTSW_CENOPS.Application.Common.Identity;
using NTSW_CENOPS.Application.Monitoring;
using NTSW_CENOPS.Infrastructure.Identity;
using NTSW_CENOPS.Infrastructure.Knowledge;
using NTSW_CENOPS.Infrastructure.Notes;
using NTSW_CENOPS.Infrastructure.Tasks;
using NTSW_CENOPS.Infrastructure.Announcements;
using NTSW_CENOPS.Infrastructure.Jobs;
using NTSW_CENOPS.Infrastructure.Reports;
using NTSW_CENOPS.Infrastructure.Dashboards;
using NTSW_CENOPS.Application.Events;
using NTSW_CENOPS.Infrastructure.Events;
using NTSW_CENOPS.Application.Dashboards;
using NTSW_CENOPS.Application.Reports;
using NTSW_CENOPS.Application.Jobs;
using NTSW_CENOPS.Application.Notes;
using NTSW_CENOPS.Application.Tasks;
using NTSW_CENOPS.Application.Announcements;
using NTSW_CENOPS.Application.Knowledge;
using NTSW_CENOPS.Application.Chat;
using NTSW_CENOPS.Application.Profiles;
using NTSW_CENOPS.Infrastructure.Chat;
using NTSW_CENOPS.Infrastructure.Monitoring;
using NTSW_CENOPS.Infrastructure.Persistence;
using NTSW_CENOPS.Application.DatabaseInspector;
using NTSW_CENOPS.Infrastructure.DatabaseInspector;
using NTSW_CENOPS.Application.SourceIntelligence;
using NTSW_CENOPS.Infrastructure.SourceIntelligence;
using NTSW_CENOPS.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NTSW_CENOPS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' was not found. Configure it in appsettings or User Secrets.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));
        // Scoped factory creates isolated contexts for concurrent Blazor authorization checks.
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(connectionString), ServiceLifetime.Scoped);

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = false;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
        services.AddScoped<IIdentityGateway, AspNetIdentityGateway>();
        services.AddScoped<IIdentityAdministrationService, IdentityAdministrationService>();
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<IUserHoverProfileService, UserHoverProfileService>();
        services.Configure<SourceIntelligenceOptions>(configuration.GetSection("SourceIntelligence"));
        services.AddSingleton<ISourceIntelligenceService, SourceIntelligenceService>();
        services.AddScoped<IDatabaseInspectorService, SqlServerDatabaseInspectorService>();
        services.AddScoped<IChatRoomService, ChatRoomService>();
        services.AddScoped<IChatMessageService, ChatMessageService>();
        services.AddScoped<IChatSearchService, ChatSearchService>();
        services.AddScoped<ICurlService, CurlService>();
        services.AddScoped<ISqlQueryService, SqlQueryService>();
        // Single integration boundary: all consumers use IAppFileGateway, never FileStreaming.
        services.Configure<FileUploadPolicy>(configuration.GetSection("FileUpload"));
        var root = configuration["FileStreaming:RootDirectory"];
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NTSW_CENOPS", "file-storage");
        var max = configuration.GetValue<long?>("FileUpload:MaximumBytes") ?? 25L * 1024 * 1024;
        services.AddCenopsFileStreaming(options =>
        {
            options.RootDirectory = Path.GetFullPath(root);
            options.MaximumUploadBytes = max;
        });
        services.AddScoped<IFileCheckService, FileCheckService>();
        services.AddScoped<IAppFileGateway, AppFileGateway>();
        services.AddScoped<IFileResourceAccess, FileResourceAccess>();
        services.AddScoped<IReleaseStoryService, ReleaseStoryService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<ITodoService, TodoService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddSingleton<JobSourceCatalog>();
        services.AddSingleton<IJobSourceCatalog>(sp => sp.GetRequiredService<JobSourceCatalog>());
        services.AddScoped<IAutomatedJobService, AutomatedJobService>();
        services.AddScoped<IAutomatedJobDataService, AutomatedJobDataService>();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddScoped<IDashboardConfigurationService, DashboardConfigurationService>();
        services.AddScoped<IDashboardContentService, DashboardContentService>();
        services.AddScoped<IMonitoringReportService, MonitoringReportService>();
        services.AddScoped<IMonitoringReportChartService, MonitoringReportChartService>();
        services.AddScoped<AutomatedJobRunner>();
        services.AddSingleton<JobQueryExecutor>();
        services.AddHostedService<AutomatedJobWorker>();
        services.AddScoped<IHealthMonitoringRepository, EfHealthMonitoringRepository>();
        services.AddScoped<IHealthProbe, HttpHealthProbe>();

        services.AddHttpClient("health", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHostedService<HealthPollingWorker>();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/";
            options.AccessDeniedPath = "/access-denied";
            options.Cookie.Name = "NTSW_CENOPS.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        services.AddAuthorization();

        return services;
    }
}
