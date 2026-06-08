using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncludeSubfolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeSubfolders",
                table: "ScanJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeSubfolders",
                table: "ScanJobs");
        }
    }
}
