using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Avers.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mechanics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Position = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mechanics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Branch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Phones = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeadName = table.Column<string>(type: "text", nullable: false),
                    HeadPhone = table.Column<string>(type: "text", nullable: false),
                    EngineerName = table.Column<string>(type: "text", nullable: false),
                    EngineerPhone = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Streets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Streets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Login = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false),
                    CanEditAccount = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Houses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StreetId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PzhreuIndex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PzhreuAccount = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Floors = table.Column<int>(type: "integer", nullable: false),
                    Entrances = table.Column<int>(type: "integer", nullable: false),
                    Risers = table.Column<int>(type: "integer", nullable: false),
                    Apartments = table.Column<int>(type: "integer", nullable: false),
                    Zone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Serviced = table.Column<bool>(type: "boolean", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true),
                    Antennas = table.Column<string>(type: "text", nullable: false),
                    AerialCables = table.Column<string>(type: "text", nullable: false),
                    Amplifiers = table.Column<string>(type: "text", nullable: false),
                    PowerSupplies = table.Column<string>(type: "text", nullable: false),
                    TechFloorKey = table.Column<string>(type: "text", nullable: false),
                    OtherNotes = table.Column<string>(type: "text", nullable: false),
                    LastPpr = table.Column<DateOnly>(type: "date", nullable: true),
                    OpenFault = table.Column<string>(type: "text", nullable: true),
                    OpenFaultReason = table.Column<string>(type: "text", nullable: true),
                    OpenFaultPlannedFix = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Houses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Houses_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Houses_Streets_StreetId",
                        column: x => x.StreetId,
                        principalTable: "Streets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseSchemes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseSchemes_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntercomPanels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseId = table.Column<int>(type: "integer", nullable: false),
                    Entrance = table.Column<int>(type: "integer", nullable: false),
                    PanelType = table.Column<string>(type: "text", nullable: false),
                    SwitchBlock = table.Column<string>(type: "text", nullable: false),
                    Keys = table.Column<string>(type: "text", nullable: false),
                    Power = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntercomPanels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntercomPanels_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntercomTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Entrance = table.Column<int>(type: "integer", nullable: false),
                    Fault = table.Column<string>(type: "text", nullable: false),
                    Mechanic = table.Column<string>(type: "text", nullable: false),
                    Done = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntercomTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntercomTickets_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PprEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PprEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PprEntries_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscribers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseId = table.Column<int>(type: "integer", nullable: false),
                    InnerCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Account = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Patronymic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Apartment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Section = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ContractDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractTerminated = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ContractHolder = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    PzhreuIndex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PzhreuAccount = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SettlementFrom = table.Column<int>(type: "integer", nullable: false),
                    LastPaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PrintAccountWithPayDate = table.Column<bool>(type: "boolean", nullable: false),
                    Benefit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscribers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscribers_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourtCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriberId = table.Column<int>(type: "integer", nullable: false),
                    DelaAversId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Court = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourtCases_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriberId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Tariff = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    PaidAt = table.Column<DateOnly>(type: "date", nullable: true),
                    RecordedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PayerName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    HouseId = table.Column<int>(type: "integer", nullable: false),
                    SubscriberId = table.Column<int>(type: "integer", nullable: true),
                    Apartment = table.Column<string>(type: "text", nullable: false),
                    Section = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Fault = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WorkDone = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Mechanic = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AcceptedBy = table.Column<string>(type: "text", nullable: false),
                    AcceptedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DoneDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RisersNote = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_Houses_HouseId",
                        column: x => x.HouseId,
                        principalTable: "Houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tickets_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tickets_Users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UnifiedReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriberId = table.Column<int>(type: "integer", nullable: false),
                    InnerCode = table.Column<string>(type: "text", nullable: false),
                    PzhreuIndex = table.Column<string>(type: "text", nullable: false),
                    Account = table.Column<string>(type: "text", nullable: false),
                    Moment = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Paid = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    NOs = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NKass = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NOper = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NKvit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnifiedReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnifiedReceipts_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourtCases_SubscriberId",
                table: "CourtCases",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_OrganizationId",
                table: "Houses",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_PzhreuAccount",
                table: "Houses",
                column: "PzhreuAccount");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_StreetId_Number",
                table: "Houses",
                columns: new[] { "StreetId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseSchemes_HouseId",
                table: "HouseSchemes",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_IntercomPanels_HouseId",
                table: "IntercomPanels",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_IntercomTickets_HouseId",
                table: "IntercomTickets",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SubscriberId_Year_Month",
                table: "Payments",
                columns: new[] { "SubscriberId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PprEntries_HouseId",
                table: "PprEntries",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Streets_Name",
                table: "Streets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_Account",
                table: "Subscribers",
                column: "Account",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_HouseId",
                table: "Subscribers",
                column: "HouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_InnerCode",
                table: "Subscribers",
                column: "InnerCode");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_LastName",
                table: "Subscribers",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AcceptedByUserId",
                table: "Tickets",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Date",
                table: "Tickets",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_HouseId_Date",
                table: "Tickets",
                columns: new[] { "HouseId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Number",
                table: "Tickets",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SubscriberId",
                table: "Tickets",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_UnifiedReceipts_SubscriberId",
                table: "UnifiedReceipts",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Login",
                table: "Users",
                column: "Login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourtCases");

            migrationBuilder.DropTable(
                name: "HouseSchemes");

            migrationBuilder.DropTable(
                name: "IntercomPanels");

            migrationBuilder.DropTable(
                name: "IntercomTickets");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "Mechanics");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PprEntries");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "UnifiedReceipts");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Subscribers");

            migrationBuilder.DropTable(
                name: "Houses");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "Streets");
        }
    }
}
