using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusFun.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PictureShapes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MirrorSafe",
                table: "WordPictures",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShapeJson",
                table: "WordPictures",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MirrorSafe",
                table: "WordPictures");

            migrationBuilder.DropColumn(
                name: "ShapeJson",
                table: "WordPictures");
        }
    }
}
