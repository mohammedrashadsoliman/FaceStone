using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models.DTOs;

/// <summary>
/// Authoritative shared DTO representing available raw material batches physically present in Warehouse.
/// Used identically across Warehouse, Laboratory, and Production departments.
/// </summary>
public class WarehouseBatchAvailabilityDto
{
    public int WarehouseBatchId { get; set; }
    public int BatchId => WarehouseBatchId;
    public int Id => WarehouseBatchId;

    public string BatchNumber { get; set; } = string.Empty;
    public int ProductionReferenceNumber { get; set; }
    public string SampleCode { get; set; } = string.Empty;
    public int LaboratoryReportId { get; set; }

    public int RawMaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string RawMaterialName => MaterialName;

    public string MaterialClassification { get; set; } = "MECHANICAL";
    public string MaterialClassificationAr => MaterialClassification == "CHEMICAL" ? "كيميائية" : "ميكانيكية";

    public string? SupplierName { get; set; }

    public int StorageLocationId { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;

    public decimal InitialQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal CurrentQuantity => AvailableQuantity;

    public string Unit { get; set; } = "طن";
    public string ComplyStatus { get; set; } = "COMPLY";
    public string ComplyStatusAr => "مطابق (معتمد)";

    public string Status { get; set; } = "APPROVED";
    public DateTime ImportDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

/// <summary>
/// Request DTO for returning unused / excess materials from Production back to Warehouse inventory
/// </summary>
public class ProductionReturnRequestDto
{
    public int ProductionOrderId { get; set; }
    public List<ProductionReturnMaterialItemDto> Items { get; set; } = new();
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Item line for returning unused material from Production to Warehouse
/// </summary>
public class ProductionReturnMaterialItemDto
{
    public int? ProductionOrderItemId { get; set; }
    public int WarehouseBatchId { get; set; }
    public decimal ReturnQuantity { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Result DTO for Production Return operation
/// </summary>
public class ProductionReturnResultDto
{
    public int StorageOperationId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public int ProductionOrderId { get; set; }
    public string ProductionOrderNumber { get; set; } = string.Empty;
    public decimal TotalReturnedQuantity { get; set; }
    public DateTime ReturnDate { get; set; }
    public List<string> ReturnedBatches { get; set; } = new();
}

