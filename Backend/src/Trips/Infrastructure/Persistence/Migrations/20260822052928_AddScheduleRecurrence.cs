using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "anchor_date",
                schema: "trips",
                table: "schedule_templates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "days_of_month",
                schema: "trips",
                table: "schedule_templates",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "interval_days",
                schema: "trips",
                table: "schedule_templates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recurrence_kind",
                schema: "trips",
                table: "schedule_templates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "DaysOfWeek");

            migrationBuilder.AddColumn<DateOnly>(
                name: "anchor_date",
                schema: "trips",
                table: "rm_schedule_templates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "days_of_month",
                schema: "trips",
                table: "rm_schedule_templates",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "interval_days",
                schema: "trips",
                table: "rm_schedule_templates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recurrence_kind",
                schema: "trips",
                table: "rm_schedule_templates",
                type: "text",
                nullable: false,
                defaultValue: "DaysOfWeek");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anchor_date",
                schema: "trips",
                table: "schedule_templates");

            migrationBuilder.DropColumn(
                name: "days_of_month",
                schema: "trips",
                table: "schedule_templates");

            migrationBuilder.DropColumn(
                name: "interval_days",
                schema: "trips",
                table: "schedule_templates");

            migrationBuilder.DropColumn(
                name: "recurrence_kind",
                schema: "trips",
                table: "schedule_templates");

            migrationBuilder.DropColumn(
                name: "anchor_date",
                schema: "trips",
                table: "rm_schedule_templates");

            migrationBuilder.DropColumn(
                name: "days_of_month",
                schema: "trips",
                table: "rm_schedule_templates");

            migrationBuilder.DropColumn(
                name: "interval_days",
                schema: "trips",
                table: "rm_schedule_templates");

            migrationBuilder.DropColumn(
                name: "recurrence_kind",
                schema: "trips",
                table: "rm_schedule_templates");
        }
    }
}
