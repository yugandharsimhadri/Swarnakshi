using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P5_ConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseHeaders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProjectExpenses");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LabourEntries");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ContractWorks");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "PurchaseHeaders",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "ProjectExpenses",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "MaterialRequests",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "LabourEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "InventoryTransactions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "CustomerPayments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "ContractorPayments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "ContractWorks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "PurchaseHeaders");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "ProjectExpenses");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "LabourEntries");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "ContractorPayments");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "ContractWorks");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PurchaseHeaders",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProjectExpenses",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MaterialRequests",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LabourEntries",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InventoryTransactions",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CustomerPayments",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ContractorPayments",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ContractWorks",
                type: "BLOB",
                nullable: true);
        }
    }
}
