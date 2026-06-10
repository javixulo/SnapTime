using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class F7_HeuristicEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuggestionStatus",
                table: "MediaAssets",
                type: "TEXT",
                nullable: false,
                defaultValue: "Unreviewed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuggestionStatus",
                table: "MediaAssets");
        }
    }
}
