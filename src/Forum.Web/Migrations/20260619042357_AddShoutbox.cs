using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddShoutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShoutMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SenderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoutMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoutMessages_AspNetUsers_SenderId",
                        column: x => x.SenderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShoutMessages_CreatedAt",
                table: "ShoutMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShoutMessages_SenderId",
                table: "ShoutMessages",
                column: "SenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShoutMessages");
        }
    }
}
