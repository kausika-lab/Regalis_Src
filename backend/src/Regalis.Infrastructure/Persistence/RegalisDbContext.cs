using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Regalis.Domain.Common;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Operations;

namespace Regalis.Infrastructure.Persistence;

public class RegalisDbContext(DbContextOptions<RegalisDbContext> options) : DbContext(options)
{
    // Identity & Access
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Otp> Otps => Set<Otp>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();

    // Masters
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<WorkspaceType> WorkspaceTypes => Set<WorkspaceType>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<WorkspaceAmenity> WorkspaceAmenities => Set<WorkspaceAmenity>();
    public DbSet<PassPlan> PassPlans => Set<PassPlan>();
    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketIssueType> TicketIssueTypes => Set<TicketIssueType>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    // Booking & Calendar
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<WorkspaceBlock> WorkspaceBlocks => Set<WorkspaceBlock>();

    // Finance
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<CustomerPass> CustomerPasses => Set<CustomerPass>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ServiceOrderLine> ServiceOrderLines => Set<ServiceOrderLine>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderAttachment> PurchaseOrderAttachments => Set<PurchaseOrderAttachment>();
    public DbSet<PurchaseOrderStatusHistory> PurchaseOrderStatusHistory => Set<PurchaseOrderStatusHistory>();
    public DbSet<MaintenanceWorkItem> MaintenanceWorkItems => Set<MaintenanceWorkItem>();
    public DbSet<HousekeeperProfile> HousekeeperProfiles => Set<HousekeeperProfile>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();
    public DbSet<QuotationStatusHistory> QuotationStatusHistories => Set<QuotationStatusHistory>();
    public DbSet<QuotationAttachment> QuotationAttachments => Set<QuotationAttachment>();
    public DbSet<QuotationEvent> QuotationEvents => Set<QuotationEvent>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();

    // Access & Attendance
    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();
    public DbSet<Attendance> Attendances => Set<Attendance>();

    // Operations
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<HousekeepingSchedule> HousekeepingSchedules => Set<HousekeepingSchedule>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // DATABASE_DESIGN §1: money DECIMAL(18,2), timestamps DATETIME2(3) UTC.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2(3)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RegalisDbContext).Assembly);

        // Global soft-delete filter: nothing in Phase 1 reads deleted rows by default.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletableEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Equal(
                Expression.Property(parameter, nameof(ISoftDeletableEntity.IsDeleted)),
                Expression.Constant(false));
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(body, parameter));
        }

        // Matching filters for the required children of soft-deletable principals
        // (UserRole, InvoiceLine, RefreshToken, ...): rows whose parent is deleted
        // disappear with it. Without these, EF warns that filtering the principal
        // out of a required relationship can produce unexpected results.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || typeof(ISoftDeletableEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            Expression? body = null;
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                var navigation = foreignKey.DependentToPrincipal;
                if (!foreignKey.IsRequired
                    || navigation is null
                    || !typeof(ISoftDeletableEntity).IsAssignableFrom(foreignKey.PrincipalEntityType.ClrType))
                {
                    continue;
                }

                var parentNotDeleted = Expression.Equal(
                    Expression.Property(
                        Expression.Property(parameter, navigation.Name),
                        nameof(ISoftDeletableEntity.IsDeleted)),
                    Expression.Constant(false));
                body = body is null ? parentNotDeleted : Expression.AndAlso(body, parentNotDeleted);
            }

            if (body is not null)
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(Expression.Lambda(body, parameter));
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
