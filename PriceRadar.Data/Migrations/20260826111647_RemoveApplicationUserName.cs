using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PriceRadar.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveApplicationUserName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "ApplicationUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ApplicationUsers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
