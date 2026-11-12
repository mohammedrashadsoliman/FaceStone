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

namespace FaceStone.Web.Controllers;

/// <summary>
/// Production Department Controller (قسم الإنتاج)
/// Connects Storage inventory, Laboratory quality gates, Batch tracking, Output classification, and Rework workflows.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ProductionController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<ProductionController> _logger;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IWebHostEnvironment _env;
    private readonly Services.IProductionShiftService _shiftService;
    private readonly Services.IWarehouseInventoryService _inventoryService;

    public ProductionController(
        FaceStoneDbContext db,
        ILogger<ProductionController> logger,
        IHubContext<ChatHub> hubContext,
        IWebHostEnvironment env,
        Services.IProductionShiftService shiftService,
        Services.IWarehouseInventoryService inventoryService)
    {
        _db = db;
        _logger = logger;
        _hubContext = hubContext;
        _env = env;
        _shiftService = shiftService;
        _inventoryService = inventoryService;
    }

    private (bool IsAuth, string Role, string UserName, Guid? UserId) GetAuthenticatedUser()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value ??
                       User.FindFirst("FullName")?.Value ??
                       User.FindFirst("username")?.Value ??
                       User.Identity?.Name ??
                       "المستخدم الحالي";

        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = Guid.TryParse(idClaim, out var g) ? g : null;

        var hasAccess = AppRoles.HasProductionAccess(role) ||
                        AppRoles.HasStorageAccess(role) ||
                        AppRoles.HasManagementAccess(role) ||
                        AppRoles.HasLaboratoryAccess(role) ||
                        AppRoles.HasControlMonitoringAccess(role);

        return (hasAccess, role, userName, userId);
    }

    /// <summary>
    /// Resolves the authenticated employee's verified database profile, role, and server-side shift mapping.
    /// Never trusts shift or employee identity from the client.
    /// </summary>
    private async Task<(bool IsAuth, string Role, string FullName, Guid? UserId, string Email, string? Shift, string? Error)> GetResolvedEmployeeAsync()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value ??
                       User.FindFirst("FullName")?.Value ??
                       User.FindFirst("username")?.Value ??
                       User.Identity?.Name ??
                       string.Empty;

        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = Guid.TryParse(idClaim, out var g) ? g : null;

        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value ??
                         User.FindFirst("email")?.Value ??
                         string.Empty;

        // Fetch DB user record for absolute reliability
        UserAccount? dbUser = null;
        if (userId.HasValue)
        {
            dbUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        }
        if (dbUser == null && !string.IsNullOrEmpty(emailClaim))
        {
            dbUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailClaim);
        }
        if (dbUser == null && !string.IsNullOrEmpty(userName))
        {
            dbUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == userName);
        }

        var effectiveEmail = dbUser?.Email ?? emailClaim;
        var effectiveFullName = dbUser?.FullName ?? (string.IsNullOrEmpty(userName) ? "مسؤول الإنتاج" : userName);
        var effectiveRole = dbUser?.Role ?? role;
        var effectiveUserId = dbUser?.Id ?? userId;

        var isProductionManager = AppRoles.IsProductionManager(effectiveRole) ||
                                  effectiveRole == AppRoles.CEO ||
                                  effectiveRole == AppRoles.IT;

        var shift = _shiftService.GetShiftForEmail(effectiveEmail);

        // If CEO or IT without an explicit production shift mapping, default to First Shift for testing/administration
        if (string.IsNullOrEmpty(shift) && (effectiveRole == AppRoles.CEO || effectiveRole == AppRoles.IT))
        {
            shift = Services.ProductionShiftService.FirstShiftMorning;
        }

        string? error = null;
        if (!isProductionManager)
        {
            error = "هذا الإجراء مخصص لمدير إدارة الإنتاج والمصرح لهم فقط.";
        }
        else if (string.IsNullOrEmpty(shift))
        {
            error = $"حساب المستخدم ({effectiveEmail}) غير مرتبط بوردية إنتاج معتمدة في النظام.";
        }

        return (isProductionManager, effectiveRole, effectiveFullName, effectiveUserId, effectiveEmail, shift, error);
    }

    private void EnsureProductionManagerPermission(string role)
    {
        if (!AppRoles.IsProductionManager(role))
        {
            throw new UnauthorizedAccessException("هذا الإجراء مخصص لمدير إدارة الإنتاج والمصرح لهم فقط.");
        }
    }

    /// <summary>
    /// Concurrency-safe atomic sequence generator using SQL Server UPDLOCK and ROWLOCK
    /// </summary>
    private async Task<int> GetNextSequenceNumberAsync(string sequenceType)
    {
        var typeParam = sequenceType.Trim().ToUpperInvariant();

        if (_db.Database.IsSqlServer())
        {
            var result = await _db.Database
                .SqlQueryRaw<int>(
                    "SELECT NextValue FROM ProductionSequences WITH (UPDLOCK, ROWLOCK) WHERE SequenceType = {0}",
                    typeParam)
                .ToListAsync();

            if (result.Count > 0)
            {
                var currentVal = result[0];
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE ProductionSequences SET NextValue = NextValue + 1 WHERE SequenceType = {0}",
                    typeParam);
                return currentVal;
            }
        }

        var seq = await _db.ProductionSequences.FirstOrDefaultAsync(s => s.SequenceType == typeParam);
        if (seq == null)
        {
            seq = new ProductionSequence { SequenceType = typeParam, NextValue = 1 };
            await _db.ProductionSequences.AddAsync(seq);
            await _db.SaveChangesAsync();
        }

        var assigned = seq.NextValue;
        seq.NextValue += 1;
        await _db.SaveChangesAsync();
        return assigned;
    }

    // =========================================================================
    // 1. DASHBOARD & KPIS
    // =========================================================================
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<ProductionDashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardStats()
    {
        var activeBatches = await _db.ProductionBatches.CountAsync(b => b.Status == "IN_PROGRESS" || b.Status == "ON_HOLD_LABORATORY");
        var pendingRequests = await _db.ProductionOrders.CountAsync(o => o.Status == "PENDING_STORAGE_APPROVAL" || o.Status == "DRAFT");
        var onHoldBatches = await _db.ProductionBatches.CountAsync(b => b.QualityHold || b.Status == "ON_HOLD_LABORATORY");
        var inProcessTests = await _db.LaboratoryInProcessReports.CountAsync();
        var semiFinished = await _db.ProductionOutputs.CountAsync(o => o.Classification == "SEMI_FINISHED");
        var finished = await _db.ProductionOutputs.CountAsync(o => o.Classification == "FINISHED");
        var rework = await _db.ProductionReworks.CountAsync(r => r.Status != "COMPLETED" && r.Status != "CANCELLED");

        var totalIssued = await _db.ProductionOrderItems.SumAsync(i => (decimal?)i.IssuedQuantity) ?? 0m;
        var totalOutput = await _db.ProductionOutputs.SumAsync(o => (decimal?)o.Quantity) ?? 0m;
        var totalWaste = await _db.ProductionBatches.SumAsync(b => (decimal?)b.TotalWasteQuantity) ?? 0m;

        var stats = new ProductionDashboardStatsDto
        {
            ActiveBatches = activeBatches,
            PendingRequests = pendingRequests,
            OnHoldBatches = onHoldBatches,
            InProcessTestsCount = inProcessTests,
            SemiFinishedCount = semiFinished,
            FinishedCount = finished,
            ReworkCount = rework,
            TotalIssuedRawMaterial = totalIssued,
            TotalProducedOutput = totalOutput,
            TotalMeasuredWaste = totalWaste
        };

        return Ok(ApiResponse<ProductionDashboardStatsDto>.Ok(stats));
    }

    // =========================================================================
    // DAILY PRODUCTION TRACKING SYSTEM (تسجيل الإنتاج اليومي)
    // =========================================================================

    /// <summary>
    /// Gets the authenticated employee's verified server-side shift mapping and status.
    /// </summary>
    [HttpGet("current-shift-info")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionShiftInfoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentShiftInfo()
    {
        var resolved = await GetResolvedEmployeeAsync();

        var info = new DailyProductionShiftInfoDto
        {
            EmployeeName = resolved.FullName,
            EmployeeEmail = resolved.Email,
            Shift = resolved.Shift ?? string.Empty,
            IsProductionManager = resolved.IsAuth,
            CanSubmit = resolved.IsAuth && !string.IsNullOrEmpty(resolved.Shift),
            ErrorMessage = resolved.Error,
            Today = DateTime.Today.ToString("yyyy-MM-dd")
        };

        return Ok(ApiResponse<DailyProductionShiftInfoDto>.Ok(info));
    }

    /// <summary>
    /// Returns the controlled list of products from the centralized product catalogue.
    /// </summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductionProductCatalog.ProductItem>>), StatusCodes.Status200OK)]
    public IActionResult GetProductCatalog()
    {
        return Ok(ApiResponse<IReadOnlyList<ProductionProductCatalog.ProductItem>>.Ok(ProductionProductCatalog.StandardProducts));
    }

    /// <summary>
    /// Submits a new Daily Production Record (تسجيل الإنتاج اليومي).
    /// Enforces server-side employee and shift mapping, prevents duplicate products,
    /// supports multiple products and decimal arithmetic, and calculates duration, rates, net output, and waste %.
    /// </summary>
    [HttpPost("daily-submissions")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionSubmissionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateDailySubmission([FromBody] DailyProductionSubmitRequestDto dto)
    {
        var resolved = await GetResolvedEmployeeAsync();
        if (!resolved.IsAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(resolved.Error ?? "غير مصرح لك بتسجيل الإنتاج اليومي."));
        }

        if (string.IsNullOrEmpty(resolved.Shift))
        {
            return BadRequest(ApiResponse.Fail(resolved.Error ?? "حساب المستخدم غير مرتبط بوردية إنتاج معتمدة في النظام. يرجى مراجعة إدارة النظام."));
        }

        // Validate Shift Times
        if (dto.ShiftEnd <= dto.ShiftStart)
        {
            return BadRequest(ApiResponse.Fail("تاريخ ونهاية الوردية يجب أن يكون لاحقاً لتاريخ وبداية الوردية. مدة الإنتاج لا يمكن أن تكون صفراً أو سالبة."));
        }

        var durationHours = (decimal)(dto.ShiftEnd - dto.ShiftStart).TotalHours;
        if (durationHours <= 0)
        {
            return BadRequest(ApiResponse.Fail("مدة الوردية غير صالحة."));
        }

        // Validate Products Selection
        if (dto.Products == null || dto.Products.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب اختيار منتج واحد على الأقل وإدخال كميته المنتجة."));
        }

        // Prevent duplicate product entries
        var productNames = dto.Products.Select(p => p.ProductName.Trim()).ToList();
        if (productNames.Count != productNames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return BadRequest(ApiResponse.Fail("لا يمكن تكرار نفس المنتج في نفس تسجيل الوردية. يرجى دمج الكميات في حقل واحد."));
        }

        foreach (var p in dto.Products)
        {
            if (p.Quantity <= 0)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المدخلة للمنتج '{p.ProductName}' يجب أن تكون أكبر من الصفر."));
            }
        }

        if (dto.WasteQuantity < 0)
        {
            return BadRequest(ApiResponse.Fail("كمية الهالك لا يمكن أن تكون سالبة."));
        }

        var totalGrossQuantity = dto.Products.Sum(p => p.Quantity);
        var netProductionQuantity = Math.Max(0m, totalGrossQuantity - dto.WasteQuantity);
        var overallProductionRate = durationHours > 0 ? Math.Round(totalGrossQuantity / durationHours, 3) : 0m;
        var wastePercentage = totalGrossQuantity > 0 ? Math.Round((dto.WasteQuantity / totalGrossQuantity) * 100m, 3) : 0m;

        // Concurrency-safe atomic sequence number
        var nextSeq = await GetNextSequenceNumberAsync("DAILY_SUBMISSION");
        var submissionNumber = $"DPS-{dto.ProductionDate:yyyyMMdd}-{nextSeq:D4}";

        var submission = new DailyProductionSubmission
        {
            SubmissionNumber = submissionNumber,
            ProductionDate = dto.ProductionDate.Date,
            EmployeeId = resolved.UserId ?? Guid.Empty,
            EmployeeName = resolved.FullName,
            EmployeeEmail = resolved.Email,
            Shift = resolved.Shift,
            ShiftStart = dto.ShiftStart,
            ShiftEnd = dto.ShiftEnd,
            DurationHours = Math.Round(durationHours, 3),
            TotalGrossQuantity = Math.Round(totalGrossQuantity, 3),
            WasteQuantity = Math.Round(dto.WasteQuantity, 3),
            NetProductionQuantity = Math.Round(netProductionQuantity, 3),
            ProductionRatePerHour = overallProductionRate,
            WastePercentage = wastePercentage,
            Notes = dto.Notes?.Trim(),
            ProductionReferenceNumber = dto.ProductionReferenceNumber,
            Status = "SUBMITTED",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = resolved.Email
        };

        foreach (var p in dto.Products)
        {
            var pRate = durationHours > 0 ? Math.Round(p.Quantity / durationHours, 3) : 0m;
            var pWaste = p.ProductWaste;
            var pNet = pWaste.HasValue ? Math.Max(0m, p.Quantity - pWaste.Value) : (decimal?)null;

            submission.Details.Add(new DailyProductionDetail
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName.Trim(),
                Quantity = Math.Round(p.Quantity, 3),
                Unit = string.IsNullOrWhiteSpace(p.Unit) ? "طن" : p.Unit.Trim(),
                ProductionRatePerHour = pRate,
                ProductWaste = pWaste,
                NetQuantity = pNet,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.DailyProductionSubmissions.AddAsync(submission);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Daily production record {SubmissionNumber} created by {Employee} for shift {Shift}",
            submission.SubmissionNumber, submission.EmployeeName, submission.Shift);

        var resultDto = MapToSubmissionDto(submission);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<DailyProductionSubmissionDto>.Ok(resultDto, "تم حفظ تسجيل الإنتاج اليومي بنجاح."));
    }

    /// <summary>
    /// Retrieves daily production submissions with multi-criteria filtering.
    /// Real records directly from SQL Server.
    /// </summary>
    [HttpGet("daily-submissions")]
    [ProducesResponseType(typeof(ApiResponse<List<DailyProductionSubmissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDailySubmissions(
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? shift,
        [FromQuery] string? employee,
        [FromQuery] string? product)
    {
        var query = _db.DailyProductionSubmissions
            .Include(s => s.Details)
            .AsNoTracking();

        if (date.HasValue)
        {
            var d = date.Value.Date;
            query = query.Where(s => s.ProductionDate == d);
        }
        else
        {
            if (startDate.HasValue)
            {
                var sd = startDate.Value.Date;
                query = query.Where(s => s.ProductionDate >= sd);
            }
            if (endDate.HasValue)
            {
                var ed = endDate.Value.Date;
                query = query.Where(s => s.ProductionDate <= ed);
            }
        }

        if (!string.IsNullOrWhiteSpace(shift))
        {
            var shiftTrimmed = shift.Trim();
            query = query.Where(s => s.Shift.Contains(shiftTrimmed));
        }

        if (!string.IsNullOrWhiteSpace(employee))
        {
            var empTrimmed = employee.Trim();
            query = query.Where(s => s.EmployeeName.Contains(empTrimmed) || s.EmployeeEmail.Contains(empTrimmed));
        }

        if (!string.IsNullOrWhiteSpace(product))
        {
            var prodTrimmed = product.Trim();
            query = query.Where(s => s.Details.Any(d => d.ProductName.Contains(prodTrimmed)));
        }

        var list = await query
            .OrderByDescending(s => s.ProductionDate)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync();

        var dtoList = list.Select(MapToSubmissionDto).ToList();
        return Ok(ApiResponse<List<DailyProductionSubmissionDto>>.Ok(dtoList));
    }

    /// <summary>
    /// Retrieves a single daily production submission by ID.
    /// </summary>
    [HttpGet("daily-submissions/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDailySubmissionById(int id)
    {
        var submission = await _db.DailyProductionSubmissions
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null)
        {
            return NotFound(ApiResponse.Fail($"سجل الإنتاج اليومي #{id} غير موجود."));
        }

        return Ok(ApiResponse<DailyProductionSubmissionDto>.Ok(MapToSubmissionDto(submission)));
    }

    /// <summary>
    /// Updates an existing daily production submission. Preserves original ID and records audit timestamp.
    /// </summary>
    [HttpPut("daily-submissions/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDailySubmission(int id, [FromBody] DailyProductionSubmitRequestDto dto)
    {
        var resolved = await GetResolvedEmployeeAsync();
        if (!resolved.IsAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بتعديل سجل الإنتاج."));
        }

        var submission = await _db.DailyProductionSubmissions
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null)
        {
            return NotFound(ApiResponse.Fail($"سجل الإنتاج اليومي #{id} غير موجود."));
        }

        if (dto.ShiftEnd <= dto.ShiftStart)
        {
            return BadRequest(ApiResponse.Fail("تاريخ ونهاية الوردية يجب أن يكون لاحقاً لبداية الوردية."));
        }

        var durationHours = (decimal)(dto.ShiftEnd - dto.ShiftStart).TotalHours;
        if (durationHours <= 0)
        {
            return BadRequest(ApiResponse.Fail("مدة الوردية غير صالحة."));
        }

        if (dto.Products == null || dto.Products.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب اختيار منتج واحد على الأقل وإدخال كميته المنتجة."));
        }

        var productNames = dto.Products.Select(p => p.ProductName.Trim()).ToList();
        if (productNames.Count != productNames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return BadRequest(ApiResponse.Fail("لا يمكن تكرار نفس المنتج في نفس تسجيل الوردية."));
        }

        foreach (var p in dto.Products)
        {
            if (p.Quantity <= 0)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المدخلة للمنتج '{p.ProductName}' يجب أن تكون أكبر من الصفر."));
            }
        }

        if (dto.WasteQuantity < 0)
        {
            return BadRequest(ApiResponse.Fail("كمية الهالك لا يمكن أن تكون سالبة."));
        }

        var totalGrossQuantity = dto.Products.Sum(p => p.Quantity);
        var netProductionQuantity = Math.Max(0m, totalGrossQuantity - dto.WasteQuantity);
        var overallProductionRate = durationHours > 0 ? Math.Round(totalGrossQuantity / durationHours, 3) : 0m;
        var wastePercentage = totalGrossQuantity > 0 ? Math.Round((dto.WasteQuantity / totalGrossQuantity) * 100m, 3) : 0m;

        // Preserve original submission number, employee, and shift; update production values
        submission.ProductionDate = dto.ProductionDate.Date;
        submission.ShiftStart = dto.ShiftStart;
        submission.ShiftEnd = dto.ShiftEnd;
        submission.DurationHours = Math.Round(durationHours, 3);
        submission.TotalGrossQuantity = Math.Round(totalGrossQuantity, 3);
        submission.WasteQuantity = Math.Round(dto.WasteQuantity, 3);
        submission.NetProductionQuantity = Math.Round(netProductionQuantity, 3);
        submission.ProductionRatePerHour = overallProductionRate;
        submission.WastePercentage = wastePercentage;
        submission.Notes = dto.Notes?.Trim();
        submission.ProductionReferenceNumber = dto.ProductionReferenceNumber;
        submission.UpdatedAt = DateTime.UtcNow;
        submission.UpdatedBy = resolved.Email;

        _db.DailyProductionDetails.RemoveRange(submission.Details);
        submission.Details.Clear();

        foreach (var p in dto.Products)
        {
            var pRate = durationHours > 0 ? Math.Round(p.Quantity / durationHours, 3) : 0m;
            var pWaste = p.ProductWaste;
            var pNet = pWaste.HasValue ? Math.Max(0m, p.Quantity - pWaste.Value) : (decimal?)null;

            submission.Details.Add(new DailyProductionDetail
            {
                DailyProductionSubmissionId = submission.Id,
                ProductId = p.ProductId,
                ProductName = p.ProductName.Trim(),
                Quantity = Math.Round(p.Quantity, 3),
                Unit = string.IsNullOrWhiteSpace(p.Unit) ? "طن" : p.Unit.Trim(),
                ProductionRatePerHour = pRate,
                ProductWaste = pWaste,
                NetQuantity = pNet,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Daily production record {SubmissionNumber} updated by {User}",
            submission.SubmissionNumber, resolved.Email);

        return Ok(ApiResponse<DailyProductionSubmissionDto>.Ok(MapToSubmissionDto(submission), "تم تحديث تسجيل الإنتاج بنجاح."));
    }

    /// <summary>
    /// Aggregates daily production summary statistics for management without double counting.
    /// </summary>
    [HttpGet("daily-submissions/summary-stats")]
    [ProducesResponseType(typeof(ApiResponse<DailyProductionSummaryStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDailySummaryStats([FromQuery] DateTime? date, [FromQuery] string? shift)
    {
        var targetDate = (date ?? DateTime.Today).Date;

        var query = _db.DailyProductionSubmissions
            .Include(s => s.Details)
            .Where(s => s.ProductionDate == targetDate);

        if (!string.IsNullOrWhiteSpace(shift))
        {
            var shiftTrimmed = shift.Trim();
            query = query.Where(s => s.Shift.Contains(shiftTrimmed));
        }

        var submissions = await query.ToListAsync();

        var totalGross = submissions.Sum(s => s.TotalGrossQuantity);
        var totalWaste = submissions.Sum(s => s.WasteQuantity);
        var netProduction = submissions.Sum(s => s.NetProductionQuantity);
        var totalDuration = submissions.Sum(s => s.DurationHours);
        var overallRate = totalDuration > 0 ? Math.Round(totalGross / totalDuration, 3) : 0m;
        var wastePct = totalGross > 0 ? Math.Round((totalWaste / totalGross) * 100m, 3) : 0m;

        // Breakdown by product aggregated across submissions without double counting
        var breakdown = submissions
            .SelectMany(s => s.Details)
            .GroupBy(d => d.ProductName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DailyProductAggregateDto
            {
                ProductName = g.Key,
                TotalQuantity = Math.Round(g.Sum(d => d.Quantity), 3),
                Unit = g.FirstOrDefault()?.Unit ?? "طن",
                SubmissionsCount = g.Count()
            })
            .OrderByDescending(p => p.TotalQuantity)
            .ToList();

        var stats = new DailyProductionSummaryStatsDto
        {
            TotalGrossToday = Math.Round(totalGross, 3),
            TotalWasteToday = Math.Round(totalWaste, 3),
            NetProductionToday = Math.Round(netProduction, 3),
            OverallProductionRate = overallRate,
            AverageWastePercentage = wastePct,
            TotalDurationHours = Math.Round(totalDuration, 3),
            TotalSubmissionsCount = submissions.Count,
            ProductsBreakdown = breakdown
        };

        return Ok(ApiResponse<DailyProductionSummaryStatsDto>.Ok(stats));
    }

    private static DailyProductionSubmissionDto MapToSubmissionDto(DailyProductionSubmission s)
    {
        return new DailyProductionSubmissionDto
        {
            Id = s.Id,
            SubmissionNumber = s.SubmissionNumber,
            ProductionDate = s.ProductionDate,
            EmployeeId = s.EmployeeId,
            EmployeeName = s.EmployeeName,
            EmployeeEmail = s.EmployeeEmail,
            Shift = s.Shift,
            ShiftStart = s.ShiftStart,
            ShiftEnd = s.ShiftEnd,
            DurationHours = s.DurationHours,
            TotalGrossQuantity = s.TotalGrossQuantity,
            WasteQuantity = s.WasteQuantity,
            NetProductionQuantity = s.NetProductionQuantity,
            ProductionRatePerHour = s.ProductionRatePerHour,
            WastePercentage = s.WastePercentage,
            Notes = s.Notes,
            ProductionReferenceNumber = s.ProductionReferenceNumber,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            CreatedBy = s.CreatedBy,
            UpdatedAt = s.UpdatedAt,
            UpdatedBy = s.UpdatedBy,
            Details = (s.Details ?? new List<DailyProductionDetail>()).Select(d => new DailyProductionDetailDto
            {
                Id = d.Id,
                DailyProductionSubmissionId = d.DailyProductionSubmissionId,
                ProductId = d.ProductId,
                ProductName = d.ProductName,
                Quantity = d.Quantity,
                Unit = d.Unit,
                ProductionRatePerHour = d.ProductionRatePerHour,
                ProductWaste = d.ProductWaste,
                NetQuantity = d.NetQuantity
            }).ToList()
        };
    }

    // =========================================================================
    // 2. CONNECT PRODUCTION TO STORAGE INVENTORY
    // =========================================================================
    /// <summary>
    /// View warehouse inventory across storage locations from Storage source of truth
    /// </summary>
    [HttpGet("inventory")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionInventoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWarehouseInventory(
        [FromQuery] int? locationId = null,
        [FromQuery] string? materialType = null,
        [FromQuery] string? search = null,
        [FromQuery] bool onlyAvailable = false)
    {
        // 1. Group ledger movements by Location & RawMaterial to compute real running balance
        var ledgerBalances = await _db.StorageInventoryLedger
            .AsNoTracking()
            .GroupBy(l => new { l.StorageLocationId, l.RawMaterialId })
            .Select(g => new
            {
                LocationId = g.Key.StorageLocationId,
                MaterialId = g.Key.RawMaterialId,
                AvailableQuantity = g.Sum(x => x.Quantity)
            })
            .ToListAsync();

        var locations = await _db.StorageLocations.AsNoTracking().Where(l => l.IsActive).ToDictionaryAsync(l => l.Id, l => l.Name);
        var materials = await _db.RawMaterials.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m);

        // Fetch pending reserved quantities in unfulfilled production orders
        var reservedQuantities = await _db.ProductionOrderItems
            .AsNoTracking()
            .Where(i => i.ProductionOrder != null && 
                       (i.ProductionOrder.Status == "PENDING_STORAGE_APPROVAL" || i.ProductionOrder.Status == "PARTIALLY_ISSUED"))
            .GroupBy(i => new { i.StorageLocationId, i.RawMaterialId })
            .Select(g => new
            {
                LocationId = g.Key.StorageLocationId,
                MaterialId = g.Key.RawMaterialId,
                Reserved = g.Sum(x => x.RequestedQuantity - x.IssuedQuantity)
            })
            .ToListAsync();

        var reservedMap = reservedQuantities
            .Where(r => r.LocationId.HasValue)
            .ToDictionary(r => $"{r.LocationId!.Value}_{r.MaterialId}", r => r.Reserved);

        // Fetch Laboratory approved reference mappings
        var labApprovedRefs = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0 && r.RawMaterialId.HasValue)
            .GroupBy(r => r.RawMaterialId!.Value)
            .Select(g => new
            {
                MaterialId = g.Key,
                LatestRef = g.OrderByDescending(x => x.ProductionReferenceNumber).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.MaterialId, x => x.LatestRef);

        var results = new List<ProductionInventoryItemDto>();

        foreach (var b in ledgerBalances)
        {
            if (!locations.TryGetValue(b.LocationId, out var locName)) continue;
            if (!materials.TryGetValue(b.MaterialId, out var mat)) continue;

            if (locationId.HasValue && b.LocationId != locationId.Value) continue;
            if (!string.IsNullOrWhiteSpace(materialType) && !string.Equals(mat.MaterialType, materialType, StringComparison.OrdinalIgnoreCase)) continue;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                if (!mat.Name.ToLowerInvariant().Contains(s) && !locName.ToLowerInvariant().Contains(s))
                    continue;
            }

            var avail = Math.Max(0m, b.AvailableQuantity);
            if (onlyAvailable && avail <= 0) continue;

            var reservedKey = $"{b.LocationId}_{b.MaterialId}";
            var reserved = reservedMap.TryGetValue(reservedKey, out var rVal) ? Math.Max(0m, rVal) : 0m;
            var freeToOrder = Math.Max(0m, avail - reserved);

            labApprovedRefs.TryGetValue(mat.Id, out var labReport);

            results.Add(new ProductionInventoryItemDto
            {
                RawMaterialId = mat.Id,
                MaterialName = mat.Name,
                MaterialType = mat.MaterialType ?? "MECHANICAL",
                StorageLocationId = b.LocationId,
                StorageLocationName = locName,
                AvailableQuantity = avail,
                Unit = "طن",
                ReservedQuantity = reserved,
                AvailableForNewOrders = freeToOrder,
                ProductionReferenceNumber = labReport?.ProductionReferenceNumber,
                IsLabEligible = labReport != null && labReport.ApprovalStatus == "APPROVED",
                LabInspectionStatus = labReport != null ? labReport.InspectionDecision : "لم يتم الفحص بعد"
            });
        }

        return Ok(ApiResponse<List<ProductionInventoryItemDto>>.Ok(results.OrderBy(r => r.StorageLocationName).ThenBy(r => r.MaterialName).ToList()));
    }

    /// <summary>
    /// Retrieve eligible raw materials that have passed Laboratory inspection and have stock in Storage
    /// </summary>
    [HttpGet("eligible-raw-materials")]
    [ProducesResponseType(typeof(ApiResponse<List<EligibleRawMaterialDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleRawMaterials([FromQuery] int? locationId = null)
    {
        // Must be approved by Laboratory with AcceptedQuantity > 0
        var approvedLabReports = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0 && r.RawMaterialId.HasValue)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .ToListAsync();

        var locations = await _db.StorageLocations.AsNoTracking().Where(l => l.IsActive).ToListAsync();

        // Calculate current location balances
        var ledgerBalances = await _db.StorageInventoryLedger
            .AsNoTracking()
            .GroupBy(l => new { l.StorageLocationId, l.RawMaterialId })
            .Select(g => new
            {
                g.Key.StorageLocationId,
                g.Key.RawMaterialId,
                Balance = g.Sum(x => x.Quantity)
            })
            .Where(x => x.Balance > 0)
            .ToListAsync();

        var results = new List<EligibleRawMaterialDto>();

        foreach (var lab in approvedLabReports)
        {
            var mat = lab.RawMaterial;
            if (mat == null) continue;

            var matchingStock = ledgerBalances.Where(b => b.RawMaterialId == mat.Id);
            if (locationId.HasValue)
            {
                matchingStock = matchingStock.Where(b => b.StorageLocationId == locationId.Value);
            }

            foreach (var stock in matchingStock)
            {
                var loc = locations.FirstOrDefault(l => l.Id == stock.StorageLocationId);
                results.Add(new EligibleRawMaterialDto
                {
                    RawMaterialId = mat.Id,
                    MaterialName = mat.Name,
                    MaterialType = mat.MaterialType ?? lab.LaboratorySection,
                    ProductionReferenceNumber = lab.ProductionReferenceNumber,
                    AcceptedQuantity = lab.AcceptedQuantity,
                    AvailableStock = stock.Balance,
                    StorageLocationId = stock.StorageLocationId,
                    StorageLocationName = loc?.Name ?? $"مستودع #{stock.StorageLocationId}",
                    LaboratorySection = lab.LaboratorySection,
                    SupplierName = lab.SupplierName ?? string.Empty
                });
            }
        }

        return Ok(ApiResponse<List<EligibleRawMaterialDto>>.Ok(results));
    }

    /// <summary>
    /// Retrieve eligible Laboratory Production Reference Numbers for selection
    /// </summary>
    [HttpGet("eligible-references")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryReferenceLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleReferences()
    {
        var query = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .Where(r => r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0)
            .OrderByDescending(r => r.ProductionReferenceNumber)
            .ToListAsync();

        var dtos = query.Select(LaboratoryController.MapToReferenceLookupDto).ToList();
        return Ok(ApiResponse<List<LaboratoryReferenceLookupDto>>.Ok(dtos));
    }

    // =========================================================================
    // 3. PRODUCTION ORDERS & MATERIAL REQUESTS
    // =========================================================================
    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductionOrders(
        [FromQuery] string? status = null,
        [FromQuery] int? referenceNumber = null,
        [FromQuery] string? search = null)
    {
        var query = _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.StorageLocation)
            .Include(o => o.Items)
            .Include(o => o.Batches)
            .Include(o => o.Outputs)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.Status == status.Trim().ToUpperInvariant());
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(o => o.ProductionReferenceNumber == referenceNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(s) ||
                                     o.ProductName.ToLower().Contains(s) ||
                                     o.StorageLocationName.ToLower().Contains(s) ||
                                     o.ResponsibleEmployee.ToLower().Contains(s));
        }

        var list = await query.OrderByDescending(o => o.CreatedAt).Take(200).ToListAsync();
        var dtos = list.Select(MapOrderToDto).ToList();
        return Ok(ApiResponse<List<ProductionOrderDto>>.Ok(dtos));
    }

    [HttpGet("orders/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductionOrderById(int id)
    {
        var order = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.StorageLocation)
            .Include(o => o.Items)
                .ThenInclude(i => i.RawMaterial)
            .Include(o => o.Batches)
            .Include(o => o.Outputs)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(ApiResponse.Fail($"أمر الإنتاج رقم #{id} غير موجود."));
        }

        return Ok(ApiResponse<ProductionOrderDto>.Ok(MapOrderToDto(order)));
    }

    /// <summary>
    /// Create a new Production Order & Material Request to Storage
    /// Strictly verifies that raw materials have passed Laboratory inspection.
    /// </summary>
    [HttpPost("orders")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProductionOrder([FromBody] CreateProductionOrderRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات طلب الإنتاج غير صالحة."));
        }

        if (string.IsNullOrWhiteSpace(dto.ProductName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج المطلوب أو العملية الإنتاجية حقل إجباري."));
        }

        if (dto.RequestedQuantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("الكمية المستهدفة للإنتاج يجب أن تكون أكبر من الصفر."));
        }

        var location = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.StorageLocationId && l.IsActive);
        if (location == null)
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين المحدد غير موجود أو غير مفعل."));
        }

        if (dto.Items == null || dto.Items.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب تحديد مادة خام واحدة على الأقل مطلوبة لأمر الإنتاج."));
        }

        // Quality Gate: Validate shared Laboratory reference if provided
        if (dto.ProductionReferenceNumber.HasValue && dto.ProductionReferenceNumber.Value > 0)
        {
            var labApproved = await _db.LaboratoryRawMaterialReports
                .AnyAsync(r => r.ProductionReferenceNumber == dto.ProductionReferenceNumber.Value &&
                               r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0);

            if (!labApproved)
            {
                return BadRequest(ApiResponse.Fail($"الرقم المرجعي التتبعي #{dto.ProductionReferenceNumber} لم يجتز فحص واعتماد المعمل بنجاح، ولا يمكن استخدامه في الإنتاج."));
            }
        }

        // Validate items & material eligibility
        var resolvedItems = new List<(RawMaterial Material, decimal Quantity, string? Notes)>();
        foreach (var item in dto.Items)
        {
            if (item.RequestedQuantity <= 0)
            {
                return BadRequest(ApiResponse.Fail("الكمية المطلوبة لكل مادة خام يجب أن تكون أكبر من الصفر."));
            }

            var mat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Id == item.RawMaterialId);
            if (mat == null)
            {
                return BadRequest(ApiResponse.Fail($"الصنف رقم #{item.RawMaterialId} غير موجود بالنظام."));
            }

            // Quality Gate: Ensure material has passed Laboratory inspection
            var hasPassedLab = await _db.LaboratoryRawMaterialReports
                .AnyAsync(r => r.RawMaterialId == mat.Id && r.ApprovalStatus == "APPROVED" && r.AcceptedQuantity > 0);

            if (!hasPassedLab)
            {
                return BadRequest(ApiResponse.Fail($"المادة الخام '{mat.Name}' لم تجتز فحص الجودة المعملي المعتمد، ومحظور صرفها لخطوط الإنتاج."));
            }

            resolvedItems.Add((mat, item.RequestedQuantity, item.Notes));
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var seq = await GetNextSequenceNumberAsync("ORDER");
            var orderNumber = $"ORD-{DateTime.UtcNow:yyyy}-{seq:D4}";

            var order = new ProductionOrder
            {
                OrderNumber = orderNumber,
                ProductionReferenceNumber = dto.ProductionReferenceNumber,
                ProductName = dto.ProductName.Trim(),
                RequestedQuantity = dto.RequestedQuantity,
                Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "طن" : dto.Unit.Trim(),
                StorageLocationId = location.Id,
                StorageLocationName = location.Name,
                Status = "PENDING_STORAGE_APPROVAL",
                CurrentStage = "الخلط الأولي",
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                PlannedStartDate = dto.PlannedStartDate ?? DateTime.UtcNow,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.ProductionOrders.AddAsync(order);
            await _db.SaveChangesAsync();

            foreach (var (mat, qty, notes) in resolvedItems)
            {
                var orderItem = new ProductionOrderItem
                {
                    ProductionOrderId = order.Id,
                    RawMaterialId = mat.Id,
                    MaterialName = mat.Name,
                    RequestedQuantity = qty,
                    IssuedQuantity = 0m,
                    ConsumedQuantity = 0m,
                    Unit = order.Unit,
                    StorageLocationId = location.Id,
                    Notes = notes
                };
                await _db.ProductionOrderItems.AddAsync(orderItem);
            }

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Production Order {OrderNumber} (ID: {Id}) submitted by {User}.", order.OrderNumber, order.Id, userName);

            // SignalR notification to Storage & Management
            await _hubContext.Clients.All.SendAsync("ProductionOrderCreated", new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                productName = order.ProductName,
                requestedQuantity = order.RequestedQuantity,
                responsibleEmployee = userName
            });

            var result = await _db.ProductionOrders
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == order.Id);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductionOrderDto>.Ok(MapOrderToDto(result), $"تم إنشاء طلب الإنتاج بنجاح برقم {order.OrderNumber} وإرساله للمستودع للمراجعة والصرف."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to create Production Order.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ طلب الإنتاج."));
        }
    }

    // =========================================================================
    // 4. ACTIVE BATCH TRACKING & IN-PROCESS QUALITY GATES
    // =========================================================================
    [HttpGet("batches")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionBatchDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatches(
        [FromQuery] string? status = null,
        [FromQuery] int? orderId = null,
        [FromQuery] int? referenceNumber = null)
    {
        var query = _db.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProductionOrder)
            .Include(b => b.Outputs)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => b.Status == status.Trim().ToUpperInvariant());
        }

        if (orderId.HasValue && orderId.Value > 0)
        {
            query = query.Where(b => b.ProductionOrderId == orderId.Value);
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(b => b.ProductionReferenceNumber == referenceNumber.Value);
        }

        var list = await query.OrderByDescending(b => b.StartedAt).Take(200).ToListAsync();
        var dtos = list.Select(MapBatchToDto).ToList();
        return Ok(ApiResponse<List<ProductionBatchDto>>.Ok(dtos));
    }

    [HttpGet("batches/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductionBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBatchById(int id)
    {
        var batch = await _db.ProductionBatches
            .AsNoTracking()
            .Include(b => b.ProductionOrder)
            .Include(b => b.Outputs)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"تشغيلة الإنتاج رقم #{id} غير موجودة."));
        }

        var dto = MapBatchToDto(batch);

        // Fetch linked in-process lab tests
        var labTests = await _db.LaboratoryInProcessReports
            .AsNoTracking()
            .Where(t => t.ProductionBatchNumber == batch.BatchNumber ||
                       (batch.ProductionReferenceNumber.HasValue && t.ProductionReferenceNumber == batch.ProductionReferenceNumber.Value))
            .OrderByDescending(t => t.TestDateTime)
            .ToListAsync();

        dto.InProcessTests = labTests.Select(LaboratoryController.MapInProcessToDto).ToList();

        return Ok(ApiResponse<ProductionBatchDto>.Ok(dto));
    }

    /// <summary>
    /// Start a new Batch for an eligible Production Order
    /// </summary>
    [HttpPost("batches")]
    [ProducesResponseType(typeof(ApiResponse<ProductionBatchDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartBatch([FromBody] StartProductionBatchRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات بدء التشغيلة غير صالحة."));
        }

        var order = await _db.ProductionOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == dto.ProductionOrderId);

        if (order == null)
        {
            return BadRequest(ApiResponse.Fail($"أمر الإنتاج رقم #{dto.ProductionOrderId} غير موجود."));
        }

        if (order.Status == "COMPLETED" || order.Status == "CANCELLED")
        {
            return BadRequest(ApiResponse.Fail($"أمر الإنتاج في حالة '{order.Status}' ولا يمكن بدء تشغيلة جديدة له."));
        }

        // Check total issued raw material
        var totalIssued = order.Items.Sum(i => i.IssuedQuantity);
        if (totalIssued <= 0 && !order.IsStorageApproved)
        {
            _logger.LogWarning("Batch started prior to storage completion for order {OrderNumber}.", order.OrderNumber);
        }

        var seq = await GetNextSequenceNumberAsync("BATCH");
        var batchNumber = $"BATCH-{DateTime.UtcNow:yyyy}-{seq:D4}";

        var batch = new ProductionBatch
        {
            BatchNumber = batchNumber,
            ProductionOrderId = order.Id,
            ProductionReferenceNumber = order.ProductionReferenceNumber,
            CurrentStage = string.IsNullOrWhiteSpace(dto.InitialStage) ? "الخلط الأولي" : dto.InitialStage.Trim(),
            Status = "IN_PROGRESS",
            QualityHold = false,
            TotalInputQuantity = totalIssued > 0 ? totalIssued : order.RequestedQuantity,
            TotalOutputQuantity = 0m,
            TotalWasteQuantity = 0m,
            StartedAt = DateTime.UtcNow,
            ResponsibleEmployee = userName,
            ResponsibleUserId = userId,
            Notes = dto.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userName
        };

        // Link to associated ProductionWorkInProcess entity to ensure 100% compatibility with Laboratory and legacy queries
        if (order.StorageOperationId.HasValue && order.StorageOperationId.Value > 0)
        {
            var existingWip = await _db.ProductionWorkInProcess
                .FirstOrDefaultAsync(w => w.StorageOperationId == order.StorageOperationId.Value);
            if (existingWip != null)
            {
                batch.ProductionWorkInProcessId = existingWip.Id;
            }
            else
            {
                var firstItem = order.Items.FirstOrDefault();
                var op = await _db.StorageOperations.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == order.StorageOperationId.Value);
                var firstOpItem = op?.Items.FirstOrDefault();
                if (firstItem != null && firstOpItem != null)
                {
                    var wip = new ProductionWorkInProcess
                    {
                        StorageOperationId = op!.Id,
                        StorageOperationItemId = firstOpItem.Id,
                        RawMaterialId = firstItem.RawMaterialId,
                        ProductionReferenceNumber = order.ProductionReferenceNumber,
                        TrackingNumber = batchNumber,
                        MaterialQuantityIssued = batch.TotalInputQuantity,
                        SourceStorageLocation = order.StorageLocationName ?? "المستودع الرئيسي",
                        TransferDate = DateTime.UtcNow,
                        Status = "قيد التشغيل",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName
                    };
                    await _db.ProductionWorkInProcess.AddAsync(wip);
                    await _db.SaveChangesAsync();
                    batch.ProductionWorkInProcessId = wip.Id;
                }
            }
        }

        await _db.ProductionBatches.AddAsync(batch);

        order.Status = "IN_PRODUCTION";
        order.ActualStartDate ??= DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Batch {BatchNumber} started for Order {OrderNumber} by {User}.", batch.BatchNumber, order.OrderNumber, userName);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductionBatchDto>.Ok(MapBatchToDto(batch), $"تم بدء تشغيلة الإنتاج بنجاح برقم {batch.BatchNumber}."));
    }

    /// <summary>
    /// Advance Batch Stage.
    /// MANDATORY QUALITY GATE: If an in-process laboratory test has failed (DOES_NOT_MEET),
    /// the backend strictly prevents moving forward until authorized resolution.
    /// </summary>
    [HttpPost("batches/{id:int}/advance-stage")]
    [ProducesResponseType(typeof(ApiResponse<ProductionBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdvanceBatchStage(int id, [FromBody] AdvanceBatchStageRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        var batch = await _db.ProductionBatches
            .Include(b => b.ProductionOrder)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"تشغيلة الإنتاج رقم #{id} غير موجودة."));
        }

        // =========================================================================
        // MANDATORY QUALITY GATE: Verify Laboratory Hold & In-Process Test Results
        // =========================================================================
        if (batch.QualityHold || batch.Status == "ON_HOLD_LABORATORY")
        {
            return BadRequest(ApiResponse.Fail($"ممنوع الانتقال للمرحلة التالية: التشغيلة معلقة حالياً بأمر فني من قسم المعمل بسبب: '{batch.QualityHoldReason ?? "عدم مطابقة النتائج للمواصفات"}'."));
        }

        var latestFailedTest = await _db.LaboratoryInProcessReports
            .Where(t => t.ProductionBatchNumber == batch.BatchNumber && t.InspectionDecision == "DOES_NOT_MEET")
            .OrderByDescending(t => t.TestDateTime)
            .FirstOrDefaultAsync();

        if (latestFailedTest != null)
        {
            // Check if there is a newer passing retest
            var passingRetest = await _db.LaboratoryInProcessReports
                .Where(t => t.ProductionBatchNumber == batch.BatchNumber && 
                           t.InspectionDecision == "MEETS_REQUIREMENTS" && 
                           t.TestDateTime > latestFailedTest.TestDateTime)
                .AnyAsync();

            if (!passingRetest)
            {
                batch.QualityHold = true;
                batch.Status = "ON_HOLD_LABORATORY";
                batch.QualityHoldReason = latestFailedTest.Notes ?? "فحص المعمل غير مطابق للاشتراطات الفنية";
                batch.QualityHoldReportId = latestFailedTest.Id;
                batch.QualityHoldAt = latestFailedTest.TestDateTime;
                await _db.SaveChangesAsync();

                return BadRequest(ApiResponse.Fail($"ممنوع الانتقال للمرحلة التالية: توجد عينة فحص معملي غير مطابقة للاشتراطات في مرحلة '{latestFailedTest.ProductionStage}'. تم تعليق التشغيلة لحين إجراء فحص جديد أو اعتماد الإجراء التصحيحي."));
            }
        }

        if (string.IsNullOrWhiteSpace(dto?.NextStage))
        {
            return BadRequest(ApiResponse.Fail("يرجى تحديد المرحلة الإنتاجية التالية."));
        }

        batch.CurrentStage = dto.NextStage.Trim();
        if (batch.ProductionOrder != null)
        {
            batch.ProductionOrder.CurrentStage = batch.CurrentStage;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Batch {BatchNumber} advanced to stage {Stage} by {User}.", batch.BatchNumber, batch.CurrentStage, userName);

        return Ok(ApiResponse<ProductionBatchDto>.Ok(MapBatchToDto(batch), $"تم نقل التشغيلة إلى مرحلة '{batch.CurrentStage}' بنجاح."));
    }

    /// <summary>
    /// Release Laboratory Quality Gate Hold if authorized
    /// </summary>
    [HttpPost("batches/{id:int}/release-hold")]
    [ProducesResponseType(typeof(ApiResponse<ProductionBatchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReleaseHold(int id, [FromBody] AdvanceBatchStageRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        var batch = await _db.ProductionBatches.FirstOrDefaultAsync(b => b.Id == id);
        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"تشغيلة الإنتاج رقم #{id} غير موجودة."));
        }

        batch.QualityHold = false;
        batch.Status = "IN_PROGRESS";
        batch.QualityHoldReleasedAt = DateTime.UtcNow;
        batch.QualityHoldReleasedBy = userName;
        if (!string.IsNullOrWhiteSpace(dto?.Notes))
        {
            batch.Notes = string.IsNullOrWhiteSpace(batch.Notes) 
                ? $"[فك التعليق بواسطة {userName}: {dto.Notes}]" 
                : $"{batch.Notes} | [فك التعليق بواسطة {userName}: {dto.Notes}]";
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Quality Hold on Batch {BatchNumber} released by {User}.", batch.BatchNumber, userName);

        return Ok(ApiResponse<ProductionBatchDto>.Ok(MapBatchToDto(batch), "تم فك تعليق التشغيلة والسماح بمواصلة خط الإنتاج."));
    }

    // =========================================================================
    // 5. PRODUCTION OUTPUT & WASTE RECONCILIATION
    // =========================================================================
    /// <summary>
    /// Record actual production output.
    /// Classifies output as Semi-Finished, Finished, or Recoverable Waste.
    /// MANDATORY QUALITY GATE: Output recording is BLOCKED if batch is on Laboratory Hold!
    /// </summary>
    [HttpPost("batches/{id:int}/outputs")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOutputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordOutput(int id, [FromBody] CreateProductionOutputRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        var batch = await _db.ProductionBatches
            .Include(b => b.ProductionOrder)
            .Include(b => b.Outputs)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null)
        {
            return NotFound(ApiResponse.Fail($"تشغيلة الإنتاج رقم #{id} غير موجودة."));
        }

        // =========================================================================
        // QUALITY GATE: Block output if batch is on Laboratory Hold
        // =========================================================================
        if (batch.QualityHold || batch.Status == "ON_HOLD_LABORATORY")
        {
            return BadRequest(ApiResponse.Fail($"ممنوع تسجيل مخرجات: التشغيلة معلقة بأمر فني من قسم المعمل بسبب: '{batch.QualityHoldReason ?? "عدم مطابقة المواصفات"}'."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات المخرجات غير صالحة."));
        }

        if (string.IsNullOrWhiteSpace(dto.ProductName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج أو تصنيف المخرجات حقل إجباري."));
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("الكمية الفعلية للمخرجات يجب أن تكون أكبر من الصفر."));
        }

        var classification = (dto.Classification ?? "FINISHED").Trim().ToUpperInvariant();
        if (classification != "SEMI_FINISHED" && classification != "FINISHED" && classification != "RECOVERABLE_WASTE")
        {
            return BadRequest(ApiResponse.Fail("تصنيف المخرجات غير صحيح. الخيارات المتاحة: SEMI_FINISHED أو FINISHED أو RECOVERABLE_WASTE."));
        }

        StorageLocation? destLocation = null;
        if (dto.DestinationStorageLocationId.HasValue && dto.DestinationStorageLocationId.Value > 0)
        {
            destLocation = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.DestinationStorageLocationId.Value);
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var seq = await GetNextSequenceNumberAsync("OUTPUT");
            var outputNumber = $"OUT-{DateTime.UtcNow:yyyy}-{seq:D4}";

            var output = new ProductionOutput
            {
                OutputNumber = outputNumber,
                ProductionOrderId = batch.ProductionOrderId,
                ProductionBatchId = batch.Id,
                ProductionReferenceNumber = batch.ProductionReferenceNumber,
                ProductName = dto.ProductName.Trim(),
                Classification = classification,
                Quantity = dto.Quantity,
                Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "طن" : dto.Unit.Trim(),
                ProductionStage = string.IsNullOrWhiteSpace(dto.ProductionStage) ? batch.CurrentStage : dto.ProductionStage.Trim(),
                OutputDate = DateTime.UtcNow,
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                NextStage = classification == "SEMI_FINISHED" ? dto.NextStage?.Trim() : null,
                DestinationStorageLocationId = destLocation?.Id,
                DestinationStorageLocationName = destLocation?.Name,
                LaboratoryConformityStatus = classification == "FINISHED" ? "PENDING_INSPECTION" : "NOT_APPLICABLE",
                IsStorageReceived = false,
                StorageReceivedQuantity = 0m,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.ProductionOutputs.AddAsync(output);
            await _db.SaveChangesAsync();

            // Update batch total outputs and waste reconciliation
            var allOutputs = await _db.ProductionOutputs.Where(o => o.ProductionBatchId == batch.Id).ToListAsync();
            var totalOut = allOutputs.Sum(o => o.Quantity);
            batch.TotalOutputQuantity = totalOut;

            // Difference = Input - Outputs
            var diff = Math.Max(0m, batch.TotalInputQuantity - totalOut);
            batch.TotalWasteQuantity = diff;

            // If this is a finished output and batch has fulfilled order, update order status
            if (classification == "FINISHED" && batch.ProductionOrder != null)
            {
                batch.ProductionOrder.Status = "PENDING_FINAL_LABORATORY_INSPECTION";
            }

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Output {OutputNumber} recorded for Batch {BatchNumber} ({Qty} {Unit}, {Class}) by {User}.",
                output.OutputNumber, batch.BatchNumber, output.Quantity, output.Unit, output.Classification, userName);

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductionOutputDto>.Ok(MapOutputToDto(output), $"تم تسجيل المخرجات بنجاح برقم {output.OutputNumber}."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to record Production Output.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ ناتج الإنتاج."));
        }
    }

    [HttpGet("outputs")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionOutputDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutputs(
        [FromQuery] string? classification = null,
        [FromQuery] int? referenceNumber = null,
        [FromQuery] int? batchId = null)
    {
        var query = _db.ProductionOutputs
            .AsNoTracking()
            .Include(o => o.ProductionOrder)
            .Include(o => o.ProductionBatch)
            .Include(o => o.DestinationStorageLocation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(classification) && !string.Equals(classification, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => o.Classification == classification.Trim().ToUpperInvariant());
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(o => o.ProductionReferenceNumber == referenceNumber.Value);
        }

        if (batchId.HasValue && batchId.Value > 0)
        {
            query = query.Where(o => o.ProductionBatchId == batchId.Value);
        }

        var list = await query.OrderByDescending(o => o.OutputDate).Take(200).ToListAsync();
        var dtos = list.Select(MapOutputToDto).ToList();
        return Ok(ApiResponse<List<ProductionOutputDto>>.Ok(dtos));
    }

    // =========================================================================
    // 6. FINAL PRODUCT LABORATORY APPROVAL & REWORK ORDERS
    // =========================================================================
    /// <summary>
    /// Create a traceable Rework Order when a finished product fails Laboratory inspection
    /// </summary>
    [HttpPost("rework-orders")]
    [ProducesResponseType(typeof(ApiResponse<ProductionReworkDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReworkOrder([FromBody] CreateReworkOrderRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم الإنتاج."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات أمر إعادة التشغيل غير صالحة."));
        }

        var output = await _db.ProductionOutputs
            .Include(o => o.ProductionBatch)
            .Include(o => o.ProductionOrder)
            .FirstOrDefaultAsync(o => o.Id == dto.ProductionOutputId);

        if (output == null)
        {
            return BadRequest(ApiResponse.Fail($"ناتج الإنتاج رقم #{dto.ProductionOutputId} غير موجود."));
        }

        if (output.IsReworkCreated)
        {
            return BadRequest(ApiResponse.Fail("تم بالفعل إنشاء أمر إعادة تشغيل لهذا الناتج مسبقاً."));
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var seq = await GetNextSequenceNumberAsync("REWORK");
            var reworkNumber = $"RWK-{DateTime.UtcNow:yyyy}-{seq:D4}";

            var rework = new ProductionRework
            {
                ReworkNumber = reworkNumber,
                OriginalProductionOrderId = output.ProductionOrderId,
                OriginalProductionBatchId = output.ProductionBatchId,
                OriginalProductionOutputId = output.Id,
                ProductionReferenceNumber = output.ProductionReferenceNumber,
                Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "عدم مطابقة مواصفات المنتج النهائي في فحص المعمل" : dto.Reason.Trim(),
                Instructions = string.IsNullOrWhiteSpace(dto.Instructions) ? "إعادة الخلط والمعالجة الكيميائية لضبط المواصفات" : dto.Instructions.Trim(),
                Status = "IN_PROGRESS",
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.ProductionReworks.AddAsync(rework);

            output.IsReworkCreated = true;
            output.NonConformityAction = "REWORK_REMANUFACTURE";
            output.LaboratoryConformityStatus = "REWORK_REQUIRED";

            if (output.ProductionOrder != null)
            {
                output.ProductionOrder.Status = "REWORK_REQUIRED";
            }

            if (output.ProductionBatch != null)
            {
                output.ProductionBatch.Status = "REWORK";
            }

            await _db.SaveChangesAsync();
            rework.OriginalProductionOutputId = output.Id;
            output.ReworkOrderId = rework.Id;

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Rework Order {ReworkNumber} created for Output {OutputNumber} by {User}.",
                rework.ReworkNumber, output.OutputNumber, userName);

            var resultDto = new ProductionReworkDto
            {
                Id = rework.Id,
                ReworkNumber = rework.ReworkNumber,
                OriginalProductionOrderId = rework.OriginalProductionOrderId,
                OriginalOrderNumber = output.ProductionOrder?.OrderNumber ?? string.Empty,
                OriginalProductionBatchId = rework.OriginalProductionBatchId,
                OriginalBatchNumber = output.ProductionBatch?.BatchNumber ?? string.Empty,
                OriginalProductionOutputId = rework.OriginalProductionOutputId,
                OriginalOutputNumber = output.OutputNumber,
                ProductionReferenceNumber = rework.ProductionReferenceNumber,
                Reason = rework.Reason,
                Instructions = rework.Instructions,
                Status = rework.Status,
                ResponsibleEmployee = rework.ResponsibleEmployee,
                CreatedAt = rework.CreatedAt,
                CreatedBy = rework.CreatedBy
            };

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductionReworkDto>.Ok(resultDto, $"تم إنشاء أمر إعادة التشغيل بنجاح برقم {rework.ReworkNumber}."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to create Rework Order.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء إنشاء أمر إعادة التشغيل."));
        }
    }

    [HttpGet("rework-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionReworkDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReworkOrders([FromQuery] int? referenceNumber = null)
    {
        var query = _db.ProductionReworks.AsNoTracking().AsQueryable();

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(r => r.ProductionReferenceNumber == referenceNumber.Value);
        }

        var list = await query.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync();

        var ordersMap = await _db.ProductionOrders.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.OrderNumber);
        var batchesMap = await _db.ProductionBatches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.BatchNumber);
        var outputsMap = await _db.ProductionOutputs.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.OutputNumber);

        var dtos = list.Select(r => new ProductionReworkDto
        {
            Id = r.Id,
            ReworkNumber = r.ReworkNumber,
            OriginalProductionOrderId = r.OriginalProductionOrderId,
            OriginalOrderNumber = ordersMap.TryGetValue(r.OriginalProductionOrderId, out var oNum) ? oNum : string.Empty,
            OriginalProductionBatchId = r.OriginalProductionBatchId,
            OriginalBatchNumber = batchesMap.TryGetValue(r.OriginalProductionBatchId, out var bNum) ? bNum : string.Empty,
            OriginalProductionOutputId = r.OriginalProductionOutputId,
            OriginalOutputNumber = outputsMap.TryGetValue(r.OriginalProductionOutputId, out var outNum) ? outNum : string.Empty,
            ProductionReferenceNumber = r.ProductionReferenceNumber,
            Reason = r.Reason,
            Instructions = r.Instructions,
            Status = r.Status,
            NewProductionOrderId = r.NewProductionOrderId,
            NewProductionBatchId = r.NewProductionBatchId,
            ResponsibleEmployee = r.ResponsibleEmployee,
            CreatedAt = r.CreatedAt,
            CreatedBy = r.CreatedBy
        }).ToList();

        return Ok(ApiResponse<List<ProductionReworkDto>>.Ok(dtos));
    }

    // =========================================================================
    // 7. STORAGE INTEGRATION — RECEIVING FINISHED / SEMI-FINISHED OUTPUT
    // =========================================================================
    /// <summary>
    /// View outputs eligible for receiving into Storage
    /// Only finished outputs that have passed Final Laboratory Conformity (CONFORMS) and approved are eligible for finished stock.
    /// </summary>
    [HttpGet("for-storage-receiving")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionOutputDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutputsForStorageReceiving()
    {
        var outputs = await _db.ProductionOutputs
            .AsNoTracking()
            .Include(o => o.ProductionOrder)
            .Include(o => o.ProductionBatch)
            .Include(o => o.DestinationStorageLocation)
            .Where(o => !o.IsStorageReceived)
            .ToListAsync();

        // Filter eligible outputs:
        // Finished products MUST have passed Laboratory conformity (CONFORMS)
        // Semi-finished products and Recoverable waste can be received into their designated locations
        var eligible = new List<ProductionOutput>();

        foreach (var o in outputs)
        {
            if (o.Classification == "FINISHED")
            {
                // Verify Laboratory certificate
                var hasPassedFinal = false;
                if (o.ProductionReferenceNumber.HasValue)
                {
                    hasPassedFinal = await _db.LaboratoryFinalConformityCertificates
                        .AnyAsync(c => c.ProductionReferenceNumber == o.ProductionReferenceNumber.Value &&
                                       c.ConformityDecision == "CONFORMS" &&
                                       c.ApprovalStatus == "APPROVED");
                }

                if (hasPassedFinal || o.LaboratoryConformityStatus == "CONFORMS")
                {
                    eligible.Add(o);
                }
            }
            else
            {
                // SEMI_FINISHED or RECOVERABLE_WASTE
                eligible.Add(o);
            }
        }

        var dtos = eligible.Select(MapOutputToDto).ToList();
        return Ok(ApiResponse<List<ProductionOutputDto>>.Ok(dtos));
    }

    /// <summary>
    /// Storage Manager receives eligible output into warehouse inventory.
    /// Posts directly to StorageInventoryLedger and marks output as received.
    /// </summary>
    [HttpPost("receive-output-in-storage")]
    [ProducesResponseType(typeof(ApiResponse<ProductionOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveOutputInStorage([FromBody] ReceiveOutputInStorageRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح بالوصول إلى قسم المخازن."));
        }

        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات استلام المخرجات غير صالحة."));
        }

        var output = await _db.ProductionOutputs
            .Include(o => o.ProductionBatch)
            .Include(o => o.ProductionOrder)
            .FirstOrDefaultAsync(o => o.Id == dto.ProductionOutputId);

        if (output == null)
        {
            return NotFound(ApiResponse.Fail($"ناتج الإنتاج رقم #{dto.ProductionOutputId} غير موجود."));
        }

        if (output.IsStorageReceived)
        {
            return BadRequest(ApiResponse.Fail("تم استلام هذا الناتج بالمستودع مسبقاً ولا يمكن تكرار ترحيله."));
        }

        var location = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.StorageLocationId && l.IsActive);
        if (location == null)
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين المحدد غير موجود أو غير مفعل."));
        }

        // =========================================================================
        // QUALITY GATE: Finished products MUST have passed Final Laboratory Conformity
        // =========================================================================
        if (output.Classification == "FINISHED")
        {
            var isConforming = output.LaboratoryConformityStatus == "CONFORMS";
            if (!isConforming && output.ProductionReferenceNumber.HasValue)
            {
                isConforming = await _db.LaboratoryFinalConformityCertificates
                    .AnyAsync(c => c.ProductionReferenceNumber == output.ProductionReferenceNumber.Value &&
                                   c.ConformityDecision == "CONFORMS" &&
                                   c.ApprovalStatus == "APPROVED");
            }

            if (!isConforming)
            {
                return BadRequest(ApiResponse.Fail("محظور إدخال المنتج لمستودع البضاعة الجاهزة قبل صدور واعتماد شهادة المطابقة النهائية (CONFORMS) من قسم المعمل."));
            }
        }

        var receiveQty = dto.ReceivedQuantity > 0 ? dto.ReceivedQuantity : output.Quantity;
        if (receiveQty > output.Quantity)
        {
            return BadRequest(ApiResponse.Fail($"الكمية المستلمة ({receiveQty:N3} طن) لا يمكن أن تتجاوز الكمية الفعلية المنتجة ({output.Quantity:N3} طن)."));
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Find or create RawMaterial card for this output
            var matName = output.ProductName.Trim();
            var rawMat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name == matName);
            if (rawMat == null)
            {
                rawMat = new RawMaterial
                {
                    Name = matName,
                    MaterialType = output.Classification == "FINISHED" ? "FINISHED_PRODUCT" : "SEMI_FINISHED",
                    InitialQuantity = 0m,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                };
                await _db.RawMaterials.AddAsync(rawMat);
                await _db.SaveChangesAsync();
            }

            // Post to StorageInventoryLedger
            var currentStock = await _db.StorageInventoryLedger
                .Where(l => l.StorageLocationId == location.Id && l.RawMaterialId == rawMat.Id)
                .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

            var newStock = currentStock + receiveQty;

            var ledgerEntry = new StorageInventoryLedger
            {
                StorageLocationId = location.Id,
                RawMaterialId = rawMat.Id,
                MovementType = "IMPORT",
                Quantity = receiveQty, // Positive for inward receipt
                RunningBalance = newStock,
                MovementDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            await _db.StorageInventoryLedger.AddAsync(ledgerEntry);

            output.IsStorageReceived = true;
            output.StorageReceivedQuantity = receiveQty;
            output.StorageReceivedAt = DateTime.UtcNow;
            output.StorageReceivedBy = userName;
            output.DestinationStorageLocationId = location.Id;
            output.DestinationStorageLocationName = location.Name;

            // If order completed all outputs, mark order as COMPLETED
            if (output.ProductionOrder != null)
            {
                output.ProductionOrder.Status = "COMPLETED";
                output.ProductionOrder.CompletedDate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Output {OutputNumber} received into Storage {Location} ({Qty} TON) by {User}.",
                output.OutputNumber, location.Name, receiveQty, userName);

            return Ok(ApiResponse<ProductionOutputDto>.Ok(MapOutputToDto(output), $"تم استلام المخرجات بنجاح في {location.Name} وترحيل رصيد المخزن ({receiveQty:N3} طن)."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to receive Production Output into storage.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء استلام المخرجات بالمستودع."));
        }
    }

    // =========================================================================
    // 8. PRODUCTION HISTORY & TRACEABILITY
    // =========================================================================
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductionHistory(
        [FromQuery] int? referenceNumber = null,
        [FromQuery] string? search = null)
    {
        var query = _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.StorageLocation)
            .Include(o => o.Items)
            .Include(o => o.Batches)
            .Include(o => o.Outputs)
            .Where(o => o.Status == "COMPLETED" || o.Status == "IN_PRODUCTION" || o.Status == "APPROVED_FOR_STORAGE")
            .AsQueryable();

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            query = query.Where(o => o.ProductionReferenceNumber == referenceNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(s) ||
                                     o.ProductName.ToLower().Contains(s) ||
                                     o.ResponsibleEmployee.ToLower().Contains(s));
        }

        var list = await query.OrderByDescending(o => o.CreatedAt).Take(200).ToListAsync();
        var dtos = list.Select(MapOrderToDto).ToList();
        return Ok(ApiResponse<List<ProductionOrderDto>>.Ok(dtos));
    }

    [HttpGet("traceability/{referenceNumber:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductionTraceability(int referenceNumber)
    {
        var rawReport = await _db.LaboratoryRawMaterialReports
            .AsNoTracking()
            .Include(r => r.RawMaterial)
            .FirstOrDefaultAsync(r => r.ProductionReferenceNumber == referenceNumber);

        if (rawReport == null)
        {
            return NotFound(ApiResponse.Fail($"الرقم المرجعي التتبعي #{referenceNumber} غير موجود."));
        }

        var orders = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Batches)
            .Include(o => o.Outputs)
            .Where(o => o.ProductionReferenceNumber == referenceNumber)
            .ToListAsync();

        var batches = await _db.ProductionBatches
            .AsNoTracking()
            .Include(b => b.Outputs)
            .Where(b => b.ProductionReferenceNumber == referenceNumber)
            .ToListAsync();

        var inProcessTests = await _db.LaboratoryInProcessReports
            .AsNoTracking()
            .Where(t => t.ProductionReferenceNumber == referenceNumber)
            .OrderBy(t => t.TestDateTime)
            .ToListAsync();

        var finalCert = await _db.LaboratoryFinalConformityCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProductionReferenceNumber == referenceNumber);

        var reworks = await _db.ProductionReworks
            .AsNoTracking()
            .Where(r => r.ProductionReferenceNumber == referenceNumber)
            .ToListAsync();

        var result = new
        {
            productionReferenceNumber = referenceNumber,
            rawMaterialReport = rawReport,
            orders = orders.Select(MapOrderToDto).ToList(),
            batches = batches.Select(MapBatchToDto).ToList(),
            inProcessTests = inProcessTests.Select(LaboratoryController.MapInProcessToDto).ToList(),
            finalCertificate = finalCert != null ? LaboratoryController.MapFinalCertToDto(finalCert) : null,
            reworks = reworks
        };

        return Ok(ApiResponse<object>.Ok(result));
    }

    // =========================================================================
    // MAPPERS
    // =========================================================================
    public static ProductionOrderDto MapOrderToDto(ProductionOrder o)
    {
        return new ProductionOrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            ProductionReferenceNumber = o.ProductionReferenceNumber,
            ProductName = o.ProductName,
            RequestedQuantity = o.RequestedQuantity,
            Unit = o.Unit,
            StorageLocationId = o.StorageLocationId,
            StorageLocationName = o.StorageLocationName,
            Status = o.Status,
            CurrentStage = o.CurrentStage,
            ResponsibleEmployee = o.ResponsibleEmployee,
            PlannedStartDate = o.PlannedStartDate,
            ActualStartDate = o.ActualStartDate,
            CompletedDate = o.CompletedDate,
            Notes = o.Notes,
            AttachmentPath = o.AttachmentPath,
            AttachmentFileName = o.AttachmentFileName,
            IsStorageApproved = o.IsStorageApproved,
            StorageApprovedAt = o.StorageApprovedAt,
            StorageApprovedBy = o.StorageApprovedBy,
            StorageOperationId = o.StorageOperationId,
            CreatedAt = o.CreatedAt,
            CreatedBy = o.CreatedBy,
            Items = o.Items.Select(i => new ProductionOrderItemDto
            {
                Id = i.Id,
                ProductionOrderId = i.ProductionOrderId,
                RawMaterialId = i.RawMaterialId,
                MaterialName = i.MaterialName,
                RequestedQuantity = i.RequestedQuantity,
                IssuedQuantity = i.IssuedQuantity,
                ConsumedQuantity = i.ConsumedQuantity,
                Unit = i.Unit,
                StorageLocationId = i.StorageLocationId,
                Notes = i.Notes
            }).ToList(),
            Batches = o.Batches.Select(b => new ProductionBatchSummaryDto
            {
                Id = b.Id,
                BatchNumber = b.BatchNumber,
                CurrentStage = b.CurrentStage,
                Status = b.Status,
                QualityHold = b.QualityHold,
                QualityHoldReason = b.QualityHoldReason,
                TotalInputQuantity = b.TotalInputQuantity,
                TotalOutputQuantity = b.TotalOutputQuantity,
                StartedAt = b.StartedAt
            }).ToList(),
            Outputs = o.Outputs.Select(outp => new ProductionOutputSummaryDto
            {
                Id = outp.Id,
                OutputNumber = outp.OutputNumber,
                ProductName = outp.ProductName,
                Classification = outp.Classification,
                Quantity = outp.Quantity,
                Unit = outp.Unit,
                ProductionStage = outp.ProductionStage,
                LaboratoryConformityStatus = outp.LaboratoryConformityStatus,
                IsStorageReceived = outp.IsStorageReceived,
                OutputDate = outp.OutputDate
            }).ToList()
        };
    }

    public static ProductionBatchDto MapBatchToDto(ProductionBatch b)
    {
        return new ProductionBatchDto
        {
            Id = b.Id,
            BatchNumber = b.BatchNumber,
            ProductionOrderId = b.ProductionOrderId,
            OrderNumber = b.ProductionOrder?.OrderNumber ?? string.Empty,
            ProductionReferenceNumber = b.ProductionReferenceNumber,
            CurrentStage = b.CurrentStage,
            Status = b.Status,
            QualityHold = b.QualityHold,
            QualityHoldReason = b.QualityHoldReason,
            QualityHoldReportId = b.QualityHoldReportId,
            QualityHoldAt = b.QualityHoldAt,
            QualityHoldReleasedAt = b.QualityHoldReleasedAt,
            QualityHoldReleasedBy = b.QualityHoldReleasedBy,
            TotalInputQuantity = b.TotalInputQuantity,
            TotalOutputQuantity = b.TotalOutputQuantity,
            TotalWasteQuantity = b.TotalWasteQuantity,
            StartedAt = b.StartedAt,
            CompletedAt = b.CompletedAt,
            ResponsibleEmployee = b.ResponsibleEmployee,
            Notes = b.Notes,
            CreatedAt = b.CreatedAt,
            Outputs = b.Outputs.Select(outp => new ProductionOutputSummaryDto
            {
                Id = outp.Id,
                OutputNumber = outp.OutputNumber,
                ProductName = outp.ProductName,
                Classification = outp.Classification,
                Quantity = outp.Quantity,
                Unit = outp.Unit,
                ProductionStage = outp.ProductionStage,
                LaboratoryConformityStatus = outp.LaboratoryConformityStatus,
                IsStorageReceived = outp.IsStorageReceived,
                OutputDate = outp.OutputDate
            }).ToList()
        };
    }

    public static ProductionOutputDto MapOutputToDto(ProductionOutput outp)
    {
        return new ProductionOutputDto
        {
            Id = outp.Id,
            OutputNumber = outp.OutputNumber,
            ProductionOrderId = outp.ProductionOrderId,
            OrderNumber = outp.ProductionOrder?.OrderNumber ?? string.Empty,
            ProductionBatchId = outp.ProductionBatchId,
            BatchNumber = outp.ProductionBatch?.BatchNumber ?? string.Empty,
            ProductionReferenceNumber = outp.ProductionReferenceNumber,
            ProductName = outp.ProductName,
            Classification = outp.Classification,
            Quantity = outp.Quantity,
            Unit = outp.Unit,
            ProductionStage = outp.ProductionStage,
            OutputDate = outp.OutputDate,
            ResponsibleEmployee = outp.ResponsibleEmployee,
            NextStage = outp.NextStage,
            DestinationStorageLocationId = outp.DestinationStorageLocationId,
            DestinationStorageLocationName = outp.DestinationStorageLocationName,
            LaboratoryConformityStatus = outp.LaboratoryConformityStatus,
            LaboratoryCertificateId = outp.LaboratoryCertificateId,
            IsStorageReceived = outp.IsStorageReceived,
            StorageReceivedQuantity = outp.StorageReceivedQuantity,
            StorageReceivedAt = outp.StorageReceivedAt,
            StorageReceivedBy = outp.StorageReceivedBy,
            StorageOperationId = outp.StorageOperationId,
            NonConformityAction = outp.NonConformityAction,
            IsReworkCreated = outp.IsReworkCreated,
            ReworkOrderId = outp.ReworkOrderId,
            Notes = outp.Notes,
            AttachmentPath = outp.AttachmentPath,
            AttachmentFileName = outp.AttachmentFileName,
            CreatedAt = outp.CreatedAt,
            CreatedBy = outp.CreatedBy
        };
    }

    #region Production Process, Finished Product & Traceability Endpoints

    private async Task<(bool Success, string? ErrorMessage, string? StoredPath, string? OriginalFileName, string? ContentType, long FileSize)> ValidateAndSaveAttachmentAsync(
        IFormFile? file, string sectionNameAr)
    {
        if (file == null || file.Length == 0)
        {
            return (true, null, null, null, null, 0);
        }

        const long maxFileSize = 25 * 1024 * 1024; // 25 MB
        if (file.Length > maxFileSize)
        {
            return (false, $"حجم ملف مرفق {sectionNameAr} يتجاوز الحد الأقصى المسموح به (25 ميجابايت).", null, null, null, 0);
        }

        var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (fileExt != ".pdf" && fileExt != ".jpg" && fileExt != ".jpeg" && fileExt != ".png")
        {
            return (false, $"نوع ملف مرفق {sectionNameAr} غير مدعوم. الملفات المسموحة فقط: PDF, JPG, JPEG, PNG.", null, null, null, 0);
        }

        var mime = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var validMimes = new[] { "application/pdf", "image/jpeg", "image/pjpeg", "image/jpg", "image/png" };
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
            else if (fileExt == ".png")
            {
                if (magicBytes[0] != 0x89 || magicBytes[1] != 0x50 || magicBytes[2] != 0x4E || magicBytes[3] != 0x47)
                {
                    return (false, $"محتوى صورة PNG لمرفق {sectionNameAr} غير صالح.", null, null, null, 0);
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
        var uploadsDir = Path.Combine(webRoot, "uploads", "production", "weighbridges");
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

        var relativePath = $"/uploads/production/weighbridges/{uniqueFileName}";
        return (true, null, relativePath, Path.GetFileName(file.FileName), file.ContentType, file.Length);
    }

    /// <summary>
    /// Retrieve available warehouse batches ready for consumption in Production.
    /// Excludes consumed and rejected batches. Supports multi-material, multi-batch, multi-reference selection.
    /// </summary>
    [HttpGet("available-batches")]
    [ProducesResponseType(typeof(ApiResponse<List<WarehouseBatchAvailabilityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableBatches(
        [FromQuery] int? storageLocationId = null,
        [FromQuery] int? rawMaterialId = null,
        [FromQuery] int? referenceNumber = null,
        [FromQuery] string? search = null)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بالوصول لبيانات المستودع والإنتاج."));
        }

        var batches = await _inventoryService.GetAuthoritativeBatchesAsync(
            storageLocationId: storageLocationId,
            rawMaterialId: rawMaterialId,
            productionReferenceNumber: referenceNumber);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            batches = batches.Where(b =>
                b.BatchNumber.ToLower().Contains(s) ||
                b.SampleCode.ToLower().Contains(s) ||
                b.MaterialName.ToLower().Contains(s) ||
                b.StorageLocationName.ToLower().Contains(s)).ToList();
        }

        return Ok(ApiResponse<List<WarehouseBatchAvailabilityDto>>.Ok(batches));
    }

    /// <summary>
    /// Retrieve next auto-suggested identifiers for Production Process and Manufacture Number.
    /// </summary>
    [HttpGet("next-numbers")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNextNumbers()
    {
        var year = DateTime.UtcNow.Year;
        var seq = await _db.ProductionSequences.FirstOrDefaultAsync(s => s.SequenceType == "PROCESS");
        var nextProcessVal = seq?.NextValue ?? 1;
        var nextProcessNum = $"PRD-{year}-{nextProcessVal:D6}";

        var countMfg = await _db.ProductionFinishedProducts.CountAsync();
        var nextMfgNum = $"MFG-{year}-{(countMfg + 1):D4}";

        return Ok(ApiResponse<object>.Ok(new
        {
            NextProcessNumber = nextProcessNum,
            NextManufactureNumber = nextMfgNum
        }));
    }

    /// <summary>
    /// Returns transferred production orders ready to be processed in the Production Department.
    /// </summary>
    [HttpGet("ready-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<LaboratoryProductionOrderDetailDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReadyOrdersForProduction()
    {
        var orders = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Transfers)
            .Where(o => (o.Status == "TRANSFERRED_TO_PRODUCTION" || o.Status == "PARTIALLY_TRANSFERRED")
                        && o.LinkedProductionProcessId == null)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var batchIds = orders.SelectMany(o => o.Items)
            .Select(i => i.WarehouseBatchId ?? 0)
            .Where(b => b > 0)
            .Distinct()
            .ToList();

        var currentBatches = await _db.WarehouseBatches
            .AsNoTracking()
            .Where(b => batchIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.CurrentQuantity);

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
            StatusArabic = order.Status == "TRANSFERRED_TO_PRODUCTION" ? "تم النقل للإنتاج" : "تم النقل جزئياً",
            LaboratoryManagerName = order.LaboratoryManagerName ?? order.CreatedBy,
            FormulaVersion = order.FormulaVersion,
            ProductionInstructions = order.ProductionInstructions,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            ItemsCount = order.Items.Count,
            Items = order.Items.Select(i => new LaboratoryProductionOrderItemDto
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
                CurrentWarehouseBatchQuantity = i.WarehouseBatchId.HasValue && currentBatches.ContainsKey(i.WarehouseBatchId.Value) ? currentBatches[i.WarehouseBatchId.Value] : 0,
                RequestedQuantity = i.RequestedQuantity,
                IssuedQuantity = i.IssuedQuantity,
                Unit = i.Unit
            }).ToList()
        }).ToList();

        return Ok(ApiResponse<List<LaboratoryProductionOrderDetailDto>>.Ok(result));
    }

    /// <summary>
    /// Returns production orders eligible for finished product recording in the Production Department.
    /// Loaded dynamically from the database based on actual workflow status (transferred materials present).
    /// </summary>
    [HttpGet("orders/eligible-for-finished")]
    [ProducesResponseType(typeof(ApiResponse<List<EligibleProductionOrderForFinishedDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleOrdersForFinished()
    {
        var orders = await _db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Transfers)
            .Include(o => o.FinishedProducts)
            .Where(o => (o.Status == "TRANSFERRED_TO_PRODUCTION" || 
                         o.Status == "IN_PRODUCTION" || 
                         o.Status == "PARTIALLY_TRANSFERRED" || 
                         o.Status == "COMPLETED")
                        && (o.TotalTransferredQuantity > 0 || o.Transfers.Any(t => t.TransferredQuantity > 0)))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders.Select(order => new EligibleProductionOrderForFinishedDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            ProductName = order.ProductName,
            ProductDescription = order.ProductDescription,
            RequestedQuantity = order.RequestedQuantity,
            Unit = order.Unit,
            TotalPlannedRawMaterialQuantity = order.TotalPlannedRawMaterialQuantity,
            TotalTransferredQuantity = order.TotalTransferredQuantity > 0 ? order.TotalTransferredQuantity : order.Transfers.Sum(t => t.TransferredQuantity),
            LinkedProductionProcessId = order.LinkedProductionProcessId,
            LinkedProductionProcessNumber = order.LinkedProductionProcessNumber,
            Status = order.Status,
            StatusArabic = GetOrderStatusArabic(order.Status),
            CreatedAt = order.CreatedAt,
            FormattedDate = order.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            ProductionInstructions = order.ProductionInstructions,
            FinishedProductsCount = order.FinishedProducts.Count,
            Items = order.Items.Select(i => new LaboratoryProductionOrderItemDto
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
                CurrentWarehouseBatchQuantity = 0,
                RequestedQuantity = i.RequestedQuantity,
                IssuedQuantity = i.IssuedQuantity,
                Unit = i.Unit
            }).ToList(),
            Transfers = order.Transfers.Select(t => new LaboratoryProductionOrderTransferDto
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

        return Ok(ApiResponse<List<EligibleProductionOrderForFinishedDto>>.Ok(result));
    }

    private static string GetOrderStatusArabic(string status) => status switch
    {
        "PENDING_TRANSFER" => "في انتظار نقل المستودع",
        "PARTIALLY_TRANSFERRED" => "تم النقل جزئياً من المستودع",
        "TRANSFERRED_TO_PRODUCTION" => "تم النقل للإنتاج",
        "IN_PRODUCTION" => "قيد التصنيع بالإنتاج",
        "COMPLETED" => "مكتمل التصنيع",
        "CANCELLED" => "ملغي",
        _ => status
    };

    /// <summary>
    /// Create a new Production Process, selecting multiple Warehouse batches (supporting partial quantities),
    /// uploading the incoming weighbridge document, and atomically deducting from Warehouse inventory.
    /// </summary>
    [HttpPost("processes")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ProductionProcessDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateProductionProcess(
        [FromForm] CreateProductionProcessRequestDto dto,
        IFormFile? incomingWeighbridge = null)
    {
        return await CreateProductionProcessCoreAsync(dto, incomingWeighbridge);
    }

    [HttpPost("processes/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<ProductionProcessDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProductionProcessJson([FromBody] CreateProductionProcessRequestDto dto)
    {
        return await CreateProductionProcessCoreAsync(dto, null);
    }

    private async Task<IActionResult> CreateProductionProcessCoreAsync(
        CreateProductionProcessRequestDto dto,
        IFormFile? incomingWeighbridge = null)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بإنشاء عملية إنتاج."));
        }

        // Deserialize Materials if provided via MaterialsJson from client FormData
        var materials = dto.Materials ?? new List<CreateProductionProcessMaterialItemDto>();
        if (!string.IsNullOrWhiteSpace(dto.MaterialsJson))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<List<CreateProductionProcessMaterialItemDto>>(dto.MaterialsJson, options);
                if (parsed != null && parsed.Count > 0)
                {
                    materials = parsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse materialsJson in CreateProductionProcess");
                return BadRequest(ApiResponse.Fail("تنسيق بيانات المواد المختارة غير صالح."));
            }
        }

        if (materials.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب اختيار تشغيلة أو مادة أولية واحدة على الأقل لعملية الإنتاج."));
        }

        // Validate individual items
        foreach (var m in materials)
        {
            if (m.WarehouseBatchId <= 0)
            {
                return BadRequest(ApiResponse.Fail("معرف التشغيلة غير صحيح."));
            }
            if (m.ConsumedQuantity <= 0)
            {
                return BadRequest(ApiResponse.Fail("الكمية المستخدمة من كل تشغيلة يجب أن تكون أكبر من الصفر."));
            }
        }

        // Validate incoming weighbridge attachment if provided
        string? storedWeighbridgePath = null;
        string? storedWeighbridgeFileName = null;
        string? storedWeighbridgeFileType = null;
        long storedWeighbridgeFileSize = 0;

        if (incomingWeighbridge != null && incomingWeighbridge.Length > 0)
        {
            var attResult = await ValidateAndSaveAttachmentAsync(incomingWeighbridge, "ميزان البسكول للمواد الأولية");
            if (!attResult.Success)
            {
                return BadRequest(ApiResponse.Fail(attResult.ErrorMessage ?? "فشل التحقق من ملف ميزان البسكول."));
            }
            storedWeighbridgePath = attResult.StoredPath;
            storedWeighbridgeFileName = attResult.OriginalFileName;
            storedWeighbridgeFileType = attResult.ContentType;
            storedWeighbridgeFileSize = attResult.FileSize;
        }

        // Atomic Transaction Execution
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Sequential Process Number Generation
            var seq = await _db.ProductionSequences.FirstOrDefaultAsync(s => s.SequenceType == "PROCESS");
            if (seq == null)
            {
                seq = new ProductionSequence { SequenceType = "PROCESS", NextValue = 1 };
                _db.ProductionSequences.Add(seq);
                await _db.SaveChangesAsync();
            }

            var year = (dto.ProductionDate ?? DateTime.UtcNow).Year;
            var processNumber = $"PRD-{year}-{seq.NextValue:D6}";
            while (await _db.ProductionProcesses.AnyAsync(p => p.ProcessNumber == processNumber))
            {
                seq.NextValue++;
                processNumber = $"PRD-{year}-{seq.NextValue:D6}";
            }
            seq.NextValue++;

            // Batch Verification and Locking
            var batchIds = materials.Select(m => m.WarehouseBatchId).Distinct().ToList();
            var dbBatches = await _db.WarehouseBatches
                .Include(b => b.StorageLocation)
                .Include(b => b.RawMaterial)
                .Where(b => batchIds.Contains(b.Id))
                .ToListAsync();

            if (dbBatches.Count != batchIds.Count)
            {
                await transaction.RollbackAsync();
                return BadRequest(ApiResponse.Fail("إحدى التشغيلات المحددة غير موجودة في قاعدة البيانات."));
            }

            // Check if process is spawned from an approved Laboratory Production Order
            ProductionOrder? linkedOrder = null;
            if (dto.ProductionOrderId.HasValue && dto.ProductionOrderId.Value > 0)
            {
                linkedOrder = await _db.ProductionOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == dto.ProductionOrderId.Value);

                if (linkedOrder == null)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("أمر الإنتاج المرتبط غير موجود."));
                }

                if (linkedOrder.Status != "TRANSFERRED_TO_PRODUCTION" && linkedOrder.Status != "PARTIALLY_TRANSFERRED")
                {
                    await transaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"أمر الإنتاج المرتبط ({linkedOrder.OrderNumber}) ليس في حالة جاهزة للتصنيع (حالته الحالية: {linkedOrder.Status})."));
                }
            }

            if (linkedOrder == null)
            {
                // Direct Production: Verify stock sufficiency for every batch in warehouse
                foreach (var m in materials)
                {
                    var b = dbBatches.First(x => x.Id == m.WarehouseBatchId);
                    if (!b.IsActive || b.Status == "REJECTED")
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(ApiResponse.Fail($"التشغيلة {b.BatchNumber} مرفوضة أو غير نشطة ولا يمكن استخدامها في الإنتاج."));
                    }
                    if (b.CurrentQuantity < m.ConsumedQuantity)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(ApiResponse.Fail($"الكمية المطلوبة ({m.ConsumedQuantity} طن) من التشغيلة {b.BatchNumber} تتجاوز الرصيد المتاح حالياً ({b.CurrentQuantity} طن)."));
                    }
                }
            }
            else
            {
                // Order Production: Raw materials were already physically transferred from Warehouse
                foreach (var m in materials)
                {
                    var b = dbBatches.First(x => x.Id == m.WarehouseBatchId);
                    var orderItem = linkedOrder.Items.FirstOrDefault(oi => oi.WarehouseBatchId == m.WarehouseBatchId);
                    if (orderItem == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(ApiResponse.Fail($"التشغيلة {b.BatchNumber} غير مدرجة في تركيبة أمر الإنتاج {linkedOrder.OrderNumber}."));
                    }
                    if (m.ConsumedQuantity > orderItem.IssuedQuantity)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(ApiResponse.Fail($"الكمية المستهلكة ({m.ConsumedQuantity} طن) تتجاوز الكمية المنقولة فعلياً من المستودع ({orderItem.IssuedQuantity} طن) للتشغيلة {b.BatchNumber}."));
                    }
                }
            }

            var firstBatch = dbBatches.First();
            var totalInputQuantity = materials.Sum(m => m.ConsumedQuantity);

            // Create Production Process root
            var process = new ProductionProcess
            {
                ProcessNumber = processNumber,
                ProductionOrderId = linkedOrder?.Id,
                ProductionOrderNumber = linkedOrder?.OrderNumber,
                ProductionDate = dto.ProductionDate ?? DateTime.UtcNow,
                StorageLocationId = dto.StorageLocationId ?? firstBatch.StorageLocationId,
                StorageLocationName = firstBatch.StorageLocation != null ? firstBatch.StorageLocation.Name : "المستودع الرئيسي",
                TotalInputQuantity = totalInputQuantity,
                TotalOutputQuantity = 0,
                TotalWasteQuantity = 0,
                WastePercentage = 0,
                Unit = "طن",
                Status = "IN_PROGRESS",
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                Notes = dto.Notes,
                IncomingWeighbridgePath = storedWeighbridgePath,
                IncomingWeighbridgeFileName = storedWeighbridgeFileName,
                IncomingWeighbridgeFileType = storedWeighbridgeFileType,
                IncomingWeighbridgeFileSize = storedWeighbridgeFileSize,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            _db.ProductionProcesses.Add(process);
            await _db.SaveChangesAsync(); // Generates process.Id

            // Process Materials and Deduct from Warehouse Stock (ONLY if not from Order!)
            foreach (var m in materials)
            {
                var batch = dbBatches.First(x => x.Id == m.WarehouseBatchId);
                var availBefore = batch.CurrentQuantity;
                decimal remAfter = availBefore;

                if (linkedOrder == null)
                {
                    // Direct process: deduct from WarehouseBatch and write negative ledger entry
                    batch.CurrentQuantity -= m.ConsumedQuantity;
                    remAfter = batch.CurrentQuantity;

                    if (batch.CurrentQuantity <= 0)
                    {
                        batch.CurrentQuantity = 0;
                        batch.Status = "CONSUMED";
                    }

                    // Storage Inventory Ledger Deduction (MovementType = "PRODUCTION")
                    var currentStock = await _db.StorageInventoryLedger
                        .Where(l => l.StorageLocationId == batch.StorageLocationId && l.RawMaterialId == batch.RawMaterialId)
                        .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

                    var ledgerEntry = new StorageInventoryLedger
                    {
                        StorageLocationId = batch.StorageLocationId,
                        RawMaterialId = batch.RawMaterialId,
                        MovementType = "PRODUCTION",
                        Quantity = -m.ConsumedQuantity, // Negative stock deduction
                        RunningBalance = currentStock - m.ConsumedQuantity,
                        MovementDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName,
                        BatchNumber = batch.BatchNumber,
                        WarehouseBatchId = batch.Id
                    };
                    _db.StorageInventoryLedger.Add(ledgerEntry);
                }
                else
                {
                    // CRITICAL: Order raw materials were ALREADY deducted by Warehouse Transfer!
                    // Do NOT deduct again from WarehouseBatches or insert negative ledger entries.
                    remAfter = availBefore;
                }

                var procMat = new ProductionProcessMaterial
                {
                    ProductionProcessId = process.Id,
                    WarehouseBatchId = batch.Id,
                    BatchNumber = batch.BatchNumber,
                    LaboratoryReportId = batch.LaboratoryReportId,
                    ProductionReferenceNumber = batch.ProductionReferenceNumber,
                    SampleCode = batch.SampleCode,
                    RawMaterialId = batch.RawMaterialId,
                    MaterialName = batch.MaterialName,
                    MaterialClassification = batch.MaterialClassification,
                    StorageLocationId = batch.StorageLocationId,
                    StorageLocationName = batch.StorageLocation != null ? batch.StorageLocation.Name : string.Empty,
                    AvailableQuantityBefore = availBefore,
                    ConsumedQuantity = m.ConsumedQuantity,
                    RemainingQuantityAfter = remAfter,
                    Unit = "طن",
                    CreatedAt = DateTime.UtcNow
                };
                _db.ProductionProcessMaterials.Add(procMat);
            }

            if (linkedOrder != null)
            {
                linkedOrder.LinkedProductionProcessId = process.Id;
                linkedOrder.LinkedProductionProcessNumber = process.ProcessNumber;
                linkedOrder.Status = "IN_PRODUCTION";
                linkedOrder.CurrentStage = "قيد التصنيع في قسم الإنتاج";
            }

            // Record Attachment in ProductionAttachments if incoming weighbridge was uploaded
            if (!string.IsNullOrWhiteSpace(storedWeighbridgePath))
            {
                var att = new ProductionAttachment
                {
                    ProductionProcessId = process.Id,
                    FinishedProductId = null,
                    AttachmentType = "IncomingWeighbridge",
                    FilePath = storedWeighbridgePath,
                    FileName = storedWeighbridgeFileName ?? "weighbridge.pdf",
                    FileType = storedWeighbridgeFileType ?? "application/pdf",
                    FileSize = storedWeighbridgeFileSize,
                    UploadedBy = userName,
                    UploadedAt = DateTime.UtcNow
                };
                _db.ProductionAttachments.Add(att);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            // Reload for complete response DTO
            var loadedProcess = await _db.ProductionProcesses
                .Include(p => p.Materials)
                .Include(p => p.FinishedProducts)
                .Include(p => p.StorageLocation)
                .FirstAsync(p => p.Id == process.Id);

            return Ok(ApiResponse<ProductionProcessDetailDto>.Ok(
                MapProcessToDto(loadedProcess),
                $"تم إنشاء عملية الإنتاج بنجاح برقم {processNumber} وخصم المواد من المستودع."));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating production process");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ عملية الإنتاج وخصم المواد من المستودع."));
        }
    }

    /// <summary>
    /// List all Production Processes with summary metrics, status, and filter support.
    /// </summary>
    [HttpGet("processes")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionProcessDetailDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductionProcesses(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بالوصول لسجل عمليات الإنتاج."));
        }

        var query = _db.ProductionProcesses
            .AsNoTracking()
            .Include(p => p.Materials)
            .Include(p => p.FinishedProducts)
            .Include(p => p.StorageLocation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status.ToLower() == status.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.ProcessNumber.ToLower().Contains(s) ||
                p.ResponsibleEmployee.ToLower().Contains(s) ||
                p.Materials.Any(m => m.BatchNumber.ToLower().Contains(s) || m.SampleCode.ToLower().Contains(s) || m.ProductionReferenceNumber.ToString().Contains(s)) ||
                p.FinishedProducts.Any(f => f.ManufactureNumber.ToLower().Contains(s) || f.FinishedProductName.ToLower().Contains(s)));
        }

        var processes = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 200))
            .Take(Math.Clamp(pageSize, 1, 200))
            .ToListAsync();

        var dtos = processes.Select(MapProcessToDto).ToList();
        return Ok(ApiResponse<List<ProductionProcessDetailDto>>.Ok(dtos));
    }

    /// <summary>
    /// Retrieve list of Production Processes eligible for Finished Product registration.
    /// Exposes process number, order details, target product name, consumed materials, and remaining inputs.
    /// </summary>
    [HttpGet("eligible-processes")]
    [ProducesResponseType(typeof(ApiResponse<List<EligibleProductionProcessDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleProductionProcesses()
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بالوصول لعمليات الإنتاج."));
        }

        var processes = await _db.ProductionProcesses
            .AsNoTracking()
            .Include(p => p.Materials)
            .Include(p => p.FinishedProducts)
            .Include(p => p.ProductionOrder)
            .Where(p => p.Status != "CANCELLED")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var list = processes.Select(p =>
        {
            var totalInput = p.TotalInputQuantity > 0 
                ? p.TotalInputQuantity 
                : p.Materials.Sum(m => m.ConsumedQuantity);
            var totalOutput = p.FinishedProducts.Sum(f => f.ManufacturedQuantity);
            var rem = Math.Max(0m, totalInput - totalOutput);
            var targetProduct = !string.IsNullOrWhiteSpace(p.ProductionOrder?.ProductName)
                ? p.ProductionOrder.ProductName
                : (p.FinishedProducts.FirstOrDefault()?.FinishedProductName ?? "منتج خط الإنتاج");

            return new EligibleProductionProcessDto
            {
                Id = p.Id,
                ProcessNumber = p.ProcessNumber,
                ProductionOrderId = p.ProductionOrderId,
                ProductionOrderNumber = p.ProductionOrderNumber ?? p.ProductionOrder?.OrderNumber,
                TargetProductName = targetProduct,
                TotalInputQuantity = totalInput,
                TotalOutputQuantity = totalOutput,
                RemainingInputQuantity = rem,
                Unit = p.Unit ?? "طن",
                ProductionDate = p.ProductionDate,
                ResponsibleEmployee = p.ResponsibleEmployee,
                Status = p.Status,
                ConsumedMaterials = p.Materials.Select(m => new ProductionProcessMaterialDto
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
                    Unit = m.Unit
                }).ToList()
            };
        }).ToList();

        return Ok(ApiResponse<List<EligibleProductionProcessDto>>.Ok(list));
    }

    /// <summary>
    /// Retrieve single Production Process by ID.
    /// </summary>
    [HttpGet("processes/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductionProcessDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductionProcessById(int id)
    {
        var process = await _db.ProductionProcesses
            .AsNoTracking()
            .Include(p => p.Materials)
            .Include(p => p.FinishedProducts)
            .Include(p => p.StorageLocation)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (process == null)
        {
            return NotFound(ApiResponse.Fail("عملية الإنتاج المطلوبة غير موجودة."));
        }

        return Ok(ApiResponse<ProductionProcessDetailDto>.Ok(MapProcessToDto(process)));
    }

    /// <summary>
    /// Retrieve single Production Process by Process Number (e.g. PRD-2026-000001).
    /// </summary>
    [HttpGet("processes/by-number/{processNumber}")]
    [ProducesResponseType(typeof(ApiResponse<ProductionProcessDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductionProcessByNumber(string processNumber)
    {
        if (string.IsNullOrWhiteSpace(processNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم عملية الإنتاج مطلوب."));
        }

        var trimmed = processNumber.Trim();
        var process = await _db.ProductionProcesses
            .AsNoTracking()
            .Include(p => p.Materials)
            .Include(p => p.FinishedProducts)
            .Include(p => p.StorageLocation)
            .FirstOrDefaultAsync(p => p.ProcessNumber.ToLower() == trimmed.ToLower());

        if (process == null)
        {
            return NotFound(ApiResponse.Fail($"عملية الإنتاج '{trimmed}' غير موجودة."));
        }

        return Ok(ApiResponse<ProductionProcessDetailDto>.Ok(MapProcessToDto(process)));
    }

    /// <summary>
    /// Retrieve list of finished product manufacturing records with optional filters.
    /// </summary>
    [HttpGet("finished-products")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionFinishedProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinishedProducts(
        [FromQuery] int? processId = null,
        [FromQuery] int? orderId = null,
        [FromQuery] string? search = null)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك باستعراض المنتجات النهائية."));
        }

        var query = _db.ProductionFinishedProducts
            .AsNoTracking()
            .Include(f => f.ProductionProcess)
            .Include(f => f.ProductionOrder)
            .AsQueryable();

        if (processId.HasValue && processId.Value > 0)
        {
            query = query.Where(f => f.ProductionProcessId == processId.Value);
        }

        if (orderId.HasValue && orderId.Value > 0)
        {
            query = query.Where(f => f.ProductionOrderId == orderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(f =>
                f.FinishedProductName.ToLower().Contains(s) ||
                f.ManufactureNumber.ToLower().Contains(s) ||
                (f.ProductionOrderNumber != null && f.ProductionOrderNumber.ToLower().Contains(s)) ||
                (f.ProductionProcess != null && f.ProductionProcess.ProcessNumber.ToLower().Contains(s)));
        }

        var list = await query
            .OrderByDescending(f => f.ProductionDate)
            .ThenByDescending(f => f.Id)
            .ToListAsync();

        var dtos = list.Select(f => new ProductionFinishedProductDto
        {
            Id = f.Id,
            ProductionProcessId = f.ProductionProcessId,
            ProcessNumber = f.ProductionProcess?.ProcessNumber ?? string.Empty,
            ProductionOrderId = f.ProductionOrderId,
            ProductionOrderNumber = f.ProductionOrderNumber ?? f.ProductionOrder?.OrderNumber,
            FinishedProductName = f.FinishedProductName,
            ManufactureNumber = f.ManufactureNumber,
            ManufacturedQuantity = f.ManufacturedQuantity,
            Unit = f.Unit,
            DestinationStorageLocationId = f.DestinationStorageLocationId,
            DestinationStorageLocationName = f.DestinationStorageLocationName,
            ProductionDate = f.ProductionDate,
            Notes = f.Notes,
            WeighbridgeAttachmentPath = f.WeighbridgeAttachmentPath,
            WeighbridgeFileName = f.WeighbridgeFileName,
            WeighbridgeFileType = f.WeighbridgeFileType,
            WeighbridgeFileSize = f.WeighbridgeFileSize,
            CreatedAt = f.CreatedAt,
            CreatedBy = f.CreatedBy
        }).ToList();

        return Ok(ApiResponse<List<ProductionFinishedProductDto>>.Ok(dtos));
    }

    /// <summary>
    /// Record Finished Product manufactured from a Production Order & Process, upload finished weighbridge attachment,
    /// perform mass balance validation, and calculate waste accurately.
    /// </summary>
    [HttpPost("finished-products")]
    [HttpPost("processes/{id:int}/finished-products")]
    [HttpPost("orders/{orderId:int}/finished-products")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ProductionProcessDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordFinishedProduct(
        [FromForm] CreateFinishedProductRequestDto dto,
        int? id = null,
        int? orderId = null,
        IFormFile? weighbridgeFile = null)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بتسجيل المنتج النهائي."));
        }

        // Determine effective ProductionOrderId
        int effectiveOrderId = orderId.HasValue && orderId.Value > 0 ? orderId.Value : dto.ProductionOrderId;
        if (effectiveOrderId == 0 && id.HasValue && id.Value > 0)
        {
            var pLookup = await _db.ProductionProcesses.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id.Value);
            if (pLookup?.ProductionOrderId != null) effectiveOrderId = pLookup.ProductionOrderId.Value;
        }
        if (effectiveOrderId == 0 && dto.ProductionProcessId.HasValue && dto.ProductionProcessId.Value > 0)
        {
            var pLookup = await _db.ProductionProcesses.AsNoTracking().FirstOrDefaultAsync(p => p.Id == dto.ProductionProcessId.Value);
            if (pLookup?.ProductionOrderId != null) effectiveOrderId = pLookup.ProductionOrderId.Value;
        }

        if (effectiveOrderId == 0)
        {
            return BadRequest(ApiResponse.Fail("أمر الإنتاج (Production Order) مطلوب لتسجيل واعتماد المنتج النهائي."));
        }

        var parentOrder = await _db.ProductionOrders
            .Include(o => o.Items)
            .Include(o => o.Transfers)
            .Include(o => o.FinishedProducts)
            .FirstOrDefaultAsync(o => o.Id == effectiveOrderId);

        if (parentOrder == null)
        {
            return NotFound(ApiResponse.Fail("أمر الإنتاج المحدد غير موجود في قاعدة البيانات."));
        }

        if (parentOrder.TotalTransferredQuantity <= 0 && !parentOrder.Transfers.Any(t => t.TransferredQuantity > 0))
        {
            return BadRequest(ApiResponse.Fail($"أمر الإنتاج ({parentOrder.OrderNumber}) لم يتم نقل أي مواد خام له من المستودع بعد. لا يمكن تسجيل منتج نهائي قبل تأكيد نقل المواد."));
        }

        // Basic Validations
        if (string.IsNullOrWhiteSpace(dto.FinishedProductName))
        {
            return BadRequest(ApiResponse.Fail("اسم المنتج النهائي مطلوب."));
        }

        if (string.IsNullOrWhiteSpace(dto.ManufactureNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم التصنيع (Manufacture Number) مطلوب."));
        }

        if (dto.ManufacturedQuantity <= 0)
        {
            return BadRequest(ApiResponse.Fail("كمية المنتج المصنع يجب أن تكون أكبر من الصفر."));
        }

        // Validate manufacture number uniqueness across finished products
        var existingMfg = await _db.ProductionFinishedProducts
            .AnyAsync(f => f.ManufactureNumber.ToLower() == dto.ManufactureNumber.Trim().ToLower());
        if (existingMfg)
        {
            return BadRequest(ApiResponse.Fail($"رقم التصنيع '{dto.ManufactureNumber}' مسجل مسبقاً في النظام. يرجى استخدام رقم تصنيع فريد."));
        }

        // Unit compatibility check (tons standard)
        var unit = string.IsNullOrWhiteSpace(dto.Unit) ? "طن" : dto.Unit.Trim();
        if (unit != "طن" && !string.Equals(unit, "ton", StringComparison.OrdinalIgnoreCase) && !string.Equals(unit, "tons", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse.Fail("وحدة القياس المعتمدة لحسابات التوازن الكتلي للإنتاج هي 'طن'."));
        }

        // Validate Destination Storage Location
        if (!dto.DestinationStorageLocationId.HasValue || dto.DestinationStorageLocationId.Value <= 0)
        {
            return BadRequest(ApiResponse.Fail("يرجى اختيار مستودع وجهة تخزين المنتج النهائي."));
        }

        var destLocation = await _db.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.DestinationStorageLocationId.Value && l.IsActive);
        if (destLocation == null)
        {
            return BadRequest(ApiResponse.Fail("مستودع وجهة تخزين المنتج النهائي المحدد غير موجود أو غير نشط."));
        }

        // Attachment validation - mandatory weighbridge
        string? storedPath = null;
        string? storedOrigName = null;
        string? storedContentType = null;
        long storedFileSize = 0;

        if (weighbridgeFile == null || weighbridgeFile.Length == 0)
        {
            return BadRequest(ApiResponse.Fail("مرفق ميزان بسكول المنتج النهائي إلزامي لتسجيل واعتماد المنتج النهائي."));
        }

        var attResult = await ValidateAndSaveAttachmentAsync(weighbridgeFile, "ميزان بسكول المنتج النهائي");
        if (!attResult.Success)
        {
            return BadRequest(ApiResponse.Fail(attResult.ErrorMessage ?? "فشل التحقق من ملف مرفق ميزان البسكول للمنتج النهائي."));
        }
        storedPath = attResult.StoredPath;
        storedOrigName = attResult.OriginalFileName;
        storedContentType = attResult.ContentType;
        storedFileSize = attResult.FileSize;

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Determine or create linked ProductionProcess
            int? targetProcessId = (id.HasValue && id.Value > 0) ? id.Value : (dto.ProductionProcessId ?? parentOrder.LinkedProductionProcessId);
            ProductionProcess? process = null;
            if (targetProcessId.HasValue && targetProcessId.Value > 0)
            {
                process = await _db.ProductionProcesses
                    .Include(p => p.Materials)
                    .Include(p => p.FinishedProducts)
                    .Include(p => p.StorageLocation)
                    .FirstOrDefaultAsync(p => p.Id == targetProcessId.Value);
            }

            if (process == null)
            {
                var year = (dto.ProductionDate ?? DateTime.UtcNow).Year;
                var seq = await _db.ProductionSequences.FirstOrDefaultAsync(s => s.SequenceType == "PRD_PROC");
                if (seq == null)
                {
                    seq = new ProductionSequence { SequenceType = "PRD_PROC", NextValue = 1 };
                    _db.ProductionSequences.Add(seq);
                }
                var processNumber = $"PRD-{year}-{seq.NextValue:D6}";
                while (await _db.ProductionProcesses.AnyAsync(p => p.ProcessNumber == processNumber))
                {
                    seq.NextValue++;
                    processNumber = $"PRD-{year}-{seq.NextValue:D6}";
                }
                seq.NextValue++;

                var totalInputQuantity = parentOrder.TotalTransferredQuantity > 0 
                    ? parentOrder.TotalTransferredQuantity 
                    : parentOrder.Transfers.Sum(t => t.TransferredQuantity);

                process = new ProductionProcess
                {
                    ProcessNumber = processNumber,
                    ProductionOrderId = parentOrder.Id,
                    ProductionOrderNumber = parentOrder.OrderNumber,
                    ProductionDate = dto.ProductionDate ?? DateTime.UtcNow,
                    StorageLocationId = parentOrder.StorageLocationId,
                    StorageLocationName = parentOrder.StorageLocationName,
                    TotalInputQuantity = totalInputQuantity,
                    TotalOutputQuantity = 0,
                    TotalWasteQuantity = 0,
                    WastePercentage = 0,
                    Unit = "طن",
                    Status = "IN_PROGRESS",
                    ResponsibleEmployee = userName,
                    ResponsibleUserId = userId,
                    Notes = $"عملية إنتاج مرتبطة بأمر الإنتاج {parentOrder.OrderNumber} - {dto.Notes}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName
                };
                _db.ProductionProcesses.Add(process);
                await _db.SaveChangesAsync(); // Generates process.Id

                foreach (var tr in parentOrder.Transfers)
                {
                    var procMat = new ProductionProcessMaterial
                    {
                        ProductionProcessId = process.Id,
                        WarehouseBatchId = tr.WarehouseBatchId,
                        BatchNumber = tr.BatchNumber,
                        LaboratoryReportId = null,
                        ProductionReferenceNumber = tr.ProductionReferenceNumber ?? 0,
                        SampleCode = string.Empty,
                        RawMaterialId = tr.RawMaterialId,
                        MaterialName = tr.MaterialName,
                        MaterialClassification = "RAW_MATERIAL",
                        StorageLocationId = tr.StorageLocationId,
                        StorageLocationName = tr.StorageLocationName,
                        AvailableQuantityBefore = tr.TransferredQuantity,
                        ConsumedQuantity = tr.TransferredQuantity,
                        RemainingQuantityAfter = 0,
                        Unit = tr.Unit,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.ProductionProcessMaterials.Add(procMat);
                }

                parentOrder.LinkedProductionProcessId = process.Id;
                parentOrder.LinkedProductionProcessNumber = process.ProcessNumber;
                parentOrder.Status = "IN_PRODUCTION";
                parentOrder.CurrentStage = "قيد التصنيع في قسم الإنتاج";
                await _db.SaveChangesAsync();
            }
            else
            {
                if (!process.ProductionOrderId.HasValue)
                {
                    process.ProductionOrderId = parentOrder.Id;
                    process.ProductionOrderNumber = parentOrder.OrderNumber;
                }
            }

            // Mass balance validation
            var totalInput = process.TotalInputQuantity > 0 ? process.TotalInputQuantity : parentOrder.TotalTransferredQuantity;
            if (process.TotalInputQuantity <= 0 && totalInput > 0)
            {
                process.TotalInputQuantity = totalInput;
            }

            var existingOutput = await _db.ProductionFinishedProducts
                .Where(f => f.ProductionProcessId == process.Id)
                .SumAsync(f => f.ManufacturedQuantity);
            var newTotalOutput = existingOutput + dto.ManufacturedQuantity;

            if (newTotalOutput > totalInput)
            {
                await transaction.RollbackAsync();
                return BadRequest(ApiResponse.Fail(
                    $"تنبيه تدقيق الكتلة: كمية المخرجات الإجمالية ({newTotalOutput:N3} طن) تتجاوز إجمالي مدخلات المواد المنقولة في العملية ({totalInput:N3} طن). لا يمكن أن تكون كمية الهالك سالبة."));
            }

            var wasteQuantity = totalInput - newTotalOutput;
            var wastePercentage = totalInput > 0 ? Math.Round((wasteQuantity / totalInput) * 100m, 2) : 0m;

            var finishedProduct = new ProductionFinishedProduct
            {
                ProductionProcessId = process.Id,
                ProductionOrderId = parentOrder?.Id,
                ProductionOrderNumber = parentOrder?.OrderNumber,
                FinishedProductName = dto.FinishedProductName.Trim(),
                ManufactureNumber = dto.ManufactureNumber.Trim(),
                ManufacturedQuantity = dto.ManufacturedQuantity,
                Unit = unit,
                DestinationStorageLocationId = destLocation.Id,
                DestinationStorageLocationName = destLocation.Name,
                ProductionDate = dto.ProductionDate ?? DateTime.UtcNow,
                Notes = dto.Notes,
                WeighbridgeAttachmentPath = storedPath,
                WeighbridgeFileName = storedOrigName,
                WeighbridgeFileType = storedContentType,
                WeighbridgeFileSize = storedFileSize,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };

            _db.ProductionFinishedProducts.Add(finishedProduct);
            await _db.SaveChangesAsync();

            // Update Process totals and status
            process.TotalOutputQuantity = newTotalOutput;
            process.TotalWasteQuantity = wasteQuantity;
            process.WastePercentage = wastePercentage;
            process.Status = "COMPLETED";

            // Create ProductionWasteRecord
            var wasteRecord = new ProductionWasteRecord
            {
                ProductionProcessId = process.Id,
                TotalInputQuantity = totalInput,
                TotalOutputQuantity = newTotalOutput,
                WasteQuantity = wasteQuantity,
                WastePercentage = wastePercentage,
                Unit = "طن",
                CalculatedAt = DateTime.UtcNow,
                CalculatedBy = userName
            };
            _db.ProductionWasteRecords.Add(wasteRecord);

            // Record Attachment if provided
            if (!string.IsNullOrWhiteSpace(storedPath))
            {
                var att = new ProductionAttachment
                {
                    ProductionProcessId = process.Id,
                    FinishedProductId = finishedProduct.Id,
                    AttachmentType = "FinishedProductWeighbridge",
                    FilePath = storedPath,
                    FileName = storedOrigName ?? "finished_product_weighbridge.pdf",
                    FileType = storedContentType ?? "application/pdf",
                    FileSize = storedFileSize,
                    UploadedBy = userName,
                    UploadedAt = DateTime.UtcNow
                };
                _db.ProductionAttachments.Add(att);
            }

            // --- Atomically create Finished Product Stock Receipt in Destination Warehouse ---
            var finishedRawMat = await _db.RawMaterials.FirstOrDefaultAsync(m => m.Name == finishedProduct.FinishedProductName);
            if (finishedRawMat == null)
            {
                finishedRawMat = new RawMaterial
                {
                    Name = finishedProduct.FinishedProductName,
                    MaterialType = MaterialProcessType.FinalProduct,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userName,
                    CreatedByUserId = userId
                };
                _db.RawMaterials.Add(finishedRawMat);
                await _db.SaveChangesAsync();
            }

            var seqImport = await _db.StorageSequences.FirstOrDefaultAsync(s => s.SequenceType == "IMPORT");
            int txNum = 1;
            if (seqImport == null)
            {
                seqImport = new StorageSequence { SequenceType = "IMPORT", NextValue = 2 };
                _db.StorageSequences.Add(seqImport);
            }
            else
            {
                txNum = seqImport.NextValue++;
            }

            var txDate = finishedProduct.ProductionDate;
            var op = new StorageOperation
            {
                OperationType = "IMPORT",
                TransactionNumber = txNum,
                TransactionDate = txDate,
                StorageLocationId = destLocation.Id,
                StorageLocationName = destLocation.Name,
                MaterialType = "FINAL_PRODUCT",
                Source = "قسم الإنتاج والتصنيع",
                SourcePhone = "داخلي",
                DestinationDepartment = "المستودع",
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                Notes = $"استلام وتخزين منتج نهائي من خط الإنتاج - عملية {process.ProcessNumber} - تصنيع {finishedProduct.ManufactureNumber} - {dto.Notes}",
                ApprovalStatus = "APPROVED",
                ReviewedBy = userName,
                ReviewedByUserId = userId,
                ReviewedAt = DateTime.UtcNow,
                IsInventoryPosted = true,
                InternalLoadedWeight = finishedProduct.ManufacturedQuantity,
                InternalEmptyWeight = 0,
                InternalNetWeight = finishedProduct.ManufacturedQuantity,
                WeighbridgeReference = finishedProduct.ManufactureNumber,
                WeighbridgeAttachmentPath = storedPath,
                WeighbridgeFileName = storedOrigName,
                WeighbridgeFileType = storedContentType,
                WeighbridgeFileSize = storedFileSize,
                ExportType = "FINISHED_PRODUCT",
                ProductionOrderId = parentOrder?.Id,
                ProductionProcessId = process.Id,
                ManufactureNumber = finishedProduct.ManufactureNumber,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                CreatedByUserId = userId
            };
            _db.StorageOperations.Add(op);
            await _db.SaveChangesAsync();

            var opItem = new StorageOperationItem
            {
                StorageOperationId = op.Id,
                RawMaterialId = finishedRawMat.Id,
                MaterialName = finishedRawMat.Name,
                Quantity = finishedProduct.ManufacturedQuantity,
                TrackingNumber = $"FP-{finishedProduct.ManufactureNumber}",
                ProductionOrderId = parentOrder?.Id,
                ProductionProcessId = process.Id,
                ManufactureNumber = finishedProduct.ManufactureNumber
            };
            _db.StorageOperationItems.Add(opItem);
            await _db.SaveChangesAsync();

            var existingBalance = await _db.StorageInventoryLedger
                .Where(l => l.StorageLocationId == destLocation.Id && l.RawMaterialId == finishedRawMat.Id)
                .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

            var ledgerEntry = new StorageInventoryLedger
            {
                StorageLocationId = destLocation.Id,
                RawMaterialId = finishedRawMat.Id,
                StorageOperationId = op.Id,
                StorageOperationItem = opItem,
                MovementType = "FINAL_PRODUCT_INFLOW",
                Quantity = finishedProduct.ManufacturedQuantity,
                RunningBalance = existingBalance + finishedProduct.ManufacturedQuantity,
                BatchNumber = finishedProduct.ManufactureNumber,
                MovementDate = txDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName
            };
            _db.StorageInventoryLedger.Add(ledgerEntry);

            int effectiveLabReportId = 0;
            if (parentOrder != null && parentOrder.Transfers.Any())
            {
                effectiveLabReportId = await _db.WarehouseBatches
                    .Where(b => parentOrder.Transfers.Select(t => t.WarehouseBatchId).Contains(b.Id))
                    .Select(b => b.LaboratoryReportId)
                    .FirstOrDefaultAsync();
            }

            if (effectiveLabReportId == 0)
            {
                effectiveLabReportId = await _db.LaboratoryRawMaterialReports.OrderByDescending(r => r.Id).Select(r => r.Id).FirstOrDefaultAsync();
            }

            int effectivePrn = parentOrder?.Items.FirstOrDefault()?.ProductionReferenceNumber 
                ?? await _db.LaboratoryRawMaterialReports.Where(r => r.Id == effectiveLabReportId).Select(r => r.ProductionReferenceNumber).FirstOrDefaultAsync();

            var destBatch = new WarehouseBatch
            {
                StorageOperationId = op.Id,
                StorageOperationItemId = opItem.Id,
                LaboratoryReportId = effectiveLabReportId,
                ProductionReferenceNumber = effectivePrn,
                ProductionOrderId = parentOrder?.Id,
                ProductionProcessId = process.Id,
                SampleCode = finishedProduct.ManufactureNumber,
                BatchNumber = finishedProduct.ManufactureNumber,
                RawMaterialId = finishedRawMat.Id,
                MaterialName = finishedRawMat.Name,
                MaterialClassification = "FINAL_PRODUCT",
                SupplierName = "قسم الإنتاج والتصنيع",
                StorageLocationId = destLocation.Id,
                InitialQuantity = finishedProduct.ManufacturedQuantity,
                CurrentQuantity = finishedProduct.ManufacturedQuantity,
                ImportDate = txDate,
                Status = "APPROVED",
                IsActive = true,
                Notes = $"منتج نهائي من عملية إنتاج {process.ProcessNumber}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userName,
                CreatedByUserId = userId
            };
            _db.WarehouseBatches.Add(destBatch);

            if (parentOrder != null)
            {
                parentOrder.Status = "COMPLETED";
                parentOrder.CurrentStage = "مكتمل التصنيع";
                parentOrder.CompletedDate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            try
            {
                await _hubContext.Clients.All.SendAsync("OnDepartmentDataChanged", new
                {
                    Department = "Production",
                    Action = "FINISHED_PRODUCT_RECORDED",
                    ProcessId = process.Id,
                    ProcessNumber = process.ProcessNumber,
                    OrderId = parentOrder?.Id,
                    OrderNumber = parentOrder?.OrderNumber,
                    ManufactureNumber = finishedProduct.ManufactureNumber,
                    Timestamp = DateTime.UtcNow
                });
                if (parentOrder != null)
                {
                    await _hubContext.Clients.All.SendAsync("OnProductionOrderChanged", new
                    {
                        OrderId = parentOrder.Id,
                        OrderNumber = parentOrder.OrderNumber,
                        Status = parentOrder.Status,
                        Timestamp = DateTime.UtcNow
                    });
                }
                await _hubContext.Clients.All.SendAsync("OnWarehouseInventoryChanged", new
                {
                    Action = "FINISHED_PRODUCT_STORED",
                    LocationId = destLocation.Id,
                    LocationName = destLocation.Name,
                    ProductName = finishedProduct.FinishedProductName,
                    ManufactureNumber = finishedProduct.ManufactureNumber,
                    Quantity = finishedProduct.ManufacturedQuantity,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception exSignal)
            {
                _logger.LogWarning(exSignal, "Failed to broadcast finished product SignalR event");
            }

            var orderLabel = parentOrder != null ? $"لأمر الإنتاج {parentOrder.OrderNumber}" : $"للعملية {process.ProcessNumber}";
            return Ok(ApiResponse<ProductionProcessDetailDto>.Ok(
                MapProcessToDto(process),
                $"تم تسجيل المنتج النهائي '{finishedProduct.FinishedProductName}' برقم تصنيع {finishedProduct.ManufactureNumber} {orderLabel} وحساب الهالك ({wasteQuantity:N3} طن - {wastePercentage}%) بنجاح وإيداعه في مستودع '{destLocation.Name}'."));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error recording finished product");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ المنتج النهائي: " + ex.Message));
        }
    }

    /// <summary>
    /// Comprehensive Traceability Search across Production Process ID, Laboratory Reference Number, Batch Number, and Manufacture Number.
    /// Exposes full genealogy from warehouse materials to finished products and transportation-ready outputs.
    /// </summary>
    [HttpGet("traceability")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductionTraceabilityResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchTraceability(
        [FromQuery] string? query = null,
        [FromQuery] string? processNumber = null,
        [FromQuery] int? referenceNumber = null,
        [FromQuery] string? batchNumber = null,
        [FromQuery] string? manufactureNumber = null,
        [FromQuery] string? orderNumber = null)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بالوصول لبيانات التتبع."));
        }

        var baseQuery = _db.ProductionProcesses
            .AsNoTracking()
            .Include(p => p.Materials)
            .Include(p => p.FinishedProducts)
            .Include(p => p.StorageLocation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(processNumber))
        {
            var pn = processNumber.Trim().ToLower();
            baseQuery = baseQuery.Where(p => p.ProcessNumber.ToLower() == pn);
        }

        if (!string.IsNullOrWhiteSpace(orderNumber))
        {
            var on = orderNumber.Trim().ToLower();
            baseQuery = baseQuery.Where(p => (p.ProductionOrderNumber != null && p.ProductionOrderNumber.ToLower() == on) ||
                                             p.FinishedProducts.Any(f => f.ProductionOrderNumber != null && f.ProductionOrderNumber.ToLower() == on));
        }

        if (referenceNumber.HasValue && referenceNumber.Value > 0)
        {
            baseQuery = baseQuery.Where(p => p.Materials.Any(m => m.ProductionReferenceNumber == referenceNumber.Value));
        }

        if (!string.IsNullOrWhiteSpace(batchNumber))
        {
            var bn = batchNumber.Trim().ToLower();
            baseQuery = baseQuery.Where(p => p.Materials.Any(m => m.BatchNumber.ToLower() == bn));
        }

        if (!string.IsNullOrWhiteSpace(manufactureNumber))
        {
            var mn = manufactureNumber.Trim().ToLower();
            baseQuery = baseQuery.Where(p => p.FinishedProducts.Any(f => f.ManufactureNumber.ToLower() == mn));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            baseQuery = baseQuery.Where(p =>
                p.ProcessNumber.ToLower().Contains(q) ||
                p.ResponsibleEmployee.ToLower().Contains(q) ||
                (p.ProductionOrderNumber != null && p.ProductionOrderNumber.ToLower().Contains(q)) ||
                p.Materials.Any(m => m.BatchNumber.ToLower().Contains(q) || m.SampleCode.ToLower().Contains(q) || m.ProductionReferenceNumber.ToString().Contains(q) || m.MaterialName.ToLower().Contains(q)) ||
                p.FinishedProducts.Any(f => f.ManufactureNumber.ToLower().Contains(q) || f.FinishedProductName.ToLower().Contains(q) || (f.ProductionOrderNumber != null && f.ProductionOrderNumber.ToLower().Contains(q))));
        }

        var results = await baseQuery
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .ToListAsync();

        var list = results.Select(p => new ProductionTraceabilityResultDto
        {
            Process = MapProcessToDto(p),
            ProductionOrderId = p.ProductionOrderId,
            ProductionOrderNumber = p.ProductionOrderNumber,
            RelatedReferenceNumbers = p.Materials.Select(m => m.ProductionReferenceNumber).Distinct().ToList(),
            RelatedBatchNumbers = p.Materials.Select(m => m.BatchNumber).Distinct().ToList(),
            RelatedManufactureNumbers = p.FinishedProducts.Select(f => f.ManufactureNumber).Distinct().ToList(),
            Warehouses = p.Materials.Select(m => m.StorageLocationName).Where(w => !string.IsNullOrEmpty(w)).Distinct().ToList(),
            RawMaterialsSummary = p.Materials.Select(m => $"{m.MaterialName} ({m.ConsumedQuantity} {m.Unit})").Distinct().ToList(),
            TotalConsumed = p.TotalInputQuantity,
            TotalManufactured = p.TotalOutputQuantity,
            TotalWaste = p.TotalWasteQuantity,
            WastePercentage = p.WastePercentage
        }).ToList();

        return Ok(ApiResponse<List<ProductionTraceabilityResultDto>>.Ok(list));
    }

    public static ProductionProcessDetailDto MapProcessToDto(ProductionProcess p)
    {
        return new ProductionProcessDetailDto
        {
            Id = p.Id,
            ProcessNumber = p.ProcessNumber,
            ProductionDate = p.ProductionDate,
            StorageLocationId = p.StorageLocationId,
            StorageLocationName = p.StorageLocationName ?? p.StorageLocation?.Name,
            TotalInputQuantity = p.TotalInputQuantity,
            TotalOutputQuantity = p.TotalOutputQuantity,
            TotalWasteQuantity = p.TotalWasteQuantity,
            WastePercentage = p.WastePercentage,
            Unit = p.Unit,
            Status = p.Status,
            ResponsibleEmployee = p.ResponsibleEmployee,
            Notes = p.Notes,
            IncomingWeighbridgePath = p.IncomingWeighbridgePath,
            IncomingWeighbridgeFileName = p.IncomingWeighbridgeFileName,
            IncomingWeighbridgeFileType = p.IncomingWeighbridgeFileType,
            IncomingWeighbridgeFileSize = p.IncomingWeighbridgeFileSize,
            CreatedAt = p.CreatedAt,
            CreatedBy = p.CreatedBy,
            Materials = p.Materials.Select(m => new ProductionProcessMaterialDto
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
            FinishedProducts = p.FinishedProducts.Select(f => new ProductionFinishedProductDto
            {
                Id = f.Id,
                ProductionProcessId = f.ProductionProcessId,
                ProcessNumber = p.ProcessNumber,
                ProductionOrderId = f.ProductionOrderId ?? p.ProductionOrderId,
                ProductionOrderNumber = f.ProductionOrderNumber ?? p.ProductionOrderNumber,
                FinishedProductName = f.FinishedProductName,
                ManufactureNumber = f.ManufactureNumber,
                ManufacturedQuantity = f.ManufacturedQuantity,
                Unit = f.Unit,
                DestinationStorageLocationId = f.DestinationStorageLocationId,
                DestinationStorageLocationName = f.DestinationStorageLocationName,
                ProductionDate = f.ProductionDate,
                Notes = f.Notes,
                WeighbridgeAttachmentPath = f.WeighbridgeAttachmentPath,
                WeighbridgeFileName = f.WeighbridgeFileName,
                WeighbridgeFileType = f.WeighbridgeFileType,
                WeighbridgeFileSize = f.WeighbridgeFileSize,
                CreatedAt = f.CreatedAt,
                CreatedBy = f.CreatedBy
            }).ToList()
        };
    }

    /// <summary>
    /// Return unconsumed or excess raw materials from Production back to Warehouse inventory.
    /// Strictly verifies the returned quantity does not exceed the unconsumed transferred quantity,
    /// re-adds to WarehouseBatch.CurrentQuantity, sets batch status to APPROVED,
    /// posts positive entry in StorageInventoryLedger (MovementType = "RETURN"),
    /// decrements ProductionOrderItem.IssuedQuantity, and broadcasts SignalR event.
    /// </summary>
    [HttpPost("orders/{id:int}/return-to-warehouse")]
    [HttpPost("return-to-warehouse")]
    [ProducesResponseType(typeof(ApiResponse<ProductionReturnResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReturnMaterialsToWarehouse(int? id, [FromBody] ProductionReturnRequestDto dto)
    {
        var (isAuth, role, userName, userId) = GetAuthenticatedUser();
        if (!isAuth)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بإرجاع المواد إلى المستودع."));
        }

        int effectiveOrderId = (id.HasValue && id.Value > 0) ? id.Value : dto.ProductionOrderId;
        if (effectiveOrderId <= 0)
        {
            return BadRequest(ApiResponse.Fail("معرف أمر الإنتاج مطلوب لعملية الإرجاع."));
        }

        if (dto.Items == null || dto.Items.Count == 0)
        {
            return BadRequest(ApiResponse.Fail("يجب تحديد دفعة واحدة على الأقل لإرجاعها إلى المستودع."));
        }

        var order = await _db.ProductionOrders
            .Include(o => o.Items)
            .Include(o => o.Transfers)
            .FirstOrDefaultAsync(o => o.Id == effectiveOrderId);

        if (order == null)
        {
            return NotFound(ApiResponse.Fail($"أمر الإنتاج #{effectiveOrderId} غير موجود."));
        }

        var batchIds = dto.Items.Select(i => i.WarehouseBatchId).Distinct().ToList();
        var batches = await _db.WarehouseBatches
            .Include(b => b.StorageLocation)
            .Where(b => batchIds.Contains(b.Id))
            .ToListAsync();

        if (batches.Count != batchIds.Count)
        {
            return BadRequest(ApiResponse.Fail("إحدى دفعات المستودع المحددة غير موجودة في قاعدة البيانات."));
        }

        // Validate quantities
        foreach (var item in dto.Items)
        {
            if (item.ReturnQuantity <= 0)
            {
                return BadRequest(ApiResponse.Fail("كمية الإرجاع يجب أن تكون أكبر من الصفر."));
            }

            var orderItem = order.Items.FirstOrDefault(oi =>
                (item.ProductionOrderItemId.HasValue && oi.Id == item.ProductionOrderItemId.Value) ||
                oi.WarehouseBatchId == item.WarehouseBatchId);

            if (orderItem == null)
            {
                return BadRequest(ApiResponse.Fail($"الدفعة رقم {item.WarehouseBatchId} غير مدرجة في أمر الإنتاج #{order.OrderNumber}."));
            }

            if (item.ReturnQuantity > orderItem.IssuedQuantity)
            {
                return BadRequest(ApiResponse.Fail($"الكمية المراد إرجاعها ({item.ReturnQuantity:N3} طن) تتجاوز الكمية المنقولة فعلياً لأمر الإنتاج ({orderItem.IssuedQuantity:N3} طن)."));
            }
        }

        using var dbTx = await _db.Database.BeginTransactionAsync();
        try
        {
            var txDate = DateTime.UtcNow;
            var seq = await _db.StorageSequences.FirstOrDefaultAsync(s => s.SequenceType == "IMPORT" || s.SequenceType == "OPERATION");
            var opNum = seq != null ? ++seq.NextValue : 1;

            var op = new StorageOperation
            {
                OperationType = "RETURN",
                TransactionNumber = opNum,
                TransactionDate = txDate,
                StorageLocationId = order.StorageLocationId,
                StorageLocationName = order.StorageLocationName,
                MaterialType = "مرتجع من الإنتاج",
                Source = "قسم الإنتاج",
                DestinationDepartment = "المستودع",
                ResponsibleEmployee = userName,
                ResponsibleUserId = userId,
                Notes = $"إرجاع مواد غير مستهلكة من أمر الإنتاج #{order.OrderNumber}. {dto.Notes}",
                ApprovalStatus = "APPROVED",
                IsInventoryPosted = true,
                InventoryPostedAt = txDate,
                ReviewedBy = userName,
                ReviewedByUserId = userId,
                ReviewedAt = txDate,
                CreatedAt = txDate,
                CreatedBy = userName,
                CreatedByUserId = userId
            };

            await _db.StorageOperations.AddAsync(op);
            await _db.SaveChangesAsync();

            var returnedBatchesList = new List<string>();
            decimal totalReturned = 0m;

            foreach (var item in dto.Items)
            {
                var batch = batches.First(b => b.Id == item.WarehouseBatchId);
                var orderItem = order.Items.First(oi =>
                    (item.ProductionOrderItemId.HasValue && oi.Id == item.ProductionOrderItemId.Value) ||
                    oi.WarehouseBatchId == item.WarehouseBatchId);

                // 1. Re-add to WarehouseBatch
                batch.CurrentQuantity += item.ReturnQuantity;
                if (batch.Status == "DEPLETED" || batch.Status == "CONSUMED")
                {
                    batch.Status = "APPROVED";
                    batch.IsActive = true;
                }

                // 2. Decrement from ProductionOrderItem issued quantity
                orderItem.IssuedQuantity = Math.Max(0m, orderItem.IssuedQuantity - item.ReturnQuantity);

                // 3. Post positive movement to StorageInventoryLedger
                var currentStock = await _db.StorageInventoryLedger
                    .Where(l => l.StorageLocationId == batch.StorageLocationId && l.RawMaterialId == batch.RawMaterialId)
                    .SumAsync(l => (decimal?)l.Quantity) ?? 0m;

                var ledgerEntry = new StorageInventoryLedger
                {
                    StorageLocationId = batch.StorageLocationId,
                    RawMaterialId = batch.RawMaterialId,
                    WarehouseBatchId = batch.Id,
                    BatchNumber = batch.BatchNumber,
                    StorageOperationId = op.Id,
                    MovementType = "RETURN",
                    Quantity = item.ReturnQuantity, // Positive: increases warehouse stock
                    RunningBalance = currentStock + item.ReturnQuantity,
                    MovementDate = txDate,
                    CreatedAt = txDate,
                    CreatedBy = userName
                };
                await _db.StorageInventoryLedger.AddAsync(ledgerEntry);

                // 4. Create operation line item
                var opItem = new StorageOperationItem
                {
                    StorageOperationId = op.Id,
                    RawMaterialId = batch.RawMaterialId,
                    WarehouseBatchId = batch.Id,
                    BatchNumber = batch.BatchNumber,
                    SampleCode = batch.SampleCode,
                    ProductionReferenceNumber = batch.ProductionReferenceNumber,
                    MaterialName = batch.MaterialName,
                    Quantity = item.ReturnQuantity,
                    TrackingNumber = $"RET-{txDate:yyyy}-{opNum:D4}-B{batch.Id}",
                    Notes = item.Notes
                };
                await _db.StorageOperationItems.AddAsync(opItem);

                returnedBatchesList.Add(batch.BatchNumber);
                totalReturned += item.ReturnQuantity;
            }

            // Recalculate order total transferred
            order.TotalTransferredQuantity = order.Items.Sum(i => i.IssuedQuantity);
            if (order.TotalTransferredQuantity == 0 && order.Status == "TRANSFERRED_TO_PRODUCTION")
            {
                order.Status = "PENDING_STORAGE_APPROVAL";
                order.CurrentStage = "تم إرجاع كافة المواد للمستودع";
            }

            await _db.SaveChangesAsync();
            await dbTx.CommitAsync();

            _logger.LogInformation("Returned {Total} tons across {Batches} batches from Order {Order} back to Warehouse.",
                totalReturned, returnedBatchesList.Count, order.OrderNumber);

            // SignalR broadcast to Laboratory, Storage, and Production
            await _inventoryService.BroadcastInventoryChangedAsync("RETURN", new
            {
                Action = "RETURN_TO_WAREHOUSE",
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                TotalReturned = totalReturned,
                Batches = returnedBatchesList,
                Timestamp = txDate
            });

            try
            {
                await _hubContext.Clients.All.SendAsync("OnProductionOrderChanged", new
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    Status = order.Status,
                    Action = "RETURN_TO_WAREHOUSE",
                    Timestamp = txDate
                });
            }
            catch (Exception exSignal)
            {
                _logger.LogWarning(exSignal, "Failed to broadcast OnProductionOrderChanged for return.");
            }

            var result = new ProductionReturnResultDto
            {
                StorageOperationId = op.Id,
                TransactionNumber = $"#{opNum}",
                ProductionOrderId = order.Id,
                ProductionOrderNumber = order.OrderNumber,
                TotalReturnedQuantity = totalReturned,
                ReturnDate = txDate,
                ReturnedBatches = returnedBatchesList
            };

            return Ok(ApiResponse<ProductionReturnResultDto>.Ok(result, $"تم إرجاع {totalReturned:N3} طن إلى المستودع بنجاح وتحديث أرصدة المخزون المتاحة."));
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync();
            _logger.LogError(ex, "Failed to return materials from Production Order {Id} to Warehouse.", effectiveOrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء معالجة إرجاع المواد إلى المستودع."));
        }
    }

    #endregion
}
