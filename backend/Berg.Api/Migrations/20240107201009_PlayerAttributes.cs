using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Berg.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlayerAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:hstore", ",,");

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "Attributes",
                table: "Players",
                type: "hstore",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attributes",
                table: "Players");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:hstore", ",,");
        }
    }
}
