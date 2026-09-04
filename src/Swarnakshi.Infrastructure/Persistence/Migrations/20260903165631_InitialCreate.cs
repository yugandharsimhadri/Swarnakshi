using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ContactMobile = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LicenseExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contractors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Pan = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Gstin = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BankDetails = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ContractorType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contractors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Pan = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Gstin = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseHeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseHeads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabourCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabourCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RefreshTokenExpiry = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Pan = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Gstin = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCompanyAdmin = table.Column<bool>(type: "bit", nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RefreshTokenExpiry = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TokensValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalHistories_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalTable: "ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseSubheads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseHeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseSubheads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseSubheads_ExpenseHeads_ExpenseHeadId",
                        column: x => x.ExpenseHeadId,
                        principalTable: "ExpenseHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialSubcategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialSubcategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialSubcategories_MaterialCategories_MaterialCategoryId",
                        column: x => x.MaterialCategoryId,
                        principalTable: "MaterialCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    City = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    State = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Pin = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SupervisorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sites_Users_SupervisorUserId",
                        column: x => x.SupervisorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Granted = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaterialSpecDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialSubcategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Options = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    PartOfIdentity = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialSpecDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialSpecDefinitions_MaterialSubcategories_MaterialSubcategoryId",
                        column: x => x.MaterialSubcategoryId,
                        principalTable: "MaterialSubcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    MaterialSubcategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecondaryUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GenericMeasurement = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    MinStockLevel = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultPurchaseRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GstRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SpecSummary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SpecSignature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materials_MaterialSubcategories_MaterialSubcategoryId",
                        column: x => x.MaterialSubcategoryId,
                        principalTable: "MaterialSubcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Materials_Units_SecondaryUnitId",
                        column: x => x.SecondaryUnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Materials_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JoinDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LeaveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    VillaNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedCompletionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualCompletionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContractSaleValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletionPercent = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_ProjectTypes_ProjectTypeId",
                        column: x => x.ProjectTypeId,
                        principalTable: "ProjectTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Projects_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settings_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TxnNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpenseHeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteExpenses_ExpenseHeads_ExpenseHeadId",
                        column: x => x.ExpenseHeadId,
                        principalTable: "ExpenseHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteExpenses_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteExpenses_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSiteAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSiteAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSiteAssignments_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSiteAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AverageRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LastMovementAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastPurchaseRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialSpecValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialSpecDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialSpecValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialSpecValues_MaterialSpecDefinitions_MaterialSpecDefinitionId",
                        column: x => x.MaterialSpecDefinitionId,
                        principalTable: "MaterialSpecDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialSpecValues_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractWorks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkCategory = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContractAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedCompletion = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualCompletion = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    WorkStatus = table.Column<int>(type: "int", nullable: false),
                    TotalPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractWorks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractWorks_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractWorks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TxnNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TxnNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceRecovered = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePayments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayments_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePayments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TxnNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceRef = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabourEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TxnNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabourCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabourEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabourEntries_LabourCategories_LabourCategoryId",
                        column: x => x.LabourCategoryId,
                        principalTable: "LabourCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabourEntries_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabourEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TxnNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    RequestStatus = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialRequests_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TxnNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpenseHeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseSubheadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpenseType = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_ExpenseHeads_ExpenseHeadId",
                        column: x => x.ExpenseHeadId,
                        principalTable: "ExpenseHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_ExpenseSubheads_ExpenseSubheadId",
                        column: x => x.ExpenseSubheadId,
                        principalTable: "ExpenseSubheads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectExpenses_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractorPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TxnNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContractorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractWorkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PaymentKind = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorPayments_ContractWorks_ContractWorkId",
                        column: x => x.ContractWorkId,
                        principalTable: "ContractWorks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorPayments_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorPayments_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorPayments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedQty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedQty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IssuedQty = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpenseHeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpenseSubheadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialRequestItems_ExpenseHeads_ExpenseHeadId",
                        column: x => x.ExpenseHeadId,
                        principalTable: "ExpenseHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialRequestItems_ExpenseSubheads_ExpenseSubheadId",
                        column: x => x.ExpenseSubheadId,
                        principalTable: "ExpenseSubheads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialRequestItems_MaterialRequests_MaterialRequestId",
                        column: x => x.MaterialRequestId,
                        principalTable: "MaterialRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialRequestItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialRequestItems_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseHeaders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TxnNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaterialRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseHeaders_MaterialRequests_MaterialRequestId",
                        column: x => x.MaterialRequestId,
                        principalTable: "MaterialRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseHeaders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseHeaders_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseHeaders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseHeaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeliverToProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpenseHeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_ExpenseHeads_ExpenseHeadId",
                        column: x => x.ExpenseHeadId,
                        principalTable: "ExpenseHeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Projects_DeliverToProjectId",
                        column: x => x.DeliverToProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_PurchaseHeaders_PurchaseHeaderId",
                        column: x => x.PurchaseHeaderId,
                        principalTable: "PurchaseHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseHeaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPayments_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierPayments_PurchaseHeaders_PurchaseHeaderId",
                        column: x => x.PurchaseHeaderId,
                        principalTable: "PurchaseHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalHistories_ApprovalRequestId",
                table: "ApprovalHistories",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalHistories_CompanyId",
                table: "ApprovalHistories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_CompanyId",
                table: "ApprovalRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_CurrentStatus",
                table: "ApprovalRequests",
                column: "CurrentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_EntityType_EntityId",
                table: "ApprovalRequests",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_CompanyId",
                table: "Attachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_EntityType_EntityId",
                table: "Attachments",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CompanyId",
                table: "AuditLogs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Code",
                table: "Companies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                table: "Companies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorks_CompanyId",
                table: "ContractWorks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorks_ContractorId",
                table: "ContractWorks",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorks_ProjectId",
                table: "ContractWorks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_CompanyId",
                table: "ContractorPayments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_CompanyId_TxnNumber",
                table: "ContractorPayments",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_ContractWorkId",
                table: "ContractorPayments",
                column: "ContractWorkId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_ContractorId",
                table: "ContractorPayments",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_PaymentMethodId",
                table: "ContractorPayments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_ProjectId",
                table: "ContractorPayments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_CompanyId",
                table: "Contractors",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_CompanyId_Code",
                table: "Contractors",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CompanyId",
                table: "CustomerPayments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CompanyId_TxnNumber",
                table: "CustomerPayments",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CustomerId",
                table: "CustomerPayments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_PaymentMethodId",
                table: "CustomerPayments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_ProjectId",
                table: "CustomerPayments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId",
                table: "Customers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CompanyId_Code",
                table: "Customers",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayments_CompanyId",
                table: "EmployeePayments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayments_CompanyId_TxnNumber",
                table: "EmployeePayments",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayments_EmployeeId_Date",
                table: "EmployeePayments",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayments_PaymentMethodId",
                table: "EmployeePayments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayments_ProjectId",
                table: "EmployeePayments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId",
                table: "Employees",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_Code",
                table: "Employees",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_Phone",
                table: "Employees",
                columns: new[] { "CompanyId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_SiteId",
                table: "Employees",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseHeads_CompanyId",
                table: "ExpenseHeads",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSubheads_CompanyId",
                table: "ExpenseSubheads",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSubheads_CompanyId_ExpenseHeadId_Name",
                table: "ExpenseSubheads",
                columns: new[] { "CompanyId", "ExpenseHeadId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSubheads_ExpenseHeadId",
                table: "ExpenseSubheads",
                column: "ExpenseHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_CompanyId",
                table: "InventoryBalances",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_CompanyId_SiteId_MaterialId",
                table: "InventoryBalances",
                columns: new[] { "CompanyId", "SiteId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_MaterialId",
                table: "InventoryBalances",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_SiteId",
                table: "InventoryBalances",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_CompanyId",
                table: "InventoryTransactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_CompanyId_TxnNumber",
                table: "InventoryTransactions",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_MaterialId",
                table: "InventoryTransactions",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProjectId",
                table: "InventoryTransactions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_SiteId_MaterialId_Date",
                table: "InventoryTransactions",
                columns: new[] { "SiteId", "MaterialId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_UnitId",
                table: "InventoryTransactions",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_LabourCategories_CompanyId",
                table: "LabourCategories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_LabourEntries_CompanyId",
                table: "LabourEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_LabourEntries_CompanyId_TxnNumber",
                table: "LabourEntries",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabourEntries_LabourCategoryId",
                table: "LabourEntries",
                column: "LabourCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LabourEntries_PaymentMethodId",
                table: "LabourEntries",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_LabourEntries_ProjectId",
                table: "LabourEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialCategories_CompanyId",
                table: "MaterialCategories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestItems_CompanyId",
                table: "MaterialRequestItems",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestItems_ExpenseHeadId",
                table: "MaterialRequestItems",
                column: "ExpenseHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestItems_ExpenseSubheadId",
                table: "MaterialRequestItems",
                column: "ExpenseSubheadId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestItems_MaterialId",
                table: "MaterialRequestItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestItems_MaterialRequestId",
                table: "MaterialRequestItems",
                column: "MaterialRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestItems_UnitId",
                table: "MaterialRequestItems",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_CompanyId",
                table: "MaterialRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_CompanyId_TxnNumber",
                table: "MaterialRequests",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_ProjectId",
                table: "MaterialRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_SiteId",
                table: "MaterialRequests",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecDefinitions_CompanyId",
                table: "MaterialSpecDefinitions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecDefinitions_CompanyId_MaterialSubcategoryId_Key",
                table: "MaterialSpecDefinitions",
                columns: new[] { "CompanyId", "MaterialSubcategoryId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecDefinitions_MaterialSubcategoryId",
                table: "MaterialSpecDefinitions",
                column: "MaterialSubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecValues_CompanyId",
                table: "MaterialSpecValues",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecValues_CompanyId_MaterialId_MaterialSpecDefinitionId",
                table: "MaterialSpecValues",
                columns: new[] { "CompanyId", "MaterialId", "MaterialSpecDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecValues_MaterialId",
                table: "MaterialSpecValues",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecValues_MaterialSpecDefinitionId",
                table: "MaterialSpecValues",
                column: "MaterialSpecDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecValues_Value",
                table: "MaterialSpecValues",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSubcategories_CompanyId",
                table: "MaterialSubcategories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSubcategories_CompanyId_MaterialCategoryId_Name",
                table: "MaterialSubcategories",
                columns: new[] { "CompanyId", "MaterialCategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSubcategories_MaterialCategoryId",
                table: "MaterialSubcategories",
                column: "MaterialCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Brand",
                table: "Materials",
                column: "Brand");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_CompanyId",
                table: "Materials",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_CompanyId_Code",
                table: "Materials",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_CompanyId_SpecSignature",
                table: "Materials",
                columns: new[] { "CompanyId", "SpecSignature" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_IsActive",
                table: "Materials",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_MaterialSubcategoryId",
                table: "Materials",
                column: "MaterialSubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_SecondaryUnitId",
                table: "Materials",
                column: "SecondaryUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_UnitId",
                table: "Materials",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_CompanyId",
                table: "PaymentMethods",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_Username",
                table: "PlatformUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_CompanyId",
                table: "ProjectExpenses",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_CompanyId_TxnNumber",
                table: "ProjectExpenses",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_ExpenseHeadId",
                table: "ProjectExpenses",
                column: "ExpenseHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_ExpenseSubheadId",
                table: "ProjectExpenses",
                column: "ExpenseSubheadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_PaymentMethodId",
                table: "ProjectExpenses",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_ProjectId_Date",
                table: "ProjectExpenses",
                columns: new[] { "ProjectId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTypes_CompanyId",
                table: "ProjectTypes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CompanyId",
                table: "Projects",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CompanyId_Code",
                table: "Projects",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CustomerId",
                table: "Projects",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectTypeId",
                table: "Projects",
                column: "ProjectTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_SiteId",
                table: "Projects",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHeaders_CompanyId",
                table: "PurchaseHeaders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHeaders_CompanyId_TxnNumber",
                table: "PurchaseHeaders",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHeaders_MaterialRequestId",
                table: "PurchaseHeaders",
                column: "MaterialRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHeaders_ProjectId",
                table: "PurchaseHeaders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHeaders_SiteId",
                table: "PurchaseHeaders",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHeaders_SupplierId",
                table: "PurchaseHeaders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_CompanyId",
                table: "PurchaseItems",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_DeliverToProjectId",
                table: "PurchaseItems",
                column: "DeliverToProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_ExpenseHeadId",
                table: "PurchaseItems",
                column: "ExpenseHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_MaterialId",
                table: "PurchaseItems",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_PurchaseHeaderId",
                table: "PurchaseItems",
                column: "PurchaseHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_UnitId",
                table: "PurchaseItems",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_CompanyId",
                table: "Settings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_CompanyId_Key_SiteId",
                table: "Settings",
                columns: new[] { "CompanyId", "Key", "SiteId" },
                unique: true,
                filter: "[SiteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_SiteId",
                table: "Settings",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteExpenses_CompanyId",
                table: "SiteExpenses",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteExpenses_CompanyId_TxnNumber",
                table: "SiteExpenses",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteExpenses_ExpenseHeadId",
                table: "SiteExpenses",
                column: "ExpenseHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteExpenses_PaymentMethodId",
                table: "SiteExpenses",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteExpenses_SiteId_Date",
                table: "SiteExpenses",
                columns: new[] { "SiteId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Sites_CompanyId",
                table: "Sites",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_CompanyId_Code",
                table: "Sites",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sites_SupervisorUserId",
                table: "Sites",
                column: "SupervisorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_CompanyId",
                table: "SupplierPayments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_PaymentMethodId",
                table: "SupplierPayments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_PurchaseHeaderId",
                table: "SupplierPayments",
                column: "PurchaseHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CompanyId",
                table: "Suppliers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CompanyId_Code",
                table: "Suppliers",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSequences_CompanyId",
                table: "TransactionSequences",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSequences_CompanyId_Prefix_Year",
                table: "TransactionSequences",
                columns: new[] { "CompanyId", "Prefix", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_CompanyId",
                table: "Units",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_CompanyId_Code",
                table: "Units",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_CompanyId",
                table: "UserPermissions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId",
                table: "UserPermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSiteAssignments_CompanyId",
                table: "UserSiteAssignments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSiteAssignments_SiteId",
                table: "UserSiteAssignments",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSiteAssignments_UserId",
                table: "UserSiteAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId",
                table: "Users",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId_Username",
                table: "Users",
                columns: new[] { "CompanyId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Mobile",
                table: "Users",
                column: "Mobile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalHistories");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "ContractorPayments");

            migrationBuilder.DropTable(
                name: "CustomerPayments");

            migrationBuilder.DropTable(
                name: "EmployeePayments");

            migrationBuilder.DropTable(
                name: "InventoryBalances");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "LabourEntries");

            migrationBuilder.DropTable(
                name: "MaterialRequestItems");

            migrationBuilder.DropTable(
                name: "MaterialSpecValues");

            migrationBuilder.DropTable(
                name: "PlatformUsers");

            migrationBuilder.DropTable(
                name: "ProjectExpenses");

            migrationBuilder.DropTable(
                name: "PurchaseItems");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "SiteExpenses");

            migrationBuilder.DropTable(
                name: "SupplierPayments");

            migrationBuilder.DropTable(
                name: "TransactionSequences");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "UserSiteAssignments");

            migrationBuilder.DropTable(
                name: "ApprovalRequests");

            migrationBuilder.DropTable(
                name: "ContractWorks");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "LabourCategories");

            migrationBuilder.DropTable(
                name: "MaterialSpecDefinitions");

            migrationBuilder.DropTable(
                name: "ExpenseSubheads");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "PurchaseHeaders");

            migrationBuilder.DropTable(
                name: "Contractors");

            migrationBuilder.DropTable(
                name: "ExpenseHeads");

            migrationBuilder.DropTable(
                name: "MaterialSubcategories");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "MaterialRequests");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "MaterialCategories");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "ProjectTypes");

            migrationBuilder.DropTable(
                name: "Sites");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
