using System;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// BR-PLT-140: job_runs needs an idempotency key per schedule window so a duplicate enqueue is a
    /// no-op. Additive and forward-only: existing rows (none outside development) get a unique key
    /// derived from their id.
    /// </summary>
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20261101090000_AddJobRunKey")]
    public partial class AddJobRunKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "run_key",
                schema: "platform",
                table: "job_runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trigger",
                schema: "platform",
                table: "job_runs",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE platform.job_runs
                SET run_key = job || ':' || tenant_id::text || ':legacy:' || id::text
                WHERE run_key IS NULL;
                UPDATE platform.job_runs SET trigger = 'schedule' WHERE trigger IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "run_key",
                schema: "platform",
                table: "job_runs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "trigger",
                schema: "platform",
                table: "job_runs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_job_runs_tenant_job_run_key",
                schema: "platform",
                table: "job_runs",
                columns: new[] { "tenant_id", "job", "run_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_job_runs_tenant_job_run_key",
                schema: "platform",
                table: "job_runs");

            migrationBuilder.DropColumn(name: "run_key", schema: "platform", table: "job_runs");
            migrationBuilder.DropColumn(name: "trigger", schema: "platform", table: "job_runs");
        }
    }
}
