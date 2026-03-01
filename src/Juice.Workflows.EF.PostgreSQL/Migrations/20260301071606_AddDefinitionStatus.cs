using System;
using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddDefinitionStatus : Migration
    {
        private string _schema;

        public AddDefinitionStatus() { }

        public AddDefinitionStatus(ISchemaDbContext schema)
        {
            _schema = schema.Schema;
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: _schema,
                table: "WorkflowDefinition",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                schema: _schema,
                table: "WorkflowDefinition");
        }
    }
}
