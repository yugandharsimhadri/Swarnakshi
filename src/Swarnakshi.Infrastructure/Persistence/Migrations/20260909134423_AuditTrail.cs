using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Action and EntityType narrow from 512. Nothing the application has ever written comes
            // close — a type name and a short phrase — but an ALTER that narrows a column fails
            // outright if one row is too long, and failing a deployment over a legacy value in a
            // table nobody reads would be an absurd way to lose an evening. Trim first; the
            // statements below then cannot fail.
            migrationBuilder.Sql("UPDATE [AuditLogs] SET [Action] = LEFT([Action], 400) WHERE LEN([Action]) > 400;");
            migrationBuilder.Sql("UPDATE [AuditLogs] SET [EntityType] = LEFT([EntityType], 100) WHERE LEN([EntityType]) > 100;");

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "DataJson",
                table: "AuditLogs",
                type: "nvarchar(max)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity",
                table: "AuditLogs",
                columns: new[] { "CompanyId", "EntityType", "EntityId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_When",
                table: "AuditLogs",
                columns: new[] { "CompanyId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Entity",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_When",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditLogs",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "DataJson",
                table: "AuditLogs",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400);
        }
    }
}
