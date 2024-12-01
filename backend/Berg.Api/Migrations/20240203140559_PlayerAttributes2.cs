using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Berg.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlayerAttributes2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attributes",
                table: "Players");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:hstore", ",,");

            migrationBuilder.CreateTable(
                name: "PlayerAttribute",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAttribute", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerAttribute_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAttribute_PlayerId",
                table: "PlayerAttribute",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerAttribute");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:hstore", ",,");

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "Attributes",
                table: "Players",
                type: "hstore",
                nullable: true);
        }
    }
}
