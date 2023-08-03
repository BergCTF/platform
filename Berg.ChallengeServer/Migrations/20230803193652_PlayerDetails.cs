using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Berg.ChallengeServer.Migrations
{
    /// <inheritdoc />
    public partial class PlayerDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_PlayerCategories_PlayerCategoryId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "PlayerCategories");

            migrationBuilder.DropIndex(
                name: "IX_Players_PlayerCategoryId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PlayerCategoryId",
                table: "Players");

            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "Submissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Players",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Players",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Value",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Players");

            migrationBuilder.AddColumn<Guid>(
                name: "PlayerCategoryId",
                table: "Players",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlayerCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_PlayerCategoryId",
                table: "Players",
                column: "PlayerCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_PlayerCategories_PlayerCategoryId",
                table: "Players",
                column: "PlayerCategoryId",
                principalTable: "PlayerCategories",
                principalColumn: "Id");
        }
    }
}
