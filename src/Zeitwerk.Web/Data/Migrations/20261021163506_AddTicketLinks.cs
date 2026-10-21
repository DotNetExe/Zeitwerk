using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeitwerk.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TicketPattern",
                table: "Projects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketUrlTemplate",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketPattern",
                table: "Customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketUrlTemplate",
                table: "Customers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TicketPattern",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TicketUrlTemplate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TicketPattern",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TicketUrlTemplate",
                table: "Customers");
        }
    }
}
