using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Topics",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);   // các chủ đề hiện có mặc định đã duyệt (vẫn hiển thị)

            migrationBuilder.AddColumn<bool>(
                name: "RequireApproval",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "RequireApproval",
                table: "Categories");
        }
    }
}
