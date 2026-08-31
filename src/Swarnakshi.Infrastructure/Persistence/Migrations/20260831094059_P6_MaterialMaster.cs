using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Swarnakshi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P6_MaterialMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Materials",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenericMeasurement",
                table: "Materials",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecSignature",
                table: "Materials",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SpecSummary",
                table: "Materials",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            // Every existing row would otherwise share the "" default and collide on the unique
            // index below. Seed each signature with its own row Id so the index can be built; the
            // seeder (MaterialMasterSeeder.RefreshMaterialIdentityAsync) replaces these with the
            // real name|brand|specs key on the next startup.
            // CAST keeps this provider-agnostic: SQLite applies TEXT affinity, SQL Server converts
            // the uniqueidentifier to characters. No provider-specific SQL.
            migrationBuilder.Sql("UPDATE Materials SET SpecSignature = CAST(Id AS varchar(64))");

            migrationBuilder.CreateTable(
                name: "MaterialSpecDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaterialSubcategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Options = table.Column<string>(type: "TEXT", maxLength: 600, nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    PartOfIdentity = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialSpecDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialSpecDefinitions_MaterialSubcategories_MaterialSubcategoryId",
                        column: x => x.MaterialSubcategoryId,
                        principalTable: "MaterialSubcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaterialSpecValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaterialId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaterialSpecDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialSpecValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialSpecValues_MaterialSpecDefinitions_MaterialSpecDefinitionId",
                        column: x => x.MaterialSpecDefinitionId,
                        principalTable: "MaterialSpecDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialSpecValues_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Brand",
                table: "Materials",
                column: "Brand");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_IsActive",
                table: "Materials",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_SpecSignature",
                table: "Materials",
                column: "SpecSignature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecDefinitions_MaterialSubcategoryId_Key",
                table: "MaterialSpecDefinitions",
                columns: new[] { "MaterialSubcategoryId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecValues_MaterialId_MaterialSpecDefinitionId",
                table: "MaterialSpecValues",
                columns: new[] { "MaterialId", "MaterialSpecDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecValues_MaterialSpecDefinitionId",
                table: "MaterialSpecValues",
                column: "MaterialSpecDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSpecValues_Value",
                table: "MaterialSpecValues",
                column: "Value");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialSpecValues");

            migrationBuilder.DropTable(
                name: "MaterialSpecDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Materials_Brand",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_IsActive",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_SpecSignature",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "GenericMeasurement",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "SpecSignature",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "SpecSummary",
                table: "Materials");
        }
    }
}
