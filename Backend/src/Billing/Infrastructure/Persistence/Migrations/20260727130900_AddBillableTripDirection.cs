using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillableTripDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "direction",
                schema: "billing",
                table: "billable_trips",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "direction",
                schema: "billing",
                table: "billable_trips");
        }
    }
}
