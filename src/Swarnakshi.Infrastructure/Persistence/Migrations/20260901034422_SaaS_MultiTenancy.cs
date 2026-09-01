using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaaS_MultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Units_Code",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_TransactionSequences_Prefix_Year",
                table: "TransactionSequences");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Sites_Code",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Settings_Key_SiteId",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseHeaders_TxnNumber",
                table: "PurchaseHeaders");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Code",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectExpenses_TxnNumber",
                table: "ProjectExpenses");

            migrationBuilder.DropIndex(
                name: "IX_Materials_Code",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_SpecSignature",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSubcategories_MaterialCategoryId_Name",
                table: "MaterialSubcategories");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSpecValues_MaterialId_MaterialSpecDefinitionId",
                table: "MaterialSpecValues");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSpecDefinitions_MaterialSubcategoryId_Key",
                table: "MaterialSpecDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequests_TxnNumber",
                table: "MaterialRequests");

            migrationBuilder.DropIndex(
                name: "IX_LabourEntries_TxnNumber",
                table: "LabourEntries");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_TxnNumber",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBalances_SiteId_MaterialId",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseSubheads_ExpenseHeadId_Name",
                table: "ExpenseSubheads");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Code",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_TxnNumber",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_Contractors_Code",
                table: "Contractors");

            migrationBuilder.DropIndex(
                name: "IX_ContractorPayments_TxnNumber",
                table: "ContractorPayments");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsCompanyAdmin",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "TEXT",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "UserSiteAssignments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "UserPermissions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Units",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "TransactionSequences",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Suppliers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "SupplierPayments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Sites",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "PurchaseItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "PurchaseHeaders",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ProjectTypes",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ProjectExpenses",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "PaymentMethods",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Materials",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "MaterialSubcategories",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "MaterialSpecValues",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "MaterialSpecDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "MaterialRequests",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "MaterialRequestItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "MaterialCategories",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "LabourEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "LabourCategories",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "InventoryTransactions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "InventoryBalances",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ExpenseSubheads",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ExpenseHeads",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "CustomerPayments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Contractors",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ContractorPayments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ContractWorks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "AuditLogs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "Attachments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ApprovalRequests",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "ApprovalHistories",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ContactMobile = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LicenseExpiresOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    RefreshTokenExpiry = table.Column<long>(type: "INTEGER", nullable: true),
                    LastLoginAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId",
                table: "Users",
                column: "CompanyId");

            // Every carried-over user has the empty default this migration just added, so building a
            // UNIQUE index over it fails the moment a database holds more than one user — which is
            // every real install. Give each row a value that is unique by construction first.
            //
            // The row's own id is used rather than anything derived from the email, because pulling
            // out a local part needs instr/CHARINDEX and those differ per provider; this stays
            // portable. It is a placeholder, not a login: PlatformSeeder recognises a username that
            // is still its row's id and replaces it with one the person can actually type.
            migrationBuilder.Sql(
                "UPDATE Users SET Username = CAST(Id AS varchar(64)) WHERE Username IS NULL OR Username = ''");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId_Username",
                table: "Users",
                columns: new[] { "CompanyId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSiteAssignments_CompanyId",
                table: "UserSiteAssignments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_CompanyId",
                table: "UserPermissions",
                column: "CompanyId");

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
                name: "IX_TransactionSequences_CompanyId",
                table: "TransactionSequences",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSequences_CompanyId_Prefix_Year",
                table: "TransactionSequences",
                columns: new[] { "CompanyId", "Prefix", "Year" },
                unique: true);

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
                name: "IX_SupplierPayments_CompanyId",
                table: "SupplierPayments",
                column: "CompanyId");

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
                name: "IX_Settings_CompanyId",
                table: "Settings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_CompanyId_Key_SiteId",
                table: "Settings",
                columns: new[] { "CompanyId", "Key", "SiteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_CompanyId",
                table: "PurchaseItems",
                column: "CompanyId");

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
                name: "IX_Projects_CompanyId",
                table: "Projects",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CompanyId_Code",
                table: "Projects",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTypes_CompanyId",
                table: "ProjectTypes",
                column: "CompanyId");

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
                name: "IX_PaymentMethods_CompanyId",
                table: "PaymentMethods",
                column: "CompanyId");

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
                name: "IX_MaterialRequests_CompanyId",
                table: "MaterialRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_CompanyId_TxnNumber",
                table: "MaterialRequests",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequestItems_CompanyId",
                table: "MaterialRequestItems",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialCategories_CompanyId",
                table: "MaterialCategories",
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
                name: "IX_LabourCategories_CompanyId",
                table: "LabourCategories",
                column: "CompanyId");

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
                name: "IX_InventoryBalances_CompanyId",
                table: "InventoryBalances",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_CompanyId_SiteId_MaterialId",
                table: "InventoryBalances",
                columns: new[] { "CompanyId", "SiteId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_SiteId",
                table: "InventoryBalances",
                column: "SiteId");

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
                name: "IX_ExpenseHeads_CompanyId",
                table: "ExpenseHeads",
                column: "CompanyId");

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
                name: "IX_CustomerPayments_CompanyId",
                table: "CustomerPayments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CompanyId_TxnNumber",
                table: "CustomerPayments",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

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
                name: "IX_ContractorPayments_CompanyId",
                table: "ContractorPayments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_CompanyId_TxnNumber",
                table: "ContractorPayments",
                columns: new[] { "CompanyId", "TxnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorks_CompanyId",
                table: "ContractWorks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CompanyId",
                table: "AuditLogs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_CompanyId",
                table: "Attachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_CompanyId",
                table: "ApprovalRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalHistories_CompanyId",
                table: "ApprovalHistories",
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
                name: "IX_PlatformUsers_Username",
                table: "PlatformUsers",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "PlatformUsers");

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserSiteAssignments_CompanyId",
                table: "UserSiteAssignments");

            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_CompanyId",
                table: "UserPermissions");

            migrationBuilder.DropIndex(
                name: "IX_Units_CompanyId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Units_CompanyId_Code",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_TransactionSequences_CompanyId",
                table: "TransactionSequences");

            migrationBuilder.DropIndex(
                name: "IX_TransactionSequences_CompanyId_Prefix_Year",
                table: "TransactionSequences");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CompanyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CompanyId_Code",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_CompanyId",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_Sites_CompanyId",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Sites_CompanyId_Code",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Settings_CompanyId",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_Settings_CompanyId_Key_SiteId",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseItems_CompanyId",
                table: "PurchaseItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseHeaders_CompanyId",
                table: "PurchaseHeaders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseHeaders_CompanyId_TxnNumber",
                table: "PurchaseHeaders");

            migrationBuilder.DropIndex(
                name: "IX_Projects_CompanyId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_CompanyId_Code",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTypes_CompanyId",
                table: "ProjectTypes");

            migrationBuilder.DropIndex(
                name: "IX_ProjectExpenses_CompanyId",
                table: "ProjectExpenses");

            migrationBuilder.DropIndex(
                name: "IX_ProjectExpenses_CompanyId_TxnNumber",
                table: "ProjectExpenses");

            migrationBuilder.DropIndex(
                name: "IX_PaymentMethods_CompanyId",
                table: "PaymentMethods");

            migrationBuilder.DropIndex(
                name: "IX_Materials_CompanyId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_CompanyId_Code",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_CompanyId_SpecSignature",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSubcategories_CompanyId",
                table: "MaterialSubcategories");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSubcategories_CompanyId_MaterialCategoryId_Name",
                table: "MaterialSubcategories");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSubcategories_MaterialCategoryId",
                table: "MaterialSubcategories");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSpecValues_CompanyId",
                table: "MaterialSpecValues");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSpecValues_CompanyId_MaterialId_MaterialSpecDefinitionId",
                table: "MaterialSpecValues");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSpecValues_MaterialId",
                table: "MaterialSpecValues");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSpecDefinitions_CompanyId",
                table: "MaterialSpecDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSpecDefinitions_CompanyId_MaterialSubcategoryId_Key",
                table: "MaterialSpecDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialSpecDefinitions_MaterialSubcategoryId",
                table: "MaterialSpecDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequests_CompanyId",
                table: "MaterialRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequests_CompanyId_TxnNumber",
                table: "MaterialRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequestItems_CompanyId",
                table: "MaterialRequestItems");

            migrationBuilder.DropIndex(
                name: "IX_MaterialCategories_CompanyId",
                table: "MaterialCategories");

            migrationBuilder.DropIndex(
                name: "IX_LabourEntries_CompanyId",
                table: "LabourEntries");

            migrationBuilder.DropIndex(
                name: "IX_LabourEntries_CompanyId_TxnNumber",
                table: "LabourEntries");

            migrationBuilder.DropIndex(
                name: "IX_LabourCategories_CompanyId",
                table: "LabourCategories");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_CompanyId",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_CompanyId_TxnNumber",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBalances_CompanyId",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBalances_CompanyId_SiteId_MaterialId",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_InventoryBalances_SiteId",
                table: "InventoryBalances");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseSubheads_CompanyId",
                table: "ExpenseSubheads");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseSubheads_CompanyId_ExpenseHeadId_Name",
                table: "ExpenseSubheads");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseSubheads_ExpenseHeadId",
                table: "ExpenseSubheads");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseHeads_CompanyId",
                table: "ExpenseHeads");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CompanyId_Code",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_CompanyId",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_CompanyId_TxnNumber",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_Contractors_CompanyId",
                table: "Contractors");

            migrationBuilder.DropIndex(
                name: "IX_Contractors_CompanyId_Code",
                table: "Contractors");

            migrationBuilder.DropIndex(
                name: "IX_ContractorPayments_CompanyId",
                table: "ContractorPayments");

            migrationBuilder.DropIndex(
                name: "IX_ContractorPayments_CompanyId_TxnNumber",
                table: "ContractorPayments");

            migrationBuilder.DropIndex(
                name: "IX_ContractWorks_CompanyId",
                table: "ContractWorks");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CompanyId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_CompanyId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRequests_CompanyId",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalHistories_CompanyId",
                table: "ApprovalHistories");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsCompanyAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UserSiteAssignments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "TransactionSequences");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "PurchaseHeaders");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ProjectTypes");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ProjectExpenses");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "MaterialSubcategories");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "MaterialSpecValues");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "MaterialSpecDefinitions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "MaterialRequestItems");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "MaterialCategories");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "LabourEntries");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "LabourCategories");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "InventoryBalances");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ExpenseSubheads");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ExpenseHeads");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Contractors");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ContractWorks");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ApprovalHistories");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_Code",
                table: "Units",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSequences_Prefix_Year",
                table: "TransactionSequences",
                columns: new[] { "Prefix", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Code",
                table: "Suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sites_Code",
                table: "Sites",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Key_SiteId",
                table: "Settings",
                columns: new[] { "Key", "SiteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHeaders_TxnNumber",
                table: "PurchaseHeaders",
                column: "TxnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Code",
                table: "Projects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_TxnNumber",
                table: "ProjectExpenses",
                column: "TxnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Code",
                table: "Materials",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_SpecSignature",
                table: "Materials",
                column: "SpecSignature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSubcategories_MaterialCategoryId_Name",
                table: "MaterialSubcategories",
                columns: new[] { "MaterialCategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecValues_MaterialId_MaterialSpecDefinitionId",
                table: "MaterialSpecValues",
                columns: new[] { "MaterialId", "MaterialSpecDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecDefinitions_MaterialSubcategoryId_Key",
                table: "MaterialSpecDefinitions",
                columns: new[] { "MaterialSubcategoryId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_TxnNumber",
                table: "MaterialRequests",
                column: "TxnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabourEntries_TxnNumber",
                table: "LabourEntries",
                column: "TxnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TxnNumber",
                table: "InventoryTransactions",
                column: "TxnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_SiteId_MaterialId",
                table: "InventoryBalances",
                columns: new[] { "SiteId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSubheads_ExpenseHeadId_Name",
                table: "ExpenseSubheads",
                columns: new[] { "ExpenseHeadId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Code",
                table: "Customers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_TxnNumber",
                table: "CustomerPayments",
                column: "TxnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_Code",
                table: "Contractors",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractorPayments_TxnNumber",
                table: "ContractorPayments",
                column: "TxnNumber",
                unique: true);
        }
    }
}
