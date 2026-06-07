using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ReceiptExpenseTracker.Models;
using ReceiptExpenseTracker.Services;

namespace ReceiptExpenseTracker.Controllers
{
    public class TransactionsController : Controller
    {
        private readonly ITransactionService _transactionService;
        private readonly IFileService _fileService;
        private readonly ILogger<TransactionsController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public TransactionsController(
            ITransactionService transactionService,
            IFileService fileService,
            ILogger<TransactionsController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _transactionService = transactionService;
            _fileService = fileService;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(
            string? searchTerm, string? store,
            DateTime? dateFrom, DateTime? dateTo,
            decimal? minAmount, decimal? maxAmount,
            int page = 1)
        {
            const int pageSize = 10;
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var model = await _transactionService.GetTransactionsAsync(
                    searchTerm, store, dateFrom, dateTo, minAmount, maxAmount, page, pageSize, userId);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transactions");
                return View("Error", new ErrorViewModel { Message = "Unable to load transactions." });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var transaction = await _transactionService.GetTransactionDetailAsync(id, userId);
                if (transaction == null) return NotFound();
                return View(transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transaction details for ID: {Id}", id);
                return View("Error", new ErrorViewModel { Message = "Unable to load transaction details." });
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var transaction = await _transactionService.GetTransactionDetailAsync(id, userId);
                if (transaction == null) return NotFound();

                var model = new EditTransactionViewModel
                {
                    Id = transaction.Id,
                    StoreName = transaction.StoreName,
                    TransactionDate = transaction.TransactionDate,
                    TotalAmount = transaction.TotalAmount,
                    ReceiptImagePath = transaction.ReceiptImagePath,
                    Items = transaction.Items.Select(i => new EditItemViewModel
                    {
                        Id = i.Id,
                        ItemName = i.ItemName,
                        Price = i.Price,
                        Quantity = i.Quantity
                    }).ToList()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transaction for edit ID: {Id}", id);
                return View("Error", new ErrorViewModel { Message = "Unable to load transaction for editing." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditTransactionViewModel model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid) return View(model);

            try
            {
                var userId = _userManager.GetUserId(User)!;
                await _transactionService.UpdateTransactionAsync(
                    model.Id, model.StoreName, model.TransactionDate,
                    model.TotalAmount, model.Items, userId);

                TempData["SuccessMessage"] = "Transaction updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transaction ID: {Id}", id);
                ModelState.AddModelError("", "An error occurred while updating the transaction.");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var transaction = await _transactionService.GetTransactionDetailAsync(id, userId);
                if (transaction == null)
                    return Json(new { success = false, message = "Transaction not found." });

                await _transactionService.DeleteTransactionAsync(id, userId);

                if (!string.IsNullOrEmpty(transaction.ReceiptImagePath))
                    _fileService.DeleteReceiptImage(transaction.ReceiptImagePath);

                return Json(new { success = true, message = "Transaction deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction ID: {Id}", id);
                return Json(new { success = false, message = "An error occurred while deleting the transaction." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Export(
            string? searchTerm, string? store,
            DateTime? dateFrom, DateTime? dateTo,
            decimal? minAmount, decimal? maxAmount)
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var excelData = await _transactionService.ExportTransactionsToExcelAsync(
                    searchTerm, store, dateFrom, dateTo, minAmount, maxAmount, userId);

                return File(
                    excelData,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"transactions_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting transactions");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetStores()
        {
            var userId = _userManager.GetUserId(User)!;
            var stores = await _transactionService.GetStoresAsync(userId);
            return Json(stores);
        }
    }
}