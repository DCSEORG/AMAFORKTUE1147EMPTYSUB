-- Stored Procedures for Expense Management System
-- All data access goes through these procedures - no direct SQL in the application

-- ============================================================================
-- EXPENSE PROCEDURES
-- ============================================================================

-- Get all expenses with optional filtering
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenses
    @StatusFilter NVARCHAR(50) = NULL,
    @CategoryFilter NVARCHAR(100) = NULL,
    @UserIdFilter INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email AS UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        reviewer.UserName AS ReviewerName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE (@StatusFilter IS NULL OR s.StatusName = @StatusFilter)
      AND (@CategoryFilter IS NULL OR c.CategoryName = @CategoryFilter)
      AND (@UserIdFilter IS NULL OR e.UserId = @UserIdFilter)
    ORDER BY e.CreatedAt DESC;
END
GO

-- Get a single expense by ID
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email AS UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        reviewer.UserName AS ReviewerName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- Create a new expense
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @Currency NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL,
    @NewExpenseId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Get Draft status ID with error handling
    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';
    
    IF @DraftStatusId IS NULL
    BEGIN
        RAISERROR('Draft status not found in ExpenseStatus table', 16, 1);
        RETURN;
    END
    
    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile)
    VALUES (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile);
    
    SET @NewExpenseId = SCOPE_IDENTITY();
END
GO

-- Update an existing expense
CREATE OR ALTER PROCEDURE dbo.usp_UpdateExpense
    @ExpenseId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE dbo.Expenses
    SET CategoryId = @CategoryId,
        AmountMinor = @AmountMinor,
        ExpenseDate = @ExpenseDate,
        Description = @Description,
        ReceiptFile = COALESCE(@ReceiptFile, ReceiptFile)
    WHERE ExpenseId = @ExpenseId;
END
GO

-- Delete an expense
CREATE OR ALTER PROCEDURE dbo.usp_DeleteExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM dbo.Expenses WHERE ExpenseId = @ExpenseId;
END
GO

-- Submit an expense for approval
CREATE OR ALTER PROCEDURE dbo.usp_SubmitExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @SubmittedStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';
    
    IF @SubmittedStatusId IS NULL
    BEGIN
        RAISERROR('Submitted status not found in ExpenseStatus table', 16, 1);
        RETURN;
    END
    
    UPDATE dbo.Expenses
    SET StatusId = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- Approve an expense
CREATE OR ALTER PROCEDURE dbo.usp_ApproveExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ApprovedStatusId INT;
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';
    
    IF @ApprovedStatusId IS NULL
    BEGIN
        RAISERROR('Approved status not found in ExpenseStatus table', 16, 1);
        RETURN;
    END
    
    UPDATE dbo.Expenses
    SET StatusId = @ApprovedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- Reject an expense
CREATE OR ALTER PROCEDURE dbo.usp_RejectExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @RejectedStatusId INT;
    SELECT @RejectedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';
    
    IF @RejectedStatusId IS NULL
    BEGIN
        RAISERROR('Rejected status not found in ExpenseStatus table', 16, 1);
        RETURN;
    END
    
    UPDATE dbo.Expenses
    SET StatusId = @RejectedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
END
GO

-- Get pending expenses for approval
CREATE OR ALTER PROCEDURE dbo.usp_GetPendingExpenses
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email AS UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE s.StatusName = 'Submitted'
    ORDER BY e.SubmittedAt ASC;
END
GO

-- Get expense statistics
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseStats
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        (SELECT COUNT(*) FROM dbo.Expenses) AS TotalExpenses,
        (SELECT COUNT(*) FROM dbo.Expenses e INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId WHERE s.StatusName = 'Submitted') AS PendingApprovals,
        (SELECT ISNULL(SUM(AmountMinor), 0) FROM dbo.Expenses e INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId WHERE s.StatusName = 'Approved') AS TotalApprovedAmountMinor,
        (SELECT COUNT(*) FROM dbo.Expenses e INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId WHERE s.StatusName = 'Approved') AS ApprovedCount;
END
GO

-- ============================================================================
-- CATEGORY PROCEDURES
-- ============================================================================

-- Get all categories
CREATE OR ALTER PROCEDURE dbo.usp_GetCategories
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- ============================================================================
-- STATUS PROCEDURES
-- ============================================================================

-- Get all statuses
CREATE OR ALTER PROCEDURE dbo.usp_GetStatuses
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO

-- ============================================================================
-- USER PROCEDURES
-- ============================================================================

-- Get all users
CREATE OR ALTER PROCEDURE dbo.usp_GetUsers
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m ON u.ManagerId = m.UserId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

-- Get user by ID
CREATE OR ALTER PROCEDURE dbo.usp_GetUserById
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m ON u.ManagerId = m.UserId
    WHERE u.UserId = @UserId;
END
GO

-- ============================================================================
-- ROLE PROCEDURES
-- ============================================================================

-- Get all roles
CREATE OR ALTER PROCEDURE dbo.usp_GetRoles
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT RoleId, RoleName, Description
    FROM dbo.Roles
    ORDER BY RoleName;
END
GO
