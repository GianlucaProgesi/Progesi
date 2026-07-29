using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progesi.Infrastructure.EF.Migrations
{
    /// <inheritdoc />
    public partial class R2C2_EfGeometryColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObjectPayloadJson",
                table: "Variables",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObjectType",
                table: "Variables",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObjectPayloadJson",
                table: "Variables");

            migrationBuilder.DropColumn(
                name: "ObjectType",
                table: "Variables");
        }
    }
}
