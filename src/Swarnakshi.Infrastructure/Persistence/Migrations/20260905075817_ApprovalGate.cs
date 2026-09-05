using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "SupplierPayments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                table: "SupplierPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "SupplierPayments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModifiedAt",
                table: "SupplierPayments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                table: "SupplierPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "SupplierPayments",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "SupplierPayments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Every payment that existed before this change had already been added to its invoice's
            // PaidAmount — that was the only way to record one. The new column would default them
            // all to Draft, so the invoices would say paid while the payments said not yet, and the
            // handler that recomputes PaidAmount from posted rows would later zero them out.
            // 6 is TransactionStatus.Posted.
            //
            // Wrapped in EXEC, and that is load-bearing. `dotnet ef migrations script` emits the
            // whole migration as ONE sqlcmd batch, and SQL Server compiles a batch before it runs
            // any of it — so a plain UPDATE naming a column the ALTER above has not yet added fails
            // to parse with "Invalid column name", having already added the columns. EXEC defers
            // compilation to execution time. Applying through the API (--migrate) sends each
            // statement separately and would have worked either way, which is exactly what makes
            // this worth a comment: the two routes are not equivalent, and the script is the one
            // that breaks.
            migrationBuilder.Sql("EXEC(N'UPDATE [SupplierPayments] SET [Status] = 6');");

            // Same for the concurrency token: a table of rows all sharing the empty Guid would let
            // two concurrent edits of different rows look like edits of the same one.
            migrationBuilder.Sql("EXEC(N'UPDATE [SupplierPayments] SET [ConcurrencyToken] = NEWID()');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SupplierPayments");
        }
    }
}
