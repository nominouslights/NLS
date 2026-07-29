using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Drivers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxProcessingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processed_at_utc",
                schema: "drivers",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "processing_attempts",
                schema: "drivers",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "processing_last_error",
                schema: "drivers",
                table: "outbox_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processing_next_attempt_at_utc",
                schema: "drivers",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processing_status",
                schema: "drivers",
                table: "outbox_messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "drivers",
                table: "outbox_messages",
                column: "position",
                filter: "processing_status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "drivers",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "processed_at_utc",
                schema: "drivers",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "processing_attempts",
                schema: "drivers",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "processing_last_error",
                schema: "drivers",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "processing_next_attempt_at_utc",
                schema: "drivers",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "processing_status",
                schema: "drivers",
                table: "outbox_messages");
        }
    }
}
