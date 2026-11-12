using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FaceStone.Web.Data;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "FinanceAccess")]
[Produces("application/json")]
public class FinanceController : ControllerBase
{
    private readonly FaceStoneDbContext _context;
    private readonly ILogger<FinanceController> _logger;
    public FinanceController(
        FaceStoneDbContext context, 
        ILogger<FinanceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves Finance Statistics connecting Purchase Orders, Transportation, and Storage.
    /// Primary Filter: Date (defaults to server Today). Major Filter: PO.
    /// Distinguishes Daily Activity from Cumulative PO Status.
    /// Authorized only to: Accountants, IT, and CEO.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<FinanceStatisticsResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] DateTime? date,
        [FromQuery] string? po)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            var isFinanceDirector = AppRoles.IsFinanceDirector(userRole);

            // Date Range logic: defaults to today if not provided
            var dFrom = (fromDate ?? date ?? DateTime.Today).Date;
            var dTo = (toDate ?? date ?? dFrom).Date;
            if (dFrom > dTo)
            {
                var temp = dFrom;
                dFrom = dTo;
                dTo = temp;
            }
            var startRange = dFrom;
            var endRange = dTo.AddDays(1); // inclusive of entire toDate day

            var selectedPoTrimmed = string.IsNullOrWhiteSpace(po) || po.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : po.Trim();

            _logger.LogInformation("Finance statistics requested by '{Role}' for Range: {From:yyyy-MM-dd} to {To:yyyy-MM-dd}, PO: '{PO}'",
                userRole, dFrom, dTo, selectedPoTrimmed ?? "ALL");

            // 1. Authoritative Purchase Orders from database
            var allPos = await _context.PurchaseOrders
                .AsNoTracking()
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            // 2. Authoritative Payments (for paid & outstanding calculations)
            var allPayments = await _context.FinancePayments
                .AsNoTracking()
                .ToListAsync();

            // 3. Pre-fetch Transportation aggregations per PO (server-side group by across all time)
            var transStatsByPo = await _context.Transportations
                .AsNoTracking()
                .Where(t => t.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(t.ApprovalStatus))
                .GroupBy(t => t.PONumber)
                .Select(g => new
                {
                    PONumber = g.Key,
                    Count = g.Count(),
                    TotalNetWeight = g.Sum(t => t.NetWeight),
                    TotalWaste = g.Sum(t => t.WasteQuantity)
                })
                .ToDictionaryAsync(x => x.PONumber, StringComparer.OrdinalIgnoreCase);

            // 4. Pre-fetch Storage PO-related waste (server-side query across all time)
            var storageWasteByPoList = await _context.StorageTransactions
                .AsNoTracking()
                .Where(s => (s.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(s.ApprovalStatus)) && s.IsExportRelatedToPO && s.PONumber != null && s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue)
                .Select(s => new
                {
                    PONumber = s.PONumber!,
                    Waste = Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value)
                })
                .ToListAsync();

            var storageWasteByPo = storageWasteByPoList
                .GroupBy(s => s.PONumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Waste), StringComparer.OrdinalIgnoreCase);

            // Build available POs list for dropdown
            var availablePos = allPos.Select(p => new FinancePoDropdownItemDto
            {
                PoNumber = p.PONumber,
                Supplier = p.Supplier,
                Destination = p.Destination,
                TonPrice = p.TonPrice > 0 ? p.TonPrice : p.PricePerTon,
                Quantity = p.Quantity,
                RemainingQuantity = p.RemainingQuantity,
                OrderDate = p.OrderDate,
                Status = p.RemainingQuantity <= 0 ? "مكتمل" : (transStatsByPo.ContainsKey(p.PONumber) ? "قيد التوريد" : "معلق"),
                PaymentStatus = !string.IsNullOrWhiteSpace(p.PaymentStatus) ? p.PaymentStatus : "غير مدفوع"
            }).ToList();

            // 5. Build cumulative summaries for each PO
            var allPosSummary = new List<FinancePoSummaryDto>();
            foreach (var p in allPos)
            {
                var tonPrice = p.TonPrice > 0 ? p.TonPrice : (p.PricePerTon > 0 ? p.PricePerTon : 0m);
                var expectedQty = p.Quantity;
                var expectedPrice = Math.Round(expectedQty * tonPrice, 2);

                transStatsByPo.TryGetValue(p.PONumber, out var tStats);
                var transCount = tStats?.Count ?? 0;
                var transNet = tStats?.TotalNetWeight ?? 0m;
                var transWaste = Math.Round(tStats?.TotalWaste ?? 0m, 3);
                var transWastePrice = Math.Round(transWaste * tonPrice, 2);

                storageWasteByPo.TryGetValue(p.PONumber, out var sWaste);
                var storageWaste = Math.Round(sWaste, 3);
                var storageWastePrice = Math.Round(storageWaste * tonPrice, 2);

                var totalWaste = Math.Round(transWaste + storageWaste, 3);
                var totalWastePrice = Math.Round(totalWaste * tonPrice, 2);

                var deliveredQty = Math.Round(Math.Max(0m, expectedQty - totalWaste), 3);
                var deliveredPrice = Math.Round(deliveredQty * tonPrice, 2);

                var remainingQty = Math.Round(p.RemainingQuantity, 3);
                var remainingPrice = Math.Round(remainingQty * tonPrice, 2);

                var perfPct = expectedQty > 0
                    ? Math.Round((deliveredQty / expectedQty) * 100m, 2)
                    : 100m;
                var perfPricePct = expectedPrice > 0
                    ? Math.Round((deliveredPrice / expectedPrice) * 100m, 2)
                    : 100m;

                var poPaymentsSum = allPayments
                    .Where(pay => pay.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase))
                    .Sum(pay => pay.Amount);
                var outstanding = Math.Round(Math.Max(0m, deliveredPrice - poPaymentsSum), 2);

                var poStatus = remainingQty <= 0 ? "مكتمل" : (transCount > 0 ? "قيد التوريد" : "معلق");
                var paymentStatus = !string.IsNullOrWhiteSpace(p.PaymentStatus) ? p.PaymentStatus : "غير مدفوع";

                allPosSummary.Add(new FinancePoSummaryDto
                {
                    PoNumber = p.PONumber,
                    Supplier = p.Supplier,
                    Destination = p.Destination,
                    TonPrice = tonPrice,
                    OrderDate = p.OrderDate,
                    ExpectedQuantity = expectedQty,
                    ExpectedPrice = expectedPrice,
                    DeliveredQuantity = deliveredQty,
                    DeliveredPrice = deliveredPrice,
                    RemainingQuantity = remainingQty,
                    RemainingPrice = remainingPrice,
                    TransportationTransactionsCount = transCount,
                    TransportationWasteQuantity = transWaste,
                    TransportationWastePrice = transWastePrice,
                    StorageWasteQuantity = storageWaste,
                    StorageWastePrice = storageWastePrice,
                    TotalWasteQuantity = totalWaste,
                    TotalWastePrice = totalWastePrice,
                    ActualQuantity = deliveredQty,
                    ActualPrice = deliveredPrice,
                    PerformancePercentage = perfPct,
                    PerformancePricePercentage = perfPricePct,
                    TotalSuppliedNetWeight = transNet,
                    PaidAmount = Math.Round(poPaymentsSum, 2),
                    OutstandingAmount = outstanding,
                    Status = poStatus,
                    PaymentStatus = paymentStatus,
                    PaymentStatusUpdatedAt = p.PaymentStatusUpdatedAt,
                    PaymentStatusUpdatedBy = p.PaymentStatusUpdatedBy
                });
            }

            // 6. Selected PO Summary vs Overall Cumulative Summary
            FinancePoSummaryDto activePoSummary;
            FactoryPerformanceDto activePerformance;

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                var targetPo = allPosSummary.FirstOrDefault(x => x.PoNumber.Equals(selectedPoTrimmed, StringComparison.OrdinalIgnoreCase));
                if (targetPo == null)
                {
                    targetPo = new FinancePoSummaryDto
                    {
                        Scope = "SinglePo",
                        PoNumber = selectedPoTrimmed,
                        Supplier = "غير محدد",
                        Destination = "-",
                        TonPrice = 0m,
                        PerformancePercentage = 0m,
                        Status = "معلق",
                        PaymentStatus = "غير مدفوع"
                    };
                }
                targetPo.Scope = "SinglePo";
                activePoSummary = targetPo;

                var status = targetPo.PerformancePercentage >= 95m ? "High" : (targetPo.PerformancePercentage >= 80m ? "Medium" : "Low");
                var statusArabic = status == "High" ? "أداء ممتاز" : (status == "Medium" ? "أداء مستقر" : "تنبيه هالك");

                activePerformance = new FactoryPerformanceDto
                {
                    ExpectedQuantity = targetPo.ExpectedQuantity,
                    ExpectedPrice = targetPo.ExpectedPrice,
                    ActualQuantity = targetPo.DeliveredQuantity,
                    ActualPrice = targetPo.DeliveredPrice,
                    WasteQuantity = targetPo.TotalWasteQuantity,
                    WastePrice = targetPo.TotalWastePrice,
                    RemainingQuantity = targetPo.RemainingQuantity,
                    RemainingPrice = targetPo.RemainingPrice,
                    PerformancePercentage = targetPo.PerformancePercentage,
                    PerformancePricePercentage = targetPo.PerformancePricePercentage,
                    Status = status,
                    StatusLabelArabic = statusArabic
                };
            }
            else
            {
                var sumExpectedQty = allPosSummary.Sum(p => p.ExpectedQuantity);
                var sumExpectedPrice = allPosSummary.Sum(p => p.ExpectedPrice);
                var sumDeliveredQty = allPosSummary.Sum(p => p.DeliveredQuantity);
                var sumDeliveredPrice = allPosSummary.Sum(p => p.DeliveredPrice);
                var sumTotalWaste = allPosSummary.Sum(p => p.TotalWasteQuantity);
                var sumTotalWastePrice = allPosSummary.Sum(p => p.TotalWastePrice);
                var sumRemainingQty = allPosSummary.Sum(p => p.RemainingQuantity);
                var sumRemainingPrice = allPosSummary.Sum(p => p.RemainingPrice);
                var sumTransCount = allPosSummary.Sum(p => p.TransportationTransactionsCount);
                var sumTransWaste = allPosSummary.Sum(p => p.TransportationWasteQuantity);
                var sumTransWastePrice = allPosSummary.Sum(p => p.TransportationWastePrice);
                var sumStorageWaste = allPosSummary.Sum(p => p.StorageWasteQuantity);
                var sumStorageWastePrice = allPosSummary.Sum(p => p.StorageWastePrice);
                var sumPaid = allPosSummary.Sum(p => p.PaidAmount);
                var sumOutstanding = allPosSummary.Sum(p => p.OutstandingAmount);

                var overallPerf = sumExpectedQty > 0
                    ? Math.Round((sumDeliveredQty / sumExpectedQty) * 100m, 2)
                    : 100m;
                var overallPricePerf = sumExpectedPrice > 0
                    ? Math.Round((sumDeliveredPrice / sumExpectedPrice) * 100m, 2)
                    : 100m;

                activePoSummary = new FinancePoSummaryDto
                {
                    Scope = "AllPos",
                    PoNumber = "جميع أوامر الشراء",
                    Supplier = "كافة الموردين",
                    Destination = "جميع المحطات والمصانع",
                    TonPrice = allPosSummary.Count > 0 ? Math.Round(allPosSummary.Average(p => p.TonPrice), 2) : 0m,
                    OrderDate = allPosSummary.Count > 0 ? allPosSummary.Min(p => p.OrderDate) : dFrom,
                    ExpectedQuantity = sumExpectedQty,
                    ExpectedPrice = sumExpectedPrice,
                    DeliveredQuantity = sumDeliveredQty,
                    DeliveredPrice = sumDeliveredPrice,
                    RemainingQuantity = sumRemainingQty,
                    RemainingPrice = sumRemainingPrice,
                    TransportationTransactionsCount = sumTransCount,
                    TransportationWasteQuantity = sumTransWaste,
                    TransportationWastePrice = sumTransWastePrice,
                    StorageWasteQuantity = sumStorageWaste,
                    StorageWastePrice = sumStorageWastePrice,
                    TotalWasteQuantity = sumTotalWaste,
                    TotalWastePrice = sumTotalWastePrice,
                    ActualQuantity = sumDeliveredQty,
                    ActualPrice = sumDeliveredPrice,
                    PerformancePercentage = overallPerf,
                    PerformancePricePercentage = overallPricePerf,
                    PaidAmount = sumPaid,
                    OutstandingAmount = sumOutstanding,
                    Status = "كافة الأوامر",
                    PaymentStatus = "متنوع"
                };

                var status = overallPerf >= 95m ? "High" : (overallPerf >= 80m ? "Medium" : "Low");
                var statusArabic = status == "High" ? "أداء ممتاز" : (status == "Medium" ? "أداء مستقر" : "تنبيه هالك");

                activePerformance = new FactoryPerformanceDto
                {
                    ExpectedQuantity = sumExpectedQty,
                    ExpectedPrice = sumExpectedPrice,
                    ActualQuantity = sumDeliveredQty,
                    ActualPrice = sumDeliveredPrice,
                    WasteQuantity = sumTotalWaste,
                    WastePrice = sumTotalWastePrice,
                    RemainingQuantity = sumRemainingQty,
                    RemainingPrice = sumRemainingPrice,
                    PerformancePercentage = overallPerf,
                    PerformancePricePercentage = overallPricePerf,
                    Status = status,
                    StatusLabelArabic = statusArabic
                };
            }

            // 7. RANGE / DAILY ACTIVITY CALCULATION (FOR SELECTED DATE RANGE)
            var rangeTransQuery = _context.Transportations
                .AsNoTracking()
                .Where(t => t.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(t.ApprovalStatus))
                .Where(t => t.TransactionDate >= startRange && t.TransactionDate < endRange);

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeTransQuery = rangeTransQuery.Where(t => t.PONumber == selectedPoTrimmed);
            }

            var rangeTransList = await rangeTransQuery
                .Select(t => new { t.PONumber, t.NetWeight, t.WasteQuantity })
                .ToListAsync();

            var rangeTransCount = rangeTransList.Count;
            var rangeTransNetWeight = Math.Round(rangeTransList.Sum(t => t.NetWeight), 3);
            var rangeTransWaste = Math.Round(rangeTransList.Sum(t => t.WasteQuantity), 3);

            decimal rangeTransWastePrice = 0m;
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeTransWastePrice = Math.Round(rangeTransWaste * activePoSummary.TonPrice, 2);
            }
            else
            {
                var poPriceMap = allPosSummary.ToDictionary(p => p.PoNumber, p => p.TonPrice, StringComparer.OrdinalIgnoreCase);
                rangeTransWastePrice = Math.Round(rangeTransList.Sum(t =>
                {
                    poPriceMap.TryGetValue(t.PONumber, out var price);
                    return t.WasteQuantity * price;
                }), 2);
            }

            // 7.2 Storage Imports (Independent, non-PO unless recorded otherwise)
            var rangeStorageImports = await _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(s.ApprovalStatus))
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.Action == "Import" || s.ServiceType == "إيداع")
                .SumAsync(s => (decimal?)s.IncomingQuantity) ?? 0m;
            rangeStorageImports = Math.Round(rangeStorageImports, 3);

            // 7.3 Storage Exports
            var rangeStorageExportsQuery = _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(s.ApprovalStatus))
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.Action == "Export" || s.ServiceType == "صرف");

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageExportsQuery = rangeStorageExportsQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }

            var rangeStorageExports = await rangeStorageExportsQuery.SumAsync(s => (decimal?)s.OutgoingQuantity) ?? 0m;
            rangeStorageExports = Math.Round(rangeStorageExports, 3);

            // 7.4 Storage Waste
            var rangeStorageWasteQuery = _context.StorageTransactions
                .AsNoTracking()
                .Where(s => s.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(s.ApprovalStatus))
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue);

            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageWasteQuery = rangeStorageWasteQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }

            var rangeStorageWasteRows = await rangeStorageWasteQuery
                .Select(s => new
                {
                    s.PONumber,
                    Waste = Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value)
                })
                .ToListAsync();

            var rangeStorageWaste = Math.Round(rangeStorageWasteRows.Sum(s => s.Waste), 3);
            decimal rangeStorageWastePrice = 0m;
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageWastePrice = Math.Round(rangeStorageWaste * activePoSummary.TonPrice, 2);
            }
            else
            {
                var poPriceMap = allPosSummary.ToDictionary(p => p.PoNumber, p => p.TonPrice, StringComparer.OrdinalIgnoreCase);
                rangeStorageWastePrice = Math.Round(rangeStorageWasteRows.Sum(s =>
                {
                    var price = 0m;
                    if (!string.IsNullOrWhiteSpace(s.PONumber))
                    {
                        poPriceMap.TryGetValue(s.PONumber, out price);
                    }
                    return s.Waste * price;
                }), 2);
            }

            var totalRangeWaste = Math.Round(rangeTransWaste + rangeStorageWaste, 3);
            var totalRangeWastePrice = Math.Round(rangeTransWastePrice + rangeStorageWastePrice, 2);

            var dailyActivity = new FinanceDailyActivityDto
            {
                FromDate = dFrom,
                ToDate = dTo,
                SelectedDate = dFrom,
                TransportationTransactionsCount = rangeTransCount,
                TransportationNetWeight = rangeTransNetWeight,
                TransportationWasteQuantity = rangeTransWaste,
                TransportationWastePrice = rangeTransWastePrice,
                StorageImportedQuantity = rangeStorageImports,
                StorageImportedPrice = null,
                StorageImportedPriceNote = "لا يوجد مصدر تسعير للواردات المخزنية في قاعدة البيانات",
                StorageExportedQuantity = rangeStorageExports,
                StorageWasteQuantity = rangeStorageWaste,
                StorageWastePrice = rangeStorageWastePrice,
                TotalDailyWasteQuantity = totalRangeWaste,
                TotalDailyWastePrice = totalRangeWastePrice
            };

            var response = new FinanceStatisticsResponseDto
            {
                FromDate = dFrom,
                ToDate = dTo,
                SelectedDate = dFrom,
                SelectedPo = selectedPoTrimmed,
                IsCurrentUserFinanceDirector = isFinanceDirector,
                DailyActivity = dailyActivity,
                PoSummary = activePoSummary,
                Performance = activePerformance,
                AvailablePos = availablePos,
                AllPosSummary = allPosSummary
            };

            return Ok(ApiResponse<FinanceStatisticsResponseDto>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing Finance statistics.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء احتساب الإحصائيات المالية."));
        }
    }

    /// <summary>
    /// Retrieves historical list of all Purchase Orders with real database metrics.
    /// Supports both /api/finance/pos and /api/finance/purchase-orders.
    /// </summary>
    [HttpGet("pos")]
    [HttpGet("purchase-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<FinancePoSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrders()
    {
        try
        {
            var pos = await _context.PurchaseOrders
                .AsNoTracking()
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            var allPayments = await _context.FinancePayments
                .AsNoTracking()
                .ToListAsync();

            var transStatsByPo = await _context.Transportations
                .AsNoTracking()
                .Where(t => t.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(t.ApprovalStatus))
                .GroupBy(t => t.PONumber)
                .Select(g => new
                {
                    PONumber = g.Key,
                    Count = g.Count(),
                    TotalNetWeight = g.Sum(t => t.NetWeight),
                    TotalWaste = g.Sum(t => t.WasteQuantity)
                })
                .ToDictionaryAsync(x => x.PONumber, StringComparer.OrdinalIgnoreCase);

            var storageWasteByPoList = await _context.StorageTransactions
                .AsNoTracking()
                .Where(s => (s.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(s.ApprovalStatus)) && s.IsExportRelatedToPO && s.PONumber != null && s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue)
                .Select(s => new
                {
                    PONumber = s.PONumber!,
                    Waste = Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value)
                })
                .ToListAsync();

            var storageWasteByPo = storageWasteByPoList
                .GroupBy(s => s.PONumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Waste), StringComparer.OrdinalIgnoreCase);

            // Delivered quantities directly from finalized (ISSUED) invoices
            var issuedInvoices1 = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.Status == "ISSUED" && i.PurchaseOrderId.HasValue)
                .Select(i => new {
                    PoId = i.PurchaseOrderId!.Value,
                    InvoicedQty = i.InvoicedQuantity ?? (i.Items.Any() ? i.Items.Sum(it => it.Quantity) : 0m)
                })
                .ToListAsync();

            var poInvoicedMap = issuedInvoices1
                .GroupBy(x => x.PoId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.InvoicedQty));

            var results = new List<FinancePoSummaryDto>();
            foreach (var p in pos)
            {
                var tonPrice = p.TonPrice > 0 ? p.TonPrice : (p.PricePerTon > 0 ? p.PricePerTon : 0m);
                var expectedQty = p.Quantity;
                var expectedPrice = Math.Round(expectedQty * tonPrice, 2);

                transStatsByPo.TryGetValue(p.PONumber, out var tStats);
                var transCount = tStats?.Count ?? 0;
                var transNet = tStats?.TotalNetWeight ?? 0m;
                var transWaste = Math.Round(tStats?.TotalWaste ?? 0m, 3);
                var transWastePrice = Math.Round(transWaste * tonPrice, 2);

                storageWasteByPo.TryGetValue(p.PONumber, out var sWaste);
                var storageWaste = Math.Round(sWaste, 3);
                var storageWastePrice = Math.Round(storageWaste * tonPrice, 2);

                var totalWaste = Math.Round(transWaste + storageWaste, 3);
                var totalWastePrice = Math.Round(totalWaste * tonPrice, 2);

                // Delivered quantity strictly calculated from finalized/approved invoices
                var deliveredQty = poInvoicedMap.TryGetValue(p.Id, out var invQty) ? invQty : p.DeliveredQuantity;
                var deliveredPrice = Math.Round(deliveredQty * tonPrice, 2);

                var remainingQty = Math.Round(Math.Max(0m, expectedQty - deliveredQty), 3);
                var remainingPrice = Math.Round(remainingQty * tonPrice, 2);

                var perfPct = expectedQty > 0
                    ? Math.Round((deliveredQty / expectedQty) * 100m, 2)
                    : 100m;
                var perfPricePct = expectedPrice > 0
                    ? Math.Round((deliveredPrice / expectedPrice) * 100m, 2)
                    : 100m;

                var poPaymentsSum = allPayments
                    .Where(pay => pay.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase))
                    .Sum(pay => pay.Amount);
                var outstanding = Math.Round(Math.Max(0m, deliveredPrice - poPaymentsSum), 2);

                var poStatus = remainingQty <= 0 ? "مكتمل" : (deliveredQty > 0 ? "قيد التوريد" : "معلق");
                var paymentStatus = !string.IsNullOrWhiteSpace(p.PaymentStatus) ? p.PaymentStatus : "غير مدفوع";

                results.Add(new FinancePoSummaryDto
                {
                    Id = p.Id,
                    PoNumber = p.PONumber,
                    SupplierId = p.SupplierId,
                    Supplier = p.Supplier,
                    MaterialName = !string.IsNullOrWhiteSpace(p.MaterialName) ? p.MaterialName : "مواد خرسانية / ركام",
                    Unit = !string.IsNullOrWhiteSpace(p.Unit) ? p.Unit : "طن",
                    Destination = p.Destination,
                    TonPrice = tonPrice,
                    OrderDate = p.OrderDate,
                    ExpectedDeliveryDate = p.ExpectedDeliveryDate,
                    ExpectedQuantity = expectedQty,
                    ExpectedPrice = expectedPrice,
                    DeliveredQuantity = deliveredQty,
                    DeliveredPrice = deliveredPrice,
                    RemainingQuantity = remainingQty,
                    RemainingPrice = remainingPrice,
                    Notes = p.Notes,
                    TransportationTransactionsCount = transCount,
                    TransportationWasteQuantity = transWaste,
                    TransportationWastePrice = transWastePrice,
                    StorageWasteQuantity = storageWaste,
                    StorageWastePrice = storageWastePrice,
                    TotalWasteQuantity = totalWaste,
                    TotalWastePrice = totalWastePrice,
                    ActualQuantity = deliveredQty,
                    ActualPrice = deliveredPrice,
                    PerformancePercentage = perfPct,
                    PerformancePricePercentage = perfPricePct,
                    TotalSuppliedNetWeight = transNet,
                    PaidAmount = Math.Round(poPaymentsSum, 2),
                    OutstandingAmount = outstanding,
                    Status = poStatus,
                    PaymentStatus = paymentStatus,
                    PaymentStatusUpdatedAt = p.PaymentStatusUpdatedAt,
                    PaymentStatusUpdatedBy = p.PaymentStatusUpdatedBy
                });
            }

            return Ok(ApiResponse<List<FinancePoSummaryDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving purchase orders list.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء استرجاع قائمة أوامر الشراء."));
        }
    }

    /// <summary>
    /// Retrieves comprehensive details for a specific Purchase Order including transportation history,
    /// storage history, waste analysis, chronological timeline, and payment status details.
    /// </summary>
    [HttpGet("purchase-orders/{poNumber}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPurchaseOrderDetails(string poNumber)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب."));
        }

        var poNum = poNumber.Trim();
        PurchaseOrder? po = null;
        if (int.TryParse(poNum, out var parsedId))
        {
            po = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(p => p.Payments)
                .FirstOrDefaultAsync(p => p.Id == parsedId || p.PONumber == poNum);
        }
        else
        {
            po = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(p => p.Payments)
                .FirstOrDefaultAsync(p => p.PONumber == poNum);
        }

        if (po == null)
        {
            return NotFound(ApiResponse.Fail($"لم يتم العثور على أمر الشراء '{poNum}'."));
        }

        poNum = po.PONumber;

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        var isFinanceDirector = AppRoles.IsFinanceDirector(userRole);

        // Fetch Transportation transactions
        var transRecords = await _context.Transportations
            .AsNoTracking()
            .Where(t => t.PONumber == poNum && (t.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(t.ApprovalStatus)))
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        // Fetch Storage transactions
        var storageRecords = await _context.StorageTransactions
            .AsNoTracking()
            .Where(s => s.PONumber == poNum && (s.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(s.ApprovalStatus)))
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();

        // Fetch Payment audits
        var paymentAudits = await _context.PurchaseOrderPaymentAudits
            .AsNoTracking()
            .Where(a => a.PONumber == poNum)
            .OrderByDescending(a => a.ChangedAt)
            .Select(a => new PaymentStatusAuditDto
            {
                Id = a.Id,
                PoNumber = a.PONumber,
                PreviousStatus = a.PreviousStatus,
                NewStatus = a.NewStatus,
                ChangedBy = a.ChangedBy,
                ChangedAt = a.ChangedAt,
                Notes = a.Notes
            })
            .ToListAsync();

        var tonPrice = po.TonPrice > 0 ? po.TonPrice : (po.PricePerTon > 0 ? po.PricePerTon : 0m);
        var expectedQty = po.Quantity;
        var expectedPrice = Math.Round(expectedQty * tonPrice, 2);

        var transCount = transRecords.Count;
        var transNet = Math.Round(transRecords.Sum(t => t.NetWeight), 3);
        var transWaste = Math.Round(transRecords.Sum(t => t.WasteQuantity), 3);
        var transWastePrice = Math.Round(transWaste * tonPrice, 2);

        var storageWaste = Math.Round(storageRecords
            .Where(s => s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue)
            .Sum(s => Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value)), 3);
        var storageWastePrice = Math.Round(storageWaste * tonPrice, 2);

        var totalWaste = Math.Round(transWaste + storageWaste, 3);
        var totalWastePrice = Math.Round(totalWaste * tonPrice, 2);

        var deliveredInvoicesList = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.PurchaseOrderId == po.Id && i.Status == "ISSUED")
            .Select(i => i.InvoicedQuantity ?? (i.Items.Any() ? i.Items.Sum(it => it.Quantity) : 0m))
            .ToListAsync();
        var deliveredFromInvoices = deliveredInvoicesList.Sum();

        var deliveredQty = deliveredFromInvoices > 0 ? Math.Round(deliveredFromInvoices, 3) : Math.Round(po.DeliveredQuantity, 3);
        var deliveredPrice = Math.Round(deliveredQty * tonPrice, 2);

        var remainingQty = Math.Round(Math.Max(0m, po.Quantity - deliveredQty), 3);
        var remainingPrice = Math.Round(remainingQty * tonPrice, 2);

        var perfPct = expectedQty > 0
            ? Math.Round((deliveredQty / expectedQty) * 100m, 2)
            : 100m;
        var perfPricePct = expectedPrice > 0
            ? Math.Round((deliveredPrice / expectedPrice) * 100m, 2)
            : 100m;

        var paidSum = Math.Round(po.Payments.Sum(p => p.Amount), 2);
        var outstanding = Math.Round(Math.Max(0m, deliveredPrice - paidSum), 2);

        var status = remainingQty <= 0 ? "مكتمل" : (deliveredQty > 0 ? "قيد التوريد" : "معلق");
        var paymentStatus = !string.IsNullOrWhiteSpace(po.PaymentStatus) ? po.PaymentStatus : "غير مدفوع";

        // Map transportation items
        var transDtoList = transRecords.Select(t => new PoTransportationItemDto
        {
            TransactionNumber = t.TransactionNumber,
            TransactionDate = t.TransactionDate,
            CarNumber = t.CarNumber,
            DriverName = t.DriverName,
            Source = t.Source,
            Destination = t.Destination,
            MaterialType = t.MaterialType,
            InternalWeight = t.InternalWeight,
            ExternalNetWeight = t.ExternalNetWeight,
            NetWeight = t.NetWeight,
            WasteQuantity = t.WasteQuantity,
            TransactionStatus = t.TransactionStatus,
            ReceivingStatus = t.ReceivingStatus,
            Notes = t.Notes,
            ShippingSupervisor = t.ShippingSupervisor,
            DepartmentSupervisor = t.DepartmentSupervisor
        }).ToList();

        // Map storage items
        var storageDtoList = storageRecords.Select(s => new PoStorageItemDto
        {
            MovementNumber = s.MovementNumber,
            TransactionDate = s.TransactionDate,
            Action = s.Action,
            ServiceType = s.ServiceType,
            Material = s.Material,
            IncomingQuantity = s.IncomingQuantity,
            OutgoingQuantity = s.OutgoingQuantity,
            InternalNetWeight = s.InternalNetWeight,
            ExternalNetWeight = s.ExternalNetWeight,
            WasteQuantity = (s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue) ? Math.Abs(s.InternalNetWeight - s.ExternalNetWeight.Value) : 0m,
            StorageLocation = s.StorageLocation,
            ReceivingEmployee = s.ReceivingEmployee,
            Notes = s.Notes
        }).ToList();

        // Map payments
        var paymentsDtoList = po.Payments.Select(pay => new FinancePaymentDto
        {
            Id = pay.Id,
            PoNumber = pay.PONumber,
            Amount = pay.Amount,
            PaymentDate = pay.PaymentDate,
            PaymentMethod = pay.PaymentMethod,
            ReferenceNumber = pay.ReferenceNumber,
            PaidBy = pay.PaidBy,
            Notes = pay.Notes,
            CreatedAt = pay.CreatedAt
        }).OrderByDescending(p => p.PaymentDate).ToList();

        // Build Chronological Timeline (Requirement 9)
        var timeline = new List<PoTimelineEventDto>();

        // 1. PO Created Event
        timeline.Add(new PoTimelineEventDto
        {
            Timestamp = po.OrderDate,
            EventType = "PO_CREATED",
            Title = "إصدار أمر الشراء",
            Description = $"تم تسجيل أمر الشراء بكمية تعاقدية {po.Quantity:N3} طن وبسعر {tonPrice:N2} ج.م/طن للمورد {po.Supplier}.",
            Details = $"الوجهة: {po.Destination} | إجمالي القيمة: {expectedPrice:N2} ج.م",
            BadgeType = "info"
        });

        // 2. Transportation Transactions
        foreach (var t in transRecords)
        {
            timeline.Add(new PoTimelineEventDto
            {
                Timestamp = t.TransactionDate,
                EventType = "TRANSPORTATION",
                Title = $"حركة نقل #{t.TransactionNumber}",
                Description = $"سيارة: {t.CarNumber} | السائق: {t.DriverName} | صافي الشحنة: {t.NetWeight:N3} طن",
                Details = $"هالك النقل: {t.WasteQuantity:N3} طن | حالة الاستلام: {t.ReceivingStatus}",
                BadgeType = t.WasteQuantity > 0 ? "warning" : "success"
            });
        }

        // 3. Storage Transactions
        foreach (var s in storageRecords)
        {
            var sWaste = (s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue)
                ? Math.Abs(s.InternalNetWeight - s.ExternalNetWeight.Value)
                : 0m;
            timeline.Add(new PoTimelineEventDto
            {
                Timestamp = s.TransactionDate,
                EventType = "STORAGE",
                Title = $"حركة مستودع #{s.MovementNumber} ({s.Action})",
                Description = $"الكمية: {(s.Action == "Import" ? s.IncomingQuantity : s.OutgoingQuantity):N3} طن | المستلم: {s.ReceivingEmployee}",
                Details = $"الموقع: {s.StorageLocation} | هالك التخزين: {sWaste:N3} طن",
                BadgeType = "neutral"
            });
        }

        // 4. CEO Payments
        foreach (var pay in po.Payments)
        {
            timeline.Add(new PoTimelineEventDto
            {
                Timestamp = pay.PaymentDate,
                EventType = "PAYMENT",
                Title = $"سداد دفعة مالية ({pay.Amount:N2} ج.م)",
                Description = $"طريقة السداد: {pay.PaymentMethod} | معتمد من: {pay.PaidBy}",
                Details = $"المرجع: {pay.ReferenceNumber ?? "-"} | ملاحظات: {pay.Notes ?? "-"}",
                BadgeType = "success"
            });
        }

        // 5. Payment Status Changes
        foreach (var aud in paymentAudits)
        {
            timeline.Add(new PoTimelineEventDto
            {
                Timestamp = aud.ChangedAt,
                EventType = "PAYMENT_STATUS_CHANGE",
                Title = $"تغيير حالة السداد: {aud.NewStatus}",
                Description = $"تم التعديل بواسطة: {aud.ChangedBy} (الحالة السابقة: {aud.PreviousStatus})",
                Details = aud.Notes ?? "تحديث معتمد من الإدارة المالية",
                BadgeType = "accent"
            });
        }

        // Sort chronologically (oldest to newest)
        timeline = timeline.OrderBy(x => x.Timestamp).ToList();

        var detailDto = new PurchaseOrderDetailDto
        {
            Id = po.Id,
            PoNumber = po.PONumber,
            SupplierId = po.SupplierId,
            Supplier = po.Supplier,
            MaterialName = !string.IsNullOrWhiteSpace(po.MaterialName) ? po.MaterialName : "مواد خرسانية / ركام",
            Unit = !string.IsNullOrWhiteSpace(po.Unit) ? po.Unit : "طن",
            Destination = po.Destination,
            OrderDate = po.OrderDate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            Quantity = po.Quantity,
            TonPrice = tonPrice,
            TotalValue = expectedPrice,
            InvoiceNumber = po.InvoiceNumber,
            Notes = po.Notes,
            Status = status,
            CreatedAt = po.CreatedAt,
            DeliveredQuantity = deliveredQty,
            DeliveredPrice = deliveredPrice,
            RemainingQuantity = remainingQty,
            RemainingPrice = remainingPrice,
            TransportationTransactionsCount = transCount,
            TotalSuppliedNetWeight = transNet,
            TransportationWasteQuantity = transWaste,
            TransportationWastePrice = transWastePrice,
            StorageWasteQuantity = storageWaste,
            StorageWastePrice = storageWastePrice,
            TotalWasteQuantity = totalWaste,
            TotalWastePrice = totalWastePrice,
            PerformancePercentage = perfPct,
            PerformancePricePercentage = perfPricePct,
            PaidAmount = paidSum,
            OutstandingAmount = outstanding,
            PaymentStatus = paymentStatus,
            PaymentStatusUpdatedAt = po.PaymentStatusUpdatedAt,
            PaymentStatusUpdatedBy = po.PaymentStatusUpdatedBy,
            IsCurrentUserFinanceDirector = isFinanceDirector,
            PaymentAudits = paymentAudits,
            Payments = paymentsDtoList,
            TransportationTransactions = transDtoList,
            StorageTransactions = storageDtoList,
            Timeline = timeline
        };

        return Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(detailDto));
    }

    /// <summary>
    /// Creates a new Purchase Order linked to an independent Supplier.
    /// </summary>
    [HttpPost("purchase-orders")]
    [ProducesResponseType(typeof(ApiResponse<FinancePoSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderDto dto)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بإضافة أوامر الشراء."));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("بيانات أمر الشراء غير مكتملة.", errors));
        }

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.SupplierId);
        if (supplier == null)
        {
            return BadRequest(ApiResponse.Fail("المورد المحدد غير موجود في سجل الموردين."));
        }

        var year = DateTime.UtcNow.Year;
        string poNumber = dto.PONumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            var prefix = $"PO-{year}-";
            var existing = await _context.PurchaseOrders
                .Where(p => p.PONumber.StartsWith(prefix))
                .Select(p => p.PONumber)
                .ToListAsync();
            int nextSeq = 1;
            if (existing.Count > 0)
            {
                var seqs = existing.Select(n => int.TryParse(n.Replace(prefix, ""), out var s) ? s : 0).ToList();
                nextSeq = seqs.Max() + 1;
            }
            poNumber = $"{prefix}{nextSeq:D4}";
        }
        else
        {
            if (await _context.PurchaseOrders.AnyAsync(p => p.PONumber == poNumber))
            {
                return BadRequest(ApiResponse.Fail($"رقم أمر الشراء '{poNumber}' مسجل بالفعل مسبقاً."));
            }
        }

        var qty = Math.Round(dto.Quantity, 3);
        var unitPrice = Math.Round(dto.UnitPrice, 2);
        var totalVal = Math.Round(qty * unitPrice, 2);

        var po = new PurchaseOrder
        {
            PONumber = poNumber,
            SupplierId = supplier.Id,
            Supplier = supplier.Name,
            MaterialName = dto.MaterialName.Trim(),
            Quantity = qty,
            RemainingQuantity = qty,
            DeliveredQuantity = 0m,
            Unit = !string.IsNullOrWhiteSpace(dto.Unit) ? dto.Unit.Trim() : "طن",
            PricePerTon = unitPrice,
            TonPrice = unitPrice,
            TotalValue = totalVal,
            OrderDate = dto.OrderDate ?? DateTime.UtcNow,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            Destination = !string.IsNullOrWhiteSpace(dto.Destination) ? dto.Destination.Trim() : "مصنع FaceStone",
            PaymentStatus = "غير مدفوع",
            Notes = dto.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Purchase Order {PONumber} created for supplier {SupplierName}.", po.PONumber, supplier.Name);

        return Ok(ApiResponse<FinancePoSummaryDto>.Ok(new FinancePoSummaryDto
        {
            Id = po.Id,
            PoNumber = po.PONumber,
            SupplierId = po.SupplierId,
            Supplier = po.Supplier,
            MaterialName = po.MaterialName,
            Unit = po.Unit,
            Destination = po.Destination,
            TonPrice = po.TonPrice,
            OrderDate = po.OrderDate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            ExpectedQuantity = po.Quantity,
            ExpectedPrice = po.TotalValue,
            DeliveredQuantity = 0m,
            DeliveredPrice = 0m,
            RemainingQuantity = po.RemainingQuantity,
            RemainingPrice = po.TotalValue,
            Status = "قيد التوريد",
            PaymentStatus = po.PaymentStatus,
            Notes = po.Notes
        }, "تم إنشاء أمر الشراء بنجاح."));
    }

    /// <summary>
    /// Updates an existing Purchase Order with validation against delivered quantities.
    /// </summary>
    [HttpPut("purchase-orders/{id:int}")]
    public async Task<IActionResult> UpdatePurchaseOrder(int id, [FromBody] UpdatePurchaseOrderDto dto)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بتعديل أوامر الشراء."));
        }

        var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id);
        if (po == null)
        {
            return NotFound(ApiResponse.Fail("أمر الشراء غير موجود."));
        }

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == dto.SupplierId);
        if (supplier == null)
        {
            return BadRequest(ApiResponse.Fail("المورد المحدد غير موجود."));
        }

        // Calculate actual delivered quantity from finalized invoices
        var deliveredList = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.PurchaseOrderId == po.Id && i.Status == "ISSUED")
            .Select(i => i.InvoicedQuantity ?? (i.Items.Any() ? i.Items.Sum(it => it.Quantity) : 0m))
            .ToListAsync();
        var delivered = deliveredList.Sum();

        if (dto.Quantity < delivered)
        {
            return BadRequest(ApiResponse.Fail($"لا يمكن تعديل الكمية المتعاقد عليها ({dto.Quantity:N3} طن) لتكون أقل من الكمية التي تم فوترتها وتسليمها بالفعل ({delivered:N3} طن)."));
        }

        var oldQty = po.Quantity;

        po.SupplierId = supplier.Id;
        po.Supplier = supplier.Name;
        po.MaterialName = dto.MaterialName.Trim();
        po.Quantity = Math.Round(dto.Quantity, 3);
        po.Unit = !string.IsNullOrWhiteSpace(dto.Unit) ? dto.Unit.Trim() : "طن";
        po.TonPrice = Math.Round(dto.UnitPrice, 2);
        po.PricePerTon = po.TonPrice;
        po.TotalValue = Math.Round(po.Quantity * po.TonPrice, 2);
        po.DeliveredQuantity = delivered;
        po.RemainingQuantity = Math.Max(0m, po.Quantity - delivered);
        if (dto.OrderDate.HasValue) po.OrderDate = dto.OrderDate.Value;
        po.ExpectedDeliveryDate = dto.ExpectedDeliveryDate;
        if (!string.IsNullOrWhiteSpace(dto.Destination)) po.Destination = dto.Destination.Trim();
        po.Notes = dto.Notes?.Trim();
        po.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Purchase Order {PONumber} updated. Quantity changed from {OldQty} to {NewQty}.", po.PONumber, oldQty, po.Quantity);

        return Ok(ApiResponse.Ok("تم تعديل أمر الشراء بنجاح."));
    }

    /// <summary>
    /// Updates the Payment Status for a Purchase Order.
    /// Strictly authorized to Finance Director role server-side.
    /// Rejects unauthorized users with 403 Forbidden.
    /// </summary>
    [HttpPut("purchase-orders/{poNumber}/payment-status")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePaymentStatus(string poNumber, [FromBody] UpdatePaymentStatusDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات التحديث غير صالحة."));
        }

        var rawStatus = !string.IsNullOrWhiteSpace(dto.Status) 
            ? dto.Status 
            : (!string.IsNullOrWhiteSpace(dto.PaymentStatus) ? dto.PaymentStatus : dto.NewStatus);
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return BadRequest(ApiResponse.Fail("حالة السداد مطلوبة."));
        }

        var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
        if (!AppRoles.IsFinanceDirector(userRole))
        {
            _logger.LogWarning("Unauthorized attempt to update payment status for PO '{PO}' by role '{Role}'.", poNumber, userRole);
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("عذراً، تغيير حالة السداد مسموح به لمدير المالية فقط."));
        }

        var poNum = poNumber.Trim();
        var po = await _context.PurchaseOrders
            .FirstOrDefaultAsync(p => p.PONumber == poNum);

        if (po == null)
        {
            return NotFound(ApiResponse.Fail($"أمر الشراء '{poNum}' غير موجود."));
        }

        var prevStatus = string.IsNullOrWhiteSpace(po.PaymentStatus) ? "غير مدفوع" : po.PaymentStatus;
        var newStatus = rawStatus.Trim();
        var callerName = User.FindFirst(ClaimTypes.Name)?.Value 
            ?? User.FindFirst("fullName")?.Value 
            ?? "Finance Director";

        po.PaymentStatus = newStatus;
        po.PaymentStatusUpdatedAt = DateTime.UtcNow;
        po.PaymentStatusUpdatedBy = callerName;
        po.UpdatedAt = DateTime.UtcNow;

        var audit = new PurchaseOrderPaymentAudit
        {
            PurchaseOrderId = po.Id,
            PONumber = po.PONumber,
            PreviousStatus = prevStatus,
            NewStatus = newStatus,
            ChangedBy = callerName,
            ChangedAt = DateTime.UtcNow,
            Notes = dto.Notes?.Trim()
        };

        await _context.PurchaseOrderPaymentAudits.AddAsync(audit);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment status for PO '{PO}' changed from '{Prev}' to '{New}' by '{Caller}'.",
            poNum, prevStatus, newStatus, callerName);

        return await GetPurchaseOrderDetails(poNum);
    }

    /// <summary>
    /// Retrieves Factory Performance Details explaining WHERE every summary performance number came from,
    /// showing transparent step-by-step arithmetic formulas, contributing transportation/storage records,
    /// and clearly distinguishing Date Range Activity from Cumulative PO Status.
    /// </summary>
    [HttpGet("performance")]
    [ProducesResponseType(typeof(ApiResponse<FinancePerformanceDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPerformanceDetails(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] DateTime? date,
        [FromQuery] string? po)
    {
        try
        {
            var dFrom = (fromDate ?? date ?? DateTime.Today).Date;
            var dTo = (toDate ?? date ?? dFrom).Date;
            if (dFrom > dTo)
            {
                var temp = dFrom;
                dFrom = dTo;
                dTo = temp;
            }
            var startRange = dFrom;
            var endRange = dTo.AddDays(1);

            var selectedPoTrimmed = string.IsNullOrWhiteSpace(po) || po.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : po.Trim();

            // 1. Authoritative POs
            var poQuery = _context.PurchaseOrders.AsNoTracking();
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                poQuery = poQuery.Where(p => p.PONumber == selectedPoTrimmed);
            }
            var poList = await poQuery.ToListAsync();

            decimal expectedQty = poList.Sum(p => p.Quantity);
            decimal expectedPrice = poList.Sum(p => p.Quantity * (p.TonPrice > 0 ? p.TonPrice : (p.PricePerTon > 0 ? p.PricePerTon : 0m)));
            decimal averageTonPrice = expectedQty > 0 ? Math.Round(expectedPrice / expectedQty, 2) : 0m;

            // 2. Cumulative Transportation for these PO(s)
            var transQuery = _context.Transportations.AsNoTracking()
                .Where(t => t.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(t.ApprovalStatus));
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                transQuery = transQuery.Where(t => t.PONumber == selectedPoTrimmed);
            }
            var cumTransList = await transQuery
                .Select(t => new { t.PONumber, t.NetWeight, t.WasteQuantity, t.TransactionDate })
                .ToListAsync();

            decimal cumTransWaste = Math.Round(cumTransList.Sum(t => t.WasteQuantity), 3);
            decimal cumTransWastePrice = Math.Round(cumTransWaste * averageTonPrice, 2);

            // 3. Cumulative Storage Waste for these PO(s)
            var storageQuery = _context.StorageTransactions.AsNoTracking()
                .Where(s => (s.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(s.ApprovalStatus)) && s.IsExportRelatedToPO && s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue);
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                storageQuery = storageQuery.Where(s => s.PONumber == selectedPoTrimmed);
            }
            var cumStorageRows = await storageQuery
                .Select(s => new { s.PONumber, Waste = Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value) })
                .ToListAsync();

            decimal cumStorageWaste = Math.Round(cumStorageRows.Sum(s => s.Waste), 3);
            decimal cumStorageWastePrice = Math.Round(cumStorageWaste * averageTonPrice, 2);

            decimal totalWaste = Math.Round(cumTransWaste + cumStorageWaste, 3);
            decimal totalWastePrice = Math.Round(cumTransWastePrice + cumStorageWastePrice, 2);

            decimal deliveredQty = Math.Round(Math.Max(0m, expectedQty - totalWaste), 3);
            decimal deliveredPrice = Math.Round(deliveredQty * averageTonPrice, 2);

            decimal remainingQty = Math.Round(poList.Sum(p => p.RemainingQuantity), 3);
            decimal remainingPrice = Math.Round(remainingQty * averageTonPrice, 2);

            decimal perfPct = expectedQty > 0 ? Math.Round((deliveredQty / expectedQty) * 100m, 2) : 100m;
            decimal perfPricePct = expectedPrice > 0 ? Math.Round((deliveredPrice / expectedPrice) * 100m, 2) : 100m;

            var status = perfPct >= 95m ? "High" : (perfPct >= 80m ? "Medium" : "Low");
            var statusArabic = status == "High" ? "أداء ممتاز" : (status == "Medium" ? "أداء مستقر" : "تنبيه هالك");

            // 4. Selected Date Range Activity (strictly within [dFrom, dTo])
            var rangeTransQuery = _context.Transportations.AsNoTracking()
                .Where(t => t.TransactionDate >= startRange && t.TransactionDate < endRange);
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeTransQuery = rangeTransQuery.Where(t => t.PONumber == selectedPoTrimmed);
            }
            var rangeTransList = await rangeTransQuery.Select(t => new { t.NetWeight, t.WasteQuantity }).ToListAsync();
            var rangeTransCount = rangeTransList.Count;
            var rangeTransNet = Math.Round(rangeTransList.Sum(t => t.NetWeight), 3);
            var rangeTransWaste = Math.Round(rangeTransList.Sum(t => t.WasteQuantity), 3);

            var rangeStorageImports = await _context.StorageTransactions.AsNoTracking()
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.Action == "Import" || s.ServiceType == "إيداع")
                .SumAsync(s => (decimal?)s.IncomingQuantity) ?? 0m;
            rangeStorageImports = Math.Round(rangeStorageImports, 3);

            var rangeStorageExportsQuery = _context.StorageTransactions.AsNoTracking()
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.Action == "Export" || s.ServiceType == "صرف");
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageExportsQuery = rangeStorageExportsQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }
            var rangeStorageExports = await rangeStorageExportsQuery.SumAsync(s => (decimal?)s.OutgoingQuantity) ?? 0m;
            rangeStorageExports = Math.Round(rangeStorageExports, 3);

            var rangeStorageWasteQuery = _context.StorageTransactions.AsNoTracking()
                .Where(s => s.TransactionDate >= startRange && s.TransactionDate < endRange)
                .Where(s => s.HasExternalWeighbridge && s.ExternalNetWeight.HasValue);
            if (!string.IsNullOrEmpty(selectedPoTrimmed))
            {
                rangeStorageWasteQuery = rangeStorageWasteQuery.Where(s => s.PONumber == selectedPoTrimmed && s.IsExportRelatedToPO);
            }
            var rangeStorageWaste = await rangeStorageWasteQuery
                .Select(s => Math.Abs(s.InternalNetWeight - s.ExternalNetWeight!.Value))
                .SumAsync();
            rangeStorageWaste = Math.Round(rangeStorageWaste, 3);

            var rangeTotalWaste = Math.Round(rangeTransWaste + rangeStorageWaste, 3);

            // Step-by-step arithmetic explanations (Requirements 11, 12, 13)
            var steps = new List<string>
            {
                $"الكمية المتوقعة: {expectedQty:N3} طن (تمثل إجمالي الكمية المتعاقد عليها لأوامر الشراء المحددة).",
                $"متوسط سعر الطن: {averageTonPrice:N2} ج.م/طن (المحدد في عقود أوامر الشراء).",
                $"القيمة المالية المتوقعة: {expectedQty:N3} طن × {averageTonPrice:N2} ج.م = {expectedPrice:N2} ج.م.",
                $"هالك النقل التراكمي: {cumTransWaste:N3} طن (مجموع الفروق بين أوزان الميزان الداخلي والخارجي لحركات النقل).",
                $"هالك التخزين التراكمي: {cumStorageWaste:N3} طن (مجموع الفروق بين أوزان الميزان الداخلي والخارجي لصادر المخزن).",
                $"إجمالي الهالك = هالك النقل ({cumTransWaste:N3}) + هالك التخزين ({cumStorageWaste:N3}) = {totalWaste:N3} طن.",
                $"القيمة المالية للهالك: {totalWaste:N3} طن × {averageTonPrice:N2} ج.م = {totalWastePrice:N2} ج.م.",
                $"الكمية المسلمة الفعلية = الكمية المتوقعة ({expectedQty:N3}) - إجمالي الهالك ({totalWaste:N3}) = {deliveredQty:N3} طن.",
                $"القيمة المالية للكمية المسلمة: {deliveredQty:N3} طن × {averageTonPrice:N2} ج.م = {deliveredPrice:N2} ج.م.",
                $"الكمية المتبقية: {remainingQty:N3} طن بقيمة تعاقدية {remainingPrice:N2} ج.م.",
                $"نسبة أداء المصنع (الكمية) = ({deliveredQty:N3} / {expectedQty:N3}) × 100 = {perfPct:F2}%.",
                $"نسبة الأداء المالي = ({deliveredPrice:N2} / {expectedPrice:N2}) × 100 = {perfPricePct:F2}%."
            };

            var poBreakdowns = new List<FinancePoSummaryDto>();
            foreach (var p in poList)
            {
                var pQty = p.Quantity;
                var pTonPrice = p.TonPrice > 0 ? p.TonPrice : (p.PricePerTon > 0 ? p.PricePerTon : 0m);
                var pExpectedPrice = Math.Round(pQty * pTonPrice, 2);

                var pTrans = cumTransList.Where(t => t.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase)).ToList();
                var pTransWaste = Math.Round(pTrans.Sum(t => t.WasteQuantity), 3);
                var pTransNet = Math.Round(pTrans.Sum(t => t.NetWeight), 3);

                var pStorageWaste = Math.Round(cumStorageRows.Where(s => !string.IsNullOrEmpty(s.PONumber) && s.PONumber.Equals(p.PONumber, StringComparison.OrdinalIgnoreCase)).Sum(s => s.Waste), 3);
                var pTotalWaste = Math.Round(pTransWaste + pStorageWaste, 3);
                var pDeliveredQty = Math.Round(Math.Max(0m, pQty - pTotalWaste), 3);
                var pDeliveredPrice = Math.Round(pDeliveredQty * pTonPrice, 2);
                var pRemQty = Math.Round(p.RemainingQuantity, 3);
                var pRemPrice = Math.Round(pRemQty * pTonPrice, 2);
                var pPerf = pQty > 0 ? Math.Round((pDeliveredQty / pQty) * 100m, 2) : 100m;

                poBreakdowns.Add(new FinancePoSummaryDto
                {
                    PoNumber = p.PONumber,
                    Supplier = p.Supplier,
                    Destination = p.Destination,
                    TonPrice = pTonPrice,
                    OrderDate = p.OrderDate,
                    ExpectedQuantity = pQty,
                    ExpectedPrice = pExpectedPrice,
                    DeliveredQuantity = pDeliveredQty,
                    DeliveredPrice = pDeliveredPrice,
                    RemainingQuantity = pRemQty,
                    RemainingPrice = pRemPrice,
                    TransportationTransactionsCount = pTrans.Count,
                    TransportationWasteQuantity = pTransWaste,
                    StorageWasteQuantity = pStorageWaste,
                    TotalWasteQuantity = pTotalWaste,
                    TotalWastePrice = Math.Round(pTotalWaste * pTonPrice, 2),
                    PerformancePercentage = pPerf,
                    Status = pRemQty <= 0 ? "مكتمل" : (pTrans.Count > 0 ? "قيد التوريد" : "معلق"),
                    PaymentStatus = !string.IsNullOrWhiteSpace(p.PaymentStatus) ? p.PaymentStatus : "غير مدفوع"
                });
            }

            var detailsDto = new FinancePerformanceDetailsDto
            {
                FromDate = dFrom,
                ToDate = dTo,
                SelectedPo = selectedPoTrimmed,
                Scope = selectedPoTrimmed == null ? "AllPos" : "SinglePo",
                ExpectedQuantity = expectedQty,
                TonPrice = averageTonPrice,
                ExpectedPrice = expectedPrice,
                ExpectedFormula = $"{expectedQty:N3} طن × {averageTonPrice:N2} ج.م = {expectedPrice:N2} ج.م",
                DeliveredQuantity = deliveredQty,
                DeliveredPrice = deliveredPrice,
                DeliveredFormula = $"{expectedQty:N3} طن (المتوقع) - {totalWaste:N3} طن (الهالك) = {deliveredQty:N3} طن",
                TransportationWasteQuantity = cumTransWaste,
                TransportationWastePrice = cumTransWastePrice,
                TransportationWasteFormula = $"مجموع هالك حركات النقل = {cumTransWaste:N3} طن (بقيمة {cumTransWastePrice:N2} ج.م)",
                StorageWasteQuantity = cumStorageWaste,
                StorageWastePrice = cumStorageWastePrice,
                StorageWasteFormula = $"مجموع هالك حركات المخازن = {cumStorageWaste:N3} طن (بقيمة {cumStorageWastePrice:N2} ج.م)",
                TotalWasteQuantity = totalWaste,
                TotalWastePrice = totalWastePrice,
                TotalWasteFormula = $"هالك النقل ({cumTransWaste:N3}) + هالك التخزين ({cumStorageWaste:N3}) = {totalWaste:N3} طن",
                RemainingQuantity = remainingQty,
                RemainingPrice = remainingPrice,
                RemainingFormula = $"{remainingQty:N3} طن × {averageTonPrice:N2} ج.م = {remainingPrice:N2} ج.م",
                QuantityPerformancePercentage = perfPct,
                QuantityPerformanceFormula = $"({deliveredQty:N3} / {expectedQty:N3}) × 100 = {perfPct:F2}%",
                PricePerformancePercentage = perfPricePct,
                PricePerformanceFormula = $"({deliveredPrice:N2} / {expectedPrice:N2}) × 100 = {perfPricePct:F2}%",
                Status = status,
                StatusLabelArabic = statusArabic,
                RangeTransportationTransactionsCount = rangeTransCount,
                RangeTransportationNetWeight = rangeTransNet,
                RangeTransportationWaste = rangeTransWaste,
                RangeStorageImportQuantity = rangeStorageImports,
                RangeStorageExportQuantity = rangeStorageExports,
                RangeStorageWaste = rangeStorageWaste,
                RangeTotalWaste = rangeTotalWaste,
                StepByStepCalculations = steps,
                PoBreakdowns = poBreakdowns
            };

            // Real Forecasting Calculation from active database POs
            var activePos = poList.Where(p => p.RemainingQuantity > 0).ToList();
            var expectedRemQty = activePos.Sum(p => p.RemainingQuantity);
            var expectedRemVal = activePos.Sum(p => p.RemainingQuantity * (p.TonPrice > 0 ? p.TonPrice : p.PricePerTon));
            var overallProgress = expectedQty > 0 ? Math.Round((deliveredQty / expectedQty) * 100, 2) : 0m;
            
            // Average daily delivery pace based on real transportation transactions
            var earliestTransDate = cumTransList.Any() ? cumTransList.Min(t => t.TransactionDate) : (DateTime?)null;
            var daysActive = earliestTransDate.HasValue ? Math.Max(1, (int)(DateTime.UtcNow.Date - earliestTransDate.Value.Date).TotalDays + 1) : 1;
            var avgDailyQty = daysActive > 0 ? Math.Round(deliveredQty / daysActive, 3) : 0m;
            var estimatedDays = avgDailyQty > 0 ? (int)Math.Ceiling(expectedRemQty / avgDailyQty) : 0;
            var projDate = estimatedDays > 0 ? DateTime.UtcNow.Date.AddDays(estimatedDays) : (DateTime?)null;

            detailsDto.Forecasting = new FinanceForecastingDto
            {
                ExpectedRemainingQuantity = expectedRemQty,
                ExpectedRemainingValue = expectedRemVal,
                OverallProgressPercentage = overallProgress,
                AverageDailyDeliveredQuantity = avgDailyQty,
                EstimatedDaysToCompletion = estimatedDays,
                ProjectedCompletionDate = projDate,
                ExpectedFinalFinancialYield = Math.Max(0, expectedPrice - totalWastePrice),
                ForecastingSummaryArabic = avgDailyQty > 0
                    ? $"بناءً على متوسط توريد فعلي قدره {avgDailyQty:N2} طن/يوم عبر {cumTransList.Count} نقلة منفذة، يُتوقع استكمال الكميات المتبقية ({expectedRemQty:N2} طن) خلال {estimatedDays} يوماً تشغيلياً تقريباً بحلول {projDate:yyyy-MM-dd}."
                    : $"المتبقي للتوريد {expectedRemQty:N2} طن بقيمة متوقعة {expectedRemVal:N2} ج.م في انتظار استئناف دورة حركات النقل لتحديد متوسط الإنجاز اليومي."
            };

            return Ok(ApiResponse<FinancePerformanceDetailsDto>.Ok(detailsDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance details.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء احتساب تفاصيل أداء المصنع."));
        }
    }

    /// <summary>
    /// Retrieves aggregated Finance Management data, summary KPIs, and filter options with server-side pagination.
    /// Strictly authorized only to: Accountants, IT, and CEO.
    /// </summary>
    [HttpGet("management-data")]
    [ProducesResponseType(typeof(ApiResponse<FinancePageResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFinanceManagementData([FromQuery] FinanceQueryParameters query)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            _logger.LogInformation("Finance management data requested by role '{Role}'. Search: '{Search}', Page: {Page}, PageSize: {PageSize}",
                userRole, query.Search, query.Page, query.PageSize);

            // 1. Fetch Purchase Orders with their Payments
            var poEntities = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(p => p.Payments)
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            // 2. Fetch Transportation records
            var transRecords = await _context.Transportations
                .AsNoTracking()
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            // 3. Fetch Management records
            var mgmtRecords = await _context.Management
                .AsNoTracking()
                .ToListAsync();

            // 4. Fetch Storage Transactions
            var storageRecords = await _context.StorageTransactions
                .AsNoTracking()
                .ToListAsync();

            // 5. Fetch all Payments (for direct PO mapping)
            var allPayments = await _context.FinancePayments
                .AsNoTracking()
                .ToListAsync();

            // Collect all unique PO Numbers across POs, Transportation, Management, Storage
            var poMap = new Dictionary<string, FinanceRecordDto>(StringComparer.OrdinalIgnoreCase);

            // Populate known Purchase Orders from database
            foreach (var po in poEntities)
            {
                var poNum = po.PONumber.Trim();
                var poPaymentsSum = allPayments
                    .Where(p => p.PONumber.Trim().Equals(poNum, StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount);

                poMap[poNum] = new FinanceRecordDto
                {
                    PoNumber = poNum,
                    Supplier = po.Supplier.Trim(),
                    OrderDate = po.OrderDate,
                    PoQuantity = po.Quantity,
                    TonPrice = po.TonPrice > 0 ? po.TonPrice : po.PricePerTon,
                    PoValue = po.TotalValue > 0 ? po.TotalValue : Math.Round(po.Quantity * (po.TonPrice > 0 ? po.TonPrice : po.PricePerTon), 2),
                    InvoiceNumber = string.IsNullOrWhiteSpace(po.InvoiceNumber) ? null : po.InvoiceNumber.Trim(),
                    PaidAmount = poPaymentsSum,
                    SuppliedQuantity = 0m,
                    MovementsCount = 0,
                    ApprovedQuantity = 0m,
                    UnapprovedQuantity = 0m
                };
            }

            // Discover and aggregate from Transportation records
            var transByPo = transRecords
                .Where(t => !string.IsNullOrWhiteSpace(t.PONumber))
                .GroupBy(t => t.PONumber.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in transByPo)
            {
                var poNum = group.Key;
                var sortedTrans = group.OrderBy(t => t.TransactionDate).ToList();
                var firstTrans = sortedTrans.First();
                var latestTrans = sortedTrans.Last();

                var transSupplied = group.Sum(t => t.NetWeight);
                var transMovements = group.Count();
                var transApproved = group
                    .Where(t => t.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.NetWeight);
                var transUnapproved = group
                    .Where(t => !t.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.NetWeight);

                if (poMap.TryGetValue(poNum, out var record))
                {
                    record.SuppliedQuantity = transSupplied;
                    record.MovementsCount = transMovements;
                    record.ApprovedQuantity = transApproved;
                    record.UnapprovedQuantity = transUnapproved;
                    if (string.IsNullOrWhiteSpace(record.Supplier) && !string.IsNullOrWhiteSpace(latestTrans.Source))
                    {
                        record.Supplier = latestTrans.Source.Trim();
                    }
                }
                else
                {
                    // PO exists in Transportation but wasn't in PO table yet
                    var supplier = !string.IsNullOrWhiteSpace(latestTrans.Source) ? latestTrans.Source.Trim() : "المورد الرئيسي للخرسانة";
                    var orderDate = firstTrans.TransactionDate;
                    var estimatedTonPrice = 1450.00m;
                    var estimatedPoQty = Math.Max(transSupplied * 1.2m, 100.0m);
                    var poVal = Math.Round(estimatedPoQty * estimatedTonPrice, 2);

                    var poPaymentsSum = allPayments
                        .Where(p => p.PONumber.Trim().Equals(poNum, StringComparison.OrdinalIgnoreCase))
                        .Sum(p => p.Amount);

                    poMap[poNum] = new FinanceRecordDto
                    {
                        PoNumber = poNum,
                        Supplier = supplier,
                        OrderDate = orderDate,
                        PoQuantity = Math.Round(estimatedPoQty, 3),
                        TonPrice = estimatedTonPrice,
                        PoValue = poVal,
                        InvoiceNumber = null,
                        SuppliedQuantity = transSupplied,
                        MovementsCount = transMovements,
                        ApprovedQuantity = transApproved,
                        UnapprovedQuantity = transUnapproved,
                        PaidAmount = poPaymentsSum
                    };
                }
            }

            // Incorporate Management records if applicable
            var mgmtByPo = mgmtRecords
                .Where(m => !string.IsNullOrWhiteSpace(m.PO))
                .GroupBy(m => m.PO!.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in mgmtByPo)
            {
                var poNum = group.Key;
                var mgmtTotal = group.Sum(m => m.TotalReceivedAmount);
                var mgmtAccepted = group.Sum(m => m.AcceptedQuantity);
                var mgmtUnaccepted = group.Sum(m => m.UnacceptedPendingQuantity);
                var mgmtMovements = group.Sum(m => m.NumberOfTransactions);

                if (poMap.TryGetValue(poNum, out var record))
                {
                    if (record.SuppliedQuantity == 0 && mgmtTotal > 0)
                    {
                        record.SuppliedQuantity = mgmtTotal;
                    }
                    if (record.MovementsCount == 0 && mgmtMovements > 0)
                    {
                        record.MovementsCount = mgmtMovements;
                    }
                    if (record.ApprovedQuantity == 0 && mgmtAccepted > 0)
                    {
                        record.ApprovedQuantity = mgmtAccepted;
                    }
                    if (record.UnapprovedQuantity == 0 && mgmtUnaccepted > 0)
                    {
                        record.UnapprovedQuantity = mgmtUnaccepted;
                    }
                }
            }

            // Incorporate Storage transactions if applicable
            var storageByPo = storageRecords
                .Where(s => !string.IsNullOrWhiteSpace(s.PONumber))
                .GroupBy(s => s.PONumber!.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in storageByPo)
            {
                var poNum = group.Key;
                var incomingSum = group.Where(s => s.ServiceType.Equals("إضافة", StringComparison.OrdinalIgnoreCase)).Sum(s => s.IncomingQuantity);

                if (poMap.TryGetValue(poNum, out var record))
                {
                    if (record.SuppliedQuantity == 0 && incomingSum > 0)
                    {
                        record.SuppliedQuantity = incomingSum;
                        if (record.ApprovedQuantity == 0) record.ApprovedQuantity = incomingSum;
                    }
                }
            }

            // Compute all calculated financial formulas for each record (Read-Only fields)
            var allRecords = poMap.Values.ToList();
            foreach (var r in allRecords)
            {
                // Ensure proper precision
                r.PoQuantity = Math.Round(r.PoQuantity, 3);
                r.TonPrice = Math.Round(r.TonPrice, 2);
                r.SuppliedQuantity = Math.Round(r.SuppliedQuantity, 3);

                // 5. قيمة أمر الشراء = كمية أمر الشراء × سعر الطن
                r.PoValue = Math.Round(r.PoQuantity * r.TonPrice, 2);

                // 8. قيمة الكمية التي تم توريدها = الكمية التي تم توريدها × سعر الطن
                r.SuppliedValue = Math.Round(r.SuppliedQuantity * r.TonPrice, 2);

                // 9. الكمية المتبقية = كمية أمر الشراء - الكمية التي تم توريدها
                r.RemainingQuantity = Math.Round(Math.Max(0m, r.PoQuantity - r.SuppliedQuantity), 3);

                // 10. القيمة المتبقية = الكمية المتبقية × سعر الطن
                r.RemainingValue = Math.Round(r.RemainingQuantity * r.TonPrice, 2);

                // 12 & 13. الكمية المعتمدة والغير معتمدة
                r.ApprovedQuantity = Math.Round(r.ApprovedQuantity, 3);
                r.UnapprovedQuantity = Math.Round(Math.Max(0m, r.SuppliedQuantity - r.ApprovedQuantity), 3);

                // 14. المبلغ المدفوع
                r.PaidAmount = Math.Round(r.PaidAmount, 2);

                // 15. المبلغ المستحق = قيمة الكمية التي تم توريدها - المبلغ المدفوع
                r.OutstandingAmount = Math.Round(Math.Max(0m, r.SuppliedValue - r.PaidAmount), 2);

                // 16. حالة السداد
                if (r.PaidAmount <= 0)
                {
                    r.PaymentStatus = "غير مدفوع";
                }
                else if (r.PaidAmount > 0 && r.PaidAmount < r.SuppliedValue)
                {
                    r.PaymentStatus = "مدفوع جزئياً";
                }
                else
                {
                    r.PaymentStatus = "مدفوع بالكامل";
                }
            }

            // Order records chronologically (newest POs first)
            allRecords = allRecords.OrderByDescending(r => r.OrderDate).ToList();

            // Calculate overall KPI Summary before filters
            var kpiSummary = new FinanceKpiSummaryDto
            {
                TotalPoValue = Math.Round(allRecords.Sum(r => r.PoValue), 2),
                TotalSuppliedQuantity = Math.Round(allRecords.Sum(r => r.SuppliedQuantity), 3),
                TotalRemainingQuantity = Math.Round(allRecords.Sum(r => r.RemainingQuantity), 3),
                TotalPaidAmount = Math.Round(allRecords.Sum(r => r.PaidAmount), 2),
                TotalOutstandingAmount = Math.Round(allRecords.Sum(r => r.OutstandingAmount), 2),
                TotalMovementsCount = allRecords.Sum(r => r.MovementsCount),
                TotalPosCount = allRecords.Count
            };

            // Build dynamic Filter Options
            var filterOptions = new FinanceFilterOptionsDto
            {
                Suppliers = allRecords.Select(r => r.Supplier).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                PaymentStatuses = new List<string> { "غير مدفوع", "مدفوع جزئياً", "مدفوع بالكامل" },
                InvoiceNumbers = allRecords.Select(r => r.InvoiceNumber).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i!).Distinct().OrderBy(i => i).ToList()
            };

            // Apply Search & Filters
            var filtered = allRecords.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                filtered = filtered.Where(r =>
                    r.PoNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    r.Supplier.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (r.InvoiceNumber != null && r.InvoiceNumber.Contains(s, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (!string.IsNullOrWhiteSpace(query.Po) && !query.Po.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.PoNumber.Contains(query.Po.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Supplier) && !query.Supplier.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.Supplier.Equals(query.Supplier.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.PaymentStatus) && !query.PaymentStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.PaymentStatus.Equals(query.PaymentStatus.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.InvoiceNumber) && !query.InvoiceNumber.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.InvoiceNumber != null && r.InvoiceNumber.Equals(query.InvoiceNumber.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (query.DateFrom.HasValue)
            {
                var dFrom = query.DateFrom.Value.Date;
                filtered = filtered.Where(r => r.OrderDate.Date >= dFrom);
            }

            if (query.DateTo.HasValue)
            {
                var dTo = query.DateTo.Value.Date;
                filtered = filtered.Where(r => r.OrderDate.Date <= dTo);
            }

            var filteredList = filtered.ToList();
            var totalCount = filteredList.Count;

            // Apply Server-Side Pagination
            var pageSize = Math.Clamp(query.PageSize, 5, 100);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var page = Math.Clamp(query.Page, 1, Math.Max(1, totalPages));

            var pagedRecords = filteredList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new FinancePageResponseDto
            {
                KpiSummary = kpiSummary,
                Records = pagedRecords,
                FilterOptions = filterOptions,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(ApiResponse<FinancePageResponseDto>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Finance management data.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("حدث خطأ أثناء تحميل البيانات المالية. يرجى المحاولة لاحقاً."));
        }
    }

    /// <summary>
    /// Retrieves payment history for a given Purchase Order.
    /// Supports the future CEO Dashboard architecture.
    /// </summary>
    [HttpGet("payments/{poNumber}")]
    [ProducesResponseType(typeof(ApiResponse<List<FinancePaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentsByPo(string poNumber)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب."));
        }

        var targetPo = poNumber.Trim();
        var payments = await _context.FinancePayments
            .AsNoTracking()
            .Where(p => p.PONumber == targetPo)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new FinancePaymentDto
            {
                Id = p.Id,
                PoNumber = p.PONumber,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                PaymentMethod = p.PaymentMethod,
                ReferenceNumber = p.ReferenceNumber,
                PaidBy = p.PaidBy,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(ApiResponse<List<FinancePaymentDto>>.Ok(payments));
    }

    /// <summary>
    /// Backend Architecture Foundation for Future CEO Payment Entry.
    /// Records an official payment against a Purchase Order.
    /// Strictly authorized to CEO and IT roles.
    /// </summary>
    [HttpPost("payments")]
    [Authorize(Roles = "CEO,IT")]
    [ProducesResponseType(typeof(ApiResponse<FinancePaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordCeoPayment([FromBody] CreateFinancePaymentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PoNumber))
        {
            return BadRequest(ApiResponse.Fail("رقم أمر الشراء مطلوب لتسجيل الدفعة."));
        }

        if (dto.Amount <= 0)
        {
            return BadRequest(ApiResponse.Fail("مبلغ الدفعة يجب أن يكون أكبر من الصفر."));
        }

        var poNum = dto.PoNumber.Trim();
        var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PONumber == poNum);

        var callerName = User.FindFirst(ClaimTypes.Name)?.Value 
            ?? User.FindFirst("fullName")?.Value 
            ?? "CEO Office";

        var payment = new FinancePayment
        {
            PONumber = poNum,
            PurchaseOrderId = po?.Id,
            Amount = Math.Round(dto.Amount, 2),
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Bank Transfer" : dto.PaymentMethod.Trim(),
            ReferenceNumber = dto.ReferenceNumber?.Trim(),
            PaidBy = callerName,
            Notes = dto.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _context.FinancePayments.AddAsync(payment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("CEO Payment of {Amount} recorded for PO '{PONumber}' by '{Caller}'.",
            payment.Amount, poNum, callerName);

        var resultDto = new FinancePaymentDto
        {
            Id = payment.Id,
            PoNumber = payment.PONumber,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod,
            ReferenceNumber = payment.ReferenceNumber,
            PaidBy = payment.PaidBy,
            Notes = payment.Notes,
            CreatedAt = payment.CreatedAt
        };

        return Ok(ApiResponse<FinancePaymentDto>.Ok(resultDto, "تم تسجيل الدفعة المالية بنجاح."));
    }

    // ========================================================================
    // 1. التاريخ — FINANCE HISTORY (/api/finance/history)
    // ========================================================================
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<FinanceHistoryResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinanceHistory(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? transactionType,
        [FromQuery] string? po,
        [FromQuery] string? supplier,
        [FromQuery] string? search)
    {
        try
        {
            var dFrom = fromDate?.Date;
            var dTo = toDate?.Date.AddDays(1); // inclusive

            var items = new List<FinanceHistoryItemDto>();

            // 1. Finance Payments
            var paymentsQuery = _context.FinancePayments.AsNoTracking().AsQueryable();
            if (dFrom.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= dFrom.Value);
            if (dTo.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate < dTo.Value);
            var payments = await paymentsQuery.ToListAsync();

            var poSuppliers = await _context.PurchaseOrders.AsNoTracking()
                .ToDictionaryAsync(p => p.PONumber, p => p.Supplier, StringComparer.OrdinalIgnoreCase);

            foreach (var p in payments)
            {
                poSuppliers.TryGetValue(p.PONumber, out var supName);
                items.Add(new FinanceHistoryItemDto
                {
                    Id = $"PAY-{p.Id}",
                    Date = p.PaymentDate,
                    TransactionType = "سداد دفعة مورد",
                    TransactionNumber = p.ReferenceNumber ?? $"PAY-{p.Id}",
                    PoNumber = p.PONumber,
                    Supplier = supName ?? "-",
                    Amount = p.Amount,
                    Direction = "Out",
                    Status = "مدفوع",
                    User = p.PaidBy ?? "الإدارة المالية",
                    Details = $"سداد دفعة لأمر الشراء {p.PONumber} عبر {p.PaymentMethod}. {p.Notes}",
                    ReferenceNumber = p.ReferenceNumber
                });
            }

            // 2. Bank Transactions
            var bankQuery = _context.BankTransactions.AsNoTracking().AsQueryable();
            if (dFrom.HasValue) bankQuery = bankQuery.Where(b => b.TransactionDate >= dFrom.Value);
            if (dTo.HasValue) bankQuery = bankQuery.Where(b => b.TransactionDate < dTo.Value);
            var bankTxs = await bankQuery.ToListAsync();

            foreach (var b in bankTxs)
            {
                items.Add(new FinanceHistoryItemDto
                {
                    Id = $"BANK-{b.Id}",
                    Date = b.TransactionDate,
                    TransactionType = $"معاملة بنكية: {b.TransactionType}",
                    TransactionNumber = b.ReferenceNumber ?? $"BNK-{b.Id}",
                    PoNumber = null,
                    Supplier = b.BankName,
                    Amount = b.Amount,
                    Direction = b.Direction,
                    Status = "معتمد",
                    User = b.CreatedBy,
                    Details = $"{b.BankName} | {b.Description}. {b.Notes}",
                    ReferenceNumber = b.ReferenceNumber
                });
            }

            // 3. Expenses
            var expensesQuery = _context.Expenses.AsNoTracking().AsQueryable();
            if (dFrom.HasValue) expensesQuery = expensesQuery.Where(e => e.ExpenseDate >= dFrom.Value);
            if (dTo.HasValue) expensesQuery = expensesQuery.Where(e => e.ExpenseDate < dTo.Value);
            var expenses = await expensesQuery.ToListAsync();

            foreach (var e in expenses)
            {
                items.Add(new FinanceHistoryItemDto
                {
                    Id = $"EXP-{e.Id}",
                    Date = e.ExpenseDate,
                    TransactionType = $"مصروف: {e.Category}",
                    TransactionNumber = e.ReferenceNumber ?? $"EXP-{e.Id}",
                    PoNumber = null,
                    Supplier = e.Beneficiary,
                    Amount = e.Amount,
                    Direction = "Out",
                    Status = "مصروف",
                    User = e.CreatedBy,
                    Details = $"{e.Title} | المستفيد: {e.Beneficiary} ({e.PaymentMethod}). {e.Notes}",
                    ReferenceNumber = e.ReferenceNumber
                });
            }

            // 4. Physical Transportation deliveries
            var transQuery = _context.Transportations.AsNoTracking()
                .Where(t => t.ApprovalStatus == "APPROVED" || string.IsNullOrEmpty(t.ApprovalStatus))
                .AsQueryable();
            if (dFrom.HasValue) transQuery = transQuery.Where(t => t.TransactionDate >= dFrom.Value);
            if (dTo.HasValue) transQuery = transQuery.Where(t => t.TransactionDate < dTo.Value);
            var deliveries = await transQuery.OrderByDescending(t => t.TransactionDate).Take(100).ToListAsync();

            var poPrices = await _context.PurchaseOrders.AsNoTracking()
                .ToDictionaryAsync(p => p.PONumber, p => p.TonPrice > 0 ? p.TonPrice : p.PricePerTon, StringComparer.OrdinalIgnoreCase);

            foreach (var t in deliveries)
            {
                poPrices.TryGetValue(t.PONumber, out var tonPrice);
                var estimatedVal = Math.Round(t.NetWeight * tonPrice, 2);
                items.Add(new FinanceHistoryItemDto
                {
                    Id = $"TR-{t.Id}",
                    Date = t.TransactionDate,
                    TransactionType = "توريد خامات (نقل)",
                    TransactionNumber = $"TR-{t.TransactionNumber}",
                    PoNumber = t.PONumber,
                    Supplier = !string.IsNullOrWhiteSpace(t.CompanyName) ? t.CompanyName : t.Source,
                    Amount = estimatedVal,
                    Direction = "In",
                    Status = t.ReceivingStatus ?? "مستلم",
                    User = t.ShippingSupervisor ?? "مسؤول الشحن",
                    Details = $"توريد {t.NetWeight:N3} طن خامة {t.MaterialType} | سيارة: {t.CarNumber} | هالك: {t.WasteQuantity:N3} طن",
                    ReferenceNumber = t.InternalWeighbridgeCode ?? t.BillOfLadingFileName
                });
            }

            // In-Memory filtering for search, po, supplier, type
            if (!string.IsNullOrWhiteSpace(transactionType) && !transactionType.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(i => i.TransactionType.Contains(transactionType, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(po) && !po.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(i => i.PoNumber != null && i.PoNumber.Equals(po.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(supplier) && !supplier.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(i => i.Supplier != null && i.Supplier.Contains(supplier.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                items = items.Where(i => 
                    i.TransactionNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (i.PoNumber != null && i.PoNumber.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (i.Supplier != null && i.Supplier.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    i.Details.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (i.ReferenceNumber != null && i.ReferenceNumber.Contains(q, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            items = items.OrderByDescending(i => i.Date).ToList();

            var totalIn = items.Where(i => i.Direction.Equals("In", StringComparison.OrdinalIgnoreCase)).Sum(i => i.Amount);
            var totalOut = items.Where(i => i.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase)).Sum(i => i.Amount);

            return Ok(ApiResponse<FinanceHistoryResponseDto>.Ok(new FinanceHistoryResponseDto
            {
                Items = items,
                TotalCount = items.Count,
                TotalInflow = totalIn,
                TotalOutflow = totalOut,
                FilterFromDate = fromDate?.ToString("yyyy-MM-dd"),
                FilterToDate = toDate?.ToString("yyyy-MM-dd"),
                FilterTransactionType = transactionType,
                FilterPo = po,
                FilterSupplier = supplier
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Finance History.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب السجل التاريخي للمالية."));
        }
    }

    // ========================================================================
    // 2. الموردين — INDEPENDENT SUPPLIERS (/api/finance/suppliers)
    // ========================================================================
    [HttpGet("suppliers")]
    [ProducesResponseType(typeof(ApiResponse<List<SupplierSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliers()
    {
        try
        {
            // Auto sync if empty
            if (!await _context.Suppliers.AnyAsync())
            {
                await DbInitializer.SynchronizeSuppliersAndPOsAsync(_context, _logger);
            }

            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.SupplierCode)
                .Select(s => new SupplierSummaryDto
                {
                    Id = s.Id,
                    SupplierCode = s.SupplierCode,
                    Name = s.Name,
                    PhoneNumber = s.PhoneNumber,
                    Email = s.Email,
                    Address = s.Address,
                    TaxNumber = s.TaxNumber,
                    CommercialRegister = s.CommercialRegister,
                    ContactPerson = s.ContactPerson,
                    PrimaryMaterialName = s.PrimaryMaterialName,
                    PrimaryMaterialType = s.PrimaryMaterialType,
                    SourceDepartment = s.SourceDepartment,
                    RelatedLaboratoryReferenceNumber = s.RelatedLaboratoryReferenceNumber,
                    RelatedWarehouseImportNumber = s.RelatedWarehouseImportNumber,
                    RegistrationDate = s.RegistrationDate,
                    TotalAmountOwed = s.TotalAmountOwed,
                    TotalAmountPaid = s.TotalAmountPaid,
                    RemainingBalance = s.RemainingBalance,
                    LastPaymentDate = s.LastPaymentDate,
                    TransactionsCount = s.FinancialTransactions.Count,
                    PurchaseOrdersCount = s.PurchaseOrders.Count,
                    IsActive = s.IsActive
                })
                .ToListAsync();

            return Ok(ApiResponse<List<SupplierSummaryDto>>.Ok(suppliers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suppliers list.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب بيانات الموردين."));
        }
    }

    [HttpGet("suppliers/{id}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplierDetail(string id)
    {
        try
        {
            var raw = Uri.UnescapeDataString(id).Trim();
            Supplier? s = null;

            if (int.TryParse(raw, out var parsedId))
            {
                s = await _context.Suppliers
                    .AsNoTracking()
                    .Include(x => x.FinancialTransactions)
                    .Include(x => x.PurchaseOrders)
                    .FirstOrDefaultAsync(x => x.Id == parsedId);
            }

            if (s == null)
            {
                s = await _context.Suppliers
                    .AsNoTracking()
                    .Include(x => x.FinancialTransactions)
                    .Include(x => x.PurchaseOrders)
                    .FirstOrDefaultAsync(x => x.Name.ToLower() == raw.ToLower() || x.SupplierCode == raw);
            }

            if (s == null)
            {
                return NotFound(ApiResponse.Fail($"المورد '{raw}' غير موجود في قاعدة البيانات."));
            }

            var txDtos = s.FinancialTransactions
                .OrderByDescending(t => t.TransactionDate)
                .Select(t => new SupplierFinancialTransactionDto
                {
                    Id = t.Id,
                    SupplierId = t.SupplierId,
                    TransactionType = t.TransactionType,
                    TransactionTypeArabic = t.TransactionType switch
                    {
                        "AMOUNT_OWED" => "مستحق للمورد",
                        "PAYMENT" => "دفعة مسددة",
                        "ADJUSTMENT" => "تسوية مالية",
                        "CHARGE" => "خصم / رسوم إضافية",
                        _ => t.TransactionType
                    },
                    Amount = t.Amount,
                    TransactionDate = t.TransactionDate,
                    Description = t.Description,
                    ReferenceNumber = t.ReferenceNumber,
                    PaymentMethod = t.PaymentMethod,
                    Notes = t.Notes,
                    InvoiceId = t.InvoiceId,
                    CreatedBy = t.CreatedBy,
                    CreatedAt = t.CreatedAt
                })
                .ToList();

            var poDtos = s.PurchaseOrders
                .OrderByDescending(p => p.OrderDate)
                .Select(p => new SupplierPoItemDto
                {
                    Id = p.Id,
                    PONumber = p.PONumber,
                    MaterialName = p.MaterialName,
                    OrderedQuantity = p.Quantity,
                    DeliveredQuantity = p.DeliveredQuantity,
                    RemainingQuantity = p.RemainingQuantity,
                    TonPrice = p.TonPrice,
                    TotalValue = p.TotalValue,
                    OrderDate = p.OrderDate,
                    Status = p.RemainingQuantity <= 0 ? "مكتمل" : "قيد التوريد"
                })
                .ToList();

            var detail = new SupplierDetailDto
            {
                Id = s.Id,
                SupplierCode = s.SupplierCode,
                Name = s.Name,
                PhoneNumber = s.PhoneNumber,
                Email = s.Email,
                Address = s.Address,
                TaxNumber = s.TaxNumber,
                CommercialRegister = s.CommercialRegister,
                ContactPerson = s.ContactPerson,
                Notes = s.Notes,
                PrimaryMaterialName = s.PrimaryMaterialName,
                PrimaryMaterialType = s.PrimaryMaterialType,
                SourceDepartment = s.SourceDepartment,
                RelatedLaboratoryReferenceNumber = s.RelatedLaboratoryReferenceNumber,
                RelatedWarehouseImportNumber = s.RelatedWarehouseImportNumber,
                RegistrationDate = s.RegistrationDate,
                TotalAmountOwed = s.TotalAmountOwed,
                TotalAmountPaid = s.TotalAmountPaid,
                RemainingBalance = s.RemainingBalance,
                LastPaymentDate = s.LastPaymentDate,
                IsActive = s.IsActive,
                Transactions = txDtos,
                PurchaseOrders = poDtos
            };

            return Ok(ApiResponse<SupplierDetailDto>.Ok(detail));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supplier details for id {Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب تفاصيل المورد."));
        }
    }

    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierDto dto)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بإضافة موردين."));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("بيانات المورد غير مكتملة.", errors));
        }

        var trimmedName = dto.Name.Trim();
        if (await _context.Suppliers.AnyAsync(s => s.Name.ToLower() == trimmedName.ToLower()))
        {
            return BadRequest(ApiResponse.Fail($"المورد '{trimmedName}' مسجل بالفعل في النظام."));
        }

        var existingCodes = await _context.Suppliers
            .Where(s => s.SupplierCode.StartsWith("SUP-"))
            .Select(s => s.SupplierCode)
            .ToListAsync();
        int nextSeq = 1;
        if (existingCodes.Any())
        {
            var seqs = existingCodes.Select(c => int.TryParse(c.Replace("SUP-", ""), out var v) ? v : 0).ToList();
            nextSeq = seqs.Max() + 1;
        }

        var currentUserName = User.FindFirst(ClaimTypes.Name)?.Value ?? "المدير المالي";

        var supplier = new Supplier
        {
            SupplierCode = $"SUP-{nextSeq:D4}",
            Name = trimmedName,
            PhoneNumber = dto.PhoneNumber?.Trim(),
            Email = dto.Email?.Trim(),
            Address = dto.Address?.Trim(),
            TaxNumber = dto.TaxNumber?.Trim(),
            CommercialRegister = dto.CommercialRegister?.Trim(),
            ContactPerson = dto.ContactPerson?.Trim(),
            PrimaryMaterialName = dto.PrimaryMaterialName?.Trim() ?? "مواد خرسانية / ركام",
            PrimaryMaterialType = dto.PrimaryMaterialType?.Trim() ?? "MECHANICAL",
            SourceDepartment = "FINANCE",
            Notes = dto.Notes?.Trim(),
            RegistrationDate = DateTime.UtcNow,
            TotalAmountOwed = 0m,
            TotalAmountPaid = 0m,
            RemainingBalance = 0m,
            IsActive = true,
            CreatedBy = currentUserName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Supplier {SupplierCode} - {SupplierName} created by {User}.", supplier.SupplierCode, supplier.Name, currentUserName);
        return Ok(ApiResponse<SupplierSummaryDto>.Ok(new SupplierSummaryDto
        {
            Id = supplier.Id,
            SupplierCode = supplier.SupplierCode,
            Name = supplier.Name,
            PhoneNumber = supplier.PhoneNumber,
            Email = supplier.Email,
            Address = supplier.Address,
            PrimaryMaterialName = supplier.PrimaryMaterialName,
            PrimaryMaterialType = supplier.PrimaryMaterialType,
            SourceDepartment = supplier.SourceDepartment,
            RegistrationDate = supplier.RegistrationDate,
            TotalAmountOwed = 0m,
            TotalAmountPaid = 0m,
            RemainingBalance = 0m,
            IsActive = true
        }, "تم إنشاء المورد بنجاح."));
    }

    [HttpPut("suppliers/{id:int}")]
    public async Task<IActionResult> UpdateSupplier(int id, [FromBody] UpdateSupplierDto dto)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بتعديل بيانات الموردين."));
        }

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (supplier == null) return NotFound(ApiResponse.Fail("المورد غير موجود."));

        var trimmedName = dto.Name.Trim();
        if (await _context.Suppliers.AnyAsync(s => s.Id != id && s.Name.ToLower() == trimmedName.ToLower()))
        {
            return BadRequest(ApiResponse.Fail($"اسم المورد '{trimmedName}' مستخدم بالفعل لمورد آخر."));
        }

        supplier.Name = trimmedName;
        supplier.PhoneNumber = dto.PhoneNumber?.Trim();
        supplier.Email = dto.Email?.Trim();
        supplier.Address = dto.Address?.Trim();
        supplier.TaxNumber = dto.TaxNumber?.Trim();
        supplier.CommercialRegister = dto.CommercialRegister?.Trim();
        supplier.ContactPerson = dto.ContactPerson?.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PrimaryMaterialName)) supplier.PrimaryMaterialName = dto.PrimaryMaterialName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PrimaryMaterialType)) supplier.PrimaryMaterialType = dto.PrimaryMaterialType.Trim();
        supplier.Notes = dto.Notes?.Trim();
        supplier.IsActive = dto.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(ApiResponse.Ok("تم تحديث بيانات المورد بنجاح."));
    }

    [HttpPost("suppliers/{id:int}/transactions")]
    public async Task<IActionResult> AddSupplierFinancialTransaction(int id, [FromBody] CreateSupplierTransactionDto dto)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بإضافة معاملات مالية للموردين."));
        }

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (supplier == null) return NotFound(ApiResponse.Fail("المورد غير موجود."));

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("بيانات المعاملة المالية غير صحيحة.", errors));
        }

        var currentUserName = User.FindFirst(ClaimTypes.Name)?.Value ?? "المدير المالي";
        var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? currentUserId = Guid.TryParse(currentUserIdStr, out var gid) ? gid : null;

        var amount = Math.Round(dto.Amount, 3);
        var txType = dto.TransactionType.ToUpperInvariant();

        var desc = !string.IsNullOrWhiteSpace(dto.Description)
            ? dto.Description.Trim()
            : (!string.IsNullOrWhiteSpace(dto.Notes) ? dto.Notes.Trim() : (txType == "PAYMENT" ? "سداد دفعة مالية" : "استحقاق مالي"));

        var tx = new SupplierFinancialTransaction
        {
            SupplierId = supplier.Id,
            TransactionType = txType,
            Amount = amount,
            TransactionDate = dto.TransactionDate ?? DateTime.UtcNow,
            Description = desc,
            ReferenceNumber = dto.ReferenceNumber?.Trim(),
            PaymentMethod = dto.PaymentMethod?.Trim(),
            Notes = dto.Notes?.Trim(),
            InvoiceId = dto.InvoiceId,
            CreatedBy = currentUserName,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        // Recalculate supplier balance
        if (txType == "AMOUNT_OWED" || txType == "CHARGE")
        {
            supplier.TotalAmountOwed += amount;
        }
        else if (txType == "PAYMENT")
        {
            supplier.TotalAmountPaid += amount;
            supplier.LastPaymentDate = tx.TransactionDate;
        }
        else if (txType == "ADJUSTMENT")
        {
            supplier.TotalAmountOwed += amount;
        }

        supplier.RemainingBalance = supplier.TotalAmountOwed - supplier.TotalAmountPaid;
        supplier.UpdatedAt = DateTime.UtcNow;

        _context.SupplierFinancialTransactions.Add(tx);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Supplier {SupplierCode} transaction {TxType} {Amount} EGP added. New balance: {Balance} EGP.",
            supplier.SupplierCode, txType, amount, supplier.RemainingBalance);

        return Ok(ApiResponse<SupplierSummaryDto>.Ok(new SupplierSummaryDto
        {
            Id = supplier.Id,
            SupplierCode = supplier.SupplierCode,
            Name = supplier.Name,
            TotalAmountOwed = supplier.TotalAmountOwed,
            TotalAmountPaid = supplier.TotalAmountPaid,
            RemainingBalance = supplier.RemainingBalance,
            LastPaymentDate = supplier.LastPaymentDate,
            IsActive = supplier.IsActive
        }));
    }

    [HttpPost("suppliers/sync")]
    public async Task<IActionResult> SyncSuppliers()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك."));
        }

        await DbInitializer.SynchronizeSuppliersAndPOsAsync(_context, _logger);
        return Ok(ApiResponse.Ok("تمت مزامنة واستكشاف الموردين من أقسام المعمل والمستودعات وأوامر الشراء بنجاح."));
    }

    // ========================================================================
    // 3. البنك — BANK TRANSACTIONS (/api/finance/bank)
    // ========================================================================
    [HttpGet("bank")]
    [ProducesResponseType(typeof(ApiResponse<BankSummaryResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBankTransactions()
    {
        try
        {
            var txs = await _context.BankTransactions.AsNoTracking()
                .OrderByDescending(b => b.TransactionDate)
                .ToListAsync();

            var dtos = txs.Select(b => new BankTransactionDto
            {
                Id = b.Id,
                TransactionDate = b.TransactionDate,
                TransactionType = b.TransactionType,
                Direction = b.Direction,
                Amount = b.Amount,
                BankName = b.BankName,
                ReferenceNumber = b.ReferenceNumber,
                Description = b.Description,
                CreatedBy = b.CreatedBy,
                Notes = b.Notes,
                CreatedAt = b.CreatedAt
            }).ToList();

            var totalReceived = dtos.Where(d => d.Direction.Equals("In", StringComparison.OrdinalIgnoreCase)).Sum(d => d.Amount);
            var totalSent = dtos.Where(d => d.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase)).Sum(d => d.Amount);
            var balance = totalReceived - totalSent;

            var bankBalances = dtos.GroupBy(d => d.BankName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Where(x => x.Direction.Equals("In", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount)
                       - g.Where(x => x.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount)
                );

            return Ok(ApiResponse<BankSummaryResponseDto>.Ok(new BankSummaryResponseDto
            {
                CurrentBalance = balance,
                TotalReceived = totalReceived,
                TotalSent = totalSent,
                TransactionsCount = dtos.Count,
                Transactions = dtos,
                BankBalances = bankBalances
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bank transactions.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب المعاملات البنكية."));
        }
    }

    [HttpPost("bank")]
    [ProducesResponseType(typeof(ApiResponse<BankTransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateBankTransaction([FromBody] CreateBankTransactionDto dto)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (!AppRoles.IsFinanceDirector(userRole))
            {
                return StatusCode(StatusCodes.Status403Forbidden, 
                    ApiResponse.Fail("غير مصرح: إضافة المعاملات البنكية مقتصر حصرياً على الإدارة المالية والمدير المالي."));
            }

            if (dto.Amount <= 0)
            {
                return BadRequest(ApiResponse.Fail("مبلغ المعاملة يجب أن يكون قيمة رقمية موجبة أكبر من الصفر."));
            }

            if (string.IsNullOrWhiteSpace(dto.BankName))
            {
                return BadRequest(ApiResponse.Fail("اسم البنك مطلوب."));
            }

            if (string.IsNullOrWhiteSpace(dto.TransactionType))
            {
                return BadRequest(ApiResponse.Fail("نوع المعاملة البنكية مطلوب."));
            }

            var dir = !string.IsNullOrWhiteSpace(dto.Direction) && (dto.Direction.Equals("Out", StringComparison.OrdinalIgnoreCase) || dto.Direction.Contains("صرف") || dto.Direction.Contains("Sent") || dto.Direction.Contains("سحب")) ? "Out" : "In";
            var callerName = User.FindFirst(ClaimTypes.Name)?.Value 
                ?? User.FindFirst("fullName")?.Value 
                ?? "مدير الإدارة المالية";

            var record = new BankTransaction
            {
                TransactionDate = dto.TransactionDate == default ? DateTime.UtcNow : dto.TransactionDate,
                TransactionType = dto.TransactionType.Trim(),
                Direction = dir,
                Amount = Math.Round(dto.Amount, 2),
                BankName = dto.BankName.Trim(),
                ReferenceNumber = dto.ReferenceNumber?.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? $"{dto.TransactionType} في {dto.BankName}" : dto.Description.Trim(),
                CreatedBy = callerName,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _context.BankTransactions.AddAsync(record);
            await _context.SaveChangesAsync();

            var result = new BankTransactionDto
            {
                Id = record.Id,
                TransactionDate = record.TransactionDate,
                TransactionType = record.TransactionType,
                Direction = record.Direction,
                Amount = record.Amount,
                BankName = record.BankName,
                ReferenceNumber = record.ReferenceNumber,
                Description = record.Description,
                CreatedBy = record.CreatedBy,
                Notes = record.Notes,
                CreatedAt = record.CreatedAt
            };

            return StatusCode(StatusCodes.Status201Created, ApiResponse<BankTransactionDto>.Ok(result, "تم حفظ المعاملة البنكية بنجاح."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding bank transaction.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ المعاملة البنكية."));
        }
    }

    // ========================================================================
    // 4. المصاريف — EXPENSES (/api/finance/expenses)
    // ========================================================================
    [HttpGet("expenses")]
    [ProducesResponseType(typeof(ApiResponse<ExpensesSummaryResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? category)
    {
        try
        {
            var allExpenses = await _context.Expenses.AsNoTracking()
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            var totalAll = allExpenses.Sum(e => e.Amount);
            var today = DateTime.UtcNow.Date;
            var todayExpenses = allExpenses.Where(e => e.ExpenseDate.Date == today).Sum(e => e.Amount);

            var filtered = allExpenses.AsEnumerable();
            if (fromDate.HasValue) filtered = filtered.Where(e => e.ExpenseDate.Date >= fromDate.Value.Date);
            if (toDate.HasValue) filtered = filtered.Where(e => e.ExpenseDate.Date <= toDate.Value.Date);
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(e => e.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();
            var rangeExpenses = filteredList.Sum(e => e.Amount);

            var byCat = filteredList.GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            var dtos = filteredList.Select(e => new ExpenseDto
            {
                Id = e.Id,
                ExpenseDate = e.ExpenseDate,
                Category = e.Category,
                Amount = e.Amount,
                Title = e.Title,
                Beneficiary = e.Beneficiary,
                ReferenceNumber = e.ReferenceNumber,
                PaymentMethod = e.PaymentMethod,
                CreatedBy = e.CreatedBy,
                Notes = e.Notes,
                CreatedAt = e.CreatedAt
            }).ToList();

            var catBreakdown = byCat.Select(kv => new ExpenseCategoryBreakdownDto
            {
                Category = kv.Key,
                TotalAmount = kv.Value,
                Count = filteredList.Count(e => e.Category.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)),
                Percentage = totalAll > 0 ? Math.Round((kv.Value / totalAll) * 100, 2) : 0
            }).ToList();

            return Ok(ApiResponse<ExpensesSummaryResponseDto>.Ok(new ExpensesSummaryResponseDto
            {
                TotalExpenses = totalAll,
                TodayExpenses = todayExpenses,
                RangeExpenses = rangeExpenses,
                ExpensesCount = dtos.Count,
                ExpensesByCategory = byCat,
                CategoryBreakdown = catBreakdown,
                Expenses = dtos
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب بيانات المصاريف."));
        }
    }

    [HttpPost("expenses")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto dto)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (!AppRoles.IsFinanceDirector(userRole))
            {
                return StatusCode(StatusCodes.Status403Forbidden, 
                    ApiResponse.Fail("غير مصرح: إضافة المصاريف مقتصر حصرياً على الإدارة المالية والمدير المالي."));
            }

            if (dto.Amount <= 0)
            {
                return BadRequest(ApiResponse.Fail("مبلغ المصروف يجب أن يكون قيمة رقمية موجبة أكبر من الصفر."));
            }

            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest(ApiResponse.Fail("بيان المصروف مطلوب."));
            }

            if (string.IsNullOrWhiteSpace(dto.Beneficiary))
            {
                return BadRequest(ApiResponse.Fail("المستفيد / الجهة مطلوبة."));
            }

            var callerName = User.FindFirst(ClaimTypes.Name)?.Value 
                ?? User.FindFirst("fullName")?.Value 
                ?? "مدير الإدارة المالية";

            var expense = new Expense
            {
                ExpenseDate = dto.ExpenseDate == default ? DateTime.UtcNow : dto.ExpenseDate,
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "أخرى" : dto.Category.Trim(),
                Amount = Math.Round(dto.Amount, 2),
                Title = dto.Title.Trim(),
                Beneficiary = dto.Beneficiary.Trim(),
                ReferenceNumber = dto.ReferenceNumber?.Trim(),
                PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Bank Transfer" : dto.PaymentMethod.Trim(),
                CreatedBy = callerName,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _context.Expenses.AddAsync(expense);
            await _context.SaveChangesAsync();

            var result = new ExpenseDto
            {
                Id = expense.Id,
                ExpenseDate = expense.ExpenseDate,
                Category = expense.Category,
                Amount = expense.Amount,
                Title = expense.Title,
                Beneficiary = expense.Beneficiary,
                ReferenceNumber = expense.ReferenceNumber,
                PaymentMethod = expense.PaymentMethod,
                CreatedBy = expense.CreatedBy,
                Notes = expense.Notes,
                CreatedAt = expense.CreatedAt
            };

            return StatusCode(StatusCodes.Status201Created, ApiResponse<ExpenseDto>.Ok(result, "تم تسجيل المصروف بنجاح."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تسجيل المصروف."));
        }
    }

    // ========================================================================
    // 5. الموظفين — EMPLOYEES FINANCE (/api/finance/employees)
    // ========================================================================
    [HttpGet("employees")]
    [ProducesResponseType(typeof(ApiResponse<List<FinanceEmployeeSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployeesFinance()
    {
        try
        {
            var users = await _context.Users.AsNoTracking()
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var finRecords = await _context.EmployeeFinancialRecords.AsNoTracking()
                .ToDictionaryAsync(r => r.UserId);

            var lendings = await _context.EmployeeLendings.AsNoTracking().ToListAsync();
            var attendance = await _context.AttendanceRecords.AsNoTracking().ToListAsync();

            var result = new List<FinanceEmployeeSummaryDto>();

            foreach (var u in users)
            {
                finRecords.TryGetValue(u.Id, out var fin);
                var uLendings = lendings.Where(l => l.UserId == u.Id).ToList();
                var uAtt = attendance.Where(a => a.UserId == u.Id).ToList();

                var totLending = uLendings.Sum(l => l.LendingAmount);
                var paidLending = uLendings.Sum(l => l.PaidAmount);
                var remLending = uLendings.Sum(l => l.RemainingAmount);

                var daysPresent = uAtt.Count(a => a.Status.Equals("Present", StringComparison.OrdinalIgnoreCase));
                var daysAbsent = uAtt.Count(a => a.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase));
                var totDays = daysPresent + daysAbsent;
                decimal? attPct = totDays > 0 ? Math.Round((decimal)daysPresent / totDays * 100, 1) : null;

                var dept = u.Role switch
                {
                    "TRANSPORTATION_MANAGER" or "Transportation Logistics Manager" => "إدارة النقل واللوجستيات",
                    "STORAGE_MANAGER" or "Storage & Factory Operations Manager" => "إدارة المخازن والمستودعات",
                    "FACTORY_MANAGER" or "Factory Manager" => "إدارة المصنع والتشغيل",
                    "FINANCE_DIRECTOR" or "Accountant" => "الإدارة المالية والمحاسبة",
                    "CEO" => "الإدارة العليا",
                    "IT" => "تكنولوجيا المعلومات",
                    _ => "شؤون العمليات والمواقع"
                };

                result.Add(new FinanceEmployeeSummaryDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Username = u.Username,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role,
                    Department = dept,
                    BasicSalary = fin?.BasicSalary ?? 0m,
                    NetSalary = fin?.NetSalary ?? 0m,
                    HasSalaryRecord = fin != null,
                    TotalLending = totLending,
                    PaidLending = paidLending,
                    RemainingLending = remLending,
                    DaysPresent = daysPresent,
                    DaysAbsent = daysAbsent,
                    AttendancePercentage = attPct,
                    FinancialStatus = remLending > 0 ? "توجد سلفة مستحقة" : "منتظم"
                });
            }

            return Ok(ApiResponse<List<FinanceEmployeeSummaryDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee financial summaries.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب بيانات الموظفين المالية."));
        }
    }

    [HttpGet("employees/{id}")]
    [ProducesResponseType(typeof(ApiResponse<FinanceEmployeeDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployeeFinanceDetail(string id)
    {
        try
        {
            UserAccount? user = null;
            if (Guid.TryParse(id, out var userGuid))
            {
                user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userGuid);
            }
            if (user == null)
            {
                user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == id);
            }

            if (user == null)
            {
                return NotFound(ApiResponse.Fail($"الموظف '{id}' غير موجود."));
            }

            var fin = await _context.EmployeeFinancialRecords.AsNoTracking().FirstOrDefaultAsync(f => f.UserId == user.Id);
            var lendings = await _context.EmployeeLendings.AsNoTracking()
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.RequestDate)
                .ToListAsync();

            var attendance = await _context.AttendanceRecords.AsNoTracking()
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.Date)
                .Take(30)
                .ToListAsync();

            var totLending = lendings.Sum(l => l.LendingAmount);
            var paidLending = lendings.Sum(l => l.PaidAmount);
            var remLending = lendings.Sum(l => l.RemainingAmount);

            var daysPresent = attendance.Count(a => a.Status.Equals("Present", StringComparison.OrdinalIgnoreCase));
            var daysAbsent = attendance.Count(a => a.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase));
            var totDays = daysPresent + daysAbsent;
            decimal? attPct = totDays > 0 ? Math.Round((decimal)daysPresent / totDays * 100, 1) : null;

            var dept = user.Role switch
            {
                "TRANSPORTATION_MANAGER" or "Transportation Logistics Manager" => "إدارة النقل واللوجستيات",
                "STORAGE_MANAGER" or "Storage & Factory Operations Manager" => "إدارة المخازن والمستودعات",
                "FACTORY_MANAGER" or "Factory Manager" => "إدارة المصنع والتشغيل",
                "FINANCE_DIRECTOR" or "Accountant" => "الإدارة المالية والمحاسبة",
                "CEO" => "الإدارة العليا",
                "IT" => "تكنولوجيا المعلومات",
                _ => "شؤون العمليات والمواقع"
            };

            var lendingDtos = lendings.Select(l => new EmployeeLendingDto
            {
                Id = l.Id,
                LendingAmount = l.LendingAmount,
                PaidAmount = l.PaidAmount,
                RemainingAmount = l.RemainingAmount,
                RequestDate = l.RequestDate,
                ApprovalStatus = l.ApprovalStatus,
                ApprovedBy = l.ApprovedBy,
                Notes = l.Notes
            }).ToList();

            var attendanceDtos = attendance.Select(a => new AttendanceRecordDto
            {
                Id = a.Id,
                Date = a.Date,
                Status = a.Status,
                CheckInTime = a.CheckInTime,
                CheckOutTime = a.CheckOutTime,
                Notes = a.Notes
            }).ToList();

            return Ok(ApiResponse<FinanceEmployeeDetailDto>.Ok(new FinanceEmployeeDetailDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                Department = dept,
                BasicSalary = fin?.BasicSalary ?? 0m,
                Allowances = fin?.Allowances ?? 0m,
                InsuranceDeductions = fin?.InsuranceDeductions ?? 0m,
                NetSalary = fin?.NetSalary ?? 0m,
                HasSalaryRecord = fin != null,
                PaymentMethod = fin?.PaymentMethod ?? "تحويل بنكي",
                BankAccountNumber = fin?.BankAccountNumber,
                TotalLending = totLending,
                PaidLending = paidLending,
                RemainingLending = remLending,
                DaysPresent = daysPresent,
                DaysAbsent = daysAbsent,
                AttendancePercentage = attPct,
                FinancialStatus = remLending > 0 ? "توجد سلفة مستحقة" : "منتظم",
                Lendings = lendingDtos,
                RecentAttendance = attendanceDtos
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee financial detail for: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب تفاصيل الموظف المالية."));
        }
    }

    // =========================================================================
    // INVOICES & BILLING MODULE (الفواتير)
    // =========================================================================

    /// <summary>
    /// Get Invoices KPI Statistics for Hub Card and View Header.
    /// </summary>
    [HttpGet("invoices/statistics")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceStatisticsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoiceStatistics()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بالوصول لبيانات الفواتير."));
        }

        try
        {
            var total = await _context.Invoices.CountAsync();
            var drafts = await _context.Invoices.CountAsync(i => i.Status == "DRAFT");
            var issued = await _context.Invoices.CountAsync(i => i.Status == "ISSUED");
            var printed = await _context.Invoices.CountAsync(i => i.IsPrinted);
            var totalAmount = await _context.Invoices
                .Where(i => i.Status != "CANCELLED")
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            return Ok(ApiResponse<InvoiceStatisticsDto>.Ok(new InvoiceStatisticsDto
            {
                TotalInvoices = total,
                DraftInvoices = drafts,
                IssuedInvoices = issued,
                PrintedInvoices = printed,
                TotalInvoicedAmount = totalAmount
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading invoice statistics.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء تحميل إحصائيات الفواتير."));
        }
    }

    /// <summary>
    /// Get paginated list of invoices with filtering.
    /// </summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] InvoiceQueryParameters query)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بالوصول لقائمة الفواتير."));
        }

        try
        {
            var q = _context.Invoices
                .AsNoTracking()
                .Include(i => i.Items)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim().ToLower();
                q = q.Where(i => i.InvoiceNumber.ToLower().Contains(s) ||
                                 i.CompanyName.ToLower().Contains(s) ||
                                 (i.PONumber != null && i.PONumber.ToLower().Contains(s)) ||
                                 i.ProjectCodeValue.ToLower().Contains(s) ||
                                 i.ProjectName.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "ALL")
            {
                if (query.Status.Equals("PRINTED", StringComparison.OrdinalIgnoreCase))
                {
                    q = q.Where(i => i.IsPrinted);
                }
                else
                {
                    var st = query.Status.ToUpper();
                    q = q.Where(i => i.Status == st);
                }
            }

            if (query.ProjectCodeId.HasValue && query.ProjectCodeId.Value > 0)
            {
                q = q.Where(i => i.ProjectCodeId == query.ProjectCodeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.PONumber))
            {
                var poTrim = query.PONumber.Trim();
                q = q.Where(i => i.PONumber == poTrim);
            }

            if (query.CompanyId.HasValue && query.CompanyId.Value > 0)
            {
                q = q.Where(i => i.CompanyId == query.CompanyId.Value);
            }

            if (query.FromDate.HasValue)
            {
                q = q.Where(i => i.InvoiceDate >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                q = q.Where(i => i.InvoiceDate <= query.ToDate.Value);
            }

            var totalCount = await q.CountAsync();
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 5, 100);

            var items = await q
                .OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new InvoiceSummaryDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    ProjectCodeId = i.ProjectCodeId,
                    ProjectCodeValue = i.ProjectCodeValue,
                    ProjectName = i.ProjectName,
                    RelatedEntityType = i.RelatedEntityType,
                    PurchaseOrderId = i.PurchaseOrderId,
                    PONumber = i.PONumber,
                    SupplierId = i.SupplierId,
                    SupplierName = i.SupplierName,
                    MaterialName = i.MaterialName,
                    CompanyName = i.CompanyName,
                    TransactionCount = i.Items.Count,
                    InvoicedQuantity = i.InvoicedQuantity ?? i.Items.Sum(it => it.Quantity),
                    Subtotal = i.Subtotal,
                    ShippingPrice = i.ShippingPrice,
                    TaxRate = i.TaxRate,
                    TaxAmount = i.TaxAmount,
                    DiscountAmount = i.DiscountAmount,
                    TotalAmount = i.TotalAmount,
                    Status = i.Status,
                    IsPrinted = i.IsPrinted,
                    PrintedAt = i.PrintedAt,
                    CreatedBy = i.CreatedBy,
                    CreatedAt = i.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                message = "تم استرجاع قائمة الفواتير بنجاح.",
                data = items,
                totalCount,
                page,
                pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching invoices list.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب قائمة الفواتير."));
        }
    }

    /// <summary>
    /// Get Project Codes list for Invoice creation/filter dropdown.
    /// </summary>
    [HttpGet("invoices/project-codes")]
    [ProducesResponseType(typeof(ApiResponse<List<ProjectCodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoiceProjectCodes()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك."));
        }

        var codes = await _context.ProjectCodes
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

        return Ok(ApiResponse<List<ProjectCodeDto>>.Ok(codes));
    }

    /// <summary>
    /// Get Companies (StorageClients) for Invoice association.
    /// </summary>
    [HttpGet("invoices/companies")]
    public async Task<IActionResult> GetInvoiceCompanies()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك."));
        }

        var clients = await _context.StorageClients
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.PhoneNumber
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(clients));
    }

    /// <summary>
    /// Get Purchase Orders for Invoice association with live delivered and remaining quantities.
    /// </summary>
    [HttpGet("invoices/purchase-orders")]
    [ProducesResponseType(typeof(ApiResponse<List<EligiblePurchaseOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoicePurchaseOrders()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك."));
        }

        var issuedInvoices2 = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Status == "ISSUED" && i.PurchaseOrderId.HasValue)
            .Select(i => new {
                PoId = i.PurchaseOrderId!.Value,
                InvoicedQty = i.InvoicedQuantity ?? (i.Items.Any() ? i.Items.Sum(it => it.Quantity) : 0m)
            })
            .ToListAsync();

        var activeInvoices = issuedInvoices2
            .GroupBy(x => x.PoId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.InvoicedQty));

        var pos = await _context.PurchaseOrders
            .AsNoTracking()
            .OrderByDescending(p => p.OrderDate)
            .ToListAsync();

        var dtos = pos.Select(p =>
        {
            var prevInvoiced = activeInvoices.TryGetValue(p.Id, out var inv) ? inv : 0m;
            var remaining = Math.Max(0m, p.Quantity - prevInvoiced);
            var tonPrice = p.TonPrice > 0 ? p.TonPrice : (p.PricePerTon > 0 ? p.PricePerTon : 0m);
            return new EligiblePurchaseOrderDto
            {
                Id = p.Id,
                PONumber = p.PONumber,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier,
                MaterialName = !string.IsNullOrWhiteSpace(p.MaterialName) ? p.MaterialName : "مواد خرسانية / ركام",
                OrderedQuantity = p.Quantity,
                PreviouslyInvoicedQuantity = prevInvoiced,
                RemainingQuantity = remaining,
                Unit = !string.IsNullOrWhiteSpace(p.Unit) ? p.Unit : "طن",
                UnitPrice = tonPrice,
                TotalValue = Math.Round(p.Quantity * tonPrice, 2),
                OrderDate = p.OrderDate,
                Destination = p.Destination
            };
        }).ToList();

        return Ok(ApiResponse<List<EligiblePurchaseOrderDto>>.Ok(dtos));
    }

    /// <summary>
    /// Shared helper to retrieve approved Transportation transactions belonging exclusively to a ProjectCode
    /// that still have remaining un-invoiced net weight.
    /// </summary>
    private async Task<List<EligibleTransportationTransactionDto>> FetchEligibleTransportationsForProjectAsync(ProjectCode prj)
    {
        var normalizedCode = (prj.Code ?? string.Empty).Trim();
        var transList = await _context.Transportations
            .AsNoTracking()
            .Where(t => (t.ProjectCodeId == prj.Id
                         || (t.ProjectCodeId == null && t.ProjectCodeValue != null && t.ProjectCodeValue == normalizedCode))
                        && t.ApprovalStatus != "REJECTED"
                        && t.NetWeight > 0)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        if (transList.Count == 0)
        {
            return new List<EligibleTransportationTransactionDto>();
        }

        var transIds = transList.Select(t => t.Id).ToList();

        var invoicedMap = await _context.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.TransportationId.HasValue && transIds.Contains(ii.TransportationId.Value) && ii.Invoice.Status != "CANCELLED")
            .GroupBy(ii => ii.TransportationId!.Value)
            .Select(g => new { TransportationId = g.Key, InvoicedQty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.TransportationId, x => x.InvoicedQty);

        var result = new List<EligibleTransportationTransactionDto>();

        foreach (var t in transList)
        {
            var invoiced = invoicedMap.TryGetValue(t.Id, out var inv) ? inv : 0m;
            var remaining = Math.Max(0m, t.NetWeight - invoiced);

            if (remaining <= 0.0001m) continue;

            result.Add(new EligibleTransportationTransactionDto
            {
                TransportationId = t.Id,
                TransactionNumber = t.TransactionNumber,
                WarehouseExportNumber = !string.IsNullOrWhiteSpace(t.ExportTransactionNumber)
                    ? t.ExportTransactionNumber
                    : (t.StorageOperationId.HasValue ? $"#{t.StorageOperationId.Value}" : "-"),
                ProductName = !string.IsNullOrWhiteSpace(t.MaterialType) ? t.MaterialType : "منتج خرساني / مادة خام",
                ReferenceNumber = t.ManufactureNumber ?? (t.StorageOperationId.HasValue ? $"EXP-{t.StorageOperationId}" : t.PONumber),
                BatchNumber = t.BatchNumber ?? "-",
                ProductionOrderNumber = t.ProductionOrderId.HasValue ? $"PO-{t.ProductionOrderId}" : null,
                TotalTransportedQuantity = t.NetWeight,
                PreviouslyInvoicedQuantity = invoiced,
                RemainingInvoicableQuantity = remaining,
                Unit = "طن",
                TransactionDate = t.TransactionDate,
                Destination = !string.IsNullOrWhiteSpace(t.Destination) ? t.Destination : (prj.ProjectName ?? "-"),
                ProjectCodeId = prj.Id,
                ProjectCodeValue = prj.Code,
                ProjectName = prj.ProjectName ?? string.Empty,
                CarNumber = t.CarNumber,
                DriverName = t.DriverName
            });
        }

        return result;
    }

    /// <summary>
    /// Get all project details + eligible un-invoiced Transportation transactions for a Project Code.
    /// Used by the Project-Code-only Invoice Creation workflow.
    /// </summary>
    [HttpGet("invoices/project-data")]
    [ProducesResponseType(typeof(ApiResponse<ProjectInvoiceDataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectInvoiceData([FromQuery] int projectCodeId)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بالوصول لبيانات فواتير المشاريع."));
        }

        if (projectCodeId <= 0)
        {
            return BadRequest(ApiResponse.Fail("يرجى اختيار كود المشروع أولاً."));
        }

        try
        {
            var prj = await _context.ProjectCodes
                .AsNoTracking()
                .Include(p => p.StorageClientDestination!)
                    .ThenInclude(d => d.StorageClient)
                .FirstOrDefaultAsync(p => p.Id == projectCodeId);

            if (prj == null)
            {
                return NotFound(ApiResponse.Fail("كود المشروع المحدد غير موجود في قاعدة البيانات."));
            }

            var eligible = await FetchEligibleTransportationsForProjectAsync(prj);

            var linkedClient = prj.StorageClientDestination?.StorageClient;
            if (linkedClient == null && !string.IsNullOrWhiteSpace(prj.ClientName))
            {
                var clientNameTrimmed = prj.ClientName.Trim();
                linkedClient = await _context.StorageClients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Name == clientNameTrimmed);
            }

            string resolvedClientName = !string.IsNullOrWhiteSpace(linkedClient?.Name)
                ? linkedClient.Name
                : (!string.IsNullOrWhiteSpace(prj.ClientName) ? prj.ClientName : "عميل فيس ستون");

            string? resolvedAddress = !string.IsNullOrWhiteSpace(prj.StorageClientDestination?.DestinationName)
                ? prj.StorageClientDestination.DestinationName
                : eligible.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Destination))?.Destination;

            var payload = new ProjectInvoiceDataDto
            {
                ProjectCodeId = prj.Id,
                Code = prj.Code,
                ProjectName = prj.ProjectName,
                ClientName = resolvedClientName,
                Description = prj.Description,
                CompanyId = linkedClient?.Id,
                CompanyName = resolvedClientName,
                CompanyPhone = linkedClient?.PhoneNumber,
                CompanyAddress = resolvedAddress,
                HasEligibleRecords = eligible.Count > 0,
                Message = eligible.Count > 0
                    ? null
                    : $"لا توجد معاملات نقل مؤهلة أو متبقية للفوترة تابعة لكود المشروع ({prj.Code}) حالياً.",
                EligibleTransportations = eligible
            };

            return Ok(ApiResponse<ProjectInvoiceDataDto>.Ok(payload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching project invoice data for ProjectCodeId={ProjectCodeId}", projectCodeId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء استرجاع بيانات المشروع للفوترة."));
        }
    }

    /// <summary>
    /// Retrieve eligible Transportation transactions for a specific Project Code.
    /// Calculates previously invoiced quantities and remaining uninvoiced quantities.
    /// Excludes fully invoiced transactions.
    /// </summary>
    [HttpGet("invoices/eligible-transportations")]
    [ProducesResponseType(typeof(ApiResponse<List<EligibleTransportationTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleTransportations([FromQuery] int projectCodeId)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بالوصول لمعاملات النقل."));
        }

        if (projectCodeId <= 0)
        {
            return BadRequest(ApiResponse.Fail("كود المشروع مطلوب."));
        }

        try
        {
            var prj = await _context.ProjectCodes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectCodeId);
            if (prj == null)
            {
                return NotFound(ApiResponse.Fail("كود المشروع المحدد غير موجود."));
            }

            var result = await FetchEligibleTransportationsForProjectAsync(prj);
            var message = result.Count == 0 ? "لا توجد معاملات نقل مسجلة لهذا المشروع حالياً." : null;
            return Ok(ApiResponse<List<EligibleTransportationTransactionDto>>.Ok(result, message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching eligible transportation transactions for project code: {ProjectCodeId}", projectCodeId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء جلب معاملات النقل المؤهلة للفوترة."));
        }
    }

    /// <summary>
    /// Get single Invoice by ID with all details.
    /// </summary>
    [HttpGet("invoices/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoiceById(int id)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بالوصول لبيانات الفاتورة."));
        }

        var inv = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .Include(i => i.ProjectCode)
            .Include(i => i.Company)
            .Include(i => i.PurchaseOrder)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (inv == null)
        {
            return NotFound(ApiResponse.Fail("الفاتورة المطلوبة غير موجودة."));
        }

        var dto = new InvoiceDto
        {
            Id = inv.Id,
            InvoiceNumber = inv.InvoiceNumber,
            InvoiceDate = inv.InvoiceDate,
            ProjectCodeId = inv.ProjectCodeId,
            ProjectCodeValue = inv.ProjectCodeValue,
            ProjectName = inv.ProjectName,
            RelatedEntityType = inv.RelatedEntityType,
            PurchaseOrderId = inv.PurchaseOrderId,
            PONumber = inv.PONumber,
            SupplierId = inv.SupplierId,
            SupplierName = inv.SupplierName,
            MaterialName = inv.MaterialName,
            OrderedQuantity = inv.OrderedQuantity,
            InvoicedQuantity = inv.InvoicedQuantity ?? inv.Items.Sum(it => it.Quantity),
            PoUnitPrice = inv.PoUnitPrice,
            InvoiceUnitPrice = inv.InvoiceUnitPrice,
            PriceChangedBy = inv.PriceChangedBy,
            PriceChangedAt = inv.PriceChangedAt,
            CompanyId = inv.CompanyId,
            CompanyName = inv.CompanyName,
            CompanyAddress = inv.CompanyAddress,
            CompanyTaxNumber = inv.CompanyTaxNumber,
            CompanyPhone = inv.CompanyPhone,
            Subtotal = inv.Subtotal,
            ShippingPrice = inv.ShippingPrice,
            TaxRate = inv.TaxRate,
            TaxAmount = inv.TaxAmount,
            DiscountAmount = inv.DiscountAmount,
            TotalAmount = inv.TotalAmount,
            Status = inv.Status,
            IsPrinted = inv.IsPrinted,
            PrintedAt = inv.PrintedAt,
            Notes = inv.Notes,
            CreatedBy = inv.CreatedBy,
            CreatedAt = inv.CreatedAt,
            UpdatedAt = inv.UpdatedAt,
            Items = inv.Items.Select(item => new InvoiceItemDto
            {
                Id = item.Id,
                InvoiceId = item.InvoiceId,
                TransportationId = item.TransportationId,
                TransportationTransactionNumber = item.TransportationTransactionNumber,
                PurchaseOrderId = item.PurchaseOrderId,
                PONumber = item.PONumber,
                WarehouseExportNumber = item.WarehouseExportNumber,
                ProductName = item.ProductName,
                ReferenceNumber = item.ReferenceNumber,
                BatchNumber = item.BatchNumber,
                ProductionOrderNumber = item.ProductionOrderNumber,
                Quantity = item.Quantity,
                Unit = item.Unit,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal,
                Destination = item.Destination,
                TransactionDate = item.TransactionDate
            }).ToList()
        };

        return Ok(ApiResponse<InvoiceDto>.Ok(dto));
    }

    /// <summary>
    /// Create new Invoice (Draft or Issued) with concurrency protection.
    /// Supports direct Purchase Order selection with partial invoicing, editable unit price,
    /// shipping price, dynamic calculations, and supplier financial synchronization.
    /// </summary>
    [HttpPost("invoices")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceDto dto)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بإنشاء أو إصدار الفواتير."));
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("بيانات الفاتورة غير مكتملة.", errors));
        }

        var currentUserName = User.FindFirst(ClaimTypes.Name)?.Value ?? "المدير المالي";
        var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? currentUserId = Guid.TryParse(currentUserIdStr, out var gid) ? gid : null;

        // Generate unique invoice number: FS-INV-YYYY-XXXX
        var year = DateTime.UtcNow.Year;
        var prefix = $"FS-INV-{year}-";
        var maxExisting = await _context.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .Select(i => i.InvoiceNumber)
            .ToListAsync();

        int nextSeq = 1;
        if (maxExisting.Count > 0)
        {
            var seqs = maxExisting
                .Select(n => int.TryParse(n.Replace(prefix, string.Empty), out var s) ? s : 0)
                .ToList();
            nextSeq = seqs.Max() + 1;
        }
        var invoiceNumber = $"{prefix}{nextSeq:D4}";

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // ==========================================================
            // BRANCH 1: INVOICE LINKED TO PURCHASE ORDER (Legacy/PO Direct API)
            // ==========================================================
            if ((dto.RelatedEntityType.Equals("PURCHASE_ORDER", StringComparison.OrdinalIgnoreCase) && !dto.ProjectCodeId.HasValue) || dto.PurchaseOrderId.HasValue)
            {
                var poId = dto.PurchaseOrderId ?? 0;
                var po = await _context.PurchaseOrders
                    .Include(p => p.SupplierEntity)
                    .FirstOrDefaultAsync(p => p.Id == poId || (!string.IsNullOrWhiteSpace(dto.PONumber) && p.PONumber == dto.PONumber.Trim()));

                if (po == null)
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("أمر الشراء المحدد غير موجود في النظام."));
                }

                // Calculate live previously invoiced quantity from all existing ISSUED invoices
                var previouslyInvoicedList = await _context.Invoices
                    .AsNoTracking()
                    .Where(i => i.PurchaseOrderId == po.Id && i.Status == "ISSUED")
                    .Select(i => i.InvoicedQuantity ?? (i.Items.Any() ? i.Items.Sum(it => it.Quantity) : 0m))
                    .ToListAsync();
                var previouslyInvoiced = previouslyInvoicedList.Sum();

                var remainingOrderQty = Math.Max(0m, po.Quantity - previouslyInvoiced);

                // Determine quantity to invoice
                decimal qtyToBill = (dto.InvoicedQuantity.HasValue && dto.InvoicedQuantity.Value > 0)
                    ? dto.InvoicedQuantity.Value
                    : (dto.Items != null && dto.Items.Count > 0 ? dto.Items.Sum(x => x.Quantity ?? 0m) : 0m);

                if (qtyToBill <= 0.0001m)
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("يجب تحديد كمية صالحة للفوترة أكبر من الصفر."));
                }

                // Strict validation: Invoiced Quantity <= Remaining Quantity
                if (qtyToBill > remainingOrderQty + 0.0001m)
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"الكمية المطلوبة ({qtyToBill:N3} {po.Unit ?? "طن"}) تتجاوز الكمية المتبقية للتوريد في أمر الشراء ({remainingOrderQty:N3} {po.Unit ?? "طن"})."));
                }

                // Unit Price logic: Default to PO price, allow Finance Manager to edit
                decimal poTonPrice = po.TonPrice > 0 ? po.TonPrice : (po.PricePerTon > 0 ? po.PricePerTon : 0m);
                decimal invoiceUnitPrice = (dto.InvoiceUnitPrice.HasValue && dto.InvoiceUnitPrice.Value > 0)
                    ? dto.InvoiceUnitPrice.Value
                    : (dto.Items != null && dto.Items.Count > 0 && dto.Items[0].UnitPrice > 0 ? dto.Items[0].UnitPrice : poTonPrice);

                if (invoiceUnitPrice < 0)
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail("سعر الوحدة يجب أن يكون رقماً موجباً."));
                }

                string? priceChangedBy = null;
                DateTime? priceChangedAt = null;
                if (Math.Abs(invoiceUnitPrice - poTonPrice) > 0.001m)
                {
                    priceChangedBy = currentUserName;
                    priceChangedAt = DateTime.UtcNow;
                }

                // Calculations
                decimal shippingPrice = Math.Max(0m, dto.ShippingPrice);
                decimal subtotal = Math.Round(qtyToBill * invoiceUnitPrice, 3);
                decimal taxRate = Math.Max(0m, dto.TaxRate);
                decimal taxAmount = Math.Round(subtotal * (taxRate / 100m), 3);
                decimal discount = Math.Max(0m, dto.DiscountAmount);
                decimal totalAmount = Math.Round(Math.Max(0m, subtotal + shippingPrice + taxAmount - discount), 3);

                // Optional Project Code
                ProjectCode? prj = null;
                if (dto.ProjectCodeId.HasValue && dto.ProjectCodeId.Value > 0)
                {
                    prj = await _context.ProjectCodes.FirstOrDefaultAsync(p => p.Id == dto.ProjectCodeId.Value);
                }

                // Resolve Supplier details
                int? supplierId = po.SupplierId ?? dto.SupplierId;
                string supplierName = !string.IsNullOrWhiteSpace(dto.SupplierName) 
                    ? dto.SupplierName.Trim() 
                    : (!string.IsNullOrWhiteSpace(po.Supplier) ? po.Supplier : "مورد فيس ستون");
                string materialName = !string.IsNullOrWhiteSpace(dto.MaterialName)
                    ? dto.MaterialName.Trim()
                    : (!string.IsNullOrWhiteSpace(po.MaterialName) ? po.MaterialName : "مواد خام");

                var itemEntities = new List<InvoiceItem>();
                if (dto.Items != null && dto.Items.Any(x => x.TransportationId.HasValue))
                {
                    foreach (var it in dto.Items.Where(x => x.TransportationId.HasValue))
                    {
                        var t = await _context.Transportations.FirstOrDefaultAsync(tr => tr.Id == it.TransportationId!.Value);
                        itemEntities.Add(new InvoiceItem
                        {
                            TransportationId = t?.Id,
                            TransportationTransactionNumber = t?.TransactionNumber ?? 0,
                            WarehouseExportNumber = t?.ExportTransactionNumber ?? (t != null && t.StorageOperationId.HasValue ? $"#{t.StorageOperationId.Value}" : null),
                            ProductName = materialName,
                            PurchaseOrderId = po.Id,
                            PONumber = po.PONumber,
                            Quantity = it.Quantity ?? qtyToBill,
                            Unit = !string.IsNullOrWhiteSpace(po.Unit) ? po.Unit : "طن",
                            UnitPrice = it.UnitPrice > 0 ? it.UnitPrice : invoiceUnitPrice,
                            LineTotal = Math.Round((it.Quantity ?? qtyToBill) * (it.UnitPrice > 0 ? it.UnitPrice : invoiceUnitPrice), 3),
                            Destination = po.Destination,
                            TransactionDate = t?.TransactionDate ?? po.OrderDate
                        });
                    }
                }
                else
                {
                    itemEntities.Add(new InvoiceItem
                    {
                        PurchaseOrderId = po.Id,
                        PONumber = po.PONumber,
                        ProductName = materialName,
                        Quantity = qtyToBill,
                        Unit = !string.IsNullOrWhiteSpace(po.Unit) ? po.Unit : "طن",
                        UnitPrice = invoiceUnitPrice,
                        LineTotal = subtotal,
                        Destination = po.Destination,
                        TransactionDate = po.OrderDate
                    });
                }

                var invoice = new Invoice
                {
                    InvoiceNumber = invoiceNumber,
                    InvoiceDate = DateTime.UtcNow,
                    ProjectCodeId = prj?.Id,
                    ProjectCodeValue = prj?.Code ?? string.Empty,
                    ProjectName = prj?.ProjectName ?? string.Empty,
                    RelatedEntityType = "PURCHASE_ORDER",
                    PurchaseOrderId = po.Id,
                    PONumber = po.PONumber,
                    SupplierId = supplierId,
                    SupplierName = supplierName,
                    MaterialName = materialName,
                    OrderedQuantity = po.Quantity,
                    InvoicedQuantity = qtyToBill,
                    PoUnitPrice = poTonPrice,
                    InvoiceUnitPrice = invoiceUnitPrice,
                    PriceChangedBy = priceChangedBy,
                    PriceChangedAt = priceChangedAt,
                    CompanyName = supplierName,
                    Subtotal = subtotal,
                    ShippingPrice = shippingPrice,
                    TaxRate = taxRate,
                    TaxAmount = taxAmount,
                    DiscountAmount = discount,
                    TotalAmount = totalAmount,
                    Status = dto.Status.ToUpperInvariant() == "DRAFT" ? "DRAFT" : "ISSUED",
                    IsPrinted = false,
                    Notes = dto.Notes?.Trim(),
                    CreatedBy = currentUserName,
                    CreatedByUserId = currentUserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Items = itemEntities
                };

                _context.Invoices.Add(invoice);

                // If invoice is ISSUED, update Purchase Order delivered/remaining quantity and supplier balance
                if (invoice.Status == "ISSUED")
                {
                    po.DeliveredQuantity = previouslyInvoiced + qtyToBill;
                    po.RemainingQuantity = Math.Max(0m, po.Quantity - po.DeliveredQuantity);
                    if (po.RemainingQuantity <= 0.0001m)
                    {
                        po.Status = "COMPLETED";
                    }
                    po.UpdatedAt = DateTime.UtcNow;

                    // Update Supplier Financial Record
                    if (supplierId.HasValue)
                    {
                        var sup = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId.Value);
                        if (sup != null)
                        {
                            sup.TotalAmountOwed += totalAmount;
                            sup.RemainingBalance = Math.Round(sup.TotalAmountOwed - sup.TotalAmountPaid, 2);
                            sup.UpdatedAt = DateTime.UtcNow;

                            _context.SupplierFinancialTransactions.Add(new SupplierFinancialTransaction
                            {
                                SupplierId = sup.Id,
                                TransactionType = "AMOUNT_OWED",
                                Amount = totalAmount,
                                TransactionDate = invoice.InvoiceDate,
                                PaymentMethod = "INVOICE",
                                ReferenceNumber = invoice.InvoiceNumber,
                                Notes = $"فاتورة معتمدة رقم {invoice.InvoiceNumber} لأمر الشراء {po.PONumber} ({materialName})",
                                CreatedBy = currentUserName,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("Invoice {InvoiceNumber} created for PO {PO} (Status: {Status}). Total: {Total} EGP.",
                    invoice.InvoiceNumber, po.PONumber, invoice.Status, invoice.TotalAmount);

                return Ok(ApiResponse<InvoiceDto>.Ok(MapToInvoiceDto(invoice), "تم إنشاء الفاتورة بنجاح."));
            }

            // ==========================================================
            // BRANCH 2: INVOICE LINKED TO PROJECT CODE / TRANSPORTATION
            // ==========================================================
            if (!dto.ProjectCodeId.HasValue || dto.ProjectCodeId.Value <= 0)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse.Fail("يرجى اختيار كود المشروع أولاً لإنشاء الفاتورة."));
            }

            if (dto.Items == null || dto.Items.Count == 0)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse.Fail("لا توجد بنود نقل مؤهلة للفوترة لهذا المشروع. تعذر إنشاء الفاتورة."));
            }

            var prjCode = await _context.ProjectCodes
                .Include(p => p.StorageClientDestination!)
                    .ThenInclude(d => d.StorageClient)
                .FirstOrDefaultAsync(p => p.Id == dto.ProjectCodeId.Value);

            if (prjCode == null)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse.Fail("كود المشروع المحدد غير موجود في النظام."));
            }

            var linkedClient = prjCode.StorageClientDestination?.StorageClient;
            if (linkedClient == null && !string.IsNullOrWhiteSpace(prjCode.ClientName))
            {
                var clientNameTrimmed = prjCode.ClientName.Trim();
                linkedClient = await _context.StorageClients
                    .FirstOrDefaultAsync(c => c.Name == clientNameTrimmed);
            }

            string companyName = string.Empty;
            string? companyPhone = null;
            string? companyAddress = null;
            string? companyTaxNum = null;
            int? resolvedCompanyId = null;

            if (dto.CompanyId.HasValue && dto.CompanyId.Value > 0)
            {
                var cl = await _context.StorageClients.FirstOrDefaultAsync(c => c.Id == dto.CompanyId.Value);
                if (cl != null)
                {
                    resolvedCompanyId = cl.Id;
                    companyName = cl.Name;
                    companyPhone = cl.PhoneNumber;
                }
            }
            else if (linkedClient != null)
            {
                resolvedCompanyId = linkedClient.Id;
                companyName = linkedClient.Name;
                companyPhone = linkedClient.PhoneNumber;
            }

            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                companyName = dto.CompanyName.Trim();
            }
            else if (string.IsNullOrWhiteSpace(companyName))
            {
                companyName = !string.IsNullOrWhiteSpace(prjCode.ClientName) ? prjCode.ClientName : "عميل فيس ستون";
            }

            companyPhone = !string.IsNullOrWhiteSpace(dto.CompanyPhone) ? dto.CompanyPhone.Trim() : companyPhone;
            companyAddress = !string.IsNullOrWhiteSpace(dto.CompanyAddress)
                ? dto.CompanyAddress.Trim()
                : (prjCode.StorageClientDestination?.DestinationName ?? "جمهورية مصر العربية");
            companyTaxNum = dto.CompanyTaxNumber?.Trim();

            var itemEntitiesCompany = new List<InvoiceItem>();
            decimal subtotalComp = 0m;

            var transIds = dto.Items.Where(x => x.TransportationId.HasValue).Select(x => x.TransportationId!.Value).Distinct().ToList();
            var transportations = await _context.Transportations
                .Where(t => transIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t);

            var liveInvoicedMap = await _context.InvoiceItems
                .Where(ii => ii.TransportationId.HasValue && transIds.Contains(ii.TransportationId.Value) && ii.Invoice.Status != "CANCELLED")
                .GroupBy(ii => ii.TransportationId!.Value)
                .Select(g => new { TransportationId = g.Key, InvoicedQty = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.TransportationId, x => x.InvoicedQty);

            var seenTransIds = new HashSet<int>();
            var normalizedPrjCode = (prjCode.Code ?? string.Empty).Trim();

            foreach (var itemDto in dto.Items)
            {
                if (!itemDto.TransportationId.HasValue || !transportations.TryGetValue(itemDto.TransportationId.Value, out var t))
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"معاملة النقل رقم {itemDto.TransportationId} غير موجودة."));
                }

                if (!seenTransIds.Add(t.Id))
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"تم تكرار معاملة النقل #{t.TransactionNumber} في نفس الفاتورة."));
                }

                bool belongsToProject = t.ProjectCodeId == prjCode.Id
                    || (t.ProjectCodeId == null && !string.IsNullOrWhiteSpace(t.ProjectCodeValue) && string.Equals(t.ProjectCodeValue.Trim(), normalizedPrjCode, StringComparison.OrdinalIgnoreCase));
                if (!belongsToProject)
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"معاملة النقل #{t.TransactionNumber} لا تنتمي إلى كود المشروع المحدد ({prjCode.Code})."));
                }

                if (string.Equals(t.ApprovalStatus, "REJECTED", StringComparison.OrdinalIgnoreCase))
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"معاملة النقل #{t.TransactionNumber} مرفوضة ولا يمكن فوترتها."));
                }

                if (itemDto.UnitPrice <= 0)
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"سعر الوحدة للمعاملة #{t.TransactionNumber} يجب أن يكون رقماً موجباً أكبر من الصفر."));
                }

                var previouslyInvoiced = liveInvoicedMap.TryGetValue(t.Id, out var inv) ? inv : 0m;
                var remainingInvoicable = Math.Max(0m, t.NetWeight - previouslyInvoiced);

                if (remainingInvoicable <= 0.0001m)
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"معاملة النقل #{t.TransactionNumber} تم فوترة كامل كميتها مسبقاً ({t.NetWeight:N3} طن). لا يمكن تكرار فوترتها."));
                }

                decimal qtyToBill = (itemDto.Quantity.HasValue && itemDto.Quantity.Value > 0)
                    ? itemDto.Quantity.Value
                    : remainingInvoicable;

                if (qtyToBill > remainingInvoicable + 0.0001m)
                {
                    await dbTransaction.RollbackAsync();
                    return BadRequest(ApiResponse.Fail($"الكمية المطلوبة ({qtyToBill:N3} طن) للمعاملة #{t.TransactionNumber} تتجاوز الرصيد المتاح للفوترة ({remainingInvoicable:N3} طن)."));
                }

                decimal lineTotal = Math.Round(qtyToBill * itemDto.UnitPrice, 3);
                subtotalComp += lineTotal;

                itemEntitiesCompany.Add(new InvoiceItem
                {
                    TransportationId = t.Id,
                    TransportationTransactionNumber = t.TransactionNumber,
                    WarehouseExportNumber = t.ExportTransactionNumber ?? (t.StorageOperationId.HasValue ? $"#{t.StorageOperationId.Value}" : null),
                    ProductName = !string.IsNullOrWhiteSpace(t.MaterialType) ? t.MaterialType : "خرسانة / مادة توريد",
                    ReferenceNumber = t.ManufactureNumber ?? (t.StorageOperationId.HasValue ? $"EXP-#{t.StorageOperationId}" : t.PONumber),
                    BatchNumber = t.BatchNumber,
                    ProductionOrderNumber = t.ProductionOrderId.HasValue ? $"PO-{t.ProductionOrderId}" : null,
                    Quantity = qtyToBill,
                    Unit = "طن",
                    UnitPrice = Math.Round(itemDto.UnitPrice, 3),
                    LineTotal = lineTotal,
                    Destination = t.Destination,
                    TransactionDate = t.TransactionDate
                });
            }

            decimal shippingPriceComp = Math.Max(0m, dto.ShippingPrice);
            decimal taxRateComp = Math.Max(0m, dto.TaxRate);
            decimal taxAmountComp = Math.Round(subtotalComp * (taxRateComp / 100m), 3);
            decimal discountComp = Math.Max(0m, dto.DiscountAmount);
            decimal totalAmountComp = Math.Round(Math.Max(0m, subtotalComp + shippingPriceComp + taxAmountComp - discountComp), 3);

            var companyInvoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                InvoiceDate = DateTime.UtcNow,
                ProjectCodeId = prjCode.Id,
                ProjectCodeValue = prjCode.Code,
                ProjectName = prjCode.ProjectName,
                RelatedEntityType = "COMPANY",
                CompanyId = resolvedCompanyId,
                CompanyName = companyName,
                CompanyAddress = companyAddress,
                CompanyTaxNumber = companyTaxNum,
                CompanyPhone = companyPhone,
                InvoicedQuantity = itemEntitiesCompany.Sum(it => it.Quantity),
                Subtotal = subtotalComp,
                ShippingPrice = shippingPriceComp,
                TaxRate = taxRateComp,
                TaxAmount = taxAmountComp,
                DiscountAmount = discountComp,
                TotalAmount = totalAmountComp,
                Status = dto.Status.ToUpperInvariant() == "DRAFT" ? "DRAFT" : "ISSUED",
                IsPrinted = false,
                Notes = dto.Notes?.Trim(),
                CreatedBy = currentUserName,
                CreatedByUserId = currentUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = itemEntitiesCompany
            };

            _context.Invoices.Add(companyInvoice);
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Company Invoice {InvoiceNumber} created successfully. Total: {Total} EGP.",
                companyInvoice.InvoiceNumber, companyInvoice.TotalAmount);

            return Ok(ApiResponse<InvoiceDto>.Ok(MapToInvoiceDto(companyInvoice), "تم إنشاء الفاتورة وحفظها بنجاح."));
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error creating invoice.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("حدث خطأ أثناء حفظ الفاتورة في قاعدة البيانات."));
        }
    }

    private static InvoiceDto MapToInvoiceDto(Invoice inv)
    {
        return new InvoiceDto
        {
            Id = inv.Id,
            InvoiceNumber = inv.InvoiceNumber,
            InvoiceDate = inv.InvoiceDate,
            ProjectCodeId = inv.ProjectCodeId,
            ProjectCodeValue = inv.ProjectCodeValue,
            ProjectName = inv.ProjectName,
            RelatedEntityType = inv.RelatedEntityType,
            PurchaseOrderId = inv.PurchaseOrderId,
            PONumber = inv.PONumber,
            SupplierId = inv.SupplierId,
            SupplierName = inv.SupplierName,
            MaterialName = inv.MaterialName,
            OrderedQuantity = inv.OrderedQuantity,
            InvoicedQuantity = inv.InvoicedQuantity ?? inv.Items.Sum(it => it.Quantity),
            PoUnitPrice = inv.PoUnitPrice,
            InvoiceUnitPrice = inv.InvoiceUnitPrice,
            PriceChangedBy = inv.PriceChangedBy,
            PriceChangedAt = inv.PriceChangedAt,
            CompanyId = inv.CompanyId,
            CompanyName = inv.CompanyName,
            CompanyAddress = inv.CompanyAddress,
            CompanyTaxNumber = inv.CompanyTaxNumber,
            CompanyPhone = inv.CompanyPhone,
            Subtotal = inv.Subtotal,
            ShippingPrice = inv.ShippingPrice,
            TaxRate = inv.TaxRate,
            TaxAmount = inv.TaxAmount,
            DiscountAmount = inv.DiscountAmount,
            TotalAmount = inv.TotalAmount,
            Status = inv.Status,
            IsPrinted = inv.IsPrinted,
            PrintedAt = inv.PrintedAt,
            Notes = inv.Notes,
            CreatedBy = inv.CreatedBy,
            CreatedAt = inv.CreatedAt,
            UpdatedAt = inv.UpdatedAt,
            Items = inv.Items.Select(item => new InvoiceItemDto
            {
                Id = item.Id,
                InvoiceId = item.InvoiceId,
                TransportationId = item.TransportationId,
                TransportationTransactionNumber = item.TransportationTransactionNumber,
                PurchaseOrderId = item.PurchaseOrderId,
                PONumber = item.PONumber,
                WarehouseExportNumber = item.WarehouseExportNumber,
                ProductName = item.ProductName,
                ReferenceNumber = item.ReferenceNumber,
                BatchNumber = item.BatchNumber,
                ProductionOrderNumber = item.ProductionOrderNumber,
                Quantity = item.Quantity,
                Unit = item.Unit,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal,
                Destination = item.Destination,
                TransactionDate = item.TransactionDate
            }).ToList()
        };
    }

    /// <summary>
    /// Update Draft Invoice (company details, line item unit prices/quantities, shipping, tax, discount, notes, or issue).
    /// </summary>
    [HttpPut("invoices/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateInvoice(int id, [FromBody] UpdateInvoiceDto dto)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بتعديل الفواتير."));
        }

        var inv = await _context.Invoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id);
        if (inv == null) return NotFound(ApiResponse.Fail("الفاتورة غير موجودة."));

        if (inv.Status != "DRAFT")
        {
            return BadRequest(ApiResponse.Fail("لا يمكن تعديل الفاتورة إلا إذا كانت في حالة 'مسودة'. الفواتير المعتمدة والمصدرة لا يمكن تعديلها للحفاظ على السجل المالي التاريخي."));
        }

        if (!string.IsNullOrWhiteSpace(dto.CompanyName))
        {
            inv.CompanyName = dto.CompanyName.Trim();
        }
        if (dto.CompanyPhone != null)
        {
            inv.CompanyPhone = string.IsNullOrWhiteSpace(dto.CompanyPhone) ? null : dto.CompanyPhone.Trim();
        }
        if (dto.CompanyAddress != null)
        {
            inv.CompanyAddress = string.IsNullOrWhiteSpace(dto.CompanyAddress) ? null : dto.CompanyAddress.Trim();
        }
        if (dto.CompanyTaxNumber != null)
        {
            inv.CompanyTaxNumber = string.IsNullOrWhiteSpace(dto.CompanyTaxNumber) ? null : dto.CompanyTaxNumber.Trim();
        }

        // Update item pricing & optional quantities for project/transportation invoices
        if (dto.Items != null && dto.Items.Count > 0 && !inv.PurchaseOrderId.HasValue)
        {
            var transIds = inv.Items.Where(x => x.TransportationId.HasValue).Select(x => x.TransportationId!.Value).Distinct().ToList();
            var transWeights = transIds.Count > 0
                ? await _context.Transportations.AsNoTracking()
                    .Where(t => transIds.Contains(t.Id))
                    .ToDictionaryAsync(t => t.Id, t => t.NetWeight)
                : new Dictionary<int, decimal>();

            var otherInvoicedMap = transIds.Count > 0
                ? await _context.InvoiceItems.AsNoTracking()
                    .Where(ii => ii.TransportationId.HasValue && transIds.Contains(ii.TransportationId.Value) && ii.InvoiceId != inv.Id && ii.Invoice.Status != "CANCELLED")
                    .GroupBy(ii => ii.TransportationId!.Value)
                    .Select(g => new { TransportationId = g.Key, InvoicedQty = g.Sum(x => x.Quantity) })
                    .ToDictionaryAsync(x => x.TransportationId, x => x.InvoicedQty)
                : new Dictionary<int, decimal>();

            foreach (var itemDto in dto.Items)
            {
                var existingItem = itemDto.TransportationId.HasValue
                    ? inv.Items.FirstOrDefault(x => x.TransportationId == itemDto.TransportationId.Value)
                    : null;

                if (existingItem == null)
                {
                    return BadRequest(ApiResponse.Fail("لا يمكن إضافة بنود خارج بنود كود المشروع المسجلة في المسودة."));
                }

                if (itemDto.UnitPrice <= 0)
                {
                    return BadRequest(ApiResponse.Fail($"سعر الوحدة للبند #{existingItem.TransportationTransactionNumber} يجب أن يكون أكبر من الصفر."));
                }

                if (itemDto.Quantity.HasValue && itemDto.Quantity.Value > 0 && existingItem.TransportationId.HasValue)
                {
                    var netWeight = transWeights.TryGetValue(existingItem.TransportationId.Value, out var w) ? w : existingItem.Quantity;
                    var otherInvoiced = otherInvoicedMap.TryGetValue(existingItem.TransportationId.Value, out var o) ? o : 0m;
                    var maxAllowed = Math.Max(0m, netWeight - otherInvoiced);
                    if (itemDto.Quantity.Value > maxAllowed + 0.0001m)
                    {
                        return BadRequest(ApiResponse.Fail($"الكمية المطلوبة ({itemDto.Quantity.Value:N3} طن) تتجاوز المتاح للفوترة ({maxAllowed:N3} طن)."));
                    }
                    existingItem.Quantity = Math.Round(itemDto.Quantity.Value, 3);
                }

                existingItem.UnitPrice = Math.Round(itemDto.UnitPrice, 3);
                existingItem.LineTotal = Math.Round(existingItem.Quantity * existingItem.UnitPrice, 3);
            }

            inv.Subtotal = inv.Items.Sum(x => x.LineTotal);
            inv.InvoicedQuantity = inv.Items.Sum(x => x.Quantity);
        }

        inv.TaxRate = Math.Max(0m, dto.TaxRate);
        inv.DiscountAmount = Math.Max(0m, dto.DiscountAmount);
        inv.ShippingPrice = Math.Max(0m, dto.ShippingPrice);
        inv.Notes = dto.Notes?.Trim();
        inv.UpdatedAt = DateTime.UtcNow;

        // Recalculate totals
        inv.TaxAmount = Math.Round(inv.Subtotal * (inv.TaxRate / 100m), 3);
        inv.TotalAmount = Math.Round(Math.Max(0m, inv.Subtotal + inv.ShippingPrice + inv.TaxAmount - inv.DiscountAmount), 3);

        if (string.Equals(dto.Status, "ISSUED", StringComparison.OrdinalIgnoreCase))
        {
            inv.Status = "ISSUED";

            // If PO invoice, update PO delivered quantity and supplier balance
            if (inv.PurchaseOrderId.HasValue)
            {
                var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == inv.PurchaseOrderId.Value);
                if (po != null)
                {
                    var prevInvoicedList = await _context.Invoices
                        .AsNoTracking()
                        .Where(i => i.PurchaseOrderId == po.Id && i.Status == "ISSUED" && i.Id != inv.Id)
                        .Select(i => i.InvoicedQuantity ?? (i.Items.Any() ? i.Items.Sum(it => it.Quantity) : 0m))
                        .ToListAsync();
                    var previouslyInvoiced = prevInvoicedList.Sum();

                    var invoiceQty = inv.InvoicedQuantity ?? inv.Items.Sum(it => it.Quantity);
                    po.DeliveredQuantity = previouslyInvoiced + invoiceQty;
                    po.RemainingQuantity = Math.Max(0m, po.Quantity - po.DeliveredQuantity);
                    if (po.RemainingQuantity <= 0.0001m) po.Status = "COMPLETED";
                    po.UpdatedAt = DateTime.UtcNow;

                    var supId = inv.SupplierId ?? po.SupplierId;
                    if (supId.HasValue)
                    {
                        var sup = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == supId.Value);
                        if (sup != null)
                        {
                            sup.TotalAmountOwed += inv.TotalAmount;
                            sup.RemainingBalance = Math.Round(sup.TotalAmountOwed - sup.TotalAmountPaid, 2);
                            sup.UpdatedAt = DateTime.UtcNow;

                            _context.SupplierFinancialTransactions.Add(new SupplierFinancialTransaction
                            {
                                SupplierId = sup.Id,
                                TransactionType = "AMOUNT_OWED",
                                Amount = inv.TotalAmount,
                                TransactionDate = DateTime.UtcNow,
                                PaymentMethod = "INVOICE",
                                ReferenceNumber = inv.InvoiceNumber,
                                Notes = $"اعتماد فاتورة مسودة رقم {inv.InvoiceNumber} لأمر الشراء {po.PONumber}",
                                CreatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "المدير المالي",
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponse<InvoiceDto>.Ok(MapToInvoiceDto(inv), "تم تحديث مسودة الفاتورة بنجاح."));
    }

    /// <summary>
    /// Issue / Finalize a Draft Invoice.
    /// Only finalized / approved invoices count toward Delivered Quantity and Supplier Debt.
    /// </summary>
    [HttpPost("invoices/{id:int}/issue")]
    public async Task<IActionResult> IssueDraftInvoice(int id)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك باعتماد الفواتير."));
        }

        var inv = await _context.Invoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id);
        if (inv == null) return NotFound(ApiResponse.Fail("الفاتورة غير موجودة."));

        if (inv.Status == "ISSUED")
        {
            return BadRequest(ApiResponse.Fail("الفاتورة معتمدة ومصدرة بالفعل مسبقاً."));
        }
        if (inv.Status == "CANCELLED")
        {
            return BadRequest(ApiResponse.Fail("لا يمكن اعتماد فاتورة ملغاة."));
        }

        inv.Status = "ISSUED";
        inv.UpdatedAt = DateTime.UtcNow;

        if (inv.PurchaseOrderId.HasValue)
        {
            var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == inv.PurchaseOrderId.Value);
            if (po != null)
            {
                var prevInvoicedList = await _context.Invoices
                    .AsNoTracking()
                    .Where(i => i.PurchaseOrderId == po.Id && i.Status == "ISSUED" && i.Id != inv.Id)
                    .Select(i => i.InvoicedQuantity ?? (i.Items.Any() ? i.Items.Sum(it => it.Quantity) : 0m))
                    .ToListAsync();
                var previouslyInvoiced = prevInvoicedList.Sum();

                var invoiceQty = inv.InvoicedQuantity ?? inv.Items.Sum(it => it.Quantity);
                po.DeliveredQuantity = previouslyInvoiced + invoiceQty;
                po.RemainingQuantity = Math.Max(0m, po.Quantity - po.DeliveredQuantity);
                if (po.RemainingQuantity <= 0.0001m) po.Status = "COMPLETED";
                po.UpdatedAt = DateTime.UtcNow;

                var supId = inv.SupplierId ?? po.SupplierId;
                if (supId.HasValue)
                {
                    var sup = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == supId.Value);
                    if (sup != null)
                    {
                        sup.TotalAmountOwed += inv.TotalAmount;
                        sup.RemainingBalance = Math.Round(sup.TotalAmountOwed - sup.TotalAmountPaid, 2);
                        sup.UpdatedAt = DateTime.UtcNow;

                        _context.SupplierFinancialTransactions.Add(new SupplierFinancialTransaction
                        {
                            SupplierId = sup.Id,
                            TransactionType = "AMOUNT_OWED",
                            Amount = inv.TotalAmount,
                            TransactionDate = DateTime.UtcNow,
                            PaymentMethod = "INVOICE",
                            ReferenceNumber = inv.InvoiceNumber,
                            Notes = $"اعتماد فاتورة مسودة رقم {inv.InvoiceNumber} لأمر الشراء {po.PONumber}",
                            CreatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "المدير المالي",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponse.Ok("تم اعتماد وإصدار الفاتورة بنجاح."));
    }

    /// <summary>
    /// Cancel an Invoice.
    /// If the invoice was ISSUED, its quantity is deducted from the Delivered Quantity,
    /// restoring the Remaining Quantity of the Purchase Order, and supplier debt is adjusted.
    /// </summary>
    [HttpPost("invoices/{id:int}/cancel")]
    public async Task<IActionResult> CancelInvoice(int id)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك بإلغاء الفواتير."));
        }

        var inv = await _context.Invoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id);
        if (inv == null) return NotFound(ApiResponse.Fail("الفاتورة غير موجودة."));

        if (inv.Status == "CANCELLED")
        {
            return BadRequest(ApiResponse.Fail("الفاتورة ملغاة بالفعل مسبقاً."));
        }

        var wasIssued = inv.Status == "ISSUED";
        inv.Status = "CANCELLED";
        inv.UpdatedAt = DateTime.UtcNow;

        if (wasIssued && inv.PurchaseOrderId.HasValue)
        {
            var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == inv.PurchaseOrderId.Value);
            if (po != null)
            {
                // Recalculate delivered quantity strictly from remaining ISSUED invoices (excluding this one)
                var remainingDeliveredList = await _context.Invoices
                    .AsNoTracking()
                    .Where(i => i.PurchaseOrderId == po.Id && i.Status == "ISSUED" && i.Id != inv.Id)
                    .Select(i => i.InvoicedQuantity ?? (i.Items.Any() ? i.Items.Sum(it => it.Quantity) : 0m))
                    .ToListAsync();
                var remainingDelivered = remainingDeliveredList.Sum();

                po.DeliveredQuantity = remainingDelivered;
                po.RemainingQuantity = Math.Max(0m, po.Quantity - po.DeliveredQuantity);
                if (po.RemainingQuantity > 0.0001m) po.Status = "ACTIVE";
                po.UpdatedAt = DateTime.UtcNow;

                var supId = inv.SupplierId ?? po.SupplierId;
                if (supId.HasValue)
                {
                    var sup = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == supId.Value);
                    if (sup != null)
                    {
                        sup.TotalAmountOwed = Math.Max(0m, sup.TotalAmountOwed - inv.TotalAmount);
                        sup.RemainingBalance = Math.Round(sup.TotalAmountOwed - sup.TotalAmountPaid, 2);
                        sup.UpdatedAt = DateTime.UtcNow;

                        _context.SupplierFinancialTransactions.Add(new SupplierFinancialTransaction
                        {
                            SupplierId = sup.Id,
                            TransactionType = "ADJUSTMENT",
                            Amount = -inv.TotalAmount,
                            TransactionDate = DateTime.UtcNow,
                            PaymentMethod = "INVOICE_CANCEL",
                            ReferenceNumber = inv.InvoiceNumber,
                            Notes = $"إلغاء فاتورة رقم {inv.InvoiceNumber} لأمر الشراء {po.PONumber} - تسوية خصم المستحق",
                            CreatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "المدير المالي",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
        return Ok(ApiResponse.Ok("تم إلغاء الفاتورة بنجاح واستعادة الكميات المتبقية لأمر الشراء وتحديث الحسابات."));
    }

    /// <summary>
    /// Mark Invoice as Printed.
    /// </summary>
    [HttpPost("invoices/{id:int}/mark-printed")]
    public async Task<IActionResult> MarkInvoicePrinted(int id)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasFinanceAccess(role))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("غير مصرح لك."));
        }

        var inv = await _context.Invoices.FindAsync(id);
        if (inv == null) return NotFound(ApiResponse.Fail("الفاتورة غير موجودة."));

        inv.IsPrinted = true;
        inv.PrintedAt = DateTime.UtcNow;
        inv.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse.Ok("تم تسجيل طباعة الفاتورة بنجاح."));
    }
}
