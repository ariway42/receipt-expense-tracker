-- =============================================
-- Receipt Expense Tracker - Database Setup Script
-- SQL Server 2019+
-- =============================================

-- Create database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ReceiptExpenseTracker')
BEGIN
    CREATE DATABASE ReceiptExpenseTracker;
END
GO

USE ReceiptExpenseTracker;
GO

-- =============================================
-- Tables
-- =============================================

-- Transactions table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Transactions')
BEGIN
    CREATE TABLE Transactions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        StoreName NVARCHAR(200) NOT NULL,
        TransactionDate DATE NOT NULL,
        TotalAmount DECIMAL(12,2) NOT NULL,
        ReceiptImagePath NVARCHAR(500) NULL,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        UpdatedDate DATETIME2 NULL
    );

    PRINT 'Transactions table created successfully.';
END
GO

-- TransactionItems table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransactionItems')
BEGIN
    CREATE TABLE TransactionItems (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TransactionId INT NOT NULL,
        ItemName NVARCHAR(200) NOT NULL,
        Price DECIMAL(12,2) NOT NULL,
        Quantity INT NOT NULL DEFAULT 1,
        CreatedDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        CONSTRAINT FK_TransactionItems_Transactions FOREIGN KEY (TransactionId)
            REFERENCES Transactions(Id) ON DELETE CASCADE
    );

    PRINT 'TransactionItems table created successfully.';
END
GO

-- =============================================
-- Indexes
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Transactions_TransactionDate')
BEGIN
    CREATE INDEX IX_Transactions_TransactionDate
    ON Transactions(TransactionDate DESC);
    PRINT 'Index IX_Transactions_TransactionDate created.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Transactions_StoreName')
BEGIN
    CREATE INDEX IX_Transactions_StoreName
    ON Transactions(StoreName);
    PRINT 'Index IX_Transactions_StoreName created.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TransactionItems_TransactionId')
BEGIN
    CREATE INDEX IX_TransactionItems_TransactionId
    ON TransactionItems(TransactionId);
    PRINT 'Index IX_TransactionItems_TransactionId created.';
END
GO

-- =============================================
-- Triggers
-- =============================================

IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_Transactions_UpdatedDate')
BEGIN
    DROP TRIGGER TR_Transactions_UpdatedDate;
END
GO

CREATE TRIGGER TR_Transactions_UpdatedDate
ON Transactions
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE t
    SET UpdatedDate = SYSDATETIME()
    FROM Transactions t
    INNER JOIN inserted i ON t.Id = i.Id;
END
GO

PRINT 'Database setup completed successfully.';
GO
