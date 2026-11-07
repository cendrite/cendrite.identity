using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class add_account_key_rotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_key_rotation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_account_public_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    new_account_public_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    proof = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    approval = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_key_rotation", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_key_rotation_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_account_key_rotation_user_id",
                table: "account_key_rotation",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_key_rotation");
        }
    }
}
