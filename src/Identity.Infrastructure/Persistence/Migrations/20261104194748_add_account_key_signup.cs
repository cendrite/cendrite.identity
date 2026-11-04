using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class add_account_key_signup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password",
                table: "user",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "user",
                type: "citext",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "account_public_key",
                table: "user",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approval_signature",
                table: "device",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approved_by",
                table: "device",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_account_public_key",
                table: "user",
                column: "account_public_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_account_public_key",
                table: "user");

            migrationBuilder.DropColumn(
                name: "account_public_key",
                table: "user");

            migrationBuilder.DropColumn(
                name: "approval_signature",
                table: "device");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "device");

            migrationBuilder.AlterColumn<string>(
                name: "password",
                table: "user",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "user",
                type: "citext",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
