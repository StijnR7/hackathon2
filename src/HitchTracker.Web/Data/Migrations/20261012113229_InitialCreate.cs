using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HitchTracker.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CenterLat = table.Column<double>(type: "REAL", nullable: false),
                    CenterLng = table.Column<double>(type: "REAL", nullable: false),
                    ServiceRadiusKm = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LicenseNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Places",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Lat = table.Column<double>(type: "REAL", nullable: false),
                    Lng = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Places", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Places_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tariffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    StartHour = table.Column<int>(type: "INTEGER", nullable: false),
                    EndHour = table.Column<int>(type: "INTEGER", nullable: false),
                    Rates_BaseFare = table.Column<decimal>(type: "TEXT", nullable: false),
                    Rates_PricePerKm = table.Column<decimal>(type: "TEXT", nullable: false),
                    Rates_PricePerMinute = table.Column<decimal>(type: "TEXT", nullable: false),
                    Rates_MinimumFare = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tariffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tariffs_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Taxis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CityId = table.Column<int>(type: "INTEGER", nullable: false),
                    DriverId = table.Column<int>(type: "INTEGER", nullable: false),
                    LicensePlate = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    VehicleDescription = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApiKeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Taxis_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Taxis_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CityId = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxiId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PickupName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PickupLat = table.Column<double>(type: "REAL", nullable: false),
                    PickupLng = table.Column<double>(type: "REAL", nullable: false),
                    DestinationName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    DestinationLat = table.Column<double>(type: "REAL", nullable: false),
                    DestinationLng = table.Column<double>(type: "REAL", nullable: false),
                    PlannedRoute = table.Column<string>(type: "TEXT", nullable: false),
                    PlannedDistanceMeters = table.Column<double>(type: "REAL", nullable: false),
                    PlannedDurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    RouteSource = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TariffName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Rates_BaseFare = table.Column<decimal>(type: "TEXT", nullable: false),
                    Rates_PricePerKm = table.Column<decimal>(type: "TEXT", nullable: false),
                    Rates_PricePerMinute = table.Column<decimal>(type: "TEXT", nullable: false),
                    Rates_MinimumFare = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    EstimatedPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    EstimatedPriceHigh = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrentLat = table.Column<double>(type: "REAL", nullable: true),
                    CurrentLng = table.Column<double>(type: "REAL", nullable: true),
                    LastPositionAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DrivenDistanceMeters = table.Column<double>(type: "REAL", nullable: false),
                    ElapsedSeconds = table.Column<double>(type: "REAL", nullable: false),
                    ProgressAlongRouteMeters = table.Column<double>(type: "REAL", nullable: false),
                    RemainingRoute = table.Column<string>(type: "TEXT", nullable: false),
                    RemainingDistanceMeters = table.Column<double>(type: "REAL", nullable: false),
                    RemainingDurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    RemainingRouteCalculatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CurrentPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    EstimatedFinalPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsOffRoute = table.Column<bool>(type: "INTEGER", nullable: false),
                    PriceAlertSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    FinalPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Taxis_TaxiId",
                        column: x => x.TaxiId,
                        principalTable: "Taxis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TripAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TripId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripAlerts_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TripId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReportedOvercharge = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripFeedback_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripPositions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TripId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Lat = table.Column<double>(type: "REAL", nullable: false),
                    Lng = table.Column<double>(type: "REAL", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DistanceFromRouteMeters = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripPositions_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripSimulations",
                columns: table => new
                {
                    TripId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    PathLengthMeters = table.Column<double>(type: "REAL", nullable: false),
                    DistanceTravelledMeters = table.Column<double>(type: "REAL", nullable: false),
                    SpeedMetersPerSecond = table.Column<double>(type: "REAL", nullable: false),
                    WithDetour = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSimulations", x => x.TripId);
                    table.ForeignKey(
                        name: "FK_TripSimulations_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Places_CityId",
                table: "Places",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Tariffs_CityId",
                table: "Tariffs",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Taxis_CityId",
                table: "Taxis",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Taxis_DriverId",
                table: "Taxis",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Taxis_LicensePlate",
                table: "Taxis",
                column: "LicensePlate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripAlerts_TripId",
                table: "TripAlerts",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripFeedback_TripId",
                table: "TripFeedback",
                column: "TripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripPositions_TripId_RecordedAt",
                table: "TripPositions",
                columns: new[] { "TripId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_CityId",
                table: "Trips",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_Status",
                table: "Trips",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TaxiId",
                table: "Trips",
                column: "TaxiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Places");

            migrationBuilder.DropTable(
                name: "Tariffs");

            migrationBuilder.DropTable(
                name: "TripAlerts");

            migrationBuilder.DropTable(
                name: "TripFeedback");

            migrationBuilder.DropTable(
                name: "TripPositions");

            migrationBuilder.DropTable(
                name: "TripSimulations");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Taxis");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "Drivers");
        }
    }
}
