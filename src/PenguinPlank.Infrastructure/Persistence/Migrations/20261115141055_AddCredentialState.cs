using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PenguinPlank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CredentialState",
                table: "IntegrationConnections",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CredentialStateChangedAtUtc",
                table: "IntegrationConnections",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialState",
                table: "IntegrationConnections");

            migrationBuilder.DropColumn(
                name: "CredentialStateChangedAtUtc",
                table: "IntegrationConnections");
        }
    }
}
