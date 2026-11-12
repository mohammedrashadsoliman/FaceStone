using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Source of truth inventory ledger recording finalized, approved stock movements per location, material, and batch
/// </summary>
public class StorageInventoryLedger
{
    public int Id { get; set; }

    public int StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    /// <summary>
    /// Foreign key to linked WarehouseBatch
    /// </summary>
    public int? WarehouseBatchId { get; set; }
    public WarehouseBatch? WarehouseBatch { get; set; }

    /// <summary>
    /// Batch Number (رقم الدفعة, e.g. B-0001)
    /// </summary>
    public string? BatchNumber { get; set; }

    public int? StorageOperationId { get; set; }
    public StorageOperation? StorageOperation { get; set; }

    public int? StorageOperationItemId { get; set; }
    public StorageOperationItem? StorageOperationItem { get; set; }

    /// <summary>
    /// Movement Type: IMPORT | EXPORT | TRANSFER | OPENING_BALANCE
    /// </summary>
    public string MovementType { get; set; } = "IMPORT";

    /// <summary>
    /// Signed quantity (TON): Positive for inward receipts (IMPORT, OPENING_BALANCE), negative for outward issues (EXPORT, TRANSFER)
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Calculated running stock balance in this storage location after this movement
    /// </summary>
    public decimal RunningBalance { get; set; }

    /// <summary>
    /// Transaction / movement date
    /// </summary>
    public DateTime MovementDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }
}
