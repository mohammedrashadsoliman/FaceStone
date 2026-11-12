using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLaboratorySampleAndMaterialClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaterialClassification",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "MECHANICAL");

            migrationBuilder.AddColumn<string>(
                name: "QuantityConfirmationFileName",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QuantityConfirmationFileSize",
                table: "LaboratoryRawMaterialReports",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "QuantityConfirmationFileType",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuantityConfirmationPath",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleCode",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TestedMaterialWeight",
                table: "LaboratoryRawMaterialReports",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SampleCode",
                table: "LaboratoryInProcessReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SampleNumber",
                table: "LaboratoryInProcessReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleCode",
                table: "LaboratoryFinalConformityCertificates",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SampleNumber",
                table: "LaboratoryFinalConformityCertificates",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_MaterialClassification",
                table: "LaboratoryRawMaterialReports",
                column: "MaterialClassification");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_SampleCode",
                table: "LaboratoryRawMaterialReports",
                column: "SampleCode",
                unique: true,
                filter: "[SampleCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LaboratoryRawMaterialReports_MaterialClassification",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropIndex(
                name: "IX_LaboratoryRawMaterialReports_SampleCode",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "MaterialClassification",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "QuantityConfirmationFileName",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "QuantityConfirmationFileSize",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "QuantityConfirmationFileType",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "QuantityConfirmationPath",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "SampleCode",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "TestedMaterialWeight",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "SampleCode",
                table: "LaboratoryInProcessReports");

            migrationBuilder.DropColumn(
                name: "SampleNumber",
                table: "LaboratoryInProcessReports");

            migrationBuilder.DropColumn(
                name: "SampleCode",
                table: "LaboratoryFinalConformityCertificates");

            migrationBuilder.DropColumn(
                name: "SampleNumber",
                table: "LaboratoryFinalConformityCertificates");
        }
    }
}
