using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceRadar.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialScrapeFailureStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InitialScrapeFailed",
                table: "TrackedProducts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialScrapeFailed",
                table: "TrackedProducts");
        }
    }
}
