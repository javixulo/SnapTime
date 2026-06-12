using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class F10_AddSettingsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeuristicConfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeuristicConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfidenceThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxConcurrency = table.Column<int>(type: "INTEGER", nullable: false),
                    BatchSize = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageExtensionsCsv = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    VideoExtensionsCsv = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OllamaEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OllamaModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OllamaTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ThumbnailMaxDimension = table.Column<int>(type: "INTEGER", nullable: false),
                    ThumbnailQuality = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeuristicConfigs");

            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
