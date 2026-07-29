using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillableTripIsEmptyLeg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_empty_leg",
                schema: "billing",
                table: "billable_trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_empty_leg",
                schema: "billing",
                table: "billable_trips");
        }
    }
}
