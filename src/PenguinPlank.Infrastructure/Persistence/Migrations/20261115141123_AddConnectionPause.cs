using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinPlank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionPause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "IntegrationConnections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAtUtc",
                table: "IntegrationConnections",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "PausedAtUtc",
                table: "IntegrationConnections");
        }
    }
}
