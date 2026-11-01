using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyHolidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    NormalizedSerialNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceDealer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UnitCostMinor = table.Column<long>(type: "bigint", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RetiredReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => new { x.TenantId, x.Key });
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    VatRateBps = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HoursPerDay = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    OvertimeMultiplierBps = table.Column<int>(type: "int", nullable: false),
                    VarianceBasis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PayrollRunsPerMonth = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: true),
                    HoursPerDay = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    OvertimeMultiplierBps = table.Column<int>(type: "int", nullable: false),
                    RunsPerMonth = table.Column<int>(type: "int", nullable: false),
                    StatutoryBasis = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestDayBps = table.Column<int>(type: "int", nullable: false),
                    SpecialDayBps = table.Column<int>(type: "int", nullable: false),
                    SpecialRestDayBps = table.Column<int>(type: "int", nullable: false),
                    RegularHolidayBps = table.Column<int>(type: "int", nullable: false),
                    RegularHolidayRestDayBps = table.Column<int>(type: "int", nullable: false),
                    PremiumDayOvertimeExtraBps = table.Column<int>(type: "int", nullable: false),
                    NightDifferentialBps = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Client = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContractPriceMinor = table.Column<long>(type: "bigint", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StatusBeforeArchive = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ArchiveReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ArchiveNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BudgetBaselineLockedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BudgetBaselineLockedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BaselineBudgetNetMinor = table.Column<long>(type: "bigint", nullable: true),
                    BaselineBudgetGrossMinor = table.Column<long>(type: "bigint", nullable: true),
                    BaselineContractPriceMinor = table.Column<long>(type: "bigint", nullable: true),
                    BudgetBaselineUnlockReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BudgetBaselineUnlockedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatutorySchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Agency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PeriodBasis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmployeeRateBps = table.Column<int>(type: "int", nullable: true),
                    EmployerRateBps = table.Column<int>(type: "int", nullable: true),
                    BaseFloorMinor = table.Column<long>(type: "bigint", nullable: true),
                    BaseCeilingMinor = table.Column<long>(type: "bigint", nullable: true),
                    EmployeeMinimumMinor = table.Column<long>(type: "bigint", nullable: true),
                    EmployeeMaximumMinor = table.Column<long>(type: "bigint", nullable: true),
                    EmployerMinimumMinor = table.Column<long>(type: "bigint", nullable: true),
                    EmployerMaximumMinor = table.Column<long>(type: "bigint", nullable: true),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RetiredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutorySchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Tin = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Roles = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    FailedSignInCount = table.Column<int>(type: "int", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSignInUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SecurityStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NormalizedEmployeeNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    DateHired = table.Column<DateOnly>(type: "date", nullable: false),
                    MaritalStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SpouseName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Religion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EducationalAttainment = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PresentAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ProvincialAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ContactNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsSssMember = table.Column<bool>(type: "bit", nullable: false),
                    IsPhilHealthMember = table.Column<bool>(type: "bit", nullable: false),
                    IsPagIbigMember = table.Column<bool>(type: "bit", nullable: false),
                    IsTaxExempt = table.Column<bool>(type: "bit", nullable: false),
                    RestDay = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StatusChangedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bonds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BondNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
                    OtherPartyName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReleasedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ReleaseNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CalledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CallNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bonds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bonds_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChangeOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContractPriceDeltaMinor = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BudgetEffectNetMinor = table.Column<long>(type: "bigint", nullable: false),
                    BudgetEffectVatMinor = table.Column<long>(type: "bigint", nullable: false),
                    BudgetEffectGrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidedFromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeOrders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: true),
                    ChainIndex = table.Column<int>(type: "int", nullable: true),
                    PreviousInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NextInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CumulativeWorkMinor = table.Column<long>(type: "bigint", nullable: true),
                    PreviousCumulativeWorkMinor = table.Column<long>(type: "bigint", nullable: false),
                    GrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    NetMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatRateBps = table.Column<int>(type: "int", nullable: false),
                    RetentionBps = table.Column<int>(type: "int", nullable: false),
                    RetentionMinor = table.Column<long>(type: "bigint", nullable: false),
                    WithholdingBps = table.Column<int>(type: "int", nullable: false),
                    WithholdingMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatWithholdingBps = table.Column<int>(type: "int", nullable: false),
                    VatWithholdingMinor = table.Column<long>(type: "bigint", nullable: false),
                    AdvanceBps = table.Column<int>(type: "int", nullable: false),
                    AdvanceRecoveryMinor = table.Column<long>(type: "bigint", nullable: false),
                    NetReceivableMinor = table.Column<long>(type: "bigint", nullable: false),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientInvoices_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectBillingTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetentionBps = table.Column<int>(type: "int", nullable: false),
                    CreditableWithholdingBps = table.Column<int>(type: "int", nullable: false),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    AdvanceBps = table.Column<int>(type: "int", nullable: false),
                    VatWithholdingBps = table.Column<int>(type: "int", nullable: false),
                    IsGovernmentClient = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBillingTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectBillingTerms_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockBalances_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockBalances_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FromKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    FromProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ToProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MovementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ChargedAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReversalOfMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReversedByMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_InventoryItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Projects_FromProjectId",
                        column: x => x.FromProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Projects_ToProjectId",
                        column: x => x.ToProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ArchivedWithProject = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubProjects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryScheduleRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    FromMinor = table.Column<long>(type: "bigint", nullable: false),
                    ToMinor = table.Column<long>(type: "bigint", nullable: true),
                    EmployeeMinor = table.Column<long>(type: "bigint", nullable: false),
                    EmployerMinor = table.Column<long>(type: "bigint", nullable: false),
                    BaseTaxMinor = table.Column<long>(type: "bigint", nullable: false),
                    RateBps = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryScheduleRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatutoryScheduleRows_StatutorySchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "StatutorySchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RegularHours = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NightHours = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RegularPayMinor = table.Column<long>(type: "bigint", nullable: false),
                    PremiumPayMinor = table.Column<long>(type: "bigint", nullable: false),
                    OvertimePayMinor = table.Column<long>(type: "bigint", nullable: false),
                    NightPayMinor = table.Column<long>(type: "bigint", nullable: false),
                    HolidayPayMinor = table.Column<long>(type: "bigint", nullable: false),
                    ThirteenthMonthPayMinor = table.Column<long>(type: "bigint", nullable: false),
                    ThirteenthMonthBasisMinor = table.Column<long>(type: "bigint", nullable: false),
                    ThirteenthMonthYear = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLines_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollLines_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkerCases_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerCases_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RateMinor = table.Column<long>(type: "bigint", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkerRates_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerStatusChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectiveOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkerStatusChanges_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BondEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BondId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PreviousValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    NewValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BondEvents_Bonds_BondId",
                        column: x => x.BondId,
                        principalTable: "Bonds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientPayments_ClientInvoices_ClientInvoiceId",
                        column: x => x.ClientInvoiceId,
                        principalTable: "ClientInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegularHours = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NightHours = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceEntries_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceEntries_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceEntries_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Division = table.Column<int>(type: "int", nullable: true),
                    MainScope = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SubScope = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    WbsCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitCostMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatTreatment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VatRateBps = table.Column<int>(type: "int", nullable: false),
                    NetMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatMinor = table.Column<long>(type: "bigint", nullable: false),
                    GrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RemovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemovedReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ChangeOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetLines_ChangeOrders_ChangeOrderId",
                        column: x => x.ChangeOrderId,
                        principalTable: "ChangeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetLines_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SupplierReference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NetMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatMinor = table.Column<long>(type: "bigint", nullable: false),
                    GrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CloseReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subcontracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContractSumMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatTreatment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VatRateBps = table.Column<int>(type: "int", nullable: false),
                    RetentionBps = table.Column<int>(type: "int", nullable: false),
                    WithholdingBps = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RevisedSumMinor = table.Column<long>(type: "bigint", nullable: false),
                    RevisedNetMinor = table.Column<long>(type: "bigint", nullable: false),
                    RevisedVatMinor = table.Column<long>(type: "bigint", nullable: false),
                    RevisedGrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    WbsCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CertifiedWorkMinor = table.Column<long>(type: "bigint", nullable: false),
                    CertifiedNetMinor = table.Column<long>(type: "bigint", nullable: false),
                    CertifiedGrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    RetentionHeldMinor = table.Column<long>(type: "bigint", nullable: false),
                    RetentionReleasedMinor = table.Column<long>(type: "bigint", nullable: false),
                    WithholdingMinor = table.Column<long>(type: "bigint", nullable: false),
                    NetPayableMinor = table.Column<long>(type: "bigint", nullable: false),
                    PaidOnClaimsMinor = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AwardedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AwardedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcontracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subcontracts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subcontracts_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subcontracts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkerAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerAssignments_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerAssignments_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegularHours = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    BasicPayMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollAllocations_PayrollLines_PayrollLineId",
                        column: x => x.PayrollLineId,
                        principalTable: "PayrollLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollAllocations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollAllocations_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollDeductions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Agency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CalculatedMinor = table.Column<long>(type: "bigint", nullable: true),
                    EmployerShareMinor = table.Column<long>(type: "bigint", nullable: false),
                    IsAdjusted = table.Column<bool>(type: "bit", nullable: false),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollDeductions_PayrollLines_PayrollLineId",
                        column: x => x.PayrollLineId,
                        principalTable: "PayrollLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollDeductions_StatutorySchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "StatutorySchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollHolidayPays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClaimDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollHolidayPays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollHolidayPays_PayrollLines_PayrollLineId",
                        column: x => x.PayrollLineId,
                        principalTable: "PayrollLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollHolidayPays_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollHolidayPays_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollHolidayPays_Workers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "Workers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChangeOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BudgetLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Division = table.Column<int>(type: "int", nullable: true),
                    MainScope = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SubScope = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    WbsCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitCostMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatTreatment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VatRateBps = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NetMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatMinor = table.Column<long>(type: "bigint", nullable: false),
                    GrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    PreviousQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PreviousUnitCostMinor = table.Column<long>(type: "bigint", nullable: true),
                    PreviousNetMinor = table.Column<long>(type: "bigint", nullable: true),
                    PreviousVatMinor = table.Column<long>(type: "bigint", nullable: true),
                    PreviousGrossMinor = table.Column<long>(type: "bigint", nullable: true),
                    EffectNetMinor = table.Column<long>(type: "bigint", nullable: false),
                    EffectVatMinor = table.Column<long>(type: "bigint", nullable: false),
                    EffectGrossMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeOrderLines_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChangeOrderLines_ChangeOrders_ChangeOrderId",
                        column: x => x.ChangeOrderId,
                        principalTable: "ChangeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeOrderLines_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgressEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PercentCompleteBps = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressEntries_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgressEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryReceiptNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    NormalizedDeliveryReceiptNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Division = table.Column<int>(type: "int", nullable: true),
                    MainScope = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SubScope = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    WbsCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitCostMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatTreatment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VatRateBps = table.Column<int>(type: "int", nullable: false),
                    NetMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatMinor = table.Column<long>(type: "bigint", nullable: false),
                    GrossMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractVariations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubcontractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    VariationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractVariations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractVariations_Subcontracts_SubcontractId",
                        column: x => x.SubcontractId,
                        principalTable: "Subcontracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Division = table.Column<int>(type: "int", nullable: true),
                    MainScope = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SubScope = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    WbsCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitCostMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatTreatment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VatRateBps = table.Column<int>(type: "int", nullable: false),
                    NetMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatMinor = table.Column<long>(type: "bigint", nullable: false),
                    GrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDealer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedSourceDealer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    NormalizedInvoiceNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostEntries_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostEntries_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubcontractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimNumber = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClaimDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    CumulativeWorkMinor = table.Column<long>(type: "bigint", nullable: false),
                    PreviousCumulativeMinor = table.Column<long>(type: "bigint", nullable: false),
                    ThisClaimBaseMinor = table.Column<long>(type: "bigint", nullable: false),
                    NetMinor = table.Column<long>(type: "bigint", nullable: false),
                    VatMinor = table.Column<long>(type: "bigint", nullable: false),
                    GrossMinor = table.Column<long>(type: "bigint", nullable: false),
                    RetentionMinor = table.Column<long>(type: "bigint", nullable: false),
                    WithholdingMinor = table.Column<long>(type: "bigint", nullable: false),
                    NetPayableMinor = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CostEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CertifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CertifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractClaims_CostEntries_CostEntryId",
                        column: x => x.CostEntryId,
                        principalTable: "CostEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractClaims_Subcontracts_SubcontractId",
                        column: x => x.SubcontractId,
                        principalTable: "Subcontracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubcontractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VoidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientRequestId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubcontractPayments_SubcontractClaims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "SubcontractClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubcontractPayments_Subcontracts_SubcontractId",
                        column: x => x.SubcontractId,
                        principalTable: "Subcontracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_PayrollRunId",
                table: "AttendanceEntries",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_ProjectId",
                table: "AttendanceEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_SubProjectId",
                table: "AttendanceEntries",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_TenantId_ClientRequestId",
                table: "AttendanceEntries",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_TenantId_PayrollRunId",
                table: "AttendanceEntries",
                columns: new[] { "TenantId", "PayrollRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_TenantId_ProjectId_WorkDate",
                table: "AttendanceEntries",
                columns: new[] { "TenantId", "ProjectId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_TenantId_Status_WorkDate",
                table: "AttendanceEntries",
                columns: new[] { "TenantId", "Status", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_TenantId_WorkerId_ProjectId_SubProjectId_WorkDate",
                table: "AttendanceEntries",
                columns: new[] { "TenantId", "WorkerId", "ProjectId", "SubProjectId", "WorkDate" },
                unique: true,
                filter: "[Status] <> 'Voided'");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_TenantId_WorkerId_WorkDate",
                table: "AttendanceEntries",
                columns: new[] { "TenantId", "WorkerId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEntries_WorkerId",
                table: "AttendanceEntries",
                column: "WorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TenantId_EntityType_EntityId",
                table: "AuditEntries",
                columns: new[] { "TenantId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TenantId_OccurredAtUtc",
                table: "AuditEntries",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BondEvents_BondId_AtUtc",
                table: "BondEvents",
                columns: new[] { "BondId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BondEvents_TenantId",
                table: "BondEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Bonds_ProjectId",
                table: "Bonds",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Bonds_TenantId_ClientRequestId",
                table: "Bonds",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bonds_TenantId_ProjectId_Status",
                table: "Bonds",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bonds_TenantId_Status_ValidUntil",
                table: "Bonds",
                columns: new[] { "TenantId", "Status", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_ChangeOrderId",
                table: "BudgetLines",
                column: "ChangeOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_ProjectId_Division_WbsCode",
                table: "BudgetLines",
                columns: new[] { "ProjectId", "Division", "WbsCode" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_SubProjectId",
                table: "BudgetLines",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_TenantId_ClientRequestId",
                table: "BudgetLines",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_TenantId_ProjectId_Status",
                table: "BudgetLines",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrderLines_BudgetLineId",
                table: "ChangeOrderLines",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrderLines_ChangeOrderId_LineNumber",
                table: "ChangeOrderLines",
                columns: new[] { "ChangeOrderId", "LineNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrderLines_SubProjectId",
                table: "ChangeOrderLines",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrderLines_TenantId",
                table: "ChangeOrderLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_ProjectId",
                table: "ChangeOrders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_TenantId_ClientRequestId",
                table: "ChangeOrders",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_TenantId_ProjectId_CreatedAtUtc",
                table: "ChangeOrders",
                columns: new[] { "TenantId", "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_TenantId_ProjectId_Number",
                table: "ChangeOrders",
                columns: new[] { "TenantId", "ProjectId", "Number" },
                unique: true,
                filter: "[Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_TenantId_ProjectId_Status_DecidedAtUtc",
                table: "ChangeOrders",
                columns: new[] { "TenantId", "ProjectId", "Status", "DecidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientInvoices_OneLiveAdvancePerProject",
                table: "ClientInvoices",
                columns: new[] { "TenantId", "ProjectId" },
                unique: true,
                filter: "[Kind] = 'Advance' AND [Status] <> 'Void'");

            migrationBuilder.CreateIndex(
                name: "IX_ClientInvoices_ProjectId",
                table: "ClientInvoices",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientInvoices_TenantId_BillingDate",
                table: "ClientInvoices",
                columns: new[] { "TenantId", "BillingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientInvoices_TenantId_ClientRequestId",
                table: "ClientInvoices",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientInvoices_TenantId_Number",
                table: "ClientInvoices",
                columns: new[] { "TenantId", "Number" },
                unique: true,
                filter: "[Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientInvoices_TenantId_ProjectId_ChainIndex",
                table: "ClientInvoices",
                columns: new[] { "TenantId", "ProjectId", "ChainIndex" },
                unique: true,
                filter: "[Status] = 'Issued'");

            migrationBuilder.CreateIndex(
                name: "IX_ClientInvoices_TenantId_ProjectId_Status",
                table: "ClientInvoices",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientInvoices_TenantId_Status_DueDate",
                table: "ClientInvoices",
                columns: new[] { "TenantId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_ClientInvoiceId_IsVoided",
                table: "ClientPayments",
                columns: new[] { "ClientInvoiceId", "IsVoided" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_TenantId",
                table: "ClientPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientPayments_TenantId_ClientRequestId",
                table: "ClientPayments",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyHolidays_TenantId_Date",
                table: "CompanyHolidays",
                columns: new[] { "TenantId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_ProjectId_Division_WbsCode",
                table: "CostEntries",
                columns: new[] { "ProjectId", "Division", "WbsCode" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_PurchaseOrderLineId",
                table: "CostEntries",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_SubProjectId",
                table: "CostEntries",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_ClientRequestId",
                table: "CostEntries",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_Origin_OriginReferenceId",
                table: "CostEntries",
                columns: new[] { "TenantId", "Origin", "OriginReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_ProjectId_EntryDate",
                table: "CostEntries",
                columns: new[] { "TenantId", "ProjectId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_ProjectId_NormalizedSourceDealer_NormalizedInvoiceNumber",
                table: "CostEntries",
                columns: new[] { "TenantId", "ProjectId", "NormalizedSourceDealer", "NormalizedInvoiceNumber" },
                unique: true,
                filter: "[Status] = 'Posted' AND [Origin] = 'Manual'");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_ProjectId_Status",
                table: "CostEntries",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_PurchaseOrderLineId",
                table: "CostEntries",
                columns: new[] { "TenantId", "PurchaseOrderLineId" },
                filter: "[PurchaseOrderLineId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "Status", "NetMinor", "GrossMinor" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_GoodsReceiptId",
                table: "GoodsReceiptLines",
                column: "GoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_PurchaseOrderLineId",
                table: "GoodsReceiptLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_TenantId_PurchaseOrderLineId",
                table: "GoodsReceiptLines",
                columns: new[] { "TenantId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_OnePostedNumberPerSupplier",
                table: "GoodsReceipts",
                columns: new[] { "TenantId", "SupplierId", "NormalizedDeliveryReceiptNumber" },
                unique: true,
                filter: "[Status] = 'Posted' AND [NormalizedDeliveryReceiptNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_ProjectId",
                table: "GoodsReceipts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_PurchaseOrderId",
                table: "GoodsReceipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_SupplierId",
                table: "GoodsReceipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_TenantId_ClientRequestId",
                table: "GoodsReceipts",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_TenantId_PurchaseOrderId_Status",
                table: "GoodsReceipts",
                columns: new[] { "TenantId", "PurchaseOrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId_Description",
                table: "InventoryItems",
                columns: new[] { "TenantId", "Description" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId_NormalizedSerialNumber",
                table: "InventoryItems",
                columns: new[] { "TenantId", "NormalizedSerialNumber" },
                unique: true,
                filter: "[NormalizedSerialNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId_Status",
                table: "InventoryItems",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAllocations_PayrollLineId",
                table: "PayrollAllocations",
                column: "PayrollLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAllocations_ProjectId",
                table: "PayrollAllocations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAllocations_SubProjectId",
                table: "PayrollAllocations",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAllocations_TenantId_ProjectId",
                table: "PayrollAllocations",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_PayrollLineId",
                table: "PayrollDeductions",
                column: "PayrollLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_ScheduleId",
                table: "PayrollDeductions",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_TenantId",
                table: "PayrollDeductions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductions_TenantId_ScheduleId",
                table: "PayrollDeductions",
                columns: new[] { "TenantId", "ScheduleId" },
                filter: "[ScheduleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHolidayPays_PayrollLineId",
                table: "PayrollHolidayPays",
                column: "PayrollLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHolidayPays_ProjectId",
                table: "PayrollHolidayPays",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHolidayPays_SubProjectId",
                table: "PayrollHolidayPays",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHolidayPays_TenantId_ClaimDate",
                table: "PayrollHolidayPays",
                columns: new[] { "TenantId", "ClaimDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHolidayPays_TenantId_WorkerId_ClaimDate",
                table: "PayrollHolidayPays",
                columns: new[] { "TenantId", "WorkerId", "ClaimDate" },
                unique: true,
                filter: "[ClaimDate] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollHolidayPays_WorkerId",
                table: "PayrollHolidayPays",
                column: "WorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_PayrollRunId_WorkerId",
                table: "PayrollLines",
                columns: new[] { "PayrollRunId", "WorkerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_TenantId_WorkerId",
                table: "PayrollLines",
                columns: new[] { "TenantId", "WorkerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_TenantId_WorkerId_ThirteenthMonthYear",
                table: "PayrollLines",
                columns: new[] { "TenantId", "WorkerId", "ThirteenthMonthYear" },
                unique: true,
                filter: "[ThirteenthMonthYear] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_WorkerId",
                table: "PayrollLines",
                column: "WorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_ClientRequestId",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_CreatedAtUtc",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_Kind_Status_PeriodEnd",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "Kind", "Status", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_TenantId_Status",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayRules_TenantId",
                table: "PayRules",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEntries_BudgetLineId",
                table: "ProgressEntries",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEntries_ProjectId",
                table: "ProgressEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEntries_TenantId_BudgetLineId_AsOfDate_RecordedAtUtc",
                table: "ProgressEntries",
                columns: new[] { "TenantId", "BudgetLineId", "AsOfDate", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEntries_TenantId_ClientRequestId_BudgetLineId",
                table: "ProgressEntries",
                columns: new[] { "TenantId", "ClientRequestId", "BudgetLineId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressEntries_TenantId_ProjectId",
                table: "ProgressEntries",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBillingTerms_ProjectId",
                table: "ProjectBillingTerms",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBillingTerms_TenantId_ProjectId",
                table: "ProjectBillingTerms",
                columns: new[] { "TenantId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_NormalizedName",
                table: "Projects",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_Status",
                table: "Projects",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId_LineNumber",
                table: "PurchaseOrderLines",
                columns: new[] { "PurchaseOrderId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_TenantId_PurchaseOrderId",
                table: "PurchaseOrderLines",
                columns: new[] { "TenantId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ProjectId",
                table: "PurchaseOrders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SubProjectId",
                table: "PurchaseOrders",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_ClientRequestId",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_Number",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_ProjectId_Status",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_Status_OrderDate",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "Status", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_SupplierId",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryScheduleRows_ScheduleId_RowNumber",
                table: "StatutoryScheduleRows",
                columns: new[] { "ScheduleId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryScheduleRows_TenantId",
                table: "StatutoryScheduleRows",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StatutorySchedules_TenantId_Agency_EffectiveFrom",
                table: "StatutorySchedules",
                columns: new[] { "TenantId", "Agency", "EffectiveFrom" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_StatutorySchedules_TenantId_ClientRequestId",
                table: "StatutorySchedules",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StatutorySchedules_TenantId_Status_Agency_EffectiveFrom",
                table: "StatutorySchedules",
                columns: new[] { "TenantId", "Status", "Agency", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_ItemId_LocationKey",
                table: "StockBalances",
                columns: new[] { "ItemId", "LocationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_ProjectId",
                table: "StockBalances",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StockBalances_TenantId_ProjectId",
                table: "StockBalances",
                columns: new[] { "TenantId", "ProjectId" },
                filter: "[ProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_FromProjectId",
                table: "StockMovements",
                column: "FromProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId_RecordedAtUtc",
                table: "StockMovements",
                columns: new[] { "ItemId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId_ClientRequestId",
                table: "StockMovements",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ToProjectId",
                table: "StockMovements",
                column: "ToProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractClaims_CostEntryId",
                table: "SubcontractClaims",
                column: "CostEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractClaims_OneDraft",
                table: "SubcontractClaims",
                column: "SubcontractId",
                unique: true,
                filter: "[Status] = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractClaims_SubcontractId_ClaimNumber",
                table: "SubcontractClaims",
                columns: new[] { "SubcontractId", "ClaimNumber" },
                unique: true,
                filter: "[ClaimNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractClaims_TenantId",
                table: "SubcontractClaims",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractClaims_TenantId_ClientRequestId",
                table: "SubcontractClaims",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractPayments_ClaimId",
                table: "SubcontractPayments",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractPayments_SubcontractId",
                table: "SubcontractPayments",
                column: "SubcontractId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractPayments_TenantId",
                table: "SubcontractPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractPayments_TenantId_ClientRequestId",
                table: "SubcontractPayments",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subcontracts_ProjectId",
                table: "Subcontracts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Subcontracts_SubProjectId",
                table: "Subcontracts",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Subcontracts_SupplierId",
                table: "Subcontracts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Subcontracts_TenantId_ClientRequestId",
                table: "Subcontracts",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subcontracts_TenantId_Number",
                table: "Subcontracts",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subcontracts_TenantId_ProjectId_Status",
                table: "Subcontracts",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Subcontracts_TenantId_Status_CreatedAtUtc",
                table: "Subcontracts",
                columns: new[] { "TenantId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Subcontracts_TenantId_SupplierId",
                table: "Subcontracts",
                columns: new[] { "TenantId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractVariations_SubcontractId_Number",
                table: "SubcontractVariations",
                columns: new[] { "SubcontractId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractVariations_TenantId",
                table: "SubcontractVariations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_ProjectId_NormalizedName",
                table: "SubProjects",
                columns: new[] { "ProjectId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_TenantId",
                table: "SubProjects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_IsArchived_Kind",
                table: "Suppliers",
                columns: new[] { "TenantId", "IsArchived", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_NormalizedName",
                table: "Suppliers",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAssignments_ProjectId",
                table: "WorkerAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAssignments_SubProjectId",
                table: "WorkerAssignments",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAssignments_TenantId_ProjectId_EndDate",
                table: "WorkerAssignments",
                columns: new[] { "TenantId", "ProjectId", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAssignments_WorkerId",
                table: "WorkerAssignments",
                column: "WorkerId",
                unique: true,
                filter: "[EndDate] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAssignments_WorkerId_StartDate",
                table: "WorkerAssignments",
                columns: new[] { "WorkerId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCases_ProjectId",
                table: "WorkerCases",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCases_TenantId",
                table: "WorkerCases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCases_WorkerId_CaseDate",
                table: "WorkerCases",
                columns: new[] { "WorkerId", "CaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerRates_TenantId",
                table: "WorkerRates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerRates_WorkerId_EffectiveFrom",
                table: "WorkerRates",
                columns: new[] { "WorkerId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workers_TenantId_ClientRequestId",
                table: "Workers",
                columns: new[] { "TenantId", "ClientRequestId" },
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Workers_TenantId_LastName_FirstName",
                table: "Workers",
                columns: new[] { "TenantId", "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Workers_TenantId_NormalizedEmployeeNumber",
                table: "Workers",
                columns: new[] { "TenantId", "NormalizedEmployeeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workers_TenantId_Status",
                table: "Workers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerStatusChanges_TenantId",
                table: "WorkerStatusChanges",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerStatusChanges_WorkerId_EffectiveOn",
                table: "WorkerStatusChanges",
                columns: new[] { "WorkerId", "EffectiveOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceEntries");

            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "BondEvents");

            migrationBuilder.DropTable(
                name: "ChangeOrderLines");

            migrationBuilder.DropTable(
                name: "ClientPayments");

            migrationBuilder.DropTable(
                name: "CompanyHolidays");

            migrationBuilder.DropTable(
                name: "GoodsReceiptLines");

            migrationBuilder.DropTable(
                name: "NumberSequences");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "PayrollAllocations");

            migrationBuilder.DropTable(
                name: "PayrollDeductions");

            migrationBuilder.DropTable(
                name: "PayrollHolidayPays");

            migrationBuilder.DropTable(
                name: "PayRules");

            migrationBuilder.DropTable(
                name: "ProgressEntries");

            migrationBuilder.DropTable(
                name: "ProjectBillingTerms");

            migrationBuilder.DropTable(
                name: "StatutoryScheduleRows");

            migrationBuilder.DropTable(
                name: "StockBalances");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "SubcontractPayments");

            migrationBuilder.DropTable(
                name: "SubcontractVariations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkerAssignments");

            migrationBuilder.DropTable(
                name: "WorkerCases");

            migrationBuilder.DropTable(
                name: "WorkerRates");

            migrationBuilder.DropTable(
                name: "WorkerStatusChanges");

            migrationBuilder.DropTable(
                name: "Bonds");

            migrationBuilder.DropTable(
                name: "ClientInvoices");

            migrationBuilder.DropTable(
                name: "GoodsReceipts");

            migrationBuilder.DropTable(
                name: "PayrollLines");

            migrationBuilder.DropTable(
                name: "BudgetLines");

            migrationBuilder.DropTable(
                name: "StatutorySchedules");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "SubcontractClaims");

            migrationBuilder.DropTable(
                name: "PayrollRuns");

            migrationBuilder.DropTable(
                name: "Workers");

            migrationBuilder.DropTable(
                name: "ChangeOrders");

            migrationBuilder.DropTable(
                name: "CostEntries");

            migrationBuilder.DropTable(
                name: "Subcontracts");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "SubProjects");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
