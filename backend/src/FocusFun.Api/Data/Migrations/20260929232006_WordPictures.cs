using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusFun.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class WordPictures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PictureHint",
                table: "CategoryWords",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "UseImagePictures",
                table: "Categories",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "WordPictures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Provider = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Model = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Prompt = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EditedFromId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ImageKey = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalKey = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordPictures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WordPictures_CategoryWords_WordId",
                        column: x => x.WordId,
                        principalTable: "CategoryWords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WordPictures_WordId_Status",
                table: "WordPictures",
                columns: new[] { "WordId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WordPictures");

            migrationBuilder.DropColumn(
                name: "PictureHint",
                table: "CategoryWords");

            migrationBuilder.DropColumn(
                name: "UseImagePictures",
                table: "Categories");
        }
    }
}
