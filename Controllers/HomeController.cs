using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ReceiptExpenseTracker.Models;
using ReceiptExpenseTracker.Services;

namespace ReceiptExpenseTracker.Controllers
{
    public class HomeController : Controller
    {
        private readonly ITransactionService _transactionService;
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ITransactionService transactionService,
            ILogger<HomeController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _transactionService = transactionService;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = _userManager.GetUserId(User)!;
                var dashboard = await _transactionService.GetDashboardDataAsync(userId);
                return View(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                return View("Error", new ErrorViewModel { Message = "Unable to load dashboard data." });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}