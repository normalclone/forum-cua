using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddChatAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentName",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentType",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsImageAttachment",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentName",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentType",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsImageAttachment",
                table: "ChatMessages");
        }
    }
}
