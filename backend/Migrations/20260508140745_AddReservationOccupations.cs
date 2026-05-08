using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kennel.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationOccupations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "ArrivalTime",
                table: "Reservations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DepartureTime",
                table: "Reservations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Occupations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReservationId = table.Column<int>(type: "INTEGER", nullable: false),
                    KennelId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Occupations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Occupations_Kennels_KennelId",
                        column: x => x.KennelId,
                        principalTable: "Kennels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Occupations_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Occupations_KennelId",
                table: "Occupations",
                column: "KennelId");

            migrationBuilder.CreateIndex(
                name: "IX_Occupations_ReservationId",
                table: "Occupations",
                column: "ReservationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Occupations");

            migrationBuilder.DropColumn(
                name: "ArrivalTime",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "DepartureTime",
                table: "Reservations");
        }
    }
}
