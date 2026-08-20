using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingPasswordResets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingPasswordResets",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingPasswordResets", x => x.Id);
                    table.CheckConstraint("CK_PendingPasswordResets_ExpiresAfterCreated", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_PendingPasswordResets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingPasswordResets_ExpiresAtUtc",
                schema: "auth",
                table: "PendingPasswordResets",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "PendingPasswordResetUserIndex",
                schema: "auth",
                table: "PendingPasswordResets",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingPasswordResets",
                schema: "auth");
        }
    }
}
