using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeTracker.Migrations
{
    /// <inheritdoc />
    public partial class V2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserWatched_Users_UserId",
                table: "UserWatched");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserWatched",
                table: "UserWatched");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "UserWatched",
                newName: "userwatched");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users");

            migrationBuilder.RenameIndex(
                name: "IX_UserWatched_UserId",
                table: "userwatched",
                newName: "IX_userwatched_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_userwatched",
                table: "userwatched",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_userwatched_users_UserId",
                table: "userwatched",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_userwatched_users_UserId",
                table: "userwatched");

            migrationBuilder.DropPrimaryKey(
                name: "PK_userwatched",
                table: "userwatched");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.RenameTable(
                name: "userwatched",
                newName: "UserWatched");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "Users");

            migrationBuilder.RenameIndex(
                name: "IX_userwatched_UserId",
                table: "UserWatched",
                newName: "IX_UserWatched_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserWatched",
                table: "UserWatched",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserWatched_Users_UserId",
                table: "UserWatched",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
