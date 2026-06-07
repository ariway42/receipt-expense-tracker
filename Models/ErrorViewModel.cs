using System.ComponentModel.DataAnnotations;

namespace ReceiptExpenseTracker.Models
{
    public class DashboardViewModel
    {
        public decimal TotalToday { get; set; }
        public decimal TotalThisMonth { get; set; }
        public int TotalTransactions { get; set; }
        public List<TransactionSummaryViewModel> RecentTransactions { get; set; } = new();
        public List<ChartDataPoint> DailySpending { get; set; } = new();
        public List<StoreSummary> TopStores { get; set; } = new();
    }

    public class TransactionSummaryViewModel
    {
        public int Id { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class StoreSummary
    {
        public string StoreName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
    }

    public class TransactionListViewModel
    {
        public List<TransactionDetailViewModel> Transactions { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public TransactionFilterViewModel Filter { get; set; } = new();
        public List<string> Stores { get; set; } = new();
    }

    public class TransactionFilterViewModel
    {
        public string? SearchTerm { get; set; }
        public string? Store { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
    }

    public class TransactionDetailViewModel
    {
        public int Id { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? ReceiptImagePath { get; set; }
        public List<TransactionItemViewModel> Items { get; set; } = new();
        public DateTime CreatedDate { get; set; }
    }

    public class TransactionItemViewModel
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => Price * Quantity;
    }

    public class UploadReceiptViewModel
    {
        [Required]
        [StringLength(200)]
        [Display(Name = "Store Name")]
        public string StoreName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Transaction Date")]
        public DateTime TransactionDate { get; set; } = DateTime.Today;

        [Required]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        public IFormFile? ReceiptImage { get; set; }
        public List<ReceiptItemViewModel> Items { get; set; } = new();
    }

    public class ReceiptItemViewModel
    {
        [Required]
        [StringLength(200)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;
    }

    public class EditTransactionViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Store Name")]
        public string StoreName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Transaction Date")]
        public DateTime TransactionDate { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        public string? ReceiptImagePath { get; set; }
        public List<EditItemViewModel> Items { get; set; } = new();
    }

    public class EditItemViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;
    }

    public class ReportViewModel
    {
        public string Period { get; set; } = "monthly";
        public decimal PeriodTotal { get; set; }
        public int PeriodTransactionCount { get; set; }
        public decimal PeriodAverage { get; set; }
        public List<ChartDataPoint> SpendingTrend { get; set; } = new();
        public List<StoreSummary> StoreSummaries { get; set; } = new();
        public List<MonthlyComparison> MonthlyComparisons { get; set; } = new();
    }

    public class MonthlyComparison
    {
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        public string? Message { get; set; }
    }
}
