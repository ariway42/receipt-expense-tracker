using Microsoft.EntityFrameworkCore;
using ReceiptExpenseTracker.Data;
using ReceiptExpenseTracker.Models;

namespace ReceiptExpenseTracker.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly ApplicationDbContext _context;

        public TransactionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Transaction?> GetByIdAsync(int id)
        {
            return await _context.Transactions.FindAsync(id);
        }

        public async Task<Transaction?> GetByIdWithItemsAsync(int id)
        {
            return await _context.Transactions
                .Include(t => t.TransactionItems)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<IEnumerable<Transaction>> GetAllAsync(string? userId = null)
        {
            var query = _context.Transactions
                .Include(t => t.TransactionItems)
                .AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(t => t.UserId == userId);

            return await query
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetFilteredAsync(
            string? searchTerm, string? store,
            DateTime? dateFrom, DateTime? dateTo,
            decimal? minAmount, decimal? maxAmount,
            string? userId = null)
        {
            var query = _context.Transactions
                .Include(t => t.TransactionItems)
                .AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(t => t.UserId == userId);

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(t => t.StoreName.ToLower().Contains(searchTerm.ToLower()));

            if (!string.IsNullOrEmpty(store))
                query = query.Where(t => t.StoreName == store);

            if (dateFrom.HasValue)
                query = query.Where(t => t.TransactionDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(t => t.TransactionDate <= dateTo.Value);

            if (minAmount.HasValue)
                query = query.Where(t => t.TotalAmount >= minAmount.Value);

            if (maxAmount.HasValue)
                query = query.Where(t => t.TotalAmount <= maxAmount.Value);

            return await query
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.CreatedDate)
                .ToListAsync();
        }

        public async Task<Transaction> CreateAsync(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task UpdateAsync(Transaction transaction)
        {
            transaction.UpdatedDate = DateTime.UtcNow;
            _context.Transactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<decimal> GetTotalAmountForDateRangeAsync(
            DateTime startDate, DateTime endDate, string? userId = null)
        {
            var query = _context.Transactions
                .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(t => t.UserId == userId);

            return await query.SumAsync(t => t.TotalAmount);
        }

        public async Task<int> GetTransactionCountAsync(string? userId = null)
        {
            var query = _context.Transactions.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(t => t.UserId == userId);

            return await query.CountAsync();
        }

        public async Task<IEnumerable<Transaction>> GetRecentAsync(int count, string? userId = null)
        {
            var query = _context.Transactions
                .Include(t => t.TransactionItems)
                .AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(t => t.UserId == userId);

            return await query
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.CreatedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetDistinctStoresAsync(string? userId = null)
        {
            var query = _context.Transactions.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(t => t.UserId == userId);

            return await query
                .Select(t => t.StoreName)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }
    }

    public class TransactionItemRepository : ITransactionItemRepository
    {
        private readonly ApplicationDbContext _context;

        public TransactionItemRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TransactionItem?> GetByIdAsync(int id)
        {
            return await _context.TransactionItems.FindAsync(id);
        }

        public async Task<IEnumerable<TransactionItem>> GetByTransactionIdAsync(int transactionId)
        {
            return await _context.TransactionItems
                .Where(i => i.TransactionId == transactionId)
                .ToListAsync();
        }

        public async Task<TransactionItem> CreateAsync(TransactionItem item)
        {
            _context.TransactionItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task CreateRangeAsync(IEnumerable<TransactionItem> items)
        {
            _context.TransactionItems.AddRange(items);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(TransactionItem item)
        {
            _context.TransactionItems.Update(item);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var item = await _context.TransactionItems.FindAsync(id);
            if (item != null)
            {
                _context.TransactionItems.Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteByTransactionIdAsync(int transactionId)
        {
            var items = await _context.TransactionItems
                .Where(i => i.TransactionId == transactionId)
                .ToListAsync();
            _context.TransactionItems.RemoveRange(items);
            await _context.SaveChangesAsync();
        }
    }
}