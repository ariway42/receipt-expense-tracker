using ReceiptExpenseTracker.Models;
namespace ReceiptExpenseTracker.Repositories
{
    public interface ITransactionRepository
    {
        Task<Transaction?> GetByIdAsync(int id);
        Task<Transaction?> GetByIdWithItemsAsync(int id);
        Task<IEnumerable<Transaction>> GetAllAsync(string? userId = null);
        Task<IEnumerable<Transaction>> GetFilteredAsync(
            string? searchTerm,
            string? store,
            DateTime? dateFrom,
            DateTime? dateTo,
            decimal? minAmount,
            decimal? maxAmount,
            string? userId = null);
        Task<Transaction> CreateAsync(Transaction transaction);
        Task UpdateAsync(Transaction transaction);
        Task DeleteAsync(int id);
        Task<decimal> GetTotalAmountForDateRangeAsync(DateTime startDate, DateTime endDate, string? userId = null);
        Task<int> GetTransactionCountAsync(string? userId = null);
        Task<IEnumerable<Transaction>> GetRecentAsync(int count, string? userId = null);
        Task<IEnumerable<string>> GetDistinctStoresAsync(string? userId = null);
    }

    public interface ITransactionItemRepository
    {
        Task<TransactionItem?> GetByIdAsync(int id);
        Task<IEnumerable<TransactionItem>> GetByTransactionIdAsync(int transactionId);
        Task<TransactionItem> CreateAsync(TransactionItem item);
        Task CreateRangeAsync(IEnumerable<TransactionItem> items);
        Task UpdateAsync(TransactionItem item);
        Task DeleteAsync(int id);
        Task DeleteByTransactionIdAsync(int transactionId);
    }
}