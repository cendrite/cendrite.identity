using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class add_user_tag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "user_tag",
                table: "user",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "idx_user_tag",
                table: "user",
                column: "user_tag",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_user_tag",
                table: "user");

            migrationBuilder.DropColumn(
                name: "user_tag",
                table: "user");
        }
    }
}
