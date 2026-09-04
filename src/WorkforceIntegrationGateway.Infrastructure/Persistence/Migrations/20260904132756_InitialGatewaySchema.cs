using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkforceIntegrationGateway.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialGatewaySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gateway");

            migrationBuilder.CreateTable(
                name: "verification_requests",
                schema: "gateway",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    employee_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    employer_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_requests", x => x.id);
                    table.CheckConstraint("ck_verification_requests_client_reference", "client_reference ~ '^[A-Za-z0-9._-]{1,100}$'");
                    table.CheckConstraint("ck_verification_requests_employee_reference", "employee_reference ~ '^[A-Za-z0-9._-]{1,100}$'");
                    table.CheckConstraint("ck_verification_requests_employer_reference", "employer_reference ~ '^[A-Za-z0-9._-]{1,100}$'");
                    table.CheckConstraint("ck_verification_requests_status", "status = 'Pending'");
                });

            migrationBuilder.CreateTable(
                name: "verification_request_data",
                schema: "gateway",
                columns: table => new
                {
                    verification_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_request_data", x => new { x.verification_request_id, x.value });
                    table.CheckConstraint("ck_verification_request_data_ordinal", "ordinal BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_verification_request_data_value", "value IN ('EmploymentStatus', 'JobTitle', 'EmploymentDates')");
                    table.ForeignKey(
                        name: "FK_verification_request_data_verification_requests_verificatio~",
                        column: x => x.verification_request_id,
                        principalSchema: "gateway",
                        principalTable: "verification_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_verification_request_data_verification_request_id_ordinal",
                schema: "gateway",
                table: "verification_request_data",
                columns: new[] { "verification_request_id", "ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "verification_request_data",
                schema: "gateway");

            migrationBuilder.DropTable(
                name: "verification_requests",
                schema: "gateway");
        }
    }
}
