using System;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Procurement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitProcurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "procurement");

            migrationBuilder.CreateTable(
                name: "purchase_requests",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_requests", x => x.id);
                    table.UniqueConstraint("ak_purchase_requests_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_purchase_requests_status", "status in ('draft', 'submitted', 'approved', 'rejected', 'cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "purchase_request_items",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_request_items", x => x.id);
                    table.CheckConstraint("ck_purchase_request_items_quantity_amount", "quantity_amount > 0");
                    table.CheckConstraint("ck_purchase_request_items_quantity_unit", "length(trim(quantity_unit)) between 1 and 20");
                    table.ForeignKey(
                        name: "fk_purchase_request_items_purchase_requests_tenant_id_purchase",
                        columns: x => new { x.tenant_id, x.purchase_request_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_requests",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_request_items_tenant_id_purchase_request_id",
                schema: "procurement",
                table: "purchase_request_items",
                columns: new[] { "tenant_id", "purchase_request_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_requests_tenant_project_created_at",
                schema: "procurement",
                table: "purchase_requests",
                columns: new[] { "tenant_id", "project_id", "created_at" });

            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "purchase_requests");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "purchase_request_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_request_items",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_requests",
                schema: "procurement");
        }
    }
}
