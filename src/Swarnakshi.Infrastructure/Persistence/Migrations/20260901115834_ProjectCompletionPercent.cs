using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectCompletionPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompletionPercent",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // A default of 0 across the board would report every already-finished villa as untouched,
            // and drag the average completion down with it. Anything already marked Completed
            // (ProjectStatus.Completed = 3) is by definition fully built.
            migrationBuilder.Sql("UPDATE Projects SET CompletionPercent = 100 WHERE Status = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionPercent",
                table: "Projects");
        }
    }
}
