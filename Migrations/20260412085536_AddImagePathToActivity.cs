using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinalBlog.Migrations
{
    /// <inheritdoc />
    public partial class AddImagePathToActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Activities",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Activities");
        }
    }
}
