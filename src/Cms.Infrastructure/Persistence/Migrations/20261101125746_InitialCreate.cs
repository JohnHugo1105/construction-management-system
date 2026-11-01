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
                    HoursPerDay = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    OvertimeMultiplierBps = table.Column<int>(type: "int", nullable: false),
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
                    RegularPayMinor = table.Column<long>(type: "bigint", nullable: false),
                    OvertimePayMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLines_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLines", x => x.Id);
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
                        name: "FK_CostEntries_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
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
                    GrossMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollAllocations_PayrollLines_PayrollLineId",
                        column: x => x.PayrollLineId,
                        principalTable: "PayrollLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false)
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
                name: "IX_CostEntries_ProjectId_Division_WbsCode",
                table: "CostEntries",
                columns: new[] { "ProjectId", "Division", "WbsCode" });

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
                name: "IX_PayrollDeductions_TenantId",
                table: "PayrollDeductions",
                column: "TenantId");

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
                name: "IX_PayrollRuns_TenantId_Status",
                table: "PayrollRuns",
                columns: new[] { "TenantId", "Status" });

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
                name: "IX_SubProjects_ProjectId_NormalizedName",
                table: "SubProjects",
                columns: new[] { "ProjectId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_TenantId",
                table: "SubProjects",
                column: "TenantId");

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
                name: "BudgetLines");

            migrationBuilder.DropTable(
                name: "CostEntries");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "PayrollAllocations");

            migrationBuilder.DropTable(
                name: "PayrollDeductions");

            migrationBuilder.DropTable(
                name: "StockBalances");

            migrationBuilder.DropTable(
                name: "StockMovements");

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
                name: "PayrollLines");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "SubProjects");

            migrationBuilder.DropTable(
                name: "PayrollRuns");

            migrationBuilder.DropTable(
                name: "Workers");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
