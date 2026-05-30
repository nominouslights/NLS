using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PrimaryContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrimaryContactTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    StreetAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Province = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    GstHstNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PreferredPaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NetPaymentTerms = table.Column<int>(type: "integer", nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ComplianceNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsMinesite = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Industry = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProjectSite = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_calendar_blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_calendar_blocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_file_blobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileData = table.Column<byte[]>(type: "bytea", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_file_blobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainEventLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainEventLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    HireDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "saved_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurchaseOrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VehicleType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeatCapacity = table.Column<int>(type: "integer", nullable: true),
                    PricePerSeat = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RefreshTokenExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VIN = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    Make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LicensePlate = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Province = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PassengerCapacity = table.Column<int>(type: "integer", nullable: false),
                    CurrentOdometerKm = table.Column<int>(type: "integer", nullable: false),
                    AcquisitionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistrationExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InsuranceProvider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InsurancePolicyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InsuranceExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RenewalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contracts_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "driver_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TestDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TestResultValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LicenseClass = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IssuedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LicenseProvince = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CheckResultValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IssuingAuthority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ViolationCount = table.Column<int>(type: "integer", nullable: true),
                    AtFaultAccidentCount = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_driver_documents_drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "driver_roster_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShiftStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ShiftEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_roster_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_driver_roster_entries_drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_passengers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactInfo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SeatNumber = table.Column<int>(type: "integer", nullable: true),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BookingReference = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CutoffDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BookedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Fare = table.Column<decimal>(type: "numeric(10,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_passengers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_passengers_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_post_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    OdometerStart = table.Column<int>(type: "integer", nullable: false),
                    OdometerEnd = table.Column<int>(type: "integer", nullable: false),
                    FuelAddedLitres = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    FuelCostDollars = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    HasIncident = table.Column<bool>(type: "boolean", nullable: false),
                    IncidentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IncidentDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AdditionalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsReadyToInvoice = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_post_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_post_reports_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_pre_inspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    OdometerStart = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_pre_inspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_pre_inspections_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_stops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    LocationName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_stops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_stops_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_inspection_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InspectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InspectorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InspectionFacility = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InspectionResult = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DeficienciesNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrectiveActionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CostDollars = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_inspection_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_inspection_records_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_service_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceCategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FluidType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsPlanned = table.Column<bool>(type: "boolean", nullable: false),
                    ServiceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OdometerAtService = table.Column<int>(type: "integer", nullable: true),
                    EstimatedCostDollars = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ActualCostDollars = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ServiceProvider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TechnicianName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PartsNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsWarrantyWork = table.Column<bool>(type: "boolean", nullable: false),
                    NextServiceDueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextServiceDueOdometerKm = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_service_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_service_records_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_rate_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxDistanceKm = table.Column<int>(type: "integer", nullable: true),
                    CargoIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    DayRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_rate_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_rate_lines_contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_inspection_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_inspection_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_inspection_items_trip_pre_inspections_PreInspectionId",
                        column: x => x.PreInspectionId,
                        principalTable: "trip_pre_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_calendar_blocks_BlockedDate",
                table: "community_calendar_blocks",
                column: "BlockedDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_rate_lines_ContractId_BillingCode",
                table: "contract_rate_lines",
                columns: new[] { "ContractId", "BillingCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_ClientId_IsActive",
                table: "contracts",
                columns: new[] { "ClientId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_document_file_blobs_StorageKey",
                table: "document_file_blobs",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainEventLog_AggregateId",
                table: "DomainEventLog",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainEventLog_OccurredOn",
                table: "DomainEventLog",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_driver_documents_DriverId_DocumentType",
                table: "driver_documents",
                columns: new[] { "DriverId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_driver_roster_entries_DriverId_EntryDate",
                table: "driver_roster_entries",
                columns: new[] { "DriverId", "EntryDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_EmployeeId",
                table: "drivers",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_locations_Name",
                table: "saved_locations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_trip_inspection_items_PreInspectionId",
                table: "trip_inspection_items",
                column: "PreInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_BookingReference",
                table: "trip_passengers",
                column: "BookingReference",
                unique: true,
                filter: "\"BookingReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_TripId",
                table: "trip_passengers",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_trip_post_reports_TripId",
                table: "trip_post_reports",
                column: "TripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_pre_inspections_TripId",
                table: "trip_pre_inspections",
                column: "TripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_stops_TripId_SequenceOrder",
                table: "trip_stops",
                columns: new[] { "TripId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trips_ClientId",
                table: "trips",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_trips_DriverId_Status",
                table: "trips",
                columns: new[] { "DriverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_trips_ServiceType",
                table: "trips",
                column: "ServiceType");

            migrationBuilder.CreateIndex(
                name: "IX_trips_Status",
                table: "trips",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_inspection_records_VehicleId",
                table: "vehicle_inspection_records",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_inspection_records_VehicleId_InspectionType",
                table: "vehicle_inspection_records",
                columns: new[] { "VehicleId", "InspectionType" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_service_records_VehicleId",
                table: "vehicle_service_records",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_service_records_VehicleId_ServiceCategory",
                table: "vehicle_service_records",
                columns: new[] { "VehicleId", "ServiceCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_LicensePlate",
                table: "vehicles",
                column: "LicensePlate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_UnitCode",
                table: "vehicles",
                column: "UnitCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_VIN",
                table: "vehicles",
                column: "VIN",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_calendar_blocks");

            migrationBuilder.DropTable(
                name: "contract_rate_lines");

            migrationBuilder.DropTable(
                name: "document_file_blobs");

            migrationBuilder.DropTable(
                name: "DomainEventLog");

            migrationBuilder.DropTable(
                name: "driver_documents");

            migrationBuilder.DropTable(
                name: "driver_roster_entries");

            migrationBuilder.DropTable(
                name: "saved_locations");

            migrationBuilder.DropTable(
                name: "trip_inspection_items");

            migrationBuilder.DropTable(
                name: "trip_passengers");

            migrationBuilder.DropTable(
                name: "trip_post_reports");

            migrationBuilder.DropTable(
                name: "trip_stops");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "vehicle_inspection_records");

            migrationBuilder.DropTable(
                name: "vehicle_service_records");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropTable(
                name: "trip_pre_inspections");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "trips");
        }
    }
}
