using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Berg.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSolveId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Solves",
                table: "Solves");

            migrationBuilder.DropIndex(
                name: "IX_Solves_PlayerId",
                table: "Solves");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Solves");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Solves",
                table: "Solves",
                columns: new[] { "PlayerId", "ChallengeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Solves",
                table: "Solves");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "Solves",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Solves",
                table: "Solves",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Solves_PlayerId",
                table: "Solves",
                column: "PlayerId");
        }
    }
}
