using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseDirectToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeliverToProjectId",
                table: "PurchaseItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseHeadId",
                table: "PurchaseItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_DeliverToProjectId",
                table: "PurchaseItems",
                column: "DeliverToProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_ExpenseHeadId",
                table: "PurchaseItems",
                column: "ExpenseHeadId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseItems_ExpenseHeads_ExpenseHeadId",
                table: "PurchaseItems",
                column: "ExpenseHeadId",
                principalTable: "ExpenseHeads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseItems_Projects_DeliverToProjectId",
                table: "PurchaseItems",
                column: "DeliverToProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseItems_ExpenseHeads_ExpenseHeadId",
                table: "PurchaseItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseItems_Projects_DeliverToProjectId",
                table: "PurchaseItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseItems_DeliverToProjectId",
                table: "PurchaseItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseItems_ExpenseHeadId",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "DeliverToProjectId",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "ExpenseHeadId",
                table: "PurchaseItems");
        }
    }
}
