using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapTime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEntryScanJobIdAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScanJobId",
                table: "AuditEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanJobs_CreatedAt",
                table: "ScanJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EventType",
                table: "AuditEntries",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ScanJobId",
                table: "AuditEntries",
                column: "ScanJobId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditEntries_ScanJobs_ScanJobId",
                table: "AuditEntries",
                column: "ScanJobId",
                principalTable: "ScanJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditEntries_ScanJobs_ScanJobId",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_ScanJobs_CreatedAt",
                table: "ScanJobs");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_CreatedAt",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_EventType",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_ScanJobId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "ScanJobId",
                table: "AuditEntries");
        }
    }
}
