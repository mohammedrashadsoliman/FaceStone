using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseBatchesAndLaboratoryIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "StorageOperationItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionReferenceNumber",
                table: "StorageOperationItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleCode",
                table: "StorageOperationItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseBatchId",
                table: "StorageOperationItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "StorageInventoryLedger",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseBatchId",
                table: "StorageInventoryLedger",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "ProductionWorkInProcess",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleCode",
                table: "ProductionWorkInProcess",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseBatchId",
                table: "ProductionWorkInProcess",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WarehouseBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LaboratoryReportId = table.Column<int>(type: "int", nullable: false),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: false),
                    SampleCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RawMaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    MaterialClassification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "MECHANICAL"),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StorageLocationId = table.Column<int>(type: "int", nullable: false),
                    InitialQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CurrentQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    StorageOperationId = table.Column<int>(type: "int", nullable: true),
                    StorageOperationItemId = table.Column<int>(type: "int", nullable: true),
                    ImportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseBatches_LaboratoryRawMaterialReports_LaboratoryReportId",
                        column: x => x.LaboratoryReportId,
                        principalTable: "LaboratoryRawMaterialReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseBatches_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseBatches_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseBatches_StorageOperationItems_StorageOperationItemId",
                        column: x => x.StorageOperationItemId,
                        principalTable: "StorageOperationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseBatches_StorageOperations_StorageOperationId",
                        column: x => x.StorageOperationId,
                        principalTable: "StorageOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationItems_BatchNumber",
                table: "StorageOperationItems",
                column: "BatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationItems_WarehouseBatchId",
                table: "StorageOperationItems",
                column: "WarehouseBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageInventoryLedger_WarehouseBatchId",
                table: "StorageInventoryLedger",
                column: "WarehouseBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkInProcess_WarehouseBatchId",
                table: "ProductionWorkInProcess",
                column: "WarehouseBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_BatchNumber",
                table: "WarehouseBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_LaboratoryReportId",
                table: "WarehouseBatches",
                column: "LaboratoryReportId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ProductionReferenceNumber",
                table: "WarehouseBatches",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_RawMaterialId",
                table: "WarehouseBatches",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_SampleCode",
                table: "WarehouseBatches",
                column: "SampleCode");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_Status",
                table: "WarehouseBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_StorageLocationId",
                table: "WarehouseBatches",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_StorageOperationId",
                table: "WarehouseBatches",
                column: "StorageOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_StorageOperationItemId",
                table: "WarehouseBatches",
                column: "StorageOperationItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionWorkInProcess_WarehouseBatches_WarehouseBatchId",
                table: "ProductionWorkInProcess",
                column: "WarehouseBatchId",
                principalTable: "WarehouseBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageInventoryLedger_WarehouseBatches_WarehouseBatchId",
                table: "StorageInventoryLedger",
                column: "WarehouseBatchId",
                principalTable: "WarehouseBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageOperationItems_WarehouseBatches_WarehouseBatchId",
                table: "StorageOperationItems",
                column: "WarehouseBatchId",
                principalTable: "WarehouseBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionWorkInProcess_WarehouseBatches_WarehouseBatchId",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageInventoryLedger_WarehouseBatches_WarehouseBatchId",
                table: "StorageInventoryLedger");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageOperationItems_WarehouseBatches_WarehouseBatchId",
                table: "StorageOperationItems");

            migrationBuilder.DropTable(
                name: "WarehouseBatches");

            migrationBuilder.DropIndex(
                name: "IX_StorageOperationItems_BatchNumber",
                table: "StorageOperationItems");

            migrationBuilder.DropIndex(
                name: "IX_StorageOperationItems_WarehouseBatchId",
                table: "StorageOperationItems");

            migrationBuilder.DropIndex(
                name: "IX_StorageInventoryLedger_WarehouseBatchId",
                table: "StorageInventoryLedger");

            migrationBuilder.DropIndex(
                name: "IX_ProductionWorkInProcess_WarehouseBatchId",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "StorageOperationItems");

            migrationBuilder.DropColumn(
                name: "ProductionReferenceNumber",
                table: "StorageOperationItems");

            migrationBuilder.DropColumn(
                name: "SampleCode",
                table: "StorageOperationItems");

            migrationBuilder.DropColumn(
                name: "WarehouseBatchId",
                table: "StorageOperationItems");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "StorageInventoryLedger");

            migrationBuilder.DropColumn(
                name: "WarehouseBatchId",
                table: "StorageInventoryLedger");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "SampleCode",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "WarehouseBatchId",
                table: "ProductionWorkInProcess");
        }
    }
}
