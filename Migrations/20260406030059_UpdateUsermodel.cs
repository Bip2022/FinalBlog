using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinalBlog.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUsermodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsVerified",
                table: "Users",
                newName: "IsApproved");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsApproved",
                table: "Users",
                newName: "IsVerified");
        }
    }
}
