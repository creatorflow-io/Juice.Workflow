using Juice.EF;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juice.Workflows.EF.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowDefinitionStatus : Migration
    {
        private string _schema;

        public AddWorkflowDefinitionStatus() { }

        public AddWorkflowDefinitionStatus(ISchemaDbContext schema)
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
                type: "int",
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
