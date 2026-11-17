using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FaceStone.Web.Data;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;
using FaceStone.Web.Services;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "StorageAccess")]
[Produces("application/json")]
public class StorageController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<StorageController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<FaceStone.Web.Hubs.ChatHub> _hubContext;
    private readonly IWarehouseInventoryService _inventoryService;

    public StorageController(
        FaceStoneDbContext db, 
        ILogger<StorageController> logger,
        IWebHostEnvironment env,
        Microsoft.AspNetCore.SignalR.IHubContext<FaceStone.Web.Hubs.ChatHub> hubContext,
        IWarehouseInventoryService inventoryService)
    {
        _db = db;
        _logger = logger;
        _env = env;
        _hubContext = hubContext;
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Supported Materials matching FaceStone project standard: powder and slurry
    /// </summary>
    public static readonly List<string> MaterialTypes = new()
    {
        "powder",
        "slurry"
    };

    /// <summary>
    /// Supported Actions ONLY: import (إيداع) and export (صرف)
    /// </summary>
    public static readonly List<string> ActionTypes = new()
    {
        "import",
        "export"
    };

    /// <summary>
    /// Retrieve configuration options: Material list, Action list, eligible Employees, next movement number.
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(ApiResponse<StorageConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig()
    {
        // 1. Fetch eligible employees from Users table
        var employees = await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == AppRoles.StorageManager
                     || u.Role == AppRoles.StorageManagerCode
                     || u.Role == AppRoles.StorageManagerDisplay
                     || u.Role == AppRoles.FactoryManager
                     || u.Role == AppRoles.FactoryManagerDisplay
                     || u.Role == AppRoles.CEO
                     || u.Role == AppRoles.IT)
            .OrderBy(u => u.FullName)
            .Select(u => new EmployeeSummaryDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Role = u.Role
            })
            .ToListAsync();

        // Fallback: If no users with storage roles are found, provide active users
        if (employees.Count == 0)
        {
            employees = await _db.Users
                .AsNoTracking()
                .OrderBy(u => u.FullName)
                .Select(u => new EmployeeSummaryDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Username = u.Username,
                    Role = u.Role
                })
                .ToListAsync();
        }

        var maxMovement = await _db.StorageTransactions.MaxAsync(s => (int?)s.MovementNumber) ?? 5000;
        var nextMovement = maxMovement + 1;

        var config = new StorageConfigDto
        {
            Materials = MaterialTypes,
            Actions = ActionTypes,
            Employees = employees,
            NextMovementNumber = nextMovement
        };

        return Ok(ApiResponse<StorageConfigDto>.Ok(config));
    }

    /// <summary>
    /// Validates a Purchase Order for Storage Export workflow.
    /// </summary>
    [HttpGet("po/{poNumber}")]
    [HttpGet("po-lookup/{poNumber}")]
    [ProducesResponseType(typeof(ApiResponse<StoragePoLookupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LookupPurchaseOrder(string poNumber)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب."));
        }

        var trimmed = poNumber.Trim();
        var po = await _db.PurchaseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PONumber.ToLower() == trimmed.ToLower());

        if (po == null)
        {
            _logger.LogWarning("PO lookup failed for Storage: '{PONumber}' not found.", trimmed);
            return NotFound(ApiResponse.Fail("Purchase Order not found."));
        }

        var dto = new StoragePoLookupDto
        {
            Id = po.Id,
            PoNumber = po.PONumber,
            Supplier = po.Supplier,
            Quantity = po.Quantity,
            RemainingQuantity = po.RemainingQuantity,
            OrderDate = po.OrderDate,
            Notes = po.Notes
        };

        return Ok(ApiResponse<StoragePoLookupDto>.Ok(dto));
    }

    /// <summary>
    /// Check if internal weighbridge reference already exists.
    /// </summary>
    [HttpGet("check-reference/{referenceNumber}")]
    [ProducesResponseType(typeof(ApiResponse<CheckReferenceResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckReferenceNumber(string referenceNumber)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            return Ok(ApiResponse<CheckReferenceResultDto>.Ok(new CheckReferenceResultDto { ReferenceNumber = "", Exists = false }));
        }

        var trimmed = referenceNumber.Trim();
        var exists = await _db.StorageTransactions
            .AsNoTracking()
            .AnyAsync(s => s.InternalWeighbridgeReference.ToLower() == trimmed.ToLower());

        // Also check legacy ReferenceNumber if integer
        if (!exists && int.TryParse(trimmed, out var intRef))
        {
            exists = await _db.StorageTransactions
                .AsNoTracking()
                .AnyAsync(s => s.ReferenceNumber == intRef);
        }

        var result = new CheckReferenceResultDto
        {
            ReferenceNumber = trimmed,
            Exists = exists,
            Message = exists ? "رقم المرجع مستخدم بالفعل" : null
        };

        return Ok(ApiResponse<CheckReferenceResultDto>.Ok(result));
    }

    /// <summary>
    /// Retrieve recent storage transactions.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ApiResponse<List<StorageTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentTransactions([FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);

        var transactions = await _db.StorageTransactions
            .AsNoTracking()
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync();

        var dtos = transactions.Select(MapToDto).ToList();
        return Ok(ApiResponse<List<StorageTransactionDto>>.Ok(dtos));
    }

    /// <summary>
    /// Retrieve single storage transaction by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<StorageTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var transaction = await _db.StorageTransactions.FindAsync(id);
        if (transaction == null)
        {
            return NotFound(ApiResponse.Fail($"حركة المخزن #{id} غير موجودة."));
        }

        return Ok(ApiResponse<StorageTransactionDto>.Ok(MapToDto(transaction)));
    }

    /// <summary>
    /// Stream / Download weighbridge receipt attachment.
    /// </summary>
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
        var filePath = Path.Combine(webRoot, "uploads", "storage", fileName);
        string? originalName = null;

        if (!System.IO.File.Exists(filePath))
        {
            // Reverse lookup in StorageOperations
            var op = await _db.StorageOperations
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.WeighbridgeFileName == fileName ||
                    x.QocFileName == fileName ||
                    x.DocumentFileName == fileName ||
                    (x.WeighbridgeAttachmentPath != null && x.WeighbridgeAttachmentPath.Contains(fileName)) ||
                    (x.QocAttachmentPath != null && x.QocAttachmentPath.Contains(fileName)) ||
                    (x.DocumentAttachmentPath != null && x.DocumentAttachmentPath.Contains(fileName)));

            if (op != null)
            {
                var candidates = new[]
                {
                    (op.WeighbridgeAttachmentPath, op.WeighbridgeFileName),
                    (op.QocAttachmentPath, op.QocFileName),
                    (op.DocumentAttachmentPath, op.DocumentFileName)
                };
                foreach (var (p, orig) in candidates)
                {
                    if (string.IsNullOrWhiteSpace(p) && string.IsNullOrWhiteSpace(orig)) continue;
                    var storedFn = !string.IsNullOrWhiteSpace(p) ? Path.GetFileName(p) : orig!;
                    var diskP = Path.Combine(webRoot, "uploads", "storage", storedFn);
                    if (System.IO.File.Exists(diskP))
                    {
                        filePath = diskP;
                        originalName = orig;
                        break;
                    }
                }
            }

            // Fallback 1: check legacy StorageTransactions
            if (!System.IO.File.Exists(filePath))
            {
                var matchTx = await _db.StorageTransactions
                    .AsNoTracking()
                    .Where(s => (s.WeighbridgeFileName == fileName || (s.WeighbridgeAttachmentPath != null && s.WeighbridgeAttachmentPath.Contains(fileName))))
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                if (matchTx != null && !string.IsNullOrWhiteSpace(matchTx.WeighbridgeAttachmentPath))
                {
                    var storedName = Path.GetFileName(matchTx.WeighbridgeAttachmentPath);
                    var matchedPath = Path.Combine(webRoot, "uploads", "storage", storedName);
                    if (System.IO.File.Exists(matchedPath))
                    {
                        filePath = matchedPath;
                        originalName = matchTx.WeighbridgeFileName;
                    }
                }
            }

            // Fallback 2: check transportation uploads
            if (!System.IO.File.Exists(filePath))
            {
                var transFilePath = Path.Combine(webRoot, "uploads", "transportation", fileName);
                if (System.IO.File.Exists(transFilePath))
                {
                    filePath = transFilePath;
                }
            }
        }

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(ApiResponse.Fail("الملف غير موجود."));
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        if (download)
        {
            var downloadName = !string.IsNullOrWhiteSpace(originalName) ? originalName : Path.GetFileName(filePath);
            return PhysicalFile(filePath, contentType, fileDownloadName: downloadName, enableRangeProcessing: true);
        }

        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    private async Task<(bool Success, string? ErrorMessage, string? StoredFileName, string? OriginalFileName, string? ContentType, long FileSize)> ValidateAndSaveAttachmentAsync(
        IFormFile? file, string? fallbackPath, string sectionNameAr, bool isRequired)
    {
        if (file == null || file.Length == 0)
        {
            if (!string.IsNullOrWhiteSpace(fallbackPath))
            {
                var webRootF = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var existingF = Path.Combine(webRootF, "uploads", "storage", Path.GetFileName(fallbackPath));
                if (System.IO.File.Exists(existingF))
                {
                    var extF = Path.GetExtension(fallbackPath).ToLowerInvariant();
                    var ct = extF == ".pdf" ? "application/pdf" : (extF == ".png" ? "image/png" : "image/jpeg");
                    return (true, null, Path.GetFileName(fallbackPath), Path.GetFileName(fallbackPath), ct, new FileInfo(existingF).Length);
                }
            }

            if (isRequired)
            {
                return (false, $"مرفق {sectionNameAr} مطلوب.", null, null, null, 0);
            }
            return (true, null, null, null, null, 0);
        }

        const long maxFileSize = 25 * 1024 * 1024; // 25 MB
        if (file.Length > maxFileSize)
        {
            return (false, $"حجم ملف {sectionNameAr} يتجاوز الحد الأقصى المسموح به (25 ميجابايت).", null, null, null, 0);
        }

        var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (fileExt != ".pdf" && fileExt != ".jpg" && fileExt != ".jpeg" && fileExt != ".png")
        {
            return (false, $"نوع ملف {sectionNameAr} غير مدعوم. الملفات المسموحة فقط: PDF, JPG, JPEG, PNG.", null, null, null, 0);
        }

        var mime = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var validMimes = new[] { "application/pdf", "image/jpeg", "image/pjpeg", "image/jpg", "image/png" };
        if (!validMimes.Contains(mime))
        {
            return (false, $"نوع ملف {sectionNameAr} غير مدعوم.", null, null, null, 0);
        }

        // Magic bytes inspection
        byte[] magicBytes = new byte[8];
        using (var stream = file.OpenReadStream())
        {
            var bytesRead = await stream.ReadAsync(magicBytes, 0, magicBytes.Length);
            if (bytesRead < 4)
            {
                return (false, $"محتوى ملف {sectionNameAr} تالف أو غير صالح.", null, null, null, 0);
            }

            if (fileExt == ".pdf")
            {
                if (magicBytes[0] != 0x25 || magicBytes[1] != 0x50 || magicBytes[2] != 0x44 || magicBytes[3] != 0x46)
                {
                    return (false, $"محتوى ملف PDF لـ {sectionNameAr} غير صالح.", null, null, null, 0);
                }
            }
            else if (fileExt == ".png")
            {
                if (magicBytes[0] != 0x89 || magicBytes[1] != 0x50 || magicBytes[2] != 0x4E || magicBytes[3] != 0x47)
                {
                    return (false, $"محتوى صورة PNG لـ {sectionNameAr} غير صالح.", null, null, null, 0);
                }
            }
            else
            {
                if (magicBytes[0] != 0xFF || magicBytes[1] != 0xD8 || magicBytes[2] != 0xFF)
                {
                    return (false, $"محتوى صورة JPG لـ {sectionNameAr} غير صالح.", null, null, null, 0);
                }
            }
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "storage");
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

    private Task<(bool Success, string? ErrorMessage, string? StoredFileName, string? OriginalFileName, string? ContentType, long FileSize)> ValidateAndSaveAttachmentAsync(
        IFormFile? file, string? fallbackPath, bool isRequired)
    {
        return ValidateAndSaveAttachmentAsync(file, fallbackPath, "إيصال الميزان", isRequired);
    }

    private void CleanupSavedFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        try
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var p = Path.Combine(webRoot, "uploads", "storage", fileName);
            if (System.IO.File.Exists(p))
            {
                System.IO.File.Delete(p);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up file {FileName}", fileName);
        }
    }

    private void CleanupSavedFiles(IEnumerable<string?> fileNames)
    {
        foreach (var fn in fileNames)
        {
            CleanupSavedFile(fn);
        }
    }

    /// <summary>
    /// Submit a new Storage Transaction (JSON format).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<StorageTransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateJson([FromBody] CreateStorageTransactionDto dto)
    {
        return await ProcessCreateAsync(dto);
    }

    /// <summary>
    /// Submit a new Storage Transaction (multipart/form-data format with optional/required attachment).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<StorageTransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateForm([FromForm] CreateStorageTransactionDto dto)
    {
        return await ProcessCreateAsync(dto);
    }

    private async Task<IActionResult> ProcessCreateAsync(CreateStorageTransactionDto dto)
    {
        // 1. Role Authorization
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasStorageAccess(role))
        {
            _logger.LogWarning("Unauthorized user with role '{Role}' attempted to submit storage transaction.", role);
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("Access Denied: Storage department is restricted to CEO, IT, and Storage Manager."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات الحركة غير صالحة."));
        }

        // =========================================================================
        // SECTION 1 VALIDATION — MATERIAL & ACTION (النوع)
        // =========================================================================

        // 1.1 Material validation
        var material = (dto.Material ?? string.Empty).Trim().ToLowerInvariant();
        if (material != "powder" && material != "slurry")
        {
            return BadRequest(ApiResponse.Fail("نوع الخامة غير صحيح. الخيارات المتاحة فقط: powder أو slurry."));
        }

        // 1.2 Action validation
        var rawAction = (dto.Action ?? string.Empty).Trim().ToLowerInvariant();
        string normalizedAction;
        if (rawAction == "import" || rawAction == "إيداع")
        {
            normalizedAction = "Import";
        }
        else if (rawAction == "export" || rawAction == "صرف")
        {
            normalizedAction = "Export";
        }
        else
        {
            return BadRequest(ApiResponse.Fail("نوع الحركة غير صحيح. الخيارات المتاحة فقط: إيداع (Import) أو صرف (Export)."));
        }

        // 1.3 Quantity validation according to Action
        decimal incomingQty = 0m;
        decimal outgoingQty = 0m;

        if (normalizedAction == "Import")
        {
            // Import: IncomingQuantity is required, OutgoingQuantity must NOT be entered
            if (!dto.IncomingQuantity.HasValue || dto.IncomingQuantity.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الكمية الواردة مطلوبة ويجب أن تكون أكبر من الصفر في حالة الإيداع."));
            }

            if (dto.OutgoingQuantity.HasValue && dto.OutgoingQuantity.Value > 0)
            {
                return BadRequest(ApiResponse.Fail("لا يمكن إدخال كمية صادرة في حالة حركة الإيداع."));
            }

            incomingQty = dto.IncomingQuantity.Value;
            outgoingQty = 0m;
        }
        else // Export
        {
            // Export: OutgoingQuantity is required, IncomingQuantity must NOT be entered
            if (!dto.OutgoingQuantity.HasValue || dto.OutgoingQuantity.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الكمية الصادرة مطلوبة ويجب أن تكون أكبر من الصفر في حالة الصرف."));
            }

            if (dto.IncomingQuantity.HasValue && dto.IncomingQuantity.Value > 0)
            {
                return BadRequest(ApiResponse.Fail("لا يمكن إدخال كمية واردة في حالة حركة الصرف."));
            }

            outgoingQty = dto.OutgoingQuantity.Value;
            incomingQty = 0m;
        }

        // 1.4 Purchase Order Validation for Export
        int? purchaseOrderId = null;
        string? poNumber = null;

        if (normalizedAction == "Export" && dto.IsExportRelatedToPO)
        {
            if (string.IsNullOrWhiteSpace(dto.PONumber))
            {
                return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب عند تحديد أن الصادر مربوط بأمر شراء."));
            }

            var trimmedPo = dto.PONumber.Trim();
            var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber.ToLower() == trimmedPo.ToLower());

            if (po == null)
            {
                _logger.LogWarning("Storage export transaction rejected: PO '{PONumber}' not found in database.", trimmedPo);
                return BadRequest(ApiResponse.Fail("Purchase Order not found."));
            }

            purchaseOrderId = po.Id;
            poNumber = po.PONumber;
        }

        // =========================================================================
        // SECTION 2 VALIDATION — DATE (التاريخ)
        // =========================================================================
        if (!dto.TransactionDate.HasValue)
        {
            return BadRequest(ApiResponse.Fail("تاريخ ووقت الحركة مطلوب."));
        }
        var transactionDateTime = dto.TransactionDate.Value;

        // =========================================================================
        // SECTION 3 VALIDATION — WEIGHBRIDGE (الميزان)
        // =========================================================================

        // 3.1 Internal Weighbridge
        if (string.IsNullOrWhiteSpace(dto.InternalWeighbridgeReference))
        {
            return BadRequest(ApiResponse.Fail("رقم مرجع الميزان الداخلي مطلوب ويجب إدخاله يدوياً."));
        }
        var internalRef = dto.InternalWeighbridgeReference.Trim();

        if (dto.InternalLoadedWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي قائم يجب أن يكون أكبر من الصفر."));
        }
        if (dto.InternalEmptyWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي فارغ يجب أن يكون أكبر من الصفر."));
        }
        if (dto.InternalLoadedWeight <= dto.InternalEmptyWeight)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي قائم يجب أن يكون أكبر من الوزن الداخلي فارغ."));
        }

        if (!dto.InternalLoadedWeightTime.HasValue)
        {
            return BadRequest(ApiResponse.Fail("تاريخ ووقت الوزن القائم الداخلي مطلوب."));
        }

        var internalNetWeight = Math.Round(dto.InternalLoadedWeight - dto.InternalEmptyWeight, 3);
        if (internalNetWeight <= 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الداخلي الصافي المحسوب يجب أن يكون أكبر من الصفر."));
        }

        // 3.2 External Weighbridge
        decimal? externalLoaded = null;
        decimal? externalEmpty = null;
        decimal? externalNet = null;
        DateTime? externalEmptyTime = null;
        DateTime? externalLoadedTime = null;
        string? externalRef = null;

        if (dto.HasExternalWeighbridge)
        {
            if (string.IsNullOrWhiteSpace(dto.ExternalWeighbridgeReference))
            {
                return BadRequest(ApiResponse.Fail("رقم مرجع الميزان الخارجي مطلوب عند تفعيل الميزان الخارجي."));
            }
            externalRef = dto.ExternalWeighbridgeReference.Trim();

            if (!dto.ExternalLoadedWeight.HasValue || dto.ExternalLoadedWeight.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الوزن الخارجي قائم يجب أن يكون أكبر من الصفر عند تفعيل الميزان الخارجي."));
            }
            if (!dto.ExternalEmptyWeight.HasValue || dto.ExternalEmptyWeight.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الوزن الخارجي فارغ يجب أن يكون أكبر من الصفر عند تفعيل الميزان الخارجي."));
            }
            if (dto.ExternalLoadedWeight.Value <= dto.ExternalEmptyWeight.Value)
            {
                return BadRequest(ApiResponse.Fail("الوزن الخارجي قائم يجب أن يكون أكبر من الوزن الخارجي فارغ."));
            }

            if (!dto.ExternalLoadedWeightTime.HasValue)
            {
                return BadRequest(ApiResponse.Fail("تاريخ ووقت الوزن القائم الخارجي مطلوب عند تفعيل الميزان الخارجي."));
            }

            externalLoaded = dto.ExternalLoadedWeight.Value;
            externalEmpty = dto.ExternalEmptyWeight.Value;
            externalNet = Math.Round(externalLoaded.Value - externalEmpty.Value, 3);
            externalEmptyTime = dto.ExternalEmptyWeightTime;
            externalLoadedTime = dto.ExternalLoadedWeightTime;

            if (externalNet.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("الوزن الخارجي الصافي المحسوب يجب أن يكون أكبر من الصفر."));
            }
        }

        // 3.3 Weighbridge Receipt Attachment Validation
        // Required if external weighbridge is enabled
        var isAttachmentRequired = dto.HasExternalWeighbridge;
        var attRes = await ValidateAndSaveAttachmentAsync(dto.WeighbridgeAttachment, dto.WeighbridgeAttachmentPath, isAttachmentRequired);
        if (!attRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attRes.ErrorMessage ?? "خطأ في التحقق من مرفق إيصال الميزان."));
        }

        // =========================================================================
        // SECTION 4 VALIDATION — RESPONSIBLE EMPLOYEE (الموظف المسؤول)
        // =========================================================================
        if (string.IsNullOrWhiteSpace(dto.ResponsibleEmployee))
        {
            CleanupSavedFile(attRes.StoredFileName);
            return BadRequest(ApiResponse.Fail("الموظف المسؤول مطلوب."));
        }

        var responsibleEmp = dto.ResponsibleEmployee.Trim();
        var empExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.FullName.ToLower() == responsibleEmp.ToLower() || u.Username.ToLower() == responsibleEmp.ToLower());

        if (!empExists)
        {
            CleanupSavedFile(attRes.StoredFileName);
            return BadRequest(ApiResponse.Fail("الموظف المسؤول المحدد غير موجود في قاعدة المستخدمين المعتمدة."));
        }

        // Generate movement number and reference number for backwards compatibility
        var maxMovement = await _db.StorageTransactions.MaxAsync(s => (int?)s.MovementNumber) ?? 5000;
        int movementNumber = (dto.MovementNumber.HasValue && dto.MovementNumber.Value > 0)
            ? dto.MovementNumber.Value
            : maxMovement + 1;

        var maxRef = await _db.StorageTransactions.MaxAsync(s => (int?)s.ReferenceNumber) ?? 7000;
        int legacyReferenceNumber = maxRef + 1;

        // Caller identity
        var callerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف المخزن";
        Guid? callerUserId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            callerUserId = parsedGuid;
        }

        // Build entity
        var transaction = new StorageTransaction
        {
            MovementNumber = movementNumber,
            TransactionDate = transactionDateTime,
            Material = material,
            Action = normalizedAction,
            ServiceType = normalizedAction == "Import" ? "إيداع" : "صرف",
            ReferenceNumber = legacyReferenceNumber,
            IsExportRelatedToPO = (normalizedAction == "Export" && dto.IsExportRelatedToPO),
            PONumber = poNumber,
            PurchaseOrderId = purchaseOrderId,
            IncomingQuantity = incomingQty,
            OutgoingQuantity = outgoingQty,
            OpeningBalance = 0m,
            ClosingBalance = incomingQty - outgoingQty,
            StorageLocation = "مستودع الخامات الرئيسي",

            // Weighbridge
            InternalWeighbridgeReference = internalRef,
            InternalLoadedWeight = dto.InternalLoadedWeight,
            InternalEmptyWeight = dto.InternalEmptyWeight,
            InternalNetWeight = internalNetWeight,
            InternalEmptyWeightTime = dto.InternalEmptyWeightTime,
            InternalLoadedWeightTime = dto.InternalLoadedWeightTime,

            HasExternalWeighbridge = dto.HasExternalWeighbridge,
            ExternalWeighbridgeReference = externalRef,
            ExternalLoadedWeight = externalLoaded,
            ExternalEmptyWeight = externalEmpty,
            ExternalNetWeight = externalNet,
            ExternalEmptyWeightTime = externalEmptyTime,
            ExternalLoadedWeightTime = externalLoadedTime,

            // Attachment
            WeighbridgeAttachmentPath = !string.IsNullOrWhiteSpace(attRes.StoredFileName) ? $"/api/storage/attachments/{attRes.StoredFileName}" : null,
            WeighbridgeFileName = attRes.OriginalFileName,
            WeighbridgeFileType = attRes.ContentType,
            WeighbridgeFileSize = attRes.FileSize,

            // Responsible Employee
            ReceivingEmployee = responsibleEmp,
            Notes = dto.Notes?.Trim(),
            CreatedBy = callerName,
            CreatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            ApprovalStatus = "APPROVED",
            ReviewedBy = "System (Auto-Approved)",
            ReviewedAt = DateTime.UtcNow
        };

        try
        {
            await _db.StorageTransactions.AddAsync(transaction);
            await _db.SaveChangesAsync();

            // Record initial audit trail entry
            var history = new FormApprovalHistory
            {
                FormType = "Storage",
                FormId = transaction.Id,
                PONumber = transaction.PONumber,
                TransactionOrMovementNumber = $"#{transaction.MovementNumber}",
                PreviousStatus = string.Empty,
                NewStatus = "PENDING",
                Action = "SUBMITTED",
                ActorName = callerName,
                ActorUserId = callerUserId,
                ActorRole = role,
                Comment = "تم إرسال حركة المستودع وبانتظار مراجعة واعتماد قسم التحكم والمراقبة.",
                Timestamp = DateTime.UtcNow
            };
            await _db.FormApprovalHistories.AddAsync(history);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Storage Transaction #{MovementNumber} created successfully as PENDING. Action={Action}, Material={Material}",
                transaction.MovementNumber, transaction.Action, transaction.Material);

            try
            {
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    department = "control-monitoring",
                    action = "SUBMITTED",
                    formType = "Storage",
                    id = transaction.Id,
                    po = transaction.PONumber,
                    movementNumber = transaction.MovementNumber,
                    submittedBy = callerName,
                    timestamp = DateTime.UtcNow
                });

                await _hubContext.Clients.All.SendAsync("OnFormSubmittedForReview", new
                {
                    formType = "Storage",
                    id = transaction.Id,
                    po = transaction.PONumber,
                    movementNumber = transaction.MovementNumber,
                    submittedBy = callerName,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception hubEx)
            {
                _logger.LogWarning(hubEx, "Failed to broadcast SignalR notification for new storage form.");
            }

            var resultDto = MapToDto(transaction);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<StorageTransactionDto>.Ok(resultDto, "تم حفظ حركة المخزن بنجاح وبانتظار اعتماد قسم التحكم والمراقبة"));
        }
        catch (Exception ex)
        {
            CleanupSavedFile(attRes.StoredFileName);
            _logger.LogError(ex, "Error persisting storage transaction.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء حفظ حركة المخزن في قاعدة البيانات."));
        }
    }

    private static StorageTransactionDto MapToDto(StorageTransaction s)
    {
        return new StorageTransactionDto
        {
            Id = s.Id,
            MovementNumber = s.MovementNumber,
            TransactionDate = s.TransactionDate,
            Material = s.Material,
            Action = s.Action,
            ServiceType = s.ServiceType,
            IncomingQuantity = (s.Action == "Export" || s.ServiceType == "صرف") ? null : s.IncomingQuantity,
            OutgoingQuantity = (s.Action == "Import" || s.ServiceType == "إيداع" || s.ServiceType == "إضافة") ? null : s.OutgoingQuantity,
            IsExportRelatedToPO = s.IsExportRelatedToPO,
            PONumber = s.PONumber,
            PurchaseOrderId = s.PurchaseOrderId,

            // Weighbridge
            InternalWeighbridgeReference = s.InternalWeighbridgeReference,
            InternalLoadedWeight = s.InternalLoadedWeight,
            InternalEmptyWeight = s.InternalEmptyWeight,
            InternalNetWeight = s.InternalNetWeight,
            InternalEmptyWeightTime = s.InternalEmptyWeightTime,
            InternalLoadedWeightTime = s.InternalLoadedWeightTime,

            HasExternalWeighbridge = s.HasExternalWeighbridge,
            ExternalWeighbridgeReference = s.ExternalWeighbridgeReference,
            ExternalLoadedWeight = s.ExternalLoadedWeight,
            ExternalEmptyWeight = s.ExternalEmptyWeight,
            ExternalNetWeight = s.ExternalNetWeight,
            ExternalEmptyWeightTime = s.ExternalEmptyWeightTime,
            ExternalLoadedWeightTime = s.ExternalLoadedWeightTime,

            WeighbridgeAttachmentPath = s.WeighbridgeAttachmentPath,
            WeighbridgeFileName = s.WeighbridgeFileName,
            WeighbridgeFileType = s.WeighbridgeFileType,
            WeighbridgeFileSize = s.WeighbridgeFileSize,

            ResponsibleEmployee = s.ReceivingEmployee,
            Notes = s.Notes,
            CreatedBy = s.CreatedBy,
            CreatedAt = s.CreatedAt,
            ApprovalStatus = string.IsNullOrWhiteSpace(s.ApprovalStatus) ? "APPROVED" : s.ApprovalStatus,
            ReviewedBy = s.ReviewedBy,
            ReviewedAt = s.ReviewedAt,
            RejectionReason = s.RejectionReason
        };
    }

    /// <summary>
    /// Resubmit / Update a rejected Storage transaction.
    /// Updates form fields, replaces any new attachment, resets status to PENDING,
    /// and logs a RESUBMITTED audit trail entry while preserving full previous history.
    /// </summary>
    [HttpPost("{id:int}/resubmit")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<StorageTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResubmitStorage(
        int id,
        [FromForm] CreateStorageTransactionDto dto,
        [FromForm] IFormFile? weighbridgeAttachment = null,
        [FromForm] string? comment = null)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var callerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "موظف المخزن";
        Guid? callerUserId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            callerUserId = parsedGuid;
        }

        var entity = await _db.StorageTransactions.FirstOrDefaultAsync(s => s.Id == id);
        if (entity == null)
        {
            return NotFound(ApiResponse.Fail($"لم يتم العثور على حركة المستودع #{id}."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات الحركة غير صالحة."));
        }

        var material = (dto.Material ?? string.Empty).Trim().ToLowerInvariant();
        if (material != "powder" && material != "slurry")
        {
            return BadRequest(ApiResponse.Fail("نوع الخامة غير صحيح. الخيارات المتاحة فقط: powder أو slurry."));
        }

        var rawAction = (dto.Action ?? string.Empty).Trim().ToLowerInvariant();
        string normalizedAction = (rawAction == "import" || rawAction == "إيداع") ? "Import" : "Export";

        decimal incomingQty = 0m;
        decimal outgoingQty = 0m;
        string? poNumber = null;
        int? purchaseOrderId = null;

        if (normalizedAction == "Import")
        {
            incomingQty = Math.Round(dto.IncomingQuantity ?? 0m, 3);
            if (incomingQty <= 0) return BadRequest(ApiResponse.Fail("الكمية الواردة مطلوبة ويجب أن تكون أكبر من الصفر."));
        }
        else
        {
            outgoingQty = Math.Round(dto.OutgoingQuantity ?? 0m, 3);
            if (outgoingQty <= 0) return BadRequest(ApiResponse.Fail("الكمية الصادرة مطلوبة ويجب أن تكون أكبر من الصفر."));

            if (dto.IsExportRelatedToPO)
            {
                poNumber = dto.PONumber?.Trim();
                if (string.IsNullOrWhiteSpace(poNumber)) return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب عند ربط الصرف بأمر شراء."));
                var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber == poNumber);
                if (po == null) return BadRequest(ApiResponse.Fail("أمر الشراء غير موجود."));
                purchaseOrderId = po.Id;
            }
        }

        // Validate and save attachment
        var attRes = await ValidateAndSaveAttachmentAsync(
            weighbridgeAttachment ?? dto.WeighbridgeAttachment,
            entity.WeighbridgeAttachmentPath,
            dto.HasExternalWeighbridge);

        if (!attRes.Success)
        {
            return BadRequest(ApiResponse.Fail(attRes.ErrorMessage!));
        }

        var internalNetWeight = Math.Round(dto.InternalLoadedWeight - dto.InternalEmptyWeight, 3);
        decimal? externalNet = null;
        if (dto.HasExternalWeighbridge && dto.ExternalLoadedWeight.HasValue && dto.ExternalEmptyWeight.HasValue)
        {
            externalNet = Math.Round(dto.ExternalLoadedWeight.Value - dto.ExternalEmptyWeight.Value, 3);
        }

        // Update fields
        entity.Material = material;
        entity.Action = normalizedAction;
        entity.ServiceType = normalizedAction == "Import" ? "إيداع" : "صرف";
        entity.IsExportRelatedToPO = (normalizedAction == "Export" && dto.IsExportRelatedToPO);
        entity.PONumber = poNumber;
        entity.PurchaseOrderId = purchaseOrderId;
        entity.IncomingQuantity = incomingQty;
        entity.OutgoingQuantity = outgoingQty;
        entity.ClosingBalance = incomingQty - outgoingQty;

        if (dto.TransactionDate.HasValue) entity.TransactionDate = dto.TransactionDate.Value;

        entity.InternalWeighbridgeReference = dto.InternalWeighbridgeReference?.Trim() ?? entity.InternalWeighbridgeReference;
        entity.InternalLoadedWeight = dto.InternalLoadedWeight;
        entity.InternalEmptyWeight = dto.InternalEmptyWeight;
        entity.InternalNetWeight = internalNetWeight;
        if (dto.InternalEmptyWeightTime.HasValue) entity.InternalEmptyWeightTime = dto.InternalEmptyWeightTime;
        if (dto.InternalLoadedWeightTime.HasValue) entity.InternalLoadedWeightTime = dto.InternalLoadedWeightTime;

        entity.HasExternalWeighbridge = dto.HasExternalWeighbridge;
        entity.ExternalWeighbridgeReference = dto.ExternalWeighbridgeReference?.Trim();
        entity.ExternalLoadedWeight = dto.ExternalLoadedWeight;
        entity.ExternalEmptyWeight = dto.ExternalEmptyWeight;
        entity.ExternalNetWeight = externalNet;
        if (dto.ExternalEmptyWeightTime.HasValue) entity.ExternalEmptyWeightTime = dto.ExternalEmptyWeightTime;
        if (dto.ExternalLoadedWeightTime.HasValue) entity.ExternalLoadedWeightTime = dto.ExternalLoadedWeightTime;

        if (weighbridgeAttachment != null && !string.IsNullOrWhiteSpace(attRes.StoredFileName))
        {
            entity.WeighbridgeAttachmentPath = $"/api/storage/attachments/{attRes.StoredFileName}";
            entity.WeighbridgeFileName = attRes.OriginalFileName;
            entity.WeighbridgeFileType = attRes.ContentType;
            entity.WeighbridgeFileSize = attRes.FileSize;
        }

        if (!string.IsNullOrWhiteSpace(dto.ResponsibleEmployee)) entity.ReceivingEmployee = dto.ResponsibleEmployee.Trim();
        if (dto.Notes != null) entity.Notes = dto.Notes.Trim();

        var prevStatus = entity.ApprovalStatus;
        entity.ApprovalStatus = "PENDING";
        entity.RejectionReason = null;
        entity.ReviewedBy = null;
        entity.ReviewedAt = null;

        var history = new FormApprovalHistory
        {
            FormType = "Storage",
            FormId = entity.Id,
            PONumber = entity.PONumber,
            TransactionOrMovementNumber = $"#{entity.MovementNumber}",
            PreviousStatus = prevStatus,
            NewStatus = "PENDING",
            Action = "RESUBMITTED",
            ActorName = callerName,
            ActorUserId = callerUserId,
            ActorRole = role,
            Comment = string.IsNullOrWhiteSpace(comment) ? "تم تعديل بيانات النموذج والمرفقات وإعادة تقديمه للتحكم والمراقبة." : comment.Trim(),
            Timestamp = DateTime.UtcNow
        };
        await _db.FormApprovalHistories.AddAsync(history);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Storage #{Movement} resubmitted by {User}. Status reset to PENDING.", entity.MovementNumber, callerName);

        try
        {
            await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
            {
                department = "control-monitoring",
                action = "RESUBMITTED",
                formType = "Storage",
                id = entity.Id,
                po = entity.PONumber,
                transactionNumber = entity.MovementNumber,
                submittedBy = callerName,
                timestamp = DateTime.UtcNow
            });

            await _hubContext.Clients.All.SendAsync("OnFormSubmittedForReview", new
            {
                formType = "Storage",
                id = entity.Id,
                po = entity.PONumber,
                transactionNumber = entity.MovementNumber,
                submittedBy = callerName,
                status = "PENDING",
                timestamp = DateTime.UtcNow
            });

            await _hubContext.Clients.All.SendAsync("OnFormApprovalStatusChanged", new
            {
                formType = "Storage",
                id = entity.Id,
                status = "PENDING",
                action = "RESUBMITTED"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR notification for resubmitted Storage #{Id}", entity.Id);
        }

        return Ok(ApiResponse<StorageTransactionDto>.Ok(MapToDto(entity), "تم تعديل بيانات النموذج وإعادة إرساله لقسم التحكم والمراقبة بنجاح."));
    }

    // =========================================================================
    // ITEM CARD & RAW MATERIAL MANAGEMENT (كارت صنف وإدارة الخامات)
    // =========================================================================

    /// <summary>
    /// استرجاع كافة أصناف المواد الخام مع أرصدتها المحسوبة (مع إمكانية الفلترة بنوع الخامة: ميكانيكية أو كيميائية)
    /// </summary>
    [HttpGet("materials")]
    [ProducesResponseType(typeof(ApiResponse<List<RawMaterialSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRawMaterials([FromQuery] string? type = null)
    {
        var query = _db.RawMaterials.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type) && !type.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normType = type.Trim().ToUpperInvariant();
            if (normType == "خامات ميكانيكية" || normType == "MECHANICAL")
            {
                query = query.Where(m => m.MaterialType == MaterialProcessType.Mechanical);
            }
            else if (normType == "خامات كيميائية" || normType == "CHEMICAL")
            {
                query = query.Where(m => m.MaterialType == MaterialProcessType.Chemical);
            }
            else
            {
                query = query.Where(m => m.MaterialType == normType);
            }
        }

        var materials = await query
            .OrderBy(m => m.Name)
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.MaterialType,
                m.InitialQuantity,
                m.CreatedAt,
                m.CreatedBy,
                Transactions = m.Transactions.Select(t => new
                {
                    t.TransactionType,
                    t.Quantity,
                    t.TotalValue
                }).ToList()
            })
            .ToListAsync();

        var result = materials.Select(m =>
        {
            decimal totalIncoming = m.Transactions
                .Where(t => t.TransactionType == "Incoming" || t.TransactionType == "OpeningStock")
                .Sum(t => t.Quantity);

            decimal totalOutgoing = m.Transactions
                .Where(t => t.TransactionType == "Outgoing")
                .Sum(t => t.Quantity);

            decimal incomingVal = m.Transactions
                .Where(t => t.TransactionType == "Incoming" || t.TransactionType == "OpeningStock")
                .Sum(t => t.TotalValue);

            decimal outgoingVal = m.Transactions
                .Where(t => t.TransactionType == "Outgoing")
                .Sum(t => t.TotalValue);

            decimal currentQty = Math.Max(0m, totalIncoming - totalOutgoing);
            decimal currentVal = Math.Max(0m, incomingVal - outgoingVal);
            decimal avgUnitPrice = currentQty > 0 ? Math.Round(currentVal / currentQty, 2) : 0m;

            return new RawMaterialSummaryDto
            {
                Id = m.Id,
                Name = m.Name,
                MaterialType = m.MaterialType,
                InitialQuantity = m.InitialQuantity,
                CreatedAt = m.CreatedAt,
                CreatedBy = m.CreatedBy,
                CurrentQuantity = currentQty,
                CurrentValue = currentVal,
                AverageUnitPrice = avgUnitPrice,
                TotalIncoming = totalIncoming,
                TotalOutgoing = totalOutgoing,
                TransactionCount = m.Transactions.Count
            };
        }).ToList();

        return Ok(ApiResponse<List<RawMaterialSummaryDto>>.Ok(result));
    }

    /// <summary>
    /// استرجاع صنف مادة خام محدد بالمعرف
    /// </summary>
    [HttpGet("materials/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<RawMaterialSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRawMaterialById(int id)
    {
        var m = await _db.RawMaterials
            .AsNoTracking()
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (m == null)
        {
            return NotFound(ApiResponse.Fail($"الصنف رقم #{id} غير موجود."));
        }

        decimal totalIncoming = m.Transactions
            .Where(t => t.TransactionType == "Incoming" || t.TransactionType == "OpeningStock")
            .Sum(t => t.Quantity);

        decimal totalOutgoing = m.Transactions
            .Where(t => t.TransactionType == "Outgoing")
            .Sum(t => t.Quantity);

        decimal incomingVal = m.Transactions
            .Where(t => t.TransactionType == "Incoming" || t.TransactionType == "OpeningStock")
            .Sum(t => t.TotalValue);

        decimal outgoingVal = m.Transactions
            .Where(t => t.TransactionType == "Outgoing")
            .Sum(t => t.TotalValue);

        decimal currentQty = Math.Max(0m, totalIncoming - totalOutgoing);
        decimal currentVal = Math.Max(0m, incomingVal - outgoingVal);
        decimal avgUnitPrice = currentQty > 0 ? Math.Round(currentVal / currentQty, 2) : 0m;

        var dto = new RawMaterialSummaryDto
        {
            Id = m.Id,
            Name = m.Name,
            MaterialType = m.MaterialType,
            InitialQuantity = m.InitialQuantity,
            CreatedAt = m.CreatedAt,
            CreatedBy = m.CreatedBy,
            CurrentQuantity = currentQty,
            CurrentValue = currentVal,
            AverageUnitPrice = avgUnitPrice,
            TotalIncoming = totalIncoming,
            TotalOutgoing = totalOutgoing,
            TransactionCount = m.Transactions.Count
        };

        return Ok(ApiResponse<RawMaterialSummaryDto>.Ok(dto));
    }

    /// <summary>
    /// إضافة صنف خامة جديد مع الرصيد الافتتاحي (+ إضافة صنف)
    /// </summary>
    [HttpPost("materials")]
    [ProducesResponseType(typeof(ApiResponse<RawMaterialSummaryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRawMaterial([FromBody] CreateRawMaterialDto dto)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasStorageAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("غير مصرح: إدارة الأصناف والمخازن مقتصرة على الرئيس التنفيذي ومدير تكنولوجيا المعلومات ومدير المستودعات."));
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(ApiResponse.Fail("اسم الصنف مطلوب."));
        }

        var trimmedName = dto.Name.Trim();
        if (trimmedName.Length > 200)
        {
            return BadRequest(ApiResponse.Fail("اسم الصنف لا يمكن أن يتجاوز 200 حرف."));
        }

        if (dto.InitialQuantity < 0)
        {
            return BadRequest(ApiResponse.Fail("الكمية يجب أن تكون صفر أو أكبر."));
        }

        var exists = await _db.RawMaterials
            .AsNoTracking()
            .AnyAsync(m => m.Name.ToLower() == trimmedName.ToLower());

        if (exists)
        {
            return BadRequest(ApiResponse.Fail("اسم الصنف مسجل مسبقاً، يرجى إدخال اسم فريد."));
        }

        var callerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "إدارة المخازن";
        Guid? callerUserId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            callerUserId = parsedGuid;
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var rawMaterial = new RawMaterial
            {
                Name = trimmedName,
                MaterialType = !string.IsNullOrWhiteSpace(dto.MaterialType) ? dto.MaterialType.Trim().ToUpperInvariant() : MaterialProcessType.Mechanical,
                InitialQuantity = Math.Round(dto.InitialQuantity, 3),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = callerName,
                CreatedByUserId = callerUserId
            };

            await _db.RawMaterials.AddAsync(rawMaterial);
            await _db.SaveChangesAsync();

            // If initial quantity > 0, record opening stock transaction in the ledger
            if (rawMaterial.InitialQuantity > 0)
            {
                var openingTx = new StorageItemTransaction
                {
                    RawMaterialId = rawMaterial.Id,
                    TransactionDate = DateTime.UtcNow,
                    TransactionType = "OpeningStock",
                    Description = "رصيد افتتاحي",
                    Quantity = rawMaterial.InitialQuantity,
                    UnitPrice = 0m,
                    TotalValue = 0m,
                    RunningQuantityBalance = rawMaterial.InitialQuantity,
                    RunningValueBalance = 0m,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = callerName,
                    CreatedByUserId = callerUserId,
                    Notes = "رصيد افتتاحي مسجل عند إنشاء الصنف"
                };

                await _db.StorageItemTransactions.AddAsync(openingTx);
                await _db.SaveChangesAsync();
            }

            await dbTx.CommitAsync();

            var summary = new RawMaterialSummaryDto
            {
                Id = rawMaterial.Id,
                Name = rawMaterial.Name,
                MaterialType = rawMaterial.MaterialType,
                InitialQuantity = rawMaterial.InitialQuantity,
                CreatedAt = rawMaterial.CreatedAt,
                CreatedBy = rawMaterial.CreatedBy,
                CurrentQuantity = rawMaterial.InitialQuantity,
                CurrentValue = 0m,
                AverageUnitPrice = 0m,
                TotalIncoming = rawMaterial.InitialQuantity,
                TotalOutgoing = 0m,
                TransactionCount = rawMaterial.InitialQuantity > 0 ? 1 : 0
            };

            try
            {
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    department = "storage",
                    action = "MATERIAL_CREATED",
                    materialId = rawMaterial.Id,
                    materialName = rawMaterial.Name,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast SignalR event for material created.");
            }

            return StatusCode(StatusCodes.Status201Created, ApiResponse<RawMaterialSummaryDto>.Ok(summary, "تم حفظ الصنف بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to create raw material {Name}", trimmedName);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ الصنف."));
        }
    }

    /// <summary>
    /// استعراض كارت الصنف الكامل مع كشف حركة الحساب والأرصدة التراكمية
    /// </summary>
    [HttpGet("materials/{id:int}/ledger")]
    [ProducesResponseType(typeof(ApiResponse<ItemCardLedgerResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemCardLedger(int id)
    {
        var material = await _db.RawMaterials
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (material == null)
        {
            return NotFound(ApiResponse.Fail($"الصنف #{id} غير موجود."));
        }

        var transactions = await _db.StorageItemTransactions
            .AsNoTracking()
            .Where(t => t.RawMaterialId == id)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync();

        decimal runningQty = 0m;
        decimal runningVal = 0m;
        decimal totalIncomingQty = 0m;
        decimal totalOutgoingQty = 0m;

        var ledgerRows = new List<ItemTransactionLedgerItemDto>();

        foreach (var tx in transactions)
        {
            var isIncomingOrOpening = tx.TransactionType == "Incoming" || tx.TransactionType == "OpeningStock";
            var isOutgoing = tx.TransactionType == "Outgoing";

            decimal? inQty = null;
            decimal? inPrice = null;
            decimal? inTotal = null;

            decimal? outQty = null;
            decimal? outPrice = null;
            decimal? outTotal = null;

            if (isIncomingOrOpening)
            {
                inQty = tx.Quantity;
                inPrice = tx.UnitPrice;
                inTotal = tx.TotalValue;
                runningQty += tx.Quantity;
                runningVal += tx.TotalValue;
                totalIncomingQty += tx.Quantity;
            }
            else if (isOutgoing)
            {
                outQty = tx.Quantity;
                outPrice = tx.UnitPrice;
                outTotal = tx.TotalValue;
                runningQty = Math.Max(0m, runningQty - tx.Quantity);
                runningVal = Math.Max(0m, runningVal - tx.TotalValue);
                totalOutgoingQty += tx.Quantity;
            }

            ledgerRows.Add(new ItemTransactionLedgerItemDto
            {
                Id = tx.Id,
                TransactionDate = tx.TransactionDate,
                TransactionType = tx.TransactionType,
                Description = tx.Description,
                IncomingQuantity = inQty,
                IncomingUnitPrice = inPrice,
                IncomingTotalValue = inTotal,
                OutgoingQuantity = outQty,
                OutgoingUnitPrice = outPrice,
                OutgoingTotalValue = outTotal,
                DestinationType = tx.DestinationType,
                Destination = tx.Destination,
                RunningQuantityBalance = Math.Round(runningQty, 3),
                RunningValueBalance = Math.Round(runningVal, 2),
                SourceTransportationId = tx.SourceTransportationId,
                SourceTransportationNumber = tx.SourceTransportationNumber,
                CreatedAt = tx.CreatedAt,
                CreatedBy = tx.CreatedBy
            });
        }

        decimal avgCost = runningQty > 0 ? Math.Round(runningVal / runningQty, 2) : 0m;

        var materialSummary = new RawMaterialSummaryDto
        {
            Id = material.Id,
            Name = material.Name,
            InitialQuantity = material.InitialQuantity,
            CreatedAt = material.CreatedAt,
            CreatedBy = material.CreatedBy,
            CurrentQuantity = Math.Round(runningQty, 3),
            CurrentValue = Math.Round(runningVal, 2),
            AverageUnitPrice = avgCost,
            TotalIncoming = Math.Round(totalIncomingQty, 3),
            TotalOutgoing = Math.Round(totalOutgoingQty, 3),
            TransactionCount = transactions.Count
        };

        var response = new ItemCardLedgerResponseDto
        {
            Material = materialSummary,
            OpeningBalance = material.InitialQuantity,
            CurrentQuantityBalance = Math.Round(runningQty, 3),
            CurrentValueBalance = Math.Round(runningVal, 2),
            AverageUnitCost = avgCost,
            TotalIncomingQuantity = Math.Round(totalIncomingQty, 3),
            TotalOutgoingQuantity = Math.Round(totalOutgoingQty, 3),
            Transactions = ledgerRows
        };

        return Ok(ApiResponse<ItemCardLedgerResponseDto>.Ok(response));
    }

    /// <summary>
    /// تسجيل حركة جديدة في كارت الصنف (وارد أو منصرف)
    /// </summary>
    [HttpPost("materials/{id:int}/transactions")]
    [ProducesResponseType(typeof(ApiResponse<ItemTransactionLedgerItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateItemTransaction(int id, [FromBody] CreateItemTransactionDto dto)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasStorageAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("غير مصرح: تسجيل حركات كارت الصنف مقتصر على مصرحي إدارة المستودعات."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات الحركة غير صالحة."));
        }

        // Validate Description (البيان: علاء | اسلام | محمود)
        var desc = (dto.Description ?? string.Empty).Trim();
        var allowedDescriptions = new[] { "علاء", "اسلام", "محمود" };
        if (!allowedDescriptions.Contains(desc))
        {
            return BadRequest(ApiResponse.Fail("البيان غير صحيح. الخيارات المتاحة فقط: علاء، اسلام، محمود."));
        }

        // Normalize TransactionType
        var rawType = (dto.TransactionType ?? string.Empty).Trim().ToLowerInvariant();
        string normalizedType;
        if (rawType == "incoming" || rawType == "وارد")
        {
            normalizedType = "Incoming";
        }
        else if (rawType == "outgoing" || rawType == "منصرف")
        {
            normalizedType = "Outgoing";
        }
        else
        {
            return BadRequest(ApiResponse.Fail("نوع الحركة غير صحيح. الخيارات المتاحة: وارد (Incoming) أو منصرف (Outgoing)."));
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("الكمية يجب أن تكون أكبر من الصفر."));
        }

        if (dto.UnitPrice < 0)
        {
            return BadRequest(ApiResponse.Fail("سعر الوحدة يجب أن يكون صفر أو أكبر."));
        }

        string? destinationType = null;
        string? destination = null;

        if (normalizedType == "Outgoing")
        {
            var rawDestType = (dto.DestinationType ?? string.Empty).Trim().ToLowerInvariant();
            if (rawDestType == "داخلي" || rawDestType == "internal")
            {
                destinationType = "داخلي";
                destination = "المصنع";
            }
            else if (rawDestType == "خارجي" || rawDestType == "external")
            {
                destinationType = "خارجي";
                if (string.IsNullOrWhiteSpace(dto.Destination))
                {
                    return BadRequest(ApiResponse.Fail("جهة الصرف الخارجية مطلوبة عند اختيار نوع صرف خارجي."));
                }
                destination = dto.Destination.Trim();
            }
            else
            {
                return BadRequest(ApiResponse.Fail("نوع الصرف مطلوب. الخيارات المتاحة: داخلي (المصنع) أو خارجي."));
            }
        }

        // Transportation Linking Validation
        int? transId = null;
        int? transNum = null;

        if (normalizedType == "Incoming" && dto.SourceTransportationId.HasValue)
        {
            var trans = await _db.Transportations
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == dto.SourceTransportationId.Value);

            if (trans == null)
            {
                return BadRequest(ApiResponse.Fail("حركة النقل المحددة غير موجودة في النظام."));
            }

            if (trans.ApprovalStatus != "APPROVED")
            {
                return BadRequest(ApiResponse.Fail("لا يمكن إضافة كمية من حركة نقل غير معتمدة رسمياً من قسم التحكم والمراقبة."));
            }

            // Prevent duplicate posting
            var alreadyPosted = await _db.StorageItemTransactions
                .AsNoTracking()
                .AnyAsync(t => t.SourceTransportationId == trans.Id);

            if (alreadyPosted)
            {
                return BadRequest(ApiResponse.Fail($"حركة النقل رقم #{trans.TransactionNumber} تم إضافتها للمخزن مسبقاً ولا يمكن تكرار إضافتها."));
            }

            transId = trans.Id;
            transNum = trans.TransactionNumber;
        }

        var callerName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "إدارة المخازن";
        Guid? callerUserId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedGuid))
        {
            callerUserId = parsedGuid;
        }

        decimal totalValue = Math.Round(dto.Quantity * dto.UnitPrice, 2);

        // Transaction Safe Concurrency & Balance Recalculation
        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Lock RawMaterial row
            var material = await _db.RawMaterials
                .FromSqlInterpolated($"SELECT * FROM RawMaterials WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}")
                .FirstOrDefaultAsync();

            if (material == null)
            {
                await dbTx.RollbackAsync();
                return NotFound(ApiResponse.Fail($"الصنف #{id} غير موجود."));
            }

            // Calculate current balance
            var existingTxs = await _db.StorageItemTransactions
                .Where(t => t.RawMaterialId == id)
                .OrderBy(t => t.TransactionDate)
                .ThenBy(t => t.Id)
                .ToListAsync();

            decimal totalIn = existingTxs
                .Where(t => t.TransactionType == "Incoming" || t.TransactionType == "OpeningStock")
                .Sum(t => t.Quantity);

            decimal totalOut = existingTxs
                .Where(t => t.TransactionType == "Outgoing")
                .Sum(t => t.Quantity);

            decimal curQty = totalIn - totalOut;

            decimal valIn = existingTxs
                .Where(t => t.TransactionType == "Incoming" || t.TransactionType == "OpeningStock")
                .Sum(t => t.TotalValue);

            decimal valOut = existingTxs
                .Where(t => t.TransactionType == "Outgoing")
                .Sum(t => t.TotalValue);

            decimal curVal = Math.Max(0m, valIn - valOut);

            // Validation for Outgoing: cannot exceed available balance!
            if (normalizedType == "Outgoing")
            {
                if (dto.Quantity > curQty)
                {
                    await dbTx.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"الكمية المنصرفة المطلوبة ({dto.Quantity:N3}) تتجاوز الرصيد المتاح حالياً بالمخزن ({curQty:N3})."));
                }
            }

            // New running balance
            decimal newQtyBalance;
            decimal newValBalance;

            if (normalizedType == "Incoming")
            {
                newQtyBalance = curQty + dto.Quantity;
                newValBalance = curVal + totalValue;
            }
            else
            {
                newQtyBalance = Math.Max(0m, curQty - dto.Quantity);
                newValBalance = Math.Max(0m, curVal - totalValue);
            }

            var itemTx = new StorageItemTransaction
            {
                RawMaterialId = material.Id,
                TransactionDate = dto.TransactionDate,
                TransactionType = normalizedType,
                Description = desc,
                Quantity = Math.Round(dto.Quantity, 3),
                UnitPrice = Math.Round(dto.UnitPrice, 2),
                TotalValue = totalValue,
                DestinationType = destinationType,
                Destination = destination,
                SourceTransportationId = transId,
                SourceTransportationNumber = transNum,
                RunningQuantityBalance = Math.Round(newQtyBalance, 3),
                RunningValueBalance = Math.Round(newValBalance, 2),
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = callerName,
                CreatedByUserId = callerUserId
            };

            await _db.StorageItemTransactions.AddAsync(itemTx);
            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Storage Item Transaction recorded: Material={Material}, Type={Type}, Qty={Qty}, NewBalance={Balance}",
                material.Name, normalizedType, itemTx.Quantity, newQtyBalance);

            try
            {
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    department = "storage",
                    action = "ITEM_TRANSACTION_RECORDED",
                    materialId = material.Id,
                    materialName = material.Name,
                    transactionId = itemTx.Id,
                    transactionType = itemTx.TransactionType,
                    quantity = itemTx.Quantity,
                    newBalance = newQtyBalance,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast SignalR event for item transaction.");
            }

            var resultDto = new ItemTransactionLedgerItemDto
            {
                Id = itemTx.Id,
                TransactionDate = itemTx.TransactionDate,
                TransactionType = itemTx.TransactionType,
                Description = itemTx.Description,
                IncomingQuantity = normalizedType == "Incoming" ? itemTx.Quantity : null,
                IncomingUnitPrice = normalizedType == "Incoming" ? itemTx.UnitPrice : null,
                IncomingTotalValue = normalizedType == "Incoming" ? itemTx.TotalValue : null,
                OutgoingQuantity = normalizedType == "Outgoing" ? itemTx.Quantity : null,
                OutgoingUnitPrice = normalizedType == "Outgoing" ? itemTx.UnitPrice : null,
                OutgoingTotalValue = normalizedType == "Outgoing" ? itemTx.TotalValue : null,
                DestinationType = itemTx.DestinationType,
                Destination = itemTx.Destination,
                RunningQuantityBalance = itemTx.RunningQuantityBalance,
                RunningValueBalance = itemTx.RunningValueBalance,
                SourceTransportationId = itemTx.SourceTransportationId,
                SourceTransportationNumber = itemTx.SourceTransportationNumber,
                CreatedAt = itemTx.CreatedAt,
                CreatedBy = itemTx.CreatedBy
            };

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<ItemTransactionLedgerItemDto>.Ok(resultDto, "تم تسجيل الحركة بنجاح وتحديث رصيد الصنف."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Error recording item transaction for material #{Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ حركة الصنف."));
        }
    }

    /// <summary>
    /// استرجاع حركات النقل المعتمدة المؤهلة للربط بوارد المخزن
    /// </summary>
    [HttpGet("transportation-candidates")]
    [ProducesResponseType(typeof(ApiResponse<List<TransportationCandidateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransportationCandidates([FromQuery] bool unpostedOnly = false)
    {
        var postedTransIds = await _db.StorageItemTransactions
            .AsNoTracking()
            .Where(t => t.SourceTransportationId.HasValue)
            .Select(t => new { TransId = t.SourceTransportationId!.Value, MaterialName = t.RawMaterial != null ? t.RawMaterial.Name : string.Empty })
            .ToDictionaryAsync(x => x.TransId, x => x.MaterialName);

        var query = _db.Transportations
            .AsNoTracking()
            .Include(t => t.PurchaseOrder)
            .Where(t => t.ApprovalStatus == "APPROVED");

        var list = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .Take(100)
            .ToListAsync();

        var candidates = list.Select(t =>
        {
            bool isPosted = postedTransIds.ContainsKey(t.Id);
            string? postedMat = isPosted ? postedTransIds[t.Id] : null;

            return new TransportationCandidateDto
            {
                TransportationId = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                PONumber = t.PONumber,
                MaterialType = t.MaterialType,
                NetWeight = t.NetWeight,
                TonPrice = t.PurchaseOrder?.TonPrice ?? (t.PurchaseOrder?.PricePerTon > 0 ? t.PurchaseOrder.PricePerTon : null),
                DriverName = t.DriverName,
                CarNumber = t.CarNumber,
                Source = t.Source,
                IsAlreadyPosted = isPosted,
                PostedMaterialName = postedMat
            };
        }).Where(c => !unpostedOnly || !c.IsAlreadyPosted).ToList();

        return Ok(ApiResponse<List<TransportationCandidateDto>>.Ok(candidates));
    }

    // =========================================================================
    // STORAGE DEPARTMENT REDESIGN — 3 MAIN CARDS (IMPORT, EXPORT, TRANSFER)
    // =========================================================================

    /// <summary>
    /// Retrieve all configured active storage locations.
    /// </summary>
    [HttpGet("locations")]
    [ProducesResponseType(typeof(ApiResponse<List<StorageLocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocations()
    {
        var locations = await _db.StorageLocations
            .AsNoTracking()
            .Where(l => l.IsActive)
            .OrderBy(l => l.Id)
            .Select(l => new StorageLocationDto
            {
                Id = l.Id,
                Name = l.Name,
                Code = l.Code,
                Description = l.Description,
                IsActive = l.IsActive
            })
            .ToListAsync();

        return Ok(ApiResponse<List<StorageLocationDto>>.Ok(locations));
    }

    /// <summary>
    /// Retrieve all registered clients and their project destinations for Export operations.
    /// </summary>
    [HttpGet("clients")]
    [ProducesResponseType(typeof(ApiResponse<List<StorageClientDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClients()
    {
        var clients = await _db.StorageClients
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.Destinations.Where(d => d.IsActive))
            .OrderBy(c => c.Name)
            .Select(c => new StorageClientDto
            {
                Id = c.Id,
                Name = c.Name,
                PhoneNumber = c.PhoneNumber,
                IsActive = c.IsActive,
                Destinations = c.Destinations.Select(d => new StorageClientDestinationDto
                {
                    Id = d.Id,
                    StorageClientId = d.StorageClientId,
                    DestinationName = d.DestinationName,
                    IsActive = d.IsActive
                }).ToList()
            })
            .ToListAsync();

        return Ok(ApiResponse<List<StorageClientDto>>.Ok(clients));
    }

    /// <summary>
    /// Retrieve materials list with current approved available stock in a specific storage location.
    /// Never combines stock across locations when locationId is not provided.
    /// </summary>
    [HttpGet("materials-stock")]
    [ProducesResponseType(typeof(ApiResponse<List<StorageMaterialStockDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMaterialsStock([FromQuery] int? locationId, [FromQuery] bool inStockOnly = false, [FromQuery] string? type = null)
    {
        var normalizedType = type?.Trim().ToUpperInvariant();

        // 1. If no locationId specified or invalid, NEVER combine quantities across locations!
        if (!locationId.HasValue || locationId.Value <= 0)
        {
            if (inStockOnly)
            {
                return Ok(ApiResponse<List<StorageMaterialStockDto>>.Ok(new List<StorageMaterialStockDto>()));
            }

            var query = _db.RawMaterials.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(normalizedType))
            {
                query = query.Where(m => m.MaterialType == normalizedType);
            }

            var allMaterials = await query
                .OrderBy(m => m.Name)
                .ToListAsync();

            var zeroStockList = allMaterials.Select(m => new StorageMaterialStockDto
            {
                Id = m.Id,
                Name = m.Name,
                MaterialType = m.MaterialType,
                AvailableQuantity = 0m,
                StorageLocationId = null,
                StorageLocationName = null
            }).ToList();

            return Ok(ApiResponse<List<StorageMaterialStockDto>>.Ok(zeroStockList));
        }

        var location = await _db.StorageLocations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId.Value);
        if (location == null)
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين المحدد غير موجود."));
        }

        var matQuery = _db.RawMaterials.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedType))
        {
            matQuery = matQuery.Where(m => m.MaterialType == normalizedType);
        }

        var materials = await matQuery
            .OrderBy(m => m.Name)
            .ToListAsync();

        // Calculate approved inventory from ledger (source of truth) for THIS location ONLY
        var stockMap = await _db.StorageInventoryLedger
            .AsNoTracking()
            .Where(l => l.StorageLocationId == locationId.Value)
            .GroupBy(l => l.RawMaterialId)
            .Select(g => new { MaterialId = g.Key, Balance = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.MaterialId, x => x.Balance);

        var result = materials.Select(m => new StorageMaterialStockDto
        {
            Id = m.Id,
            Name = m.Name,
            MaterialType = m.MaterialType,
            AvailableQuantity = stockMap.TryGetValue(m.Id, out var bal) ? Math.Max(0m, bal) : 0m,
            StorageLocationId = location.Id,
            StorageLocationName = location.Name
        });

        if (inStockOnly)
        {
            result = result.Where(m => m.AvailableQuantity > 0m);
        }

        return Ok(ApiResponse<List<StorageMaterialStockDto>>.Ok(result.ToList()));
    }

    /// <summary>
    /// Retrieve next automatic transaction serial numbers for Import, Export, and Transfer.
    /// </summary>
    [HttpGet("next-numbers")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNextNumbers()
    {
        var nextImport = await PeekNextSequenceNumberAsync("IMPORT");
        var nextExport = await PeekNextSequenceNumberAsync("EXPORT");
        var nextTransfer = await PeekNextSequenceNumberAsync("TRANSFER");

        return Ok(ApiResponse<object>.Ok(new
        {
            nextImport,
            nextExport,
            nextTransfer
        }));
    }

    private async Task<CreateStorageImportRequestDto?> ParseImportRequestAsync()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var dto = new CreateStorageImportRequestDto();
            if (int.TryParse(form["storageLocationId"], out var locId)) dto.StorageLocationId = locId;
            dto.MaterialType = form["materialType"].ToString() ?? "MECHANICAL";
            dto.Source = form["source"].ToString() ?? string.Empty;
            dto.SourcePhone = form["sourcePhone"];
            if (DateTime.TryParse(form["transactionDate"], out var dt)) dto.TransactionDate = dt;
            dto.Notes = form["notes"];
            dto.ItemsJson = form["itemsJson"];

            if (decimal.TryParse(form["externalReportedWeight"], out var extW)) dto.ExternalReportedWeight = extW;
            if (decimal.TryParse(form["internalLoadedWeight"], out var inLw)) dto.InternalLoadedWeight = inLw;
            if (decimal.TryParse(form["internalEmptyWeight"], out var inEw)) dto.InternalEmptyWeight = inEw;
            if (decimal.TryParse(form["internalNetWeight"], out var inNw)) dto.InternalNetWeight = inNw;
            if (DateTime.TryParse(form["internalWeighingTime"], out var inWt)) dto.InternalWeighingTime = inWt;
            dto.WeighbridgeReference = form["weighbridgeReference"];
            dto.WeighingResponsibleEmployee = form["weighingResponsibleEmployee"];
            dto.DiscrepancyExplanation = form["discrepancyExplanation"];
            if (int.TryParse(form["transportationId"], out var trId)) dto.TransportationId = trId;
            if (int.TryParse(form["productionReferenceNumber"], out var prnImp)) dto.ProductionReferenceNumber = prnImp;
            if (int.TryParse(form["laboratoryReportId"], out var lrIdImp)) dto.LaboratoryReportId = lrIdImp;
            if (int.TryParse(form["laboratoryCertificateId"], out var certIdImp)) dto.LaboratoryCertificateId = certIdImp;
            if (int.TryParse(form["productionFinishedProductId"], out var pfpIdImp)) dto.ProductionFinishedProductId = pfpIdImp;
            dto.SampleCode = form["sampleCode"];
            if (int.TryParse(form["numberOfBatches"], out var nbImp)) dto.NumberOfBatches = nbImp;
            dto.BatchesJson = form["batchesJson"];

            dto.WeighbridgeAttachment = form.Files.GetFile("weighbridgeAttachment") ?? form.Files.GetFile("WeighbridgeAttachment");
            dto.QocAttachment = form.Files.GetFile("qocAttachment") ?? form.Files.GetFile("QocAttachment");
            dto.DocumentAttachment = form.Files.GetFile("documentAttachment") ?? form.Files.GetFile("DocumentAttachment");
            dto.WeighbridgeAttachmentPath = form["weighbridgeAttachmentPath"];
            dto.QocAttachmentPath = form["qocAttachmentPath"];
            dto.DocumentAttachmentPath = form["documentAttachmentPath"];

            if (!string.IsNullOrWhiteSpace(dto.BatchesJson))
            {
                try
                {
                    dto.Batches = System.Text.Json.JsonSerializer.Deserialize<List<BatchDivisionInputDto>>(
                        dto.BatchesJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(dto.ItemsJson))
            {
                try
                {
                    dto.Items = System.Text.Json.JsonSerializer.Deserialize<List<StorageOperationLineInputDto>>(
                        dto.ItemsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch { }
            }
            return dto;
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body)) return null;
            var dto = System.Text.Json.JsonSerializer.Deserialize<CreateStorageImportRequestDto>(
                body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto != null)
            {
                if ((dto.Batches == null || dto.Batches.Count == 0) && !string.IsNullOrWhiteSpace(dto.BatchesJson))
                {
                    try
                    {
                        dto.Batches = System.Text.Json.JsonSerializer.Deserialize<List<BatchDivisionInputDto>>(
                            dto.BatchesJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    }
                    catch { }
                }
                if ((dto.Items == null || dto.Items.Count == 0) && !string.IsNullOrWhiteSpace(dto.ItemsJson))
                {
                    try
                    {
                        dto.Items = System.Text.Json.JsonSerializer.Deserialize<List<StorageOperationLineInputDto>>(
                            dto.ItemsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    }
                    catch { }
                }
            }
            return dto;
        }
    }

    private async Task<CreateStorageExportRequestDto?> ParseExportRequestAsync()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var dto = new CreateStorageExportRequestDto();
            if (int.TryParse(form["storageLocationId"], out var locId)) dto.StorageLocationId = locId;
            if (int.TryParse(form["clientId"], out var clId)) dto.ClientId = clId;
            if (int.TryParse(form["destinationId"], out var dstId)) dto.DestinationId = dstId;
            dto.ClientPhone = form["clientPhone"];
            dto.MaterialType = form["materialType"];
            if (DateTime.TryParse(form["transactionDate"], out var dt)) dto.TransactionDate = dt;
            dto.Notes = form["notes"];
            dto.ItemsJson = form["itemsJson"];
            dto.SelectionMode = form["selectionMode"].ToString() ?? "BY_REFERENCE";
            if (string.IsNullOrWhiteSpace(dto.SelectionMode)) dto.SelectionMode = "BY_REFERENCE";
            dto.SampleCode = form["sampleCode"];
            if (int.TryParse(form["productionReferenceNumber"], out var prnExp)) dto.ProductionReferenceNumber = prnExp;
            if (int.TryParse(form["laboratoryReportId"], out var lrExp)) dto.LaboratoryReportId = lrExp;

            if (!string.IsNullOrWhiteSpace(form["exportType"])) dto.ExportType = form["exportType"].ToString()!;
            if (int.TryParse(form["productionOrderId"], out var poIdExp)) dto.ProductionOrderId = poIdExp;
            if (int.TryParse(form["productionProcessId"], out var procIdExp)) dto.ProductionProcessId = procIdExp;
            if (!string.IsNullOrWhiteSpace(form["manufactureNumber"])) dto.ManufactureNumber = form["manufactureNumber"].ToString()!;

            if (decimal.TryParse(form["externalReportedWeight"], out var extW)) dto.ExternalReportedWeight = extW;
            if (decimal.TryParse(form["internalLoadedWeight"], out var inLw)) dto.InternalLoadedWeight = inLw;
            if (decimal.TryParse(form["internalEmptyWeight"], out var inEw)) dto.InternalEmptyWeight = inEw;
            if (decimal.TryParse(form["internalNetWeight"], out var inNw)) dto.InternalNetWeight = inNw;
            if (DateTime.TryParse(form["internalWeighingTime"], out var inWt)) dto.InternalWeighingTime = inWt;
            dto.WeighbridgeReference = form["weighbridgeReference"];
            dto.WeighingResponsibleEmployee = form["weighingResponsibleEmployee"];
            dto.DiscrepancyExplanation = form["discrepancyExplanation"];
            if (int.TryParse(form["transportationId"], out var trId)) dto.TransportationId = trId;

            dto.WeighbridgeAttachment = form.Files.GetFile("weighbridgeAttachment") ?? form.Files.GetFile("WeighbridgeAttachment");
            dto.DocumentAttachment = form.Files.GetFile("documentAttachment") ?? form.Files.GetFile("DocumentAttachment");
            dto.WeighbridgeAttachmentPath = form["weighbridgeAttachmentPath"];
            dto.DocumentAttachmentPath = form["documentAttachmentPath"];

            if (!string.IsNullOrWhiteSpace(dto.ItemsJson))
            {
                try
                {
                    dto.Items = System.Text.Json.JsonSerializer.Deserialize<List<StorageOperationLineInputDto>>(
                        dto.ItemsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch { }
            }
            return dto;
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body)) return null;
            var dto = System.Text.Json.JsonSerializer.Deserialize<CreateStorageExportRequestDto>(
                body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto != null && (dto.Items == null || dto.Items.Count == 0) && !string.IsNullOrWhiteSpace(dto.ItemsJson))
            {
                try
                {
                    dto.Items = System.Text.Json.JsonSerializer.Deserialize<List<StorageOperationLineInputDto>>(
                        dto.ItemsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch { }
            }
            return dto;
        }
    }

    private async Task<CreateStorageTransferRequestDto?> ParseTransferRequestAsync()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            var dto = new CreateStorageTransferRequestDto();
            if (int.TryParse(form["storageLocationId"], out var locId)) dto.StorageLocationId = locId;
            if (DateTime.TryParse(form["transactionDate"], out var dt)) dto.TransactionDate = dt;
            dto.Notes = form["notes"];
            dto.DestinationType = form["destinationType"].ToString();
            if (string.IsNullOrWhiteSpace(dto.DestinationType)) dto.DestinationType = "PRODUCTION";
            if (int.TryParse(form["destinationStorageLocationId"], out var dstLoc)) dto.DestinationStorageLocationId = dstLoc;
            dto.ItemsJson = form["itemsJson"];
            dto.SelectionMode = form["selectionMode"].ToString() ?? "BY_REFERENCE";
            if (string.IsNullOrWhiteSpace(dto.SelectionMode)) dto.SelectionMode = "BY_REFERENCE";
            dto.SampleCode = form["sampleCode"];
            if (int.TryParse(form["laboratoryReportId"], out var lrTrf)) dto.LaboratoryReportId = lrTrf;
            if (int.TryParse(form["productionReferenceNumber"], out var prnTrf)) dto.ProductionReferenceNumber = prnTrf;
            dto.DocumentAttachment = form.Files.GetFile("documentAttachment") ?? form.Files.GetFile("DocumentAttachment");
            dto.WeighbridgeAttachment = form.Files.GetFile("weighbridgeAttachment") ?? form.Files.GetFile("WeighbridgeAttachment");
            dto.DocumentAttachmentPath = form["documentAttachmentPath"];
            dto.WeighbridgeAttachmentPath = form["weighbridgeAttachmentPath"];

            if (!string.IsNullOrWhiteSpace(dto.ItemsJson))
            {
                try
                {
                    dto.Items = System.Text.Json.JsonSerializer.Deserialize<List<StorageOperationLineInputDto>>(
                        dto.ItemsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch { }
            }
            return dto;
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body)) return null;
            var dto = System.Text.Json.JsonSerializer.Deserialize<CreateStorageTransferRequestDto>(
                body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto != null && (dto.Items == null || dto.Items.Count == 0) && !string.IsNullOrWhiteSpace(dto.ItemsJson))
            {
                try
                {
                    dto.Items = System.Text.Json.JsonSerializer.Deserialize<List<StorageOperationLineInputDto>>(
                        dto.ItemsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
                catch { }
            }
            return dto;
        }
    }

    /// <summary>
    /// Submit a new Import Transaction (إذن إضافة مواد إلى المخزن).
    /// Requires 3 mandatory attachments: Weighbridge, QOC Report, and Written Document.
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ApiResponse<StorageOperationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateImport()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم المخازن."));
        }

        var dto = await ParseImportRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات إذن الإضافة غير صالحة."));
        }

        // 1. Validate Location
        var location = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.StorageLocationId && l.IsActive);
        if (location == null)
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين المحدد غير موجود أو غير مفعل."));
        }

        // 2. Validate Material Type (Required: MECHANICAL or CHEMICAL or FINAL_PRODUCT)
        var materialType = (dto.MaterialType ?? string.Empty).Trim().ToUpperInvariant();
        var isFinishedProduct = materialType == "FINAL_PRODUCT" ||
                                (dto.LaboratoryCertificateId.HasValue && dto.LaboratoryCertificateId.Value > 0) ||
                                (dto.ProductionFinishedProductId.HasValue && dto.ProductionFinishedProductId.Value > 0);

        if (!isFinishedProduct && (string.IsNullOrWhiteSpace(materialType) || (materialType != MaterialProcessType.Mechanical && materialType != MaterialProcessType.Chemical)))
        {
            return BadRequest(ApiResponse.Fail("يرجى تحديد نوع الخامات (خامات ميكانيكية أو خامات كيميائية أو منتج نهائي) قبل المتابعة."));
        }

        // 3. Validate Source / Supplier
        var source = (dto.Source ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(source))
        {
            source = isFinishedProduct ? "قسم الإنتاج والتصنيع" : string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return BadRequest(ApiResponse.Fail("المورد / المصدر مطلوب."));
            }
        }

        // 4. Validate Phone Number (Optional, but validate format if entered)
        var phone = (dto.SourcePhone ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(phone) && !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^(\+?[0-9\s\-()]{7,25})$"))
        {
            return BadRequest(ApiResponse.Fail("رقم هاتف المورد / المصدر غير صالح. يرجى إدخال رقم هاتف صحيح أو تركه فارغاً."));
        }

        // 5. Check if this is a Finished Product Certificate Import OR Raw Material Reference-based Import
        LaboratoryFinalConformityCertificate? finalCert = null;
        if (dto.LaboratoryCertificateId.HasValue && dto.LaboratoryCertificateId.Value > 0)
        {
            finalCert = await _db.LaboratoryFinalConformityCertificates
                .Include(c => c.ProductionFinishedProduct)
                    .ThenInclude(f => f.ProductionProcess)
                        .ThenInclude(p => p.Materials)
                .FirstOrDefaultAsync(c => c.Id == dto.LaboratoryCertificateId.Value);
        }
        else if (isFinishedProduct && !string.IsNullOrWhiteSpace(dto.SampleCode))
        {
            var sc = dto.SampleCode.Trim().ToLowerInvariant();
            finalCert = await _db.LaboratoryFinalConformityCertificates
                .Include(c => c.ProductionFinishedProduct)
                    .ThenInclude(f => f.ProductionProcess)
                        .ThenInclude(p => p.Materials)
                .FirstOrDefaultAsync(c => c.CertificateNumber.ToLower() == sc);
        }
        else if (isFinishedProduct && dto.ProductionFinishedProductId.HasValue && dto.ProductionFinishedProductId.Value > 0)
        {
            finalCert = await _db.LaboratoryFinalConformityCertificates
                .Include(c => c.ProductionFinishedProduct)
                    .ThenInclude(f => f.ProductionProcess)
                        .ThenInclude(p => p.Materials)
                .FirstOrDefaultAsync(c => c.ProductionFinishedProductId == dto.ProductionFinishedProductId.Value);
        }

        LaboratoryRawMaterialReport? labReport = null;
        if (finalCert == null)
        {
            if (dto.LaboratoryReportId.HasValue && dto.LaboratoryReportId.Value > 0)
            {
                labReport = await _db.LaboratoryRawMaterialReports
                    .Include(r => r.RawMaterial)
                    .FirstOrDefaultAsync(r => r.Id == dto.LaboratoryReportId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(dto.SampleCode))
            {
                var sc = dto.SampleCode.Trim().ToLowerInvariant();
                labReport = await _db.LaboratoryRawMaterialReports
                    .Include(r => r.RawMaterial)
                    .FirstOrDefaultAsync(r => r.SampleCode != null && r.SampleCode.ToLower() == sc);
            }
            else if (dto.ProductionReferenceNumber.HasValue && dto.ProductionReferenceNumber.Value > 0)
            {
                labReport = await _db.LaboratoryRawMaterialReports
                    .Include(r => r.RawMaterial)
                    .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == dto.ProductionReferenceNumber.Value);
            }
        }

        decimal remainingAvailable = 0m;
        List<BatchDivisionInputDto> resolvedBatches = new();

        if (finalCert != null)
        {
            // STRICT QUALITY GATE:
            // Finished product MUST be APPROVED by BOTH Physical AND Chemical inspections!
            if (finalCert.OverallStatus != "APPROVED" ||
                finalCert.PhysicalApprovalStatus != "APPROVED" ||
                finalCert.ChemicalApprovalStatus != "APPROVED" ||
                finalCert.ApprovalStatus != "APPROVED" ||
                finalCert.ConformityDecision != "CONFORMS")
            {
                return BadRequest(ApiResponse.Fail($"لا يمكن استلام المنتج النهائي ({finalCert.CertificateNumber}) في المستودع: المنتج لم يجتز اعتمادي قسم المعمل (الفيزيائي والكيميائي) بنجاح (الحالة: {finalCert.OverallStatus})."));
            }

            if (finalCert.ActualFinalProductQuantity <= 0)
            {
                return BadRequest(ApiResponse.Fail($"شهادة مطابقة المنتج النهائي ({finalCert.CertificateNumber}) لا تحتوي على كمية صالحة للاستلام."));
            }

            var fpObj = finalCert.ProductionFinishedProduct;
            var mfgNo = finalCert.ManufactureNumber ?? fpObj?.ManufactureNumber ?? string.Empty;

            var alreadyAllocated = await _db.WarehouseBatches
                .Where(b => (b.LaboratoryCertificateId == finalCert.Id ||
                            (b.MaterialClassification == "FINAL_PRODUCT" && !string.IsNullOrEmpty(mfgNo) && b.BatchNumber == mfgNo)) &&
                            b.Status != "REJECTED")
                .SumAsync(b => (decimal?)b.InitialQuantity) ?? 0m;

            remainingAvailable = Math.Round(finalCert.ActualFinalProductQuantity - alreadyAllocated, 3);
            if (remainingAvailable <= 0)
            {
                return BadRequest(ApiResponse.Fail($"المنتج النهائي المعتمد ({finalCert.CertificateNumber}) تم استلام كامل كميته في المستودع مسبقاً ({alreadyAllocated:N3} طن)."));
            }

            if (dto.Batches != null && dto.Batches.Count > 0)
            {
                resolvedBatches = dto.Batches.Where(b => b.Quantity > 0).ToList();
            }
            else if (dto.Items != null && dto.Items.Count > 0)
            {
                resolvedBatches = dto.Items.Where(i => i.Quantity > 0).Select(i => new BatchDivisionInputDto
                {
                    Quantity = i.Quantity,
                    Notes = i.Notes
                }).ToList();
            }
            else
            {
                resolvedBatches = new List<BatchDivisionInputDto>
                {
                    new BatchDivisionInputDto
                    {
                        BatchNumber = !string.IsNullOrWhiteSpace(mfgNo) ? mfgNo : $"FP-{finalCert.CertificateNumber}",
                        Quantity = remainingAvailable
                    }
                };
            }

            if (resolvedBatches.Count == 0)
            {
                return BadRequest(ApiResponse.Fail("يجب تحديد دفعة واحدة على الأقل للاستلام."));
            }

            var sumBatches = Math.Round(resolvedBatches.Sum(b => b.Quantity), 3);
            if (sumBatches > remainingAvailable)
            {
                return BadRequest(ApiResponse.Fail($"إجمالي كميات الدفعات المستلمة ({sumBatches:N3} طن) يتجاوز الرصيد المتبقي المعتمد للاستلام ({remainingAvailable:N3} طن)."));
            }
            if (sumBatches <= 0)
            {
                return BadRequest(ApiResponse.Fail("يجب أن يكون إجمالي كميات الدفعات أكبر من الصفر."));
            }

            materialType = "FINAL_PRODUCT";
            source = !string.IsNullOrWhiteSpace(source) ? source : "قسم الإنتاج والتصنيع";
        }
        else if (labReport != null)
        {
            // Strictly validate compliance status
            if (labReport.ApprovalStatus != "APPROVED")
            {
                return BadRequest(ApiResponse.Fail($"المرجع المعملي ({labReport.SampleCode ?? $"#{labReport.ProductionReferenceNumber}"}) قيد المراجعة ({labReport.ApprovalStatus}) ولا يمكن الاستلام قبل اعتماد قسم التحكم والمراقبة."));
            }
            if (labReport.ComplyStatus != "COMPLY" || labReport.InspectionDecision == "REJECTED")
            {
                return BadRequest(ApiResponse.Fail($"المرجع المعملي ({labReport.SampleCode ?? $"#{labReport.ProductionReferenceNumber}"}) غير مطابق لمواصفات الجودة ({labReport.ComplyStatus} - {labReport.InspectionDecision}) ولا يمكن استلامه في المستودع."));
            }
            if (labReport.AcceptedQuantity <= 0)
            {
                return BadRequest(ApiResponse.Fail($"المرجع المعملي ({labReport.SampleCode ?? $"#{labReport.ProductionReferenceNumber}"}) لا يحتوي على كمية مقبولة للاستلام."));
            }

            var alreadyAllocated = await _db.WarehouseBatches
                .Where(b => b.LaboratoryReportId == labReport.Id && b.Status != "REJECTED")
                .SumAsync(b => (decimal?)b.InitialQuantity) ?? 0m;

            remainingAvailable = Math.Round(labReport.AcceptedQuantity - alreadyAllocated, 3);
            if (remainingAvailable <= 0)
            {
                return BadRequest(ApiResponse.Fail($"المرجع المعملي ({labReport.SampleCode ?? $"#{labReport.ProductionReferenceNumber}"}) تم استلام كامل كميته وتوزيعها على دفعات سابقة ({alreadyAllocated:N3} طن)."));
            }

            // Batches input validation
            if (dto.Batches != null && dto.Batches.Count > 0)
            {
                resolvedBatches = dto.Batches.Where(b => b.Quantity > 0).ToList();
            }
            else if (dto.Items != null && dto.Items.Count > 0)
            {
                resolvedBatches = dto.Items.Where(i => i.Quantity > 0).Select(i => new BatchDivisionInputDto
                {
                    Quantity = i.Quantity,
                    Notes = i.Notes
                }).ToList();
            }
            else
            {
                resolvedBatches = new List<BatchDivisionInputDto>
                {
                    new BatchDivisionInputDto { Quantity = remainingAvailable }
                };
            }

            if (resolvedBatches.Count == 0)
            {
                return BadRequest(ApiResponse.Fail("يجب تحديد دفعة واحدة على الأقل للاستلام."));
            }

            var sumBatches = Math.Round(resolvedBatches.Sum(b => b.Quantity), 3);
            if (sumBatches > remainingAvailable)
            {
                return BadRequest(ApiResponse.Fail($"إجمالي كميات الدفعات ({sumBatches:N3} طن) يتجاوز الرصيد المتبقي المتاح للاستلام ({remainingAvailable:N3} طن)."));
            }
            if (sumBatches <= 0)
            {
                return BadRequest(ApiResponse.Fail("يجب أن يكون إجمالي كميات الدفعات أكبر من الصفر."));
            }

            if (alreadyAllocated + sumBatches >= labReport.AcceptedQuantity)
            {
                labReport.IsStorageReceived = true;
                labReport.StorageReceivedAt = DateTime.UtcNow;
            }

            // Auto-align classification & source if from Lab Report
            if (!string.IsNullOrWhiteSpace(labReport.MaterialClassification))
            {
                materialType = labReport.MaterialClassification.Trim().ToUpperInvariant();
            }
            if (!string.IsNullOrWhiteSpace(labReport.SupplierName))
            {
                source = labReport.SupplierName.Trim();
            }
        }
        else
        {
            // Legacy / Direct Item validation fallback
            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(ApiResponse.Fail("يجب اختيار رقم عينة معتمد من المعمل أو تحديد صنف وارد واحد على الأقل."));
            }
        }

        // 6. Internal Weighing & Discrepancy Validation
        decimal? internalNet = null;
        if (dto.InternalLoadedWeight.HasValue && dto.InternalEmptyWeight.HasValue)
        {
            if (dto.InternalLoadedWeight.Value < 0 || dto.InternalEmptyWeight.Value < 0)
            {
                return BadRequest(ApiResponse.Fail("أوزان الميزان الداخلي لا يمكن أن تكون سالبة."));
            }
            if (dto.InternalLoadedWeight.Value < dto.InternalEmptyWeight.Value)
            {
                return BadRequest(ApiResponse.Fail("الوزن القائم (المحمل) لا يمكن أن يكون أقل من وزن السيارة فارغة."));
            }
            internalNet = Math.Round(dto.InternalLoadedWeight.Value - dto.InternalEmptyWeight.Value, 3);
        }
        else if (dto.InternalNetWeight.HasValue)
        {
            if (dto.InternalNetWeight.Value < 0)
            {
                return BadRequest(ApiResponse.Fail("الوزن الصافي الداخلي لا يمكن أن يكون سالباً."));
            }
            internalNet = Math.Round(dto.InternalNetWeight.Value, 3);
        }

        var externalWeight = dto.ExternalReportedWeight;
        if (externalWeight.HasValue && externalWeight.Value < 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الخارجي المورد لا يمكن أن يكون سالباً."));
        }

        decimal? diff = null;
        decimal? diffPct = null;
        bool isFlagged = false;
        if (externalWeight.HasValue && internalNet.HasValue && externalWeight.Value > 0)
        {
            diff = Math.Round(Math.Abs(internalNet.Value - externalWeight.Value), 3);
            diffPct = Math.Round((diff.Value / externalWeight.Value) * 100m, 2);
            if (diffPct.Value > 0.5m || diff.Value > 0.05m)
            {
                isFlagged = true;
            }
        }

        if (isFlagged && string.IsNullOrWhiteSpace(dto.DiscrepancyExplanation))
        {
            return BadRequest(ApiResponse.Fail($"تم رصد فارق وزن بين الكمية الموردة ({externalWeight:N3} طن) والوزن الصافي الداخلي ({internalNet:N3} طن) بمقدار {diff:N3} طن ({diffPct:N2}%) يتجاوز نسبة التفاوت المسموحة (0.5%). يرجى إدخال سبب الفارق في الميزان الداخلي للمتابعة."));
        }

        // 7. Validate Attachments (Optional, saved if uploaded)
        var weighbridgeRes = await ValidateAndSaveAttachmentAsync(dto.WeighbridgeAttachment, dto.WeighbridgeAttachmentPath, "مرفق الميزان", isRequired: false);
        if (!weighbridgeRes.Success)
        {
            return BadRequest(ApiResponse.Fail(weighbridgeRes.ErrorMessage ?? "مرفق الميزان غير صالح."));
        }

        var qocRes = await ValidateAndSaveAttachmentAsync(dto.QocAttachment, dto.QocAttachmentPath, "تقرير QOC", isRequired: false);
        if (!qocRes.Success)
        {
            CleanupSavedFiles(new[] { weighbridgeRes.StoredFileName });
            return BadRequest(ApiResponse.Fail(qocRes.ErrorMessage ?? "تقرير QOC غير صالح."));
        }

        var docRes = await ValidateAndSaveAttachmentAsync(dto.DocumentAttachment, dto.DocumentAttachmentPath, "المستند الورقي", isRequired: false);
        if (!docRes.Success)
        {
            CleanupSavedFiles(new[] { weighbridgeRes.StoredFileName, qocRes.StoredFileName });
            return BadRequest(ApiResponse.Fail(docRes.ErrorMessage ?? "المستند الورقي غير صالح."));
        }

        var txDate = dto.TransactionDate ?? DateTime.UtcNow;

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var txNumber = await GetNextSequenceNumberAsync("IMPORT");

            var op = new StorageOperation
            {
                OperationType = "IMPORT",
                TransactionNumber = txNumber,
                TransactionDate = txDate,
                StorageLocationId = location.Id,
                StorageLocationName = location.Name,
                MaterialType = materialType,
                Source = source,
                SourcePhone = phone,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                Notes = dto.Notes,
                ApprovalStatus = "PENDING",
                IsInventoryPosted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                CreatedByUserId = userId,

                // Internal Weighing
                ExternalReportedWeight = externalWeight,
                InternalLoadedWeight = dto.InternalLoadedWeight,
                InternalEmptyWeight = dto.InternalEmptyWeight,
                InternalNetWeight = internalNet,
                InternalWeighingTime = dto.InternalWeighingTime ?? DateTime.UtcNow,
                WeighbridgeReference = (dto.WeighbridgeReference ?? string.Empty).Trim(),
                WeighingResponsibleEmployee = (dto.WeighingResponsibleEmployee ?? userName).Trim(),
                WeightDifference = diff,
                WeightDifferencePercentage = diffPct,
                IsDiscrepancyFlagged = isFlagged,
                DiscrepancyExplanation = dto.DiscrepancyExplanation?.Trim(),
                TransportationId = dto.TransportationId,
                ProductionReferenceNumber = finalCert != null ? (finalCert.ProductionReferenceNumber > 0 ? finalCert.ProductionReferenceNumber : (finalCert.ProductionFinishedProduct?.ProductionOrderId ?? 0)) : (labReport != null ? labReport.ProductionReferenceNumber : (dto.ProductionReferenceNumber ?? 0)),
                SampleCode = finalCert != null ? finalCert.CertificateNumber : (labReport != null ? (labReport.SampleCode ?? $"SMP-{labReport.SampleNumber:D4}") : dto.SampleCode),
                ProductionOrderId = finalCert != null ? (finalCert.ProductionOrderId ?? finalCert.ProductionFinishedProduct?.ProductionOrderId) : null,
                ProductionProcessId = finalCert != null ? (finalCert.ProductionProcessId ?? finalCert.ProductionFinishedProduct?.ProductionProcessId) : null,
                ManufactureNumber = finalCert != null ? (finalCert.ManufactureNumber ?? finalCert.ProductionFinishedProduct?.ManufactureNumber) : null,

                // Attachments
                WeighbridgeAttachmentPath = !string.IsNullOrWhiteSpace(weighbridgeRes.StoredFileName) ? $"/uploads/storage/{weighbridgeRes.StoredFileName}" : null,
                WeighbridgeFileName = weighbridgeRes.OriginalFileName,
                WeighbridgeFileType = weighbridgeRes.ContentType,
                WeighbridgeFileSize = weighbridgeRes.FileSize,

                QocAttachmentPath = !string.IsNullOrWhiteSpace(qocRes.StoredFileName) ? $"/uploads/storage/{qocRes.StoredFileName}" : null,
                QocFileName = qocRes.OriginalFileName,
                QocFileType = qocRes.ContentType,
                QocFileSize = qocRes.FileSize,

                DocumentAttachmentPath = !string.IsNullOrWhiteSpace(docRes.StoredFileName) ? $"/uploads/storage/{docRes.StoredFileName}" : null,
                DocumentFileName = docRes.OriginalFileName,
                DocumentFileType = docRes.ContentType,
                DocumentFileSize = docRes.FileSize
            };

            await _db.StorageOperations.AddAsync(op);
            await _db.SaveChangesAsync();

            var createdBatches = new List<WarehouseBatch>();
            var createdItems = new List<StorageOperationItem>();

            if (finalCert != null && resolvedBatches.Count > 0)
            {
                var fpName = (!string.IsNullOrWhiteSpace(finalCert.FinalProductName) ? finalCert.FinalProductName : "منتج نهائي").Trim();
                var rawMat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name.ToLower() == fpName.ToLower());
                if (rawMat == null)
                {
                    rawMat = new RawMaterial
                    {
                        Name = fpName,
                        MaterialType = MaterialProcessType.FinalProduct,
                        InitialQuantity = 0m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName,
                        CreatedByUserId = userId
                    };
                    await _db.RawMaterials.AddAsync(rawMat);
                    await _db.SaveChangesAsync();
                }

                var fpObj = finalCert.ProductionFinishedProduct;
                var mfgNo = finalCert.ManufactureNumber ?? fpObj?.ManufactureNumber ?? string.Empty;
                var batchNumbers = await ReserveBatchNumbersAsync(resolvedBatches.Count);

                int? fallbackLabReportId = null;
                if (fpObj?.ProductionProcess?.Materials != null)
                {
                    fallbackLabReportId = fpObj.ProductionProcess.Materials
                        .Where(m => m.LaboratoryReportId.HasValue && m.LaboratoryReportId.Value > 0)
                        .Select(m => m.LaboratoryReportId)
                        .FirstOrDefault();
                }
                if (!fallbackLabReportId.HasValue || fallbackLabReportId.Value == 0)
                {
                    fallbackLabReportId = await _db.LaboratoryRawMaterialReports.OrderByDescending(r => r.Id).Select(r => (int?)r.Id).FirstOrDefaultAsync();
                }

                for (int idx = 0; idx < resolvedBatches.Count; idx++)
                {
                    var bIn = resolvedBatches[idx];
                    var preferredNum = !string.IsNullOrWhiteSpace(bIn.BatchNumber) && !await _db.WarehouseBatches.AnyAsync(b => b.BatchNumber == bIn.BatchNumber.Trim())
                        ? bIn.BatchNumber.Trim()
                        : (idx == 0 && !string.IsNullOrWhiteSpace(mfgNo) && !await _db.WarehouseBatches.AnyAsync(b => b.BatchNumber == mfgNo.Trim())
                            ? mfgNo.Trim()
                            : batchNumbers[idx]);

                    var batch = new WarehouseBatch
                    {
                        LaboratoryReportId = fallbackLabReportId,
                        LaboratoryCertificateId = finalCert.Id,
                        ProductionFinishedProductId = finalCert.ProductionFinishedProductId,
                        ProductionReferenceNumber = finalCert.ProductionReferenceNumber > 0 ? finalCert.ProductionReferenceNumber : (fpObj?.ProductionOrderId ?? 0),
                        SampleCode = finalCert.CertificateNumber,
                        BatchNumber = preferredNum,
                        RawMaterialId = rawMat.Id,
                        MaterialName = fpName,
                        MaterialClassification = "FINAL_PRODUCT",
                        SupplierName = source,
                        StorageLocationId = location.Id,
                        InitialQuantity = Math.Round(bIn.Quantity, 3),
                        CurrentQuantity = Math.Round(bIn.Quantity, 3),
                        StorageOperationId = op.Id,
                        ImportDate = txDate,
                        Status = "APPROVED",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName,
                        CreatedByUserId = userId,
                        Notes = bIn.Notes ?? $"استلام منتج نهائي معتمد مخبرياً - شهادة {finalCert.CertificateNumber}"
                    };
                    await _db.WarehouseBatches.AddAsync(batch);
                    await _db.SaveChangesAsync();

                    var opItem = new StorageOperationItem
                    {
                        StorageOperationId = op.Id,
                        RawMaterialId = rawMat.Id,
                        WarehouseBatchId = batch.Id,
                        BatchNumber = preferredNum,
                        SampleCode = batch.SampleCode,
                        ProductionReferenceNumber = batch.ProductionReferenceNumber,
                        MaterialName = fpName,
                        Quantity = Math.Round(bIn.Quantity, 3),
                        TrackingNumber = $"IMP-{txDate:yyyy}-{txNumber:D4}-FP{idx + 1}",
                        ProductionOrderId = finalCert.ProductionOrderId ?? fpObj?.ProductionOrderId,
                        ProductionProcessId = finalCert.ProductionProcessId ?? fpObj?.ProductionProcessId,
                        ManufactureNumber = preferredNum,
                        Notes = bIn.Notes
                    };
                    await _db.StorageOperationItems.AddAsync(opItem);
                    await _db.SaveChangesAsync();

                    batch.StorageOperationItemId = opItem.Id;
                    createdBatches.Add(batch);
                    createdItems.Add(opItem);
                }

                finalCert.IsStoragePosted = true;
                finalCert.StoragePostedAt = DateTime.UtcNow;

                if (finalCert.ProductionFinishedProductId.HasValue)
                {
                    var fpRecord = await _db.ProductionFinishedProducts.FirstOrDefaultAsync(f => f.Id == finalCert.ProductionFinishedProductId.Value);
                    if (fpRecord != null)
                    {
                        fpRecord.IsStorageReceived = true;
                        fpRecord.WarehouseImportOperationId = op.Id;
                        fpRecord.StorageReceivedDate = DateTime.UtcNow;
                    }
                }
            }
            else if (labReport != null && resolvedBatches.Count > 0)
            {
                var sampleName = (!string.IsNullOrWhiteSpace(labReport.SampleName) ? labReport.SampleName : labReport.RawMaterialDescription).Trim();
                var rawMat = labReport.RawMaterial;
                if (rawMat == null)
                {
                    rawMat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name.ToLower() == sampleName.ToLower());
                    if (rawMat == null)
                    {
                        rawMat = new RawMaterial
                        {
                            Name = sampleName,
                            MaterialType = materialType == "CHEMICAL" ? MaterialProcessType.Chemical : MaterialProcessType.Mechanical,
                            InitialQuantity = 0m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = userName,
                            CreatedByUserId = userId
                        };
                        await _db.RawMaterials.AddAsync(rawMat);
                        await _db.SaveChangesAsync();
                    }
                }

                var batchNumbers = await ReserveBatchNumbersAsync(resolvedBatches.Count);

                for (int idx = 0; idx < resolvedBatches.Count; idx++)
                {
                    var bIn = resolvedBatches[idx];
                    var batchNum = !string.IsNullOrWhiteSpace(bIn.BatchNumber) && !await _db.WarehouseBatches.AnyAsync(b => b.BatchNumber == bIn.BatchNumber.Trim())
                        ? bIn.BatchNumber.Trim()
                        : batchNumbers[idx];

                    var batch = new WarehouseBatch
                    {
                        LaboratoryReportId = labReport.Id,
                        ProductionReferenceNumber = labReport.ProductionReferenceNumber,
                        SampleCode = labReport.SampleCode ?? $"SMP-{labReport.SampleNumber:D4}",
                        BatchNumber = batchNum,
                        RawMaterialId = rawMat.Id,
                        MaterialName = sampleName,
                        MaterialClassification = materialType,
                        SupplierName = source,
                        StorageLocationId = location.Id,
                        InitialQuantity = Math.Round(bIn.Quantity, 3),
                        CurrentQuantity = Math.Round(bIn.Quantity, 3),
                        StorageOperationId = op.Id,
                        ImportDate = txDate,
                        Status = "APPROVED",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName,
                        CreatedByUserId = userId,
                        Notes = bIn.Notes
                    };
                    await _db.WarehouseBatches.AddAsync(batch);
                    await _db.SaveChangesAsync();

                    var opItem = new StorageOperationItem
                    {
                        StorageOperationId = op.Id,
                        RawMaterialId = rawMat.Id,
                        WarehouseBatchId = batch.Id,
                        BatchNumber = batchNum,
                        SampleCode = batch.SampleCode,
                        ProductionReferenceNumber = labReport.ProductionReferenceNumber,
                        MaterialName = sampleName,
                        Quantity = Math.Round(bIn.Quantity, 3),
                        TrackingNumber = $"IMP-{txDate:yyyy}-{txNumber:D4}-B{idx + 1}",
                        Notes = bIn.Notes
                    };
                    await _db.StorageOperationItems.AddAsync(opItem);
                    await _db.SaveChangesAsync();

                    batch.StorageOperationItemId = opItem.Id;
                    createdBatches.Add(batch);
                    createdItems.Add(opItem);
                }
            }
            else
            {
                // Fallback for direct manual items
                int lineIndex = 1;
                foreach (var item in (dto.Items ?? new List<StorageOperationLineInputDto>()))
                {
                    if (item.Quantity <= 0) continue;
                    RawMaterial? mat = null;
                    if (item.RawMaterialId.HasValue && item.RawMaterialId.Value > 0)
                    {
                        mat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Id == item.RawMaterialId.Value);
                    }
                    else if (!string.IsNullOrWhiteSpace(item.NewMaterialName))
                    {
                        var cleanName = item.NewMaterialName.Trim();
                        mat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name.ToLower() == cleanName.ToLower());
                        if (mat == null)
                        {
                            mat = new RawMaterial
                            {
                                Name = cleanName,
                                MaterialType = materialType,
                                InitialQuantity = 0m,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = userName,
                                CreatedByUserId = userId
                            };
                            await _db.RawMaterials.AddAsync(mat);
                            await _db.SaveChangesAsync();
                        }
                    }

                    if (mat == null) continue;

                    var opItem = new StorageOperationItem
                    {
                        StorageOperationId = op.Id,
                        RawMaterialId = mat.Id,
                        MaterialName = mat.Name,
                        Quantity = Math.Round(item.Quantity, 3),
                        TrackingNumber = $"IMP-{txDate:yyyy}-{txNumber:D4}-L{lineIndex}",
                        Notes = item.Notes
                    };
                    await _db.StorageOperationItems.AddAsync(opItem);
                    createdItems.Add(opItem);
                    lineIndex++;
                }
            }

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            // Auto-Approve: Automatically approve and post inventory ledger immediately
            var (appOk, _, appDto) = await ApproveStorageOperationCoreAsync(_db, _logger, _hubContext, op.Id, "System (Auto-Approved)", userId, "SYSTEM");
            if (appOk && appDto != null)
            {
                op.ApprovalStatus = "APPROVED";
                op.IsInventoryPosted = true;
            }

            _logger.LogInformation("Import Storage Operation #{TxNumber} (ID: {Id}) with {BatchesCount} batches auto-approved successfully.",
                op.TransactionNumber, op.Id, createdBatches.Count);

            op.CreatedBatches = createdBatches;
            var resultDto = MapOperationToDto(op, createdItems);

            // SignalR notification
            var msg = isFlagged 
                ? $"تم تسجيل إذن إضافة مواد جديد (#{op.TransactionNumber}) مع وجود تنبيه فارق وزن بالميزان الداخلي قيد الاعتماد."
                : $"تم تسجيل إذن إضافة مواد جديد (#{op.TransactionNumber}) قيد الاعتماد.";
            await NotifyOperationChangeAsync("IMPORT", op.Id, $"#{op.TransactionNumber}", "PENDING", userName, msg);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<StorageOperationDto>.Ok(resultDto, $"تم تسجيل إذن إضافة المواد بنجاح برقم عملية #{op.TransactionNumber}."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(new[] { weighbridgeRes.StoredFileName, qocRes.StoredFileName, docRes.StoredFileName });
            _logger.LogError(ex, "Failed to submit Import Storage Operation.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ إذن الإضافة."));
        }
    }

    /// <summary>
    /// Submit a new Export Transaction (إذن صرف بضاعة من المخزن).
    /// Requires 2 mandatory attachments: Weighbridge and Written Document.
    /// Strictly validates that materials belong to selected storage location and have sufficient available stock.
    /// </summary>
    [HttpPost("export")]
    [ProducesResponseType(typeof(ApiResponse<StorageOperationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateExport()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم المخازن."));
        }

        var dto = await ParseExportRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات إذن الصرف غير صالحة."));
        }

        var isFinishedProductExport = string.Equals(dto.ExportType, "FINISHED_PRODUCT", StringComparison.OrdinalIgnoreCase);

        // 1. Validate Location
        var location = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.StorageLocationId && l.IsActive);
        if (location == null)
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين المحدد غير موجود أو غير مفعل."));
        }

        // 2. Validate Client
        var client = await _db.StorageClients.FirstOrDefaultAsync(c => c.Id == dto.ClientId && c.IsActive);
        if (client == null)
        {
            return BadRequest(ApiResponse.Fail("العميل المحدد غير موجود أو غير مفعل."));
        }

        // 3. Validate Destination
        var destination = await _db.StorageClientDestinations
            .FirstOrDefaultAsync(d => d.Id == dto.DestinationId && d.StorageClientId == client.Id && d.IsActive);
        if (destination == null)
        {
            return BadRequest(ApiResponse.Fail("الوجهة المحددة غير صالحة أو لا تنتمي للعميل المختار."));
        }

        // 4. Validate Phone Number (Optional, but validate format if entered)
        var phone = (dto.ClientPhone ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(phone) && !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^(\+?[0-9\s\-()]{7,25})$"))
        {
            return BadRequest(ApiResponse.Fail("رقم هاتف العميل / المسؤول غير صالح. يرجى إدخال رقم هاتف صحيح أو تركه فارغاً."));
        }

        // 5. Validate Items & Real-time Location Stock / Batch Stock
        if (dto.Items == null || dto.Items.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب تحديد صنف أو دفعة واحدة على الأقل للصرف."));
        }

        var isByReference = !isFinishedProductExport && string.Equals(dto.SelectionMode, "BY_REFERENCE", StringComparison.OrdinalIgnoreCase);
        if (isByReference)
        {
            if (!dto.LaboratoryReportId.HasValue && string.IsNullOrWhiteSpace(dto.SampleCode) && !dto.ProductionReferenceNumber.HasValue)
            {
                return BadRequest(ApiResponse.Fail("في وضع الصرف برقم العينة، يجب اختيار رقم العينة المرجعية."));
            }
        }

        var resolvedItems = new List<(RawMaterial Material, decimal Quantity, string? Notes, WarehouseBatch? Batch)>();
        foreach (var item in dto.Items)
        {
            if (item.Quantity <= 0)
            {
                return BadRequest(ApiResponse.Fail("يجب أن تكون كمية الصرف أكبر من الصفر."));
            }

            WarehouseBatch? batch = null;
            if (item.WarehouseBatchId.HasValue && item.WarehouseBatchId.Value > 0)
            {
                batch = await _db.WarehouseBatches.FirstOrDefaultAsync(b => b.Id == item.WarehouseBatchId.Value && b.IsActive);
                if (batch == null)
                {
                    return BadRequest(ApiResponse.Fail($"الدفعة رقم #{item.WarehouseBatchId} غير موجودة أو غير نشطة."));
                }
            }
            else if (!string.IsNullOrWhiteSpace(item.BatchNumber))
            {
                batch = await _db.WarehouseBatches.FirstOrDefaultAsync(b => b.BatchNumber == item.BatchNumber && b.StorageLocationId == location.Id && b.IsActive);
                if (batch == null)
                {
                    return BadRequest(ApiResponse.Fail($"الدفعة رقم '{item.BatchNumber}' غير موجودة في هذا المستودع أو غير نشطة."));
                }
            }

            if (batch != null)
            {
                if (batch.StorageLocationId != location.Id)
                {
                    return BadRequest(ApiResponse.Fail($"الدفعة '{batch.BatchNumber}' غير موجودة في مكان التخزين المختار ({location.Name})."));
                }

                if (batch.Status != "APPROVED")
                {
                    return BadRequest(ApiResponse.Fail($"الدفعة '{batch.BatchNumber}' غير معتمدة للصرف (حالتها الحالية: {batch.Status})."));
                }

                if (isByReference)
                {
                    if (dto.LaboratoryReportId.HasValue && batch.LaboratoryReportId != dto.LaboratoryReportId.Value)
                    {
                        return BadRequest(ApiResponse.Fail($"الدفعة '{batch.BatchNumber}' لا تتبع رقم التقرير المعملي المختار."));
                    }
                    if (!string.IsNullOrWhiteSpace(dto.SampleCode) && !string.Equals(batch.SampleCode, dto.SampleCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(ApiResponse.Fail($"الدفعة '{batch.BatchNumber}' تتبع رقم العينة '{batch.SampleCode}' ولا تتبع رقم العينة المختار ({dto.SampleCode})."));
                    }
                }

                if (item.Quantity > batch.CurrentQuantity)
                {
                    return BadRequest(ApiResponse.Fail($"الكمية المطلوبة من الدفعة '{batch.BatchNumber}' ({item.Quantity:N3} طن) تتجاوز الرصيد المتاح للدفعة ({batch.CurrentQuantity:N3} طن)."));
                }
            }

            // Resolve material
            RawMaterial? mat = null;
            var targetMatId = item.RawMaterialId ?? batch?.RawMaterialId;
            if (targetMatId.HasValue && targetMatId.Value > 0)
            {
                mat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Id == targetMatId.Value);
            }
            else if (batch != null && !string.IsNullOrWhiteSpace(batch.MaterialName))
            {
                mat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name.ToLower() == batch.MaterialName.ToLower());
            }

            if (mat == null)
            {
                return BadRequest(ApiResponse.Fail("معرف الصنف الصادر مطلوب أو غير موجود."));
            }

            // Real-time server-side stock validation strictly for this location
            var ledgerStock = await GetAvailableStockAsync(location.Id, mat.Id);
            var batchStock = batch != null ? batch.CurrentQuantity : 0m;
            var availableStock = Math.Max(ledgerStock, batchStock);
            if (availableStock < item.Quantity)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المطلوبة من الصنف '{mat.Name}' ({item.Quantity:N3} طن) تتجاوز الرصيد المتاح حالياً في {location.Name} ({availableStock:N3} طن)."));
            }

            // Enforce Material Type compatibility if specified (only for raw materials, finished products are always allowed)
            var expMatType = (dto.MaterialType ?? string.Empty).Trim().ToUpperInvariant();
            if (!isFinishedProductExport && !string.IsNullOrWhiteSpace(expMatType) && !string.IsNullOrWhiteSpace(mat.MaterialType) && mat.MaterialType != expMatType)
            {
                var expectedAr = expMatType == MaterialProcessType.Mechanical ? "خامات ميكانيكية" : "خامات كيميائية";
                var actualAr = mat.MaterialType == MaterialProcessType.Mechanical ? "خامات ميكانيكية" : "خامات كيميائية";
                return BadRequest(ApiResponse.Fail($"الصنف '{mat.Name}' يتبع ({actualAr}) ولا يمكن صرفه ضمن إذن صرف ({expectedAr})."));
            }

            resolvedItems.Add((mat, item.Quantity, item.Notes, batch));
        }

        // 6. Internal Weighing & Discrepancy Validation
        decimal? internalNet = null;
        if (dto.InternalLoadedWeight.HasValue && dto.InternalEmptyWeight.HasValue)
        {
            if (dto.InternalLoadedWeight.Value < 0 || dto.InternalEmptyWeight.Value < 0)
            {
                return BadRequest(ApiResponse.Fail("أوزان الميزان الداخلي لا يمكن أن تكون سالبة."));
            }
            if (dto.InternalLoadedWeight.Value < dto.InternalEmptyWeight.Value)
            {
                return BadRequest(ApiResponse.Fail("الوزن القائم (المحمل) لا يمكن أن يكون أقل من وزن السيارة فارغة."));
            }
            internalNet = Math.Round(dto.InternalLoadedWeight.Value - dto.InternalEmptyWeight.Value, 3);
        }
        else if (dto.InternalNetWeight.HasValue)
        {
            if (dto.InternalNetWeight.Value < 0)
            {
                return BadRequest(ApiResponse.Fail("الوزن الصافي الداخلي لا يمكن أن يكون سالباً."));
            }
            internalNet = Math.Round(dto.InternalNetWeight.Value, 3);
        }

        var externalWeight = dto.ExternalReportedWeight;
        if (externalWeight.HasValue && externalWeight.Value < 0)
        {
            return BadRequest(ApiResponse.Fail("الوزن الصادر المطلوب لا يمكن أن يكون سالباً."));
        }

        decimal? diff = null;
        decimal? diffPct = null;
        bool isFlagged = false;
        if (externalWeight.HasValue && internalNet.HasValue && externalWeight.Value > 0)
        {
            diff = Math.Round(Math.Abs(internalNet.Value - externalWeight.Value), 3);
            diffPct = Math.Round((diff.Value / externalWeight.Value) * 100m, 2);
            // Factory standard tolerance: 0.5% or > 0.05 tons
            if (diffPct.Value > 0.5m || diff.Value > 0.05m)
            {
                isFlagged = true;
            }
        }

        if (isFlagged && string.IsNullOrWhiteSpace(dto.DiscrepancyExplanation))
        {
            return BadRequest(ApiResponse.Fail($"تم رصد فارق وزن بين الكمية المصرح بها ({externalWeight:N3} طن) والوزن الصافي بالميزان الداخلي ({internalNet:N3} طن) بمقدار {diff:N3} طن ({diffPct:N2}%) يتجاوز نسبة التفاوت المسموحة (0.5%). يرجى إدخال سبب الفارق في الميزان الداخلي للمتابعة."));
        }

        // 7. Validate Attachments (Optional, saved if uploaded)
        var weighbridgeRes = await ValidateAndSaveAttachmentAsync(dto.WeighbridgeAttachment, dto.WeighbridgeAttachmentPath, "مرفق الميزان", isRequired: false);
        if (!weighbridgeRes.Success)
        {
            return BadRequest(ApiResponse.Fail(weighbridgeRes.ErrorMessage ?? "مرفق الميزان غير صالح."));
        }

        var docRes = await ValidateAndSaveAttachmentAsync(dto.DocumentAttachment, dto.DocumentAttachmentPath, "المستند الورقي", isRequired: false);
        if (!docRes.Success)
        {
            CleanupSavedFiles(new[] { weighbridgeRes.StoredFileName });
            return BadRequest(ApiResponse.Fail(docRes.ErrorMessage ?? "المستند الورقي غير صالح."));
        }

        var txDate = dto.TransactionDate ?? DateTime.UtcNow;

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var txNumber = await GetNextSequenceNumberAsync("EXPORT");

            var expMatType = (dto.MaterialType ?? string.Empty).Trim().ToUpperInvariant();
            var effectiveMaterialType = !string.IsNullOrWhiteSpace(expMatType)
                ? expMatType
                : (resolvedItems.Select(x => x.Material.MaterialType).Distinct().Count() == 1 ? resolvedItems.First().Material.MaterialType : null);

            var op = new StorageOperation
            {
                OperationType = "EXPORT",
                TransactionNumber = txNumber,
                TransactionDate = txDate,
                StorageLocationId = location.Id,
                StorageLocationName = location.Name,
                MaterialType = effectiveMaterialType,
                ClientId = client.Id,
                ClientName = client.Name,
                DestinationId = destination.Id,
                DestinationName = destination.DestinationName,
                ClientPhone = phone,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                Notes = dto.Notes,
                ExportType = isFinishedProductExport ? "FINISHED_PRODUCT" : "RAW_MATERIAL",
                ProductionOrderId = dto.ProductionOrderId ?? resolvedItems.FirstOrDefault(x => x.Batch?.ProductionOrderId > 0).Batch?.ProductionOrderId,
                ProductionProcessId = dto.ProductionProcessId ?? resolvedItems.FirstOrDefault(x => x.Batch?.ProductionProcessId > 0).Batch?.ProductionProcessId,
                ManufactureNumber = !string.IsNullOrWhiteSpace(dto.ManufactureNumber) ? dto.ManufactureNumber : resolvedItems.FirstOrDefault(x => !string.IsNullOrEmpty(x.Batch?.BatchNumber) && isFinishedProductExport).Batch?.BatchNumber,
                SelectionMode = dto.SelectionMode ?? "BY_REFERENCE",
                SampleCode = dto.SampleCode ?? resolvedItems.FirstOrDefault(x => !string.IsNullOrEmpty(x.Batch?.SampleCode)).Batch?.SampleCode,
                ProductionReferenceNumber = dto.ProductionReferenceNumber ?? resolvedItems.FirstOrDefault(x => x.Batch?.ProductionReferenceNumber > 0).Batch?.ProductionReferenceNumber,
                ApprovalStatus = "PENDING",
                IsInventoryPosted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                CreatedByUserId = userId,

                // Internal Weighing
                ExternalReportedWeight = externalWeight,
                InternalLoadedWeight = dto.InternalLoadedWeight,
                InternalEmptyWeight = dto.InternalEmptyWeight,
                InternalNetWeight = internalNet,
                InternalWeighingTime = dto.InternalWeighingTime ?? DateTime.UtcNow,
                WeighbridgeReference = (dto.WeighbridgeReference ?? string.Empty).Trim(),
                WeighingResponsibleEmployee = (dto.WeighingResponsibleEmployee ?? userName).Trim(),
                WeightDifference = diff,
                WeightDifferencePercentage = diffPct,
                IsDiscrepancyFlagged = isFlagged,
                DiscrepancyExplanation = dto.DiscrepancyExplanation?.Trim(),
                TransportationId = dto.TransportationId,

                // Attachments
                WeighbridgeAttachmentPath = !string.IsNullOrWhiteSpace(weighbridgeRes.StoredFileName) ? $"/uploads/storage/{weighbridgeRes.StoredFileName}" : null,
                WeighbridgeFileName = weighbridgeRes.OriginalFileName,
                WeighbridgeFileType = weighbridgeRes.ContentType,
                WeighbridgeFileSize = weighbridgeRes.FileSize,

                DocumentAttachmentPath = !string.IsNullOrWhiteSpace(docRes.StoredFileName) ? $"/uploads/storage/{docRes.StoredFileName}" : null,
                DocumentFileName = docRes.OriginalFileName,
                DocumentFileType = docRes.ContentType,
                DocumentFileSize = docRes.FileSize
            };

            await _db.StorageOperations.AddAsync(op);
            await _db.SaveChangesAsync();

            int lineIndex = 1;
            foreach (var (mat, qty, notes, batch) in resolvedItems)
            {
                var tracking = $"EXP-{txDate:yyyy}-{txNumber:D4}-L{lineIndex}";
                var opItem = new StorageOperationItem
                {
                    StorageOperationId = op.Id,
                    RawMaterialId = mat.Id,
                    MaterialName = mat.Name,
                    Quantity = qty,
                    TrackingNumber = tracking,
                    Notes = notes,
                    WarehouseBatchId = batch?.Id,
                    BatchNumber = batch?.BatchNumber,
                    SampleCode = batch?.SampleCode ?? dto.SampleCode,
                    ProductionReferenceNumber = batch?.ProductionReferenceNumber ?? dto.ProductionReferenceNumber,
                    ProductionOrderId = dto.ProductionOrderId ?? batch?.ProductionOrderId,
                    ProductionProcessId = dto.ProductionProcessId ?? batch?.ProductionProcessId,
                    ManufactureNumber = batch?.BatchNumber
                };
                await _db.StorageOperationItems.AddAsync(opItem);
                lineIndex++;
            }

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            // Auto-Approve: Automatically approve and post inventory ledger immediately
            var (expOk, _, expDto) = await ApproveStorageOperationCoreAsync(_db, _logger, _hubContext, op.Id, "System (Auto-Approved)", userId, "SYSTEM");
            if (expOk && expDto != null)
            {
                op.ApprovalStatus = "APPROVED";
                op.IsInventoryPosted = true;
            }

            _logger.LogInformation("Export Storage Operation #{TxNumber} (ID: {Id}) auto-approved successfully.",
                op.TransactionNumber, op.Id);

            var resultDto = MapOperationToDto(op, await _db.StorageOperationItems.Where(i => i.StorageOperationId == op.Id).ToListAsync());

            // SignalR notification
            var msg = isFlagged
                ? $"تم تسجيل إذن صرف بضاعة جديد (#{op.TransactionNumber}) مع وجود تنبيه فارق وزن بالميزان الداخلي قيد الاعتماد."
                : $"تم تسجيل إذن صرف بضاعة جديد (#{op.TransactionNumber}) قيد الاعتماد.";
            await NotifyOperationChangeAsync("EXPORT", op.Id, $"#{op.TransactionNumber}", "PENDING", userName, msg);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<StorageOperationDto>.Ok(resultDto, $"تم تسجيل إذن صرف البضاعة بنجاح برقم عملية #{op.TransactionNumber}."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(new[] { weighbridgeRes.StoredFileName, docRes.StoredFileName });
            _logger.LogError(ex, "Failed to submit Export Storage Operation.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ إذن الصرف."));
        }
    }

    /// <summary>
    /// Submit a new Production Transfer Transaction (تحويل مواد من المخزن إلى الإنتاج).
    /// Requires 2 mandatory attachments: Written Document and Weighbridge.
    /// Strictly validates that materials belong to source storage and have sufficient available stock.
    /// </summary>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(ApiResponse<StorageOperationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTransfer()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم المخازن."));
        }

        var dto = await ParseTransferRequestAsync();
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات إذن التحويل غير صالحة."));
        }

        // 1. Validate Location
        var location = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.StorageLocationId && l.IsActive);
        if (location == null)
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين المصدر غير موجود أو غير مفعل."));
        }

        // 2. Validate Items & Real-time Location Stock / Batch Stock
        if (dto.Items == null || dto.Items.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب تحديد مادة أو دفعة واحدة على الأقل للتحويل إلى الإنتاج."));
        }

        var isByReference = string.Equals(dto.SelectionMode, "BY_REFERENCE", StringComparison.OrdinalIgnoreCase);
        if (isByReference)
        {
            if (!dto.LaboratoryReportId.HasValue && string.IsNullOrWhiteSpace(dto.SampleCode) && !dto.ProductionReferenceNumber.HasValue)
            {
                return BadRequest(ApiResponse.Fail("في وضع التحويل برقم العينة، يجب اختيار رقم العينة المرجعية."));
            }
        }

        // 2.5 Validate Laboratory Production Reference (shared traceability key for the production batch)
        if (dto.ProductionReferenceNumber.HasValue && dto.ProductionReferenceNumber.Value > 0)
        {
            var labReportExists = await _db.LaboratoryRawMaterialReports
                .AnyAsync(r => r.ProductionReferenceNumber == dto.ProductionReferenceNumber.Value);
            if (!labReportExists)
            {
                return BadRequest(ApiResponse.Fail($"الرقم المرجعي التتبعي #{dto.ProductionReferenceNumber} غير مسجل بقسم المعمل. يرجى اختيار رقم مرجعي صحيح."));
            }
        }

        var resolvedItems = new List<(RawMaterial Material, decimal Quantity, string? Notes, WarehouseBatch? Batch)>();
        foreach (var item in dto.Items)
        {
            if (item.Quantity <= 0)
            {
                return BadRequest(ApiResponse.Fail("يجب أن تكون كمية التحويل أكبر من الصفر."));
            }

            WarehouseBatch? batch = null;
            if (item.WarehouseBatchId.HasValue && item.WarehouseBatchId.Value > 0)
            {
                batch = await _db.WarehouseBatches.FirstOrDefaultAsync(b => b.Id == item.WarehouseBatchId.Value && b.IsActive);
                if (batch == null)
                {
                    return BadRequest(ApiResponse.Fail($"الدفعة رقم #{item.WarehouseBatchId} غير موجودة أو غير نشطة."));
                }
            }
            else if (!string.IsNullOrWhiteSpace(item.BatchNumber))
            {
                batch = await _db.WarehouseBatches.FirstOrDefaultAsync(b => b.BatchNumber == item.BatchNumber && b.StorageLocationId == location.Id && b.IsActive);
                if (batch == null)
                {
                    return BadRequest(ApiResponse.Fail($"الدفعة رقم '{item.BatchNumber}' غير موجودة في هذا المستودع أو غير نشطة."));
                }
            }

            if (batch != null)
            {
                if (batch.StorageLocationId != location.Id)
                {
                    return BadRequest(ApiResponse.Fail($"الدفعة '{batch.BatchNumber}' غير موجودة في مكان التخزين المصدر ({location.Name})."));
                }

                if (batch.Status != "APPROVED")
                {
                    return BadRequest(ApiResponse.Fail($"الدفعة '{batch.BatchNumber}' غير معتمدة للتحويل (حالتها الحالية: {batch.Status})."));
                }

                if (isByReference)
                {
                    if (dto.LaboratoryReportId.HasValue && batch.LaboratoryReportId != dto.LaboratoryReportId.Value)
                    {
                        return BadRequest(ApiResponse.Fail($"الدفعة '{batch.BatchNumber}' لا تتبع رقم التقرير المعملي المختار."));
                    }
                    if (!string.IsNullOrWhiteSpace(dto.SampleCode) && !string.Equals(batch.SampleCode, dto.SampleCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(ApiResponse.Fail($"الدفعة '{batch.BatchNumber}' تتبع رقم العينة '{batch.SampleCode}' ولا تتبع رقم العينة المختار ({dto.SampleCode})."));
                    }
                }

                if (item.Quantity > batch.CurrentQuantity)
                {
                    return BadRequest(ApiResponse.Fail($"الكمية المراد تحويلها من الدفعة '{batch.BatchNumber}' ({item.Quantity:N3} طن) تتجاوز الرصيد المتاح للدفعة ({batch.CurrentQuantity:N3} طن)."));
                }
            }

            // Resolve material
            RawMaterial? mat = null;
            var targetMatId = item.RawMaterialId ?? batch?.RawMaterialId;
            if (targetMatId.HasValue && targetMatId.Value > 0)
            {
                mat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Id == targetMatId.Value);
            }
            else if (batch != null && !string.IsNullOrWhiteSpace(batch.MaterialName))
            {
                mat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name.ToLower() == batch.MaterialName.ToLower());
            }

            if (mat == null)
            {
                return BadRequest(ApiResponse.Fail("معرف المادة المحولة مطلوب أو غير موجود."));
            }

            var ledgerStock = await GetAvailableStockAsync(location.Id, mat.Id);
            var batchStock = batch != null ? batch.CurrentQuantity : 0m;
            var availableStock = Math.Max(ledgerStock, batchStock);
            if (availableStock < item.Quantity)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المراد تحويلها من '{mat.Name}' ({item.Quantity:N3} طن) تتجاوز الرصيد المتاح حالياً في {location.Name} ({availableStock:N3} طن)."));
            }

            resolvedItems.Add((mat, item.Quantity, item.Notes, batch));
        }

        // 3. Validate attachments (Optional for Transfer, saved if uploaded)
        var docRes = await ValidateAndSaveAttachmentAsync(dto.DocumentAttachment, dto.DocumentAttachmentPath, "المستند الورقي", isRequired: false);
        if (!docRes.Success)
        {
            return BadRequest(ApiResponse.Fail(docRes.ErrorMessage ?? "المستند الورقي غير صالح."));
        }

        var weighbridgeRes = await ValidateAndSaveAttachmentAsync(dto.WeighbridgeAttachment, dto.WeighbridgeAttachmentPath, "مرفق الميزان", isRequired: false);
        if (!weighbridgeRes.Success)
        {
            CleanupSavedFiles(new[] { docRes.StoredFileName });
            return BadRequest(ApiResponse.Fail(weighbridgeRes.ErrorMessage ?? "مرفق الميزان غير صالح."));
        }

        StorageLocation? destLocation = null;
        var isWarehouseTransfer = string.Equals(dto.DestinationType, "WAREHOUSE", StringComparison.OrdinalIgnoreCase);
        if (isWarehouseTransfer)
        {
            if (!dto.DestinationStorageLocationId.HasValue || dto.DestinationStorageLocationId.Value <= 0)
            {
                return BadRequest(ApiResponse.Fail("مكان التخزين المستقبل مطلوب عند التحويل لمستودع آخر."));
            }
            if (dto.DestinationStorageLocationId.Value == location.Id)
            {
                return BadRequest(ApiResponse.Fail("مكان التخزين المستقبل لا يمكن أن يكون نفس مكان التخزين المصدر."));
            }
            destLocation = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.DestinationStorageLocationId.Value && l.IsActive);
            if (destLocation == null)
            {
                return BadRequest(ApiResponse.Fail("مكان التخزين المستقبل غير موجود أو غير مفعل."));
            }
        }

        var txDate = dto.TransactionDate ?? DateTime.UtcNow;

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var txNumber = await GetNextSequenceNumberAsync("TRANSFER");

            var op = new StorageOperation
            {
                OperationType = "TRANSFER",
                TransactionNumber = txNumber,
                TransactionDate = txDate,
                StorageLocationId = location.Id,
                StorageLocationName = location.Name,
                DestinationDepartment = isWarehouseTransfer ? "مستودع" : "الإنتاج",
                DestinationName = isWarehouseTransfer ? destLocation!.Name : "خطوط الإنتاج والتصنيع",
                DestinationId = isWarehouseTransfer ? destLocation!.Id : null,
                SelectionMode = dto.SelectionMode ?? "BY_REFERENCE",
                SampleCode = dto.SampleCode ?? resolvedItems.FirstOrDefault(x => !string.IsNullOrEmpty(x.Batch?.SampleCode)).Batch?.SampleCode,
                ProductionReferenceNumber = dto.ProductionReferenceNumber ?? resolvedItems.FirstOrDefault(x => x.Batch?.ProductionReferenceNumber > 0).Batch?.ProductionReferenceNumber,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                Notes = dto.Notes,
                ApprovalStatus = "PENDING",
                IsInventoryPosted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                CreatedByUserId = userId,

                // Attachments
                DocumentAttachmentPath = !string.IsNullOrWhiteSpace(docRes.StoredFileName) ? $"/uploads/storage/{docRes.StoredFileName}" : null,
                DocumentFileName = docRes.OriginalFileName,
                DocumentFileType = docRes.ContentType,
                DocumentFileSize = docRes.FileSize,

                WeighbridgeAttachmentPath = !string.IsNullOrWhiteSpace(weighbridgeRes.StoredFileName) ? $"/uploads/storage/{weighbridgeRes.StoredFileName}" : null,
                WeighbridgeFileName = weighbridgeRes.OriginalFileName,
                WeighbridgeFileType = weighbridgeRes.ContentType,
                WeighbridgeFileSize = weighbridgeRes.FileSize
            };

            await _db.StorageOperations.AddAsync(op);
            await _db.SaveChangesAsync();

            int lineIndex = 1;
            foreach (var (mat, qty, notes, batch) in resolvedItems)
            {
                // Concurrency-safe unique tracking identifier per transfer line
                var tracking = $"TRF-{txDate:yyyy}-{txNumber:D4}-L{lineIndex}";
                var opItem = new StorageOperationItem
                {
                    StorageOperationId = op.Id,
                    RawMaterialId = mat.Id,
                    MaterialName = mat.Name,
                    Quantity = qty,
                    TrackingNumber = tracking,
                    Notes = notes,
                    WarehouseBatchId = batch?.Id,
                    BatchNumber = batch?.BatchNumber,
                    SampleCode = batch?.SampleCode ?? dto.SampleCode,
                    ProductionReferenceNumber = batch?.ProductionReferenceNumber ?? dto.ProductionReferenceNumber,
                    ProductionOrderId = dto.ProductionOrderId ?? batch?.ProductionOrderId,
                    ProductionProcessId = dto.ProductionProcessId ?? batch?.ProductionProcessId,
                    ManufactureNumber = batch?.BatchNumber
                };
                await _db.StorageOperationItems.AddAsync(opItem);
                lineIndex++;
            }

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            // Auto-Approve: Automatically approve and post inventory ledger immediately
            var (trfOk, _, trfDto) = await ApproveStorageOperationCoreAsync(_db, _logger, _hubContext, op.Id, "System (Auto-Approved)", userId, "SYSTEM");
            if (trfOk && trfDto != null)
            {
                op.ApprovalStatus = "APPROVED";
                op.IsInventoryPosted = true;
            }

            _logger.LogInformation("Transfer Storage Operation #{TxNumber} (ID: {Id}) auto-approved successfully.",
                op.TransactionNumber, op.Id);

            var resultDto = MapOperationToDto(op, await _db.StorageOperationItems.Where(i => i.StorageOperationId == op.Id).ToListAsync());

            // SignalR notification
            await NotifyOperationChangeAsync("TRANSFER", op.Id, $"#{op.TransactionNumber}", "PENDING", userName, "تم تسجيل إذن تحويل مواد للإنتاج قيد الاعتماد.");

            return StatusCode(StatusCodes.Status201Created, ApiResponse<StorageOperationDto>.Ok(resultDto, $"تم تسجيل إذن التحويل للإنتاج بنجاح برقم عملية #{op.TransactionNumber}."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            CleanupSavedFiles(new[] { docRes.StoredFileName, weighbridgeRes.StoredFileName });
            _logger.LogError(ex, "Failed to submit Transfer Storage Operation.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ إذن التحويل."));
        }
    }

    /// <summary>
    /// Retrieve list of Storage Operations with filters and pagination.
    /// </summary>
    [HttpGet("operations")]
    [ProducesResponseType(typeof(ApiResponse<List<StorageOperationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOperations(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? locationId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.StorageOperations
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type) && type.ToUpperInvariant() != "ALL")
        {
            var normType = type.Trim().ToUpperInvariant();
            query = query.Where(o => o.OperationType == normType);
        }

        if (!string.IsNullOrWhiteSpace(status) && status.ToUpperInvariant() != "ALL")
        {
            var normStatus = status.Trim().ToUpperInvariant();
            query = query.Where(o => o.ApprovalStatus == normStatus);
        }

        if (locationId.HasValue && locationId.Value > 0)
        {
            query = query.Where(o => o.StorageLocationId == locationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.TransactionNumber.ToString().Contains(s) ||
                (o.Source != null && o.Source.ToLower().Contains(s)) ||
                (o.ClientName != null && o.ClientName.ToLower().Contains(s)) ||
                (o.DestinationName != null && o.DestinationName.ToLower().Contains(s)) ||
                o.StorageLocationName.ToLower().Contains(s) ||
                o.ResponsibleEmployee.ToLower().Contains(s) ||
                o.Items.Any(i => i.MaterialName.ToLower().Contains(s) || i.TrackingNumber.ToLower().Contains(s)));
        }

        var list = await query
            .OrderByDescending(o => o.TransactionDate)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = list.Select(o => MapOperationToDto(o, o.Items.ToList())).ToList();
        return Ok(ApiResponse<List<StorageOperationDto>>.Ok(dtos));
    }

    /// <summary>
    /// Retrieve single Storage Operation with items and status history.
    /// </summary>
    [HttpGet("operations/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<StorageOperationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOperationById(int id)
    {
        var op = await _db.StorageOperations
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.ProductionBatches)
                .ThenInclude(p => p.RawMaterial)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (op == null)
        {
            return NotFound(ApiResponse.Fail($"حركة المستودع #{id} غير موجودة."));
        }

        var dto = MapOperationToDto(op, op.Items.ToList());
        return Ok(ApiResponse<StorageOperationDto>.Ok(dto));
    }

    /// <summary>
    /// Approve Storage Operation and post movements to the Inventory Ledger atomically.
    /// </summary>
    [HttpPost("operations/{id:int}/approve")]
    [ProducesResponseType(typeof(ApiResponse<StorageOperationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveOperation(int id)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح باعتماد حركات المستودعات."));
        }

        var (success, errorMsg, dto) = await ApproveStorageOperationCoreAsync(
            _db, _logger, _hubContext, id, userName, userId, role);

        if (!success)
        {
            return BadRequest(ApiResponse.Fail(errorMsg ?? "فشل اعتماد العملية."));
        }

        return Ok(ApiResponse<StorageOperationDto>.Ok(dto!, "تم اعتماد الحركة وترحيلها إلى رصيد المخزن بنجاح."));
    }

    /// <summary>
    /// Reject Storage Operation with a stated reason.
    /// </summary>
    [HttpPost("operations/{id:int}/reject")]
    [ProducesResponseType(typeof(ApiResponse<StorageOperationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectOperation(int id, [FromBody] RejectStorageOperationDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح برفض حركات المستودعات."));
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(ApiResponse.Fail("سبب الرفض مطلوب."));
        }

        var (success, errorMsg, resDto) = await RejectStorageOperationCoreAsync(
            _db, _logger, _hubContext, id, dto.Reason.Trim(), userName, userId, role);

        if (!success)
        {
            return BadRequest(ApiResponse.Fail(errorMsg ?? "فشل رفض العملية."));
        }

        return Ok(ApiResponse<StorageOperationDto>.Ok(resDto!, "تم رفض الحركة وتحديث حالتها بنجاح."));
    }

    /// <summary>
    /// Resubmit a rejected Storage Operation.
    /// </summary>
    [HttpPost("operations/{id:int}/resubmit")]
    [ProducesResponseType(typeof(ApiResponse<StorageOperationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResubmitOperation(int id)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم المخازن."));
        }

        var (success, errorMsg, resDto) = await ResubmitStorageOperationCoreAsync(
            _db, _logger, _hubContext, id, userName, userId);

        if (!success)
        {
            return BadRequest(ApiResponse.Fail(errorMsg ?? "فشل إعادة إرسال العملية."));
        }

        return Ok(ApiResponse<StorageOperationDto>.Ok(resDto!, "تم إعادة إرسال الحركة للمراجعة والاعتماد بنجاح."));
    }

    /// <summary>
    /// Retrieve active Production Work-in-Process (WIP) batches transferred from storage.
    /// </summary>
    [HttpGet("production-batches")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionWorkInProcessDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductionBatches()
    {
        var batches = await _db.ProductionWorkInProcess
            .AsNoTracking()
            .Include(p => p.RawMaterial)
            .OrderByDescending(p => p.TransferDate)
            .ThenByDescending(p => p.Id)
            .Take(100)
            .Select(p => new ProductionWorkInProcessDto
            {
                Id = p.Id,
                StorageOperationId = p.StorageOperationId,
                StorageOperationItemId = p.StorageOperationItemId,
                RawMaterialId = p.RawMaterialId,
                MaterialName = p.RawMaterial != null ? p.RawMaterial.Name : "Unknown",
                TrackingNumber = p.TrackingNumber,
                MaterialQuantityIssued = p.MaterialQuantityIssued,
                SourceStorageLocation = p.SourceStorageLocation,
                TransferDate = p.TransferDate,
                Status = p.Status,
                FinalProductName = p.FinalProductName,
                FinalProductQuantity = p.FinalProductQuantity,
                WasteQuantity = p.WasteQuantity,
                WastePercentage = p.WastePercentage,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ProductionWorkInProcessDto>>.Ok(batches));
    }

    /// <summary>
    /// Retrieve compliant, approved Laboratory references eligible for Warehouse Import with batch division.
    /// Only displays materials that have passed Laboratory compliance and have unallocated accepted quantities remaining.
    /// </summary>
    [HttpGet("compliant-references")]
    [ProducesResponseType(typeof(ApiResponse<List<WarehouseCompliantReferenceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompliantLaboratoryReferences()
    {
        var reports = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" &&
                        r.ComplyStatus == "COMPLY" &&
                        r.InspectionDecision != "REJECTED" &&
                        r.AcceptedQuantity > 0)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .Take(150)
            .ToListAsync();

        var reportIds = reports.Select(r => r.Id).ToList();
        var allocatedGrouped = await _db.WarehouseBatches
            .AsNoTracking()
            .Where(b => b.LaboratoryReportId.HasValue && reportIds.Contains(b.LaboratoryReportId.Value) && b.Status != "REJECTED")
            .GroupBy(b => b.LaboratoryReportId!.Value)
            .Select(g => new { LabId = g.Key, Allocated = g.Sum(x => x.InitialQuantity) })
            .ToDictionaryAsync(x => x.LabId, x => x.Allocated);

        var result = new List<WarehouseCompliantReferenceDto>();
        foreach (var r in reports)
        {
            allocatedGrouped.TryGetValue(r.Id, out var allocated);
            var remaining = Math.Round(r.AcceptedQuantity - allocated, 3);
            if (remaining > 0)
            {
                result.Add(new WarehouseCompliantReferenceDto
                {
                    LaboratoryReportId = r.Id,
                    ProductionReferenceNumber = r.ProductionReferenceNumber,
                    SampleNumber = r.SampleNumber,
                    SampleCode = !string.IsNullOrWhiteSpace(r.SampleCode) ? r.SampleCode : (r.ProductionReferenceNumber > 0 ? $"REF-{r.ProductionReferenceNumber:D4}" : $"SMP-{r.SampleNumber:D4}"),
                    MaterialName = !string.IsNullOrWhiteSpace(r.SampleName) ? r.SampleName : (r.RawMaterial != null ? r.RawMaterial.Name : r.RawMaterialDescription),
                    MaterialClassification = r.MaterialClassification ?? "MECHANICAL",
                    SupplierName = !string.IsNullOrWhiteSpace(r.SupplierName) ? r.SupplierName : "داخلي",
                    ReceivedQuantity = r.ReceivedQuantity,
                    AcceptedQuantity = r.AcceptedQuantity,
                    AlreadyAllocatedQuantity = allocated,
                    RemainingAvailableQuantity = remaining,
                    ComplyStatus = r.ComplyStatus,
                    InspectionDecision = r.InspectionDecision,
                    ApprovalStatus = r.ApprovalStatus,
                    AnalysisDateTime = r.AnalysisDateTime,
                    SampleReceivedDate = r.SampleReceivedDate,
                    RawMaterialId = r.RawMaterialId ?? r.RawMaterial?.Id,
                    AttachmentPath = r.AttachmentPath,
                    AttachmentFileName = r.AttachmentFileName,
                    Notes = r.Notes,
                    IsFinishedProduct = false
                });
            }
        }

        // Include certified finished products that have passed BOTH Physical and Chemical Laboratory approvals
        var finalCerts = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .Include(c => c.ProductionFinishedProduct)
                .ThenInclude(f => f.ProductionProcess)
            .Include(c => c.ProductionFinishedProduct)
                .ThenInclude(f => f.ProductionOrder)
            .Where(c => c.ApprovalStatus == "APPROVED" &&
                        c.OverallStatus == "APPROVED" &&
                        c.PhysicalApprovalStatus == "APPROVED" &&
                        c.ChemicalApprovalStatus == "APPROVED" &&
                        c.ConformityDecision == "CONFORMS" &&
                        c.ActualFinalProductQuantity > 0)
            .OrderByDescending(c => c.Id)
            .Take(150)
            .ToListAsync();

        if (finalCerts.Any())
        {
            var certIds = finalCerts.Select(c => c.Id).ToList();
            var certAllocated = await _db.WarehouseBatches
                .AsNoTracking()
                .Where(b => (b.LaboratoryCertificateId.HasValue && certIds.Contains(b.LaboratoryCertificateId.Value)) ||
                            (b.MaterialClassification == "FINAL_PRODUCT" && b.BatchNumber != null))
                .GroupBy(b => b.LaboratoryCertificateId ?? 0)
                .Select(g => new { CertId = g.Key, Allocated = g.Sum(x => x.InitialQuantity) })
                .ToDictionaryAsync(x => x.CertId, x => x.Allocated);

            var batchSumsByNumber = await _db.WarehouseBatches
                .AsNoTracking()
                .Where(b => b.MaterialClassification == "FINAL_PRODUCT" && b.Status != "REJECTED")
                .GroupBy(b => b.BatchNumber)
                .Select(g => new { BatchNumber = g.Key, TotalAllocated = g.Sum(x => x.InitialQuantity) })
                .ToDictionaryAsync(x => x.BatchNumber, x => x.TotalAllocated);

            foreach (var cert in finalCerts)
            {
                var fp = cert.ProductionFinishedProduct;
                var mfgNo = cert.ManufactureNumber ?? fp?.ManufactureNumber ?? cert.ProductionBatchNumber ?? string.Empty;
                decimal allocated = 0m;
                if (certAllocated.TryGetValue(cert.Id, out var directAlloc))
                {
                    allocated += directAlloc;
                }
                else if (!string.IsNullOrWhiteSpace(mfgNo) && batchSumsByNumber.TryGetValue(mfgNo, out var byMfg))
                {
                    allocated += byMfg;
                }

                var totalQty = cert.ActualFinalProductQuantity;
                var remaining = Math.Round(totalQty - allocated, 3);
                if (remaining > 0)
                {
                    var procNumber = cert.ProductionProcessNumber ?? fp?.ProductionProcess?.ProcessNumber ?? string.Empty;
                    var orderNumber = cert.ProductionOrderNumber ?? fp?.ProductionOrderNumber ?? fp?.ProductionOrder?.OrderNumber ?? string.Empty;
                    var prodName = cert.FinalProductName ?? fp?.FinishedProductName ?? "منتج نهائي معتمد";

                    result.Add(new WarehouseCompliantReferenceDto
                    {
                        LaboratoryCertificateId = cert.Id,
                        ProductionFinishedProductId = cert.ProductionFinishedProductId,
                        ProductionReferenceNumber = cert.ProductionReferenceNumber > 0 ? cert.ProductionReferenceNumber : (fp?.ProductionOrderId ?? 0),
                        SampleNumber = cert.Id,
                        SampleCode = cert.CertificateNumber,
                        CertificateNumber = cert.CertificateNumber,
                        ManufactureNumber = mfgNo,
                        ProductionProcessNumber = procNumber,
                        ProductionOrderNumber = orderNumber,
                        MaterialName = prodName,
                        MaterialClassification = "FINAL_PRODUCT",
                        SupplierName = "قسم الإنتاج والتصنيع",
                        ReceivedQuantity = totalQty,
                        AcceptedQuantity = totalQty,
                        AlreadyAllocatedQuantity = allocated,
                        RemainingAvailableQuantity = remaining,
                        ComplyStatus = "COMPLY",
                        InspectionDecision = "ACCEPTED",
                        ApprovalStatus = "APPROVED",
                        AnalysisDateTime = cert.ReviewedAt ?? cert.CreatedAt,
                        SampleReceivedDate = cert.ProductionCompletionDateTime,
                        AttachmentPath = cert.AttachmentPath ?? fp?.WeighbridgeAttachmentPath,
                        AttachmentFileName = cert.AttachmentFileName ?? fp?.WeighbridgeFileName,
                        Notes = $"منتج نهائي معتمد مخبرياً (فيزيائي + كيميائي) - شهادة {cert.CertificateNumber} - عملية {procNumber} - تصنيع {mfgNo}",
                        IsFinishedProduct = true
                    });
                }
            }
        }

        return Ok(ApiResponse<List<WarehouseCompliantReferenceDto>>.Ok(result));
    }

    /// <summary>
    /// Retrieve available approved batches physically located in the specified storage location.
    /// Excludes zero-balance, pending, rejected, or fully consumed batches.
    /// </summary>
    [HttpGet("locations/{locationId:int}/available-batches")]
    [ProducesResponseType(typeof(ApiResponse<List<LocationBatchAvailabilityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocationAvailableBatches(
        int locationId,
        [FromQuery] string? sampleCode = null,
        [FromQuery] int? referenceNumber = null)
    {
        var query = _db.WarehouseBatches
            .AsNoTracking()
            .Include(b => b.StorageLocation)
            .Where(b => b.StorageLocationId == locationId &&
                        b.CurrentQuantity > 0 &&
                        b.Status == "APPROVED" &&
                        b.IsActive);

        if (!string.IsNullOrWhiteSpace(sampleCode))
        {
            var sc = sampleCode.Trim().ToLowerInvariant();
            query = query.Where(b => b.SampleCode.ToLower() == sc);
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(b => b.ProductionReferenceNumber == referenceNumber.Value);
        }

        var batches = await query
            .OrderBy(b => b.BatchNumber)
            .Select(b => new LocationBatchAvailabilityDto
            {
                BatchId = b.Id,
                BatchNumber = b.BatchNumber,
                SampleCode = b.SampleCode,
                ProductionReferenceNumber = b.ProductionReferenceNumber,
                LaboratoryReportId = b.LaboratoryReportId ?? 0,
                RawMaterialId = b.RawMaterialId,
                MaterialName = b.MaterialName,
                MaterialClassification = b.MaterialClassification,
                SupplierName = b.SupplierName,
                StorageLocationId = b.StorageLocationId,
                StorageLocationName = b.StorageLocation != null ? b.StorageLocation.Name : string.Empty,
                InitialQuantity = b.InitialQuantity,
                AvailableQuantity = b.CurrentQuantity,
                ImportDate = b.ImportDate
            })
            .ToListAsync();

        return Ok(ApiResponse<List<LocationBatchAvailabilityDto>>.Ok(batches));
    }

    /// <summary>
    /// Retrieve distinct Laboratory references available in the specified storage location with active batch balances.
    /// </summary>
    [HttpGet("locations/{locationId:int}/available-references")]
    [ProducesResponseType(typeof(ApiResponse<List<WarehouseCompliantReferenceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocationAvailableReferences(int locationId)
    {
        var activeBatches = await _db.WarehouseBatches
            .AsNoTracking()
            .Where(b => b.StorageLocationId == locationId &&
                        b.CurrentQuantity > 0 &&
                        b.Status == "APPROVED" &&
                        b.IsActive)
            .ToListAsync();

        if (!activeBatches.Any())
        {
            return Ok(ApiResponse<List<WarehouseCompliantReferenceDto>>.Ok(new List<WarehouseCompliantReferenceDto>()));
        }

        var labIds = activeBatches.Where(b => b.LaboratoryReportId.HasValue && b.LaboratoryReportId.Value > 0).Select(b => b.LaboratoryReportId!.Value).Distinct().ToList();
        var reportsMap = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Where(r => labIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id);

        var certIds = activeBatches.Where(b => b.LaboratoryCertificateId.HasValue && b.LaboratoryCertificateId.Value > 0).Select(b => b.LaboratoryCertificateId!.Value).Distinct().ToList();
        var certsMap = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .Where(c => certIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var grouped = activeBatches
            .GroupBy(b => new
            {
                RefNo = b.ProductionReferenceNumber,
                Code = b.SampleCode,
                LabId = b.LaboratoryReportId ?? 0,
                CertId = b.LaboratoryCertificateId ?? 0,
                Mat = b.MaterialName
            })
            .Select(g =>
            {
                reportsMap.TryGetValue(g.Key.LabId, out var rep);
                certsMap.TryGetValue(g.Key.CertId, out var cert);
                var totalAvailable = g.Sum(x => x.CurrentQuantity);
                var sampleCode = !string.IsNullOrWhiteSpace(g.Key.Code)
                    ? g.Key.Code
                    : (cert != null && !string.IsNullOrWhiteSpace(cert.CertificateNumber)
                        ? cert.CertificateNumber
                        : (rep != null && !string.IsNullOrWhiteSpace(rep.SampleCode)
                            ? rep.SampleCode
                            : (g.Key.RefNo > 0 ? $"REF-{g.Key.RefNo:D4}" : $"SMP-{g.Key.LabId:D4}")));
                var matName = !string.IsNullOrWhiteSpace(g.Key.Mat)
                    ? g.Key.Mat
                    : (cert?.FinalProductName ?? rep?.SampleName ?? "خام معملي");
                var matClass = cert != null ? "FINAL_PRODUCT" : (rep?.MaterialClassification ?? g.First().MaterialClassification ?? "MECHANICAL");
                var suppName = cert != null ? "قسم الإنتاج والتصنيع" : (rep?.SupplierName ?? g.First().SupplierName);

                return new WarehouseCompliantReferenceDto
                {
                    LaboratoryReportId = g.Key.LabId,
                    LaboratoryCertificateId = g.Key.CertId > 0 ? g.Key.CertId : null,
                    ProductionReferenceNumber = g.Key.RefNo,
                    SampleNumber = cert?.Id ?? rep?.SampleNumber ?? 0,
                    SampleCode = sampleCode,
                    CertificateNumber = cert?.CertificateNumber,
                    MaterialName = matName,
                    MaterialClassification = matClass,
                    SupplierName = suppName,
                    ReceivedQuantity = cert?.ActualFinalProductQuantity ?? rep?.ReceivedQuantity ?? totalAvailable,
                    AcceptedQuantity = cert?.ActualFinalProductQuantity ?? rep?.AcceptedQuantity ?? totalAvailable,
                    RemainingAvailableQuantity = totalAvailable,
                    ComplyStatus = cert != null ? "COMPLY" : (rep?.ComplyStatus ?? "COMPLY"),
                    InspectionDecision = cert != null ? "ACCEPTED" : (rep?.InspectionDecision ?? "ACCEPTED"),
                    ApprovalStatus = "APPROVED",
                    AnalysisDateTime = cert?.ReviewedAt ?? rep?.AnalysisDateTime ?? g.First().ImportDate,
                    SampleReceivedDate = cert?.ProductionCompletionDateTime ?? rep?.SampleReceivedDate ?? g.First().ImportDate,
                    RawMaterialId = g.First().RawMaterialId,
                    AttachmentPath = cert?.AttachmentPath ?? rep?.AttachmentPath,
                    AttachmentFileName = cert?.AttachmentFileName ?? rep?.AttachmentFileName,
                    Notes = cert != null ? $"منتج نهائي معتمد ({cert.CertificateNumber})" : rep?.Notes,
                    IsFinishedProduct = cert != null || matClass == "FINAL_PRODUCT"
                };
            })
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .ToList();

        return Ok(ApiResponse<List<WarehouseCompliantReferenceDto>>.Ok(grouped));
    }

    /// <summary>
    /// Retrieve real-time dynamic inventory of active warehouse batches.
    /// Can be filtered by storage location ID. Excludes zero-balance or consumed batches.
    /// </summary>
    [HttpGet("inventory")]
    [ProducesResponseType(typeof(ApiResponse<List<WarehouseBatchAvailabilityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWarehouseInventory([FromQuery] int? locationId = null)
    {
        var batches = await _inventoryService.GetAuthoritativeBatchesAsync(locationId);
        return Ok(ApiResponse<List<WarehouseBatchAvailabilityDto>>.Ok(batches));
    }

    /// <summary>
    /// Preview next consecutive unique batch numbers for form generation.
    /// </summary>
    [HttpGet("preview-batch-numbers")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewBatchNumbers([FromQuery] int count = 1)
    {
        if (count < 1) count = 1;
        if (count > 50) count = 50;
        var batchNumbers = await PeekNextBatchNumbersAsync(count);
        return Ok(ApiResponse<List<string>>.Ok(batchNumbers));
    }

    /// <summary>
    /// Retrieve all batches associated with a specific storage operation (for reprint and details).
    /// </summary>
    [HttpGet("operations/{id:int}/batches")]
    [ProducesResponseType(typeof(ApiResponse<List<WarehouseBatchDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOperationBatches(int id)
    {
        var batches = await _db.WarehouseBatches
            .AsNoTracking()
            .Include(b => b.StorageLocation)
            .Include(b => b.StorageOperation)
            .Where(b => b.StorageOperationId == id)
            .OrderBy(b => b.BatchNumber)
            .ToListAsync();

        var dtos = batches.Select(MapBatchToDto).ToList();
        return Ok(ApiResponse<List<WarehouseBatchDto>>.Ok(dtos));
    }

    /// <summary>
    /// Retrieve details of a single warehouse batch by Id.
    /// </summary>
    [HttpGet("batches/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBatchById(int id)
    {
        var batch = await _db.WarehouseBatches
            .AsNoTracking()
            .Include(b => b.StorageLocation)
            .Include(b => b.StorageOperation)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"الدفعة رقم #{id} غير موجودة."));
        }

        return Ok(ApiResponse<WarehouseBatchDto>.Ok(MapBatchToDto(batch)));
    }

    /// <summary>
    /// Retrieve available finished products stored in warehouses with stock > 0.
    /// </summary>
    [HttpGet("available-finished-products")]
    [HttpGet("locations/{locationId:int}/available-finished-products")]
    [ProducesResponseType(typeof(ApiResponse<List<AvailableFinishedProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableFinishedProducts([FromQuery] int? locationId = null, [FromRoute] int? routeLocationId = null)
    {
        var targetLocationId = (routeLocationId.HasValue && routeLocationId.Value > 0) ? routeLocationId : locationId;
        var query = _db.WarehouseBatches
            .AsNoTracking()
            .Include(b => b.StorageLocation)
            .Include(b => b.ProductionOrder)
            .Include(b => b.ProductionProcess)
            .Where(b => b.IsActive && b.CurrentQuantity > 0 && b.Status == "APPROVED" &&
                   (b.MaterialClassification == "FINAL_PRODUCT" || b.ProductionOrderId != null || b.ProductionProcessId != null));

        if (targetLocationId.HasValue && targetLocationId.Value > 0)
        {
            query = query.Where(b => b.StorageLocationId == targetLocationId.Value);
        }

        var batches = await query
            .OrderByDescending(b => b.ImportDate)
            .ThenByDescending(b => b.Id)
            .ToListAsync();

        var dtos = batches.Select(b => new AvailableFinishedProductDto
        {
            WarehouseBatchId = b.Id,
            BatchNumber = b.BatchNumber,
            ManufactureNumber = b.BatchNumber,
            ProductName = b.MaterialName,
            StorageLocationId = b.StorageLocationId,
            StorageLocationName = b.StorageLocation?.Name ?? string.Empty,
            AvailableQuantity = b.CurrentQuantity,
            InitialQuantity = b.InitialQuantity,
            Unit = "طن",
            ProductionOrderId = b.ProductionOrderId,
            ProductionOrderNumber = b.ProductionOrder?.OrderNumber,
            ProductionProcessId = b.ProductionProcessId,
            ProcessNumber = b.ProductionProcess?.ProcessNumber,
            ProductionDate = b.ImportDate
        }).ToList();

        return Ok(ApiResponse<List<AvailableFinishedProductDto>>.Ok(dtos));
    }

    /// <summary>
    /// Record manufacturing output for a production batch and calculate actual waste.
    /// </summary>
    [HttpPost("production-batches/{id:int}/record-output")]
    [ProducesResponseType(typeof(ApiResponse<ProductionWorkInProcessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordProductionOutput(int id, [FromBody] RecordProductionOutputDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بتسجيل مخرجات الإنتاج."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات الإنتاج غير صالحة."));
        }

        var batch = await _db.ProductionWorkInProcess.FirstOrDefaultAsync(p => p.Id == id);
        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"دفعة التشغيل رقم #{id} غير موجودة."));
        }

        if (batch.IsRecoveredStockPosted)
        {
            return BadRequest(ApiResponse.Fail("تم ترحيل مخرجات هذه الدفعة مسبقاً ولا يمكن تسجيلها مرة أخرى."));
        }

        // Determine quantities (support waste recorded as final product)
        var finalProductQty = dto.FinalProductQuantity.HasValue && dto.FinalProductQuantity.Value > 0
            ? dto.FinalProductQuantity.Value
            : 0m;

        var recoverableQty = dto.RecoverableQuantity.HasValue && dto.RecoverableQuantity.Value > 0
            ? dto.RecoverableQuantity.Value
            : 0m;

        if (finalProductQty <= 0 && recoverableQty <= 0)
        {
            return BadRequest(ApiResponse.Fail("يجب إدخال كمية المنتج النهائي أو كمية المادة المستردة / المنتج المصنف من الهالك (أكبر من الصفر)."));
        }

        // Total outputs (final product + any classified recoverable) must not exceed raw material issued
        var totalOutputs = finalProductQty + recoverableQty;
        if (totalOutputs > batch.MaterialQuantityIssued)
        {
            return BadRequest(ApiResponse.Fail($"إجمالي المخرجات ({totalOutputs:N3} طن) لا يمكن أن يتجاوز كمية المادة الخام المحولة ({batch.MaterialQuantityIssued:N3} طن)."));
        }

        // Validate waste classification
        var wasteClass = (dto.WasteClassification ?? "ORDINARY").Trim().ToUpperInvariant();
        var validClasses = new[] { "ORDINARY", MaterialProcessType.FinalProduct, MaterialProcessType.RecoverableWaste };
        if (!validClasses.Contains(wasteClass))
        {
            return BadRequest(ApiResponse.Fail($"تصنيف الهالك '{wasteClass}' غير صالح. القيم المسموحة: ORDINARY، FINAL_PRODUCT، RECOVERABLE_WASTE."));
        }

        if ((wasteClass == MaterialProcessType.FinalProduct || wasteClass == MaterialProcessType.RecoverableWaste) && recoverableQty <= 0)
        {
            return BadRequest(ApiResponse.Fail("يجب إدخال كمية المواد المستردة عند تصنيف الهالك كمنتج نهائي أو مادة مستردة."));
        }

        if ((wasteClass == MaterialProcessType.FinalProduct || wasteClass == MaterialProcessType.RecoverableWaste) && string.IsNullOrWhiteSpace(dto.RecoverableProductName))
        {
            return BadRequest(ApiResponse.Fail("يجب إدخال اسم المنتج / المادة المستردة."));
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var wasteQty = Math.Max(0m, batch.MaterialQuantityIssued - totalOutputs);
            var wastePercent = batch.MaterialQuantityIssued > 0 ? Math.Round((wasteQty / batch.MaterialQuantityIssued) * 100m, 2) : 0m;

            batch.FinalProductName = !string.IsNullOrWhiteSpace(dto.FinalProductName) ? dto.FinalProductName.Trim() : (batch.FinalProductName ?? dto.RecoverableProductName);
            batch.FinalProductQuantity = finalProductQty;
            batch.WasteQuantity = wasteQty;
            batch.WastePercentage = wastePercent;
            batch.WasteClassification = wasteClass;
            batch.Status = "مكتمل";
            batch.OutputResponsibleEmployee = userName;

            var historyEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm}: {userName} recorded output (Class: {wasteClass}, Waste: {wasteQty:N3}t, Recovered: {recoverableQty:N3}t)";
            batch.ClassificationHistory = string.IsNullOrEmpty(batch.ClassificationHistory)
                ? historyEntry
                : $"{batch.ClassificationHistory}\n{historyEntry}";

            if (!string.IsNullOrWhiteSpace(dto.Notes))
            {
                batch.Notes = string.IsNullOrWhiteSpace(batch.Notes) ? dto.Notes : $"{batch.Notes} | {dto.Notes}";
            }

            // Handle recoverable waste / final product stock posting
            if ((wasteClass == MaterialProcessType.FinalProduct || wasteClass == MaterialProcessType.RecoverableWaste) && recoverableQty > 0)
            {
                // Validate destination location
                int? destLocId = dto.DestinationStorageLocationId;
                if (!destLocId.HasValue || destLocId.Value <= 0)
                {
                    return BadRequest(ApiResponse.Fail("يجب تحديد موقع التخزين المستهدف للمواد المستردة / المنتج النهائي."));
                }

                var destLoc = await _db.StorageLocations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == destLocId.Value && l.IsActive);
                if (destLoc == null)
                {
                    return BadRequest(ApiResponse.Fail("موقع التخزين المستهدف للمواد المستردة غير موجود أو غير مفعل."));
                }

                // Find or create the recoverable material record
                var recoverableName = dto.RecoverableProductName!.Trim();
                var recoverableMat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name.ToLower() == recoverableName.ToLower());
                if (recoverableMat == null)
                {
                    recoverableMat = new RawMaterial
                    {
                        Name = recoverableName,
                        MaterialType = wasteClass == MaterialProcessType.FinalProduct ? MaterialProcessType.FinalProduct : MaterialProcessType.RecoverableWaste,
                        InitialQuantity = 0m,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName,
                        CreatedByUserId = userId
                    };
                    await _db.RawMaterials.AddAsync(recoverableMat);
                    await _db.SaveChangesAsync();
                }

                // Post to inventory ledger (prevents double posting since we check IsRecoveredStockPosted above)
                var ledgerMovementType = wasteClass == MaterialProcessType.FinalProduct ? "FINAL_PRODUCT_INFLOW" : "RECOVERED_WASTE_INFLOW";

                var existingBalance = await _db.StorageInventoryLedger
                    .Where(l => l.StorageLocationId == destLocId.Value && l.RawMaterialId == recoverableMat.Id)
                    .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

                var ledgerEntry = new StorageInventoryLedger
                {
                    StorageLocationId = destLocId.Value,
                    RawMaterialId = recoverableMat.Id,
                    MovementType = ledgerMovementType,
                    Quantity = recoverableQty,
                    RunningBalance = existingBalance + recoverableQty,
                    StorageOperationId = batch.StorageOperationId,
                    MovementDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                };
                await _db.StorageInventoryLedger.AddAsync(ledgerEntry);

                batch.RecoverableMaterialId = recoverableMat.Id;
                batch.RecoverableProductName = recoverableName;
                batch.RecoverableQuantity = recoverableQty;
                batch.RecoverableUnit = dto.RecoverableUnit ?? "طن";
                batch.DestinationStorageLocationId = destLocId.Value;
                batch.DestinationStorageLocationName = destLoc.Name;
                batch.IsRecoveredStockPosted = true;
                batch.RecoveredStockPostedAt = DateTime.UtcNow;

                _logger.LogInformation("Recoverable waste/final product {Name} ({Qty:N3}) posted to ledger at location {Loc} for batch #{Tracking}.",
                    recoverableName, recoverableQty, destLoc.Name, batch.TrackingNumber);
            }

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            var mat = await _db.RawMaterials.FindAsync(batch.RawMaterialId);
            var resultDto = new ProductionWorkInProcessDto
            {
                Id = batch.Id,
                StorageOperationId = batch.StorageOperationId,
                StorageOperationItemId = batch.StorageOperationItemId,
                RawMaterialId = batch.RawMaterialId,
                MaterialName = mat?.Name ?? "Unknown",
                TrackingNumber = batch.TrackingNumber,
                MaterialQuantityIssued = batch.MaterialQuantityIssued,
                SourceStorageLocation = batch.SourceStorageLocation,
                TransferDate = batch.TransferDate,
                Status = batch.Status,
                FinalProductName = batch.FinalProductName,
                FinalProductQuantity = batch.FinalProductQuantity,
                WasteQuantity = batch.WasteQuantity,
                WastePercentage = batch.WastePercentage,
                WasteClassification = batch.WasteClassification,
                RecoverableProductName = batch.RecoverableProductName,
                RecoverableQuantity = batch.RecoverableQuantity,
                RecoverableUnit = batch.RecoverableUnit,
                DestinationStorageLocationId = batch.DestinationStorageLocationId,
                DestinationStorageLocationName = batch.DestinationStorageLocationName,
                IsRecoveredStockPosted = batch.IsRecoveredStockPosted,
                RecoveredStockPostedAt = batch.RecoveredStockPostedAt,
                Notes = batch.Notes,
                CreatedAt = batch.CreatedAt
            };

            return Ok(ApiResponse<ProductionWorkInProcessDto>.Ok(resultDto, "تم تسجيل مخرجات الإنتاج بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to record production output for batch #{Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تسجيل مخرجات الإنتاج."));
        }
    }

    // =========================================================================
    // CORE STATIC APPROVAL HELPERS (SHARED WITH CONTROL & MONITORING)
    // =========================================================================

    public static async Task<(bool Success, string? ErrorMessage, StorageOperationDto? Dto)> ApproveStorageOperationCoreAsync(
        FaceStoneDbContext db,
        ILogger logger,
        IHubContext<FaceStone.Web.Hubs.ChatHub> hubContext,
        int operationId,
        string reviewerName,
        Guid? reviewerId,
        string reviewerRole)
    {
        using var dbTransaction = await db.Database.BeginTransactionAsync();
        try
        {
            var op = await db.StorageOperations
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == operationId);

            if (op == null)
            {
                await dbTransaction.RollbackAsync();
                return (false, "حركة المستودع غير موجودة.", null);
            }

            if (op.ApprovalStatus == "APPROVED" || op.IsInventoryPosted)
            {
                await dbTransaction.RollbackAsync();
                return (false, "تم اعتماد هذه العملية مسبقاً ولا يمكن تكرار ترحيل المخزون.", null);
            }

            var prevStatus = op.ApprovalStatus;
            var opType = op.OperationType.ToUpperInvariant();

            // Perform inventory updates per operation type
            if (opType == "IMPORT")
            {
                // Activate batches created for this import operation
                var batches = await db.WarehouseBatches.Where(b => b.StorageOperationId == op.Id).ToListAsync();
                foreach (var b in batches)
                {
                    b.Status = "APPROVED";
                    b.CurrentQuantity = b.InitialQuantity;
                    b.IsActive = true;
                }

                // Inflow: increase stock for each material line in this location
                foreach (var item in op.Items)
                {
                    var currentStock = await db.StorageInventoryLedger
                        .Where(l => l.StorageLocationId == op.StorageLocationId && l.RawMaterialId == item.RawMaterialId)
                        .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

                    var newStock = currentStock + item.Quantity;

                    var ledgerEntry = new StorageInventoryLedger
                    {
                        StorageLocationId = op.StorageLocationId,
                        RawMaterialId = item.RawMaterialId,
                        StorageOperationId = op.Id,
                        StorageOperationItemId = item.Id,
                        WarehouseBatchId = item.WarehouseBatchId,
                        BatchNumber = item.BatchNumber,
                        MovementType = op.MaterialType == "FINAL_PRODUCT" ? "FINAL_PRODUCT_INFLOW" : "IMPORT",
                        Quantity = item.Quantity, // Positive for inflow
                        RunningBalance = newStock,
                        MovementDate = op.TransactionDate,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = reviewerName
                    };

                    await db.StorageInventoryLedger.AddAsync(ledgerEntry);
                }
            }
            else if (opType == "EXPORT")
            {
                // Outflow: verify stock and decrease stock for each material line and batch
                foreach (var item in op.Items)
                {
                    if (item.WarehouseBatchId.HasValue && item.WarehouseBatchId.Value > 0)
                    {
                        var batch = await db.WarehouseBatches.FirstOrDefaultAsync(b => b.Id == item.WarehouseBatchId.Value);
                        if (batch == null || batch.CurrentQuantity < item.Quantity)
                        {
                            await dbTransaction.RollbackAsync();
                            var avail = batch?.CurrentQuantity ?? 0m;
                            return (false, $"الرصيد المتاح للدفعة '{item.BatchNumber}' في {op.StorageLocationName} ({avail:N3} طن) غير كافٍ لصرف الكمية المطلوبة ({item.Quantity:N3} طن).", null);
                        }
                        batch.CurrentQuantity = Math.Round(batch.CurrentQuantity - item.Quantity, 3);
                        if (batch.CurrentQuantity <= 0)
                        {
                            batch.CurrentQuantity = 0m;
                            batch.Status = "CONSUMED";
                        }
                    }

                    var currentStock = await db.StorageInventoryLedger
                        .Where(l => l.StorageLocationId == op.StorageLocationId && l.RawMaterialId == item.RawMaterialId)
                        .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

                    if (currentStock < item.Quantity)
                    {
                        await dbTransaction.RollbackAsync();
                        return (false, $"الرصيد المتاح للصنف '{item.MaterialName}' في {op.StorageLocationName} ({currentStock:N3} طن) غير كافٍ لصرف الكمية المطلوبة ({item.Quantity:N3} طن).", null);
                    }

                    var newStock = currentStock - item.Quantity;

                    var ledgerEntry = new StorageInventoryLedger
                    {
                        StorageLocationId = op.StorageLocationId,
                        RawMaterialId = item.RawMaterialId,
                        StorageOperationId = op.Id,
                        StorageOperationItemId = item.Id,
                        WarehouseBatchId = item.WarehouseBatchId,
                        BatchNumber = item.BatchNumber,
                        MovementType = "EXPORT",
                        Quantity = -item.Quantity, // Negative for outflow
                        RunningBalance = newStock,
                        MovementDate = op.TransactionDate,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = reviewerName
                    };

                    await db.StorageInventoryLedger.AddAsync(ledgerEntry);
                }
            }
            else if (opType == "TRANSFER")
            {
                // Transfer to production: deduct warehouse stock and record WIP batch
                foreach (var item in op.Items)
                {
                    WarehouseBatch? batch = null;
                    if (item.WarehouseBatchId.HasValue && item.WarehouseBatchId.Value > 0)
                    {
                        batch = await db.WarehouseBatches.FirstOrDefaultAsync(b => b.Id == item.WarehouseBatchId.Value);
                        if (batch == null || batch.CurrentQuantity < item.Quantity)
                        {
                            await dbTransaction.RollbackAsync();
                            var avail = batch?.CurrentQuantity ?? 0m;
                            return (false, $"الرصيد المتاح للدفعة '{item.BatchNumber}' في {op.StorageLocationName} ({avail:N3} طن) غير كافٍ للتحويل إلى الإنتاج ({item.Quantity:N3} طن).", null);
                        }
                        batch.CurrentQuantity = Math.Round(batch.CurrentQuantity - item.Quantity, 3);
                        if (batch.CurrentQuantity <= 0)
                        {
                            batch.CurrentQuantity = 0m;
                            batch.Status = "CONSUMED";
                        }
                    }

                    var currentStock = await db.StorageInventoryLedger
                        .Where(l => l.StorageLocationId == op.StorageLocationId && l.RawMaterialId == item.RawMaterialId)
                        .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

                    if (currentStock < item.Quantity)
                    {
                        await dbTransaction.RollbackAsync();
                        return (false, $"الرصيد المتاح للمادة '{item.MaterialName}' في {op.StorageLocationName} ({currentStock:N3} طن) غير كافٍ للتحويل إلى الإنتاج ({item.Quantity:N3} طن).", null);
                    }

                    var newStock = currentStock - item.Quantity;

                    var ledgerEntry = new StorageInventoryLedger
                    {
                        StorageLocationId = op.StorageLocationId,
                        RawMaterialId = item.RawMaterialId,
                        StorageOperationId = op.Id,
                        StorageOperationItemId = item.Id,
                        WarehouseBatchId = item.WarehouseBatchId,
                        BatchNumber = item.BatchNumber,
                        MovementType = "TRANSFER",
                        Quantity = -item.Quantity, // Negative for transfer out of warehouse
                        RunningBalance = newStock,
                        MovementDate = op.TransactionDate,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = reviewerName
                    };

                    await db.StorageInventoryLedger.AddAsync(ledgerEntry);

                    if (op.DestinationDepartment == "مستودع" && op.DestinationId.HasValue && op.DestinationId.Value > 0)
                    {
                        var destLocId = op.DestinationId.Value;
                        var destCurrentStock = await db.StorageInventoryLedger
                            .Where(l => l.StorageLocationId == destLocId && l.RawMaterialId == item.RawMaterialId)
                            .SumAsync(l => (decimal?)l.Quantity) ?? 0m;
                        var destNewStock = destCurrentStock + item.Quantity;

                        var destBatch = new WarehouseBatch
                        {
                            LaboratoryReportId = batch?.LaboratoryReportId ?? 0,
                            ProductionReferenceNumber = item.ProductionReferenceNumber ?? batch?.ProductionReferenceNumber ?? op.ProductionReferenceNumber ?? 0,
                            SampleCode = item.SampleCode ?? batch?.SampleCode ?? op.SampleCode ?? string.Empty,
                            BatchNumber = $"{item.BatchNumber}-T{op.TransactionNumber}",
                            RawMaterialId = item.RawMaterialId,
                            MaterialName = item.MaterialName,
                            MaterialClassification = batch?.MaterialClassification ?? "MECHANICAL",
                            SupplierName = batch?.SupplierName,
                            StorageLocationId = destLocId,
                            InitialQuantity = item.Quantity,
                            CurrentQuantity = item.Quantity,
                            StorageOperationId = op.Id,
                            StorageOperationItemId = item.Id,
                            ImportDate = op.TransactionDate,
                            Status = "APPROVED",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = reviewerName,
                            Notes = $"محول من {op.StorageLocationName} بإذن تحويل #{op.TransactionNumber}"
                        };
                        await db.WarehouseBatches.AddAsync(destBatch);
                        await db.SaveChangesAsync();

                        var destLedgerEntry = new StorageInventoryLedger
                        {
                            StorageLocationId = destLocId,
                            RawMaterialId = item.RawMaterialId,
                            StorageOperationId = op.Id,
                            StorageOperationItemId = item.Id,
                            WarehouseBatchId = destBatch.Id,
                            BatchNumber = destBatch.BatchNumber,
                            MovementType = "TRANSFER",
                            Quantity = item.Quantity, // Inflow into destination warehouse
                            RunningBalance = destNewStock,
                            MovementDate = op.TransactionDate,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = reviewerName
                        };
                        await db.StorageInventoryLedger.AddAsync(destLedgerEntry);
                    }
                    else
                    {
                        // Create Production WIP record
                        var wip = new ProductionWorkInProcess
                        {
                            StorageOperationId = op.Id,
                            StorageOperationItemId = item.Id,
                            RawMaterialId = item.RawMaterialId,
                            WarehouseBatchId = item.WarehouseBatchId ?? batch?.Id,
                            BatchNumber = item.BatchNumber ?? batch?.BatchNumber,
                            SampleCode = item.SampleCode ?? batch?.SampleCode ?? op.SampleCode,
                            ProductionReferenceNumber = item.ProductionReferenceNumber ?? batch?.ProductionReferenceNumber ?? op.ProductionReferenceNumber,
                            TrackingNumber = !string.IsNullOrWhiteSpace(item.TrackingNumber) ? item.TrackingNumber : $"TRF-{op.TransactionDate:yyyy}-{op.TransactionNumber:D4}-L{item.Id}",
                            MaterialQuantityIssued = item.Quantity,
                            SourceStorageLocation = op.StorageLocationName,
                            TransferDate = op.TransactionDate,
                            Status = "قيد التشغيل",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = reviewerName
                        };

                        await db.ProductionWorkInProcess.AddAsync(wip);
                    }
                }
            }

            // Update operation status
            op.ApprovalStatus = "APPROVED";
            op.IsInventoryPosted = true;
            op.InventoryPostedAt = DateTime.UtcNow;
            op.ReviewedBy = reviewerName;
            op.ReviewedByUserId = reviewerId;
            op.ReviewedAt = DateTime.UtcNow;
            op.RejectionReason = null;

            // Mark the Laboratory traceability source as received exactly once (idempotent with IsInventoryPosted guard above)
            if (opType == "IMPORT")
            {
                LaboratoryFinalConformityCertificate? finalCert = null;
                if (!string.IsNullOrEmpty(op.SampleCode))
                {
                    finalCert = await db.LaboratoryFinalConformityCertificates
                        .FirstOrDefaultAsync(c => c.CertificateNumber == op.SampleCode);
                }
                if (finalCert == null && op.ProductionReferenceNumber.HasValue)
                {
                    finalCert = await db.LaboratoryFinalConformityCertificates
                        .FirstOrDefaultAsync(c => c.ProductionReferenceNumber == op.ProductionReferenceNumber.Value &&
                                                  c.ApprovalStatus == "APPROVED" && c.ConformityDecision == "CONFORMS");
                }

                if (finalCert != null)
                {
                    if (!finalCert.IsStoragePosted)
                    {
                        finalCert.IsStoragePosted = true;
                        finalCert.StoragePostedAt = DateTime.UtcNow;
                        logger.LogInformation("Final Product Conformity Certificate {Cert} posted to finished-product storage via Storage Operation #{Tx}.",
                            finalCert.CertificateNumber, op.TransactionNumber);
                    }

                    if (finalCert.ProductionFinishedProductId.HasValue)
                    {
                        var fp = await db.ProductionFinishedProducts.FirstOrDefaultAsync(f => f.Id == finalCert.ProductionFinishedProductId.Value);
                        if (fp != null && !fp.IsStorageReceived)
                        {
                            fp.IsStorageReceived = true;
                            fp.WarehouseImportOperationId = op.Id;
                            fp.StorageReceivedDate = DateTime.UtcNow;
                        }
                    }
                }
                else if (op.ProductionReferenceNumber.HasValue)
                {
                    var refNo = op.ProductionReferenceNumber.Value;
                    var labRaw = await db.LaboratoryRawMaterialReports
                        .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == refNo);
                    if (labRaw != null && !labRaw.IsStorageReceived)
                    {
                        labRaw.IsStorageReceived = true;
                        labRaw.StorageReceivedAt = DateTime.UtcNow;
                        labRaw.StorageOperationId = op.Id;
                        logger.LogInformation("Laboratory Raw Material Report (Ref #{Ref}) marked as storage-received via Storage Operation #{Tx}.",
                            refNo, op.TransactionNumber);
                    }
                }
            }

            // Add to FormApprovalHistories
            var history = new FormApprovalHistory
            {
                FormType = "Storage",
                FormId = op.Id,
                TransactionOrMovementNumber = $"#{op.TransactionNumber}",
                PreviousStatus = prevStatus,
                NewStatus = "APPROVED",
                Action = "APPROVED",
                ActorName = reviewerName,
                ActorUserId = reviewerId,
                ActorRole = reviewerRole,
                Comment = $"تم اعتماد حركة المستودع ({op.OperationType}) وترحيل الرصيد بنجاح.",
                Timestamp = DateTime.UtcNow
            };

            await db.FormApprovalHistories.AddAsync(history);
            await db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            logger.LogInformation("Storage Operation #{Tx} (ID: {Id}) approved and posted by {Rev}.",
                op.TransactionNumber, op.Id, reviewerName);

            var dto = MapOperationToDto(op, op.Items.ToList());

            // SignalR broadcast
            try
            {
                var payload = new
                {
                    FormType = "Storage",
                    FormId = op.Id,
                    OperationType = op.OperationType,
                    TransactionNumber = $"#{op.TransactionNumber}",
                    Status = "APPROVED",
                    ReviewedBy = reviewerName,
                    Timestamp = DateTime.UtcNow
                };
                await hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", payload);
                await hubContext.Clients.All.SendAsync("OnFormApprovalStatusChanged", payload);
                await hubContext.Clients.All.SendAsync("OnFormApproved", payload);
                await hubContext.Clients.All.SendAsync("OnWarehouseInventoryChanged", new
                {
                    Action = op.OperationType,
                    OperationId = op.Id,
                    TransactionNumber = $"#{op.TransactionNumber}",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception sEx)
            {
                logger.LogWarning(sEx, "SignalR notification failed for approved operation.");
            }

            return (true, null, dto);
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            logger.LogError(ex, "Failed to approve Storage Operation #{Id}.", operationId);
            return (false, "حدث خطأ أثناء معالجة اعتماد الحركة.", null);
        }
    }

    public static async Task<(bool Success, string? ErrorMessage, StorageOperationDto? Dto)> RejectStorageOperationCoreAsync(
        FaceStoneDbContext db,
        ILogger logger,
        IHubContext<FaceStone.Web.Hubs.ChatHub> hubContext,
        int operationId,
        string reason,
        string reviewerName,
        Guid? reviewerId,
        string reviewerRole)
    {
        var op = await db.StorageOperations
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == operationId);

        if (op == null)
        {
            return (false, "حركة المستودع غير موجودة.", null);
        }

        if (op.ApprovalStatus == "APPROVED" || op.IsInventoryPosted)
        {
            return (false, "لا يمكن رفض حركة تم اعتمادها وترحيل رصيدها بالفعل.", null);
        }

        var prevStatus = op.ApprovalStatus;
        op.ApprovalStatus = "REJECTED";
        op.RejectionReason = reason;
        op.ReviewedBy = reviewerName;
        op.ReviewedByUserId = reviewerId;
        op.ReviewedAt = DateTime.UtcNow;

        if (op.OperationType.ToUpperInvariant() == "IMPORT")
        {
            var batches = await db.WarehouseBatches.Where(b => b.StorageOperationId == op.Id).ToListAsync();
            foreach (var b in batches)
            {
                b.Status = "REJECTED";
                b.IsActive = false;
                b.CurrentQuantity = 0m;
            }
        }

        var history = new FormApprovalHistory
        {
            FormType = "Storage",
            FormId = op.Id,
            TransactionOrMovementNumber = $"#{op.TransactionNumber}",
            PreviousStatus = prevStatus,
            NewStatus = "REJECTED",
            Action = "REJECTED",
            ActorName = reviewerName,
            ActorUserId = reviewerId,
            ActorRole = reviewerRole,
            Comment = $"تم رفض حركة المستودع: {reason}",
            Timestamp = DateTime.UtcNow
        };

        await db.FormApprovalHistories.AddAsync(history);
        await db.SaveChangesAsync();

        var dto = MapOperationToDto(op, op.Items.ToList());

        // SignalR broadcast
        try
        {
            var payload = new
            {
                FormType = "Storage",
                FormId = op.Id,
                OperationType = op.OperationType,
                TransactionNumber = $"#{op.TransactionNumber}",
                Status = "REJECTED",
                RejectionReason = reason,
                ReviewedBy = reviewerName,
                Timestamp = DateTime.UtcNow
            };
            await hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", payload);
            await hubContext.Clients.All.SendAsync("OnFormApprovalStatusChanged", payload);
            await hubContext.Clients.All.SendAsync("OnFormRejected", payload);
            if (op.CreatedByUserId.HasValue)
            {
                await hubContext.Clients.Group($"Employee_{op.CreatedByUserId.Value}").SendAsync("OnFormRejectedForEmployee", payload);
            }
        }
        catch (Exception sEx)
        {
            logger.LogWarning(sEx, "SignalR notification failed for rejected operation.");
        }

        return (true, null, dto);
    }

    public static async Task<(bool Success, string? ErrorMessage, StorageOperationDto? Dto)> ResubmitStorageOperationCoreAsync(
        FaceStoneDbContext db,
        ILogger logger,
        IHubContext<FaceStone.Web.Hubs.ChatHub> hubContext,
        int operationId,
        string resubmitterName,
        Guid? resubmitterId)
    {
        var op = await db.StorageOperations
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == operationId);

        if (op == null)
        {
            return (false, "حركة المستودع غير موجودة.", null);
        }

        if (op.ApprovalStatus == "APPROVED" || op.IsInventoryPosted)
        {
            return (false, "لا يمكن إعادة إرسال حركة معتمدة بالفعل.", null);
        }

        var prevStatus = op.ApprovalStatus;
        op.ApprovalStatus = "PENDING";
        op.RejectionReason = null;
        op.ReviewedBy = null;
        op.ReviewedByUserId = null;
        op.ReviewedAt = null;

        var history = new FormApprovalHistory
        {
            FormType = "Storage",
            FormId = op.Id,
            TransactionOrMovementNumber = $"#{op.TransactionNumber}",
            PreviousStatus = prevStatus,
            NewStatus = "PENDING",
            Action = "RESUBMITTED",
            ActorName = resubmitterName,
            ActorUserId = resubmitterId,
            Comment = "تمت إعادة إرسال الحركة للمراجعة والاعتماد بعد المراجعة.",
            Timestamp = DateTime.UtcNow
        };

        await db.FormApprovalHistories.AddAsync(history);
        await db.SaveChangesAsync();

        var dto = MapOperationToDto(op, op.Items.ToList());

        try
        {
            var payload = new
            {
                FormType = "Storage",
                FormId = op.Id,
                OperationType = op.OperationType,
                TransactionNumber = $"#{op.TransactionNumber}",
                Status = "PENDING",
                ResubmittedBy = resubmitterName,
                Timestamp = DateTime.UtcNow
            };
            await hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", payload);
            await hubContext.Clients.All.SendAsync("OnFormApprovalStatusChanged", payload);
            await hubContext.Clients.All.SendAsync("OnFormSubmittedForReview", payload);
        }
        catch (Exception sEx)
        {
            logger.LogWarning(sEx, "SignalR notification failed for resubmitted operation.");
        }

        return (true, null, dto);
    }

    // =========================================================================
    // PRIVATE STORAGE CONTROLLER HELPERS
    // =========================================================================

    private async Task<int> GetNextSequenceNumberAsync(string sequenceType)
    {
        var upper = sequenceType.Trim().ToUpperInvariant();
        var nextValParam = new Microsoft.Data.SqlClient.SqlParameter("@nextVal", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        var typeParam = new Microsoft.Data.SqlClient.SqlParameter("@seqType", upper);

        await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE StorageSequences WITH (UPDLOCK, ROWLOCK)
            SET @nextVal = NextValue, NextValue = NextValue + 1
            WHERE SequenceType = @seqType;
            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO StorageSequences (SequenceType, NextValue) VALUES (@seqType, 2);
                SET @nextVal = 1;
            END", typeParam, nextValParam);

        return (int)nextValParam.Value;
    }

    private async Task<int> PeekNextSequenceNumberAsync(string sequenceType)
    {
        var upper = sequenceType.Trim().ToUpperInvariant();
        var seq = await _db.StorageSequences.AsNoTracking().FirstOrDefaultAsync(s => s.SequenceType == upper);
        if (seq != null) return seq.NextValue;
        return 1;
    }

    private async Task<List<string>> ReserveBatchNumbersAsync(int count)
    {
        if (count <= 0) return new List<string>();
        var countParam = new Microsoft.Data.SqlClient.SqlParameter("@count", count);
        var startValParam = new Microsoft.Data.SqlClient.SqlParameter("@startVal", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        var typeParam = new Microsoft.Data.SqlClient.SqlParameter("@seqType", "BATCH");

        await _db.Database.ExecuteSqlRawAsync(@"
            UPDATE StorageSequences WITH (UPDLOCK, ROWLOCK)
            SET @startVal = NextValue, NextValue = NextValue + @count
            WHERE SequenceType = @seqType;
            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO StorageSequences (SequenceType, NextValue) VALUES (@seqType, 1 + @count);
                SET @startVal = 1;
            END", typeParam, countParam, startValParam);

        int startVal = (int)startValParam.Value;
        var batchNumbers = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            batchNumbers.Add($"B-{(startVal + i):D4}");
        }
        return batchNumbers;
    }

    private async Task<List<string>> PeekNextBatchNumbersAsync(int count)
    {
        if (count <= 0) count = 1;
        var seq = await _db.StorageSequences.AsNoTracking().FirstOrDefaultAsync(s => s.SequenceType == "BATCH");
        int nextVal = seq?.NextValue ?? 1;
        var batchNumbers = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            batchNumbers.Add($"B-{(nextVal + i):D4}");
        }
        return batchNumbers;
    }

    public static WarehouseBatchDto MapBatchToDto(WarehouseBatch b)
    {
        return new WarehouseBatchDto
        {
            Id = b.Id,
            LaboratoryReportId = b.LaboratoryReportId,
            ProductionReferenceNumber = b.ProductionReferenceNumber,
            SampleCode = b.SampleCode,
            BatchNumber = b.BatchNumber,
            RawMaterialId = b.RawMaterialId,
            MaterialName = b.MaterialName,
            MaterialClassification = b.MaterialClassification,
            SupplierName = b.SupplierName,
            StorageLocationId = b.StorageLocationId,
            StorageLocationName = b.StorageLocation != null ? b.StorageLocation.Name : string.Empty,
            InitialQuantity = b.InitialQuantity,
            CurrentQuantity = b.CurrentQuantity,
            StorageOperationId = b.StorageOperationId,
            StorageOperationTransactionNumber = b.StorageOperation != null ? b.StorageOperation.TransactionNumber : null,
            ImportDate = b.ImportDate,
            Status = b.Status,
            IsActive = b.IsActive,
            CreatedAt = b.CreatedAt,
            CreatedBy = b.CreatedBy,
            Notes = b.Notes
        };
    }

    private async Task<decimal> GetAvailableStockAsync(int locationId, int rawMaterialId)
    {
        return await _db.StorageInventoryLedger
            .Where(l => l.StorageLocationId == locationId && l.RawMaterialId == rawMaterialId)
            .SumAsync(l => (decimal?)l.Quantity) ?? 0m;
    }

    private (bool IsAuth, string Role, string UserName, Guid? UserId) GetAuthenticatedUser()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasStorageAccess(role))
        {
            return (false, role, string.Empty, null);
        }

        var userName = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "مسؤول المستودع";
        Guid? userId = null;
        if (Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var g))
        {
            userId = g;
        }

        return (true, role, userName, userId);
    }

    public static StorageOperationDto MapOperationToDto(StorageOperation op, List<StorageOperationItem> items)
    {
        var effectiveItems = (items != null && items.Count > 0)
            ? items
            : (op.Items != null && op.Items.Count > 0 ? op.Items.ToList() : new List<StorageOperationItem>());

        var attachments = new List<StorageAttachmentItemDto>();
        if (!string.IsNullOrWhiteSpace(op.WeighbridgeAttachmentPath) || !string.IsNullOrWhiteSpace(op.WeighbridgeFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(op.WeighbridgeAttachmentPath)
                ? Path.GetFileName(op.WeighbridgeAttachmentPath)
                : op.WeighbridgeFileName!;
            attachments.Add(new StorageAttachmentItemDto
            {
                Category = "Weighbridge",
                Title = "إيصال الميزان",
                FileName = op.WeighbridgeFileName ?? fn,
                FileType = op.WeighbridgeFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = op.WeighbridgeFileSize,
                ViewUrl = $"/api/storage/attachments/{fn}",
                DownloadUrl = $"/api/storage/attachments/{fn}?download=true"
            });
        }

        if (!string.IsNullOrWhiteSpace(op.QocAttachmentPath) || !string.IsNullOrWhiteSpace(op.QocFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(op.QocAttachmentPath)
                ? Path.GetFileName(op.QocAttachmentPath)
                : op.QocFileName!;
            attachments.Add(new StorageAttachmentItemDto
            {
                Category = "QOC",
                Title = "تقرير QOC",
                FileName = op.QocFileName ?? fn,
                FileType = op.QocFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = op.QocFileSize,
                ViewUrl = $"/api/storage/attachments/{fn}",
                DownloadUrl = $"/api/storage/attachments/{fn}?download=true"
            });
        }

        if (!string.IsNullOrWhiteSpace(op.DocumentAttachmentPath) || !string.IsNullOrWhiteSpace(op.DocumentFileName))
        {
            var fn = !string.IsNullOrWhiteSpace(op.DocumentAttachmentPath)
                ? Path.GetFileName(op.DocumentAttachmentPath)
                : op.DocumentFileName!;
            attachments.Add(new StorageAttachmentItemDto
            {
                Category = "Document",
                Title = "المستند الورقي",
                FileName = op.DocumentFileName ?? fn,
                FileType = op.DocumentFileType ?? (fn.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "PDF" : "Image"),
                FileSize = op.DocumentFileSize,
                ViewUrl = $"/api/storage/attachments/{fn}",
                DownloadUrl = $"/api/storage/attachments/{fn}?download=true"
            });
        }

        return new StorageOperationDto
        {
            Id = op.Id,
            OperationType = op.OperationType,
            TransactionNumber = op.TransactionNumber,
            TransactionDate = op.TransactionDate,
            StorageLocationId = op.StorageLocationId,
            StorageLocationName = op.StorageLocationName,
            MaterialType = op.MaterialType,
            ProductionReferenceNumber = op.ProductionReferenceNumber,
            ProductionOrderId = op.ProductionOrderId,
            ProductionOrderNumber = op.ProductionOrder?.OrderNumber,
            ProductionProcessId = op.ProductionProcessId,
            ProcessNumber = op.ProductionProcess?.ProcessNumber,
            ManufactureNumber = op.ManufactureNumber,
            ExportType = op.ExportType,
            SampleCode = op.SampleCode,
            SelectionMode = op.SelectionMode,
            Batches = (op.CreatedBatches ?? new List<WarehouseBatch>()).Select(MapBatchToDto).ToList(),
            Source = op.Source,
            SourcePhone = op.SourcePhone,
            ClientId = op.ClientId,
            ClientName = op.ClientName,
            DestinationId = op.DestinationId,
            DestinationName = op.DestinationName,
            ClientPhone = op.ClientPhone,
            DestinationDepartment = op.DestinationDepartment,
            ResponsibleEmployee = op.ResponsibleEmployee,
            ResponsibleUserId = op.ResponsibleUserId,
            Notes = op.Notes,
            ApprovalStatus = op.ApprovalStatus,
            ReviewedBy = op.ReviewedBy,
            ReviewedByUserId = op.ReviewedByUserId,
            ReviewedAt = op.ReviewedAt,
            RejectionReason = op.RejectionReason,
            IsInventoryPosted = op.IsInventoryPosted,
            InventoryPostedAt = op.InventoryPostedAt,
            CreatedAt = op.CreatedAt,
            CreatedBy = op.CreatedBy,

            // Internal Weighing
            ExternalReportedWeight = op.ExternalReportedWeight,
            InternalLoadedWeight = op.InternalLoadedWeight,
            InternalEmptyWeight = op.InternalEmptyWeight,
            InternalNetWeight = op.InternalNetWeight,
            InternalWeighingTime = op.InternalWeighingTime,
            WeighbridgeReference = op.WeighbridgeReference,
            WeighingResponsibleEmployee = op.WeighingResponsibleEmployee,
            WeightDifference = op.WeightDifference,
            WeightDifferencePercentage = op.WeightDifferencePercentage,
            IsDiscrepancyFlagged = op.IsDiscrepancyFlagged,
            DiscrepancyExplanation = op.DiscrepancyExplanation,
            TransportationId = op.TransportationId,

            TotalQuantity = effectiveItems.Sum(i => i.Quantity),
            Items = effectiveItems.Select(i => new StorageOperationItemDto
            {
                Id = i.Id,
                StorageOperationId = i.StorageOperationId,
                RawMaterialId = i.RawMaterialId,
                WarehouseBatchId = i.WarehouseBatchId,
                BatchNumber = i.BatchNumber,
                SampleCode = i.SampleCode,
                ProductionReferenceNumber = i.ProductionReferenceNumber,
                MaterialName = i.MaterialName,
                Quantity = i.Quantity,
                TrackingNumber = i.TrackingNumber,
                ProductionOrderId = i.ProductionOrderId,
                ProductionProcessId = i.ProductionProcessId,
                ManufactureNumber = i.ManufactureNumber,
                Notes = i.Notes
            }).ToList(),

            WeighbridgeAttachmentPath = op.WeighbridgeAttachmentPath,
            WeighbridgeFileName = op.WeighbridgeFileName,
            WeighbridgeFileType = op.WeighbridgeFileType,
            WeighbridgeFileSize = op.WeighbridgeFileSize,

            QocAttachmentPath = op.QocAttachmentPath,
            QocFileName = op.QocFileName,
            QocFileType = op.QocFileType,
            QocFileSize = op.QocFileSize,

            DocumentAttachmentPath = op.DocumentAttachmentPath,
            DocumentFileName = op.DocumentFileName,
            DocumentFileType = op.DocumentFileType,
            DocumentFileSize = op.DocumentFileSize,

            Attachments = attachments,

            ProductionBatches = (op.ProductionBatches ?? new List<ProductionWorkInProcess>()).Select(p => new ProductionWorkInProcessDto
            {
                Id = p.Id,
                StorageOperationId = p.StorageOperationId,
                StorageOperationItemId = p.StorageOperationItemId,
                RawMaterialId = p.RawMaterialId,
                MaterialName = p.RawMaterial?.Name ?? (p.StorageOperationItem != null ? p.StorageOperationItem.MaterialName : "Unknown"),
                TrackingNumber = p.TrackingNumber,
                MaterialQuantityIssued = p.MaterialQuantityIssued,
                SourceStorageLocation = p.SourceStorageLocation,
                TransferDate = p.TransferDate,
                Status = p.Status,
                FinalProductName = p.FinalProductName,
                FinalProductQuantity = p.FinalProductQuantity,
                WasteQuantity = p.WasteQuantity,
                WastePercentage = p.WastePercentage,
                WasteClassification = p.WasteClassification,
                RecoverableProductName = p.RecoverableProductName,
                RecoverableMaterialId = p.RecoverableMaterialId,
                RecoverableQuantity = p.RecoverableQuantity,
                RecoverableUnit = p.RecoverableUnit,
                DestinationStorageLocationId = p.DestinationStorageLocationId,
                DestinationStorageLocationName = p.DestinationStorageLocationName,
                IsRecoveredStockPosted = p.IsRecoveredStockPosted,
                RecoveredStockPostedAt = p.RecoveredStockPostedAt,
                OutputResponsibleEmployee = p.OutputResponsibleEmployee,
                ClassificationHistory = p.ClassificationHistory,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt
            }).ToList()
        };
    }

    private async Task NotifyOperationChangeAsync(string opType, int id, string txNumber, string status, string actor, string comment)
    {
        try
        {
            var payload = new
            {
                FormType = "Storage",
                FormId = id,
                OperationType = opType,
                TransactionNumber = txNumber,
                Status = status,
                Actor = actor,
                Comment = comment,
                Timestamp = DateTime.UtcNow
            };
            await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", payload);
            await _hubContext.Clients.All.SendAsync("OnFormApprovalStatusChanged", payload);
            await _hubContext.Clients.All.SendAsync("OnFormSubmittedForReview", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast SignalR notification for Storage Operation #{Id}.", id);
        }
    }

    // =========================================================================
    // PRODUCTION DEPARTMENT INTEGRATIONS (طلبات صرف المواد للإنتاج)
    // =========================================================================
    /// <summary>
    /// Retrieve pending production material requests for Storage review
    /// </summary>
    [HttpGet("production-requests")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingProductionRequests()
    {
        var orders = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.StorageLocation)
            .Include(o => o.Items)
                .ThenInclude(i => i.RawMaterial)
            .Where(o => o.Status == "PENDING_STORAGE_APPROVAL" || o.Status == "PARTIALLY_ISSUED")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var dtos = orders.Select(ProductionController.MapOrderToDto).ToList();
        return Ok(ApiResponse<List<ProductionOrderDto>>.Ok(dtos));
    }

    /// <summary>
    /// Storage Manager reviews and issues raw materials for a Production Order.
    /// Strictly verifies available stock, creates TRANSFER storage operation,
    /// posts negative movement in StorageInventoryLedger, and updates Production Order.
    /// </summary>
    [HttpPost("production-requests/{id:int}/issue")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IssueMaterialsForProduction(int id, [FromBody] Dictionary<string, string>? payload = null)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم المخازن."));
        }

        var order = await _db.ProductionOrders
            .Include(o => o.Items)
                .ThenInclude(i => i.RawMaterial)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(ApiResponse.Fail($"طلب الإنتاج رقم #{id} غير موجود."));
        }

        if (order.IsStorageApproved && order.Status != "PARTIALLY_ISSUED" && order.Status != "PENDING_STORAGE_APPROVAL")
        {
            return BadRequest(ApiResponse.Fail("تم صرف هذا الطلب مسبقاً ولا يمكن تكرار الصرف."));
        }

        var locId = order.StorageLocationId;
        var location = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == locId);
        if (location == null)
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين المحدد للطلب غير موجود."));
        }

        // Verify stock for all order items
        foreach (var item in order.Items)
        {
            var requiredQty = item.RequestedQuantity - item.IssuedQuantity;
            if (requiredQty <= 0) continue;

            var availStock = await GetAvailableStockAsync(locId, item.RawMaterialId);
            if (availStock < requiredQty)
            {
                return BadRequest(ApiResponse.Fail($"الرصيد المتاح للصنف '{item.MaterialName}' في {location.Name} ({availStock:N3} طن) غير كافٍ لصرف الكمية المطلوبة ({requiredQty:N3} طن)."));
            }
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var txNumber = await GetNextSequenceNumberAsync("TRANSFER");
            var txDate = DateTime.UtcNow;

            var op = new StorageOperation
            {
                OperationType = "TRANSFER",
                TransactionNumber = txNumber,
                TransactionDate = txDate,
                StorageLocationId = location.Id,
                StorageLocationName = location.Name,
                DestinationDepartment = "الإنتاج",
                ProductionReferenceNumber = order.ProductionReferenceNumber,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                Notes = $"صرف مواد خام لأمر الإنتاج #{order.OrderNumber} ({order.ProductName})",
                ApprovalStatus = "APPROVED",
                IsInventoryPosted = true,
                InventoryPostedAt = DateTime.UtcNow,
                ReviewedBy = userName,
                ReviewedByUserId = userId,
                ReviewedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                CreatedByUserId = userId
            };

            await _db.StorageOperations.AddAsync(op);
            await _db.SaveChangesAsync();

            int lineIdx = 1;
            foreach (var item in order.Items)
            {
                var issueQty = item.RequestedQuantity - item.IssuedQuantity;
                if (issueQty <= 0) continue;

                var tracking = $"TRF-{txDate:yyyy}-{txNumber:D4}-L{lineIdx}";
                var opItem = new StorageOperationItem
                {
                    StorageOperationId = op.Id,
                    RawMaterialId = item.RawMaterialId,
                    MaterialName = item.MaterialName,
                    Quantity = issueQty,
                    TrackingNumber = tracking,
                    Notes = $"صرف لأمر الإنتاج #{order.OrderNumber}"
                };

                await _db.StorageOperationItems.AddAsync(opItem);
                await _db.SaveChangesAsync();

                // Deduct stock in StorageInventoryLedger
                var currentStock = await _db.StorageInventoryLedger
                    .Where(l => l.StorageLocationId == location.Id && l.RawMaterialId == item.RawMaterialId)
                    .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

                var newStock = currentStock - issueQty;

                var ledgerEntry = new StorageInventoryLedger
                {
                    StorageLocationId = location.Id,
                    RawMaterialId = item.RawMaterialId,
                    StorageOperationId = op.Id,
                    StorageOperationItemId = opItem.Id,
                    MovementType = "TRANSFER",
                    Quantity = -issueQty, // Negative for transfer issue
                    RunningBalance = newStock,
                    MovementDate = txDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                };

                await _db.StorageInventoryLedger.AddAsync(ledgerEntry);

                // Update item issued quantity
                item.IssuedQuantity += issueQty;
                lineIdx++;
            }

            order.IsStorageApproved = true;
            order.StorageApprovedAt = DateTime.UtcNow;
            order.StorageApprovedBy = userName;
            order.StorageOperationId = op.Id;
            order.Status = "READY_FOR_PRODUCTION";

            await _db.SaveChangesAsync();

            // Create ProductionWorkInProcess entity for traceability
            var firstItem = order.Items.FirstOrDefault();
            var firstOpItem = op.Items.FirstOrDefault();
            if (firstItem != null && firstOpItem != null)
            {
                var totalIssued = order.Items.Sum(i => i.IssuedQuantity);
                var wip = new ProductionWorkInProcess
                {
                    StorageOperationId = op.Id,
                    StorageOperationItemId = firstOpItem.Id,
                    RawMaterialId = firstItem.RawMaterialId,
                    ProductionReferenceNumber = order.ProductionReferenceNumber,
                    TrackingNumber = $"TRF-{txDate:yyyy}-{txNumber:D4}",
                    MaterialQuantityIssued = totalIssued,
                    SourceStorageLocation = location.Name,
                    TransferDate = txDate,
                    Status = "قيد التشغيل",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                };

                await _db.ProductionWorkInProcess.AddAsync(wip);
                await _db.SaveChangesAsync();
            }

            await dbTx.CommitAsync();

            _logger.LogInformation("Materials issued for Production Order {OrderNumber} via Storage Transfer #{TxNumber} by {User}.",
                order.OrderNumber, op.TransactionNumber, userName);

            await NotifyOperationChangeAsync("TRANSFER", op.Id, $"#{op.TransactionNumber}", "APPROVED", userName,
                $"تم صرف المواد الخام لأمر الإنتاج #{order.OrderNumber} وترحيل المخزون.");

            return Ok(ApiResponse<ProductionOrderDto>.Ok(ProductionController.MapOrderToDto(order),
                $"تم صرف المواد بنجاح وترحيل حركة المخازن بإذن تحويل #{op.TransactionNumber}."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to issue materials for Production Order #{Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء صرف المواد للإنتاج."));
        }
    }

    /// <summary>
    /// Storage Manager rejects a Production Material Request
    /// </summary>
    [HttpPost("production-requests/{id:int}/reject")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectProductionRequest(int id, [FromBody] Dictionary<string, string>? payload = null)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم المخازن."));
        }

        var order = await _db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound(ApiResponse.Fail($"طلب الإنتاج رقم #{id} غير موجود."));
        }

        var reason = payload != null && payload.TryGetValue("reason", out var rVal) ? rVal : "عدم كفاية الرصيد أو رفض أمين المستودع";

        order.Status = "CANCELLED";
        order.Notes = string.IsNullOrWhiteSpace(order.Notes)
            ? $"[تم رفض الطلب بواسطة أمين المستودع ({userName}): {reason}]"
            : $"{order.Notes} | [تم رفض الطلب بواسطة أمين المستودع ({userName}): {reason}]";

        await _db.SaveChangesAsync();
        _logger.LogInformation("Production Order {OrderNumber} rejected by Storage Manager {User}.", order.OrderNumber, userName);

        return Ok(ApiResponse<ProductionOrderDto>.Ok(ProductionController.MapOrderToDto(order), "تم رفض طلب المواد من قِبل إدارة المستودعات."));
    }

    // =========================================================================
    // 12. LABORATORY PRODUCTION ORDERS WAREHOUSE TRANSFER INTEGRATION
    // =========================================================================

    /// <summary>
    /// Gets all Laboratory Production Orders waiting for physical transfer to production.
    /// </summary>
    [HttpGet("production-orders/pending-transfers")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryProductionOrderDetailDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingProductionOrderTransfers()
    {
        var orders = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Transfers)
            .Where(o => o.Status == "PENDING_TRANSFER" || o.Status == "PARTIALLY_TRANSFERRED")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var batchIds = orders.SelectMany(o => o.Items)
            .Select(i => i.WarehouseBatchId ?? 0)
            .Where(b => b > 0)
            .Distinct()
            .ToList();

        var batchNumbers = orders.SelectMany(o => o.Items)
            .Select(i => i.BatchNumber)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct()
            .ToList();

        var currentBatches = await _db.WarehouseBatches
            .AsNoTracking()
            .Where(b => batchIds.Contains(b.Id) || (b.BatchNumber != null && batchNumbers.Contains(b.BatchNumber)))
            .ToListAsync();

        var currentBatchesById = currentBatches.ToDictionary(b => b.Id, b => b.CurrentQuantity);
        var currentBatchesByNumber = currentBatches
            .Where(b => !string.IsNullOrWhiteSpace(b.BatchNumber))
            .GroupBy(b => b.BatchNumber!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CurrentQuantity).First().CurrentQuantity);

        var result = orders.Select(order => new LaboratoryProductionOrderDetailDto
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
            StatusArabic = order.Status == "PENDING_TRANSFER" ? "في انتظار نقل المستودع" : "تم النقل جزئياً",
            LaboratoryManagerName = order.LaboratoryManagerName ?? order.CreatedBy,
            FormulaVersion = order.FormulaVersion,
            ProductionInstructions = order.ProductionInstructions,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            ItemsCount = order.Items.Count,
            Items = order.Items.Select(i => {
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
            }).ToList(),
            Transfers = order.Transfers.OrderByDescending(t => t.ConfirmedAt).Select(t => new LaboratoryProductionOrderTransferDto
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
            }).ToList()
        }).ToList();

        return Ok(ApiResponse<List<LaboratoryProductionOrderDetailDto>>.Ok(result));
    }

    /// <summary>
    /// Warehouse Manager confirms physical transfer of raw materials to Production.
    /// Supports partial transfers: deducts only confirmed quantities from WarehouseBatches and StorageInventoryLedger.
    /// </summary>
    [HttpPost("production-orders/{id:int}/confirm-transfer")]
    [ProducesResponseType(typeof(ApiResponse<LaboratoryProductionOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmProductionOrderTransfer(int id, [FromBody] ConfirmWarehouseTransferRequestDto request)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بتأكيد نقل المواد من المستودع."));
        }

        var order = await _db.ProductionOrders
            .Include(o => o.Items)
            .Include(o => o.Transfers)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(ApiResponse.Fail("أمر الإنتاج المطلوب غير موجود."));
        }

        if (order.Status != "PENDING_TRANSFER" && order.Status != "PARTIALLY_TRANSFERRED")
        {
            return BadRequest(ApiResponse.Fail("لا يمكن نقل هذا الأمر لأنه في حالة: " + order.Status));
        }

        if (request.Items == null || request.Items.Count == 0 || !request.Items.Any(i => i.TransferQuantity > 0))
        {
            return BadRequest(ApiResponse.Fail("يجب تحديد كمية نقل أكبر من الصفر لمادة واحدة على الأقل."));
        }

        // Fallback: If request items have WarehouseBatchId <= 0, resolve from order item
        foreach (var reqItem in request.Items.Where(i => i.TransferQuantity > 0))
        {
            var orderItem = order.Items.FirstOrDefault(oi => oi.Id == reqItem.ProductionOrderItemId);
            if (orderItem != null && reqItem.WarehouseBatchId <= 0 && orderItem.WarehouseBatchId.HasValue && orderItem.WarehouseBatchId.Value > 0)
            {
                reqItem.WarehouseBatchId = orderItem.WarehouseBatchId.Value;
            }
        }

        // Validate items and quantities
        var validTransferItems = request.Items.Where(i => i.TransferQuantity > 0).ToList();
        var batchIds = validTransferItems.Select(i => i.WarehouseBatchId).Where(b => b > 0).Distinct().ToList();
        var batches = await _db.WarehouseBatches
            .Include(b => b.StorageLocation)
            .Where(b => batchIds.Contains(b.Id))
            .ToListAsync();

        // Fallback: Check if any items can be resolved by BatchNumber
        if (batches.Count < batchIds.Count)
        {
            var missingBatchNumbers = order.Items
                .Where(oi => !string.IsNullOrWhiteSpace(oi.BatchNumber) && !batches.Any(b => b.Id == oi.WarehouseBatchId))
                .Select(oi => oi.BatchNumber!)
                .Distinct()
                .ToList();

            if (missingBatchNumbers.Count > 0)
            {
                var extraBatches = await _db.WarehouseBatches
                    .Include(b => b.StorageLocation)
                    .Where(b => missingBatchNumbers.Contains(b.BatchNumber))
                    .ToListAsync();
                batches.AddRange(extraBatches.Where(eb => !batches.Any(b => b.Id == eb.Id)));
            }
        }

        foreach (var reqItem in validTransferItems)
        {
            var orderItem = order.Items.FirstOrDefault(oi => oi.Id == reqItem.ProductionOrderItemId);
            if (orderItem == null)
            {
                return BadRequest(ApiResponse.Fail($"عنصر أمر الإنتاج رقم {reqItem.ProductionOrderItemId} غير موجود في هذا الأمر."));
            }

            var remainingRequested = orderItem.RequestedQuantity - orderItem.IssuedQuantity;
            if (reqItem.TransferQuantity > remainingRequested)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المطلوب نقلها ({reqItem.TransferQuantity:N3} طن) للمادة {orderItem.MaterialName} تتجاوز المتبقي من أمر الإنتاج ({remainingRequested:N3} طن)."));
            }

            var batch = batches.FirstOrDefault(b => b.Id == reqItem.WarehouseBatchId)
                     ?? (orderItem.WarehouseBatchId.HasValue ? batches.FirstOrDefault(b => b.Id == orderItem.WarehouseBatchId.Value) : null)
                     ?? (!string.IsNullOrWhiteSpace(orderItem.BatchNumber) ? batches.FirstOrDefault(b => b.BatchNumber == orderItem.BatchNumber) : null);

            if (batch == null)
            {
                return BadRequest(ApiResponse.Fail($"التشغيلة ذات المعرف {reqItem.WarehouseBatchId} غير موجودة."));
            }

            // Ensure reqItem has the resolved batch id
            reqItem.WarehouseBatchId = batch.Id;

            if (batch.CurrentQuantity < reqItem.TransferQuantity)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المتوفرة حالياً في تشغيلة المستودع {batch.BatchNumber} ({batch.CurrentQuantity:N3} طن) غير كافية لنقل {reqItem.TransferQuantity:N3} طن."));
            }
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var txDate = DateTime.UtcNow;
            var txNumber = await GetNextSequenceNumberAsync("TRANSFER");

            var op = new StorageOperation
            {
                OperationType = "TRANSFER",
                TransactionNumber = txNumber,
                TransactionDate = txDate,
                StorageLocationId = order.StorageLocationId,
                StorageLocationName = order.StorageLocationName,
                DestinationDepartment = "الإنتاج",
                DestinationName = "خطوط الإنتاج والتصنيع",
                MaterialType = "مواد خام لإنتاج",
                Source = "المستودع",
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                Notes = $"نقل مواد خام لأمر إنتاج {order.OrderNumber} - {request.Notes}",
                ApprovalStatus = "APPROVED",
                IsInventoryPosted = true,
                InventoryPostedAt = txDate,
                CreatedAt = txDate,
                CreatedBy = userName,
                CreatedByUserId = userId
            };
            _db.StorageOperations.Add(op);
            await _db.SaveChangesAsync(); // Generates op.Id

            int lineIdx = 1;
            foreach (var reqItem in validTransferItems)
            {
                var orderItem = order.Items.First(oi => oi.Id == reqItem.ProductionOrderItemId);
                var batch = batches.First(b => b.Id == reqItem.WarehouseBatchId);
                var transferQty = reqItem.TransferQuantity;

                // 1. Deduct from WarehouseBatch
                batch.CurrentQuantity -= transferQty;
                if (batch.CurrentQuantity <= 0)
                {
                    batch.CurrentQuantity = 0;
                    batch.Status = "DEPLETED";
                }

                // 2. Update ProductionOrderItem
                orderItem.IssuedQuantity += transferQty;

                // 2.5 Create StorageOperationItem
                var tracking = $"TRF-{txDate:yyyy}-{op.TransactionNumber:D4}-L{lineIdx++}";
                var opItem = new StorageOperationItem
                {
                    StorageOperationId = op.Id,
                    RawMaterialId = batch.RawMaterialId,
                    MaterialName = orderItem.MaterialName,
                    Quantity = transferQty,
                    TrackingNumber = tracking,
                    Notes = $"نقل لأمر الإنتاج #{order.OrderNumber}",
                    WarehouseBatchId = batch.Id,
                    BatchNumber = batch.BatchNumber,
                    SampleCode = batch.SampleCode,
                    ProductionReferenceNumber = orderItem.ProductionReferenceNumber ?? batch.ProductionReferenceNumber
                };
                await _db.StorageOperationItems.AddAsync(opItem);

                // 3. Deduct from StorageInventoryLedger
                var currentStock = await _db.StorageInventoryLedger
                    .Where(l => l.StorageLocationId == batch.StorageLocationId && l.RawMaterialId == batch.RawMaterialId)
                    .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

                var ledgerEntry = new StorageInventoryLedger
                {
                    StorageLocationId = batch.StorageLocationId,
                    RawMaterialId = batch.RawMaterialId,
                    StorageOperationId = op.Id,
                    StorageOperationItem = opItem,
                    MovementType = "TRANSFER",
                    Quantity = -transferQty, // Negative stock deduction upon physical warehouse transfer
                    RunningBalance = currentStock - transferQty,
                    BatchNumber = batch.BatchNumber,
                    WarehouseBatchId = batch.Id,
                    MovementDate = txDate,
                    CreatedAt = txDate,
                    CreatedBy = userName
                };
                _db.StorageInventoryLedger.Add(ledgerEntry);

                // 4. Create ProductionOrderTransfer record
                var transferRecord = new ProductionOrderTransfer
                {
                    ProductionOrderId = order.Id,
                    ProductionOrderItemId = orderItem.Id,
                    WarehouseBatchId = batch.Id,
                    BatchNumber = batch.BatchNumber,
                    RawMaterialId = batch.RawMaterialId,
                    MaterialName = orderItem.MaterialName,
                    ProductionReferenceNumber = orderItem.ProductionReferenceNumber,
                    StorageOperationId = op.Id,
                    TransferTransactionNumber = $"TRF-{txDate:yyyy}-{op.TransactionNumber:D6}",
                    TransferredQuantity = transferQty,
                    Unit = orderItem.Unit,
                    StorageLocationId = batch.StorageLocationId,
                    StorageLocationName = batch.StorageLocation != null ? batch.StorageLocation.Name : order.StorageLocationName,
                    ConfirmedBy = userName,
                    ConfirmedByUserId = userId,
                    ConfirmedAt = txDate,
                    Notes = request.Notes
                };
                _db.ProductionOrderTransfers.Add(transferRecord);
            }

            // Recalculate totals and update status
            order.TotalTransferredQuantity = order.Items.Sum(i => i.IssuedQuantity);
            var isFullyTransferred = order.Items.All(i => i.IssuedQuantity >= i.RequestedQuantity);

            if (isFullyTransferred)
            {
                order.Status = "TRANSFERRED_TO_PRODUCTION";
                order.CurrentStage = "تم النقل بالكامل - جاهز للتشغيل بالإنتاج";
            }
            else
            {
                order.Status = "PARTIALLY_TRANSFERRED";
                order.CurrentStage = "تم النقل جزئياً من المستودع";
            }

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            try
            {
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    Department = "Storage",
                    Action = "TRANSFER_CONFIRMED",
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    Timestamp = DateTime.UtcNow
                });
                await _hubContext.Clients.All.SendAsync("OnWarehouseInventoryChanged", new
                {
                    Action = "TRANSFER_CONFIRMED",
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
                _logger.LogWarning(exSignal, "Failed to broadcast transfer confirmation SignalR event for {OrderNumber}", order.OrderNumber);
            }

            _logger.LogInformation("Confirmed transfer of {Count} items for Production Order {OrderNumber}. New Status: {Status}.",
                validTransferItems.Count, order.OrderNumber, order.Status);

            var message = isFullyTransferred
                ? "تم تأكيد نقل كافة المواد الخام المطلوبة بنجاح وإرسالها للإنتاج."
                : "تم تأكيد النقل الجزئي للمواد الخام وخصمها من رصيد المستودع بنجاح.";

            return Ok(ApiResponse.Ok(message));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to confirm transfer for Production Order #{Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تأكيد نقل المواد: " + ex.Message));
        }
    }
}
