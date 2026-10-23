using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyNoticeAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "privacy_notice_accepted_at_utc",
                schema: "app",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "privacy_notice_version",
                schema: "app",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "privacy_notice_accepted_at_utc",
                schema: "app",
                table: "email_challenge_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "privacy_notice_version",
                schema: "app",
                table: "email_challenge_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "privacy_notice_accepted_at_utc",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "privacy_notice_version",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "privacy_notice_accepted_at_utc",
                schema: "app",
                table: "email_challenge_sessions");

            migrationBuilder.DropColumn(
                name: "privacy_notice_version",
                schema: "app",
                table: "email_challenge_sessions");
        }
    }
}
