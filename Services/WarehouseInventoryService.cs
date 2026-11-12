using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FaceStone.Web.Data;
using FaceStone.Web.Hubs;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Services;

/// <summary>
/// Authoritative implementation of the shared warehouse inventory service.
/// Consolidates warehouse inventory truth around WarehouseBatches and StorageInventoryLedger.
/// </summary>
public class WarehouseInventoryService : IWarehouseInventoryService
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<WarehouseInventoryService> _logger;
    private readonly IHubContext<ChatHub> _hubContext;

    public WarehouseInventoryService(
        FaceStoneDbContext db,
        ILogger<WarehouseInventoryService> logger,
        IHubContext<ChatHub> hubContext)
    {
        _db = db;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public async Task<List<WarehouseBatchAvailabilityDto>> GetAuthoritativeBatchesAsync(
        int? storageLocationId = null,
        string? materialType = null,
        int? rawMaterialId = null,
        int? productionReferenceNumber = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.WarehouseBatches
            .AsNoTracking()
            .Include(b => b.StorageLocation)
            .Include(b => b.RawMaterial)
            .Where(b => b.IsActive && b.CurrentQuantity > 0 && b.Status == "APPROVED");

        if (storageLocationId.HasValue && storageLocationId.Value > 0)
        {
            query = query.Where(b => b.StorageLocationId == storageLocationId.Value);
        }

        if (rawMaterialId.HasValue && rawMaterialId.Value > 0)
        {
            query = query.Where(b => b.RawMaterialId == rawMaterialId.Value);
        }

        if (productionReferenceNumber.HasValue && productionReferenceNumber.Value > 0)
        {
            query = query.Where(b => b.ProductionReferenceNumber == productionReferenceNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(materialType))
        {
            var cleanType = materialType.Trim().ToUpperInvariant();
            query = query.Where(b => b.MaterialClassification.ToUpper() == cleanType);
        }

        var batches = await query
            .OrderBy(b => b.ProductionReferenceNumber)
            .ThenBy(b => b.BatchNumber)
            .ToListAsync(cancellationToken);

        return batches.Select(b => new WarehouseBatchAvailabilityDto
        {
            WarehouseBatchId = b.Id,
            BatchNumber = b.BatchNumber,
            ProductionReferenceNumber = b.ProductionReferenceNumber,
            SampleCode = b.SampleCode,
            LaboratoryReportId = b.LaboratoryReportId,
            RawMaterialId = b.RawMaterialId,
            MaterialName = !string.IsNullOrWhiteSpace(b.MaterialName)
                ? b.MaterialName
                : (b.RawMaterial != null ? b.RawMaterial.Name : string.Empty),
            MaterialClassification = b.MaterialClassification ?? "MECHANICAL",
            SupplierName = b.SupplierName,
            StorageLocationId = b.StorageLocationId,
            StorageLocationName = b.StorageLocation != null ? b.StorageLocation.Name : string.Empty,
            InitialQuantity = b.InitialQuantity,
            AvailableQuantity = b.CurrentQuantity,
            Unit = "طن",
            ComplyStatus = "COMPLY",
            Status = b.Status,
            ImportDate = b.ImportDate
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<WarehouseBatch?> GetBatchByIdAsync(int batchId, CancellationToken cancellationToken = default)
    {
        return await _db.WarehouseBatches
            .Include(b => b.StorageLocation)
            .Include(b => b.RawMaterial)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<decimal> GetMaterialRunningStockAsync(
        int storageLocationId,
        int rawMaterialId,
        CancellationToken cancellationToken = default)
    {
        return await _db.StorageInventoryLedger
            .AsNoTracking()
            .Where(l => l.StorageLocationId == storageLocationId && l.RawMaterialId == rawMaterialId)
            .SumAsync(l => (decimal?)l.Quantity, cancellationToken) ?? 0m;
    }

    /// <inheritdoc />
    public async Task BroadcastInventoryChangedAsync(string action, object? payload = null)
    {
        try
        {
            var eventData = payload ?? new
            {
                Action = action,
                Timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.All.SendAsync("OnWarehouseInventoryChanged", eventData);
            await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
            {
                Department = "Storage",
                Action = action,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Broadcasted OnWarehouseInventoryChanged SignalR event: {Action}", action);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast OnWarehouseInventoryChanged SignalR event.");
        }
    }
}

