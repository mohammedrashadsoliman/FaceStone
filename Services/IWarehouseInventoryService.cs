using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Services;

/// <summary>
/// Authoritative shared inventory service establishing ONE single source of truth
/// for warehouse raw material stock across Laboratory, Warehouse, and Production.
/// </summary>
public interface IWarehouseInventoryService
{
    /// <summary>
    /// Single authoritative source: Retrieve dynamic live available warehouse batches.
    /// Excludes depleted, rejected, inactive, or unapproved batches.
    /// Returns live quantities directly from the SQL Server database.
    /// </summary>
    Task<List<WarehouseBatchAvailabilityDto>> GetAuthoritativeBatchesAsync(
        int? storageLocationId = null,
        string? materialType = null,
        int? rawMaterialId = null,
        int? productionReferenceNumber = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve single WarehouseBatch by ID with location and material info.
    /// </summary>
    Task<WarehouseBatch?> GetBatchByIdAsync(int batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate current total running stock for a material at a location from the stock ledger.
    /// </summary>
    Task<decimal> GetMaterialRunningStockAsync(int storageLocationId, int rawMaterialId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast warehouse inventory change event via SignalR to all connected clients across departments.
    /// </summary>
    Task BroadcastInventoryChangedAsync(string action, object? payload = null);
}

