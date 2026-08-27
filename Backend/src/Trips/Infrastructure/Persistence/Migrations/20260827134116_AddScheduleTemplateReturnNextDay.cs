using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleTemplateReturnNextDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "return_next_day",
                schema: "trips",
                table: "schedule_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "return_next_day",
                schema: "trips",
                table: "rm_schedule_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "return_next_day",
                schema: "trips",
                table: "schedule_templates");

            migrationBuilder.DropColumn(
                name: "return_next_day",
                schema: "trips",
                table: "rm_schedule_templates");
        }
    }
}
