using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientVehicles.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class VinUniqueAcrossArchivedVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Vehicles_VinKey_Active",
                table: "Vehicles");

            migrationBuilder.CreateIndex(
                name: "UX_Vehicles_VinKey",
                table: "Vehicles",
                column: "VinKey",
                unique: true,
                filter: "\"VinKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Vehicles_VinKey",
                table: "Vehicles");

            migrationBuilder.CreateIndex(
                name: "UX_Vehicles_VinKey_Active",
                table: "Vehicles",
                column: "VinKey",
                unique: true,
                filter: "\"IsArchived\" = 0 AND \"VinKey\" IS NOT NULL");
        }
    }
}
