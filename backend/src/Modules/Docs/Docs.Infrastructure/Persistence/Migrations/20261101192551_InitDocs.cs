using System;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Docs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitDocs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "docs");

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "docs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    linked_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.UniqueConstraint("ak_documents_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_documents_record_type", "length(trim(record_type)) between 1 and 100");
                    table.CheckConstraint("ck_documents_title", "length(trim(title)) between 1 and 300");
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                schema: "docs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_versions", x => x.id);
                    table.CheckConstraint("ck_document_versions_status", "status in ('draft','approved','superseded')");
                    table.CheckConstraint("ck_document_versions_version_number", "version_number > 0");
                    table.ForeignKey(
                        name: "fk_document_versions_documents_tenant_id_document_record_id",
                        columns: x => new { x.tenant_id, x.document_record_id },
                        principalSchema: "docs",
                        principalTable: "documents",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_tenant_id_document_record_id_status",
                schema: "docs",
                table: "document_versions",
                columns: new[] { "tenant_id", "document_record_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_tenant_id_document_record_id_version_numb",
                schema: "docs",
                table: "document_versions",
                columns: new[] { "tenant_id", "document_record_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_tenant_id_record_type_linked_record_id",
                schema: "docs",
                table: "documents",
                columns: new[] { "tenant_id", "record_type", "linked_record_id" });

            migrationBuilder.EnableTenantRowLevelSecurity("docs", "documents");
            migrationBuilder.EnableTenantRowLevelSecurity("docs", "document_versions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_versions",
                schema: "docs");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "docs");
        }
    }
}
