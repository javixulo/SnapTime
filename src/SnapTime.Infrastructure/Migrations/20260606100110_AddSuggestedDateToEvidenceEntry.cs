using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuggestedDateToEvidenceEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SuggestedDate",
                table: "EvidenceEntries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuggestedDate",
                table: "EvidenceEntries");
        }
    }
}
