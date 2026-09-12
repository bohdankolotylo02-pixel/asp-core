using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientVehicles.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PhoneKey = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TaxNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    NameKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.CheckConstraint("CK_Clients_RequiredText", "length(trim(\"Name\")) > 0 AND length(trim(\"Phone\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    LicensePlate = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LicensePlateKey = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Vin = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    VinKey = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Mileage = table.Column<int>(type: "INTEGER", nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedWithClient = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.CheckConstraint("CK_Vehicles_MileageNotNegative", "\"Mileage\" IS NULL OR \"Mileage\" >= 0");
                    table.CheckConstraint("CK_Vehicles_RequiredText", "length(trim(\"LicensePlate\")) > 0 AND length(trim(\"Brand\")) > 0 AND length(trim(\"Model\")) > 0");
                    table.ForeignKey(
                        name: "FK_Vehicles_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_IsArchived",
                table: "Clients",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_NameKey",
                table: "Clients",
                column: "NameKey");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_PhoneKey",
                table: "Clients",
                column: "PhoneKey");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ClientId",
                table: "Vehicles",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_IsArchived",
                table: "Vehicles",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "UX_Vehicles_LicensePlateKey_Active",
                table: "Vehicles",
                column: "LicensePlateKey",
                unique: true,
                filter: "\"IsArchived\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Vehicles_VinKey_Active",
                table: "Vehicles",
                column: "VinKey",
                unique: true,
                filter: "\"IsArchived\" = 0 AND \"VinKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
