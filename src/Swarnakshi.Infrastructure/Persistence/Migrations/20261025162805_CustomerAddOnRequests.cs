using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAddOnRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "add_on_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    txn_number = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    expense_head_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    expected_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    remarks = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_add_on_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_add_on_requests_expense_heads_expense_head_id",
                        column: x => x.expense_head_id,
                        principalTable: "expense_heads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_add_on_requests_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_add_on_requests_company_id",
                table: "add_on_requests",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_add_on_requests_company_id_txn_number",
                table: "add_on_requests",
                columns: new[] { "company_id", "txn_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_add_on_requests_expense_head_id",
                table: "add_on_requests",
                column: "expense_head_id");

            migrationBuilder.CreateIndex(
                name: "ix_add_on_requests_project_id_status",
                table: "add_on_requests",
                columns: new[] { "project_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "add_on_requests");
        }
    }
}
