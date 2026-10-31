using System;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20261030204500_AddJobRuns")]
    public partial class AddJobRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_runs",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job = table.Column<string>(type: "text", nullable: false),
                    queue = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_runs", x => x.id);
                    table.CheckConstraint("ck_job_runs_status", "status in ('running','succeeded','failed','timed_out','skipped')");
                    table.CheckConstraint("ck_job_runs_queue", "queue in ('critical','default','reports','collectors','ai')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_tenant_job_started",
                schema: "platform",
                table: "job_runs",
                columns: new[] { "tenant_id", "job", "started_at" });

            migrationBuilder.Sql("""
                ALTER TABLE platform.job_runs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE platform.job_runs FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON platform.job_runs
                  USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
                  WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_runs",
                schema: "platform");
        }
    }
}
