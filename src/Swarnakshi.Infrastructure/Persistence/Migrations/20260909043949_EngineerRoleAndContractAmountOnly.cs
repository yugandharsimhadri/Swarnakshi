using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EngineerRoleAndContractAmountOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A work order now carries only what was agreed with the contractor. The estimate beside
            // it was read by no report and compared against by no screen, and two figures where one
            // is the truth is an invitation to pay against the wrong one.
            //
            // This drops data, deliberately: a column nothing reads is how a schema fills up with
            // numbers nobody can explain. What was recorded is in the backup taken before the
            // upgrade, which is the reason that backup is not optional.
            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "ContractWorks");

            // The Engineer role (UserRole.Engineer = 5) needs no schema change — Role is an int and
            // this is one more value. Named here so that whoever reads the migration next to the
            // release notes is not left hunting for it.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "ContractWorks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
