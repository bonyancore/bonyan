using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxEnvelopeMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "actor_id",
                schema: "platform",
                table: "outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "causation_id",
                schema: "platform",
                table: "outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "correlation_id",
                schema: "platform",
                table: "outbox",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                schema: "platform",
                table: "outbox",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actor_id",
                schema: "platform",
                table: "outbox");

            migrationBuilder.DropColumn(
                name: "causation_id",
                schema: "platform",
                table: "outbox");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                schema: "platform",
                table: "outbox");

            migrationBuilder.DropColumn(
                name: "project_id",
                schema: "platform",
                table: "outbox");
        }
    }
}
