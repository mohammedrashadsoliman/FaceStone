using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FaceStone.Web.Data;
using FaceStone.Web.Hubs;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;
using FaceStone.Web.Services;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class LaboratoryController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LaboratoryController> _logger;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IWarehouseInventoryService _inventoryService;

    public LaboratoryController(
        FaceStoneDbContext db,
        IWebHostEnvironment env,
        ILogger<LaboratoryController> logger,
        IHubContext<ChatHub> hubContext,
        IWarehouseInventoryService inventoryService)
    {
        _db = db;
        _env = env;
        _logger = logger;
        _hubContext = hubContext;
        _inventoryService = inventoryService;
    }

    // =========================================================================
    // 1. DASHBOARD STATS & OVERVIEW
    // =========================================================================
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<LaboratoryDashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats()
    {
        var rawReports = await _db.LaboratoryRawMaterialReports.AsNoTracking().ToListAsync();
        var inProc = await _db.LaboratoryInProcessReports.AsNoTracking().ToListAsync();
        var finalCerts = await _db.LaboratoryFinalConformityCertificates.AsNoTracking().ToListAsync();

        var stats = new LaboratoryDashboardStatsDto
        {
            PhysicalReportsCount = rawReports.Count(r => r.LaboratorySection == "PHYSICAL"),
            ChemicalReportsCount = rawReports.Count(r => r.LaboratorySection == "CHEMICAL"),
            InProcessTestsCount = inProc.Count,
            FinalCertificatesCount = finalCerts.Count,
            PendingApprovalCount = rawReports.Count(r => r.ApprovalStatus == "PENDING") + finalCerts.Count(c => c.ApprovalStatus == "PENDING"),
            ConformingBatchesCount = finalCerts.Count(c => c.ConformityDecision == "CONFORMS"),
            NonConformingBatchesCount = finalCerts.Count(c => c.ConformityDecision == "NON_CONFORMING"),
            TotalAcceptedRawMaterialTons = rawReports.Where(r => r.ApprovalStatus == "APPROVED").Sum(r => r.AcceptedQuantity),
            TotalFinalProductTons = finalCerts.Where(c => c.ConformityDecision == "CONFORMS" && c.ApprovalStatus == "APPROVED").Sum(c => c.ActualFinalProductQuantity),
            ActiveTraceableLifecyclesCount = rawReports.Select(r => r.ProductionReferenceNumber).Distinct().Count()
        };

        return Ok(ApiResponse<LaboratoryDashboardStatsDto>.Ok(stats));
    }

    // =========================================================================
    // 2. SHARED PRODUCTION REFERENCE LOOKUPS (Cross-department dropdowns)
    // =========================================================================
    [HttpGet("references")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferences(
        [FromQuery] string? section = null,
        [FromQuery] string? decision = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var query = _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Include(r => r.InProcessReports)
            .Include(r => r.FinalCertificates)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var secNorm = section.Trim().ToUpperInvariant();
            query = query.Where(r => r.LaboratorySection == secNorm);
        }

        if (!string.IsNullOrWhiteSpace(decision) && !decision.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var decNorm = decision.Trim().ToUpperInvariant();
            query = query.Where(r => r.InspectionDecision == decNorm);
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var stNorm = status.Trim().ToUpperInvariant();
            query = query.Where(r => r.ApprovalStatus == stNorm);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.ProductionReferenceNumber.ToString().Contains(s) ||
                r.RawMaterialDescription.ToLower().Contains(s) ||
                (r.SupplierName != null && r.SupplierName.ToLower().Contains(s)) ||
                (r.PONumber != null && r.PONumber.ToLower().Contains(s)));
        }

        var reports = await query.OrderByDescending(r => r.ProductionReferenceNumber).Take(200).ToListAsync();

        var dtos = reports.Select(MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    /// <summary>
    /// Returns the current authenticated user's next incremental sample number (read-only in form)
    /// </summary>
    [HttpGet("next-sample-number")]
    [ProducesResponseType(typeof(ApiResponse<NextSampleNumberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNextSampleNumber()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth || !userId.HasValue)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة."));
        }

        var (nextNo, nextCode) = await PeekNextGlobalSampleNumberAsync();
        var section = AppRoles.GetDefaultLaboratorySection(role);

        return Ok(ApiResponse<NextSampleNumberDto>.Ok(new NextSampleNumberDto
        {
            SampleNumber = nextNo,
            SampleCode = nextCode,
            UserId = userId.Value,
            UserName = userName,
            LaboratorySection = section
        }));
    }

    /// <summary>
    /// Searchable selection control populated from existing backend references (Production, Storage, Lab).
    /// </summary>
    [HttpGet("batch-references")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryBatchLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatchReferences([FromQuery] string? search = null)
    {
        var results = new List<LaboratoryBatchLookupDto>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. From LaboratoryRawMaterialReports
        var labReports = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Take(100)
            .ToListAsync();

        foreach (var r in labReports)
        {
            var key = !string.IsNullOrWhiteSpace(r.BatchNumber) ? r.BatchNumber : $"REF-{r.ProductionReferenceNumber:D4}";
            if (seenKeys.Add(key))
            {
                results.Add(new LaboratoryBatchLookupDto
                {
                    ProductionReferenceNumber = r.ProductionReferenceNumber,
                    SampleNumber = r.SampleNumber,
                    SampleCode = !string.IsNullOrWhiteSpace(r.SampleCode) ? r.SampleCode : $"SMP-{r.SampleNumber:D4}",
                    BatchNumber = key,
                    SampleName = !string.IsNullOrWhiteSpace(r.SampleName) ? r.SampleName : r.RawMaterialDescription,
                    SampleDescription = !string.IsNullOrWhiteSpace(r.SampleDescription) ? r.SampleDescription : "بودرة رمادية اللون",
                    SupplierName = r.SupplierName,
                    PONumber = r.PONumber,
                    LaboratorySection = r.LaboratorySection,
                    CreatedAt = r.CreatedAt,
                    SourceType = "LABORATORY_REPORT"
                });
            }
        }

        // 2. From ProductionBatches
        var prodBatches = await _db.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProductionOrder)
            .OrderByDescending(b => b.Id)
            .Take(100)
            .ToListAsync();

        foreach (var pb in prodBatches)
        {
            var key = !string.IsNullOrWhiteSpace(pb.BatchNumber) ? pb.BatchNumber : $"PRD-BATCH-{pb.Id:D4}";
            if (seenKeys.Add(key))
            {
                results.Add(new LaboratoryBatchLookupDto
                {
                    ProductionReferenceNumber = pb.ProductionReferenceNumber ?? pb.Id,
                    BatchNumber = key,
                    SampleName = pb.ProductionOrder?.OrderNumber != null ? $"تشغيلة {pb.ProductionOrder.OrderNumber}" : "سيليكا باودر (Micro Silica Powder)",
                    SampleDescription = $"مرحلة: {pb.CurrentStage}",
                    SupplierName = "FaceStone Production Line",
                    PONumber = pb.ProductionOrder?.OrderNumber,
                    LaboratorySection = "PHYSICAL",
                    CreatedAt = DateTime.UtcNow,
                    SourceType = "PRODUCTION_BATCH"
                });
            }
        }

        // 3. From StorageOperations (Imports)
        var storageOps = await _db.StorageOperations
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.OperationType == "IMPORT")
            .OrderByDescending(o => o.Id)
            .Take(100)
            .ToListAsync();

        foreach (var op in storageOps)
        {
            var key = !string.IsNullOrWhiteSpace(op.WeighbridgeReference) ? op.WeighbridgeReference : $"STR-IMP-{op.TransactionNumber:D4}";
            if (seenKeys.Add(key))
            {
                var firstItem = op.Items.FirstOrDefault();
                results.Add(new LaboratoryBatchLookupDto
                {
                    ProductionReferenceNumber = op.ProductionReferenceNumber ?? op.TransactionNumber,
                    BatchNumber = key,
                    SampleName = firstItem?.MaterialName ?? "سيليكا باودر (Micro Silica Powder)",
                    SampleDescription = op.StorageLocationName ?? "بودرة رمادية اللون",
                    SupplierName = op.Source,
                    PONumber = null,
                    LaboratorySection = op.MaterialType == "CHEMICAL" ? "CHEMICAL" : "PHYSICAL",
                    CreatedAt = op.TransactionDate,
                    SourceType = "STORAGE_IMPORT"
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            results = results.Where(r =>
                r.BatchNumber.ToLower().Contains(s) ||
                r.SampleName.ToLower().Contains(s) ||
                (r.SupplierName != null && r.SupplierName.ToLower().Contains(s)) ||
                (r.PONumber != null && r.PONumber.ToLower().Contains(s)) ||
                r.ProductionReferenceNumber.ToString().Contains(s)
            ).ToList();
        }

        return Ok(ApiResponse<List<LaboratoryBatchLookupDto>>.Ok(results));
    }

    /// <summary>
    /// References with approved accepted raw material quantities or conforming finished products available for Storage Import.
    /// </summary>
    [HttpGet("for-storage-import")]
    [HttpGet("references/for-storage-import")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferencesForStorageImport()
    {
        var rawQuery = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0 && !r.IsStorageReceived)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .ToListAsync();

        var dtos = rawQuery.Select(r =>
        {
            var d = MapToReferenceLookupDto(r);
            d.Stage = "RAW_MATERIAL_ACCEPTED";
            return d;
        }).ToList();

        // Also include approved conforming finished products ready for finished goods storage receipt
        var finalCerts = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .Where(c => c.ApprovalStatus == "APPROVED" && c.ConformityDecision == "CONFORMS" && !c.IsStoragePosted)
            .OrderByDescending(c => c.ProductionReferenceNumber)
            .ToListAsync();

        foreach (var c in finalCerts)
        {
            dtos.Add(new LaboratoryReferenceLookupDto
            {
                ProductionReferenceNumber = c.ProductionReferenceNumber,
                LaboratorySection = c.LaboratorySection,
                RawMaterialDescription = c.FinalProductName,
                FinalProductName = c.FinalProductName,
                FinalProductQuantity = c.ActualFinalProductQuantity,
                SupplierName = "FaceStone Production Line",
                PONumber = c.ProductionBatchNumber,
                ReceivedQuantity = c.ActualFinalProductQuantity,
                AcceptedQuantity = c.ActualFinalProductQuantity,
                RejectedQuantity = 0,
                InspectionDecision = c.ConformityDecision,
                ConformityDecision = c.ConformityDecision,
                AnalysisDateTime = c.ProductionCompletionDateTime,
                ApprovalStatus = c.ApprovalStatus,
                IsStorageReceived = c.IsStoragePosted,
                HasFinalCertificate = true,
                Stage = "FINAL_PRODUCT_APPROVED"
            });
        }

        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    /// <summary>
    /// References eligible for Production Work-In-Process transfer / batch tracking.
    /// </summary>
    [HttpGet("for-production")]
    [HttpGet("references/for-production")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferencesForProduction()
    {
        var query = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .ToListAsync();

        var dtos = query.Select(MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    /// <summary>
    /// References eligible for In-Process testing.
    /// </summary>
    [HttpGet("for-in-process")]
    [HttpGet("references/for-in-process")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferencesForInProcess([FromQuery] string? section = null)
    {
        var query = _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0);

        if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var secNorm = section.Trim().ToUpperInvariant();
            query = query.Where(r => r.LaboratorySection == secNorm);
        }

        var list = await query.OrderByDescending(r => r.ProductionReferenceNumber).ToListAsync();
        var dtos = list.Select(MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    /// <summary>
    /// References eligible for Final Product Conformity inspection.
    /// </summary>
    [HttpGet("for-final-certificate")]
    [HttpGet("references/for-final-certificate")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferencesForFinalCertificate([FromQuery] string? section = null)
    {
        var query = _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Include(r => r.FinalCertificates)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0);

        if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var secNorm = section.Trim().ToUpperInvariant();
            query = query.Where(r => r.LaboratorySection == secNorm);
        }

        var list = await query.OrderByDescending(r => r.ProductionReferenceNumber).ToListAsync();
        var dtos = list.Select(MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    // =========================================================================
    // 3. FULL LIFECYCLE TRACEABILITY (Management, Finance & Quality Audit)
    // =========================================================================
    [HttpGet("traceability/{referenceNumber:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductionReferenceTraceabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraceability(int referenceNumber)
    {
        var rawReport = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Include(r => r.SourceTransportation)
            .Include(r => r.PurchaseOrder)
            .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == referenceNumber);

        if (rawReport == null)
        {
            return NotFound(ApiResponse.Fail($"الرقم المرجعي التتبعي #{referenceNumber} غير موجود بالنظام."));
        }

        // Linked Storage Operations (Imports or Transfers for this reference)
        var storageOps = await _db.StorageOperations
            .AsNoTracking()
            .Include(o => o.StorageLocation)
            .Include(o => o.Client)
            .Include(o => o.Items)
            .Where(o => o.ProductionReferenceNumber == referenceNumber || (rawReport.StorageOperationId.HasValue && o.Id == rawReport.StorageOperationId.Value))
            .ToListAsync();

        var storageOpDtos = storageOps.Select(op => StorageController.MapOperationToDto(op, op.Items.ToList())).ToList();

        // Linked Production WIP Batches
        var wipBatches = await _db.ProductionWorkInProcess
            .AsNoTracking()
            .Where(w => w.ProductionReferenceNumber == referenceNumber)
            .ToListAsync();

        var wipDtos = wipBatches.Select(w => new ProductionWipBatchDto
        {
            Id = w.Id,
            TrackingNumber = w.TrackingNumber,
            MaterialQuantityIssued = w.MaterialQuantityIssued,
            SourceStorageLocation = w.SourceStorageLocation,
            TransferDate = w.TransferDate,
            Status = w.Status,
            FinalProductName = w.FinalProductName,
            FinalProductQuantity = w.FinalProductQuantity,
            WasteQuantity = w.WasteQuantity,
            WasteClassification = w.WasteClassification,
            RecoverableProductName = w.RecoverableProductName,
            RecoverableQuantity = w.RecoverableQuantity
        }).ToList();

        // Linked In-Process Reports
        var inProcessList = await _db.LaboratoryInProcessReports
            .AsNoTracking()
            .Where(p => p.ProductionReferenceNumber == referenceNumber)
            .OrderBy(p => p.TestDateTime)
            .ToListAsync();

        var inProcDtos = inProcessList.Select(MapInProcessToDto).ToList();

        // Linked Final Conformity Certificate
        var finalCert = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProductionReferenceNumber == referenceNumber);

        var finalCertDto = finalCert != null ? MapFinalCertToDto(finalCert) : null;

        // Audit Trail (FormApprovalHistory) for Laboratory raw report, storage, and certificate
        var auditHistories = await _db.FormApprovalHistories
            .AsNoTracking()
            .Where(h => (h.FormType == "LaboratoryRawMaterial" && h.FormId == rawReport.Id) ||
                        (h.FormType == "LaboratoryFinalCert" && finalCert != null && h.FormId == finalCert.Id) ||
                        (h.FormType == "Storage" && storageOps.Select(o => o.Id).Contains(h.FormId)))
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new FormApprovalHistoryDto
            {
                Id = h.Id,
                FormType = h.FormType,
                FormId = h.FormId,
                PONumber = h.PONumber,
                TransactionOrMovementNumber = h.TransactionOrMovementNumber,
                PreviousStatus = h.PreviousStatus,
                NewStatus = h.NewStatus,
                Action = h.Action,
                ActorName = h.ActorName,
                ActorRole = h.ActorRole,
                Comment = h.Comment,
                Timestamp = h.Timestamp
            })
            .ToListAsync();

        // Linked Transportation Summary
        TransportationSummaryDto? transDto = null;
        if (rawReport.SourceTransportation != null)
        {
            transDto = new TransportationSummaryDto
            {
                Id = rawReport.SourceTransportation.Id,
                TransactionNumber = rawReport.SourceTransportation.TransactionNumber,
                TransactionDate = rawReport.SourceTransportation.TransactionDate,
                CarNumber = rawReport.SourceTransportation.CarNumber,
                DriverName = rawReport.SourceTransportation.DriverName,
                Source = rawReport.SourceTransportation.Source,
                PONumber = rawReport.SourceTransportation.PONumber,
                MaterialType = rawReport.SourceTransportation.MaterialType,
                NetWeight = rawReport.SourceTransportation.NetWeight,
                ApprovalStatus = rawReport.SourceTransportation.ApprovalStatus
            };
        }

        // Linked Purchase Order Summary
        PurchaseOrderSummaryDto? poDto = null;
        if (rawReport.PurchaseOrder != null)
        {
            poDto = new PurchaseOrderSummaryDto
            {
                Id = rawReport.PurchaseOrder.Id,
                PONumber = rawReport.PurchaseOrder.PONumber,
                Supplier = rawReport.PurchaseOrder.Supplier,
                Quantity = rawReport.PurchaseOrder.Quantity,
                RemainingQuantity = rawReport.PurchaseOrder.RemainingQuantity,
                PricePerTon = rawReport.PurchaseOrder.PricePerTon > 0 ? rawReport.PurchaseOrder.PricePerTon : rawReport.PurchaseOrder.TonPrice,
                TotalValue = rawReport.PurchaseOrder.TotalValue,
                PaymentStatus = rawReport.PurchaseOrder.PaymentStatus
            };
        }

        var result = new ProductionReferenceTraceabilityDto
        {
            ProductionReferenceNumber = referenceNumber,
            LaboratorySection = rawReport.LaboratorySection,
            RawMaterialReport = MapRawMaterialToDto(rawReport),
            StorageImports = storageOpDtos,
            ProductionBatches = wipDtos,
            InProcessReports = inProcDtos,
            FinalCertificate = finalCertDto,
            AuditTrail = auditHistories,
            RelatedTransportation = transDto,
            RelatedPurchaseOrder = poDto
        };

        return Ok(ApiResponse<ProductionReferenceTraceabilityDto>.Ok(result));
    }

    // =========================================================================
    // 4. CARD ONE: RAW MATERIAL ANALYSIS REPORT (تقرير نتائج تحليل المواد الخام)
    // =========================================================================
    [HttpGet("raw-material-reports")]
    [ProducesResponseType(typeof(ApiResponse<List<RawMaterialReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRawMaterialReports(
        [FromQuery] string? section = null,
        [FromQuery] string? decision = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var query = _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .AsQueryable();

        // Enforce employee laboratory section scoping if employee
        if (AppRoles.HasPhysicalLabAccess(role) && !AppRoles.HasChemicalLabAccess(role))
        {
            query = query.Where(r => r.LaboratorySection == "PHYSICAL");
        }
        else if (AppRoles.HasChemicalLabAccess(role) && !AppRoles.HasPhysicalLabAccess(role))
        {
            query = query.Where(r => r.LaboratorySection == "CHEMICAL");
        }
        else if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var secNorm = section.Trim().ToUpperInvariant();
            query = query.Where(r => r.LaboratorySection == secNorm);
        }

        if (!string.IsNullOrWhiteSpace(decision) && !decision.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.InspectionDecision == decision.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.ApprovalStatus == status.Trim().ToUpperInvariant());
        }

        if (fromDate.HasValue)
        {
            var f = fromDate.Value.Date;
            query = query.Where(r => r.AnalysisDateTime >= f);
        }

        if (toDate.HasValue)
        {
            var t = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(r => r.AnalysisDateTime <= t);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.ProductionReferenceNumber.ToString().Contains(s) ||
                r.SampleNumber.ToString().Contains(s) ||
                (r.SampleName != null && r.SampleName.ToLower().Contains(s)) ||
                (r.BatchNumber != null && r.BatchNumber.ToLower().Contains(s)) ||
                r.RawMaterialDescription.ToLower().Contains(s) ||
                (r.SupplierName != null && r.SupplierName.ToLower().Contains(s)) ||
                (r.PONumber != null && r.PONumber.ToLower().Contains(s)) ||
                (r.ComplyStatus != null && r.ComplyStatus.ToLower().Contains(s)) ||
                r.ResponsibleEmployee.ToLower().Contains(s));
        }

        var reports = await query.OrderByDescending(r => r.Id).Take(300).ToListAsync();
        var dtos = reports.Select(MapRawMaterialToDto).ToList();
        return Ok(ApiResponse<List<RawMaterialReportDto>>.Ok(dtos));
    }

    [HttpGet("raw-material-reports/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<RawMaterialReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRawMaterialReportById(int id)
    {
        var report = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            return NotFound(ApiResponse.Fail($"تقرير فحص المواد الخام #{id} غير موجود."));
        }

        return Ok(ApiResponse<RawMaterialReportDto>.Ok(MapRawMaterialToDto(report)));
    }

    [HttpPost("raw-material-reports")]
    [ProducesResponseType(typeof(ApiResponse<RawMaterialReportDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRawMaterialReport()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        if (!AppRoles.HasLaboratoryAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: الوصول مقتصر على موظفي المعمل المعتمدين."));
        }

        var dto = await ParseRawMaterialReportRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات تقرير فحص المواد الخام غير صالحة."));
        }

        // 1. Determine & Enforce Laboratory Section
        string assignedSection = AppRoles.GetDefaultLaboratorySection(role);
        string section = "PHYSICAL";

        if (assignedSection == "PHYSICAL")
        {
            section = "PHYSICAL";
        }
        else if (assignedSection == "CHEMICAL")
        {
            section = "CHEMICAL";
        }
        else if (assignedSection == "ALL")
        {
            // CEO or IT can specify section in request
            var requestedSec = (dto.LaboratorySection ?? "PHYSICAL").Trim().ToUpperInvariant();
            section = requestedSec == "CHEMICAL" ? "CHEMICAL" : "PHYSICAL";
        }

        // Strict cross-section submission check
        if (assignedSection == "PHYSICAL" && !string.IsNullOrWhiteSpace(dto.LaboratorySection) && dto.LaboratorySection.Trim().ToUpperInvariant() == "CHEMICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الفيزيائي لا يمكنه تسجيل نتائج للمعمل الكيميائي."));
        }
        if (assignedSection == "CHEMICAL" && !string.IsNullOrWhiteSpace(dto.LaboratorySection) && dto.LaboratorySection.Trim().ToUpperInvariant() == "PHYSICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الكيميائي لا يمكنه تسجيل نتائج للمعمل الفيزيائي."));
        }

        // 2. Validate / normalize Sample Name & Description
        var sampleName = (dto.SampleName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sampleName))
        {
            sampleName = "سيليكا باودر (Micro Silica Powder)";
        }

        var sampleDesc = (dto.SampleDescription ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sampleDesc))
        {
            sampleDesc = "بودرة رمادية اللون";
        }

        var desc = (dto.RawMaterialDescription ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(desc))
        {
            desc = $"{sampleName} - {sampleDesc}";
        }

        // 3. Validate Comply Status & Inspection Decision
        var complyStatus = (dto.ComplyStatus ?? "COMPLY").Trim().ToUpperInvariant();
        if (complyStatus != "COMPLY" && complyStatus != "NOT_COMPLIED")
        {
            complyStatus = "COMPLY";
        }

        var decision = complyStatus == "COMPLY" ? "ACCEPTED" : "REJECTED";
        if (!string.IsNullOrWhiteSpace(dto.InspectionDecision) && dto.InspectionDecision.Equals("REJECTED", StringComparison.OrdinalIgnoreCase))
        {
            decision = "REJECTED";
            complyStatus = "NOT_COMPLIED";
        }

        decimal receivedQuantity = dto.ReceivedQuantity;
        decimal acceptedQuantity = dto.AcceptedQuantity;
        decimal rejectedQuantity = dto.RejectedQuantity ?? (receivedQuantity - acceptedQuantity);

        // 4. Validate Linked Transportation or PO if provided
        int? transId = null;
        int? transNum = null;
        if (dto.SourceTransportationId.HasValue && dto.SourceTransportationId.Value > 0)
        {
            var trans = await _db.Transportations.FirstOrDefaultAsync(t => t.Id == dto.SourceTransportationId.Value);
            if (trans != null)
            {
                transId = trans.Id;
                transNum = trans.TransactionNumber;
            }
        }

        int? poId = null;
        string? poNumber = dto.PONumber?.Trim();
        if (dto.PurchaseOrderId.HasValue && dto.PurchaseOrderId.Value > 0)
        {
            var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == dto.PurchaseOrderId.Value);
            if (po != null)
            {
                poId = po.Id;
                poNumber = po.PONumber;
            }
        }
        else if (!string.IsNullOrWhiteSpace(poNumber))
        {
            var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber.ToLower() == poNumber.ToLower());
            if (po != null)
            {
                poId = po.Id;
            }
        }

        // 4. Validate Tested Material Weight
        if (dto.TestedMaterialWeight < 0)
        {
            return BadRequest(ApiResponse.Fail("إجمالي وزن المواد التي تم اختبارها لا يمكن أن يكون سالباً."));
        }

        // Material Classification (MECHANICAL | CHEMICAL)
        var classification = (dto.MaterialClassification ?? "MECHANICAL").Trim().ToUpperInvariant();
        if (classification != "CHEMICAL" && classification != "MECHANICAL")
        {
            classification = "MECHANICAL";
        }

        // 5. Handle Technical Report Attachment & Quantity Confirmation Document
        var attachRes = await ValidateAndSaveAttachmentAsync(dto.Attachment, dto.AttachmentPath, "تقرير الفحص الفني");
        if (!attachRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attachRes.ErrorMessage ?? "مرفق التقرير الفني غير صالح."));
        }

        var qtyAttachRes = await ValidateAndSaveAttachmentAsync(dto.QuantityConfirmationDocument, dto.QuantityConfirmationPath, "مستند تأكيد الكمية المستلمة", isRequired: false);
        if (!qtyAttachRes.Success)
        {
            CleanupSavedFiles(new[] { attachRes.StoredFileName });
            return BadRequest(ApiResponse.Fail(qtyAttachRes.ErrorMessage ?? "مستند تأكيد الكمية المستلمة غير صالح."));
        }

        var analysisDate = dto.AnalysisDateTime ?? DateTime.UtcNow;

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. Automatically register product as a Warehouse Raw Material if not already present (Stock remains 0)
            var matName = sampleName.Trim();
            var existingMat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name.ToLower() == matName.ToLower());
            int registeredRawMatId;
            if (existingMat == null)
            {
                var newMaterial = new RawMaterial
                {
                    Name = matName,
                    MaterialType = classification == "CHEMICAL" ? MaterialProcessType.Chemical : MaterialProcessType.Mechanical,
                    InitialQuantity = 0m, // Important: stock does NOT increase until warehouse approved import
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName,
                    CreatedByUserId = userId
                };
                await _db.RawMaterials.AddAsync(newMaterial);
                await _db.SaveChangesAsync();
                registeredRawMatId = newMaterial.Id;
            }
            else
            {
                registeredRawMatId = existingMat.Id;
            }

            // 2. Shared Production Reference Number (Batch Reference)
            int refNumber;
            if (dto.ProductionReferenceNumber.HasValue && dto.ProductionReferenceNumber.Value > 0)
            {
                refNumber = dto.ProductionReferenceNumber.Value;
            }
            else
            {
                refNumber = await GetNextProductionReferenceNumberAsync();
            }

            // 3. Central Primary Reference: Concurrency-Safe Globally Unique Sample Number (رقم العينة)
            var (sampleNumber, sampleCode) = await GetNextGlobalSampleNumberAsync();

            var batchNumber = (dto.BatchNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(batchNumber))
            {
                batchNumber = sampleCode;
            }

            var unit = string.IsNullOrWhiteSpace(dto.Unit) ? "طن" : dto.Unit.Trim();

            var report = new LaboratoryRawMaterialReport
            {
                ProductionReferenceNumber = refNumber,
                SampleNumber = sampleNumber,
                SampleCode = sampleCode,
                MaterialClassification = classification,
                SampleName = sampleName,
                SampleDescription = sampleDesc,
                BatchNumber = batchNumber,
                SampleReceivedDate = dto.SampleReceivedDate ?? analysisDate,
                ReportIssueDate = dto.ReportIssueDate ?? DateTime.UtcNow,
                LabManagerName = string.IsNullOrWhiteSpace(dto.LabManagerName) ? userName : dto.LabManagerName.Trim(),
                LabManagerSignature = string.IsNullOrWhiteSpace(dto.LabManagerSignature) ? "معتمد" : dto.LabManagerSignature.Trim(),
                LabManagerDate = dto.LabManagerDate ?? DateTime.UtcNow,
                ComplyStatus = "PENDING",
                LaboratorySection = "PHYSICAL",
                AnalysisDateTime = analysisDate,
                RawMaterialDescription = desc,
                RawMaterialId = registeredRawMatId,
                SupplierName = dto.SupplierName?.Trim(),
                SourceTransportationId = transId,
                TransportationTransactionNumber = transNum,
                PurchaseOrderId = poId,
                PONumber = poNumber,
                ReceivedQuantity = Math.Round(receivedQuantity, 3),
                TestedMaterialWeight = Math.Round(dto.TestedMaterialWeight, 3),
                Unit = unit,
                AcceptedQuantity = 0, // CRITICAL: Does not enter warehouse until dual approval!
                RejectedQuantity = 0,
                AnalysisResultsJson = dto.AnalysisResultsJson,
                PhysicalInspectionStatus = "APPROVED",
                PhysicalInspectedBy = userName,
                PhysicalInspectedAt = DateTime.UtcNow,
                PhysicalNotes = dto.Notes,
                ChemicalInspectionStatus = "PENDING",
                OverallStatus = "PENDING_CHEMICAL",
                ApprovalStatus = "PENDING", // Dual approval gate enforces pending until chemical inspection
                InspectionDecision = "PENDING",
                Notes = !string.IsNullOrWhiteSpace(dto.Notes) ? dto.Notes.Trim() : "فحص المعمل الفيزيائي مكتمل وبانتظار الفحص الكيميائي",
                AttachmentPath = !string.IsNullOrWhiteSpace(attachRes.StoredFileName) ? $"/uploads/laboratory/{attachRes.StoredFileName}" : null,
                AttachmentFileName = attachRes.OriginalFileName,
                AttachmentFileType = attachRes.ContentType,
                AttachmentFileSize = attachRes.FileSize,
                QuantityConfirmationPath = !string.IsNullOrWhiteSpace(qtyAttachRes.StoredFileName) ? $"/uploads/laboratory/{qtyAttachRes.StoredFileName}" : (!string.IsNullOrWhiteSpace(dto.QuantityConfirmationPath) ? dto.QuantityConfirmationPath : null),
                QuantityConfirmationFileName = qtyAttachRes.OriginalFileName ?? dto.QuantityConfirmationFileName,
                QuantityConfirmationFileType = qtyAttachRes.ContentType,
                QuantityConfirmationFileSize = qtyAttachRes.FileSize,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                ReviewedBy = null,
                ReviewedAt = null,
                IsStorageReceived = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.LaboratoryRawMaterialReports.AddAsync(report);
            await _db.SaveChangesAsync();

            // Link ProductionReferenceNumber on linked Transportation if not already set
            if (transId.HasValue)
            {
                var trans = await _db.Transportations.FirstOrDefaultAsync(t => t.Id == transId.Value);
                if (trans != null && !trans.ProductionReferenceNumber.HasValue)
                {
                    trans.ProductionReferenceNumber = refNumber;
                }
            }

            // Create FormApprovalHistory
            var history = new FormApprovalHistory
            {
                FormType = "LaboratoryRawMaterial",
                FormId = report.Id,
                PONumber = poNumber ?? string.Empty,
                TransactionOrMovementNumber = $"REF-#{sampleCode}",
                PreviousStatus = "NONE",
                NewStatus = "PENDING_CHEMICAL",
                Action = "PHYSICAL_INSPECTION_COMPLETED",
                ActorName = userName,
                ActorUserId = userId,
                ActorRole = role,
                Comment = $"تم تسجيل فحص المعمل الفيزيائي للعينة {sampleCode} للمادة '{sampleName}' ({classification}) بكمية {receivedQuantity:F3} {unit}. المادة بانتظار استكمال الفحص الكيميائي لاعتماد المطابقة.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Physical Laboratory Sample {SampleCode} created for Material '{Material}' by user {User}. Pending Chemical Inspection.",
                sampleCode, sampleName, userName);

            // SignalR broadcast to notify Chemical Laboratory
            var notifMsg = $"تم تسجيل فحص معمل فيزيائي جديد للمادة ({sampleName}) برقم مرجعي ({sampleCode}) وهو متاح الآن للفحص الكيميائي.";
            await NotifyLaboratoryChangeAsync("RAW_REPORT", report.Id, refNumber, "PENDING_CHEMICAL", userName, notifMsg);

            var resultDto = MapRawMaterialToDto(report);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<RawMaterialReportDto>.Ok(resultDto, $"تم تسجيل فحص المعمل الفيزيائي بنجاح وتوليد الرقم المرجعي الموحد ({sampleCode}). المادة بانتظار الفحص الكيميائي لاعتمادها."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(new[] { attachRes.StoredFileName, qtyAttachRes.StoredFileName });
            _logger.LogError(ex, "Failed to create Laboratory Raw Material Report.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ تقرير فحص العينة."));
        }
    }

    /// <summary>
    /// Retrieve all Physical Laboratory reports that are pending Chemical Laboratory inspection.
    /// Used by the Chemical Laboratory to select the Physical reference and auto-populate shared material data.
    /// </summary>
    [HttpGet("pending-chemical-references")]
    [ProducesResponseType(typeof(ApiResponse<List<PendingChemicalReferenceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingChemicalReferences()
    {
        var pending = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Where(r => r.OverallStatus == "PENDING_CHEMICAL" || 
                        (r.PhysicalInspectionStatus == "APPROVED" && (r.ChemicalInspectionStatus == "PENDING" || string.IsNullOrEmpty(r.ChemicalInspectionStatus))))
            .OrderByDescending(r => r.Id)
            .Take(100)
            .ToListAsync();

        var list = pending.Select(r => new PendingChemicalReferenceDto
        {
            Id = r.Id,
            ProductionReferenceNumber = r.ProductionReferenceNumber,
            SampleNumber = r.SampleNumber,
            SampleCode = !string.IsNullOrWhiteSpace(r.SampleCode) ? r.SampleCode : $"LAB-REF-{r.CreatedAt.Year}-{r.SampleNumber:D6}",
            SampleName = !string.IsNullOrWhiteSpace(r.SampleName) ? r.SampleName : r.RawMaterialDescription,
            SampleDescription = r.SampleDescription,
            MaterialClassification = r.MaterialClassification ?? "MECHANICAL",
            SupplierName = r.SupplierName,
            SampleReceivedDate = r.SampleReceivedDate,
            ReceivedQuantity = r.ReceivedQuantity,
            TestedMaterialWeight = r.TestedMaterialWeight,
            Unit = !string.IsNullOrWhiteSpace(r.Unit) ? r.Unit : "طن",
            PhysicalNotes = r.PhysicalNotes ?? r.Notes,
            PhysicalInspectedBy = r.PhysicalInspectedBy ?? r.ResponsibleEmployee,
            PhysicalInspectedAt = r.PhysicalInspectedAt ?? r.CreatedAt,
            PhysicalResultsJson = r.AnalysisResultsJson,
            AttachmentPath = r.AttachmentPath,
            AttachmentFileName = r.AttachmentFileName
        }).ToList();

        return Ok(ApiResponse<List<PendingChemicalReferenceDto>>.Ok(list));
    }

    /// <summary>
    /// Chemical Laboratory submits inspection results and compliance decision for a Physical Laboratory reference.
    /// Dual approval gate: material only becomes COMPLIANT_APPROVED and available for Warehouse Import
    /// if Physical Lab is APPROVED and Chemical Lab submits COMPLY.
    /// </summary>
    [HttpPost("chemical-inspection")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<RawMaterialReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitChemicalInspection(
        [FromForm] SubmitChemicalInspectionDto dto,
        IFormFile? attachment = null)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        if (dto.LaboratoryReportId <= 0 && string.IsNullOrWhiteSpace(dto.SampleCode))
        {
            return BadRequest(ApiResponse.Fail("رقم المرجع المعملي للتقرير مطلوب."));
        }

        var report = await _db.LaboratoryRawMaterialReports
            .FirstOrDefaultAsync(r => (dto.LaboratoryReportId > 0 && r.Id == dto.LaboratoryReportId) ||
                                      (!string.IsNullOrWhiteSpace(dto.SampleCode) && r.SampleCode == dto.SampleCode));

        if (report == null)
        {
            return NotFound(ApiResponse.Fail("تقرير فحص المعمل الفيزيائي المحدد غير موجود."));
        }

        var comply = (dto.ComplyStatus ?? "COMPLY").Trim().ToUpperInvariant();
        bool isCompliant = comply == "COMPLY";

        string? storedAttachmentPath = null;
        string? storedAttachmentFileName = null;
        string? storedAttachmentFileType = null;
        long storedAttachmentFileSize = 0;

        var attachFile = attachment ?? dto.Attachment;
        if (attachFile != null && attachFile.Length > 0)
        {
            var attachRes = await ValidateAndSaveAttachmentAsync(attachFile, null, "مرفق تقرير المعمل الكيميائي", isRequired: false);
            if (!attachRes.Success)
            {
                return BadRequest(ApiResponse.Fail(attachRes.ErrorMessage ?? "مرفق تقرير المعمل الكيميائي غير صالح."));
            }
            storedAttachmentPath = !string.IsNullOrWhiteSpace(attachRes.StoredFileName) ? $"/uploads/laboratory/{attachRes.StoredFileName}" : null;
            storedAttachmentFileName = attachRes.OriginalFileName;
            storedAttachmentFileType = attachRes.ContentType;
            storedAttachmentFileSize = attachRes.FileSize;
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            report.ChemicalInspectionStatus = isCompliant ? "COMPLIANT" : "NON_COMPLIANT";
            report.ChemicalInspectedBy = userName;
            report.ChemicalInspectedAt = DateTime.UtcNow;
            report.ChemicalNotes = dto.Notes;
            report.ChemicalResultsJson = dto.AnalysisResultsJson;

            if (!string.IsNullOrWhiteSpace(dto.LabManagerName))
            {
                report.LabManagerName = dto.LabManagerName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(dto.LabManagerSignature))
            {
                report.LabManagerSignature = dto.LabManagerSignature.Trim();
            }
            if (dto.LabManagerDate.HasValue)
            {
                report.LabManagerDate = dto.LabManagerDate.Value;
            }

            if (storedAttachmentPath != null)
            {
                report.AttachmentPath = storedAttachmentPath;
                report.AttachmentFileName = storedAttachmentFileName;
                report.AttachmentFileType = storedAttachmentFileType;
                report.AttachmentFileSize = storedAttachmentFileSize;
            }

            // DUAL APPROVAL GATE:
            // Physical must be APPROVED and Chemical must be COMPLIANT
            if (report.PhysicalInspectionStatus == "APPROVED" && isCompliant)
            {
                report.OverallStatus = "COMPLIANT_APPROVED";
                report.ApprovalStatus = "APPROVED";
                report.ComplyStatus = "COMPLY";
                report.InspectionDecision = "ACCEPTED";
                report.AcceptedQuantity = report.ReceivedQuantity > 0 ? report.ReceivedQuantity : report.TestedMaterialWeight;
                report.RejectedQuantity = 0;
            }
            else
            {
                report.OverallStatus = "NON_COMPLIANT";
                report.ApprovalStatus = "REJECTED";
                report.ComplyStatus = "NOT_COMPLIED";
                report.InspectionDecision = "REJECTED";
                report.AcceptedQuantity = 0;
                report.RejectedQuantity = report.ReceivedQuantity > 0 ? report.ReceivedQuantity : report.TestedMaterialWeight;
            }

            var hist = new FormApprovalHistory
            {
                FormType = "LaboratoryRawMaterial",
                FormId = report.Id,
                PONumber = report.PONumber ?? string.Empty,
                TransactionOrMovementNumber = $"REF-#{report.SampleCode}",
                PreviousStatus = "PENDING_CHEMICAL",
                NewStatus = report.OverallStatus,
                Action = isCompliant ? "DUAL_APPROVED" : "CHEMICAL_REJECTED",
                ActorName = userName,
                ActorUserId = userId,
                ActorRole = role,
                Comment = isCompliant 
                    ? $"تم فحص المادة بالمعمل الكيميائي ومطابقتها للمواصفات. تم اعتماد المادة رسمياً (Dual Approval Gate Passed) وأصبحت جاهزة للاستلام في المستودع."
                    : $"تم فحص المادة بالمعمل الكيميائي وتبيّن عدم مطابقتها للمواصفات. تم حظر المادة ومنع توريدها.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(hist);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            // Real-time broadcast
            try
            {
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    Department = "Laboratory",
                    Action = isCompliant ? "LAB_DUAL_APPROVED" : "LAB_CHEMICAL_REJECTED",
                    ReportId = report.Id,
                    SampleCode = report.SampleCode,
                    Status = report.OverallStatus,
                    Timestamp = DateTime.UtcNow
                });
                if (isCompliant)
                {
                    await _hubContext.Clients.All.SendAsync("OnWarehouseInventoryChanged", new
                    {
                        Action = "LAB_MATERIAL_QUALIFIED",
                        SampleCode = report.SampleCode,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception exSignal)
            {
                _logger.LogWarning(exSignal, "SignalR notification failed for chemical inspection");
            }

            var resultDto = MapRawMaterialToDto(report);
            var msg = isCompliant
                ? $"تم حفظ واعتماد الفحص الكيميائي للمرجع {report.SampleCode} بنجاح. المادة أصبحت مطابقة ومعتمدة بالكامل ومتاحة للاستلام في المستودع."
                : $"تم تسجيل نتيجة الفحص الكيميائي كـ (غير مطابق) للمرجع {report.SampleCode}. تم حظر المادة ومنع توريدها للمستودع.";

            return Ok(ApiResponse<RawMaterialReportDto>.Ok(resultDto, msg));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Failed to submit Chemical Laboratory inspection.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ الفحص الكيميائي: " + ex.Message));
        }
    }

    // =========================================================================
    // 5. CARD TWO: FINAL PRODUCT CONFORMITY CERTIFICATE (شهادة مطابقة المنتج النهائي)
    // =========================================================================
    [HttpGet("final-certificates")]
    [HttpGet("final-conformity-certificates")]
    [ProducesResponseType(typeof(ApiResponse<List<FinalConformityCertificateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinalCertificates(
        [FromQuery] string? section = null,
        [FromQuery] string? decision = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var query = _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .Include(c => c.RawMaterialReport)
            .AsQueryable();

        if (AppRoles.HasPhysicalLabAccess(role) && !AppRoles.HasChemicalLabAccess(role))
        {
            query = query.Where(c => c.LaboratorySection == "PHYSICAL");
        }
        else if (AppRoles.HasChemicalLabAccess(role) && !AppRoles.HasPhysicalLabAccess(role))
        {
            query = query.Where(c => c.LaboratorySection == "CHEMICAL");
        }
        else if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.LaboratorySection == section.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(decision) && !decision.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.ConformityDecision == decision.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.ApprovalStatus == status.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.CertificateNumber.ToLower().Contains(s) ||
                c.ProductionReferenceNumber.ToString().Contains(s) ||
                c.FinalProductName.ToLower().Contains(s) ||
                c.ProductionBatchNumber.ToLower().Contains(s) ||
                c.ResponsibleEmployee.ToLower().Contains(s));
        }

        var certs = await query.OrderByDescending(c => c.Id).Take(200).ToListAsync();
        var dtos = certs.Select(MapFinalCertToDto).ToList();
        return Ok(ApiResponse<List<FinalConformityCertificateDto>>.Ok(dtos));
    }

    [HttpGet("final-certificates/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<FinalConformityCertificateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFinalCertificateById(int id)
    {
        var cert = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cert == null)
        {
            return NotFound(ApiResponse.Fail($"شهادة مطابقة المنتج النهائي #{id} غير موجودة."));
        }

        return Ok(ApiResponse<FinalConformityCertificateDto>.Ok(MapFinalCertToDto(cert)));
    }

    [HttpGet("eligible-finished-products")]
    [ProducesResponseType(typeof(ApiResponse<List<EligibleFinishedProductForLabDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleFinishedProducts(
        [FromQuery] string? status = null, 
        [FromQuery] string? search = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        var query = _db.ProductionFinishedProducts
            .AsNoTracking()
            .Include(f => f.ProductionProcess)
                .ThenInclude(p => p.Materials)
            .Include(f => f.ProductionProcess)
                .ThenInclude(p => p.ProductionOrder)
                    .ThenInclude(o => o.Transfers)
            .Include(f => f.ProductionProcess)
                .ThenInclude(p => p.ProductionOrder)
                    .ThenInclude(o => o.Items)
            .Include(f => f.ProductionOrder)
                .ThenInclude(o => o.Transfers)
            .Include(f => f.ProductionOrder)
                .ThenInclude(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(f =>
                f.FinishedProductName.ToLower().Contains(s) ||
                f.ManufactureNumber.ToLower().Contains(s) ||
                (f.ProductionProcess != null && f.ProductionProcess.ProcessNumber.ToLower().Contains(s)) ||
                (f.ProductionOrderNumber != null && f.ProductionOrderNumber.ToLower().Contains(s)));
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(f => f.ProductionDate >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            var endOfDay = dateTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(f => f.ProductionDate <= endOfDay);
        }

        var list = await query
            .OrderByDescending(f => f.ProductionDate)
            .ThenByDescending(f => f.Id)
            .Take(1000)
            .ToListAsync();

        var prodIds = list.Select(f => f.Id).ToList();
        var certs = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .Where(c => c.ProductionFinishedProductId.HasValue && prodIds.Contains(c.ProductionFinishedProductId.Value))
            .ToListAsync();
        var certsByProdId = certs.GroupBy(c => c.ProductionFinishedProductId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First());

        var filterStatus = !string.IsNullOrWhiteSpace(status) ? status.Trim().ToUpperInvariant() : null;

        var dtos = list.Select(f =>
        {
            var proc = f.ProductionProcess;
            var mats = proc?.Materials?.ToList() ?? new List<ProductionProcessMaterial>();
            var orderTransfers = (f.ProductionOrder?.Transfers ?? proc?.ProductionOrder?.Transfers)?.ToList() ?? new List<ProductionOrderTransfer>();
            var orderItems = (f.ProductionOrder?.Items ?? proc?.ProductionOrder?.Items)?.ToList() ?? new List<ProductionOrderItem>();

            var refSet = new HashSet<int>();
            foreach (var m in mats) { if (m.ProductionReferenceNumber > 0) refSet.Add(m.ProductionReferenceNumber); }
            foreach (var t in orderTransfers) { if (t.ProductionReferenceNumber.HasValue && t.ProductionReferenceNumber.Value > 0) refSet.Add(t.ProductionReferenceNumber.Value); }
            foreach (var i in orderItems) { if (i.ProductionReferenceNumber.HasValue && i.ProductionReferenceNumber.Value > 0) refSet.Add(i.ProductionReferenceNumber.Value); }
            var refNumbers = refSet.ToList();

            var batchSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in mats) { if (!string.IsNullOrWhiteSpace(m.BatchNumber)) batchSet.Add(m.BatchNumber); }
            foreach (var t in orderTransfers) { if (!string.IsNullOrWhiteSpace(t.BatchNumber)) batchSet.Add(t.BatchNumber); }
            var batchNumbers = batchSet.ToList();

            var targetQty = f.ProductionOrder?.RequestedQuantity 
                            ?? proc?.ProductionOrder?.RequestedQuantity 
                            ?? proc?.TotalInputQuantity 
                            ?? f.ManufacturedQuantity;

            LaboratoryFinalConformityCertificate? linkedCert = null;
            if (certsByProdId.TryGetValue(f.Id, out var cVal))
            {
                linkedCert = cVal;
            }

            var physStatus = linkedCert != null ? linkedCert.PhysicalApprovalStatus : (f.LaboratoryConformityStatus == "APPROVED" ? "APPROVED" : "PENDING");
            var chemStatus = linkedCert != null ? linkedCert.ChemicalApprovalStatus : (f.LaboratoryConformityStatus == "APPROVED" ? "APPROVED" : "PENDING");
            var overallSt = linkedCert != null 
                ? linkedCert.OverallStatus 
                : (f.LaboratoryConformityStatus == "APPROVED" 
                    ? "APPROVED" 
                    : (f.LaboratoryConformityStatus == "REJECTED" 
                        ? "REJECTED" 
                        : (physStatus == "APPROVED" ? "PENDING_CHEMICAL" : "PENDING_PHYSICAL")));

            if (!string.IsNullOrWhiteSpace(filterStatus) && !filterStatus.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                if (filterStatus == "PENDING_PHYSICAL" || filterStatus == "PENDING_INSPECTION")
                {
                    if (overallSt == "APPROVED" || overallSt == "REJECTED" || physStatus == "APPROVED") return null;
                }
                else if (filterStatus == "PENDING_CHEMICAL")
                {
                    if (overallSt == "APPROVED" || overallSt == "REJECTED" || physStatus != "APPROVED" || chemStatus == "APPROVED") return null;
                }
                else if (filterStatus == "APPROVED")
                {
                    if (overallSt != "APPROVED") return null;
                }
                else if (filterStatus == "REJECTED")
                {
                    if (overallSt != "REJECTED") return null;
                }
            }

            return new EligibleFinishedProductForLabDto
            {
                FinishedProductId = f.Id,
                ProductionProcessId = f.ProductionProcessId,
                ProcessNumber = proc?.ProcessNumber ?? string.Empty,
                ProductionOrderId = f.ProductionOrderId ?? proc?.ProductionOrderId,
                ProductionOrderNumber = f.ProductionOrderNumber ?? proc?.ProductionOrderNumber,
                FinishedProductName = f.FinishedProductName,
                ManufactureNumber = f.ManufactureNumber,
                ManufacturedQuantity = f.ManufacturedQuantity,
                Unit = f.Unit ?? "طن",
                TargetQuantity = targetQty,
                TotalInputQuantity = proc?.TotalInputQuantity ?? 0m,
                TotalWasteQuantity = proc?.TotalWasteQuantity ?? 0m,
                WastePercentage = proc?.WastePercentage ?? 0m,
                DestinationStorageLocationId = f.DestinationStorageLocationId,
                DestinationStorageLocationName = f.DestinationStorageLocationName,
                ProductionDate = f.ProductionDate,
                ResponsibleEmployee = proc?.ResponsibleEmployee ?? f.CreatedBy,
                Notes = f.Notes,
                WeighbridgeAttachmentPath = f.WeighbridgeAttachmentPath,
                LaboratoryConformityStatus = string.IsNullOrWhiteSpace(f.LaboratoryConformityStatus) ? "PENDING_PHYSICAL" : f.LaboratoryConformityStatus,
                LaboratoryCertificateId = linkedCert?.Id ?? f.LaboratoryCertificateId,
                LaboratoryCertificateNumber = linkedCert?.CertificateNumber ?? f.LaboratoryCertificateNumber,
                LaboratoryCertifiedAt = f.LaboratoryCertifiedAt,
                LaboratoryCertifiedBy = f.LaboratoryCertifiedBy,
                LaboratoryRejectionReason = linkedCert?.RejectionReason ?? f.LaboratoryRejectionReason,
                LaboratoryCorrectiveAction = linkedCert?.CorrectiveAction ?? f.LaboratoryCorrectiveAction,
                PhysicalApprovalStatus = physStatus,
                PhysicalApprovedBy = linkedCert?.PhysicalApprovedBy,
                PhysicalApprovedAt = linkedCert?.PhysicalApprovedAt,
                PhysicalNotes = linkedCert?.PhysicalNotes,
                PhysicalTestResults = linkedCert?.PhysicalTestResults,
                ChemicalApprovalStatus = chemStatus,
                ChemicalApprovedBy = linkedCert?.ChemicalApprovedBy,
                ChemicalApprovedAt = linkedCert?.ChemicalApprovedAt,
                ChemicalNotes = linkedCert?.ChemicalNotes,
                ChemicalTestResults = linkedCert?.ChemicalTestResults,
                OverallStatus = overallSt,
                AssociatedReferenceNumbers = refNumbers,
                AssociatedBatchNumbers = batchNumbers,
                ConsumedMaterials = mats.Select(m => new EligibleFinishedProductMaterialDto
                {
                    MaterialId = m.Id,
                    MaterialName = m.MaterialName,
                    MaterialClassification = m.MaterialClassification,
                    ProductionReferenceNumber = m.ProductionReferenceNumber,
                    BatchNumber = m.BatchNumber,
                    SampleCode = m.SampleCode,
                    ConsumedQuantity = m.ConsumedQuantity,
                    Unit = m.Unit,
                    StorageLocationName = m.StorageLocationName
                }).ToList()
            };
        }).Where(d => d != null).Select(d => d!).ToList();

        return Ok(ApiResponse<List<EligibleFinishedProductForLabDto>>.Ok(dtos));
    }

    [HttpGet("finished-products/{id:int}/context")]
    [ProducesResponseType(typeof(ApiResponse<EligibleFinishedProductForLabDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFinishedProductContext(int id)
    {
        var f = await _db.ProductionFinishedProducts
            .AsNoTracking()
            .Include(f => f.ProductionProcess)
                .ThenInclude(p => p.Materials)
            .Include(f => f.ProductionProcess)
                .ThenInclude(p => p.ProductionOrder)
                    .ThenInclude(o => o.Transfers)
            .Include(f => f.ProductionProcess)
                .ThenInclude(p => p.ProductionOrder)
                    .ThenInclude(o => o.Items)
            .Include(f => f.ProductionOrder)
                .ThenInclude(o => o.Transfers)
            .Include(f => f.ProductionOrder)
                .ThenInclude(o => o.Items)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (f == null)
        {
            return NotFound(ApiResponse.Fail($"المنتج النهائي #{id} غير موجود في قاعدة البيانات."));
        }

        var proc = f.ProductionProcess;
        var mats = proc?.Materials?.ToList() ?? new List<ProductionProcessMaterial>();
        var orderTransfers = (f.ProductionOrder?.Transfers ?? proc?.ProductionOrder?.Transfers)?.ToList() ?? new List<ProductionOrderTransfer>();
        var orderItems = (f.ProductionOrder?.Items ?? proc?.ProductionOrder?.Items)?.ToList() ?? new List<ProductionOrderItem>();

        var refSet = new HashSet<int>();
        foreach (var m in mats) { if (m.ProductionReferenceNumber > 0) refSet.Add(m.ProductionReferenceNumber); }
        foreach (var t in orderTransfers) { if (t.ProductionReferenceNumber.HasValue && t.ProductionReferenceNumber.Value > 0) refSet.Add(t.ProductionReferenceNumber.Value); }
        foreach (var i in orderItems) { if (i.ProductionReferenceNumber.HasValue && i.ProductionReferenceNumber.Value > 0) refSet.Add(i.ProductionReferenceNumber.Value); }
        var refNumbers = refSet.ToList();

        var batchSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in mats) { if (!string.IsNullOrWhiteSpace(m.BatchNumber)) batchSet.Add(m.BatchNumber); }
        foreach (var t in orderTransfers) { if (!string.IsNullOrWhiteSpace(t.BatchNumber)) batchSet.Add(t.BatchNumber); }
        var batchNumbers = batchSet.ToList();

        var targetQty = f.ProductionOrder?.RequestedQuantity 
                        ?? proc?.ProductionOrder?.RequestedQuantity 
                        ?? proc?.TotalInputQuantity 
                        ?? f.ManufacturedQuantity;

        var linkedCert = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .Where(c => c.ProductionFinishedProductId == f.Id)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync();

        var physStatus = linkedCert != null ? linkedCert.PhysicalApprovalStatus : (f.LaboratoryConformityStatus == "APPROVED" ? "APPROVED" : "PENDING");
        var chemStatus = linkedCert != null ? linkedCert.ChemicalApprovalStatus : (f.LaboratoryConformityStatus == "APPROVED" ? "APPROVED" : "PENDING");
        var overallSt = linkedCert != null 
            ? linkedCert.OverallStatus 
            : (f.LaboratoryConformityStatus == "APPROVED" 
                ? "APPROVED" 
                : (f.LaboratoryConformityStatus == "REJECTED" 
                    ? "REJECTED" 
                    : (physStatus == "APPROVED" ? "PENDING_CHEMICAL" : "PENDING_PHYSICAL")));

        var dto = new EligibleFinishedProductForLabDto
        {
            FinishedProductId = f.Id,
            ProductionProcessId = f.ProductionProcessId,
            ProcessNumber = proc?.ProcessNumber ?? string.Empty,
            ProductionOrderId = f.ProductionOrderId ?? proc?.ProductionOrderId,
            ProductionOrderNumber = f.ProductionOrderNumber ?? proc?.ProductionOrderNumber,
            FinishedProductName = f.FinishedProductName,
            ManufactureNumber = f.ManufactureNumber,
            ManufacturedQuantity = f.ManufacturedQuantity,
            Unit = f.Unit ?? "طن",
            TargetQuantity = targetQty,
            TotalInputQuantity = proc?.TotalInputQuantity ?? 0m,
            TotalWasteQuantity = proc?.TotalWasteQuantity ?? 0m,
            WastePercentage = proc?.WastePercentage ?? 0m,
            DestinationStorageLocationId = f.DestinationStorageLocationId,
            DestinationStorageLocationName = f.DestinationStorageLocationName,
            ProductionDate = f.ProductionDate,
            ResponsibleEmployee = proc?.ResponsibleEmployee ?? f.CreatedBy,
            Notes = f.Notes,
            WeighbridgeAttachmentPath = f.WeighbridgeAttachmentPath,
            LaboratoryConformityStatus = string.IsNullOrWhiteSpace(f.LaboratoryConformityStatus) ? "PENDING_PHYSICAL" : f.LaboratoryConformityStatus,
            LaboratoryCertificateId = linkedCert?.Id ?? f.LaboratoryCertificateId,
            LaboratoryCertificateNumber = linkedCert?.CertificateNumber ?? f.LaboratoryCertificateNumber,
            LaboratoryCertifiedAt = f.LaboratoryCertifiedAt,
            LaboratoryCertifiedBy = f.LaboratoryCertifiedBy,
            LaboratoryRejectionReason = linkedCert?.RejectionReason ?? f.LaboratoryRejectionReason,
            LaboratoryCorrectiveAction = linkedCert?.CorrectiveAction ?? f.LaboratoryCorrectiveAction,
            PhysicalApprovalStatus = physStatus,
            PhysicalApprovedBy = linkedCert?.PhysicalApprovedBy,
            PhysicalApprovedAt = linkedCert?.PhysicalApprovedAt,
            PhysicalNotes = linkedCert?.PhysicalNotes,
            PhysicalTestResults = linkedCert?.PhysicalTestResults,
            ChemicalApprovalStatus = chemStatus,
            ChemicalApprovedBy = linkedCert?.ChemicalApprovedBy,
            ChemicalApprovedAt = linkedCert?.ChemicalApprovedAt,
            ChemicalNotes = linkedCert?.ChemicalNotes,
            ChemicalTestResults = linkedCert?.ChemicalTestResults,
            OverallStatus = overallSt,
            AssociatedReferenceNumbers = refNumbers,
            AssociatedBatchNumbers = batchNumbers,
            ConsumedMaterials = mats.Select(m => new EligibleFinishedProductMaterialDto
            {
                MaterialId = m.Id,
                MaterialName = m.MaterialName,
                MaterialClassification = m.MaterialClassification,
                ProductionReferenceNumber = m.ProductionReferenceNumber,
                BatchNumber = m.BatchNumber,
                SampleCode = m.SampleCode,
                ConsumedQuantity = m.ConsumedQuantity,
                Unit = m.Unit,
                StorageLocationName = m.StorageLocationName
            }).ToList()
        };

        return Ok(ApiResponse<EligibleFinishedProductForLabDto>.Ok(dto));
    }

    [HttpGet("final-certificates/by-finished-product/{finishedProductId:int}")]
    [ProducesResponseType(typeof(ApiResponse<FinalConformityCertificateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCertificateByFinishedProduct(int finishedProductId)
    {
        var cert = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .Include(c => c.RawMaterialReport)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(c => c.ProductionFinishedProductId == finishedProductId);

        if (cert == null)
        {
            var pfp = await _db.ProductionFinishedProducts.AsNoTracking().FirstOrDefaultAsync(f => f.Id == finishedProductId);
            if (pfp != null && !string.IsNullOrEmpty(pfp.ManufactureNumber))
            {
                cert = await _db.LaboratoryFinalConformityCertificates
                    .AsNoTracking()
                    .Include(c => c.RawMaterialReport)
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync(c => c.ManufactureNumber == pfp.ManufactureNumber);
            }
        }

        if (cert == null)
        {
            return NotFound(ApiResponse.Fail($"لا توجد شهادة مطابقة مخبرية مسجلة لهذا المنتج النهائي #{finishedProductId}."));
        }

        return Ok(ApiResponse<FinalConformityCertificateDto>.Ok(MapFinalCertToDto(cert)));
    }

    /// <summary>
    /// المرحلة الأولى: اعتماد المعمل الفيزيائي لشهادة مطابقة المنتج النهائي
    /// Physical Laboratory Specialist Approval
    /// </summary>
    [HttpPost("final-certificates/physical-approval")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(ApiResponse<FinalConformityCertificateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FinalConformityCertificateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitFinalPhysicalApproval()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        if (!AppRoles.HasPhysicalLabAccess(role) && !AppRoles.HasLaboratoryAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: اعتماد الفحص الفيزيائي مقتصر على أخصائي المعمل الفيزيائي المعتمد."));
        }

        var dto = await ParseFinalCertificateRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات فحص المعمل الفيزيائي غير صالحة."));
        }

        ProductionFinishedProduct? finishedProd = null;
        if (dto.ProductionFinishedProductId.HasValue && dto.ProductionFinishedProductId.Value > 0)
        {
            finishedProd = await _db.ProductionFinishedProducts
                .Include(f => f.ProductionProcess)
                    .ThenInclude(p => p.Materials)
                .Include(f => f.ProductionProcess)
                    .ThenInclude(p => p.ProductionOrder)
                .Include(f => f.ProductionOrder)
                .FirstOrDefaultAsync(f => f.Id == dto.ProductionFinishedProductId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(dto.ManufactureNumber))
        {
            finishedProd = await _db.ProductionFinishedProducts
                .Include(f => f.ProductionProcess)
                    .ThenInclude(p => p.Materials)
                .Include(f => f.ProductionProcess)
                    .ThenInclude(p => p.ProductionOrder)
                .Include(f => f.ProductionOrder)
                .FirstOrDefaultAsync(f => f.ManufactureNumber.ToLower() == dto.ManufactureNumber.Trim().ToLower());
        }

        if (finishedProd == null)
        {
            return BadRequest(ApiResponse.Fail("المنتج النهائي المطلوب فحصه غير موجود بقاعدة البيانات."));
        }

        var inputDecision = (dto.ConformityDecision ?? "APPROVED").Trim().ToUpperInvariant();
        bool isApproved = inputDecision == "APPROVED" || inputDecision == "CONFORMS" || inputDecision.Contains("مطابق");
        string physDecision = isApproved ? "APPROVED" : "REJECTED";

        if (!isApproved && string.IsNullOrWhiteSpace(dto.RejectionReason))
        {
            return BadRequest(ApiResponse.Fail("يرجى توضيح سبب الرفض الإلزامي للمنتج غير المطابق فيزيائياً."));
        }

        var attachRes = await ValidateAndSaveAttachmentAsync(dto.Attachment, dto.AttachmentPath, "مرفق الفحص الفيزيائي", isRequired: false);
        if (!attachRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attachRes.ErrorMessage ?? "مرفق الفحص الفيزيائي غير صالح."));
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            LaboratoryFinalConformityCertificate? cert = null;
            if (finishedProd.LaboratoryCertificateId.HasValue && finishedProd.LaboratoryCertificateId.Value > 0)
            {
                cert = await _db.LaboratoryFinalConformityCertificates.FirstOrDefaultAsync(c => c.Id == finishedProd.LaboratoryCertificateId.Value);
            }
            if (cert == null)
            {
                cert = await _db.LaboratoryFinalConformityCertificates.FirstOrDefaultAsync(c => c.ProductionFinishedProductId == finishedProd.Id);
            }

            var physicalResults = !string.IsNullOrWhiteSpace(dto.PhysicalTestResults) ? dto.PhysicalTestResults : dto.FinalTestResults;
            var physicalNotes = !string.IsNullOrWhiteSpace(dto.PhysicalNotes) ? dto.PhysicalNotes : dto.InspectorNotes;

            var qualityDict = new Dictionary<string, string>
            {
                ["finalTestResults"] = physicalResults ?? string.Empty,
                ["inspectorNotes"] = physicalNotes ?? string.Empty,
                ["physicalApprovedBy"] = userName,
                ["physicalApprovedAt"] = DateTime.UtcNow.ToString("o")
            };

            if (cert == null)
            {
                var certIndex = await _db.LaboratoryFinalConformityCertificates.CountAsync() + 1;
                var certNumber = $"CERT-{DateTime.UtcNow:yyyy}-{certIndex:D4}";

                cert = new LaboratoryFinalConformityCertificate
                {
                    CertificateNumber = certNumber,
                    ProductionReferenceNumber = dto.ProductionReferenceNumber ?? (finishedProd.Id > 0 ? finishedProd.Id : 1),
                    LaboratorySection = "PHYSICAL",
                    FinalProductName = finishedProd.FinishedProductName,
                    ActualFinalProductQuantity = finishedProd.ManufacturedQuantity,
                    QuantityUnit = finishedProd.Unit ?? "طن",
                    ProductionBatchNumber = finishedProd.ManufactureNumber,
                    ProductionProcessId = finishedProd.ProductionProcessId,
                    ProductionProcessNumber = finishedProd.ProductionProcess?.ProcessNumber,
                    ProductionFinishedProductId = finishedProd.Id,
                    ManufactureNumber = finishedProd.ManufactureNumber,
                    ProductionOrderId = finishedProd.ProductionOrderId ?? finishedProd.ProductionProcess?.ProductionOrderId,
                    ProductionOrderNumber = finishedProd.ProductionOrderNumber ?? finishedProd.ProductionProcess?.ProductionOrderNumber,
                    TargetQuantity = finishedProd.ProductionOrder?.RequestedQuantity ?? finishedProd.ManufacturedQuantity,
                    AssociatedReferenceNumbers = dto.AssociatedReferenceNumbers,
                    ProductionCompletionDateTime = finishedProd.ProductionDate,
                    ActualRawMaterialInputQuantity = finishedProd.ProductionProcess?.TotalInputQuantity ?? finishedProd.ManufacturedQuantity,
                    CalculatedDifference = (finishedProd.ProductionProcess?.TotalInputQuantity ?? finishedProd.ManufacturedQuantity) - finishedProd.ManufacturedQuantity,
                    PhysicalApprovalStatus = physDecision,
                    PhysicalApprovedBy = userName,
                    PhysicalApprovedByUserId = userId,
                    PhysicalApprovedAt = DateTime.UtcNow,
                    PhysicalNotes = physicalNotes,
                    PhysicalTestResults = physicalResults,
                    ChemicalApprovalStatus = "PENDING",
                    OverallStatus = isApproved ? "PENDING_CHEMICAL" : "REJECTED",
                    ApprovalStatus = isApproved ? "PENDING" : "REJECTED",
                    ConformityDecision = isApproved ? "PENDING_TESTING" : "NON_CONFORMING",
                    RejectionReason = !isApproved ? (dto.RejectionReason ?? "غير مطابق فيزيائياً") : null,
                    CorrectiveAction = !isApproved ? dto.CorrectiveAction : null,
                    NonConformityAction = !isApproved ? dto.NonConformityAction : null,
                    SpecificationResultsJson = physicalResults,
                    QualityParametersJson = System.Text.Json.JsonSerializer.Serialize(qualityDict),
                    Observations = physicalNotes,
                    AttachmentPath = !string.IsNullOrWhiteSpace(attachRes.StoredFileName) ? $"/uploads/laboratory/{attachRes.StoredFileName}" : null,
                    AttachmentFileName = attachRes.OriginalFileName,
                    AttachmentFileType = attachRes.ContentType,
                    AttachmentFileSize = attachRes.FileSize,
                    ResponsibleEmployee = userName,
                    ResponsibleUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                };

                await _db.LaboratoryFinalConformityCertificates.AddAsync(cert);
            }
            else
            {
                if (cert.OverallStatus == "APPROVED")
                {
                    return BadRequest(ApiResponse.Fail("تم اعتماد هذه الشهادة رسمياً مسبقاً ولا يمكن تعديل الفحص الفيزيائي."));
                }

                cert.PhysicalApprovalStatus = physDecision;
                cert.PhysicalApprovedBy = userName;
                cert.PhysicalApprovedByUserId = userId;
                cert.PhysicalApprovedAt = DateTime.UtcNow;
                cert.PhysicalNotes = physicalNotes;
                cert.PhysicalTestResults = physicalResults;
                cert.SpecificationResultsJson = physicalResults;
                cert.QualityParametersJson = System.Text.Json.JsonSerializer.Serialize(qualityDict);
                cert.Observations = physicalNotes;

                if (isApproved)
                {
                    cert.OverallStatus = "PENDING_CHEMICAL";
                    cert.ApprovalStatus = "PENDING";
                    cert.ConformityDecision = "PENDING_TESTING";
                    cert.RejectionReason = null;
                    cert.CorrectiveAction = null;
                }
                else
                {
                    cert.ChemicalApprovalStatus = "NOT_APPLICABLE";
                    cert.OverallStatus = "REJECTED";
                    cert.ApprovalStatus = "REJECTED";
                    cert.ConformityDecision = "NON_CONFORMING";
                    cert.RejectionReason = dto.RejectionReason ?? "غير مطابق فيزيائياً";
                    cert.CorrectiveAction = dto.CorrectiveAction;
                    cert.NonConformityAction = dto.NonConformityAction;
                }

                if (!string.IsNullOrWhiteSpace(attachRes.StoredFileName))
                {
                    cert.AttachmentPath = $"/uploads/laboratory/{attachRes.StoredFileName}";
                    cert.AttachmentFileName = attachRes.OriginalFileName;
                    cert.AttachmentFileType = attachRes.ContentType;
                    cert.AttachmentFileSize = attachRes.FileSize;
                }
            }

            await _db.SaveChangesAsync();

            finishedProd.LaboratoryCertificateId = cert.Id;
            finishedProd.LaboratoryCertificateNumber = cert.CertificateNumber;
            finishedProd.LaboratoryConformityStatus = isApproved ? "PENDING_CHEMICAL" : "REJECTED";
            finishedProd.LaboratoryNotes = cert.Observations;
            finishedProd.LaboratoryQualityParametersJson = cert.QualityParametersJson;

            var wb = await _db.WarehouseBatches.FirstOrDefaultAsync(b => b.BatchNumber == finishedProd.ManufactureNumber);
            if (!isApproved)
            {
                finishedProd.LaboratoryRejectionReason = cert.RejectionReason;
                finishedProd.LaboratoryCorrectiveAction = cert.CorrectiveAction;
                if (wb != null) wb.Status = "REJECTED";
            }
            else
            {
                finishedProd.LaboratoryRejectionReason = null;
                finishedProd.LaboratoryCorrectiveAction = null;
                if (wb != null) wb.Status = "PENDING_LAB_INSPECTION";
            }

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return Ok(ApiResponse<FinalConformityCertificateDto>.Ok(MapFinalCertToDto(cert), isApproved 
                ? "تم اعتماد الفحص الفيزيائي للمنتج بنجاح! تم تحويل الشهادة إلى المعمل الكيميائي (المرحلة الثانية)."
                : "تم تسجيل قرار الرفض الفيزيائي للمنتج وإيقاف دورة الاعتماد."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error submitting laboratory final physical approval.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ الفحص الفيزيائي: " + ex.Message));
        }
    }

    /// <summary>
    /// المرحلة الثانية: اعتماد المعمل الكيميائي لشهادة مطابقة المنتج النهائي
    /// Chemical Laboratory Specialist Confirmation
    /// </summary>
    [HttpPost("final-certificates/chemical-approval")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(ApiResponse<FinalConformityCertificateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitFinalChemicalApproval()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        if (!AppRoles.HasChemicalLabAccess(role) && !AppRoles.HasLaboratoryAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: اعتماد الفحص الكيميائي مقتصر على أخصائي المعمل الكيميائي المعتمد."));
        }

        var form = await Request.ReadFormAsync();
        int certId = 0;
        int finishedProdId = 0;
        int.TryParse(form["certificateId"].FirstOrDefault(), out certId);
        int.TryParse(form["productionFinishedProductId"].FirstOrDefault(), out finishedProdId);

        LaboratoryFinalConformityCertificate? cert = null;
        if (certId > 0)
        {
            cert = await _db.LaboratoryFinalConformityCertificates.FirstOrDefaultAsync(c => c.Id == certId);
        }
        if (cert == null && finishedProdId > 0)
        {
            cert = await _db.LaboratoryFinalConformityCertificates.FirstOrDefaultAsync(c => c.ProductionFinishedProductId == finishedProdId);
        }

        if (cert == null)
        {
            return NotFound(ApiResponse.Fail("شهادة المطابقة غير موجودة. يرجى إتمام واعتماد الفحص الفيزيائي أولاً."));
        }

        // CRITICAL WORKFLOW ENFORCEMENT:
        // Physical Laboratory must be approved first!
        if (cert.PhysicalApprovalStatus != "APPROVED")
        {
            if (cert.PhysicalApprovalStatus == "REJECTED")
            {
                return BadRequest(ApiResponse.Fail("غير مصرح: لا يمكن اعتماد الفحص الكيميائي لمنتج تم رفضه مسبقاً في المرحلة الفيزيائية."));
            }
            return BadRequest(ApiResponse.Fail("غير مصرح: لا يمكن اعتماد الفحص الكيميائي قبل إتمام واعتماد الفحص الفيزيائي أولاً (Physical Approval Required)."));
        }

        if (cert.OverallStatus == "APPROVED")
        {
            return BadRequest(ApiResponse.Fail("تم اعتماد الشهادة مسبقاً بشكل نهائي ولا يمكن تكرار الاعتماد."));
        }

        var decision = (form["decision"].FirstOrDefault() ?? form["conformityDecision"].FirstOrDefault() ?? "APPROVED").Trim().ToUpperInvariant();
        bool isApproved = decision == "APPROVED" || decision == "CONFORMS" || decision.Contains("مطابق");
        string chemDecision = isApproved ? "APPROVED" : "REJECTED";

        var chemicalTestResults = form["chemicalTestResults"].FirstOrDefault()?.Trim();
        var chemicalNotes = form["chemicalNotes"].FirstOrDefault()?.Trim() ?? form["notes"].FirstOrDefault()?.Trim();
        var rejectionReason = form["rejectionReason"].FirstOrDefault()?.Trim();
        var correctiveAction = form["correctiveAction"].FirstOrDefault()?.Trim();
        var nonConfAction = form["nonConformityAction"].FirstOrDefault()?.Trim();

        if (!isApproved && string.IsNullOrWhiteSpace(rejectionReason))
        {
            return BadRequest(ApiResponse.Fail("يرجى توضيح سبب الرفض الإلزامي للمنتج غير المطابق كيميائياً."));
        }

        var attachFile = form.Files.GetFile("attachment");
        var attachRes = await ValidateAndSaveAttachmentAsync(attachFile, form["attachmentPath"].FirstOrDefault(), "مرفق الفحص الكيميائي", isRequired: false);
        if (!attachRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attachRes.ErrorMessage ?? "مرفق الفحص الكيميائي غير صالح."));
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            cert.ChemicalApprovalStatus = chemDecision;
            cert.ChemicalApprovedBy = userName;
            cert.ChemicalApprovedByUserId = userId;
            cert.ChemicalApprovedAt = DateTime.UtcNow;
            cert.ChemicalNotes = chemicalNotes;
            cert.ChemicalTestResults = chemicalTestResults;

            // Merge into QualityParametersJson
            var qualityMap = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(cert.QualityParametersJson))
            {
                try
                {
                    qualityMap = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(cert.QualityParametersJson) ?? new Dictionary<string, string>();
                }
                catch { }
            }
            qualityMap["chemicalTestResults"] = chemicalTestResults ?? string.Empty;
            qualityMap["chemicalNotes"] = chemicalNotes ?? string.Empty;
            qualityMap["chemicalApprovedBy"] = userName;
            qualityMap["chemicalApprovedAt"] = DateTime.UtcNow.ToString("o");
            cert.QualityParametersJson = System.Text.Json.JsonSerializer.Serialize(qualityMap);

            if (isApproved)
            {
                // BOTH STAGES ARE APPROVED! Final Certificate is officially Approved!
                cert.OverallStatus = "APPROVED";
                cert.ApprovalStatus = "APPROVED";
                cert.ConformityDecision = "CONFORMS";
                cert.ReviewedBy = userName;
                cert.ReviewedByUserId = userId;
                cert.ReviewedAt = DateTime.UtcNow;
                cert.RejectionReason = null;
                cert.CorrectiveAction = null;
            }
            else
            {
                cert.OverallStatus = "REJECTED";
                cert.ApprovalStatus = "REJECTED";
                cert.ConformityDecision = "NON_CONFORMING";
                cert.RejectionReason = rejectionReason ?? "غير مطابق كيميائياً";
                cert.CorrectiveAction = correctiveAction;
                cert.NonConformityAction = nonConfAction;
                cert.ReviewedBy = userName;
                cert.ReviewedAt = DateTime.UtcNow;
            }

            if (!string.IsNullOrWhiteSpace(attachRes.StoredFileName))
            {
                cert.AttachmentPath = $"/uploads/laboratory/{attachRes.StoredFileName}";
                cert.AttachmentFileName = attachRes.OriginalFileName;
                cert.AttachmentFileType = attachRes.ContentType;
                cert.AttachmentFileSize = attachRes.FileSize;
            }

            await _db.SaveChangesAsync();

            var finishedProd = await _db.ProductionFinishedProducts.FirstOrDefaultAsync(f => f.Id == cert.ProductionFinishedProductId);
            if (finishedProd != null)
            {
                finishedProd.LaboratoryConformityStatus = isApproved ? "APPROVED" : "REJECTED";
                finishedProd.LaboratoryCertificateId = cert.Id;
                finishedProd.LaboratoryCertificateNumber = cert.CertificateNumber;
                finishedProd.LaboratoryNotes = string.Join(" | ", new[] { cert.PhysicalNotes, cert.ChemicalNotes }.Where(s => !string.IsNullOrWhiteSpace(s)));
                finishedProd.LaboratoryQualityParametersJson = cert.QualityParametersJson;

                var wb = await _db.WarehouseBatches.FirstOrDefaultAsync(b => b.BatchNumber == finishedProd.ManufactureNumber);

                if (isApproved)
                {
                    finishedProd.LaboratoryCertifiedAt = DateTime.UtcNow;
                    finishedProd.LaboratoryCertifiedBy = userName;
                    finishedProd.LaboratoryRejectionReason = null;
                    finishedProd.LaboratoryCorrectiveAction = null;
                    if (wb != null)
                    {
                        wb.Status = "APPROVED";
                        wb.IsActive = true;
                    }
                }
                else
                {
                    finishedProd.LaboratoryCertifiedAt = DateTime.UtcNow;
                    finishedProd.LaboratoryCertifiedBy = userName;
                    finishedProd.LaboratoryRejectionReason = cert.RejectionReason;
                    finishedProd.LaboratoryCorrectiveAction = cert.CorrectiveAction;
                    if (wb != null)
                    {
                        wb.Status = "REJECTED";
                    }
                }

                await _db.SaveChangesAsync();
            }

            await dbTransaction.CommitAsync();

            return Ok(ApiResponse<FinalConformityCertificateDto>.Ok(MapFinalCertToDto(cert), isApproved
                ? $"تم اعتماد الفحص الكيميائي بنجاح! اكتملت مرحلتا الاعتماد المخبري وأصبحت الشهادة رقم ({cert.CertificateNumber}) معتمدة رسمياً ومطابقة للمواصفات."
                : $"تم تسجيل قرار الرفض الكيميائي للمنتج رقم ({cert.CertificateNumber})."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error submitting laboratory final chemical approval.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ الفحص الكيميائي: " + ex.Message));
        }
    }

    [HttpPost("final-certificates")]
    [HttpPost("final-conformity-certificates")]
    [ProducesResponseType(typeof(ApiResponse<FinalConformityCertificateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateFinalCertificate()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        if (!AppRoles.HasLaboratoryAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: الوصول مقتصر على موظفي المعمل المعتمدين."));
        }

        var dto = await ParseFinalCertificateRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات شهادة مطابقة المنتج النهائي غير صالحة."));
        }

        if (dto.ApprovalStage == "CHEMICAL")
        {
            return await SubmitFinalChemicalApproval();
        }
        if (dto.ApprovalStage == "PHYSICAL")
        {
            return await SubmitFinalPhysicalApproval();
        }

        // 1. Resolve ProductionFinishedProduct and linked Production Process if provided
        ProductionFinishedProduct? finishedProd = null;
        if (dto.ProductionFinishedProductId.HasValue && dto.ProductionFinishedProductId.Value > 0)
        {
            finishedProd = await _db.ProductionFinishedProducts
                .Include(f => f.ProductionProcess)
                    .ThenInclude(p => p.Materials)
                .Include(f => f.ProductionProcess)
                    .ThenInclude(p => p.ProductionOrder)
                .Include(f => f.ProductionOrder)
                .FirstOrDefaultAsync(f => f.Id == dto.ProductionFinishedProductId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(dto.ManufactureNumber))
        {
            finishedProd = await _db.ProductionFinishedProducts
                .Include(f => f.ProductionProcess)
                    .ThenInclude(p => p.Materials)
                .Include(f => f.ProductionProcess)
                    .ThenInclude(p => p.ProductionOrder)
                .Include(f => f.ProductionOrder)
                .FirstOrDefaultAsync(f => f.ManufactureNumber.ToLower() == dto.ManufactureNumber.Trim().ToLower());
        }

        // If finished product found, populate missing details
        if (finishedProd != null)
        {
            dto.ProductionFinishedProductId = finishedProd.Id;
            dto.ManufactureNumber = finishedProd.ManufactureNumber;
            dto.ProductionProcessId = finishedProd.ProductionProcessId;
            dto.ProductionProcessNumber = finishedProd.ProductionProcess?.ProcessNumber ?? string.Empty;
            dto.ProductionOrderId = finishedProd.ProductionOrderId ?? finishedProd.ProductionProcess?.ProductionOrderId;
            dto.ProductionOrderNumber = finishedProd.ProductionOrderNumber ?? finishedProd.ProductionProcess?.ProductionOrderNumber;

            if (string.IsNullOrWhiteSpace(dto.FinalProductName))
                dto.FinalProductName = finishedProd.FinishedProductName;

            if (!dto.ActualFinalProductQuantity.HasValue || dto.ActualFinalProductQuantity.Value <= 0)
                dto.ActualFinalProductQuantity = finishedProd.ManufacturedQuantity;

            if (!dto.ActualRawMaterialInputQuantity.HasValue || dto.ActualRawMaterialInputQuantity.Value <= 0)
                dto.ActualRawMaterialInputQuantity = finishedProd.ProductionProcess?.TotalInputQuantity ?? finishedProd.ManufacturedQuantity;

            if (!dto.TargetQuantity.HasValue || dto.TargetQuantity.Value <= 0)
            {
                dto.TargetQuantity = finishedProd.ProductionOrder?.RequestedQuantity 
                                     ?? finishedProd.ProductionProcess?.ProductionOrder?.RequestedQuantity 
                                     ?? finishedProd.ProductionProcess?.TotalInputQuantity 
                                     ?? finishedProd.ManufacturedQuantity;
            }

            var refList = finishedProd.ProductionProcess?.Materials?
                .Where(m => m.ProductionReferenceNumber > 0)
                .Select(m => m.ProductionReferenceNumber)
                .Distinct()
                .ToList() ?? new List<int>();

            if ((!dto.ProductionReferenceNumber.HasValue || dto.ProductionReferenceNumber.Value <= 0) && refList.Any())
            {
                dto.ProductionReferenceNumber = refList.First();
            }

            if (string.IsNullOrWhiteSpace(dto.AssociatedReferenceNumbers) && refList.Any())
            {
                dto.AssociatedReferenceNumbers = string.Join(", ", refList);
            }
        }

        // 2. Validate Production Reference Number exists
        int effectiveRefNum = dto.ProductionReferenceNumber.GetValueOrDefault();
        if (effectiveRefNum <= 0)
        {
            if (finishedProd != null)
            {
                effectiveRefNum = finishedProd.Id > 0 ? finishedProd.Id : 1;
            }
            else
            {
                return BadRequest(ApiResponse.Fail("الرقم المرجعي التتبعي للإنتاج مطلوب. يرجى اختيار منتج نهائي صالح أو إدخال الرقم المرجعي."));
            }
        }
        dto.ProductionReferenceNumber = effectiveRefNum;

        var rawReport = await _db.LaboratoryRawMaterialReports
            .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == effectiveRefNum);

        if (rawReport == null && finishedProd?.ProductionProcess?.Materials != null)
        {
            // If rawReport is null but we have a finished product, try to find any raw report matching materials
            var matRefs = finishedProd.ProductionProcess.Materials
                .Where(m => m.ProductionReferenceNumber > 0)
                .Select(m => m.ProductionReferenceNumber)
                .ToList();
            if (matRefs.Any())
            {
                rawReport = await _db.LaboratoryRawMaterialReports
                    .FirstOrDefaultAsync(r => matRefs.Contains(r.ProductionReferenceNumber));
            }
        }

        string assignedSection = AppRoles.GetDefaultLaboratorySection(role);
        string section = rawReport?.LaboratorySection ?? (!string.IsNullOrWhiteSpace(dto.LaboratorySection) ? dto.LaboratorySection.Trim().ToUpperInvariant() : assignedSection);
        if (string.IsNullOrWhiteSpace(section) || (section != "PHYSICAL" && section != "CHEMICAL"))
        {
            section = "PHYSICAL";
        }

        if (assignedSection == "PHYSICAL" && section != "PHYSICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الفيزيائي لا يمكنه فحص منتجات المعمل الكيميائي."));
        }
        if (assignedSection == "CHEMICAL" && section != "CHEMICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الكيميائي لا يمكنه فحص منتجات المعمل الفيزيائي."));
        }

        // 3. Validate Output Fields
        var prodName = (dto.FinalProductName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prodName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج النهائي مطلوب."));
        }

        decimal effectiveActualOutput = dto.ActualFinalProductQuantity ?? (finishedProd?.ManufacturedQuantity ?? 0m);
        decimal effectiveInput = dto.ActualRawMaterialInputQuantity ?? (finishedProd?.ProductionProcess?.TotalInputQuantity ?? effectiveActualOutput);
        decimal effectiveTarget = dto.TargetQuantity ?? effectiveActualOutput;
        var calculatedDiff = effectiveInput - effectiveActualOutput;

        var inputDecision = (dto.ConformityDecision ?? "CONFORMS").Trim().ToUpperInvariant();
        string certConformityDecision;
        string certApprovalStatus;
        string prodStatus;

        if (inputDecision == "APPROVED" || inputDecision == "CONFORMS" || inputDecision.Contains("مطابق"))
        {
            certConformityDecision = "CONFORMS";
            certApprovalStatus = "APPROVED";
            prodStatus = "APPROVED";
        }
        else if (inputDecision == "REJECTED" || inputDecision == "NON_CONFORMING" || inputDecision.Contains("مرفوض") || inputDecision.Contains("غير مطابق"))
        {
            certConformityDecision = "NON_CONFORMING";
            certApprovalStatus = "REJECTED";
            prodStatus = "REJECTED";
        }
        else if (inputDecision == "UNDER_INSPECTION" || inputDecision == "PENDING_TESTING" || inputDecision.Contains("قيد الفحص"))
        {
            certConformityDecision = "PENDING_TESTING";
            certApprovalStatus = "PENDING";
            prodStatus = "UNDER_INSPECTION";
        }
        else
        {
            certConformityDecision = "PENDING_TESTING";
            certApprovalStatus = "PENDING";
            prodStatus = "PENDING_INSPECTION";
        }

        string? nonConfAction = null;
        if (certConformityDecision == "NON_CONFORMING")
        {
            nonConfAction = (dto.NonConformityAction ?? "PENDING_DECISION").Trim().ToUpperInvariant();
            if (nonConfAction != "CLASSIFIED_PRODUCT" && nonConfAction != "REWORK_REMANUFACTURE" && nonConfAction != "PENDING_DECISION")
            {
                nonConfAction = "PENDING_DECISION";
            }
        }

        var attachRes = await ValidateAndSaveAttachmentAsync(dto.Attachment, dto.AttachmentPath, "مرفق شهادة المطابقة");
        if (!attachRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attachRes.ErrorMessage ?? "مرفق شهادة المطابقة غير صالح."));
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var certIndex = await _db.LaboratoryFinalConformityCertificates.CountAsync() + 1;
            var certNumber = $"CERT-{DateTime.UtcNow:yyyy}-{certIndex:D4}";

            var cert = new LaboratoryFinalConformityCertificate
            {
                CertificateNumber = certNumber,
                ProductionReferenceNumber = effectiveRefNum,
                RawMaterialReportId = rawReport?.Id,
                SampleNumber = rawReport?.SampleNumber,
                SampleCode = rawReport?.SampleCode ?? (dto.ManufactureNumber ?? $"SMP-{effectiveRefNum}"),
                LaboratorySection = section,
                FinalProductName = prodName,
                ActualFinalProductQuantity = Math.Round(effectiveActualOutput, 3),
                QuantityUnit = string.IsNullOrWhiteSpace(dto.QuantityUnit) ? "طن" : dto.QuantityUnit.Trim(),
                ProductionBatchNumber = (dto.ProductionBatchNumber ?? dto.ManufactureNumber ?? $"BATCH-{effectiveRefNum}").Trim(),
                ProductionWorkInProcessId = dto.ProductionWorkInProcessId,
                ProductionProcessId = dto.ProductionProcessId,
                ProductionProcessNumber = dto.ProductionProcessNumber,
                ProductionFinishedProductId = dto.ProductionFinishedProductId,
                ManufactureNumber = dto.ManufactureNumber,
                ProductionOrderId = dto.ProductionOrderId,
                ProductionOrderNumber = dto.ProductionOrderNumber,
                TargetQuantity = Math.Round(effectiveTarget, 3),
                AssociatedReferenceNumbers = dto.AssociatedReferenceNumbers,
                ProductionCompletionDateTime = dto.ProductionCompletionDateTime ?? DateTime.UtcNow,
                ActualRawMaterialInputQuantity = Math.Round(effectiveInput, 3),
                CalculatedDifference = calculatedDiff,
                ActualMeasuredWaste = dto.ActualMeasuredWaste.HasValue ? Math.Round(dto.ActualMeasuredWaste.Value, 3) : null,
                WasteClassification = string.IsNullOrWhiteSpace(dto.WasteClassification) ? "ORDINARY_WASTE" : dto.WasteClassification.Trim(),
                ConformityDecision = certConformityDecision,
                NonConformityAction = nonConfAction,
                ClassifiedProductType = dto.ClassifiedProductType?.Trim(),
                ReworkInstructions = dto.ReworkInstructions?.Trim(),
                RejectionReason = dto.RejectionReason?.Trim() ?? (certConformityDecision == "NON_CONFORMING" ? dto.Observations?.Trim() : null),
                CorrectiveAction = dto.CorrectiveAction?.Trim() ?? dto.ReworkInstructions?.Trim(),
                SpecificationResultsJson = !string.IsNullOrWhiteSpace(dto.FinalTestResults) ? dto.FinalTestResults : dto.SpecificationResultsJson,
                QualityParametersJson = dto.QualityParametersJson ?? dto.FinalTestResults ?? dto.SpecificationResultsJson,
                Observations = dto.Observations?.Trim(),
                AttachmentPath = !string.IsNullOrWhiteSpace(attachRes.StoredFileName) ? $"/uploads/laboratory/{attachRes.StoredFileName}" : null,
                AttachmentFileName = attachRes.OriginalFileName,
                AttachmentFileType = attachRes.ContentType,
                AttachmentFileSize = attachRes.FileSize,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                ApprovalStatus = certApprovalStatus,
                ReviewedBy = userName,
                ReviewedAt = DateTime.UtcNow,
                IsStoragePosted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.LaboratoryFinalConformityCertificates.AddAsync(cert);
            await _db.SaveChangesAsync();

            // 4. Update the linked ProductionFinishedProduct and WarehouseBatch
            if (finishedProd != null)
            {
                finishedProd.LaboratoryConformityStatus = prodStatus;
                finishedProd.LaboratoryCertificateId = cert.Id;
                finishedProd.LaboratoryCertificateNumber = cert.CertificateNumber;
                finishedProd.LaboratoryNotes = cert.Observations;
                finishedProd.LaboratoryQualityParametersJson = cert.QualityParametersJson;

                if (prodStatus == "APPROVED")
                {
                    finishedProd.LaboratoryCertifiedAt = DateTime.UtcNow;
                    finishedProd.LaboratoryCertifiedBy = userName;
                    finishedProd.LaboratoryRejectionReason = null;
                    finishedProd.LaboratoryCorrectiveAction = null;
                }
                else if (prodStatus == "REJECTED")
                {
                    finishedProd.LaboratoryCertifiedAt = DateTime.UtcNow;
                    finishedProd.LaboratoryCertifiedBy = userName;
                    finishedProd.LaboratoryRejectionReason = cert.RejectionReason ?? "غير مطابق للمواصفات";
                    finishedProd.LaboratoryCorrectiveAction = cert.CorrectiveAction;
                }

                // Update corresponding WarehouseBatch for inventory/export control
                var mfgNo = finishedProd.ManufactureNumber;
                var wb = await _db.WarehouseBatches.FirstOrDefaultAsync(b => b.BatchNumber == mfgNo);
                if (wb != null)
                {
                    if (prodStatus == "APPROVED")
                    {
                        wb.Status = "APPROVED";
                        wb.IsActive = true;
                    }
                    else if (prodStatus == "REJECTED")
                    {
                        wb.Status = "REJECTED";
                    }
                    else
                    {
                        wb.Status = "PENDING_LAB_INSPECTION";
                    }
                }
            }

            // Link ProductionWorkInProcess if provided
            if (dto.ProductionWorkInProcessId.HasValue)
            {
                var wip = await _db.ProductionWorkInProcess.FirstOrDefaultAsync(w => w.Id == dto.ProductionWorkInProcessId.Value);
                if (wip != null)
                {
                    wip.FinalProductName = prodName;
                    wip.FinalProductQuantity = cert.ActualFinalProductQuantity;
                    wip.WasteQuantity = cert.ActualMeasuredWaste ?? calculatedDiff;
                    wip.WasteClassification = cert.WasteClassification;
                    wip.Status = certConformityDecision == "CONFORMS" ? "مكتمل" : (nonConfAction == "REWORK_REMANUFACTURE" ? "إعادة تشغيل" : "قيد المعاينة");
                }
            }

            // Update connected ProductionOutputs of classification FINISHED
            var finishedOutputs = await _db.ProductionOutputs
                .Where(o => (o.ProductionReferenceNumber == dto.ProductionReferenceNumber || (dto.ManufactureNumber != null && o.OutputNumber == dto.ManufactureNumber)) && o.Classification == "FINISHED")
                .ToListAsync();
            foreach (var outp in finishedOutputs)
            {
                outp.LaboratoryCertificateId = cert.Id;
                outp.LaboratoryConformityStatus = certConformityDecision == "CONFORMS" ? "CONFORMS" : "NON_CONFORMING";
            }

            // Create FormApprovalHistory for Control & Monitoring review
            var history = new FormApprovalHistory
            {
                FormType = "LaboratoryFinalCert",
                FormId = cert.Id,
                PONumber = rawReport?.PONumber ?? string.Empty,
                TransactionOrMovementNumber = cert.CertificateNumber,
                PreviousStatus = "PENDING_INSPECTION",
                NewStatus = certApprovalStatus,
                Action = certApprovalStatus,
                ActorName = userName,
                ActorUserId = userId,
                ActorRole = role,
                Comment = $"تم إصدار شهادة مطابقة منتج نهائي ({cert.CertificateNumber}) للمنتج {cert.FinalProductName} برقم تصنيع {cert.ManufactureNumber} بقرار: {cert.ConformityDecision}.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Laboratory Final Certificate {Cert} created for Product {Prod} (Mfg: {Mfg}) by {User}",
                cert.CertificateNumber, cert.FinalProductName, cert.ManufactureNumber, userName);

            var notifMsg = $"تم إصدار شهادة مطابقة منتج نهائي ({cert.CertificateNumber}) للمنتج ({cert.FinalProductName}) بقرار: {cert.ConformityDecision}.";
            await NotifyLaboratoryChangeAsync("FINAL_CERT", cert.Id, effectiveRefNum, certApprovalStatus, userName, notifMsg);

            // Notify Production & Warehouse of the inspection outcome
            try
            {
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    Department = "Production",
                    Action = "FINISHED_PRODUCT_INSPECTED",
                    FinishedProductId = finishedProd?.Id,
                    ManufactureNumber = cert.ManufactureNumber,
                    CertificateNumber = cert.CertificateNumber,
                    ConformityStatus = prodStatus,
                    Inspector = userName,
                    Timestamp = DateTime.UtcNow
                });
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    Department = "Laboratory",
                    Action = "CERTIFICATE_ISSUED",
                    CertificateId = cert.Id,
                    CertificateNumber = cert.CertificateNumber,
                    ManufactureNumber = cert.ManufactureNumber,
                    Status = certApprovalStatus,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception exSignal)
            {
                _logger.LogWarning(exSignal, "Failed to broadcast lab cert SignalR event");
            }

            var resultDto = MapFinalCertToDto(cert);
            var successMsg = prodStatus == "APPROVED"
                ? $"تم اعتماد شهادة مطابقة المنتج النهائي رقم {cert.CertificateNumber} بنجاح (مطابق للمواصفات)."
                : (prodStatus == "REJECTED"
                    ? $"تم تسجيل عدم مطابقة المنتج النهائي رقم {cert.CertificateNumber} (مرفوض مخبرياً)."
                    : $"تم حفظ حالة فحص المنتج النهائي رقم {cert.CertificateNumber} بنجاح.");

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<FinalConformityCertificateDto>.Ok(resultDto, successMsg));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(new[] { attachRes.StoredFileName });
            _logger.LogError(ex, "Failed to create Laboratory Final Conformity Certificate.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ شهادة المطابقة: " + ex.Message));
        }
    }

    // =========================================================================
    // 6. CARD THREE: IN-PROCESS PRODUCT ANALYSIS REPORT (تقرير نتائج تحليل المنتج أثناء التشغيل)
    // =========================================================================
    [HttpGet("in-process-reports")]
    [ProducesResponseType(typeof(ApiResponse<List<InProcessReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInProcessReports(
        [FromQuery] string? section = null,
        [FromQuery] int? referenceNumber = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? decision = null,
        [FromQuery] string? search = null)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var query = _db.LaboratoryInProcessReports
            .AsNoTracking()
            .AsQueryable();

        if (AppRoles.HasPhysicalLabAccess(role) && !AppRoles.HasChemicalLabAccess(role))
        {
            query = query.Where(p => p.LaboratorySection == "PHYSICAL");
        }
        else if (AppRoles.HasChemicalLabAccess(role) && !AppRoles.HasPhysicalLabAccess(role))
        {
            query = query.Where(p => p.LaboratorySection == "CHEMICAL");
        }
        else if (!string.IsNullOrWhiteSpace(section) && !section.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.LaboratorySection == section.Trim().ToUpperInvariant());
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(p => p.ProductionReferenceNumber == referenceNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(stage) && !stage.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.ProductionStage.ToLower().Contains(stage.Trim().ToLowerInvariant()));
        }

        if (!string.IsNullOrWhiteSpace(decision) && !decision.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.InspectionDecision == decision.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.ProductionReferenceNumber.ToString().Contains(s) ||
                p.ProductName.ToLower().Contains(s) ||
                p.ProductionBatchNumber.ToLower().Contains(s) ||
                p.ProductionStage.ToLower().Contains(s) ||
                p.ResponsibleEmployee.ToLower().Contains(s));
        }

        var list = await query.OrderByDescending(p => p.TestDateTime).Take(300).ToListAsync();
        var dtos = list.Select(MapInProcessToDto).ToList();
        return Ok(ApiResponse<List<InProcessReportDto>>.Ok(dtos));
    }

    [HttpGet("in-process-reports/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<InProcessReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInProcessReportById(int id)
    {
        var report = await _db.LaboratoryInProcessReports
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (report == null)
        {
            return NotFound(ApiResponse.Fail($"تقرير فحص التشغيل #{id} غير موجود."));
        }

        return Ok(ApiResponse<InProcessReportDto>.Ok(MapInProcessToDto(report)));
    }

    [HttpPost("in-process-reports")]
    [ProducesResponseType(typeof(ApiResponse<InProcessReportDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateInProcessReport()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, ApiResponse.Fail("المصادقة مطلوبة للوصول إلى المعمل."));
        }

        if (!AppRoles.HasLaboratoryAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: الوصول مقتصر على موظفي المعمل المعتمدين."));
        }

        var dto = await ParseInProcessReportRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات فحص التشغيل غير صالحة."));
        }

        if (dto.ProductionReferenceNumber <= 0)
        {
            return BadRequest(ApiResponse.Fail("الرقم المرجعي التتبعي للإنتاج مطلوب."));
        }

        var rawReport = await _db.LaboratoryRawMaterialReports
            .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == dto.ProductionReferenceNumber);

        if (rawReport == null)
        {
            return BadRequest(ApiResponse.Fail($"الرقم المرجعي التتبعي #{dto.ProductionReferenceNumber} غير مسجل مسبقاً."));
        }

        string assignedSection = AppRoles.GetDefaultLaboratorySection(role);
        string section = rawReport.LaboratorySection;
        if (assignedSection == "PHYSICAL" && section != "PHYSICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الفيزيائي لا يمكنه فحص تشغيل المعمل الكيميائي."));
        }
        if (assignedSection == "CHEMICAL" && section != "CHEMICAL")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح: موظف المعمل الكيميائي لا يمكنه فحص تشغيل المعمل الفيزيائي."));
        }

        var prodName = (dto.ProductName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prodName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج أو الخامة قيد التشغيل مطلوب."));
        }

        var stage = (dto.ProductionStage ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(stage))
        {
            return BadRequest(ApiResponse.Fail("مرحلة الإنتاج مطلوبة."));
        }

        var decision = (dto.InspectionDecision ?? "MEETS_REQUIREMENTS").Trim().ToUpperInvariant();

        var attachRes = await ValidateAndSaveAttachmentAsync(dto.Attachment, dto.AttachmentPath, "مرفق فحص التشغيل");
        if (!attachRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attachRes.ErrorMessage ?? "مرفق فحص التشغيل غير صالح."));
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var report = new LaboratoryInProcessReport
            {
                ProductionReferenceNumber = dto.ProductionReferenceNumber,
                RawMaterialReportId = rawReport.Id,
                SampleNumber = rawReport.SampleNumber,
                SampleCode = rawReport.SampleCode,
                ProductionBatchNumber = (dto.ProductionBatchNumber ?? $"BATCH-{dto.ProductionReferenceNumber}").Trim(),
                ProductionWorkInProcessId = dto.ProductionWorkInProcessId,
                LaboratorySection = section,
                ProductName = prodName,
                TestDateTime = dto.TestDateTime ?? DateTime.UtcNow,
                ProductionStage = stage,
                AnalysisResultsJson = dto.AnalysisResultsJson,
                SpecificationCriteria = dto.SpecificationCriteria?.Trim(),
                InspectionDecision = decision,
                Notes = dto.Notes?.Trim(),
                AttachmentPath = !string.IsNullOrWhiteSpace(attachRes.StoredFileName) ? $"/uploads/laboratory/{attachRes.StoredFileName}" : null,
                AttachmentFileName = attachRes.OriginalFileName,
                AttachmentFileType = attachRes.ContentType,
                AttachmentFileSize = attachRes.FileSize,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.LaboratoryInProcessReports.AddAsync(report);
            await _db.SaveChangesAsync();

            // Link WIP batch if provided
            if (dto.ProductionWorkInProcessId.HasValue)
            {
                var wip = await _db.ProductionWorkInProcess.FirstOrDefaultAsync(w => w.Id == dto.ProductionWorkInProcessId.Value);
                if (wip != null)
                {
                    if (decision == "DOES_NOT_MEET")
                    {
                        wip.Status = "معلق فحص معملي (تنبيه عدم مطابقة)";
                    }
                    else if (wip.Status == "قيد التشغيل" && decision == "MEETS_REQUIREMENTS")
                    {
                        wip.Status = "قيد التشغيل (فحص مطابق)";
                    }
                }
            }

            // Quality Gate: Check and update matching ProductionBatch
            if (!string.IsNullOrWhiteSpace(dto.ProductionBatchNumber))
            {
                var cleanBatchNum = dto.ProductionBatchNumber.Trim();
                var prodBatch = await _db.ProductionBatches
                    .FirstOrDefaultAsync(b => b.BatchNumber == cleanBatchNum);
                if (prodBatch != null)
                {
                    if (decision == "DOES_NOT_MEET")
                    {
                        prodBatch.QualityHold = true;
                        prodBatch.QualityHoldReason = $"فحص معملي غير مطابق للمواصفات: {dto.InspectionDecision} | {dto.Notes}";
                        prodBatch.QualityHoldReportId = report.Id;
                        prodBatch.QualityHoldAt = DateTime.UtcNow;
                        prodBatch.Status = "ON_HOLD_LABORATORY";
                    }
                    else if (prodBatch.QualityHold && (decision == "MEETS_REQUIREMENTS" || decision == "MEETS"))
                    {
                        prodBatch.QualityHold = false;
                        prodBatch.QualityHoldReleasedAt = DateTime.UtcNow;
                        prodBatch.QualityHoldReleasedBy = userName;
                        prodBatch.Status = "IN_PROGRESS";
                    }
                }
            }

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("In-process report #{Id} recorded for Ref #{Ref} at stage '{Stage}' by {User}",
                report.Id, dto.ProductionReferenceNumber, stage, userName);

            var resultDto = MapInProcessToDto(report);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<InProcessReportDto>.Ok(resultDto, $"تم تسجيل نتائج فحص التشغيل للمرحلة ({stage}) للرقم المرجعي #{dto.ProductionReferenceNumber} بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(new[] { attachRes.StoredFileName });
            _logger.LogError(ex, "Failed to create Laboratory In-Process Report.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ فحص التشغيل."));
        }
    }

    // =========================================================================
    // 7. SECURE ATTACHMENT STREAMING & DOWNLOAD
    // =========================================================================
    [AllowAnonymous]
    [HttpGet("attachments/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttachmentFile(string fileName, [FromQuery] bool download = false)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return BadRequest(ApiResponse.Fail("اسم الملف غير صالح."));
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var filePath = Path.Combine(webRoot, "uploads", "laboratory", fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(ApiResponse.Fail("الملف المطلوب غير موجود على الخادم."));
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        if (download)
        {
            return File(bytes, contentType, fileName);
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
        return File(bytes, contentType);
    }

    // =========================================================================
    // 8. ATOMIC SEQUENCE GENERATOR (Concurrency-Safe UPDLOCK, ROWLOCK)
    // =========================================================================
    private async Task<int> GetNextProductionReferenceNumberAsync()
    {
        var seqParam = new Microsoft.Data.SqlClient.SqlParameter("@seqName", "PRODUCTION_REFERENCE");
        var nextValParam = new Microsoft.Data.SqlClient.SqlParameter("@nextVal", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };

        await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE LaboratoryProductionSequences WITH (UPDLOCK, ROWLOCK)
            SET @nextVal = NextValue, NextValue = NextValue + 1
            WHERE SequenceName = @seqName;
            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO LaboratoryProductionSequences (SequenceName, NextValue) VALUES (@seqName, 2);
                SET @nextVal = 1;
            END", seqParam, nextValParam);

        return (int)nextValParam.Value;
    }

    private async Task<(int SampleNumber, string SampleCode)> GetNextGlobalSampleNumberAsync()
    {
        var seqParam = new Microsoft.Data.SqlClient.SqlParameter("@seqName", "LAB_SAMPLE_NUMBER");
        var nextValParam = new Microsoft.Data.SqlClient.SqlParameter("@nextVal", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };

        await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE LaboratoryProductionSequences WITH (UPDLOCK, ROWLOCK)
            SET @nextVal = NextValue, NextValue = NextValue + 1
            WHERE SequenceName = @seqName;
            IF @@ROWCOUNT = 0
            BEGIN
                DECLARE @maxExisting INT;
                SELECT @maxExisting = ISNULL(MAX(SampleNumber), 0) FROM LaboratoryRawMaterialReports;
                SET @nextVal = @maxExisting + 1;
                INSERT INTO LaboratoryProductionSequences (SequenceName, NextValue) VALUES (@seqName, @nextVal + 1);
            END", seqParam, nextValParam);

        int sampleNumber = (int)nextValParam.Value;
        string sampleCode = $"LAB-REF-{DateTime.UtcNow.Year}-{sampleNumber:D6}";
        return (sampleNumber, sampleCode);
    }

    private async Task<(int SampleNumber, string SampleCode)> PeekNextGlobalSampleNumberAsync()
    {
        var seq = await _db.LaboratoryProductionSequences
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SequenceName == "LAB_SAMPLE_NUMBER");

        int nextVal;
        if (seq != null)
        {
            nextVal = seq.NextValue;
        }
        else
        {
            var maxExisting = await _db.LaboratoryRawMaterialReports.MaxAsync(r => (int?)r.SampleNumber) ?? 0;
            nextVal = maxExisting + 1;
        }
        return (nextVal, $"LAB-REF-{DateTime.UtcNow.Year}-{nextVal:D6}");
    }

    private async Task<int> GetNextUserSampleNumberAsync(Guid userId)
    {
        var userParam = new Microsoft.Data.SqlClient.SqlParameter("@userId", userId);
        var nextValParam = new Microsoft.Data.SqlClient.SqlParameter("@nextVal", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };

        await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE LaboratoryUserSampleSequences WITH (UPDLOCK, ROWLOCK)
            SET @nextVal = NextSampleNumber, NextSampleNumber = NextSampleNumber + 1, UpdatedAt = GETUTCDATE()
            WHERE UserId = @userId;
            IF @@ROWCOUNT = 0
            BEGIN
                DECLARE @maxExisting INT;
                SELECT @maxExisting = ISNULL(MAX(SampleNumber), 0) FROM LaboratoryRawMaterialReports WHERE ResponsibleUserId = @userId;
                SET @nextVal = @maxExisting + 1;
                INSERT INTO LaboratoryUserSampleSequences (UserId, NextSampleNumber, UpdatedAt) VALUES (@userId, @nextVal + 1, GETUTCDATE());
            END", userParam, nextValParam);

        return (int)nextValParam.Value;
    }

    private async Task<int> PeekNextUserSampleNumberAsync(Guid userId)
    {
        var seq = await _db.LaboratoryUserSampleSequences
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (seq != null)
        {
            return seq.NextSampleNumber;
        }

        var maxExisting = await _db.LaboratoryRawMaterialReports
            .Where(r => r.ResponsibleUserId == userId)
            .MaxAsync(r => (int?)r.SampleNumber) ?? 0;

        return maxExisting + 1;
    }

    // =========================================================================
    // 9. HELPER METHODS & REQUEST PARSERS
    // =========================================================================
    private (bool IsAuth, string Role, string UserName, Guid? UserId) GetAuthenticatedUser()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف المعمل";
        Guid? userId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            userId = parsedGuid;
        }
        var isAuth = User.Identity?.IsAuthenticated ?? false;
        return (isAuth, role, userName, userId);
    }

    private async Task<CreateRawMaterialReportDto?> ParseRawMaterialReportRequestAsync()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var dto = new CreateRawMaterialReportDto
            {
                LaboratorySection = form["laboratorySection"].FirstOrDefault(),
                SampleCode = form["sampleCode"].FirstOrDefault(),
                MaterialClassification = form["materialClassification"].FirstOrDefault() ?? "MECHANICAL",
                SampleName = form["sampleName"].FirstOrDefault() ?? "سيليكا باودر (Micro Silica Powder)",
                SampleDescription = form["sampleDescription"].FirstOrDefault() ?? "بودرة رمادية اللون",
                BatchNumber = form["batchNumber"].FirstOrDefault(),
                RawMaterialDescription = form["rawMaterialDescription"].FirstOrDefault(),
                SupplierName = form["supplierName"].FirstOrDefault(),
                PONumber = form["poNumber"].FirstOrDefault(),
                InspectionDecision = form["inspectionDecision"].FirstOrDefault() ?? "ACCEPTED",
                ComplyStatus = form["complyStatus"].FirstOrDefault() ?? "COMPLY",
                LabManagerName = form["labManagerName"].FirstOrDefault(),
                LabManagerSignature = form["labManagerSignature"].FirstOrDefault(),
                Notes = form["notes"].FirstOrDefault(),
                AnalysisResultsJson = form["analysisResultsJson"].FirstOrDefault(),
                Attachment = form.Files.GetFile("attachment"),
                AttachmentPath = form["attachmentPath"].FirstOrDefault(),
                QuantityConfirmationDocument = form.Files.GetFile("quantityConfirmationDocument") ?? form.Files.GetFile("quantityDoc"),
                QuantityConfirmationPath = form["quantityConfirmationPath"].FirstOrDefault(),
                QuantityConfirmationFileName = form["quantityConfirmationFileName"].FirstOrDefault()
            };

            if (int.TryParse(form["sampleNumber"].FirstOrDefault(), out var sNum))
                dto.SampleNumber = sNum;

            if (int.TryParse(form["productionReferenceNumber"].FirstOrDefault(), out var pRef))
                dto.ProductionReferenceNumber = pRef;

            if (DateTime.TryParse(form["sampleReceivedDate"].FirstOrDefault(), out var srDate))
                dto.SampleReceivedDate = srDate;

            if (DateTime.TryParse(form["reportIssueDate"].FirstOrDefault(), out var riDate))
                dto.ReportIssueDate = riDate;

            if (DateTime.TryParse(form["labManagerDate"].FirstOrDefault(), out var lmDate))
                dto.LabManagerDate = lmDate;

            if (DateTime.TryParse(form["analysisDateTime"].FirstOrDefault(), out var parsedDate))
                dto.AnalysisDateTime = parsedDate;

            if (decimal.TryParse(form["receivedQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rQty))
                dto.ReceivedQuantity = rQty;

            if (decimal.TryParse(form["testedMaterialWeight"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tWeight))
                dto.TestedMaterialWeight = tWeight;

            if (decimal.TryParse(form["acceptedQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var aQty))
                dto.AcceptedQuantity = aQty;

            if (decimal.TryParse(form["rejectedQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rejQty))
                dto.RejectedQuantity = rejQty;

            if (int.TryParse(form["rawMaterialId"].FirstOrDefault(), out var mId))
                dto.RawMaterialId = mId;

            if (int.TryParse(form["sourceTransportationId"].FirstOrDefault(), out var tId))
                dto.SourceTransportationId = tId;

            if (int.TryParse(form["purchaseOrderId"].FirstOrDefault(), out var pId))
                dto.PurchaseOrderId = pId;

            return dto;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<CreateRawMaterialReportDto>(
                Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private async Task<CreateFinalConformityCertificateDto?> ParseFinalCertificateRequestAsync()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var dto = new CreateFinalConformityCertificateDto
            {
                FinalProductName = form["finalProductName"].FirstOrDefault() ?? string.Empty,
                QuantityUnit = form["quantityUnit"].FirstOrDefault() ?? "طن",
                ProductionBatchNumber = form["productionBatchNumber"].FirstOrDefault(),
                WasteClassification = form["wasteClassification"].FirstOrDefault() ?? "ORDINARY_WASTE",
                ConformityDecision = form["conformityDecision"].FirstOrDefault() ?? "CONFORMS",
                NonConformityAction = form["nonConformityAction"].FirstOrDefault(),
                ClassifiedProductType = form["classifiedProductType"].FirstOrDefault(),
                ReworkInstructions = form["reworkInstructions"].FirstOrDefault(),
                RejectionReason = form["rejectionReason"].FirstOrDefault(),
                CorrectiveAction = form["correctiveAction"].FirstOrDefault(),
                SpecificationResultsJson = form["specificationResultsJson"].FirstOrDefault(),
                QualityParametersJson = form["qualityParametersJson"].FirstOrDefault() ?? form["specificationResultsJson"].FirstOrDefault(),
                Observations = form["inspectorNotes"].FirstOrDefault() ?? form["observations"].FirstOrDefault() ?? form["notes"].FirstOrDefault(),
                FinalTestResults = form["finalTestResults"].FirstOrDefault() ?? form["specificationResultsJson"].FirstOrDefault(),
                InspectorNotes = form["inspectorNotes"].FirstOrDefault() ?? form["observations"].FirstOrDefault() ?? form["notes"].FirstOrDefault(),
                ApprovalStage = form["approvalStage"].FirstOrDefault() ?? form["stage"].FirstOrDefault(),
                PhysicalApprovalStatus = form["physicalApprovalStatus"].FirstOrDefault(),
                PhysicalTestResults = form["physicalTestResults"].FirstOrDefault() ?? form["finalTestResults"].FirstOrDefault(),
                PhysicalNotes = form["physicalNotes"].FirstOrDefault() ?? form["inspectorNotes"].FirstOrDefault(),
                ChemicalApprovalStatus = form["chemicalApprovalStatus"].FirstOrDefault(),
                ChemicalTestResults = form["chemicalTestResults"].FirstOrDefault(),
                ChemicalNotes = form["chemicalNotes"].FirstOrDefault(),
                ProductionProcessNumber = form["productionProcessNumber"].FirstOrDefault(),
                ManufactureNumber = form["manufactureNumber"].FirstOrDefault(),
                ProductionOrderNumber = form["productionOrderNumber"].FirstOrDefault(),
                AssociatedReferenceNumbers = form["associatedReferenceNumbers"].FirstOrDefault(),
                Attachment = form.Files.GetFile("attachment"),
                AttachmentPath = form["attachmentPath"].FirstOrDefault()
            };

            if (int.TryParse(form["productionFinishedProductId"].FirstOrDefault(), out var pfpId))
                dto.ProductionFinishedProductId = pfpId;

            if (int.TryParse(form["productionProcessId"].FirstOrDefault(), out var procId))
                dto.ProductionProcessId = procId;

            if (int.TryParse(form["productionOrderId"].FirstOrDefault(), out var pOrderId))
                dto.ProductionOrderId = pOrderId;

            if (decimal.TryParse(form["targetQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tQty))
                dto.TargetQuantity = tQty;

            if (int.TryParse(form["productionReferenceNumber"].FirstOrDefault(), out var refNo))
                dto.ProductionReferenceNumber = refNo;

            if (int.TryParse(form["productionWorkInProcessId"].FirstOrDefault(), out var wipId))
                dto.ProductionWorkInProcessId = wipId;

            if (DateTime.TryParse(form["productionCompletionDateTime"].FirstOrDefault(), out var compDate))
                dto.ProductionCompletionDateTime = compDate;

            if (decimal.TryParse(form["actualFinalProductQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var prodQty))
                dto.ActualFinalProductQuantity = prodQty;

            if (decimal.TryParse(form["actualRawMaterialInputQuantity"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var inputQty))
                dto.ActualRawMaterialInputQuantity = inputQty;

            if (decimal.TryParse(form["actualMeasuredWaste"].FirstOrDefault(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var wasteQty))
                dto.ActualMeasuredWaste = wasteQty;

            return dto;
        }

        try
        {
            if (Request.Body.CanSeek)
            {
                Request.Body.Position = 0;
            }
            using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();
            _logger.LogInformation("CreateFinalCertificate JSON body: {Body}", bodyText);
            if (string.IsNullOrWhiteSpace(bodyText))
            {
                _logger.LogWarning("CreateFinalCertificate body was empty!");
                return null;
            }
            return JsonSerializer.Deserialize<CreateFinalConformityCertificateDto>(
                bodyText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize CreateFinalConformityCertificateDto from JSON body.");
            return null;
        }
    }

    private async Task<CreateInProcessReportDto?> ParseInProcessReportRequestAsync()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var dto = new CreateInProcessReportDto
            {
                ProductionBatchNumber = form["productionBatchNumber"].FirstOrDefault(),
                ProductName = form["productName"].FirstOrDefault() ?? string.Empty,
                ProductionStage = form["productionStage"].FirstOrDefault() ?? string.Empty,
                SpecificationCriteria = form["specificationCriteria"].FirstOrDefault(),
                AnalysisResultsJson = form["analysisResultsJson"].FirstOrDefault(),
                InspectionDecision = form["inspectionDecision"].FirstOrDefault() ?? "MEETS_REQUIREMENTS",
                Notes = form["notes"].FirstOrDefault(),
                Attachment = form.Files.GetFile("attachment"),
                AttachmentPath = form["attachmentPath"].FirstOrDefault()
            };

            if (int.TryParse(form["productionReferenceNumber"].FirstOrDefault(), out var refNo))
                dto.ProductionReferenceNumber = refNo;

            if (int.TryParse(form["productionWorkInProcessId"].FirstOrDefault(), out var wipId))
                dto.ProductionWorkInProcessId = wipId;

            if (DateTime.TryParse(form["testDateTime"].FirstOrDefault(), out var tDate))
                dto.TestDateTime = tDate;

            return dto;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<CreateInProcessReportDto>(
                Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private async Task<(bool Success, string? ErrorMessage, string? StoredFileName, string? OriginalFileName, string? ContentType, long FileSize)>
        ValidateAndSaveAttachmentAsync(IFormFile? file, string? existingPath, string fieldLabel, bool isRequired = false)
    {
        if (file == null || file.Length == 0)
        {
            if (!string.IsNullOrWhiteSpace(existingPath))
            {
                var clean = Path.GetFileName(existingPath.Trim());
                return (true, null, clean, clean, "application/octet-stream", 0);
            }

            if (isRequired)
            {
                return (false, $"{fieldLabel} مطلوب بصيغة PDF أو صورة (JPG/PNG).", null, null, null, 0);
            }

            return (true, null, null, null, null, 0);
        }

        // Validate max size 15 MB
        if (file.Length > 15 * 1024 * 1024)
        {
            return (false, $"{fieldLabel} يتجاوز الحد الأقصى المسموح (15 ميجابايت).", null, null, null, 0);
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            return (false, $"{fieldLabel} يجب أن يكون بصيغة PDF أو صورة (JPG / PNG / WEBP).", null, null, null, 0);
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadDir = Path.Combine(webRoot, "uploads", "laboratory");
        if (!Directory.Exists(uploadDir))
        {
            Directory.CreateDirectory(uploadDir);
        }

        var uniqueName = $"lab_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadDir, uniqueName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return (true, null, uniqueName, file.FileName, file.ContentType, file.Length);
    }

    private void CleanupSavedFiles(IEnumerable<string?> fileNames)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        foreach (var fn in fileNames)
        {
            if (string.IsNullOrWhiteSpace(fn)) continue;
            try
            {
                var path = Path.Combine(webRoot, "uploads", "laboratory", fn);
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup uploaded laboratory file {File}", fn);
            }
        }
    }

    private async Task NotifyLaboratoryChangeAsync(string entityType, int entityId, int referenceNumber, string status, string actorName, string message)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("OnLaboratoryStatusChanged", new
            {
                entityType = entityType,
                entityId = entityId,
                productionReferenceNumber = referenceNumber,
                status = status,
                actorName = actorName,
                message = message,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SignalR broadcast for Laboratory {Entity} #{Id}", entityType, entityId);
        }
    }

    // =========================================================================
    // 10. MAPPERS
    // =========================================================================
    public static RawMaterialReportDto MapRawMaterialToDto(LaboratoryRawMaterialReport r) => new()
    {
        Id = r.Id,
        ProductionReferenceNumber = r.ProductionReferenceNumber,
        SampleNumber = r.SampleNumber,
        SampleCode = !string.IsNullOrWhiteSpace(r.SampleCode) ? r.SampleCode : $"SMP-{r.SampleNumber:D4}",
        MaterialClassification = !string.IsNullOrWhiteSpace(r.MaterialClassification) ? r.MaterialClassification : "MECHANICAL",
        SampleName = !string.IsNullOrWhiteSpace(r.SampleName) ? r.SampleName : r.RawMaterialDescription,
        SampleDescription = r.SampleDescription,
        BatchNumber = r.BatchNumber,
        SampleReceivedDate = r.SampleReceivedDate,
        ReportIssueDate = r.ReportIssueDate,
        LabManagerName = r.LabManagerName,
        LabManagerSignature = r.LabManagerSignature,
        LabManagerDate = r.LabManagerDate,
        ComplyStatus = !string.IsNullOrWhiteSpace(r.ComplyStatus) ? r.ComplyStatus : (r.InspectionDecision == "ACCEPTED" ? "COMPLY" : "NOT_COMPLIED"),
        LaboratorySection = r.LaboratorySection,
        AnalysisDateTime = r.AnalysisDateTime,
        RawMaterialDescription = r.RawMaterialDescription,
        RawMaterialId = r.RawMaterialId,
        RawMaterialName = r.RawMaterial?.Name,
        SupplierName = r.SupplierName,
        SourceTransportationId = r.SourceTransportationId,
        TransportationTransactionNumber = r.TransportationTransactionNumber,
        PurchaseOrderId = r.PurchaseOrderId,
        PONumber = r.PONumber,
        ReceivedQuantity = r.ReceivedQuantity,
        TestedMaterialWeight = r.TestedMaterialWeight,
        AcceptedQuantity = r.AcceptedQuantity,
        RejectedQuantity = r.RejectedQuantity,
        AnalysisResultsJson = r.AnalysisResultsJson,
        InspectionDecision = r.InspectionDecision,
        Notes = r.Notes,
        AttachmentPath = r.AttachmentPath,
        AttachmentFileName = r.AttachmentFileName,
        AttachmentFileType = r.AttachmentFileType,
        AttachmentFileSize = r.AttachmentFileSize,
        QuantityConfirmationPath = r.QuantityConfirmationPath,
        QuantityConfirmationFileName = r.QuantityConfirmationFileName,
        QuantityConfirmationFileType = r.QuantityConfirmationFileType,
        QuantityConfirmationFileSize = r.QuantityConfirmationFileSize,
        Unit = !string.IsNullOrWhiteSpace(r.Unit) ? r.Unit : "طن",
        PhysicalInspectionStatus = r.PhysicalInspectionStatus,
        PhysicalInspectedBy = r.PhysicalInspectedBy,
        PhysicalInspectedAt = r.PhysicalInspectedAt,
        PhysicalNotes = r.PhysicalNotes,
        ChemicalInspectionStatus = r.ChemicalInspectionStatus,
        ChemicalInspectedBy = r.ChemicalInspectedBy,
        ChemicalInspectedAt = r.ChemicalInspectedAt,
        ChemicalNotes = r.ChemicalNotes,
        ChemicalResultsJson = r.ChemicalResultsJson,
        OverallStatus = r.OverallStatus,
        ResponsibleEmployee = r.ResponsibleEmployee,
        ApprovalStatus = r.ApprovalStatus,
        ReviewedBy = r.ReviewedBy,
        ReviewedAt = r.ReviewedAt,
        RejectionReason = r.RejectionReason,
        IsStorageReceived = r.IsStorageReceived,
        StorageReceivedAt = r.StorageReceivedAt,
        StorageOperationId = r.StorageOperationId,
        CreatedAt = r.CreatedAt,
        CreatedBy = r.CreatedBy
    };

    public static InProcessReportDto MapInProcessToDto(LaboratoryInProcessReport p) => new()
    {
        Id = p.Id,
        ProductionReferenceNumber = p.ProductionReferenceNumber,
        RawMaterialReportId = p.RawMaterialReportId,
        SampleNumber = p.SampleNumber ?? p.RawMaterialReport?.SampleNumber,
        SampleCode = p.SampleCode ?? p.RawMaterialReport?.SampleCode ?? (p.RawMaterialReport != null ? $"SMP-{p.RawMaterialReport.SampleNumber:D4}" : null),
        ProductionBatchNumber = p.ProductionBatchNumber,
        ProductionWorkInProcessId = p.ProductionWorkInProcessId,
        LaboratorySection = p.LaboratorySection,
        ProductName = p.ProductName,
        TestDateTime = p.TestDateTime,
        ProductionStage = p.ProductionStage,
        AnalysisResultsJson = p.AnalysisResultsJson,
        SpecificationCriteria = p.SpecificationCriteria,
        InspectionDecision = p.InspectionDecision,
        Notes = p.Notes,
        AttachmentPath = p.AttachmentPath,
        AttachmentFileName = p.AttachmentFileName,
        AttachmentFileType = p.AttachmentFileType,
        AttachmentFileSize = p.AttachmentFileSize,
        ResponsibleEmployee = p.ResponsibleEmployee,
        CreatedAt = p.CreatedAt,
        CreatedBy = p.CreatedBy
    };

    public static FinalConformityCertificateDto MapFinalCertToDto(LaboratoryFinalConformityCertificate c)
    {
        string? finalTestResults = null;
        string? inspectorNotes = null;

        if (!string.IsNullOrWhiteSpace(c.QualityParametersJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(c.QualityParametersJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("finalTestResults", out var ftr)) finalTestResults = ftr.GetString();
                if (root.TryGetProperty("inspectorNotes", out var inotes)) inspectorNotes = inotes.GetString();
                else if (root.TryGetProperty("notes", out var legacyNotes)) inspectorNotes = legacyNotes.GetString();
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(finalTestResults) && !string.IsNullOrWhiteSpace(c.SpecificationResultsJson))
        {
            finalTestResults = c.SpecificationResultsJson;
        }

        if (string.IsNullOrWhiteSpace(inspectorNotes))
        {
            inspectorNotes = c.Observations;
        }

        var physStatus = !string.IsNullOrWhiteSpace(c.PhysicalApprovalStatus) ? c.PhysicalApprovalStatus : (c.ApprovalStatus == "APPROVED" ? "APPROVED" : "PENDING");
        var chemStatus = !string.IsNullOrWhiteSpace(c.ChemicalApprovalStatus) ? c.ChemicalApprovalStatus : (c.ApprovalStatus == "APPROVED" ? "APPROVED" : "PENDING");
        var overallStatus = !string.IsNullOrWhiteSpace(c.OverallStatus) ? c.OverallStatus : (c.ApprovalStatus == "APPROVED" ? "APPROVED" : (physStatus == "REJECTED" || chemStatus == "REJECTED" ? "REJECTED" : (physStatus == "APPROVED" ? "PENDING_CHEMICAL" : "PENDING_PHYSICAL")));

        return new FinalConformityCertificateDto
        {
            Id = c.Id,
            CertificateNumber = c.CertificateNumber,
            ProductionReferenceNumber = c.ProductionReferenceNumber,
            RawMaterialReportId = c.RawMaterialReportId,
            SampleNumber = c.SampleNumber ?? c.RawMaterialReport?.SampleNumber,
            SampleCode = c.SampleCode ?? c.RawMaterialReport?.SampleCode ?? (c.RawMaterialReport != null ? $"SMP-{c.RawMaterialReport.SampleNumber:D4}" : null),
            LaboratorySection = c.LaboratorySection,
            FinalProductName = c.FinalProductName,
            ActualFinalProductQuantity = c.ActualFinalProductQuantity,
            QuantityUnit = c.QuantityUnit,
            ProductionBatchNumber = c.ProductionBatchNumber,
            ProductionWorkInProcessId = c.ProductionWorkInProcessId,
            ProductionProcessId = c.ProductionProcessId,
            ProductionProcessNumber = c.ProductionProcessNumber,
            ProductionFinishedProductId = c.ProductionFinishedProductId,
            ManufactureNumber = c.ManufactureNumber,
            ProductionOrderId = c.ProductionOrderId,
            ProductionOrderNumber = c.ProductionOrderNumber,
            TargetQuantity = c.TargetQuantity,
            AssociatedReferenceNumbers = c.AssociatedReferenceNumbers,
            ProductionCompletionDateTime = c.ProductionCompletionDateTime,
            ActualRawMaterialInputQuantity = c.ActualRawMaterialInputQuantity,
            CalculatedDifference = c.CalculatedDifference,
            ActualMeasuredWaste = c.ActualMeasuredWaste,
            WasteClassification = c.WasteClassification,
            ConformityDecision = c.ConformityDecision,
            NonConformityAction = c.NonConformityAction,
            ClassifiedProductType = c.ClassifiedProductType,
            ReworkInstructions = c.ReworkInstructions,
            RejectionReason = c.RejectionReason,
            CorrectiveAction = c.CorrectiveAction,
            SpecificationResultsJson = c.SpecificationResultsJson,
            QualityParametersJson = c.QualityParametersJson,
            Observations = c.Observations,
            FinalTestResults = finalTestResults ?? c.PhysicalTestResults,
            InspectorNotes = inspectorNotes ?? c.PhysicalNotes,
            PhysicalApprovalStatus = physStatus,
            PhysicalApprovedBy = c.PhysicalApprovedBy ?? (c.ApprovalStatus == "APPROVED" ? (c.ReviewedBy ?? c.ResponsibleEmployee) : null),
            PhysicalApprovedAt = c.PhysicalApprovedAt ?? (c.ApprovalStatus == "APPROVED" ? c.ReviewedAt : null),
            PhysicalNotes = c.PhysicalNotes ?? inspectorNotes,
            PhysicalTestResults = c.PhysicalTestResults ?? finalTestResults,
            ChemicalApprovalStatus = chemStatus,
            ChemicalApprovedBy = c.ChemicalApprovedBy ?? (c.ApprovalStatus == "APPROVED" ? (c.ReviewedBy ?? c.ResponsibleEmployee) : null),
            ChemicalApprovedAt = c.ChemicalApprovedAt ?? (c.ApprovalStatus == "APPROVED" ? c.ReviewedAt : null),
            ChemicalNotes = c.ChemicalNotes,
            ChemicalTestResults = c.ChemicalTestResults,
            OverallStatus = overallStatus,
            AttachmentPath = c.AttachmentPath,
            AttachmentFileName = c.AttachmentFileName,
            AttachmentFileType = c.AttachmentFileType,
            AttachmentFileSize = c.AttachmentFileSize,
            ResponsibleEmployee = c.ResponsibleEmployee,
            ApprovalStatus = c.ApprovalStatus,
            ReviewedBy = c.ReviewedBy,
            ReviewedAt = c.ReviewedAt,
            IsStoragePosted = c.IsStoragePosted,
            StoragePostedAt = c.StoragePostedAt,
            CreatedAt = c.CreatedAt,
            CreatedBy = c.CreatedBy
        };
    }

    public static LaboratoryReferenceLookupDto MapToReferenceLookupDto(LaboratoryRawMaterialReport r)
    {
        var finalCert = r.FinalCertificates?.FirstOrDefault();
        return new LaboratoryReferenceLookupDto
        {
            ProductionReferenceNumber = r.ProductionReferenceNumber,
            SampleNumber = r.SampleNumber,
            SampleCode = !string.IsNullOrWhiteSpace(r.SampleCode) ? r.SampleCode : $"SMP-{r.SampleNumber:D4}",
            LaboratorySection = r.LaboratorySection,
            RawMaterialDescription = r.RawMaterialDescription,
            RawMaterialId = r.RawMaterialId,
            RawMaterialName = r.RawMaterial?.Name,
            SupplierName = r.SupplierName,
            SourceTransportationId = r.SourceTransportationId,
            TransportationTransactionNumber = r.TransportationTransactionNumber,
            PONumber = r.PONumber,
            ReceivedQuantity = r.ReceivedQuantity,
            AcceptedQuantity = r.AcceptedQuantity,
            RejectedQuantity = r.RejectedQuantity,
            InspectionDecision = r.InspectionDecision,
            AnalysisDateTime = r.AnalysisDateTime,
            ApprovalStatus = r.ApprovalStatus,
            IsStorageReceived = r.IsStorageReceived,
            InProcessTestsCount = r.InProcessReports?.Count ?? 0,
            HasFinalCertificate = finalCert != null,
            FinalProductName = finalCert?.FinalProductName,
            FinalProductQuantity = finalCert?.ActualFinalProductQuantity,
            ConformityDecision = finalCert?.ConformityDecision
        };
    }

    // =========================================================================
    // 9. LABORATORY PRODUCTION ORDERS (إصدار أمر إنتاج وسجل أوامر الإنتاج)
    // =========================================================================

    [HttpGet("available-warehouse-batches")]
    [HttpGet("production-orders/available-batches")]
    [ProducesResponseType(typeof(ApiResponse<List<WarehouseBatchAvailabilityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableWarehouseBatches()
    {
        var batches = await _inventoryService.GetAuthoritativeBatchesAsync();
        return Ok(ApiResponse<List<WarehouseBatchAvailabilityDto>>.Ok(batches));
    }

    [HttpGet("next-order-number")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNextOrderNumber()
    {
        var nextNum = await PreviewNextLabProductionOrderNumberAsync();
        return Ok(ApiResponse<string>.Ok(nextNum));
    }

    [HttpPost("production-orders")]
    [ProducesResponseType(typeof(ApiResponse<LaboratoryProductionOrderSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateProductionOrder([FromBody] CreateLaboratoryProductionOrderRequestDto request)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "مدير المختبر";
        Guid? userId = null;
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(subClaim, out var g))
        {
            userId = g;
        }

        if (!AppRoles.HasLaboratoryAccess(role) && role != "GeneralManager" && role != "Admin" && role != "SuperAdmin")
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بإصدار أوامر الإنتاج من المختبر."));
        }

        if (string.IsNullOrWhiteSpace(request.ProductName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج النهائي مطلوب."));
        }

        if (request.RequiredQuantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("الكمية المطلوبة للمنتج النهائي يجب أن تكون أكبر من الصفر."));
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب تحديد تركيبة المواد الخام وأرقام التشغيلات المطلوبة."));
        }

        // Validate items and quantities
        var batchIds = request.Items.Select(i => i.WarehouseBatchId).Distinct().ToList();
        var batches = await _db.WarehouseBatches
            .Include(b => b.StorageLocation)
            .Include(b => b.RawMaterial)
            .Where(b => batchIds.Contains(b.Id))
            .ToListAsync();

        if (batches.Count != batchIds.Count)
        {
            return BadRequest(ApiResponse.Fail("إحدى تشغيلات المستودع المحددة غير موجودة في النظام."));
        }

        foreach (var item in request.Items)
        {
            if (item.RequestedQuantity <= 0)
            {
                return BadRequest(ApiResponse.Fail("الكمية المطلوبة لكل مادة خام يجب أن تكون أكبر من الصفر."));
            }

            var batch = batches.FirstOrDefault(b => b.Id == item.WarehouseBatchId);
            if (batch == null)
            {
                return BadRequest(ApiResponse.Fail($"التشغيلة ذات المعرف {item.WarehouseBatchId} غير موجودة."));
            }

            if (!batch.IsActive || batch.Status == "REJECTED")
            {
                return BadRequest(ApiResponse.Fail($"التشغيلة {batch.BatchNumber} غير مطابقة للمواصفات أو مرفوضة ولا يمكن استخدامها."));
            }

            if (item.RequestedQuantity > batch.CurrentQuantity)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المطلوبة ({item.RequestedQuantity:N3} طن) من التشغيلة {batch.BatchNumber} تتجاوز الرصيد المتاح بالمستودع ({batch.CurrentQuantity:N3} طن)."));
            }
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var orderNumber = await GenerateNextLabProductionOrderNumberAsync();
            var firstBatch = batches.First();
            var storageLocationId = (request.StorageLocationId.HasValue && request.StorageLocationId.Value > 0)
                ? request.StorageLocationId.Value
                : firstBatch.StorageLocationId;
            var storageLocation = await _db.StorageLocations.FindAsync(storageLocationId);
            var storageLocationName = storageLocation?.Name ?? firstBatch.StorageLocation?.Name ?? "المستودع الرئيسي";

            var totalPlannedQty = request.Items.Sum(i => i.RequestedQuantity);

            var order = new ProductionOrder
            {
                OrderNumber = orderNumber,
                ProductName = request.ProductName.Trim(),
                ProductDescription = request.ProductDescription?.Trim(),
                RequestedQuantity = request.RequiredQuantity,
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? "طن" : request.Unit.Trim(),
                StorageLocationId = storageLocationId,
                StorageLocationName = storageLocationName,
                Status = "PENDING_TRANSFER",
                CurrentStage = "في انتظار نقل المستودع",
                ResponsibleEmployee = "مسؤول مستودع المواد الأولية",
                LaboratoryManagerName = userName,
                LaboratoryManagerId = userId,
                ProductionInstructions = request.ProductionInstructions?.Trim(),
                Notes = request.Notes?.Trim(),
                FormulaVersion = 1,
                TotalPlannedRawMaterialQuantity = totalPlannedQty,
                TotalTransferredQuantity = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            _db.ProductionOrders.Add(order);
            await _db.SaveChangesAsync(); // Generates order.Id

            foreach (var item in request.Items)
            {
                var b = batches.First(x => x.Id == item.WarehouseBatchId);
                var orderItem = new ProductionOrderItem
                {
                    ProductionOrderId = order.Id,
                    WarehouseBatchId = b.Id,
                    BatchNumber = b.BatchNumber,
                    ProductionReferenceNumber = b.ProductionReferenceNumber,
                    SampleCode = b.SampleCode,
                    RawMaterialId = b.RawMaterialId,
                    MaterialName = b.MaterialName,
                    MaterialType = !string.IsNullOrWhiteSpace(item.MaterialType) ? item.MaterialType.Trim() : (b.MaterialClassification ?? ""),
                    StorageLocationName = b.StorageLocation?.Name ?? storageLocationName,
                    AvailableQuantityBefore = b.CurrentQuantity,
                    RequestedQuantity = item.RequestedQuantity,
                    IssuedQuantity = 0,
                    ConsumedQuantity = 0,
                    Unit = string.IsNullOrWhiteSpace(item.Unit) ? "طن" : item.Unit.Trim()
                };
                _db.ProductionOrderItems.Add(orderItem);
            }

            var formulaVersion = new ProductionOrderFormulaVersion
            {
                ProductionOrderId = order.Id,
                VersionNumber = 1,
                ProductName = order.ProductName,
                ProductDescription = order.ProductDescription,
                TargetOutputQuantity = order.RequestedQuantity,
                TotalInputQuantity = totalPlannedQty,
                Unit = order.Unit,
                FormulaJson = JsonSerializer.Serialize(request.Items),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };
            _db.ProductionOrderFormulaVersions.Add(formulaVersion);

            // CRITICAL: Stock is NOT deducted upon order issuance! It will be deducted only upon physical Warehouse Transfer confirmation.
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            try
            {
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    Department = "Laboratory",
                    Action = "PRODUCTION_ORDER_CREATED",
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    Timestamp = DateTime.UtcNow
                });
                await _hubContext.Clients.All.SendAsync("OnProductionOrderChanged", new
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    Status = order.Status,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception exSignal)
            {
                _logger.LogWarning(exSignal, "Failed to broadcast production order SignalR event for {OrderNumber}", order.OrderNumber);
            }

            var summary = new LaboratoryProductionOrderSummaryDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                ProductName = order.ProductName,
                ProductDescription = order.ProductDescription,
                RequestedQuantity = order.RequestedQuantity,
                Unit = order.Unit,
                StorageLocationId = order.StorageLocationId,
                StorageLocationName = order.StorageLocationName,
                TotalPlannedRawMaterialQuantity = order.TotalPlannedRawMaterialQuantity,
                TotalTransferredQuantity = order.TotalTransferredQuantity,
                Status = order.Status,
                StatusArabic = GetProductionOrderStatusArabic(order.Status),
                LaboratoryManagerName = order.LaboratoryManagerName ?? userName,
                FormulaVersion = order.FormulaVersion,
                CreatedAt = order.CreatedAt,
                ItemsCount = request.Items.Count
            };

            return Ok(ApiResponse<LaboratoryProductionOrderSummaryDto>.Ok(summary, "تم إصدار أمر الإنتاج بنجاح وإرساله إلى مستودع المواد الأولية للنقل."));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating Laboratory Production Order");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء إصدار أمر الإنتاج: " + ex.Message));
        }
    }

    [HttpGet("production-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryProductionOrderSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductionOrders(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.OrderNumber.StartsWith("LAB-PO-") || o.FormulaVersion >= 1)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var stNorm = status.Trim().ToUpperInvariant();
            query = query.Where(o => o.Status == stNorm);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(o => o.CreatedAt <= endOfDay);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(o => o.OrderNumber.Contains(s)
                || o.ProductName.Contains(s)
                || (o.ProductDescription != null && o.ProductDescription.Contains(s))
                || o.Items.Any(i => (i.BatchNumber != null && i.BatchNumber.Contains(s))
                    || (i.SampleCode != null && i.SampleCode.Contains(s))
                    || (i.ProductionReferenceNumber.HasValue && i.ProductionReferenceNumber.Value.ToString().Contains(s))));
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var summaries = orders.Select(o => new LaboratoryProductionOrderSummaryDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            ProductName = o.ProductName,
            ProductDescription = o.ProductDescription,
            RequestedQuantity = o.RequestedQuantity,
            Unit = o.Unit,
            StorageLocationId = o.StorageLocationId,
            StorageLocationName = o.StorageLocationName,
            TotalPlannedRawMaterialQuantity = o.TotalPlannedRawMaterialQuantity,
            TotalTransferredQuantity = o.TotalTransferredQuantity,
            Status = o.Status,
            StatusArabic = GetProductionOrderStatusArabic(o.Status),
            LaboratoryManagerName = o.LaboratoryManagerName ?? o.CreatedBy,
            FormulaVersion = o.FormulaVersion,
            LinkedProductionProcessId = o.LinkedProductionProcessId,
            LinkedProductionProcessNumber = o.LinkedProductionProcessNumber,
            CreatedAt = o.CreatedAt,
            ItemsCount = o.Items.Count
        }).ToList();

        return Ok(ApiResponse<List<LaboratoryProductionOrderSummaryDto>>.Ok(summaries));
    }

    [HttpGet("production-orders/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<LaboratoryProductionOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductionOrderById(int id)
    {
        var order = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Transfers)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(ApiResponse.Fail("أمر الإنتاج المطلوب غير موجود."));
        }

        // Load current batch quantities for comparison
        var batchIds = order.Items.Select(i => i.WarehouseBatchId ?? 0).Where(b => b > 0).Distinct().ToList();
        var batchNumbers = order.Items.Select(i => i.BatchNumber).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToList();

        var currentBatches = await _db.WarehouseBatches
            .AsNoTracking()
            .Where(b => batchIds.Contains(b.Id) || (b.BatchNumber != null && batchNumbers.Contains(b.BatchNumber)))
            .ToListAsync();

        var currentBatchesById = currentBatches.ToDictionary(b => b.Id, b => b.CurrentQuantity);
        var currentBatchesByNumber = currentBatches
            .Where(b => !string.IsNullOrWhiteSpace(b.BatchNumber))
            .GroupBy(b => b.BatchNumber!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CurrentQuantity).First().CurrentQuantity);

        var itemDtos = order.Items.Select(i => {
            decimal curQty = 0;
            if (i.WarehouseBatchId.HasValue && currentBatchesById.ContainsKey(i.WarehouseBatchId.Value))
            {
                curQty = currentBatchesById[i.WarehouseBatchId.Value];
            }
            else if (!string.IsNullOrWhiteSpace(i.BatchNumber) && currentBatchesByNumber.ContainsKey(i.BatchNumber))
            {
                curQty = currentBatchesByNumber[i.BatchNumber];
            }

            return new LaboratoryProductionOrderItemDto
            {
                Id = i.Id,
                WarehouseBatchId = i.WarehouseBatchId ?? 0,
                BatchNumber = i.BatchNumber ?? string.Empty,
                ProductionReferenceNumber = i.ProductionReferenceNumber ?? 0,
                SampleCode = i.SampleCode ?? string.Empty,
                RawMaterialId = i.RawMaterialId,
                MaterialName = i.MaterialName,
                MaterialType = i.MaterialType ?? string.Empty,
                StorageLocationName = i.StorageLocationName ?? order.StorageLocationName,
                AvailableQuantityBefore = i.AvailableQuantityBefore,
                CurrentWarehouseBatchQuantity = curQty,
                RequestedQuantity = i.RequestedQuantity,
                IssuedQuantity = i.IssuedQuantity,
                Unit = i.Unit
            };
        }).ToList();

        var transferDtos = order.Transfers.OrderByDescending(t => t.ConfirmedAt).Select(t => new LaboratoryProductionOrderTransferDto
        {
            Id = t.Id,
            ProductionOrderItemId = t.ProductionOrderItemId,
            WarehouseBatchId = t.WarehouseBatchId,
            BatchNumber = t.BatchNumber,
            MaterialName = t.MaterialName,
            TransferredQuantity = t.TransferredQuantity,
            Unit = t.Unit,
            StorageLocationName = t.StorageLocationName,
            ConfirmedBy = t.ConfirmedBy,
            ConfirmedAt = t.ConfirmedAt,
            TransferTransactionNumber = t.TransferTransactionNumber,
            Notes = t.Notes
        }).ToList();

        ProductionProcessDetailDto? linkedProcessDto = null;
        if (order.LinkedProductionProcessId.HasValue)
        {
            var proc = await _db.ProductionProcesses
                .AsNoTracking()
                .Include(p => p.Materials)
                .Include(p => p.FinishedProducts)
                .FirstOrDefaultAsync(p => p.Id == order.LinkedProductionProcessId.Value);

            if (proc != null)
            {
                linkedProcessDto = new ProductionProcessDetailDto
                {
                    Id = proc.Id,
                    ProcessNumber = proc.ProcessNumber,
                    ProductionDate = proc.ProductionDate,
                    StorageLocationId = proc.StorageLocationId,
                    StorageLocationName = proc.StorageLocationName,
                    TotalInputQuantity = proc.TotalInputQuantity,
                    TotalOutputQuantity = proc.TotalOutputQuantity,
                    TotalWasteQuantity = proc.TotalWasteQuantity,
                    WastePercentage = proc.WastePercentage,
                    Unit = proc.Unit,
                    Status = proc.Status,
                    ResponsibleEmployee = proc.ResponsibleEmployee,
                    Notes = proc.Notes,
                    CreatedAt = proc.CreatedAt,
                    CreatedBy = proc.CreatedBy,
                    Materials = proc.Materials.Select(m => new ProductionProcessMaterialDto
                    {
                        Id = m.Id,
                        ProductionProcessId = m.ProductionProcessId,
                        WarehouseBatchId = m.WarehouseBatchId,
                        BatchNumber = m.BatchNumber,
                        LaboratoryReportId = m.LaboratoryReportId,
                        ProductionReferenceNumber = m.ProductionReferenceNumber,
                        SampleCode = m.SampleCode,
                        RawMaterialId = m.RawMaterialId,
                        MaterialName = m.MaterialName,
                        MaterialClassification = m.MaterialClassification,
                        StorageLocationId = m.StorageLocationId,
                        StorageLocationName = m.StorageLocationName,
                        AvailableQuantityBefore = m.AvailableQuantityBefore,
                        ConsumedQuantity = m.ConsumedQuantity,
                        RemainingQuantityAfter = m.RemainingQuantityAfter,
                        Unit = m.Unit,
                        CreatedAt = m.CreatedAt
                    }).ToList(),
                    FinishedProducts = proc.FinishedProducts.Select(f => new ProductionFinishedProductDto
                    {
                        Id = f.Id,
                        ProductionProcessId = f.ProductionProcessId,
                        ProcessNumber = proc.ProcessNumber,
                        FinishedProductName = f.FinishedProductName,
                        ManufactureNumber = f.ManufactureNumber,
                        ManufacturedQuantity = f.ManufacturedQuantity,
                        Unit = f.Unit,
                        ProductionDate = f.ProductionDate,
                        Notes = f.Notes,
                        CreatedAt = f.CreatedAt,
                        CreatedBy = f.CreatedBy
                    }).ToList()
                };
            }
        }

        var detail = new LaboratoryProductionOrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            ProductName = order.ProductName,
            ProductDescription = order.ProductDescription,
            RequestedQuantity = order.RequestedQuantity,
            Unit = order.Unit,
            StorageLocationId = order.StorageLocationId,
            StorageLocationName = order.StorageLocationName,
            TotalPlannedRawMaterialQuantity = order.TotalPlannedRawMaterialQuantity,
            TotalTransferredQuantity = order.TotalTransferredQuantity,
            Status = order.Status,
            StatusArabic = GetProductionOrderStatusArabic(order.Status),
            LaboratoryManagerName = order.LaboratoryManagerName ?? order.CreatedBy,
            FormulaVersion = order.FormulaVersion,
            LinkedProductionProcessId = order.LinkedProductionProcessId,
            LinkedProductionProcessNumber = order.LinkedProductionProcessNumber,
            ProductionInstructions = order.ProductionInstructions,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            ItemsCount = order.Items.Count,
            Items = itemDtos,
            Transfers = transferDtos,
            LinkedProcess = linkedProcessDto
        };

        return Ok(ApiResponse<LaboratoryProductionOrderDetailDto>.Ok(detail));
    }

    [HttpPost("production-orders/{id:int}/cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelProductionOrder(int id)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound(ApiResponse.Fail("أمر الإنتاج غير موجود."));
        }

        if (order.Status != "PENDING_TRANSFER")
        {
            return BadRequest(ApiResponse.Fail("لا يمكن إلغاء أمر الإنتاج لأنه تم البدء في نقله أو تصنيعه بالفعل."));
        }

        order.Status = "CANCELLED";
        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("تم إلغاء أمر الإنتاج بنجاح."));
    }

    private async Task<string> GenerateNextLabProductionOrderNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        int nextVal = 1;
        if (_db.Database.IsSqlServer())
        {
            var result = await _db.Database
                .SqlQueryRaw<int>(
                    "SELECT NextValue FROM ProductionSequences WITH (UPDLOCK, ROWLOCK) WHERE SequenceType = {0}",
                    "LAB_PO")
                .ToListAsync();

            if (result.Count > 0)
            {
                nextVal = result[0];
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE ProductionSequences SET NextValue = NextValue + 1 WHERE SequenceType = {0}",
                    "LAB_PO");
            }
            else
            {
                var seq = new ProductionSequence { SequenceType = "LAB_PO", NextValue = 2 };
                _db.ProductionSequences.Add(seq);
                await _db.SaveChangesAsync();
                nextVal = 1;
            }
        }
        else
        {
            var seq = await _db.ProductionSequences.FirstOrDefaultAsync(s => s.SequenceType == "LAB_PO");
            if (seq == null)
            {
                seq = new ProductionSequence { SequenceType = "LAB_PO", NextValue = 2 };
                _db.ProductionSequences.Add(seq);
                await _db.SaveChangesAsync();
                nextVal = 1;
            }
            else
            {
                nextVal = seq.NextValue;
                seq.NextValue += 1;
                await _db.SaveChangesAsync();
            }
        }

        return $"LAB-PO-{year}-{nextVal:D6}";
    }

    private async Task<string> PreviewNextLabProductionOrderNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var seq = await _db.ProductionSequences.AsNoTracking().FirstOrDefaultAsync(s => s.SequenceType == "LAB_PO");
        var val = seq?.NextValue ?? 1;
        return $"LAB-PO-{year}-{val:D6}";
    }

    private static string GetProductionOrderStatusArabic(string status) => status switch
    {
        "PENDING_TRANSFER" => "في انتظار نقل المستودع",
        "PARTIALLY_TRANSFERRED" => "تم النقل جزئياً",
        "TRANSFERRED_TO_PRODUCTION" => "تم النقل للإنتاج",
        "IN_PRODUCTION" => "قيد التصنيع بالإنتاج",
        "COMPLETED" => "مكتمل التصنيع",
        "CANCELLED" => "ملغي",
        _ => status
    };
}
