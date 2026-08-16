using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progesi.Infrastructure.EF.Migrations
{
    /// <inheritdoc />
    public partial class B3a_AxisLabelsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LabelsJson",
                table: "Axis",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelsJson",
                table: "Axis");
        }
    }
}
