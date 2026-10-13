using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OwnPocketExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "paid_by_user_id",
                table: "project_expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reimbursement_status",
                table: "project_expenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "expense_settlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    txn_number = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    paid_to_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_method_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    remarks = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_settlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_expense_settlements_payment_methods_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expense_settlements_users_paid_to_user_id",
                        column: x => x.paid_to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expense_settlement_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_settlement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_settlement_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_expense_settlement_lines_expense_settlements_expense_settle",
                        column: x => x.expense_settlement_id,
                        principalTable: "expense_settlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_expense_settlement_lines_project_expenses_project_expense_id",
                        column: x => x.project_expense_id,
                        principalTable: "project_expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_expenses_paid_by_user_id",
                table: "project_expenses",
                column: "paid_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExpenses_Reimbursement",
                table: "project_expenses",
                columns: new[] { "company_id", "reimbursement_status", "paid_by_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_settlement_lines_company_id",
                table: "expense_settlement_lines",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_settlement_lines_company_id_project_expense_id",
                table: "expense_settlement_lines",
                columns: new[] { "company_id", "project_expense_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_settlement_lines_expense_settlement_id",
                table: "expense_settlement_lines",
                column: "expense_settlement_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_settlement_lines_project_expense_id",
                table: "expense_settlement_lines",
                column: "project_expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_settlements_company_id",
                table: "expense_settlements",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_settlements_company_id_paid_to_user_id_date",
                table: "expense_settlements",
                columns: new[] { "company_id", "paid_to_user_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_settlements_company_id_txn_number",
                table: "expense_settlements",
                columns: new[] { "company_id", "txn_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_settlements_paid_to_user_id",
                table: "expense_settlements",
                column: "paid_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_settlements_payment_method_id",
                table: "expense_settlements",
                column: "payment_method_id");

            migrationBuilder.AddForeignKey(
                name: "fk_project_expenses_users_paid_by_user_id",
                table: "project_expenses",
                column: "paid_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_project_expenses_users_paid_by_user_id",
                table: "project_expenses");

            migrationBuilder.DropTable(
                name: "expense_settlement_lines");

            migrationBuilder.DropTable(
                name: "expense_settlements");

            migrationBuilder.DropIndex(
                name: "ix_project_expenses_paid_by_user_id",
                table: "project_expenses");

            migrationBuilder.DropIndex(
                name: "IX_ProjectExpenses_Reimbursement",
                table: "project_expenses");

            migrationBuilder.DropColumn(
                name: "paid_by_user_id",
                table: "project_expenses");

            migrationBuilder.DropColumn(
                name: "reimbursement_status",
                table: "project_expenses");
        }
    }
}
