using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Finance.Billing;
using Regalis.Infrastructure.Persistence.Interceptors;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>DependencyInjection.AddInfrastructure needs two edits: <c>services.AddBillingFinance();</c>
/// and <c>provider.GetRequiredService&lt;BillingFinanceRulesInterceptor&gt;()</c> appended to the
/// AddInterceptors(...) call of AddDbContext.</summary>
public static class BillingFinanceRegistration
{
    public static IServiceCollection AddBillingFinance(this IServiceCollection services)
    {
        services.AddScoped<BillingFinanceRulesInterceptor>();
        services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
        services.AddScoped<IPurchaseBillService, PurchaseBillService>();
        services.AddScoped<IVendorPaymentService, VendorPaymentService>();
        services.AddScoped<IPartyLedgerService, PartyLedgerService>();
        services.AddScoped<IBankImportService, BankImportService>();
        services.AddScoped<IBankMatchService, BankMatchService>();
        services.AddScoped<ITdsService, TdsService>();
        services.AddScoped<IGstService, GstService>();
        services.AddScoped<IExpenseControlService, ExpenseControlService>();
        services.AddScoped<IPettyCashService, PettyCashService>();
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IFinanceReminderService, FinanceReminderService>();
        services.AddScoped<IAccountingExportService, AccountingExportService>();
        services.AddHostedService<BillingFinanceJobs>();
        return services;
    }
}
