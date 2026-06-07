using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ReceiptExpenseTracker.Models;
using ReceiptExpenseTracker.Services;

namespace ReceiptExpenseTracker.Controllers
{
    public class UploadController : Controller
    {
        private readonly ITransactionService _transactionService;
        private readonly IFileService _fileService;
        private readonly IOcrService _ocrService;
        private readonly ILogger<UploadController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public UploadController(
            ITransactionService transactionService,
            IFileService fileService,
            IOcrService ocrService,
            ILogger<UploadController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _transactionService = transactionService;
            _fileService = fileService;
            _ocrService = ocrService;
            _logger = logger;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new UploadReceiptViewModel
            {
                Items = new List<ReceiptItemViewModel> { new() }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessReceipt(IFormFile receiptImage)
        {
            if (receiptImage == null || receiptImage.Length == 0)
                return Json(new { success = false, message = "Please select a file to upload." });

            if (!_fileService.IsValidImage(receiptImage))
                return Json(new { success = false, message = "Invalid file type. Please upload a JPG, JPEG, or PNG image." });

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(),
                    $"temp_{Guid.NewGuid()}{Path.GetExtension(receiptImage.FileName)}");

                using (var stream = new FileStream(tempPath, FileMode.Create))
                    await receiptImage.CopyToAsync(stream);

                var ocrResult = await _ocrService.ProcessReceiptAsync(tempPath);

                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);

                if (!ocrResult.Success)
                    return Json(new { success = false, message = ocrResult.ErrorMessage ?? "OCR processing failed." });

                var imageData = await GetImageDataAsync(receiptImage);

                return Json(new
                {
                    success = true,
                    storeName = ocrResult.StoreName,
                    transactionDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    totalAmount = ocrResult.TotalAmount ?? 0,
                    items = ocrResult.Items,
                    imageData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receipt");
                return Json(new { success = false, message = "An error occurred while processing the receipt." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromBody] UploadReceiptViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Please fill in all required fields." });

            try
            {
                // Ambil userId dari user yang sedang login
                var userId = _userManager.GetUserId(User);

                var transaction = await _transactionService.CreateTransactionAsync(
                    model.StoreName,
                    model.TransactionDate,
                    model.TotalAmount,
                    null,
                    model.Items,
                    userId);  // ← pass userId

                string? imagePath = null;
                if (model.ReceiptImage != null && model.ReceiptImage.Length > 0)
                {
                    imagePath = await _fileService.SaveReceiptImageAsync(model.ReceiptImage, transaction.Id);

                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        var editItems = model.Items.Select(x => new EditItemViewModel
                        {
                            ItemName = x.ItemName,
                            Price = x.Price,
                            Quantity = x.Quantity
                        }).ToList();

                        await _transactionService.UpdateTransactionAsync(
                            transaction.Id,
                            model.StoreName,
                            model.TransactionDate,
                            model.TotalAmount,
                            editItems,
                            userId!);  // ← tambah userId
                    }
                }

                return Json(new { success = true, id = transaction.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving transaction");
                return Json(new { success = false, message = "An error occurred while saving the transaction." });
            }
        }

        private async Task<string> GetImageDataAsync(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var base64 = Convert.ToBase64String(memoryStream.ToArray());
            return $"data:{file.ContentType};base64,{base64}";
        }
    }
}