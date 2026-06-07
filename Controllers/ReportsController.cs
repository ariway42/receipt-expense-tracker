using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ReceiptExpenseTracker.Models;
using ReceiptExpenseTracker.Services;

namespace ReceiptExpenseTracker.Controllers
{
    public class ReportsController : Controller
    {
        private readonly ITransactionService _transactionService;
        private readonly ILogger<ReportsController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(
            ITransactionService transactionService,
            ILogger<ReportsController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _transactionService = transactionService;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string period = "monthly")
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var report = await _transactionService.GetReportDataAsync(period, userId);
                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading report");
                return View("Error", new ErrorViewModel { Message = "Unable to load reports." });
            }
        }

        public async Task<IActionResult> Export(string period = "monthly")
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var excelData = await _transactionService.ExportSummaryToExcelAsync(period, userId);
                return File(
                    excelData,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{period}_summary_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report");
                return RedirectToAction(nameof(Index), new { period });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetData(string period = "monthly")
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var report = await _transactionService.GetReportDataAsync(period, userId);
                return Json(new
                {
                    success = true,
                    period = report.Period,
                    periodTotal = report.PeriodTotal,
                    periodTransactionCount = report.PeriodTransactionCount,
                    periodAverage = report.PeriodAverage,
                    spendingTrend = report.SpendingTrend,
                    storeSummaries = report.StoreSummaries,
                    monthlyComparisons = report.MonthlyComparisons
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report data");
                return Json(new { success = false, message = "An error occurred while loading report data." });
            }
        }
    }
}