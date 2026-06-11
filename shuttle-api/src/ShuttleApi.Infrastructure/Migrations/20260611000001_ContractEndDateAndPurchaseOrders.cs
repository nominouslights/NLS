using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShuttleApi.Infrastructure.Persistence;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260611000001_ContractEndDateAndPurchaseOrders")]
    public partial class ContractEndDateAndPurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RenewalDate",
                table: "contracts",
                newName: "EndDate");

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderId",
                table: "trips",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TotalValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contract_purchase_orders",
                columns: table => new
                {
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_purchase_orders", x => new { x.ContractId, x.PurchaseOrderId });
                    table.ForeignKey(
                        name: "FK_contract_purchase_orders_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_purchase_orders_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_line_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_line_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_order_line_items_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trips_PurchaseOrderId",
                table: "trips",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_purchase_orders_PurchaseOrderId",
                table: "contract_purchase_orders",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_line_items_PurchaseOrderId",
                table: "purchase_order_line_items",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ClientId_PoNumber",
                table: "purchase_orders",
                columns: new[] { "ClientId", "PoNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_clients_ClientId",
                table: "purchase_orders",
                column: "ClientId",
                principalTable: "clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_trips_purchase_orders_PurchaseOrderId",
                table: "trips",
                column: "PurchaseOrderId",
                principalTable: "purchase_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trips_purchase_orders_PurchaseOrderId",
                table: "trips");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_clients_ClientId",
                table: "purchase_orders");

            migrationBuilder.DropTable(name: "contract_purchase_orders");
            migrationBuilder.DropTable(name: "purchase_order_line_items");
            migrationBuilder.DropTable(name: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_trips_PurchaseOrderId",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "trips");

            migrationBuilder.RenameColumn(
                name: "EndDate",
                table: "contracts",
                newName: "RenewalDate");
        }
    }
}
