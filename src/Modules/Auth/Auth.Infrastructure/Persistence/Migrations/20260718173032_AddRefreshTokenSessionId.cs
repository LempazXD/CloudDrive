using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                schema: "auth",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            // Строки, созданные до появления понятия "сессия", считаем корнем собственной
            // однотокенной сессии - это лишь дополнительно сужает будущий отзыв относительно
            // прежнего поведения (отзыв всего аккаунта), а не наоборот.
            migrationBuilder.Sql(
                "UPDATE \"auth\".\"RefreshTokens\" SET \"SessionId\" = \"Id\" WHERE \"SessionId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                schema: "auth",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessionId",
                schema: "auth",
                table: "RefreshTokens",
                column: "SessionId",
                filter: "\"RevokedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_SessionId",
                schema: "auth",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "auth",
                table: "RefreshTokens");
        }
    }
}
