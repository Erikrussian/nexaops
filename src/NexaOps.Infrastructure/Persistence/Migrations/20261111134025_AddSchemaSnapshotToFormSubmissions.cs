using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemaSnapshotToFormSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "submission_approval_steps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEscalated",
                table: "submission_approval_steps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitHours",
                table: "submission_approval_steps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FormVersion",
                table: "form_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "SchemaSnapshotJson",
                table: "form_submissions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "submission_approval_steps");

            migrationBuilder.DropColumn(
                name: "IsEscalated",
                table: "submission_approval_steps");

            migrationBuilder.DropColumn(
                name: "TimeLimitHours",
                table: "submission_approval_steps");

            migrationBuilder.DropColumn(
                name: "FormVersion",
                table: "form_submissions");

            migrationBuilder.DropColumn(
                name: "SchemaSnapshotJson",
                table: "form_submissions");
        }
    }
}
