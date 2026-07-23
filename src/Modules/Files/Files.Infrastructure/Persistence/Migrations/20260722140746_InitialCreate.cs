using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Files.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "files");

            migrationBuilder.CreateTable(
                name: "Folders",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentFolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Folders_Folders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalSchema: "files",
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Declared = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UploadId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpectedPartCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                    table.CheckConstraint("CK_StoredFiles_SizeBytesPositive", "\"SizeBytes\" > 0");
                    table.ForeignKey(
                        name: "FK_StoredFiles_Folders_FolderId",
                        column: x => x.FolderId,
                        principalSchema: "files",
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Folders_OwnerId_Name",
                schema: "files",
                table: "Folders",
                columns: new[] { "OwnerId", "Name" },
                unique: true,
                filter: "\"ParentFolderId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_OwnerId_ParentFolderId_Name",
                schema: "files",
                table: "Folders",
                columns: new[] { "OwnerId", "ParentFolderId", "Name" },
                unique: true,
                filter: "\"ParentFolderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_ParentFolderId",
                schema: "files",
                table: "Folders",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_FolderId",
                schema: "files",
                table: "StoredFiles",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerId",
                schema: "files",
                table: "StoredFiles",
                column: "OwnerId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredFiles",
                schema: "files");

            migrationBuilder.DropTable(
                name: "Folders",
                schema: "files");
        }
    }
}
