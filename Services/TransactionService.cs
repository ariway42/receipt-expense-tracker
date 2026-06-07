using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ReceiptExpenseTracker.Models;
using ReceiptExpenseTracker.Repositories;

namespace ReceiptExpenseTracker.Services
{
    public interface ITransactionService
    {
        Task<DashboardViewModel> GetDashboardDataAsync(string userId);
        Task<TransactionListViewModel> GetTransactionsAsync(
            string? searchTerm, string? store, DateTime? dateFrom, DateTime? dateTo,
            decimal? minAmount, decimal? maxAmount, int page, int pageSize, string userId);
        Task<TransactionDetailViewModel?> GetTransactionDetailAsync(int id, string userId);
        Task<Transaction> CreateTransactionAsync(
            string storeName, DateTime transactionDate, decimal totalAmount,
            string? receiptImagePath, List<ReceiptItemViewModel> items,
            string? userId = null);
        Task<bool> UpdateTransactionAsync(int id, string storeName, DateTime transactionDate,
            decimal totalAmount, List<EditItemViewModel> items, string userId);
        Task<bool> DeleteTransactionAsync(int id, string userId);
        Task<ReportViewModel> GetReportDataAsync(string period, string userId);
        Task<byte[]> ExportTransactionsToExcelAsync(
            string? searchTerm, string? store, DateTime? dateFrom, DateTime? dateTo,
            decimal? minAmount, decimal? maxAmount, string userId);
        Task<byte[]> ExportSummaryToExcelAsync(string period, string userId);
        Task<List<string>> GetStoresAsync(string userId);
    }

    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionItemRepository _itemRepository;

        public TransactionService(
            ITransactionRepository transactionRepository,
            ITransactionItemRepository itemRepository)
        {
            _transactionRepository = transactionRepository;
            _itemRepository = itemRepository;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(string userId)
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var recent = await _transactionRepository.GetRecentAsync(5, userId);
            var allTransactions = await _transactionRepository.GetAllAsync(userId);

            var totalToday = await _transactionRepository.GetTotalAmountForDateRangeAsync(
                today, today.AddDays(1).AddSeconds(-1), userId);
            var totalMonth = await _transactionRepository.GetTotalAmountForDateRangeAsync(
                monthStart, today, userId);
            var totalCount = await _transactionRepository.GetTransactionCountAsync(userId);

            var storeGroups = allTransactions
                .GroupBy(t => t.StoreName)
                .Select(g => new StoreSummary
                {
                    StoreName = g.Key,
                    TotalAmount = g.Sum(t => t.TotalAmount),
                    TransactionCount = g.Count()
                })
                .OrderByDescending(s => s.TotalAmount)
                .Take(5)
                .ToList();

            var last30Days = Enumerable.Range(0, 30)
                .Select(i => today.AddDays(-29 + i))
                .Select(date => new ChartDataPoint
                {
                    Label = date.ToString("MMM dd"),
                    Amount = allTransactions
                        .Where(t => t.TransactionDate.Date == date)
                        .Sum(t => t.TotalAmount)
                })
                .ToList();

            return new DashboardViewModel
            {
                TotalToday = totalToday,
                TotalThisMonth = totalMonth,
                TotalTransactions = totalCount,
                RecentTransactions = recent.Select(MapToSummary).ToList(),
                DailySpending = last30Days,
                TopStores = storeGroups
            };
        }

        public async Task<TransactionListViewModel> GetTransactionsAsync(
            string? searchTerm, string? store, DateTime? dateFrom, DateTime? dateTo,
            decimal? minAmount, decimal? maxAmount, int page, int pageSize, string userId)
        {
            var transactions = await _transactionRepository.GetFilteredAsync(
                searchTerm, store, dateFrom, dateTo, minAmount, maxAmount, userId);

            var stores = await _transactionRepository.GetDistinctStoresAsync(userId);
            var totalCount = transactions.Count();
            var paged = transactions
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            return new TransactionListViewModel
            {
                Transactions = paged.Select(MapToDetail).ToList(),
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize,
                Filter = new TransactionFilterViewModel
                {
                    SearchTerm = searchTerm,
                    Store = store,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    MinAmount = minAmount,
                    MaxAmount = maxAmount
                },
                Stores = stores.ToList()
            };
        }

        public async Task<TransactionDetailViewModel?> GetTransactionDetailAsync(int id, string userId)
        {
            var transaction = await _transactionRepository.GetByIdWithItemsAsync(id);
            if (transaction == null || transaction.UserId != userId) return null;
            return MapToDetail(transaction);
        }

        public async Task<Transaction> CreateTransactionAsync(
            string storeName, DateTime transactionDate, decimal totalAmount,
            string? receiptImagePath, List<ReceiptItemViewModel> items,
            string? userId = null)
        {
            var transaction = new Transaction
            {
                StoreName = storeName,
                TransactionDate = transactionDate,
                TotalAmount = totalAmount,
                ReceiptImagePath = receiptImagePath,
                UserId = userId
            };

            var created = await _transactionRepository.CreateAsync(transaction);

            if (items.Any())
            {
                var transactionItems = items.Select(i => new TransactionItem
                {
                    TransactionId = created.Id,
                    ItemName = i.ItemName,
                    Price = i.Price,
                    Quantity = i.Quantity
                });
                await _itemRepository.CreateRangeAsync(transactionItems);
            }

            return created;
        }

        public async Task<bool> UpdateTransactionAsync(int id, string storeName,
            DateTime transactionDate, decimal totalAmount,
            List<EditItemViewModel> items, string userId)
        {
            var transaction = await _transactionRepository.GetByIdAsync(id);
            if (transaction == null || transaction.UserId != userId) return false;

            transaction.StoreName = storeName;
            transaction.TransactionDate = transactionDate;
            transaction.TotalAmount = totalAmount;

            await _transactionRepository.UpdateAsync(transaction);

            await _itemRepository.DeleteByTransactionIdAsync(id);
            if (items.Any())
            {
                var newItems = items.Select(i => new TransactionItem
                {
                    TransactionId = id,
                    ItemName = i.ItemName,
                    Price = i.Price,
                    Quantity = i.Quantity
                });
                await _itemRepository.CreateRangeAsync(newItems);
            }

            return true;
        }

        public async Task<bool> DeleteTransactionAsync(int id, string userId)
        {
            var transaction = await _transactionRepository.GetByIdAsync(id);
            if (transaction == null || transaction.UserId != userId) return false;

            await _transactionRepository.DeleteAsync(id);
            return true;
        }

        public async Task<ReportViewModel> GetReportDataAsync(string period, string userId)
        {
            var today = DateTime.Today;
            DateTime startDate, endDate;

            switch (period.ToLower())
            {
                case "daily":
                    startDate = today;
                    endDate = today.AddDays(1).AddSeconds(-1);
                    break;
                case "weekly":
                    startDate = today.AddDays(-(int)today.DayOfWeek);
                    endDate = startDate.AddDays(6);
                    break;
                case "yearly":
                    startDate = new DateTime(today.Year, 1, 1);
                    endDate = new DateTime(today.Year, 12, 31);
                    break;
                default:
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    break;
            }

            var transactions = await _transactionRepository.GetAllAsync(userId);

            var periodTransactions = transactions
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate)
                .ToList();

            var periodTotal = periodTransactions.Sum(t => t.TotalAmount);
            var periodCount = periodTransactions.Count;
            var periodAverage = periodCount > 0 ? periodTotal / periodCount : 0;

            var storeSummaries = periodTransactions
                .GroupBy(t => t.StoreName)
                .Select(g => new StoreSummary
                {
                    StoreName = g.Key,
                    TotalAmount = g.Sum(t => t.TotalAmount),
                    TransactionCount = g.Count()
                })
                .OrderByDescending(s => s.TotalAmount)
                .ToList();

            var monthlyComparisons = Enumerable.Range(0, 6)
                .Select(i =>
                {
                    var month = today.AddMonths(-i);
                    var monthStart = new DateTime(month.Year, month.Month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                    return new MonthlyComparison
                    {
                        Month = month.ToString("MMM"),
                        Amount = transactions
                            .Where(t => t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd)
                            .Sum(t => t.TotalAmount)
                    };
                })
                .Reverse()
                .ToList();

            var spendingTrend = GetSpendingTrend(period, transactions, today);

            return new ReportViewModel
            {
                Period = period,
                PeriodTotal = periodTotal,
                PeriodTransactionCount = periodCount,
                PeriodAverage = periodAverage,
                SpendingTrend = spendingTrend,
                StoreSummaries = storeSummaries,
                MonthlyComparisons = monthlyComparisons
            };
        }

        private List<ChartDataPoint> GetSpendingTrend(string period, IEnumerable<Transaction> transactions, DateTime today)
        {
            return period.ToLower() switch
            {
                "daily" => transactions
                    .Where(t => t.TransactionDate >= today.AddDays(-30))
                    .GroupBy(t => t.TransactionDate.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new ChartDataPoint { Label = g.Key.ToString("MMM dd"), Amount = g.Sum(t => t.TotalAmount) })
                    .ToList(),
                "weekly" => transactions
                    .Where(t => t.TransactionDate >= today.AddDays(-84))
                    .GroupBy(t => $"{t.TransactionDate.Year}-W{GetWeekNumber(t.TransactionDate)}")
                    .OrderBy(g => g.Key)
                    .Select(g => new ChartDataPoint { Label = g.Key, Amount = g.Sum(t => t.TotalAmount) })
                    .ToList(),
                "yearly" => transactions
                    .GroupBy(t => t.TransactionDate.Year)
                    .OrderBy(g => g.Key)
                    .Select(g => new ChartDataPoint { Label = g.Key.ToString(), Amount = g.Sum(t => t.TotalAmount) })
                    .ToList(),
                _ => transactions
                    .Where(t => t.TransactionDate >= today.AddMonths(-12))
                    .GroupBy(t => new DateTime(t.TransactionDate.Year, t.TransactionDate.Month, 1))
                    .OrderBy(g => g.Key)
                    .Select(g => new ChartDataPoint { Label = g.Key.ToString("MMM yyyy"), Amount = g.Sum(t => t.TotalAmount) })
                    .ToList()
            };
        }

        private int GetWeekNumber(DateTime date)
        {
            return System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
        }

        public async Task<byte[]> ExportTransactionsToExcelAsync(
            string? searchTerm, string? store, DateTime? dateFrom, DateTime? dateTo,
            decimal? minAmount, decimal? maxAmount, string userId)
        {
            var transactions = await _transactionRepository.GetFilteredAsync(
                searchTerm, store, dateFrom, dateTo, minAmount, maxAmount, userId);

            using var stream = new MemoryStream();
            using var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Transactions" });

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            var headerRow = new Row();
            headerRow.Append(
                CreateCell("Date"), CreateCell("Store"), CreateCell("Items"),
                CreateCell("Total Amount"), CreateCell("Created Date"));
            sheetData.AppendChild(headerRow);

            foreach (var t in transactions)
            {
                var row = new Row();
                row.Append(
                    CreateCell(t.TransactionDate.ToString("yyyy-MM-dd")),
                    CreateCell(t.StoreName),
                    CreateCell(string.Join(", ", t.TransactionItems.Select(i => $"{i.ItemName} ({i.Quantity}x Rp{i.Price:F0})"))),
                    CreateCell(t.TotalAmount.ToString("F0")),
                    CreateCell(t.CreatedDate.ToString("yyyy-MM-dd")));
                sheetData.AppendChild(row);
            }

            document.Save();
            return stream.ToArray();
        }

        public async Task<byte[]> ExportSummaryToExcelAsync(string period, string userId)
        {
            var report = await GetReportDataAsync(period, userId);

            using var stream = new MemoryStream();
            using var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Summary" });

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            var headerRow = new Row();
            headerRow.Append(
                CreateCell("Store"), CreateCell("Total Spent"),
                CreateCell("Transaction Count"), CreateCell("Average"));
            sheetData.AppendChild(headerRow);

            foreach (var s in report.StoreSummaries)
            {
                var row = new Row();
                row.Append(
                    CreateCell(s.StoreName),
                    CreateCell(s.TotalAmount.ToString("F0")),
                    CreateCell(s.TransactionCount.ToString()),
                    CreateCell((s.TotalAmount / s.TransactionCount).ToString("F0")));
                sheetData.AppendChild(row);
            }

            document.Save();
            return stream.ToArray();
        }

        public async Task<List<string>> GetStoresAsync(string userId)
        {
            return (await _transactionRepository.GetDistinctStoresAsync(userId)).ToList();
        }

        private static TransactionSummaryViewModel MapToSummary(Transaction t)
        {
            return new TransactionSummaryViewModel
            {
                Id = t.Id,
                StoreName = t.StoreName,
                TransactionDate = t.TransactionDate,
                TotalAmount = t.TotalAmount,
                ItemCount = t.TransactionItems?.Count ?? 0
            };
        }

        private static TransactionDetailViewModel MapToDetail(Transaction t)
        {
            return new TransactionDetailViewModel
            {
                Id = t.Id,
                StoreName = t.StoreName,
                TransactionDate = t.TransactionDate,
                TotalAmount = t.TotalAmount,
                ReceiptImagePath = t.ReceiptImagePath,
                Items = t.TransactionItems?.Select(i => new TransactionItemViewModel
                {
                    Id = i.Id,
                    ItemName = i.ItemName,
                    Price = i.Price,
                    Quantity = i.Quantity
                }).ToList() ?? new List<TransactionItemViewModel>(),
                CreatedDate = t.CreatedDate
            };
        }

        private static Cell CreateCell(string value)
        {
            return new Cell { CellValue = new CellValue(value), DataType = CellValues.String };
        }
    }
}