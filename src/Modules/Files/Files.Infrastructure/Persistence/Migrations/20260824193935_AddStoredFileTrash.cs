using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Files.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFileTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_OwnerId_FolderId_OriginalFileName",
                schema: "files",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_OwnerId_OriginalFileName",
                schema: "files",
                table: "StoredFiles");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                schema: "files",
                table: "StoredFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_DeletedAtUtc",
                schema: "files",
                table: "StoredFiles",
                column: "DeletedAtUtc",
                filter: "\"DeletedAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerId_FolderId_OriginalFileName",
                schema: "files",
                table: "StoredFiles",
                columns: new[] { "OwnerId", "FolderId", "OriginalFileName" },
                unique: true,
                filter: "\"FolderId\" IS NOT NULL AND \"DeletedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerId_OriginalFileName",
                schema: "files",
                table: "StoredFiles",
                columns: new[] { "OwnerId", "OriginalFileName" },
                unique: true,
                filter: "\"FolderId\" IS NULL AND \"DeletedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_DeletedAtUtc",
                schema: "files",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_OwnerId_FolderId_OriginalFileName",
                schema: "files",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_OwnerId_OriginalFileName",
                schema: "files",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "files",
                table: "StoredFiles");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerId_FolderId_OriginalFileName",
                schema: "files",
                table: "StoredFiles",
                columns: new[] { "OwnerId", "FolderId", "OriginalFileName" },
                unique: true,
                filter: "\"FolderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerId_OriginalFileName",
                schema: "files",
                table: "StoredFiles",
                columns: new[] { "OwnerId", "OriginalFileName" },
                unique: true,
                filter: "\"FolderId\" IS NULL");
        }
    }
}
