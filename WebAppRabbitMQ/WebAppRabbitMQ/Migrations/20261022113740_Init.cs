using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAppRabbitMQ.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Version",
                table: "Tasks",
                newName: "xmin");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                table: "Tasks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldRowVersion: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "xmin",
                table: "Tasks",
                newName: "Version");

            migrationBuilder.AlterColumn<string>(
                name: "Version",
                table: "Tasks",
                type: "text",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);
        }
    }
}
