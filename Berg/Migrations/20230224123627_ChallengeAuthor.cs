using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Berg.Migrations
{
    /// <inheritdoc />
    public partial class ChallengeAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "Challenges",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Author",
                table: "Challenges");
        }
    }
}
