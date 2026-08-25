using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceRadar.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Marketplace",
                table: "TrackedProducts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Marketplace",
                table: "TrackedProducts");
        }
    }
}
