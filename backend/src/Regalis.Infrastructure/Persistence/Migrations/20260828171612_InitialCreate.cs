using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Regalis.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MST_Amenity",
                columns: table => new
                {
                    AmenityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AmenityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IconName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_Amenity", x => x.AmenityId);
                });

            migrationBuilder.CreateTable(
                name: "MST_AppSetting",
                columns: table => new
                {
                    AppSettingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SettingGroup = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsEditableByAdmin = table.Column<bool>(type: "bit", nullable: false),
                    IsSecret = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_AppSetting", x => x.AppSettingId);
                });

            migrationBuilder.CreateTable(
                name: "MST_Location",
                columns: table => new
                {
                    LocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LocationName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContactNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    WorkingHoursStart = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    WorkingHoursEnd = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    WorkingDays = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_Location", x => x.LocationId);
                });

            migrationBuilder.CreateTable(
                name: "MST_NumberSequence",
                columns: table => new
                {
                    NumberSequenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SequenceKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CurrentValue = table.Column<long>(type: "bigint", nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_NumberSequence", x => x.NumberSequenceId);
                });

            migrationBuilder.CreateTable(
                name: "MST_Permission",
                columns: table => new
                {
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PermissionCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_Permission", x => x.PermissionId);
                });

            migrationBuilder.CreateTable(
                name: "MST_Role",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "bit", nullable: false),
                    PortalType = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_Role", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "MST_TicketCategory",
                columns: table => new
                {
                    TicketCategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_TicketCategory", x => x.TicketCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "MST_WorkspaceType",
                columns: table => new
                {
                    WorkspaceTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TypeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IconName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    RequiresCapacity = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_WorkspaceType", x => x.WorkspaceTypeId);
                });

            migrationBuilder.CreateTable(
                name: "TRN_AccessLog",
                columns: table => new
                {
                    AccessLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessTokenId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    ScannedTokenValue = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    WorkspaceId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    AccessMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    Result = table.Column<byte>(type: "tinyint", nullable: false),
                    DenyReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DeviceStatus = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IsManualOverride = table.Column<bool>(type: "bit", nullable: false),
                    OverrideByUserId = table.Column<long>(type: "bigint", nullable: true),
                    OverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LoggedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_AccessLog", x => x.AccessLogId);
                });

            migrationBuilder.CreateTable(
                name: "TRN_AuditLog",
                columns: table => new
                {
                    AuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_AuditLog", x => x.AuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "TRN_LoginHistory",
                columns: table => new
                {
                    LoginHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    AttemptedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AttemptedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_LoginHistory", x => x.LoginHistoryId);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Notification",
                columns: table => new
                {
                    NotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Audience = table.Column<byte>(type: "tinyint", nullable: false),
                    AudienceRefId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    ActionLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ActionRoute = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    SentUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_Notification", x => x.NotificationId);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Otp",
                columns: table => new
                {
                    OtpId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Channel = table.Column<byte>(type: "tinyint", nullable: false),
                    Purpose = table.Column<byte>(type: "tinyint", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    ConsumedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_Otp", x => x.OtpId);
                });

            migrationBuilder.CreateTable(
                name: "MST_Banner",
                columns: table => new
                {
                    BannerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LinkUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PromotionalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    PortalType = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_Banner", x => x.BannerId);
                    table.ForeignKey(
                        name: "FK_MST_Banner_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_InventoryItem",
                columns: table => new
                {
                    InventoryItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpeningQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_InventoryItem", x => x.InventoryItemId);
                    table.ForeignKey(
                        name: "FK_MST_InventoryItem_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_PassPlan",
                columns: table => new
                {
                    PassPlanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PassType = table.Column<byte>(type: "tinyint", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValidityDays = table.Column<int>(type: "int", nullable: false),
                    MaxGuests = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_PassPlan", x => x.PassPlanId);
                    table.ForeignKey(
                        name: "FK_MST_PassPlan_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_ServiceItem",
                columns: table => new
                {
                    ServiceItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_ServiceItem", x => x.ServiceItemId);
                    table.ForeignKey(
                        name: "FK_MST_ServiceItem_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_User",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserType = table.Column<byte>(type: "tinyint", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MobileNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PhotoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultLocationId = table.Column<int>(type: "int", nullable: true),
                    IsEmailVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsMobileVerified = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LastLoginUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LastPasswordChangeUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_User", x => x.UserId);
                    table.CheckConstraint("CK_User_UserType", "[UserType] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_MST_User_MST_Location_DefaultLocationId",
                        column: x => x.DefaultLocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_Menu",
                columns: table => new
                {
                    MenuId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentMenuId = table.Column<int>(type: "int", nullable: true),
                    MenuCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    MenuName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: true),
                    PortalType = table.Column<byte>(type: "tinyint", nullable: false),
                    MenuLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_Menu", x => x.MenuId);
                    table.ForeignKey(
                        name: "FK_MST_Menu_MST_Menu_ParentMenuId",
                        column: x => x.ParentMenuId,
                        principalTable: "MST_Menu",
                        principalColumn: "MenuId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MST_Menu_MST_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "MST_Permission",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MAP_RolePermission",
                columns: table => new
                {
                    RolePermissionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MAP_RolePermission", x => x.RolePermissionId);
                    table.ForeignKey(
                        name: "FK_MAP_RolePermission_MST_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "MST_Permission",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MAP_RolePermission_MST_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "MST_Role",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MST_TicketIssueType",
                columns: table => new
                {
                    TicketIssueTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketCategoryId = table.Column<int>(type: "int", nullable: false),
                    IssueTypeName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_TicketIssueType", x => x.TicketIssueTypeId);
                    table.ForeignKey(
                        name: "FK_MST_TicketIssueType_MST_TicketCategory_TicketCategoryId",
                        column: x => x.TicketCategoryId,
                        principalTable: "MST_TicketCategory",
                        principalColumn: "TicketCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MST_Workspace",
                columns: table => new
                {
                    WorkspaceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    WorkspaceTypeId = table.Column<int>(type: "int", nullable: false),
                    WorkspaceCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    WorkspaceName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FloorNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    PricePerHour = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PricePerDay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Equipment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    IsBookable = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_Workspace", x => x.WorkspaceId);
                    table.CheckConstraint("CK_Workspace_Capacity", "[Capacity] >= 1");
                    table.CheckConstraint("CK_Workspace_Price", "[PricePerHour] >= 0");
                    table.ForeignKey(
                        name: "FK_MST_Workspace_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MST_Workspace_MST_WorkspaceType_WorkspaceTypeId",
                        column: x => x.WorkspaceTypeId,
                        principalTable: "MST_WorkspaceType",
                        principalColumn: "WorkspaceTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_InventoryTransaction",
                columns: table => new
                {
                    InventoryTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryItemId = table.Column<int>(type: "int", nullable: false),
                    TransactionNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TransactionType = table.Column<byte>(type: "tinyint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceOrDestination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DamageDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DamageCause = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PhotoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    EnteredByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_InventoryTransaction", x => x.InventoryTransactionId);
                    table.CheckConstraint("CK_InvTxn_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_TRN_InventoryTransaction_MST_InventoryItem_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "MST_InventoryItem",
                        principalColumn: "InventoryItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MAP_UserRole",
                columns: table => new
                {
                    UserRoleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedDateUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    AssignedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MAP_UserRole", x => x.UserRoleId);
                    table.ForeignKey(
                        name: "FK_MAP_UserRole_MST_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "MST_Role",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MAP_UserRole_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MST_Wallet",
                columns: table => new
                {
                    WalletId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCredited = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDebited = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LastTransactionUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MST_Wallet", x => x.WalletId);
                    table.CheckConstraint("CK_Wallet_Balance", "[Balance] >= 0");
                    table.ForeignKey(
                        name: "FK_MST_Wallet_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_AccessToken",
                columns: table => new
                {
                    AccessTokenId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenValue = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    QrPayload = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    BookingId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerPassId = table.Column<long>(type: "bigint", nullable: true),
                    HousekeepingScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    WorkspaceId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    TokenPurpose = table.Column<byte>(type: "tinyint", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ValidToUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    MaxUsageCount = table.Column<int>(type: "int", nullable: true),
                    FirstUsedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LastUsedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_AccessToken", x => x.AccessTokenId);
                    table.ForeignKey(
                        name: "FK_TRN_AccessToken_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Attendance",
                columns: table => new
                {
                    AttendanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectType = table.Column<byte>(type: "tinyint", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    WorkspaceId = table.Column<int>(type: "int", nullable: true),
                    BookingId = table.Column<long>(type: "bigint", nullable: true),
                    HousekeepingScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckInUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CheckOutUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    TotalMinutes = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CheckInMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    CheckOutMethod = table.Column<byte>(type: "tinyint", nullable: true),
                    CheckInAccessLogId = table.Column<long>(type: "bigint", nullable: true),
                    CheckOutAccessLogId = table.Column<long>(type: "bigint", nullable: true),
                    IsException = table.Column<bool>(type: "bit", nullable: false),
                    ExceptionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsCorrected = table.Column<bool>(type: "bit", nullable: false),
                    CorrectedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OriginalCheckInUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    OriginalCheckOutUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_Attendance", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK_TRN_Attendance_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_CustomerPass",
                columns: table => new
                {
                    CustomerPassId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PassNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    PassPlanId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    PassType = table.Column<byte>(type: "tinyint", nullable: false),
                    PurchaseUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    GuestCount = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    UsedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_CustomerPass", x => x.CustomerPassId);
                    table.ForeignKey(
                        name: "FK_TRN_CustomerPass_MST_PassPlan_PassPlanId",
                        column: x => x.PassPlanId,
                        principalTable: "MST_PassPlan",
                        principalColumn: "PassPlanId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_CustomerPass_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_HousekeepingSchedule",
                columns: table => new
                {
                    HousekeepingScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StaffUserId = table.Column<long>(type: "bigint", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    WorkspaceId = table.Column<int>(type: "int", nullable: true),
                    AreaName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    TaskType = table.Column<byte>(type: "tinyint", nullable: false),
                    TaskDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    BlocksWorkspace = table.Column<bool>(type: "bit", nullable: false),
                    WorkspaceBlockId = table.Column<long>(type: "bigint", nullable: true),
                    CheckInUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CheckOutUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AssignedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_HousekeepingSchedule", x => x.HousekeepingScheduleId);
                    table.ForeignKey(
                        name: "FK_TRN_HousekeepingSchedule_MST_User_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Invoice",
                columns: table => new
                {
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    BookingId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerPassId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CgstPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SgstPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IgstPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IgstAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, computedColumnSql: "[TotalAmount] - [PaidAmount]", stored: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    PaidUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CustomerMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CustomerGstin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BillingAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyGstin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CompanyAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PdfPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_Invoice", x => x.InvoiceId);
                    table.ForeignKey(
                        name: "FK_TRN_Invoice_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Payment",
                columns: table => new
                {
                    PaymentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    BookingId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    CustomerPassId = table.Column<long>(type: "bigint", nullable: true),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<byte>(type: "tinyint", nullable: false),
                    Gateway = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    GatewayOrderId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    GatewayPaymentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    GatewaySignature = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsSignatureVerified = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PaidUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RefundReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RawGatewayResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_Payment", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_TRN_Payment_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_RefreshToken",
                columns: table => new
                {
                    RefreshTokenId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedByIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ConsumedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RevokedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_RefreshToken", x => x.RefreshTokenId);
                    table.ForeignKey(
                        name: "FK_TRN_RefreshToken_MST_User_UserId",
                        column: x => x.UserId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_ServiceOrder",
                columns: table => new
                {
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    OrderUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_ServiceOrder", x => x.ServiceOrderId);
                    table.ForeignKey(
                        name: "FK_TRN_ServiceOrder_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MAP_RoleMenu",
                columns: table => new
                {
                    RoleMenuId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    MenuId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MAP_RoleMenu", x => x.RoleMenuId);
                    table.ForeignKey(
                        name: "FK_MAP_RoleMenu_MST_Menu_MenuId",
                        column: x => x.MenuId,
                        principalTable: "MST_Menu",
                        principalColumn: "MenuId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MAP_RoleMenu_MST_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "MST_Role",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Ticket",
                columns: table => new
                {
                    TicketId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    TicketCategoryId = table.Column<int>(type: "int", nullable: false),
                    TicketIssueTypeId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    AssignedToUserId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    FirstResponseUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ResolvedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_Ticket", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_TRN_Ticket_MST_TicketCategory_TicketCategoryId",
                        column: x => x.TicketCategoryId,
                        principalTable: "MST_TicketCategory",
                        principalColumn: "TicketCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Ticket_MST_TicketIssueType_TicketIssueTypeId",
                        column: x => x.TicketIssueTypeId,
                        principalTable: "MST_TicketIssueType",
                        principalColumn: "TicketIssueTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Ticket_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MAP_WorkspaceAmenity",
                columns: table => new
                {
                    WorkspaceAmenityId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkspaceId = table.Column<int>(type: "int", nullable: false),
                    AmenityId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MAP_WorkspaceAmenity", x => x.WorkspaceAmenityId);
                    table.ForeignKey(
                        name: "FK_MAP_WorkspaceAmenity_MST_Amenity_AmenityId",
                        column: x => x.AmenityId,
                        principalTable: "MST_Amenity",
                        principalColumn: "AmenityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MAP_WorkspaceAmenity_MST_Workspace_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "MST_Workspace",
                        principalColumn: "WorkspaceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_WorkspaceBlock",
                columns: table => new
                {
                    WorkspaceBlockId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkspaceId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    BlockReason = table.Column<byte>(type: "tinyint", nullable: false),
                    ReasonNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HousekeepingScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    BlockedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ReleasedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ReleasedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_WorkspaceBlock", x => x.WorkspaceBlockId);
                    table.CheckConstraint("CK_Block_Period", "[EndUtc] > [StartUtc]");
                    table.ForeignKey(
                        name: "FK_TRN_WorkspaceBlock_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_WorkspaceBlock_MST_Workspace_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "MST_Workspace",
                        principalColumn: "WorkspaceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_WalletTransaction",
                columns: table => new
                {
                    WalletTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false),
                    Category = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReferenceId = table.Column<long>(type: "bigint", nullable: true),
                    PaymentId = table.Column<long>(type: "bigint", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TransactionUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PerformedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_WalletTransaction", x => x.WalletTransactionId);
                    table.ForeignKey(
                        name: "FK_TRN_WalletTransaction_MST_Wallet_WalletId",
                        column: x => x.WalletId,
                        principalTable: "MST_Wallet",
                        principalColumn: "WalletId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Booking",
                columns: table => new
                {
                    BookingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    WorkspaceId = table.Column<int>(type: "int", nullable: false),
                    WorkspaceTypeId = table.Column<int>(type: "int", nullable: false),
                    BookingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    BookingSource = table.Column<byte>(type: "tinyint", nullable: false),
                    CustomerPassId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CheckInUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CheckOutUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancelledByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_Booking", x => x.BookingId);
                    table.CheckConstraint("CK_Booking_Amounts", "[BaseAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0");
                    table.CheckConstraint("CK_Booking_Period", "[EndUtc] > [StartUtc]");
                    table.ForeignKey(
                        name: "FK_TRN_Booking_MST_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "MST_Location",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Booking_MST_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "MST_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Booking_MST_WorkspaceType_WorkspaceTypeId",
                        column: x => x.WorkspaceTypeId,
                        principalTable: "MST_WorkspaceType",
                        principalColumn: "WorkspaceTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Booking_MST_Workspace_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "MST_Workspace",
                        principalColumn: "WorkspaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Booking_TRN_CustomerPass_CustomerPassId",
                        column: x => x.CustomerPassId,
                        principalTable: "TRN_CustomerPass",
                        principalColumn: "CustomerPassId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_InvoiceLine",
                columns: table => new
                {
                    InvoiceLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HsnSacCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_InvoiceLine", x => x.InvoiceLineId);
                    table.ForeignKey(
                        name: "FK_TRN_InvoiceLine_TRN_Invoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "TRN_Invoice",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_ServiceOrderLine",
                columns: table => new
                {
                    ServiceOrderLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceOrderId = table.Column<long>(type: "bigint", nullable: false),
                    ServiceItemId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_ServiceOrderLine", x => x.ServiceOrderLineId);
                    table.ForeignKey(
                        name: "FK_TRN_ServiceOrderLine_MST_ServiceItem_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "MST_ServiceItem",
                        principalColumn: "ServiceItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_ServiceOrderLine_TRN_ServiceOrder_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "TRN_ServiceOrder",
                        principalColumn: "ServiceOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_TicketAttachment",
                columns: table => new
                {
                    TicketAttachmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    TicketMessageId = table.Column<long>(type: "bigint", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_TicketAttachment", x => x.TicketAttachmentId);
                    table.ForeignKey(
                        name: "FK_TRN_TicketAttachment_TRN_Ticket_TicketId",
                        column: x => x.TicketId,
                        principalTable: "TRN_Ticket",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_TicketMessage",
                columns: table => new
                {
                    TicketMessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: false),
                    SenderType = table.Column<byte>(type: "tinyint", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInternalNote = table.Column<bool>(type: "bit", nullable: false),
                    SentUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_TicketMessage", x => x.TicketMessageId);
                    table.ForeignKey(
                        name: "FK_TRN_TicketMessage_TRN_Ticket_TicketId",
                        column: x => x.TicketId,
                        principalTable: "TRN_Ticket",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TRN_BookingStatusHistory",
                columns: table => new
                {
                    BookingStatusHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    FromStatus = table.Column<byte>(type: "tinyint", nullable: true),
                    ToStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ChangedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ChangedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TRN_BookingStatusHistory", x => x.BookingStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_TRN_BookingStatusHistory_TRN_Booking_BookingId",
                        column: x => x.BookingId,
                        principalTable: "TRN_Booking",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MAP_RoleMenu_MenuId",
                table: "MAP_RoleMenu",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "UX_RoleMenu",
                table: "MAP_RoleMenu",
                columns: new[] { "RoleId", "MenuId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MAP_RolePermission_PermissionId",
                table: "MAP_RolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_RolePermission",
                table: "MAP_RolePermission",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MAP_UserRole_RoleId",
                table: "MAP_UserRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UX_UserRole",
                table: "MAP_UserRole",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MAP_WorkspaceAmenity_AmenityId",
                table: "MAP_WorkspaceAmenity",
                column: "AmenityId");

            migrationBuilder.CreateIndex(
                name: "UX_WorkspaceAmenity",
                table: "MAP_WorkspaceAmenity",
                columns: new[] { "WorkspaceId", "AmenityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AppSetting_Key",
                table: "MST_AppSetting",
                column: "SettingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MST_Banner_LocationId",
                table: "MST_Banner",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MST_InventoryItem_LocationId",
                table: "MST_InventoryItem",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryItem_Code",
                table: "MST_InventoryItem",
                column: "ItemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Location_Code",
                table: "MST_Location",
                column: "LocationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MST_Menu_ParentMenuId",
                table: "MST_Menu",
                column: "ParentMenuId");

            migrationBuilder.CreateIndex(
                name: "IX_MST_Menu_PermissionId",
                table: "MST_Menu",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_Menu_Code",
                table: "MST_Menu",
                column: "MenuCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_NumberSequence_Key_Year",
                table: "MST_NumberSequence",
                columns: new[] { "SequenceKey", "FinancialYear" },
                unique: true,
                filter: "[FinancialYear] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MST_PassPlan_LocationId",
                table: "MST_PassPlan",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "UX_PassPlan_Code",
                table: "MST_PassPlan",
                column: "PlanCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Permission_Code",
                table: "MST_Permission",
                column: "PermissionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Role_Code",
                table: "MST_Role",
                column: "RoleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MST_ServiceItem_LocationId",
                table: "MST_ServiceItem",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "UX_ServiceItem_Code",
                table: "MST_ServiceItem",
                column: "ItemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MST_TicketIssueType_TicketCategoryId",
                table: "MST_TicketIssueType",
                column: "TicketCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MST_User_DefaultLocationId",
                table: "MST_User",
                column: "DefaultLocationId");

            migrationBuilder.CreateIndex(
                name: "UX_User_Email",
                table: "MST_User",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_User_Mobile",
                table: "MST_User",
                column: "MobileNumber",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Wallet_Customer",
                table: "MST_Wallet",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MST_Workspace_WorkspaceTypeId",
                table: "MST_Workspace",
                column: "WorkspaceTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Workspace_Location_Code",
                table: "MST_Workspace",
                columns: new[] { "LocationId", "WorkspaceCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_WorkspaceType_Code",
                table: "MST_WorkspaceType",
                column: "TypeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_Logged",
                table: "TRN_AccessLog",
                column: "LoggedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_Result_Logged",
                table: "TRN_AccessLog",
                columns: new[] { "Result", "LoggedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessLog_User_Logged",
                table: "TRN_AccessLog",
                columns: new[] { "UserId", "LoggedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessToken_Booking",
                table: "TRN_AccessToken",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessToken_User_Status",
                table: "TRN_AccessToken",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_AccessToken_Value",
                table: "TRN_AccessToken",
                column: "TokenValue",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_Date_Location",
                table: "TRN_Attendance",
                columns: new[] { "AttendanceDate", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_User_Date",
                table: "TRN_Attendance",
                columns: new[] { "UserId", "AttendanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Entity",
                table: "TRN_AuditLog",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp",
                table: "TRN_AuditLog",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_User_Timestamp",
                table: "TRN_AuditLog",
                columns: new[] { "UserId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Booking_Customer_Start",
                table: "TRN_Booking",
                columns: new[] { "CustomerId", "StartUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Booking_Date_Status",
                table: "TRN_Booking",
                columns: new[] { "BookingDate", "Status" })
                .Annotation("SqlServer:Include", new[] { "LocationId", "TotalAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_Booking_Location_Start",
                table: "TRN_Booking",
                columns: new[] { "LocationId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Booking_Workspace_Period",
                table: "TRN_Booking",
                columns: new[] { "WorkspaceId", "StartUtc", "EndUtc" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Status", "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Booking_CustomerPassId",
                table: "TRN_Booking",
                column: "CustomerPassId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Booking_WorkspaceTypeId",
                table: "TRN_Booking",
                column: "WorkspaceTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Booking_Number",
                table: "TRN_Booking",
                column: "BookingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Booking_Workspace_Start",
                table: "TRN_Booking",
                columns: new[] { "WorkspaceId", "StartUtc" },
                unique: true,
                filter: "[Status] IN (1,2,3) AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistory_Booking",
                table: "TRN_BookingStatusHistory",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPass_Customer_Status",
                table: "TRN_CustomerPass",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPass_ValidTo_Status",
                table: "TRN_CustomerPass",
                columns: new[] { "ValidTo", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_CustomerPass_PassPlanId",
                table: "TRN_CustomerPass",
                column: "PassPlanId");

            migrationBuilder.CreateIndex(
                name: "UX_CustomerPass_Number",
                table: "TRN_CustomerPass",
                column: "PassNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HkSchedule_Date_Status",
                table: "TRN_HousekeepingSchedule",
                columns: new[] { "ScheduledDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HkSchedule_Staff_Date",
                table: "TRN_HousekeepingSchedule",
                columns: new[] { "StaffUserId", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HkSchedule_Workspace_Period",
                table: "TRN_HousekeepingSchedule",
                columns: new[] { "WorkspaceId", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_HkSchedule_Number",
                table: "TRN_HousekeepingSchedule",
                column: "ScheduleNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvTxn_Item_Date",
                table: "TRN_InventoryTransaction",
                columns: new[] { "InventoryItemId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InvTxn_Type_Date",
                table: "TRN_InventoryTransaction",
                columns: new[] { "TransactionType", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "UX_InvTxn_Number",
                table: "TRN_InventoryTransaction",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_Customer_Date",
                table: "TRN_Invoice",
                columns: new[] { "CustomerId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_Status_Date",
                table: "TRN_Invoice",
                columns: new[] { "Status", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "UX_Invoice_Number",
                table: "TRN_Invoice",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_InvoiceLine_InvoiceId",
                table: "TRN_InvoiceLine",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistory_Attempted",
                table: "TRN_LoginHistory",
                column: "AttemptedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Scheduled",
                table: "TRN_Notification",
                column: "ScheduledForUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_User_Created",
                table: "TRN_Notification",
                columns: new[] { "UserId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_User_IsRead",
                table: "TRN_Notification",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Otp_Destination_Purpose",
                table: "TRN_Otp",
                columns: new[] { "Destination", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Customer_Date",
                table: "TRN_Payment",
                columns: new[] { "CustomerId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Status_Date",
                table: "TRN_Payment",
                columns: new[] { "Status", "PaidUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Payment_GatewayPaymentId",
                table: "TRN_Payment",
                column: "GatewayPaymentId",
                unique: true,
                filter: "[GatewayPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Payment_Number",
                table: "TRN_Payment",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_User_Family",
                table: "TRN_RefreshToken",
                columns: new[] { "UserId", "FamilyId" });

            migrationBuilder.CreateIndex(
                name: "UX_RefreshToken_Hash",
                table: "TRN_RefreshToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrder_Customer_Date",
                table: "TRN_ServiceOrder",
                columns: new[] { "CustomerId", "OrderUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_ServiceOrder_Number",
                table: "TRN_ServiceOrder",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ServiceOrderLine_ServiceItemId",
                table: "TRN_ServiceOrderLine",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ServiceOrderLine_ServiceOrderId",
                table: "TRN_ServiceOrderLine",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Assigned_Status",
                table: "TRN_Ticket",
                columns: new[] { "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Customer_Created",
                table: "TRN_Ticket",
                columns: new[] { "CustomerId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Status_Priority",
                table: "TRN_Ticket",
                columns: new[] { "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Ticket_TicketCategoryId",
                table: "TRN_Ticket",
                column: "TicketCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Ticket_TicketIssueTypeId",
                table: "TRN_Ticket",
                column: "TicketIssueTypeId");

            migrationBuilder.CreateIndex(
                name: "UX_Ticket_Number",
                table: "TRN_Ticket",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_TicketAttachment_TicketId",
                table: "TRN_TicketAttachment",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_TicketMessage_TicketId",
                table: "TRN_TicketMessage",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_WalletTransaction_WalletId",
                table: "TRN_WalletTransaction",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxn_Category",
                table: "TRN_WalletTransaction",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTxn_Customer_Date",
                table: "TRN_WalletTransaction",
                columns: new[] { "CustomerId", "TransactionUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "UX_WalletTxn_Number",
                table: "TRN_WalletTransaction",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Block_Workspace_Period",
                table: "TRN_WorkspaceBlock",
                columns: new[] { "WorkspaceId", "StartUtc", "EndUtc" },
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_WorkspaceBlock_LocationId",
                table: "TRN_WorkspaceBlock",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MAP_RoleMenu");

            migrationBuilder.DropTable(
                name: "MAP_RolePermission");

            migrationBuilder.DropTable(
                name: "MAP_UserRole");

            migrationBuilder.DropTable(
                name: "MAP_WorkspaceAmenity");

            migrationBuilder.DropTable(
                name: "MST_AppSetting");

            migrationBuilder.DropTable(
                name: "MST_Banner");

            migrationBuilder.DropTable(
                name: "MST_NumberSequence");

            migrationBuilder.DropTable(
                name: "TRN_AccessLog");

            migrationBuilder.DropTable(
                name: "TRN_AccessToken");

            migrationBuilder.DropTable(
                name: "TRN_Attendance");

            migrationBuilder.DropTable(
                name: "TRN_AuditLog");

            migrationBuilder.DropTable(
                name: "TRN_BookingStatusHistory");

            migrationBuilder.DropTable(
                name: "TRN_HousekeepingSchedule");

            migrationBuilder.DropTable(
                name: "TRN_InventoryTransaction");

            migrationBuilder.DropTable(
                name: "TRN_InvoiceLine");

            migrationBuilder.DropTable(
                name: "TRN_LoginHistory");

            migrationBuilder.DropTable(
                name: "TRN_Notification");

            migrationBuilder.DropTable(
                name: "TRN_Otp");

            migrationBuilder.DropTable(
                name: "TRN_Payment");

            migrationBuilder.DropTable(
                name: "TRN_RefreshToken");

            migrationBuilder.DropTable(
                name: "TRN_ServiceOrderLine");

            migrationBuilder.DropTable(
                name: "TRN_TicketAttachment");

            migrationBuilder.DropTable(
                name: "TRN_TicketMessage");

            migrationBuilder.DropTable(
                name: "TRN_WalletTransaction");

            migrationBuilder.DropTable(
                name: "TRN_WorkspaceBlock");

            migrationBuilder.DropTable(
                name: "MST_Menu");

            migrationBuilder.DropTable(
                name: "MST_Role");

            migrationBuilder.DropTable(
                name: "MST_Amenity");

            migrationBuilder.DropTable(
                name: "TRN_Booking");

            migrationBuilder.DropTable(
                name: "MST_InventoryItem");

            migrationBuilder.DropTable(
                name: "TRN_Invoice");

            migrationBuilder.DropTable(
                name: "MST_ServiceItem");

            migrationBuilder.DropTable(
                name: "TRN_ServiceOrder");

            migrationBuilder.DropTable(
                name: "TRN_Ticket");

            migrationBuilder.DropTable(
                name: "MST_Wallet");

            migrationBuilder.DropTable(
                name: "MST_Permission");

            migrationBuilder.DropTable(
                name: "MST_Workspace");

            migrationBuilder.DropTable(
                name: "TRN_CustomerPass");

            migrationBuilder.DropTable(
                name: "MST_TicketIssueType");

            migrationBuilder.DropTable(
                name: "MST_WorkspaceType");

            migrationBuilder.DropTable(
                name: "MST_PassPlan");

            migrationBuilder.DropTable(
                name: "MST_User");

            migrationBuilder.DropTable(
                name: "MST_TicketCategory");

            migrationBuilder.DropTable(
                name: "MST_Location");
        }
    }
}
