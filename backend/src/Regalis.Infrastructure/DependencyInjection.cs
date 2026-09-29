using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Abstractions;
using Regalis.Application.Access;
using Regalis.Application.Administration;
using Regalis.Application.Auth;
using Regalis.Application.Bookings;
using Regalis.Application.Finance;
using Regalis.Application.Masters;
using Regalis.Application.Operations;
using Regalis.Application.Reporting;
using Regalis.Infrastructure.Adapters;
using Regalis.Infrastructure.BackgroundJobs;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Persistence.Interceptors;
using Regalis.Infrastructure.Reporting;
using Regalis.Infrastructure.Persistence.Seed;
using Regalis.Infrastructure.Security;
using Regalis.Infrastructure.Services;

namespace Regalis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<AdminEventInterceptor>();

        services.AddDbContext<RegalisDbContext>((provider, dbOptions) =>
        {
            dbOptions.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3));
            dbOptions.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>(),
                // Raises admin notifications for payments, wallet entries, new customers,
                // customer profile edits, purchase orders and maintenance status changes.
                provider.GetRequiredService<AdminEventInterceptor>());
        });

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        // Singleton: the Google verifier caches Google's signing keys between calls.
        // An empty client id leaves it reporting "not configured" rather than failing.
        services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();

        services.AddMemoryCache();
        services.AddScoped<ISettingService, SettingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMenuQueryService, MenuQueryService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IRoleAdminService, RoleAdminService>();
        services.AddScoped<IMenuAdminService, MenuAdminService>();
        services.AddScoped<DatabaseSeeder>();

        // Adapter selection by configuration (Invariant 5): Otp:Channel = "Smtp"
        // sends real mail; anything else stays on the console sender so a fresh
        // clone runs without SMTP credentials (execution-plan risk #3).
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<GraphMailOptions>(configuration.GetSection(GraphMailOptions.SectionName));
        services.AddScoped<ConsoleOtpSender>();
        services.AddScoped<SmtpOtpSender>();
        // Singleton: it holds a ClientSecretCredential, which caches the Graph access
        // token and refreshes it on expiry. A scoped sender would re-authenticate per
        // request. It is stateless apart from that cache, so sharing it is safe.
        services.AddSingleton<GraphOtpSender>();
        services.AddScoped<IOtpSender>(provider => configuration["Otp:Channel"]?.ToLowerInvariant() switch
        {
            "graph" => provider.GetRequiredService<GraphOtpSender>(),
            "smtp" => provider.GetRequiredService<SmtpOtpSender>(),
            _ => provider.GetRequiredService<ConsoleOtpSender>()
        });

        // Masters
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IServiceCatalogueService, ServiceCatalogueService>();
        services.AddScoped<IBannerService, BannerService>();
        services.AddScoped<ISettingAdminService, SettingAdminService>();

        // Booking engine
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddHostedService<BookingHoldSweeper>();

        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IBookingMutationService, BookingMutationService>();

        // Finance
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IWalletAdminService, WalletAdminService>();
        services.AddScoped<IWalletLedgerQueries, WalletLedgerQueries>();
        services.AddScoped<IWalletDeskService, WalletDeskService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPassService, PassService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IQuotationService, QuotationService>();
        services.AddScoped<IBankService, BankService>();

        // Operations
        services.AddScoped<INotificationService, NotificationService>();
        // The admin Notifications page: feed, per-admin read state and settings; the
        // worker sends the email / WhatsApp copies and raises overdue invoices.
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();
        services.AddScoped<AdminNotificationJobs>();
        services.AddHostedService<AdminNotificationWorker>();
        services.AddScoped<IHousekeepingService, HousekeepingService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddHostedService<MaintenanceOverdueSweeper>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddScoped<ITicketService, TicketService>();

        // Reporting & dashboards
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IProfileService, ProfileService>();

        // Access & attendance — the device and payroll seams are adapters
        // selected by configuration (Invariant 5, A01/A03).
        services.AddScoped<AttendanceRecorder>();
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IAccessControlAdapter, MockAccessControlAdapter>();
        services.AddScoped<IPayrollExporter, CsvPayrollExporter>();

        // Payment gateway — chosen by Payments:Provider (Invariant 5). Sandbox is
        // registered alongside so the demo checkout can sign in dev/test.
        // Quotation delivery — email through the same SMTP account the OTP sender
        // uses, WhatsApp through the Business API when one is configured and a wa.me
        // link to send by hand when it is not (Invariant 5, document §3.5).
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.AddHttpClient(nameof(QuotationDispatcher));
        services.AddScoped<IQuotationDispatcher, QuotationDispatcher>();

        services.AddScoped<SandboxPaymentGateway>();
        services.AddHttpClient<RazorpayPaymentGateway>();
        services.AddScoped<IPaymentGateway>(provider =>
            string.Equals(configuration["Payments:Provider"], RazorpayPaymentGateway.Name,
                StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<RazorpayPaymentGateway>()
                : provider.GetRequiredService<SandboxPaymentGateway>());

        return services;
    }
}
