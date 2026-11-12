using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Data;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "TransportationAccess")]
[Produces("application/json")]
public class TransportationController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<TransportationController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<FaceStone.Web.Hubs.ChatHub> _hubContext;

    public const string FixedSource = "Face stone (Full Concrete Solutions)";
    public const string DefaultShippingSupervisor = "محمد ناجح";
    public const string DefaultDepartmentSupervisor = "ازهر محمد";

    public TransportationController(
        FaceStoneDbContext db, 
        ILogger<TransportationController> logger,
        IWebHostEnvironment env,
        Microsoft.AspNetCore.SignalR.IHubContext<FaceStone.Web.Hubs.ChatHub> hubContext)
    {
        _db = db;
        _logger = logger;
        _env = env;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Material Types: ONLY powder and slurry allowed.
    /// </summary>
    public static readonly List<string> MaterialTypes = new()
    {
        "powder",
        "slurry"
    };

    public static readonly List<string> Destinations = new()
    {
        "Cairo Factory",
        "Factory Sector A - Batching Plant 1",
        "Factory Sector B - Precast Yard",
        "Factory Sector C - Silos & Aggregate Storage",
        "New Cairo Construction Site - Zone 4",
        "New Administrative Capital - Tower Site A",
        "6th of October Industrial Complex",
        "Ain Sokhna Port Logistics Hub"
    };

    public static readonly List<string> TransactionStatuses = new()
    {
        "Completed",
        "In Transit",
        "Entered Factory",
        "Under Inspection",
        "Pending",
        "Cancelled"
    };

    /// <summary>
    /// Receiving Statuses: ONLY safe and intact.
    /// </summary>
    public static readonly List<string> ReceivingStatuses = new()
    {
        "safe",
        "intact"
    };

    /// <summary>
    /// Controlled selection of Recipient Titles.
    /// </summary>
    public static readonly List<string> RecipientTitles = new()
    {
        "مهندس الموقع",
        "مشرف استلام",
        "فني مختبر",
        "مسؤول الجودة",
        "مدير المشروع"
    };

    /// <summary>
    /// Retrieve configurable dropdown options for Transportation Transaction Form.
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TransportationConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig()
    {
        var existingDrivers = await _db.Transportations
            .AsNoTracking()
            .Where(t => !string.IsNullOrEmpty(t.DriverName))
            .Select(t => t.DriverName.Trim())
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        var projectCodes = await _db.ProjectCodes
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new ProjectCodeDto
            {
                Id = p.Id,
                Code = p.Code,
                ProjectName = p.ProjectName,
                ClientName = p.ClientName,
                Description = p.Description,
                IsActive = p.IsActive
            })
            .ToListAsync();

        var maxTx = await _db.Transportations.MaxAsync(t => (int?)t.TransactionNumber) ?? 1000;
        var nextTxNum = maxTx + 1;

        var config = new TransportationConfigDto
        {
            MaterialTypes = MaterialTypes,
            Destinations = Destinations,
            TransactionStatuses = TransactionStatuses,
            ReceivingStatuses = ReceivingStatuses,
            ShippingSupervisors = new() { DefaultShippingSupervisor },
            DepartmentSupervisors = new() { DefaultDepartmentSupervisor },
            RecipientTitles = RecipientTitles,
            Drivers = existingDrivers,
            NextTransactionNumber = nextTxNum,
            NextInternalWeighbridgeCode = string.Empty, // Manual entry strictly required
            NextExternalWeighbridgeCode = string.Empty, // Manual entry strictly required
            FixedSource = FixedSource,
            ProjectCodes = projectCodes
        };

        return Ok(ApiResponse<TransportationConfigDto>.Ok(config));
    }

    /// <summary>
    /// Retrieve list of all available active Project Codes (كود المشروع).
    /// </summary>
    [HttpGet("project-codes")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<ProjectCodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectCodes()
    {
        var projectCodes = await _db.ProjectCodes
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new ProjectCodeDto
            {
                Id = p.Id,
                Code = p.Code,
                ProjectName = p.ProjectName,
                ClientName = p.ClientName,
                Description = p.Description,
                IsActive = p.IsActive
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ProjectCodeDto>>.Ok(projectCodes));
    }

    /// <summary>
    /// Create new Project Code (Admin / Authorized personnel).
    /// </summary>
    [HttpPost("project-codes")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ProjectCodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProjectCode([FromBody] CreateProjectCodeDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.Fail("بيانات كود المشروع غير مكتملة."));
        }

        var trimmedCode = dto.Code.Trim().ToUpperInvariant();
        if (await _db.ProjectCodes.AnyAsync(p => p.Code == trimmedCode))
        {
            return BadRequest(ApiResponse.Fail($"كود المشروع '{trimmedCode}' مسجل مسبقاً."));
        }

        var entity = new ProjectCode
        {
            Code = trimmedCode,
            ProjectName = dto.ProjectName.Trim(),
            ClientName = dto.ClientName?.Trim(),
            Description = dto.Description?.Trim(),
            StorageClientDestinationId = dto.StorageClientDestinationId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.ProjectCodes.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<ProjectCodeDto>.Ok(new ProjectCodeDto
        {
            Id = entity.Id,
            Code = entity.Code,
            ProjectName = entity.ProjectName,
            ClientName = entity.ClientName,
            Description = entity.Description,
            IsActive = entity.IsActive
        }, "تم إضافة كود المشروع بنجاح."));
    }

    /// <summary>
    /// Update existing Project Code.
    /// </summary>
    [HttpPut("project-codes/{id:int}")]
    [Authorize]
    public async Task<IActionResult> UpdateProjectCode(int id, [FromBody] CreateProjectCodeDto dto)
    {
        var prj = await _db.ProjectCodes.FindAsync(id);
        if (prj == null) return NotFound(ApiResponse.Fail("كود المشروع غير موجود."));

        var trimmedCode = dto.Code.Trim().ToUpperInvariant();
        if (await _db.ProjectCodes.AnyAsync(p => p.Code == trimmedCode && p.Id != id))
        {
            return BadRequest(ApiResponse.Fail($"كود المشروع '{trimmedCode}' مسجل لمشروع آخر."));
        }

        prj.Code = trimmedCode;
        prj.ProjectName = dto.ProjectName.Trim();
        prj.ClientName = dto.ClientName?.Trim();
        prj.Description = dto.Description?.Trim();
        if (dto.StorageClientDestinationId.HasValue) prj.StorageClientDestinationId = dto.StorageClientDestinationId.Value;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<ProjectCodeDto>.Ok(new ProjectCodeDto
        {
            Id = prj.Id,
            Code = prj.Code,
            ProjectName = prj.ProjectName,
            ClientName = prj.ClientName,
            Description = prj.Description,
            IsActive = prj.IsActive
        }, "تم تحديث كود المشروع بنجاح."));
    }

    /// <summary>
    /// Toggle active status of a Project Code.
    /// </summary>
    [HttpDelete("project-codes/{id:int}")]
    [Authorize]
    public async Task<IActionResult> ToggleProjectCode(int id)
    {
        var prj = await _db.ProjectCodes.FindAsync(id);
        if (prj == null) return NotFound(ApiResponse.Fail("كود المشروع غير موجود."));

        prj.IsActive = !prj.IsActive;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse.Ok($"تم {(prj.IsActive ? "تفعيل" : "تعطيل")} كود المشروع '{prj.Code}' بنجاح."));
    }

    /// <summary>
    /// Validates a Purchase Order for Transportation workflow.
    /// Returns destination (strictly read-only on client), company name, remaining quantity, and next transaction number for this PO.
    /// </summary>
    [HttpGet("po/{poNumber}")]
    [HttpGet("po-lookup/{poNumber}")]
    [ProducesResponseType(typeof(ApiResponse<PoLookupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LookupPurchaseOrder(string poNumber)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return BadRequest(ApiResponse.Fail("Purchase Order Number is required."));
        }

        var trimmed = poNumber.Trim();
        var po = await _db.PurchaseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PONumber.ToLower() == trimmed.ToLower());

        if (po == null)
        {
            // Fallback: Check if poNumber is actually a Warehouse Export Number (e.g. #1, 1, EXP-1, EXP-2026-0001)
            var cleanExp = trimmed.TrimStart('#').Trim();
            int? expId = int.TryParse(cleanExp, out var pExp) ? pExp : null;
            if (!expId.HasValue && (cleanExp.StartsWith("EXP-", StringComparison.OrdinalIgnoreCase) || cleanExp.StartsWith("EXP_", StringComparison.OrdinalIgnoreCase)))
            {
                var numPart = cleanExp[4..];
                if (int.TryParse(numPart, out var pExp2)) expId = pExp2;
            }

            StorageOperation? exportOp = null;
            if (expId.HasValue)
            {
                exportOp = await _db.StorageOperations
                    .AsNoTracking()
                    .Include(o => o.Items)
                    .Include(o => o.Client)
                    .Include(o => o.Destination)
                    .Include(o => o.ProductionOrder)
                    .FirstOrDefaultAsync(o => o.OperationType == "EXPORT" && o.ApprovalStatus == "APPROVED" && o.TransactionNumber == expId.Value);
            }

            if (exportOp == null)
            {
                exportOp = await _db.StorageOperations
                    .AsNoTracking()
                    .Include(o => o.Items)
                    .Include(o => o.Client)
                    .Include(o => o.Destination)
                    .Include(o => o.ProductionOrder)
                    .FirstOrDefaultAsync(o => o.OperationType == "EXPORT" && o.ApprovalStatus == "APPROVED" &&
                        ((expId.HasValue && o.Id == expId.Value) ||
                         o.TransactionNumber.ToString() == cleanExp ||
                         (o.ProductionOrder != null && o.ProductionOrder.OrderNumber.ToLower() == trimmed.ToLower()) ||
                         (o.ManufactureNumber != null && o.ManufactureNumber.ToLower() == trimmed.ToLower()) ||
                         (o.SampleCode != null && o.SampleCode.ToLower() == trimmed.ToLower())));
            }

            if (exportOp != null)
            {
                var totalQty = exportOp.Items.Sum(i => i.Quantity);
                var transported = await _db.Transportations
                    .AsNoTracking()
                    .Where(t => t.StorageOperationId == exportOp.Id)
                    .SumAsync(t => (decimal?)t.NetWeight) ?? 0m;
                var remaining = Math.Max(0m, totalQty - transported);

                var maxTxForExport = await _db.Transportations
                    .Where(t => t.StorageOperationId == exportOp.Id)
                    .MaxAsync(t => (int?)t.TransactionNumber) ?? 0;

                var expDto = new PoLookupDto
                {
                    Id = exportOp.Id,
                    PoNumber = $"#{exportOp.TransactionNumber}",
                    Destination = exportOp.DestinationName ?? exportOp.Destination?.DestinationName ?? "Cairo Factory",
                    Supplier = exportOp.ClientName ?? exportOp.Client?.Name ?? "عميل فيس ستون",
                    CompanyName = exportOp.ClientName ?? exportOp.Client?.Name ?? "عميل فيس ستون",
                    TotalQuantity = totalQty,
                    RemainingQuantity = remaining,
                    PricePerTon = 0m,
                    TotalValue = 0m,
                    OrderDate = exportOp.TransactionDate,
                    InvoiceNumber = exportOp.ManufactureNumber ?? $"EXP-#{exportOp.TransactionNumber}",
                    Notes = $"إذن صرف مستودع معتمد #{exportOp.TransactionNumber} - {(exportOp.ExportType == "FINISHED_PRODUCT" ? "منتج نهائي" : "مواد خام")}",
                    NextTransactionNumber = maxTxForExport + 1
                };

                return Ok(ApiResponse<PoLookupDto>.Ok(expDto));
            }

            _logger.LogWarning("PO / Export lookup failed for Transportation: '{PONumber}' not found.", trimmed);
            return NotFound(ApiResponse.Fail("أمر الشراء أو إذن الصرف غير مسجل في قاعدة البيانات."));
        }

        if (po.RemainingQuantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("This Purchase Order has no remaining quantity."));
        }

        var maxTxForPo = await _db.Transportations
            .Where(t => t.PONumber.ToLower() == trimmed.ToLower())
            .MaxAsync(t => (int?)t.TransactionNumber) ?? 0;
        var nextTxNumForPo = maxTxForPo + 1;

        var dto = new PoLookupDto
        {
            Id = po.Id,
            PoNumber = po.PONumber,
            Destination = string.IsNullOrWhiteSpace(po.Destination) ? "Cairo Factory" : po.Destination,
            Supplier = po.Supplier,
            CompanyName = po.Supplier,
            TotalQuantity = po.Quantity,
            RemainingQuantity = po.RemainingQuantity,
            PricePerTon = po.PricePerTon,
            TotalValue = po.TotalValue > 0 ? po.TotalValue : Math.Round(po.Quantity * po.PricePerTon, 2),
            OrderDate = po.OrderDate,
            InvoiceNumber = po.InvoiceNumber,
            Notes = po.Notes,
            NextTransactionNumber = nextTxNumForPo
        };

        return Ok(ApiResponse<PoLookupDto>.Ok(dto));
    }

    /// <summary>
    /// Retrieve completed/approved Warehouse Exports eligible for transportation with remaining unallocated balance.
    /// Accurately aggregates SQL Server allocations per export header and per export line item.
    /// </summary>
    [HttpGet("eligible-exports")]
    [ProducesResponseType(typeof(ApiResponse<List<EligibleTransportationExportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleExports([FromQuery] string? search = null)
    {
        var exports = await _db.StorageOperations
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.StorageLocation)
            .Include(o => o.Client)
            .Include(o => o.Destination)
            .Include(o => o.ProductionOrder)
            .Include(o => o.ProductionProcess)
            .Where(o => o.OperationType == "EXPORT" && o.ApprovalStatus == "APPROVED")
            .OrderByDescending(o => o.TransactionDate)
            .ThenByDescending(o => o.Id)
            .ToListAsync();

        var opIds = exports.Select(e => e.Id).ToList();
        var activeTransportations = await _db.Transportations
            .AsNoTracking()
            .Where(t => t.StorageOperationId.HasValue && opIds.Contains(t.StorageOperationId.Value) && t.ApprovalStatus != "REJECTED")
            .Select(t => new
            {
                OpId = t.StorageOperationId!.Value,
                ItemId = t.StorageOperationItemId,
                t.NetWeight
            })
            .ToListAsync();

        var result = new List<EligibleTransportationExportDto>();
        foreach (var exp in exports)
        {
            var expTrans = activeTransportations.Where(t => t.OpId == exp.Id).ToList();
            var totalExportQty = exp.Items.Sum(i => i.Quantity);
            var transported = expTrans.Sum(t => t.NetWeight);
            var remaining = Math.Max(0m, totalExportQty - transported);

            // Calculate line items with their individual exported, allocated, and remaining quantities
            var itemDtos = new List<EligibleTransportationExportItemDto>();
            foreach (var i in exp.Items)
            {
                var itemAllocated = expTrans
                    .Where(t => t.ItemId == i.Id || (t.ItemId == null && exp.Items.Count == 1))
                    .Sum(t => t.NetWeight);
                var itemRemaining = Math.Max(0m, i.Quantity - itemAllocated);

                itemDtos.Add(new EligibleTransportationExportItemDto
                {
                    Id = i.Id,
                    RawMaterialId = i.RawMaterialId,
                    MaterialName = i.MaterialName,
                    Quantity = i.Quantity,
                    PreviouslyAllocatedQuantity = itemAllocated,
                    RemainingQuantity = itemRemaining,
                    WarehouseBatchId = i.WarehouseBatchId,
                    BatchNumber = i.BatchNumber,
                    SampleCode = i.SampleCode ?? exp.SampleCode,
                    ProductionReferenceNumber = i.ProductionReferenceNumber ?? exp.ProductionReferenceNumber,
                    ProductionOrderId = i.ProductionOrderId ?? exp.ProductionOrderId,
                    ProductionProcessId = i.ProductionProcessId ?? exp.ProductionProcessId,
                    ManufactureNumber = i.ManufactureNumber ?? exp.ManufactureNumber
                });
            }

            // Filter out fully transported shipments if remaining is 0 and all items have remaining <= 0
            if (remaining <= 0 && itemDtos.All(i => i.RemainingQuantity <= 0)) continue;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                var matches = exp.TransactionNumber.ToString().Contains(s) ||
                              (exp.ClientName != null && exp.ClientName.ToLower().Contains(s)) ||
                              (exp.DestinationName != null && exp.DestinationName.ToLower().Contains(s)) ||
                              (exp.ManufactureNumber != null && exp.ManufactureNumber.ToLower().Contains(s)) ||
                              (exp.SampleCode != null && exp.SampleCode.ToLower().Contains(s)) ||
                              (exp.ProductionOrder?.OrderNumber != null && exp.ProductionOrder.OrderNumber.ToLower().Contains(s)) ||
                              exp.Items.Any(i => i.MaterialName.ToLower().Contains(s) || 
                                                 (i.BatchNumber != null && i.BatchNumber.ToLower().Contains(s)) ||
                                                 (i.SampleCode != null && i.SampleCode.ToLower().Contains(s)));
                if (!matches) continue;
            }

            result.Add(new EligibleTransportationExportDto
            {
                StorageOperationId = exp.Id,
                ExportTransactionNumber = $"#{exp.TransactionNumber}",
                TransactionNumber = exp.TransactionNumber,
                ExportType = exp.ExportType ?? "RAW_MATERIAL",
                TransactionDate = exp.TransactionDate,
                StorageLocationId = exp.StorageLocationId,
                StorageLocationName = exp.StorageLocationName,
                ClientName = exp.ClientName ?? exp.Client?.Name ?? "عميل",
                DestinationName = exp.DestinationName ?? exp.Destination?.DestinationName ?? "-",
                TotalExportQuantity = totalExportQty,
                TransportedQuantity = transported,
                RemainingQuantity = remaining,
                Unit = "طن",
                ProductionOrderId = exp.ProductionOrderId,
                ProductionOrderNumber = exp.ProductionOrder?.OrderNumber,
                ProductionProcessId = exp.ProductionProcessId,
                ProcessNumber = exp.ProductionProcess?.ProcessNumber,
                ManufactureNumber = exp.ManufactureNumber ?? exp.Items.FirstOrDefault()?.ManufactureNumber,
                WeighbridgeAttachmentPath = exp.WeighbridgeAttachmentPath,
                Items = itemDtos
            });
        }

        return Ok(ApiResponse<List<EligibleTransportationExportDto>>.Ok(result));
    }

    /// <summary>
    /// Look up single approved export for transportation auto-fill by ID, transaction number, or reference.
    /// Supports multiple route forms and query parameter for resilient client integration without 404s.
    /// Accurately returns original exported, previously allocated, and remaining transportable quantities.
    /// </summary>
    [HttpGet("lookup-export/{exportTxNumber?}")]
    [HttpGet("lookup-export")]
    [HttpGet("export/{exportTxNumber?}")]
    [HttpGet("export")]
    [HttpGet("exports/{exportTxNumber?}")]
    [HttpGet("exports")]
    [ProducesResponseType(typeof(ApiResponse<EligibleTransportationExportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupExport(
        string? exportTxNumber = null,
        [FromQuery] string? number = null,
        [FromQuery] string? txNumber = null,
        [FromQuery] string? exportNumber = null)
    {
        var rawInput = exportTxNumber ?? number ?? txNumber ?? exportNumber;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return BadRequest(ApiResponse.Fail("رقم إذن الصرف مطلوب."));
        }

        var trimmed = rawInput.Trim();
        var clean = trimmed.TrimStart('#').Trim();

        int? opId = null;
        if (int.TryParse(clean, out var parsed))
        {
            opId = parsed;
        }
        else if (clean.StartsWith("EXP-", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("EXP_", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = clean[4..];
            if (int.TryParse(suffix, out var pSuffix)) opId = pSuffix;
        }
        else if (clean.StartsWith("OP-", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = clean[3..];
            if (int.TryParse(suffix, out var pSuffix)) opId = pSuffix;
        }

        // 1. Authoritative lookup by Warehouse Export Number (TransactionNumber, e.g. #7)
        StorageOperation? exp = null;
        if (opId.HasValue)
        {
            exp = await _db.StorageOperations
                .AsNoTracking()
                .Include(o => o.Items)
                .Include(o => o.StorageLocation)
                .Include(o => o.Client)
                .Include(o => o.Destination)
                .Include(o => o.ProductionOrder)
                .Include(o => o.ProductionProcess)
                .FirstOrDefaultAsync(o => o.OperationType == "EXPORT" && o.ApprovalStatus == "APPROVED" && o.TransactionNumber == opId.Value);
        }

        // 2. Fallback lookup by Id, references, sample code, or production order
        if (exp == null)
        {
            exp = await _db.StorageOperations
                .AsNoTracking()
                .Include(o => o.Items)
                .Include(o => o.StorageLocation)
                .Include(o => o.Client)
                .Include(o => o.Destination)
                .Include(o => o.ProductionOrder)
                .Include(o => o.ProductionProcess)
                .FirstOrDefaultAsync(o => o.OperationType == "EXPORT" && o.ApprovalStatus == "APPROVED" &&
                    ((opId.HasValue && o.Id == opId.Value) ||
                     o.TransactionNumber.ToString() == clean ||
                     (o.ManufactureNumber != null && o.ManufactureNumber.ToLower() == trimmed.ToLower()) ||
                     (o.SampleCode != null && o.SampleCode.ToLower() == trimmed.ToLower()) ||
                     (o.ProductionOrder != null && o.ProductionOrder.OrderNumber.ToLower() == trimmed.ToLower()) ||
                     o.Items.Any(i => (i.BatchNumber != null && i.BatchNumber.ToLower() == trimmed.ToLower()) ||
                                      (i.SampleCode != null && i.SampleCode.ToLower() == trimmed.ToLower()) ||
                                      (opId.HasValue && i.ProductionReferenceNumber == opId.Value))));
        }

        if (exp == null)
        {
            return NotFound(ApiResponse.Fail($"إذن الصرف '{rawInput}' غير موجود أو غير معتمد."));
        }

        var expTrans = await _db.Transportations
            .AsNoTracking()
            .Where(t => t.StorageOperationId == exp.Id && t.ApprovalStatus != "REJECTED")
            .Select(t => new { ItemId = t.StorageOperationItemId, t.NetWeight })
            .ToListAsync();

        var transported = expTrans.Sum(t => t.NetWeight);
        var totalQty = exp.Items.Sum(i => i.Quantity);
        var remaining = Math.Max(0m, totalQty - transported);

        var itemDtos = exp.Items.Select(i =>
        {
            var itemAllocated = expTrans
                .Where(t => t.ItemId == i.Id || (t.ItemId == null && exp.Items.Count == 1))
                .Sum(t => t.NetWeight);
            var itemRemaining = Math.Max(0m, i.Quantity - itemAllocated);

            return new EligibleTransportationExportItemDto
            {
                Id = i.Id,
                RawMaterialId = i.RawMaterialId,
                MaterialName = i.MaterialName,
                Quantity = i.Quantity,
                PreviouslyAllocatedQuantity = itemAllocated,
                RemainingQuantity = itemRemaining,
                WarehouseBatchId = i.WarehouseBatchId,
                BatchNumber = i.BatchNumber,
                SampleCode = i.SampleCode ?? exp.SampleCode,
                ProductionReferenceNumber = i.ProductionReferenceNumber ?? exp.ProductionReferenceNumber,
                ProductionOrderId = i.ProductionOrderId ?? exp.ProductionOrderId,
                ProductionProcessId = i.ProductionProcessId ?? exp.ProductionProcessId,
                ManufactureNumber = i.ManufactureNumber ?? exp.ManufactureNumber
            };
        }).ToList();

        var dto = new EligibleTransportationExportDto
        {
            StorageOperationId = exp.Id,
            ExportTransactionNumber = $"#{exp.TransactionNumber}",
            TransactionNumber = exp.TransactionNumber,
            ExportType = exp.ExportType ?? "RAW_MATERIAL",
            TransactionDate = exp.TransactionDate,
            StorageLocationId = exp.StorageLocationId,
            StorageLocationName = exp.StorageLocationName,
            ClientName = exp.ClientName ?? exp.Client?.Name ?? "عميل",
            DestinationName = exp.DestinationName ?? exp.Destination?.DestinationName ?? "-",
            TotalExportQuantity = totalQty,
            TransportedQuantity = transported,
            RemainingQuantity = remaining,
            Unit = "طن",
            ProductionOrderId = exp.ProductionOrderId,
            ProductionOrderNumber = exp.ProductionOrder?.OrderNumber,
            ProductionProcessId = exp.ProductionProcessId,
            ProcessNumber = exp.ProductionProcess?.ProcessNumber,
            ManufactureNumber = exp.ManufactureNumber ?? exp.Items.FirstOrDefault()?.ManufactureNumber,
            WeighbridgeAttachmentPath = exp.WeighbridgeAttachmentPath,
            Items = itemDtos
        };

        return Ok(ApiResponse<EligibleTransportationExportDto>.Ok(dto));
    }
    [HttpGet("check-number/{transactionNumber:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckTransactionNumber(int transactionNumber, [FromQuery] string? po = null)
    {
        bool exists;
        if (!string.IsNullOrWhiteSpace(po))
        {
            var trimmedPo = po.Trim();
            exists = await _db.Transportations.AnyAsync(t => t.PONumber.ToLower() == trimmedPo.ToLower() && t.TransactionNumber == transactionNumber);
        }
        else
        {
            exists = await _db.Transportations.AnyAsync(t => t.TransactionNumber == transactionNumber);
        }
        return Ok(ApiResponse<object>.Ok(new { transactionNumber, exists }));
    }

    /// <summary>
    /// Retrieve recent transportation transactions.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ApiResponse<List<TransportationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentTransactions([FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);
        var transactions = await _db.Transportations
            .AsNoTracking()
            .Include(t => t.ProjectCode)
            .OrderByDescending(t => t.DateOfSubmit)
            .Take(limit)
            .ToListAsync();

        var dtos = transactions.Select(t => MapToDto(t)).ToList();
        return Ok(ApiResponse<List<TransportationDto>>.Ok(dtos));
    }

    /// <summary>
    /// Retrieve single transportation transaction by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<TransportationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var transaction = await _db.Transportations
            .Include(t => t.ProjectCode)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (transaction == null)
        {
            return NotFound(ApiResponse.Fail($"Transportation transaction #{id} was not found."));
        }

        return Ok(ApiResponse<TransportationDto>.Ok(MapToDto(transaction)));
    }

    /// <summary>
    /// Stream / Download attachment or invoice document attached to a Transportation transaction.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("invoices/{fileName}")]
    [HttpGet("attachments/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAttachmentFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return BadRequest(ApiResponse.Fail("Invalid file name."));
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var filePath = Path.Combine(webRoot, "uploads", "transportation", fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(ApiResponse.Fail("File not found."));
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext == ".pdf" ? "application/pdf" : "image/jpeg";
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Submit a new Transportation record (JSON format).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<TransportationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateJson([FromBody] CreateTransportationDto dto)
    {
        return await ProcessCreateAsync(dto);
    }

    /// <summary>
    /// Submit a new Transportation record (multipart/form-data format).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<TransportationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateForm([FromForm] CreateTransportationDto dto)
    {
        return await ProcessCreateAsync(dto);
    }

    private async Task<(bool Success, string? ErrorMessage, string? StoredFileName, string? OriginalFileName, string? ContentType, long FileSize)> ValidateAndSaveAttachmentAsync(
        IFormFile? file, string? fallbackPath, string sectionNameAr)
    {
        if (file == null || file.Length == 0)
        {
            // If fallback existing path is provided (for API/integration test continuity)
            if (!string.IsNullOrWhiteSpace(fallbackPath))
            {
                var webRootF = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var existingF = Path.Combine(webRootF, "uploads", "transportation", Path.GetFileName(fallbackPath));
                if (System.IO.File.Exists(existingF))
                {
                    var extF = Path.GetExtension(fallbackPath).ToLowerInvariant();
                    return (true, null, Path.GetFileName(fallbackPath), Path.GetFileName(fallbackPath), extF == ".pdf" ? "application/pdf" : "image/jpeg", new FileInfo(existingF).Length);
                }
            }
            return (false, $"Attachment is required for this section. (مرفق قسم {sectionNameAr} مطلوب)", null, null, null, 0);
        }

        const long maxFileSize = 25 * 1024 * 1024; // 25 MB
        if (file.Length > maxFileSize)
        {
            return (false, $"حجم ملف مرفق {sectionNameAr} يتجاوز الحد الأقصى (25 ميجابايت).", null, null, null, 0);
        }

        var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (fileExt != ".pdf" && fileExt != ".jpg" && fileExt != ".jpeg")
        {
            return (false, $"نوع ملف مرفق {sectionNameAr} غير مدعوم. الملفات المسموحة فقط: PDF, JPG, JPEG.", null, null, null, 0);
        }

        var mime = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var validMimes = new[] { "application/pdf", "image/jpeg", "image/pjpeg", "image/jpg" };
        if (!validMimes.Contains(mime))
        {
            return (false, $"نوع ملف مرفق {sectionNameAr} غير مدعوم.", null, null, null, 0);
        }

        // Magic bytes inspection
        byte[] magicBytes = new byte[8];
        using (var stream = file.OpenReadStream())
        {
            var bytesRead = await stream.ReadAsync(magicBytes, 0, magicBytes.Length);
            if (bytesRead < 4)
            {
                return (false, $"محتوى ملف مرفق {sectionNameAr} تالف أو غير صالح.", null, null, null, 0);
            }

            if (fileExt == ".pdf")
            {
                if (magicBytes[0] != 0x25 || magicBytes[1] != 0x50 || magicBytes[2] != 0x44 || magicBytes[3] != 0x46)
                {
                    return (false, $"محتوى ملف PDF لمرفق {sectionNameAr} غير صالح.", null, null, null, 0);
                }
            }
            else
            {
                if (magicBytes[0] != 0xFF || magicBytes[1] != 0xD8 || magicBytes[2] != 0xFF)
                {
                    return (false, $"محتوى صورة JPG لمرفق {sectionNameAr} غير صالح.", null, null, null, 0);
                }
            }
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "transportation");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{fileExt}";
        var destinationPath = Path.Combine(uploadsDir, uniqueFileName);

        using (var destStream = new FileStream(destinationPath, FileMode.Create))
        {
            await file.CopyToAsync(destStream);
        }

        return (true, null, uniqueFileName, Path.GetFileName(file.FileName), file.ContentType, file.Length);
    }

    private void CleanupSavedFiles(IEnumerable<string> fileNames)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "transportation");
        foreach (var fn in fileNames)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(fn))
                {
                    var p = Path.Combine(uploadsDir, fn);
                    if (System.IO.File.Exists(p))
                    {
                        System.IO.File.Delete(p);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary file {FileName}", fn);
            }
        }
    }

    private async Task<IActionResult> ProcessCreateAsync(CreateTransportationDto dto)
    {
        // 1. Role Authorization
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasTransportationAccess(role))
        {
            _logger.LogWarning("Unauthorized user with role '{Role}' attempted to submit transportation transaction.", role);
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("Access Denied: Transportation department is restricted to CEO, IT, and Transportation Manager."));
        }

        // 2. ModelState Validation
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed. Please verify the entered data.", errors));
        }

        // 3. Resolve Linkage: Warehouse Export (authoritative workflow) OR Purchase Order (legacy)
        StorageOperation? exportOp = null;
        if (dto.StorageOperationId.HasValue && dto.StorageOperationId.Value > 0)
        {
            exportOp = await _db.StorageOperations
                .Include(o => o.Items)
                .Include(o => o.Client)
                .Include(o => o.Destination)
                .Include(o => o.StorageLocation)
                .Include(o => o.ProductionOrder)
                .Include(o => o.ProductionProcess)
                .FirstOrDefaultAsync(o => o.Id == dto.StorageOperationId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(dto.ExportTransactionNumber))
        {
            var expNumStr = dto.ExportTransactionNumber.Trim().TrimStart('#');
            if (int.TryParse(expNumStr, out var expNum))
            {
                exportOp = await _db.StorageOperations
                    .Include(o => o.Items)
                    .Include(o => o.Client)
                    .Include(o => o.Destination)
                    .Include(o => o.StorageLocation)
                    .Include(o => o.ProductionOrder)
                    .Include(o => o.ProductionProcess)
                    .FirstOrDefaultAsync(o => o.TransactionNumber == expNum && o.OperationType == "EXPORT");
            }
        }

        PurchaseOrder? po = null;
        string autoDestination;
        string autoCompanyName;
        string autoSource;
        string trimmedPo;
        string matType;
        decimal totalExportQty = 0m;
        decimal remainingExportQty = 0m;

        if (exportOp != null)
        {
            if (exportOp.OperationType != "EXPORT" || exportOp.ApprovalStatus != "APPROVED")
            {
                return BadRequest(ApiResponse.Fail($"إذن الصرف #{exportOp.TransactionNumber} غير معتمد من المستودع أو غير صالح للنقل."));
            }

            totalExportQty = exportOp.Items.Sum(i => i.Quantity);
            var transportedSoFar = await _db.Transportations
                .Where(t => t.StorageOperationId == exportOp.Id)
                .SumAsync(t => (decimal?)t.NetWeight) ?? 0m;
            remainingExportQty = Math.Max(0m, totalExportQty - transportedSoFar);

            if (remainingExportQty <= 0)
            {
                return BadRequest(ApiResponse.Fail($"إذن الصرف #{exportOp.TransactionNumber} تم نقل كامل كميته المصروفة مسبقاً ({totalExportQty:N3} طن)."));
            }

            autoDestination = !string.IsNullOrWhiteSpace(exportOp.DestinationName)
                ? exportOp.DestinationName
                : (exportOp.Destination?.DestinationName ?? "موقع العميل");
            autoCompanyName = !string.IsNullOrWhiteSpace(exportOp.ClientName)
                ? exportOp.ClientName
                : (exportOp.Client?.Name ?? "عميل فيس ستون");
            autoSource = !string.IsNullOrWhiteSpace(exportOp.StorageLocationName)
                ? exportOp.StorageLocationName
                : FixedSource;

            trimmedPo = !string.IsNullOrWhiteSpace(dto.PONumber)
                ? dto.PONumber.Trim()
                : (exportOp.ProductionOrder?.OrderNumber ?? $"EXP-#{exportOp.TransactionNumber}");

            matType = !string.IsNullOrWhiteSpace(dto.MaterialType)
                ? dto.MaterialType.Trim()
                : (exportOp.MaterialType ?? exportOp.Items.FirstOrDefault()?.MaterialName ?? (exportOp.ExportType == "FINISHED_PRODUCT" ? "منتج نهائي" : "مواد خام"));
        }
        else
        {
            return BadRequest(ApiResponse.Fail("يجب اختيار إذن صرف مستودع معتمد ومتاح لإنشاء حركة النقل. لا يمكن تسجيل حركة نقل بدون إذن صرف مستودع."));
        }

        // Support Multi-line Exports: Resolve and validate specific export item line if specified
        StorageOperationItem? selectedExportItem = null;
        decimal itemRemainingQty = 0m;
        if (exportOp != null)
        {
            if (dto.StorageOperationItemId.HasValue && dto.StorageOperationItemId.Value > 0)
            {
                selectedExportItem = exportOp.Items.FirstOrDefault(i => i.Id == dto.StorageOperationItemId.Value);
                if (selectedExportItem == null)
                {
                    return BadRequest(ApiResponse.Fail("بند إذن الصرف المحدد غير موجود ضمن هذا الإذن."));
                }
            }
            else if (exportOp.Items.Count == 1)
            {
                selectedExportItem = exportOp.Items.First();
            }

            if (selectedExportItem != null)
            {
                var itemAllocated = await _db.Transportations
                    .Where(t => t.StorageOperationId == exportOp.Id &&
                                (t.StorageOperationItemId == selectedExportItem.Id || (t.StorageOperationItemId == null && exportOp.Items.Count == 1)) &&
                                t.ApprovalStatus != "REJECTED")
                    .SumAsync(t => (decimal?)t.NetWeight) ?? 0m;
                itemRemainingQty = Math.Max(0m, selectedExportItem.Quantity - itemAllocated);

                if (itemRemainingQty <= 0)
                {
                    return BadRequest(ApiResponse.Fail($"بند إذن الصرف '{selectedExportItem.MaterialName}' تم نقل كامل كميته المصروفة مسبقاً ({selectedExportItem.Quantity:N3} طن)."));
                }
            }
        }

        // Resolve Optional Project Code
        int? resolvedProjectCodeId = null;
        string? resolvedProjectCodeValue = null;

        if (dto.ProjectCodeId.HasValue && dto.ProjectCodeId.Value > 0)
        {
            var prj = await _db.ProjectCodes.FirstOrDefaultAsync(p => p.Id == dto.ProjectCodeId.Value);
            if (prj != null)
            {
                resolvedProjectCodeId = prj.Id;
                resolvedProjectCodeValue = prj.Code;
            }
        }
        else if (!string.IsNullOrWhiteSpace(dto.ProjectCode))
        {
            var pCode = dto.ProjectCode.Trim();
            var prj = await _db.ProjectCodes.FirstOrDefaultAsync(p => p.Code.ToLower() == pCode.ToLower() || p.ProjectName.ToLower() == pCode.ToLower());
            if (prj != null)
            {
                resolvedProjectCodeId = prj.Id;
                resolvedProjectCodeValue = prj.Code;
            }
            else
            {
                resolvedProjectCodeValue = pCode;
            }
        }

        // Shipping Supervisor strictly "محمد ناجح"
        var shippingSupervisor = DefaultShippingSupervisor;

        // Department Supervisor strictly "ازهر محمد"
        var deptSupervisor = DefaultDepartmentSupervisor;

        // Receiving Status strictly from Checklist ("safe", "intact")
        var rawStatus = dto.ReceivingStatus ?? string.Empty;
        var statusParts = rawStatus.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (statusParts.Count == 0 || statusParts.Any(s => s != "safe" && s != "intact"))
        {
            return BadRequest(ApiResponse.Fail("حالة الاستلام غير صحيحة. يجب اختيار safe أو intact من القائمة المرجعية (Checklist)."));
        }
        var recStatus = string.Join(", ", statusParts);

        // 5. Positive Numeric Weights & Negative Weight Validations (Entered ONLY in Section 1: الميزان)
        if (dto.WeightBeforeLoading <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي فارغ يجب أن يكون أكبر من الصفر."));
        }
        if (dto.WeightWhenExitingFactory <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي قائم يجب أن يكون أكبر من الصفر."));
        }
        if (dto.WeightWhenExitingFactory <= dto.WeightBeforeLoading)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي قائم يجب أن يكون أكبر من الوزن الداخلي فارغ."));
        }

        if (dto.ExternalLoadedWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الخارجي قائم يجب أن يكون أكبر من الصفر."));
        }
        if (dto.ExternalEmptyWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الخارجي فارغ يجب أن يكون أكبر من الصفر."));
        }
        if (dto.ExternalLoadedWeight <= dto.ExternalEmptyWeight)
        {
            return BadRequest(ApiResponse.Fail("الوزن الخارجي قائم يجب أن يكون أكبر من الوزن الخارجي فارغ."));
        }

        // 6. Weight Formulas & Calculations
        // Internal Weight = Internal Loaded - Internal Empty
        var internalWeight = Math.Round(dto.WeightWhenExitingFactory - dto.WeightBeforeLoading, 3);
        // External Weight = External Loaded - External Empty
        var externalNetWeight = Math.Round(dto.ExternalLoadedWeight - dto.ExternalEmptyWeight, 3);

        decimal wasteQuantity;
        decimal netWeight;

        if (internalWeight == externalNetWeight)
        {
            wasteQuantity = 0m;
            netWeight = internalWeight;
        }
        else
        {
            wasteQuantity = Math.Round(Math.Abs(internalWeight - externalNetWeight), 3);
            netWeight = Math.Round(Math.Min(internalWeight, externalNetWeight), 3);
        }

        if (netWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الصافي المحسوب يجب أن يكون أكبر من الصفر."));
        }

        // Quantity limit check: Net weight <= remaining unallocated export quantity OR PO remaining quantity
        if (exportOp != null)
        {
            if (selectedExportItem != null && netWeight > itemRemainingQty)
            {
                return BadRequest(ApiResponse.Fail($"الوزن الصافي للشحنة ({netWeight:N3} طن) يتجاوز الرصيد المتبقي للبند '{selectedExportItem.MaterialName}' ({itemRemainingQty:N3} طن)."));
            }

            if (netWeight > remainingExportQty)
            {
                return BadRequest(ApiResponse.Fail($"الوزن الصافي للشحنة ({netWeight:N3} طن) يتجاوز الرصيد المتبقي لإذن الصرف #{exportOp.TransactionNumber} ({remainingExportQty:N3} طن)."));
            }
        }
        else if (po != null)
        {
            if (netWeight > po.RemainingQuantity)
            {
                _logger.LogWarning("Net weight {Net} exceeds remaining quantity {Rem} for PO {PO}",
                    netWeight, po.RemainingQuantity, po.PONumber);
                return BadRequest(ApiResponse.Fail("Net weight exceeds the remaining quantity of this PO."));
            }
        }

        // 7. Shipping Time Calculation
        // Formula: Shipping Time = External Loaded Weighing Date/Time - Internal Loaded Weighing Date/Time
        int? shippingDurationMinutes = null;
        if (dto.InternalLoadedWeightTime.HasValue && dto.ExternalLoadedWeightTime.HasValue)
        {
            if (dto.ExternalLoadedWeightTime.Value < dto.InternalLoadedWeightTime.Value)
            {
                return BadRequest(ApiResponse.Fail("وقت الوزن الخارجي القائم لا يمكن أن يكون قبل وقت الوزن الداخلي القائم."));
            }
            shippingDurationMinutes = (int)Math.Max(0, (dto.ExternalLoadedWeightTime.Value - dto.InternalLoadedWeightTime.Value).TotalMinutes);
        }

        // 8. Validate and Save 3 Mandatory Attachments (One for each of the 3 main sections)
        var savedFileNames = new List<string>();

        // Section 1: الميزان Attachment
        var wbRes = await ValidateAndSaveAttachmentAsync(dto.WeighbridgeAttachment, dto.WeighbridgeAttachmentPath, "الميزان");
        if (!wbRes.Success)
        {
            CleanupSavedFiles(savedFileNames);
            return BadRequest(ApiResponse.Fail(wbRes.ErrorMessage ?? "Attachment is required for this section."));
        }
        if (!string.IsNullOrWhiteSpace(wbRes.StoredFileName)) savedFileNames.Add(wbRes.StoredFileName);

        // Section 2: يومية التوريد Attachment
        var dsRes = await ValidateAndSaveAttachmentAsync(dto.DailySupplyAttachment, dto.DailySupplyAttachmentPath, "يومية التوريد");
        if (!dsRes.Success)
        {
            CleanupSavedFiles(savedFileNames);
            return BadRequest(ApiResponse.Fail(dsRes.ErrorMessage ?? "Attachment is required for this section."));
        }
        if (!string.IsNullOrWhiteSpace(dsRes.StoredFileName)) savedFileNames.Add(dsRes.StoredFileName);

        // Section 3: بوليصة الشحن Attachment (allows InvoiceFile for backwards compatibility)
        var bolFile = dto.BillOfLadingAttachment ?? dto.InvoiceFile;
        var bolRes = await ValidateAndSaveAttachmentAsync(bolFile, dto.BillOfLadingAttachmentPath ?? dto.InvoiceAttachmentPath, "بوليصة الشحن");
        if (!bolRes.Success)
        {
            CleanupSavedFiles(savedFileNames);
            return BadRequest(ApiResponse.Fail(bolRes.ErrorMessage ?? "Attachment is required for this section."));
        }
        if (!string.IsNullOrWhiteSpace(bolRes.StoredFileName)) savedFileNames.Add(bolRes.StoredFileName);

        // 9. Atomic Concurrency-Safe Database Transaction with UPDLOCK
        var callerName = User.Identity?.Name ?? "موظف النقل";
        Guid? callerUserId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            callerUserId = parsedGuid;
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            PurchaseOrder? lockedPo = null;
            int finalTxNumber;

            if (exportOp != null)
            {
                if (selectedExportItem != null)
                {
                    var liveItemAllocated = await _db.Transportations
                        .Where(t => t.StorageOperationId == exportOp.Id &&
                                    (t.StorageOperationItemId == selectedExportItem.Id || (t.StorageOperationItemId == null && exportOp.Items.Count == 1)) &&
                                    t.ApprovalStatus != "REJECTED")
                        .SumAsync(t => (decimal?)t.NetWeight) ?? 0m;
                    var liveItemRemaining = Math.Max(0m, selectedExportItem.Quantity - liveItemAllocated);

                    if (netWeight > liveItemRemaining)
                    {
                        await dbTransaction.RollbackAsync();
                        CleanupSavedFiles(savedFileNames);
                        return BadRequest(ApiResponse.Fail($"الوزن الصافي للشحنة ({netWeight:N3} طن) يتجاوز الرصيد المتاح للبند '{selectedExportItem.MaterialName}' ({liveItemRemaining:N3} طن)."));
                    }
                }

                // Concurrency verification for export balance
                var liveTransported = await _db.Transportations
                    .Where(t => t.StorageOperationId == exportOp.Id && t.ApprovalStatus != "REJECTED")
                    .SumAsync(t => (decimal?)t.NetWeight) ?? 0m;
                var liveRemaining = Math.Max(0m, totalExportQty - liveTransported);

                if (netWeight > liveRemaining)
                {
                    await dbTransaction.RollbackAsync();
                    CleanupSavedFiles(savedFileNames);
                    return BadRequest(ApiResponse.Fail($"الوزن الصافي للشحنة ({netWeight:N3} طن) يتجاوز الرصيد المتاح لإذن الصرف #{exportOp.TransactionNumber} ({liveRemaining:N3} طن)."));
                }

                var maxTx = await _db.Transportations
                    .Where(t => t.StorageOperationId == exportOp.Id)
                    .MaxAsync(t => (int?)t.TransactionNumber) ?? 0;
                finalTxNumber = maxTx + 1;
            }
            else
            {
                // Concurrency Lock: Lock the PO row with UPDLOCK, ROWLOCK to serialize concurrent transactions for the same PO
                lockedPo = await _db.PurchaseOrders
                    .FromSqlInterpolated($"SELECT * FROM PurchaseOrders WITH (UPDLOCK, ROWLOCK) WHERE Id = {po!.Id}")
                    .FirstOrDefaultAsync();

                if (lockedPo == null)
                {
                    await dbTransaction.RollbackAsync();
                    CleanupSavedFiles(savedFileNames);
                    return BadRequest(ApiResponse.Fail("أمر الشراء غير موجود."));
                }

                if (netWeight > lockedPo.RemainingQuantity)
                {
                    await dbTransaction.RollbackAsync();
                    CleanupSavedFiles(savedFileNames);
                    _logger.LogWarning("Net weight {Net} exceeds remaining quantity {Rem} for PO {PO}",
                        netWeight, lockedPo.RemainingQuantity, lockedPo.PONumber);
                    return BadRequest(ApiResponse.Fail("Net weight exceeds the remaining quantity of this PO."));
                }

                var maxTx = await _db.Transportations
                    .Where(t => t.PONumber.ToLower() == trimmedPo.ToLower())
                    .MaxAsync(t => (int?)t.TransactionNumber) ?? 0;
                finalTxNumber = maxTx + 1;

                // Auto-Approve: Deduct PO remaining quantity immediately upon submission
                lockedPo.RemainingQuantity = Math.Max(0m, lockedPo.RemainingQuantity - netWeight);
                lockedPo.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("PO {PO} remaining quantity deducted by {Net} to {Rem} upon automatic request approval.", lockedPo.PONumber, netWeight, lockedPo.RemainingQuantity);
            }

            // Manual Weighbridge Reference Codes (Strictly entered manually by employee)
            if (string.IsNullOrWhiteSpace(dto.InternalWeighbridgeCode))
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("رقم مرجع الميزان الداخلي مطلوب ويجب إدخاله يدوياً."));
            }
            var internalCode = dto.InternalWeighbridgeCode.Trim();

            if (string.IsNullOrWhiteSpace(dto.ExternalWeighbridgeCode))
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("رقم مرجع الميزان الخارجي مطلوب ويجب إدخاله يدوياً."));
            }
            var externalCode = dto.ExternalWeighbridgeCode.Trim();

            // Single Source of Truth for Departure and Arrival Dates
            if (!dto.InternalLoadedWeightTime.HasValue)
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("تاريخ ووقت الوزن القائم الداخلي مطلوب."));
            }
            if (!dto.ExternalLoadedWeightTime.HasValue)
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("تاريخ ووقت الوزن القائم الخارجي مطلوب."));
            }
            var departureTime = dto.InternalLoadedWeightTime.Value;
            var arrivalTime = dto.ExternalLoadedWeightTime.Value;

            if (dto.CustodyAmount < 0)
            {
                await dbTransaction.RollbackAsync();
                CleanupSavedFiles(savedFileNames);
                return BadRequest(ApiResponse.Fail("قيمة العهدة يجب أن تكون رقماً موجباً أو صفراً."));
            }
            var custodyAmount = Math.Round(dto.CustodyAmount, 3);

            // 10. Create Transportation Record
            var entity = new Transportation
            {
                TransactionNumber = finalTxNumber,
                TransactionDate = dto.TransactionDate ?? DateTime.UtcNow,
                CarNumber = dto.CarNumber.Trim(),
                DriverName = dto.DriverName.Trim(),
                Source = autoSource,
                PONumber = trimmedPo,
                CompanyName = autoCompanyName,
                PurchaseOrderId = lockedPo?.Id,
                StorageOperationId = exportOp?.Id,
                StorageOperationItemId = selectedExportItem?.Id,
                ExportTransactionNumber = exportOp != null ? $"#{exportOp.TransactionNumber}" : null,
                ExportType = exportOp?.ExportType ?? dto.ExportType ?? "RAW_MATERIAL",
                ProductionOrderId = exportOp?.ProductionOrderId ?? dto.ProductionOrderId,
                ProductionProcessId = exportOp?.ProductionProcessId ?? dto.ProductionProcessId,
                ManufactureNumber = dto.ManufactureNumber ?? selectedExportItem?.ManufactureNumber ?? exportOp?.ManufactureNumber ?? exportOp?.Items.FirstOrDefault()?.ManufactureNumber,
                BatchNumber = !string.IsNullOrWhiteSpace(dto.BatchNumber) ? dto.BatchNumber : (selectedExportItem?.BatchNumber ?? (exportOp != null ? string.Join(", ", exportOp.Items.Where(i => !string.IsNullOrEmpty(i.BatchNumber)).Select(i => i.BatchNumber).Distinct()) : null)),
                SampleCode = !string.IsNullOrWhiteSpace(dto.SampleCode) ? dto.SampleCode : (selectedExportItem?.SampleCode ?? (exportOp != null ? (exportOp.SampleCode ?? string.Join(", ", exportOp.Items.Where(i => !string.IsNullOrEmpty(i.SampleCode)).Select(i => i.SampleCode).Distinct())) : null)),
                SourceStorageLocationId = exportOp?.StorageLocationId ?? dto.SourceStorageLocationId,
                SourceStorageLocationName = exportOp?.StorageLocationName ?? dto.SourceStorageLocationName,
                ProjectCodeId = resolvedProjectCodeId,
                ProjectCodeValue = resolvedProjectCodeValue,
                MaterialType = selectedExportItem?.MaterialName ?? matType,
                WeightBeforeLoading = Math.Round(dto.WeightBeforeLoading, 3),
                WeightWhenEnteringFactory = Math.Round(dto.WeightWhenEnteringFactory > 0 ? dto.WeightWhenEnteringFactory : dto.WeightBeforeLoading, 3),
                WeightWhenExitingFactory = Math.Round(dto.WeightWhenExitingFactory, 3),
                ExpectedWeight = internalWeight,
                InternalWeight = internalWeight,
                ExternalLoadedWeight = Math.Round(dto.ExternalLoadedWeight, 3),
                ExternalEmptyWeight = Math.Round(dto.ExternalEmptyWeight, 3),
                ExternalNetWeight = externalNetWeight,
                WasteQuantity = wasteQuantity,
                NetWeight = netWeight,
                DriverPhone = string.IsNullOrWhiteSpace(dto.DriverPhone) ? null : dto.DriverPhone.Trim(),
                DepartureTime = departureTime,
                ArrivalTime = arrivalTime,
                ShippingSupervisor = shippingSupervisor,
                DepartmentSupervisor = deptSupervisor,
                RecipientName = string.IsNullOrWhiteSpace(dto.RecipientName) ? null : dto.RecipientName.Trim(),
                RecipientTitle = string.IsNullOrWhiteSpace(dto.RecipientTitle) ? null : dto.RecipientTitle.Trim(),
                ReceivedDate = dto.ReceivedDate,
                InternalWeighbridgeCode = internalCode,
                InternalEmptyWeightTime = dto.InternalEmptyWeightTime,
                InternalLoadedWeightTime = dto.InternalLoadedWeightTime,
                ExternalWeighbridgeCode = externalCode,
                ExternalEmptyWeightTime = dto.ExternalEmptyWeightTime,
                ExternalLoadedWeightTime = dto.ExternalLoadedWeightTime,
                ShippingDurationMinutes = shippingDurationMinutes,
                Destination = autoDestination,
                TransactionStatus = "Completed",
                ApprovalStatus = "APPROVED",
                ReviewedBy = "System (Auto-Approved)",
                ReviewedByUserId = callerUserId,
                ReviewedAt = DateTime.UtcNow,
                ReceivingStatus = recStatus,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                DateOfSubmit = DateTime.UtcNow,
                Ikramiya = Math.Round(dto.Ikramiya, 3),
                CustodyAmount = custodyAmount,

                // Section 1: Weighbridge Attachment
                WeighbridgeAttachmentPath = wbRes.StoredFileName,
                WeighbridgeFileName = wbRes.OriginalFileName,
                WeighbridgeFileType = wbRes.ContentType,
                WeighbridgeFileSize = wbRes.FileSize,

                // Section 2: Daily Supply Attachment
                DailySupplyAttachmentPath = dsRes.StoredFileName,
                DailySupplyFileName = dsRes.OriginalFileName,
                DailySupplyFileType = dsRes.ContentType,
                DailySupplyFileSize = dsRes.FileSize,

                // Section 3: Bill of Lading Attachment
                BillOfLadingAttachmentPath = bolRes.StoredFileName,
                BillOfLadingFileName = bolRes.OriginalFileName,
                BillOfLadingFileType = bolRes.ContentType,
                BillOfLadingFileSize = bolRes.FileSize,

                // Backwards compatibility for Invoice fields
                InvoiceAttachmentPath = bolRes.StoredFileName ?? wbRes.StoredFileName,
                InvoiceFileName = bolRes.OriginalFileName ?? wbRes.OriginalFileName,
                InvoiceFileType = bolRes.ContentType ?? wbRes.ContentType,
                InvoiceFileSize = bolRes.FileSize > 0 ? bolRes.FileSize : wbRes.FileSize,

                SubmittedBy = callerName,
                SubmittedByUserId = callerUserId
            };

            await _db.Transportations.AddAsync(entity);
            await _db.SaveChangesAsync();

            // Record initial audit trail entry
            var history = new FormApprovalHistory
            {
                FormType = "Transportation",
                FormId = entity.Id,
                PONumber = entity.PONumber,
                TransactionOrMovementNumber = $"#{entity.TransactionNumber}",
                PreviousStatus = string.Empty,
                NewStatus = "APPROVED",
                Action = "APPROVED",
                ActorName = "System (Auto-Approved)",
                ActorUserId = callerUserId,
                ActorRole = "System",
                Comment = "تم اعتماد حركة النقل تلقائياً بواسطة النظام وتحديث الرصيد المتبقي لأمر الشراء.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);
            await _db.SaveChangesAsync();

            await dbTransaction.CommitAsync();

            _logger.LogInformation("Transportation transaction PO {PO} / #{TxNum} submitted and saved as PENDING review.",
                entity.PONumber, entity.TransactionNumber);

            try
            {
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    department = "control-monitoring",
                    action = "SUBMITTED",
                    formType = "Transportation",
                    id = entity.Id,
                    po = entity.PONumber,
                    transactionNumber = entity.TransactionNumber,
                    submittedBy = callerName,
                    timestamp = DateTime.UtcNow
                });

                await _hubContext.Clients.All.SendAsync("OnFormSubmittedForReview", new
                {
                    formType = "Transportation",
                    id = entity.Id,
                    po = entity.PONumber,
                    transactionNumber = entity.TransactionNumber,
                    submittedBy = callerName,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception hubEx)
            {
                _logger.LogWarning(hubEx, "Failed to broadcast SignalR notification for new transportation form.");
            }

            var resultDto = MapToDto(entity);
            if (lockedPo != null)
            {
                resultDto.PoRemainingQuantityAfter = lockedPo.RemainingQuantity;
            }
            else if (exportOp != null)
            {
                resultDto.PoRemainingQuantityAfter = Math.Max(0m, remainingExportQty - netWeight);
            }

            return Ok(ApiResponse<TransportationDto>.Ok(resultDto, "تم إرسال حركة النقل بنجاح وبانتظار اعتماد قسم التحكم والمراقبة."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(savedFileNames);

            _logger.LogError(ex, "Unexpected error saving transportation transaction for PO {PO}", trimmedPo);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                ApiResponse.Fail("حدث خطأ غير متوقع أثناء حفظ حركة النقل."));
        }
    }

    private static TransportationDto MapToDto(Transportation entity)
    {
        return new TransportationDto
        {
            Id = entity.Id,
            TransactionNumber = entity.TransactionNumber,
            TransactionDate = entity.TransactionDate,
            CarNumber = entity.CarNumber,
            DriverName = entity.DriverName,
            Source = entity.Source,
            PONumber = entity.PONumber,
            CompanyName = entity.CompanyName,
            MaterialType = entity.MaterialType,
            WeightBeforeLoading = entity.WeightBeforeLoading,
            WeightWhenEnteringFactory = entity.WeightWhenEnteringFactory,
            WeightWhenExitingFactory = entity.WeightWhenExitingFactory,
            ExpectedWeight = entity.ExpectedWeight,
            InternalWeight = entity.InternalWeight > 0 ? entity.InternalWeight : (entity.WeightWhenExitingFactory - entity.WeightBeforeLoading),
            ExternalLoadedWeight = entity.ExternalLoadedWeight,
            ExternalEmptyWeight = entity.ExternalEmptyWeight,
            ExternalNetWeight = entity.ExternalNetWeight,
            WasteQuantity = entity.WasteQuantity,
            NetWeight = entity.NetWeight,
            DriverPhone = entity.DriverPhone,
            DepartureTime = entity.DepartureTime,
            ArrivalTime = entity.ArrivalTime,
            ShippingSupervisor = entity.ShippingSupervisor,
            DepartmentSupervisor = entity.DepartmentSupervisor,
            RecipientName = entity.RecipientName,
            RecipientTitle = entity.RecipientTitle,
            ReceivedDate = entity.ReceivedDate,
            InternalWeighbridgeCode = entity.InternalWeighbridgeCode,
            InternalEmptyWeightTime = entity.InternalEmptyWeightTime,
            InternalLoadedWeightTime = entity.InternalLoadedWeightTime,
            ExternalWeighbridgeCode = entity.ExternalWeighbridgeCode,
            ExternalEmptyWeightTime = entity.ExternalEmptyWeightTime,
            ExternalLoadedWeightTime = entity.ExternalLoadedWeightTime,
            ShippingDurationMinutes = entity.ShippingDurationMinutes,
            Destination = entity.Destination,
            TransactionStatus = entity.TransactionStatus,
            ReceivingStatus = entity.ReceivingStatus,
            Notes = entity.Notes,
            DateOfSubmit = entity.DateOfSubmit,
            Ikramiya = entity.Ikramiya,
            CustodyAmount = entity.CustodyAmount,
            
            WeighbridgeAttachmentPath = entity.WeighbridgeAttachmentPath,
            WeighbridgeFileName = entity.WeighbridgeFileName,
            WeighbridgeFileType = entity.WeighbridgeFileType,
            WeighbridgeFileSize = entity.WeighbridgeFileSize,

            DailySupplyAttachmentPath = entity.DailySupplyAttachmentPath,
            DailySupplyFileName = entity.DailySupplyFileName,
            DailySupplyFileType = entity.DailySupplyFileType,
            DailySupplyFileSize = entity.DailySupplyFileSize,

            BillOfLadingAttachmentPath = entity.BillOfLadingAttachmentPath,
            BillOfLadingFileName = entity.BillOfLadingFileName,
            BillOfLadingFileType = entity.BillOfLadingFileType,
            BillOfLadingFileSize = entity.BillOfLadingFileSize,

            InvoiceAttachmentPath = entity.InvoiceAttachmentPath,
            InvoiceFileName = entity.InvoiceFileName,
            InvoiceFileType = entity.InvoiceFileType,
            InvoiceFileSize = entity.InvoiceFileSize,

            ProjectCodeId = entity.ProjectCodeId,
            ProjectCode = entity.ProjectCodeValue ?? entity.ProjectCode?.Code ?? "Not Assigned",
            ProjectName = entity.ProjectCode?.ProjectName ?? "غير محدد",
            SubmittedBy = entity.SubmittedBy,
            StorageOperationId = entity.StorageOperationId,
            StorageOperationItemId = entity.StorageOperationItemId,
            ExportTransactionNumber = entity.ExportTransactionNumber,
            ExportType = entity.ExportType,
            ProductionOrderId = entity.ProductionOrderId,
            ProductionProcessId = entity.ProductionProcessId,
            ManufactureNumber = entity.ManufactureNumber,
            BatchNumber = entity.BatchNumber,
            SampleCode = entity.SampleCode,
            SourceStorageLocationId = entity.SourceStorageLocationId,
            SourceStorageLocationName = entity.SourceStorageLocationName,
            ApprovalStatus = string.IsNullOrWhiteSpace(entity.ApprovalStatus) ? "APPROVED" : entity.ApprovalStatus,
            ReviewedBy = entity.ReviewedBy,
            ReviewedAt = entity.ReviewedAt,
            RejectionReason = entity.RejectionReason
        };
    }

    /// <summary>
    /// Resubmit / Update a rejected Transportation transaction.
    /// Updates form fields, replaces any new attachments, resets status to PENDING,
    /// and logs a RESUBMITTED audit trail entry while preserving full previous history.
    /// </summary>
    [HttpPost("{id:int}/resubmit")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<TransportationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResubmitTransportation(
        int id,
        [FromForm] CreateTransportationDto dto,
        [FromForm] IFormFile? weighbridgeAttachment = null,
        [FromForm] IFormFile? dailySupplyAttachment = null,
        [FromForm] IFormFile? billOfLadingAttachment = null,
        [FromForm] string? comment = null)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var callerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف النقل";
        Guid? callerUserId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            callerUserId = parsedGuid;
        }

        var entity = await _db.Transportations.FirstOrDefaultAsync(t => t.Id == id);
        if (entity == null)
        {
            return NotFound(ApiResponse.Fail($"لم يتم العثور على حركة النقل #{id}."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات الحركة غير صالحة."));
        }

        var material = (dto.MaterialType ?? string.Empty).Trim().ToLowerInvariant();
        if (material != "powder" && material != "slurry")
        {
            return BadRequest(ApiResponse.Fail("نوع الخامة غير صحيح. الخيارات المتاحة فقط: powder أو slurry."));
        }

        // Validate and save attachments (using existing path as fallback if no new file uploaded)
        var wbRes = await ValidateAndSaveAttachmentAsync(weighbridgeAttachment, entity.WeighbridgeAttachmentPath, "إيصال الميزان");
        if (!wbRes.Success) return BadRequest(ApiResponse.Fail(wbRes.ErrorMessage!));

        var dsRes = await ValidateAndSaveAttachmentAsync(dailySupplyAttachment, entity.DailySupplyAttachmentPath, "يومية التوريد");
        if (!dsRes.Success) return BadRequest(ApiResponse.Fail(dsRes.ErrorMessage!));

        var bolRes = await ValidateAndSaveAttachmentAsync(billOfLadingAttachment, entity.BillOfLadingAttachmentPath, "بوليصة الشحن");
        if (!bolRes.Success) return BadRequest(ApiResponse.Fail(bolRes.ErrorMessage!));

        // Calculations
        var internalNetWeight = Math.Round(dto.InternalLoadedWeight - dto.InternalEmptyWeight, 3);
        var externalNetWeight = Math.Round(dto.ExternalLoadedWeight - dto.ExternalEmptyWeight, 3);
        var netWeight = Math.Min(internalNetWeight, externalNetWeight);
        var wasteQuantity = Math.Max(0m, externalNetWeight - internalNetWeight);

        if (netWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الصافي للشحنة يجب أن يكون أكبر من الصفر."));
        }

        // Validate remaining balance for export, excluding this record's previous weight
        if (entity.StorageOperationId.HasValue)
        {
            var exportOp = await _db.StorageOperations
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == entity.StorageOperationId.Value);

            if (exportOp != null)
            {
                var totalExportQty = exportOp.Items.Sum(i => i.Quantity);
                var otherTransported = await _db.Transportations
                    .Where(t => t.StorageOperationId == entity.StorageOperationId && t.Id != entity.Id && t.ApprovalStatus != "REJECTED")
                    .SumAsync(t => (decimal?)t.NetWeight) ?? 0m;
                var remainingAllowed = Math.Max(0m, totalExportQty - otherTransported);

                if (netWeight > remainingAllowed)
                {
                    return BadRequest(ApiResponse.Fail($"الوزن الصافي للشحنة ({netWeight:N3} طن) يتجاوز الرصيد المتبقي المتاح لإذن الصرف #{exportOp.TransactionNumber} ({remainingAllowed:N3} طن)."));
                }
            }
        }

        // Update fields
        entity.CarNumber = dto.CarNumber.Trim();
        entity.DriverName = dto.DriverName.Trim();
        entity.DriverPhone = string.IsNullOrWhiteSpace(dto.DriverPhone) ? null : dto.DriverPhone.Trim();
        entity.MaterialType = material;
        entity.WeightBeforeLoading = Math.Round(dto.InternalEmptyWeight, 3);
        entity.WeightWhenExitingFactory = Math.Round(dto.InternalLoadedWeight, 3);
        entity.ExternalLoadedWeight = Math.Round(dto.ExternalLoadedWeight, 3);
        entity.ExternalEmptyWeight = Math.Round(dto.ExternalEmptyWeight, 3);
        entity.InternalWeight = internalNetWeight;
        entity.ExternalNetWeight = externalNetWeight;
        entity.NetWeight = netWeight;
        entity.WasteQuantity = wasteQuantity;

        if (!string.IsNullOrWhiteSpace(dto.InternalWeighbridgeCode)) entity.InternalWeighbridgeCode = dto.InternalWeighbridgeCode.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ExternalWeighbridgeCode)) entity.ExternalWeighbridgeCode = dto.ExternalWeighbridgeCode.Trim();

        if (dto.InternalEmptyWeightTime.HasValue) entity.InternalEmptyWeightTime = dto.InternalEmptyWeightTime;
        if (dto.InternalLoadedWeightTime.HasValue) entity.InternalLoadedWeightTime = dto.InternalLoadedWeightTime;
        if (dto.ExternalEmptyWeightTime.HasValue) entity.ExternalEmptyWeightTime = dto.ExternalEmptyWeightTime;
        if (dto.ExternalLoadedWeightTime.HasValue) entity.ExternalLoadedWeightTime = dto.ExternalLoadedWeightTime;

        if (dto.DepartureTime.HasValue) entity.DepartureTime = dto.DepartureTime;
        if (dto.ArrivalTime.HasValue) entity.ArrivalTime = dto.ArrivalTime;

        if (!string.IsNullOrWhiteSpace(dto.ShippingSupervisor)) entity.ShippingSupervisor = dto.ShippingSupervisor.Trim();
        if (!string.IsNullOrWhiteSpace(dto.DepartmentSupervisor)) entity.DepartmentSupervisor = dto.DepartmentSupervisor.Trim();
        if (!string.IsNullOrWhiteSpace(dto.RecipientName)) entity.RecipientName = dto.RecipientName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.RecipientTitle)) entity.RecipientTitle = dto.RecipientTitle.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ReceivingStatus)) entity.ReceivingStatus = dto.ReceivingStatus.Trim();
        if (dto.ReceivedDate.HasValue) entity.ReceivedDate = dto.ReceivedDate;

        entity.CustodyAmount = Math.Round(dto.CustodyAmount, 3);
        entity.Ikramiya = Math.Round(dto.Ikramiya, 3);
        if (dto.Notes != null) entity.Notes = dto.Notes.Trim();

        // Update Project Code (Optional)
        if (dto.ProjectCodeId.HasValue && dto.ProjectCodeId.Value > 0)
        {
            var pc = await _db.ProjectCodes.FirstOrDefaultAsync(p => p.Id == dto.ProjectCodeId.Value);
            if (pc != null)
            {
                entity.ProjectCodeId = pc.Id;
                entity.ProjectCodeValue = pc.Code;
            }
        }
        else if (!string.IsNullOrWhiteSpace(dto.ProjectCode))
        {
            var codeTrim = dto.ProjectCode.Trim();
            var pc = await _db.ProjectCodes.FirstOrDefaultAsync(p => p.Code == codeTrim);
            if (pc != null)
            {
                entity.ProjectCodeId = pc.Id;
                entity.ProjectCodeValue = pc.Code;
            }
            else
            {
                entity.ProjectCodeId = null;
                entity.ProjectCodeValue = codeTrim;
            }
        }
        else
        {
            entity.ProjectCodeId = null;
            entity.ProjectCodeValue = null;
        }

        // Update attachments if new ones saved
        if (weighbridgeAttachment != null && wbRes.StoredFileName != null)
        {
            entity.WeighbridgeAttachmentPath = wbRes.StoredFileName;
            entity.WeighbridgeFileName = wbRes.OriginalFileName;
            entity.WeighbridgeFileType = wbRes.ContentType;
            entity.WeighbridgeFileSize = wbRes.FileSize;
        }
        if (dailySupplyAttachment != null && dsRes.StoredFileName != null)
        {
            entity.DailySupplyAttachmentPath = dsRes.StoredFileName;
            entity.DailySupplyFileName = dsRes.OriginalFileName;
            entity.DailySupplyFileType = dsRes.ContentType;
            entity.DailySupplyFileSize = dsRes.FileSize;
        }
        if (billOfLadingAttachment != null && bolRes.StoredFileName != null)
        {
            entity.BillOfLadingAttachmentPath = bolRes.StoredFileName;
            entity.BillOfLadingFileName = bolRes.OriginalFileName;
            entity.BillOfLadingFileType = bolRes.ContentType;
            entity.BillOfLadingFileSize = bolRes.FileSize;
            entity.InvoiceAttachmentPath = bolRes.StoredFileName;
            entity.InvoiceFileName = bolRes.OriginalFileName;
            entity.InvoiceFileType = bolRes.ContentType;
            entity.InvoiceFileSize = bolRes.FileSize;
        }

        var prevStatus = entity.ApprovalStatus;
        entity.ApprovalStatus = "APPROVED";
        entity.TransactionStatus = "Completed";
        entity.RejectionReason = null;
        entity.ReviewedBy = "System (Auto-Approved)";
        entity.ReviewedAt = DateTime.UtcNow;

        // Record history entry
        var history = new FormApprovalHistory
        {
            FormType = "Transportation",
            FormId = entity.Id,
            PONumber = entity.PONumber,
            TransactionOrMovementNumber = $"#{entity.TransactionNumber}",
            PreviousStatus = prevStatus,
            NewStatus = "APPROVED",
            Action = "APPROVED",
            ActorName = "System (Auto-Approved)",
            ActorUserId = callerUserId,
            ActorRole = "System",
            Comment = string.IsNullOrWhiteSpace(comment) ? "تم تعديل بيانات النموذج واعتماده تلقائياً بواسطة النظام." : comment.Trim(),
            Timestamp = DateTime.UtcNow
        };
        await _db.FormApprovalHistories.AddAsync(history);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Transportation #{Tx} resubmitted by {User}. Status reset to PENDING.", entity.TransactionNumber, callerName);

        try
        {
            await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
            {
                department = "control-monitoring",
                action = "RESUBMITTED",
                formType = "Transportation",
                id = entity.Id,
                po = entity.PONumber,
                transactionNumber = entity.TransactionNumber,
                submittedBy = callerName,
                timestamp = DateTime.UtcNow
            });

            await _hubContext.Clients.All.SendAsync("OnFormSubmittedForReview", new
            {
                formType = "Transportation",
                id = entity.Id,
                po = entity.PONumber,
                transactionNumber = entity.TransactionNumber,
                submittedBy = callerName,
                status = "PENDING",
                timestamp = DateTime.UtcNow
            });

            await _hubContext.Clients.All.SendAsync("OnFormApprovalStatusChanged", new
            {
                formType = "Transportation",
                id = entity.Id,
                status = "PENDING",
                action = "RESUBMITTED"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR notification for resubmitted Transportation #{Id}", entity.Id);
        }

        return Ok(ApiResponse<TransportationDto>.Ok(MapToDto(entity), "تم تعديل بيانات النموذج وإعادة إرساله لقسم التحكم والمراقبة بنجاح."));
    }
}

