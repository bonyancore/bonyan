using System;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Procurement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "approval_reference",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_number",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_event_id",
                schema: "procurement",
                table: "purchase_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "work_package_code",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "work_package_id",
                schema: "procurement",
                table: "purchase_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "work_package_name",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_purchase_request_items_tenant_id_id",
                schema: "procurement",
                table: "purchase_request_items",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_budgets",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    limit_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    reserved_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_budgets", x => x.id);
                    table.UniqueConstraint("ak_project_budgets_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_project_budgets_limit_amount", "limit_amount >= 0 and reserved_amount >= 0 and reserved_amount <= limit_amount");
                });

            migrationBuilder.CreateTable(
                name: "requests_for_quotation",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    awarded_supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requests_for_quotation", x => x.id);
                    table.UniqueConstraint("ak_requests_for_quotation_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_requests_for_quotation_status", "status in ('draft','issued','awarded','closed','cancelled')");
                    table.ForeignKey(
                        name: "fk_requests_for_quotation_purchase_requests_tenant_id_purchase",
                        columns: x => new { x.tenant_id, x.purchase_request_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_requests",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.id);
                    table.UniqueConstraint("ak_suppliers_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_suppliers_status", "status in ('active', 'inactive')");
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    approval_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    budget_reserved_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_orders", x => x.id);
                    table.UniqueConstraint("ak_purchase_orders_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_purchase_orders_status", "status in ('draft','pendingapproval','approved','rejected','issued','cancelled')");
                    table.ForeignKey(
                        name: "fk_purchase_orders_purchase_requests_tenant_id_purchase_reques",
                        columns: x => new { x.tenant_id, x.purchase_request_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_requests",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_orders_suppliers_tenant_id_supplier_id",
                        columns: x => new { x.tenant_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rfq_invitations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_for_quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rfq_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_rfq_invitations_requests_for_quotation_tenant_id_request_fo",
                        columns: x => new { x.tenant_id, x.request_for_quotation_id },
                        principalSchema: "procurement",
                        principalTable: "requests_for_quotation",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rfq_invitations_suppliers_tenant_id_supplier_id",
                        columns: x => new { x.tenant_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_quotes",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_for_quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_quotes", x => x.id);
                    table.UniqueConstraint("ak_supplier_quotes_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_supplier_quotes_requests_for_quotation_tenant_id_request_fo",
                        columns: x => new { x.tenant_id, x.request_for_quotation_id },
                        principalSchema: "procurement",
                        principalTable: "requests_for_quotation",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_supplier_quotes_suppliers_tenant_id_supplier_id",
                        columns: x => new { x.tenant_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reversed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_receipts", x => x.id);
                    table.UniqueConstraint("ak_goods_receipts_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_goods_receipts_purchase_orders_tenant_id_purchase_order_id",
                        columns: x => new { x.tenant_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_order_lines", x => x.id);
                    table.UniqueConstraint("ak_purchase_order_line_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_purchase_order_lines_quantity", "quantity_amount > 0");
                    table.CheckConstraint("ck_purchase_order_lines_unit_price", "unit_price_amount >= 0");
                    table.ForeignKey(
                        name: "fk_purchase_order_lines_purchase_orders_tenant_id_purchase_ord",
                        columns: x => new { x.tenant_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_purchase_order_lines_purchase_request_items_tenant_id_purch",
                        columns: x => new { x.tenant_id, x.purchase_request_item_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_revisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    approval_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_order_revisions", x => x.id);
                    table.UniqueConstraint("ak_purchase_order_revisions_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_purchase_order_revisions_revision", "revision > 0");
                    table.CheckConstraint("ck_purchase_order_revisions_total", "total_amount >= 0");
                    table.ForeignKey(
                        name: "fk_purchase_order_revisions_purchase_orders_tenant_id_purchase",
                        columns: x => new { x.tenant_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoices",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    dispute_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_invoices", x => x.id);
                    table.UniqueConstraint("ak_supplier_invoices_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_supplier_invoices_status", "status in ('draft','matched','disputed','paymentscheduled','paid')");
                    table.ForeignKey(
                        name: "fk_supplier_invoices_purchase_orders_tenant_id_purchase_order_",
                        columns: x => new { x.tenant_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_invoices_suppliers_tenant_id_supplier_id",
                        columns: x => new { x.tenant_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_quote_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_quote_lines", x => x.id);
                    table.CheckConstraint("ck_supplier_quote_lines_quantity", "quantity_amount > 0");
                    table.CheckConstraint("ck_supplier_quote_lines_unit_price", "unit_price_amount >= 0");
                    table.ForeignKey(
                        name: "fk_supplier_quote_lines_purchase_request_items_tenant_id_purch",
                        columns: x => new { x.tenant_id, x.purchase_request_item_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_items",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_quote_lines_supplier_quotes_tenant_id_supplier_quo",
                        columns: x => new { x.tenant_id, x.supplier_quote_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_quotes",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goods_receipt_lines", x => x.id);
                    table.CheckConstraint("ck_goods_receipt_lines_quantity", "quantity_amount > 0");
                    table.ForeignKey(
                        name: "fk_goods_receipt_lines_goods_receipts_tenant_id_goods_receipt_",
                        columns: x => new { x.tenant_id, x.goods_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipts",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_goods_receipt_lines_purchase_order_line_tenant_id_purchase_",
                        columns: x => new { x.tenant_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_revision_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_order_revision_lines", x => x.id);
                    table.CheckConstraint("ck_purchase_order_revision_lines_price", "unit_price_amount >= 0");
                    table.CheckConstraint("ck_purchase_order_revision_lines_quantity", "quantity_amount > 0");
                    table.ForeignKey(
                        name: "fk_purchase_order_revision_lines_purchase_order_revisions_tena",
                        columns: x => new { x.tenant_id, x.purchase_order_revision_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_revisions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoice_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_invoice_lines", x => x.id);
                    table.CheckConstraint("ck_supplier_invoice_lines_quantity", "quantity_amount > 0");
                    table.CheckConstraint("ck_supplier_invoice_lines_unit_price", "unit_price_amount >= 0");
                    table.ForeignKey(
                        name: "fk_supplier_invoice_lines_purchase_order_lines_tenant_id_purch",
                        columns: x => new { x.tenant_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_invoice_lines_supplier_invoices_tenant_id_supplier",
                        columns: x => new { x.tenant_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_requests_tenant_id_request_number",
                schema: "procurement",
                table: "purchase_requests",
                columns: new[] { "tenant_id", "request_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_requests_tenant_id_source_event_id",
                schema: "procurement",
                table: "purchase_requests",
                columns: new[] { "tenant_id", "source_event_id" },
                unique: true,
                filter: "source_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_lines_tenant_id_goods_receipt_id_purchase_ord",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "goods_receipt_id", "purchase_order_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipt_lines_tenant_id_purchase_order_line_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_tenant_id_purchase_order_id_received_at",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "purchase_order_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_published_at_next_attempt_at",
                schema: "procurement",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "published_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_project_budgets_tenant_id_project_id",
                schema: "procurement",
                table: "project_budgets",
                columns: new[] { "tenant_id", "project_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_tenant_id_purchase_order_id",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_lines_tenant_id_purchase_request_item_id",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "purchase_request_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_revision_lines_tenant_id_purchase_order_revi",
                schema: "procurement",
                table: "purchase_order_revision_lines",
                columns: new[] { "tenant_id", "purchase_order_revision_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_revisions_tenant_id_purchase_order_id_revisi",
                schema: "procurement",
                table: "purchase_order_revisions",
                columns: new[] { "tenant_id", "purchase_order_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_tenant_id_number_revision",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "number", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_tenant_id_purchase_request_id",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "purchase_request_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_orders_tenant_id_supplier_id",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ix_requests_for_quotation_tenant_id_purchase_request_id",
                schema: "procurement",
                table: "requests_for_quotation",
                columns: new[] { "tenant_id", "purchase_request_id" });

            migrationBuilder.CreateIndex(
                name: "ix_rfq_invitations_tenant_id_request_for_quotation_id_supplier",
                schema: "procurement",
                table: "rfq_invitations",
                columns: new[] { "tenant_id", "request_for_quotation_id", "supplier_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rfq_invitations_tenant_id_supplier_id",
                schema: "procurement",
                table: "rfq_invitations",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoice_lines_tenant_id_purchase_order_line_id",
                schema: "procurement",
                table: "supplier_invoice_lines",
                columns: new[] { "tenant_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoice_lines_tenant_id_supplier_invoice_id_purcha",
                schema: "procurement",
                table: "supplier_invoice_lines",
                columns: new[] { "tenant_id", "supplier_invoice_id", "purchase_order_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoices_tenant_id_purchase_order_id",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_invoices_tenant_id_supplier_id_invoice_number",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "supplier_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_quote_lines_tenant_id_purchase_request_item_id",
                schema: "procurement",
                table: "supplier_quote_lines",
                columns: new[] { "tenant_id", "purchase_request_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_quote_lines_tenant_id_supplier_quote_id_purchase_r",
                schema: "procurement",
                table: "supplier_quote_lines",
                columns: new[] { "tenant_id", "supplier_quote_id", "purchase_request_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_quotes_tenant_id_request_for_quotation_id_supplier",
                schema: "procurement",
                table: "supplier_quotes",
                columns: new[] { "tenant_id", "request_for_quotation_id", "supplier_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_quotes_tenant_id_supplier_id",
                schema: "procurement",
                table: "supplier_quotes",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_tenant_id_core_party_id",
                schema: "procurement",
                table: "suppliers",
                columns: new[] { "tenant_id", "core_party_id" },
                unique: true);

            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "goods_receipts");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "goods_receipt_lines");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "outbox_messages");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "project_budgets");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "purchase_orders");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "purchase_order_lines");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "purchase_order_revisions");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "purchase_order_revision_lines");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "requests_for_quotation");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "rfq_invitations");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "supplier_invoices");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "supplier_invoice_lines");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "supplier_quotes");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "supplier_quote_lines");
            migrationBuilder.EnableTenantRowLevelSecurity("procurement", "suppliers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "goods_receipt_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "project_budgets",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_revision_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "rfq_invitations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_invoice_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_quote_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "goods_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_invoices",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_quotes",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "requests_for_quotation",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "ix_purchase_requests_tenant_id_request_number",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropIndex(
                name: "ix_purchase_requests_tenant_id_source_event_id",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_purchase_request_items_tenant_id_id",
                schema: "procurement",
                table: "purchase_request_items");

            migrationBuilder.DropColumn(
                name: "approval_reference",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "request_number",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "source_event_id",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "work_package_code",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "work_package_id",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "work_package_name",
                schema: "procurement",
                table: "purchase_requests");
        }
    }
}
