using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class SynchronizeFaceStoneModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductionOrderId",
                table: "WarehouseBatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionProcessId",
                table: "WarehouseBatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "Transportation",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportTransactionNumber",
                table: "Transportation",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportType",
                table: "Transportation",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManufactureNumber",
                table: "Transportation",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionOrderId",
                table: "Transportation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionProcessId",
                table: "Transportation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectCodeId",
                table: "Transportation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectCodeValue",
                table: "Transportation",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleCode",
                table: "Transportation",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceStorageLocationId",
                table: "Transportation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceStorageLocationName",
                table: "Transportation",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageOperationId",
                table: "Transportation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageOperationItemId",
                table: "Transportation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportType",
                table: "StorageOperations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManufactureNumber",
                table: "StorageOperations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionOrderId",
                table: "StorageOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionProcessId",
                table: "StorageOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManufactureNumber",
                table: "StorageOperationItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionOrderId",
                table: "StorageOperationItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionProcessId",
                table: "StorageOperationItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveredQuantity",
                table: "PurchaseOrders",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDeliveryDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialName",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "PurchaseOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FormulaVersion",
                table: "ProductionOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LaboratoryManagerId",
                table: "ProductionOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaboratoryManagerName",
                table: "ProductionOrders",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedProductionProcessId",
                table: "ProductionOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedProductionProcessNumber",
                table: "ProductionOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductDescription",
                table: "ProductionOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionInstructions",
                table: "ProductionOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPlannedRawMaterialQuantity",
                table: "ProductionOrders",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalTransferredQuantity",
                table: "ProductionOrders",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AvailableQuantityBefore",
                table: "ProductionOrderItems",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "ProductionOrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialType",
                table: "ProductionOrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionReferenceNumber",
                table: "ProductionOrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleCode",
                table: "ProductionOrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageLocationName",
                table: "ProductionOrderItems",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseBatchId",
                table: "ProductionOrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChemicalInspectedAt",
                table: "LaboratoryRawMaterialReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChemicalInspectedBy",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChemicalInspectionStatus",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChemicalNotes",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChemicalResultsJson",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverallStatus",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhysicalInspectedAt",
                table: "LaboratoryRawMaterialReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalInspectedBy",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalInspectionStatus",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalNotes",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProductionOrderFormulaVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProductDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TargetOutputQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalInputQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    FormulaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderFormulaVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrderFormulaVersions_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrderTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductionOrderItemId = table.Column<int>(type: "int", nullable: false),
                    WarehouseBatchId = table.Column<int>(type: "int", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RawMaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: true),
                    StorageOperationId = table.Column<int>(type: "int", nullable: true),
                    TransferTransactionNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransferredQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    StorageLocationId = table.Column<int>(type: "int", nullable: false),
                    StorageLocationName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrderTransfers_ProductionOrderItems_ProductionOrderItemId",
                        column: x => x.ProductionOrderItemId,
                        principalTable: "ProductionOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrderTransfers_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionOrderTransfers_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrderTransfers_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrderTransfers_StorageOperations_StorageOperationId",
                        column: x => x.StorageOperationId,
                        principalTable: "StorageOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionOrderTransfers_WarehouseBatches_WarehouseBatchId",
                        column: x => x.WarehouseBatchId,
                        principalTable: "WarehouseBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionProcesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StorageLocationId = table.Column<int>(type: "int", nullable: true),
                    StorageLocationName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TotalInputQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalOutputQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalWasteQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WastePercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "IN_PROGRESS"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: true),
                    ProductionOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponsibleEmployee = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IncomingWeighbridgePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IncomingWeighbridgeFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IncomingWeighbridgeFileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IncomingWeighbridgeFileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionProcesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionProcesses_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionProcesses_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StorageClientDestinationId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCodes_StorageClientDestinations_StorageClientDestinationId",
                        column: x => x.StorageClientDestinationId,
                        principalTable: "StorageClientDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CommercialRegister = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryMaterialName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrimaryMaterialType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceDepartment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "FINANCE"),
                    RelatedLaboratoryReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RelatedWarehouseImportNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmountOwed = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    TotalAmountPaid = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    RemainingBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    LastPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionFinishedProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionProcessId = table.Column<int>(type: "int", nullable: false),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: true),
                    ProductionOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FinishedProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ManufactureNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ManufacturedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    DestinationStorageLocationId = table.Column<int>(type: "int", nullable: true),
                    DestinationStorageLocationName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WeighbridgeAttachmentPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WeighbridgeFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    WeighbridgeFileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WeighbridgeFileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionFinishedProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionFinishedProducts_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionFinishedProducts_ProductionProcesses_ProductionProcessId",
                        column: x => x.ProductionProcessId,
                        principalTable: "ProductionProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionFinishedProducts_StorageLocations_DestinationStorageLocationId",
                        column: x => x.DestinationStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductionProcessMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionProcessId = table.Column<int>(type: "int", nullable: false),
                    WarehouseBatchId = table.Column<int>(type: "int", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LaboratoryReportId = table.Column<int>(type: "int", nullable: true),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: false),
                    SampleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RawMaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaterialClassification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageLocationId = table.Column<int>(type: "int", nullable: false),
                    StorageLocationName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AvailableQuantityBefore = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RemainingQuantityAfter = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionProcessMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionProcessMaterials_ProductionProcesses_ProductionProcessId",
                        column: x => x.ProductionProcessId,
                        principalTable: "ProductionProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionProcessMaterials_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionProcessMaterials_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionProcessMaterials_WarehouseBatches_WarehouseBatchId",
                        column: x => x.WarehouseBatchId,
                        principalTable: "WarehouseBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionWasteRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionProcessId = table.Column<int>(type: "int", nullable: false),
                    TotalInputQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalOutputQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WasteQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WastePercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalculatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionWasteRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionWasteRecords_ProductionProcesses_ProductionProcessId",
                        column: x => x.ProductionProcessId,
                        principalTable: "ProductionProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectCodeId = table.Column<int>(type: "int", nullable: true),
                    ProjectCodeValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "COMPANY"),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    PONumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MaterialName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    InvoicedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    PoUnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    InvoiceUnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    PriceChangedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PriceChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CompanyTaxNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ShippingPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "ISSUED"),
                    IsPrinted = table.Column<bool>(type: "bit", nullable: false),
                    PrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_ProjectCodes_ProjectCodeId",
                        column: x => x.ProjectCodeId,
                        principalTable: "ProjectCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invoices_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invoices_StorageClients_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "StorageClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductionAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionProcessId = table.Column<int>(type: "int", nullable: false),
                    FinishedProductId = table.Column<int>(type: "int", nullable: true),
                    AttachmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionAttachments_ProductionFinishedProducts_FinishedProductId",
                        column: x => x.FinishedProductId,
                        principalTable: "ProductionFinishedProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionAttachments_ProductionProcesses_ProductionProcessId",
                        column: x => x.ProductionProcessId,
                        principalTable: "ProductionProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    TransportationId = table.Column<int>(type: "int", nullable: true),
                    TransportationTransactionNumber = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    PONumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WarehouseExportNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProductionOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Transportation_TransportationId",
                        column: x => x.TransportationId,
                        principalTable: "Transportation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupplierFinancialTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierFinancialTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierFinancialTransactions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierFinancialTransactions_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ProductionOrderId",
                table: "WarehouseBatches",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseBatches_ProductionProcessId",
                table: "WarehouseBatches",
                column: "ProductionProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Transportation_ProductionOrderId",
                table: "Transportation",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Transportation_ProductionProcessId",
                table: "Transportation",
                column: "ProductionProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Transportation_ProjectCodeId",
                table: "Transportation",
                column: "ProjectCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Transportation_StorageOperationId",
                table: "Transportation",
                column: "StorageOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Transportation_StorageOperationItemId",
                table: "Transportation",
                column: "StorageOperationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_ProductionOrderId",
                table: "StorageOperations",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_ProductionProcessId",
                table: "StorageOperations",
                column: "ProductionProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationItems_ProductionOrderId",
                table: "StorageOperationItems",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationItems_ProductionProcessId",
                table: "StorageOperationItems",
                column: "ProductionProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderItems_BatchNumber",
                table: "ProductionOrderItems",
                column: "BatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderItems_ProductionReferenceNumber",
                table: "ProductionOrderItems",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderItems_StorageLocationId",
                table: "ProductionOrderItems",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderItems_WarehouseBatchId",
                table: "ProductionOrderItems",
                column: "WarehouseBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_PurchaseOrderId",
                table: "InvoiceItems",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_TransportationId",
                table: "InvoiceItems",
                column: "TransportationId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId",
                table: "Invoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ProjectCodeId",
                table: "Invoices",
                column: "ProjectCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PurchaseOrderId",
                table: "Invoices",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SupplierId",
                table: "Invoices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionAttachments_FinishedProductId",
                table: "ProductionAttachments",
                column: "FinishedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionAttachments_ProductionProcessId",
                table: "ProductionAttachments",
                column: "ProductionProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionFinishedProducts_DestinationStorageLocationId",
                table: "ProductionFinishedProducts",
                column: "DestinationStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionFinishedProducts_ManufactureNumber",
                table: "ProductionFinishedProducts",
                column: "ManufactureNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionFinishedProducts_ProductionOrderId",
                table: "ProductionFinishedProducts",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionFinishedProducts_ProductionProcessId",
                table: "ProductionFinishedProducts",
                column: "ProductionProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderFormulaVersions_ProductionOrderId",
                table: "ProductionOrderFormulaVersions",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderFormulaVersions_ProductionOrderId_VersionNumber",
                table: "ProductionOrderFormulaVersions",
                columns: new[] { "ProductionOrderId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderTransfers_ProductionOrderId",
                table: "ProductionOrderTransfers",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderTransfers_ProductionOrderItemId",
                table: "ProductionOrderTransfers",
                column: "ProductionOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderTransfers_RawMaterialId",
                table: "ProductionOrderTransfers",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderTransfers_StorageLocationId",
                table: "ProductionOrderTransfers",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderTransfers_StorageOperationId",
                table: "ProductionOrderTransfers",
                column: "StorageOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderTransfers_TransferTransactionNumber",
                table: "ProductionOrderTransfers",
                column: "TransferTransactionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderTransfers_WarehouseBatchId",
                table: "ProductionOrderTransfers",
                column: "WarehouseBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcesses_ProcessNumber",
                table: "ProductionProcesses",
                column: "ProcessNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcesses_ProductionDate",
                table: "ProductionProcesses",
                column: "ProductionDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcesses_ProductionOrderId",
                table: "ProductionProcesses",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcesses_Status",
                table: "ProductionProcesses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcesses_StorageLocationId",
                table: "ProductionProcesses",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcessMaterials_BatchNumber",
                table: "ProductionProcessMaterials",
                column: "BatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcessMaterials_ProductionProcessId",
                table: "ProductionProcessMaterials",
                column: "ProductionProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcessMaterials_ProductionReferenceNumber",
                table: "ProductionProcessMaterials",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcessMaterials_RawMaterialId",
                table: "ProductionProcessMaterials",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcessMaterials_StorageLocationId",
                table: "ProductionProcessMaterials",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcessMaterials_WarehouseBatchId",
                table: "ProductionProcessMaterials",
                column: "WarehouseBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWasteRecords_ProductionProcessId",
                table: "ProductionWasteRecords",
                column: "ProductionProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCodes_Code",
                table: "ProjectCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCodes_StorageClientDestinationId",
                table: "ProjectCodes",
                column: "StorageClientDestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFinancialTransactions_InvoiceId",
                table: "SupplierFinancialTransactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFinancialTransactions_SupplierId",
                table: "SupplierFinancialTransactions",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFinancialTransactions_TransactionDate",
                table: "SupplierFinancialTransactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFinancialTransactions_TransactionType",
                table: "SupplierFinancialTransactions",
                column: "TransactionType");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_SupplierCode",
                table: "Suppliers",
                column: "SupplierCode");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrderItems_StorageLocations_StorageLocationId",
                table: "ProductionOrderItems",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrderItems_WarehouseBatches_WarehouseBatchId",
                table: "ProductionOrderItems",
                column: "WarehouseBatchId",
                principalTable: "WarehouseBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageOperationItems_ProductionOrders_ProductionOrderId",
                table: "StorageOperationItems",
                column: "ProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageOperationItems_ProductionProcesses_ProductionProcessId",
                table: "StorageOperationItems",
                column: "ProductionProcessId",
                principalTable: "ProductionProcesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageOperations_ProductionOrders_ProductionOrderId",
                table: "StorageOperations",
                column: "ProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageOperations_ProductionProcesses_ProductionProcessId",
                table: "StorageOperations",
                column: "ProductionProcessId",
                principalTable: "ProductionProcesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transportation_ProductionOrders_ProductionOrderId",
                table: "Transportation",
                column: "ProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transportation_ProductionProcesses_ProductionProcessId",
                table: "Transportation",
                column: "ProductionProcessId",
                principalTable: "ProductionProcesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transportation_ProjectCodes_ProjectCodeId",
                table: "Transportation",
                column: "ProjectCodeId",
                principalTable: "ProjectCodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transportation_StorageOperationItems_StorageOperationItemId",
                table: "Transportation",
                column: "StorageOperationItemId",
                principalTable: "StorageOperationItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transportation_StorageOperations_StorageOperationId",
                table: "Transportation",
                column: "StorageOperationId",
                principalTable: "StorageOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseBatches_ProductionOrders_ProductionOrderId",
                table: "WarehouseBatches",
                column: "ProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseBatches_ProductionProcesses_ProductionProcessId",
                table: "WarehouseBatches",
                column: "ProductionProcessId",
                principalTable: "ProductionProcesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrderItems_StorageLocations_StorageLocationId",
                table: "ProductionOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrderItems_WarehouseBatches_WarehouseBatchId",
                table: "ProductionOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageOperationItems_ProductionOrders_ProductionOrderId",
                table: "StorageOperationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageOperationItems_ProductionProcesses_ProductionProcessId",
                table: "StorageOperationItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageOperations_ProductionOrders_ProductionOrderId",
                table: "StorageOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageOperations_ProductionProcesses_ProductionProcessId",
                table: "StorageOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_Transportation_ProductionOrders_ProductionOrderId",
                table: "Transportation");

            migrationBuilder.DropForeignKey(
                name: "FK_Transportation_ProductionProcesses_ProductionProcessId",
                table: "Transportation");

            migrationBuilder.DropForeignKey(
                name: "FK_Transportation_ProjectCodes_ProjectCodeId",
                table: "Transportation");

            migrationBuilder.DropForeignKey(
                name: "FK_Transportation_StorageOperationItems_StorageOperationItemId",
                table: "Transportation");

            migrationBuilder.DropForeignKey(
                name: "FK_Transportation_StorageOperations_StorageOperationId",
                table: "Transportation");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseBatches_ProductionOrders_ProductionOrderId",
                table: "WarehouseBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseBatches_ProductionProcesses_ProductionProcessId",
                table: "WarehouseBatches");

            migrationBuilder.DropTable(
                name: "InvoiceItems");

            migrationBuilder.DropTable(
                name: "ProductionAttachments");

            migrationBuilder.DropTable(
                name: "ProductionOrderFormulaVersions");

            migrationBuilder.DropTable(
                name: "ProductionOrderTransfers");

            migrationBuilder.DropTable(
                name: "ProductionProcessMaterials");

            migrationBuilder.DropTable(
                name: "ProductionWasteRecords");

            migrationBuilder.DropTable(
                name: "SupplierFinancialTransactions");

            migrationBuilder.DropTable(
                name: "ProductionFinishedProducts");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "ProductionProcesses");

            migrationBuilder.DropTable(
                name: "ProjectCodes");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseBatches_ProductionOrderId",
                table: "WarehouseBatches");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseBatches_ProductionProcessId",
                table: "WarehouseBatches");

            migrationBuilder.DropIndex(
                name: "IX_Transportation_ProductionOrderId",
                table: "Transportation");

            migrationBuilder.DropIndex(
                name: "IX_Transportation_ProductionProcessId",
                table: "Transportation");

            migrationBuilder.DropIndex(
                name: "IX_Transportation_ProjectCodeId",
                table: "Transportation");

            migrationBuilder.DropIndex(
                name: "IX_Transportation_StorageOperationId",
                table: "Transportation");

            migrationBuilder.DropIndex(
                name: "IX_Transportation_StorageOperationItemId",
                table: "Transportation");

            migrationBuilder.DropIndex(
                name: "IX_StorageOperations_ProductionOrderId",
                table: "StorageOperations");

            migrationBuilder.DropIndex(
                name: "IX_StorageOperations_ProductionProcessId",
                table: "StorageOperations");

            migrationBuilder.DropIndex(
                name: "IX_StorageOperationItems_ProductionOrderId",
                table: "StorageOperationItems");

            migrationBuilder.DropIndex(
                name: "IX_StorageOperationItems_ProductionProcessId",
                table: "StorageOperationItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderItems_BatchNumber",
                table: "ProductionOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderItems_ProductionReferenceNumber",
                table: "ProductionOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderItems_StorageLocationId",
                table: "ProductionOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrderItems_WarehouseBatchId",
                table: "ProductionOrderItems");

            migrationBuilder.DropColumn(
                name: "ProductionOrderId",
                table: "WarehouseBatches");

            migrationBuilder.DropColumn(
                name: "ProductionProcessId",
                table: "WarehouseBatches");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExportTransactionNumber",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExportType",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ManufactureNumber",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ProductionOrderId",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ProductionProcessId",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ProjectCodeId",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ProjectCodeValue",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "SampleCode",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "SourceStorageLocationId",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "SourceStorageLocationName",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "StorageOperationId",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "StorageOperationItemId",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExportType",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "ManufactureNumber",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "ProductionOrderId",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "ProductionProcessId",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "ManufactureNumber",
                table: "StorageOperationItems");

            migrationBuilder.DropColumn(
                name: "ProductionOrderId",
                table: "StorageOperationItems");

            migrationBuilder.DropColumn(
                name: "ProductionProcessId",
                table: "StorageOperationItems");

            migrationBuilder.DropColumn(
                name: "DeliveredQuantity",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExpectedDeliveryDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "MaterialName",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "FormulaVersion",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "LaboratoryManagerId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "LaboratoryManagerName",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "LinkedProductionProcessId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "LinkedProductionProcessNumber",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ProductDescription",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ProductionInstructions",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "TotalPlannedRawMaterialQuantity",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "TotalTransferredQuantity",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "AvailableQuantityBefore",
                table: "ProductionOrderItems");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "ProductionOrderItems");

            migrationBuilder.DropColumn(
                name: "MaterialType",
                table: "ProductionOrderItems");

            migrationBuilder.DropColumn(
                name: "ProductionReferenceNumber",
                table: "ProductionOrderItems");

            migrationBuilder.DropColumn(
                name: "SampleCode",
                table: "ProductionOrderItems");

            migrationBuilder.DropColumn(
                name: "StorageLocationName",
                table: "ProductionOrderItems");

            migrationBuilder.DropColumn(
                name: "WarehouseBatchId",
                table: "ProductionOrderItems");

            migrationBuilder.DropColumn(
                name: "ChemicalInspectedAt",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "ChemicalInspectedBy",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "ChemicalInspectionStatus",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "ChemicalNotes",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "ChemicalResultsJson",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "OverallStatus",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "PhysicalInspectedAt",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "PhysicalInspectedBy",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "PhysicalInspectionStatus",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "PhysicalNotes",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "LaboratoryRawMaterialReports");
        }
    }
}
