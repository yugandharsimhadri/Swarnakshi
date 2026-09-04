using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_CompanyId_Status_Covering",
                table: "ProjectExpenses",
                columns: new[] { "CompanyId", "Status", "ProjectId" })
                .Annotation("SqlServer:Include", new[] { "ExpenseType", "Amount", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_CompanyId_SiteId_Type_Date",
                table: "InventoryTransactions",
                columns: new[] { "CompanyId", "SiteId", "Type", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectExpenses_CompanyId_Status_Covering",
                table: "ProjectExpenses");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_CompanyId_SiteId_Type_Date",
                table: "InventoryTransactions");
        }
    }
}
