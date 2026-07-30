using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Progesi.Infrastructure.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddAxisTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Axis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AxisName = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ValueTypeKey = table.Column<string>(type: "TEXT", nullable: false),
                    AxisLength = table.Column<double>(type: "REAL", nullable: true),
                    CurvePayload = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    KeyPointsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: true),
                    FunctionId = table.Column<int>(type: "INTEGER", nullable: true),
                    FunctionHashtag = table.Column<string>(type: "TEXT", nullable: true),
                    FunctionPayload = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    StationsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", nullable: false),
                    Hashtag = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Axis", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Axis_ContentHash",
                table: "Axis",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Axis_Hashtag",
                table: "Axis",
                column: "Hashtag");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Axis");
        }
    }
}
