using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: true),
                    name_en = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    plan_code = table.Column<string>(type: "text", nullable: false),
                    base_currency = table.Column<string>(type: "char(3)", nullable: false),
                    time_zone = table.Column<string>(type: "text", nullable: false),
                    default_locale = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "char(2)", nullable: false),
                    permissions_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                    table.CheckConstraint("ck_tenants_code", "code ~ '^[a-z0-9-]{3,40}$'");
                    table.CheckConstraint("ck_tenants_name", "name_ar is not null or name_en is not null");
                    table.CheckConstraint("ck_tenants_plan", "plan_code in ('starter','growth','enterprise')");
                    table.CheckConstraint("ck_tenants_status", "status in ('provisioning','active','trial','suspended','pending_deletion','deleted')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenants_code",
                schema: "platform",
                table: "tenants",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_plan_code",
                schema: "platform",
                table: "tenants",
                column: "plan_code");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_status",
                schema: "platform",
                table: "tenants",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenants",
                schema: "platform");
        }
    }
}
