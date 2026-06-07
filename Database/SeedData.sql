-- =============================================
-- Receipt Expense Tracker - Sample Data
-- =============================================

USE ReceiptExpenseTracker;
GO

-- Clear existing data
DELETE FROM TransactionItems;
DELETE FROM Transactions;
DBCC CHECKIDENT ('TransactionItems', RESEED, 0);
DBCC CHECKIDENT ('Transactions', RESEED, 0);
GO

-- Insert sample transactions
INSERT INTO Transactions (StoreName, TransactionDate, TotalAmount, CreatedDate)
VALUES
    ('Walmart', DATEADD(DAY, 0, CAST(GETDATE() AS DATE)), 127.84, DATEADD(DAY, 0, SYSDATETIME())),
    ('Target', DATEADD(DAY, -1, CAST(GETDATE() AS DATE)), 89.99, DATEADD(DAY, -1, SYSDATETIME())),
    ('Costco', DATEADD(DAY, -2, CAST(GETDATE() AS DATE)), 245.50, DATEADD(DAY, -2, SYSDATETIME())),
    ('Whole Foods', DATEADD(DAY, -3, CAST(GETDATE() AS DATE)), 156.32, DATEADD(DAY, -3, SYSDATETIME())),
    ('Trader Joe''s', DATEADD(DAY, -4, CAST(GETDATE() AS DATE)), 78.45, DATEADD(DAY, -4, SYSDATETIME())),
    ('Amazon', DATEADD(DAY, -5, CAST(GETDATE() AS DATE)), 299.99, DATEADD(DAY, -5, SYSDATETIME())),
    ('Walmart', DATEADD(DAY, -6, CAST(GETDATE() AS DATE)), 67.89, DATEADD(DAY, -6, SYSDATETIME())),
    ('Target', DATEADD(DAY, -9, CAST(GETDATE() AS DATE)), 145.20, DATEADD(DAY, -9, SYSDATETIME())),
    ('Costco', DATEADD(DAY, -16, CAST(GETDATE() AS DATE)), 380.00, DATEADD(DAY, -16, SYSDATETIME())),
    ('Whole Foods', DATEADD(DAY, -22, CAST(GETDATE() AS DATE)), 234.56, DATEADD(DAY, -22, SYSDATETIME())),
    ('Trader Joe''s', DATEADD(DAY, -27, CAST(GETDATE() AS DATE)), 98.76, DATEADD(DAY, -27, SYSDATETIME())),
    ('Kroger', DATEADD(DAY, -32, CAST(GETDATE() AS DATE)), 165.43, DATEADD(DAY, -32, SYSDATETIME())),
    ('Safeway', DATEADD(DAY, -39, CAST(GETDATE() AS DATE)), 112.34, DATEADD(DAY, -39, SYSDATETIME())),
    ('Walmart', DATEADD(DAY, -47, CAST(GETDATE() AS DATE)), 89.99, DATEADD(DAY, -47, SYSDATETIME())),
    ('Amazon', DATEADD(DAY, -52, CAST(GETDATE() AS DATE)), 149.99, DATEADD(DAY, -52, SYSDATETIME()));
GO

-- Insert sample transaction items
INSERT INTO TransactionItems (TransactionId, ItemName, Price, Quantity)
SELECT 1, 'Groceries - Milk', 4.99, 2 UNION ALL
SELECT 1, 'Groceries - Bread', 3.49, 1 UNION ALL
SELECT 1, 'Groceries - Eggs', 6.99, 1 UNION ALL
SELECT 1, 'Household - Paper Towels', 12.99, 2 UNION ALL
SELECT 1, 'Groceries - Chicken Breast', 9.99, 3 UNION ALL
SELECT 1, 'Produce - Bananas', 2.49, 2;

INSERT INTO TransactionItems (TransactionId, ItemName, Price, Quantity)
SELECT 2, 'Home - Bed Sheets', 45.99, 1 UNION ALL
SELECT 2, 'Kitchen - Utensils Set', 24.99, 1 UNION ALL
SELECT 2, 'Bath - Towels', 19.01, 1;

INSERT INTO TransactionItems (TransactionId, ItemName, Price, Quantity)
SELECT 3, 'Bulk - Rice 25lb', 24.99, 1 UNION ALL
SELECT 3, 'Bulk - Chicken 10lb', 49.99, 1 UNION ALL
SELECT 3, 'Electronics - Batteries', 18.99, 2 UNION ALL
SELECT 3, 'Household - Laundry Detergent', 19.99, 2 UNION ALL
SELECT 3, 'Snacks - Mixed Nuts', 16.99, 1;

INSERT INTO TransactionItems (TransactionId, ItemName, Price, Quantity)
SELECT 4, 'Organic - Spinach', 5.99, 2 UNION ALL
SELECT 4, 'Organic - Salmon', 14.99, 2 UNION ALL
SELECT 4, 'Organic - Avocados', 2.49, 4 UNION ALL
SELECT 4, 'Bakery - Artisan Bread', 6.99, 2;

INSERT INTO TransactionItems (TransactionId, ItemName, Price, Quantity)
SELECT 5, 'Specialty - Chocolate', 4.99, 3 UNION ALL
SELECT 5, 'Frozen - Pizza', 5.99, 2 UNION ALL
SELECT 5, 'Produce - Bell Peppers', 1.99, 4;

INSERT INTO TransactionItems (TransactionId, ItemName, Price, Quantity)
SELECT 6, 'Electronics - Headphones', 149.99, 1 UNION ALL
SELECT 6, 'Electronics - Phone Case', 29.99, 1 UNION ALL
SELECT 6, 'Electronics - USB Cable', 12.99, 2 UNION ALL
SELECT 6, 'Books - Programming Guide', 49.99, 1;

PRINT 'Sample data inserted successfully.';
GO

-- Verify data
SELECT
    'Transactions' AS TableName,
    COUNT(*) AS RecordCount
FROM Transactions
UNION ALL
SELECT
    'TransactionItems',
    COUNT(*)
FROM TransactionItems;
GO
