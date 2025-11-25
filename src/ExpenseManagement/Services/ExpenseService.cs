using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<(List<Expense> Expenses, string? Error)> GetExpensesAsync(string? statusFilter = null, string? categoryFilter = null, int? userIdFilter = null);
    Task<(Expense? Expense, string? Error)> GetExpenseByIdAsync(int expenseId);
    Task<(int? ExpenseId, string? Error)> CreateExpenseAsync(ExpenseCreateModel model);
    Task<(bool Success, string? Error)> UpdateExpenseAsync(ExpenseUpdateModel model);
    Task<(bool Success, string? Error)> DeleteExpenseAsync(int expenseId);
    Task<(bool Success, string? Error)> SubmitExpenseAsync(int expenseId);
    Task<(bool Success, string? Error)> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<(bool Success, string? Error)> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<(List<Expense> Expenses, string? Error)> GetPendingExpensesAsync();
    Task<(ExpenseStats? Stats, string? Error)> GetExpenseStatsAsync();
    Task<(List<Category> Categories, string? Error)> GetCategoriesAsync();
    Task<(List<ExpenseStatus> Statuses, string? Error)> GetStatusesAsync();
    Task<(List<User> Users, string? Error)> GetUsersAsync();
    Task<(User? User, string? Error)> GetUserByIdAsync(int userId);
}

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string GetConnectionString()
    {
        return _configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not configured");
    }

    public async Task<(List<Expense> Expenses, string? Error)> GetExpensesAsync(string? statusFilter = null, string? categoryFilter = null, int? userIdFilter = null)
    {
        try
        {
            var expenses = new List<Expense>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_GetExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@StatusFilter", (object?)statusFilter ?? DBNull.Value);
            command.Parameters.AddWithValue("@CategoryFilter", (object?)categoryFilter ?? DBNull.Value);
            command.Parameters.AddWithValue("@UserIdFilter", (object?)userIdFilter ?? DBNull.Value);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses");
            return (GetDummyExpenses(), FormatError(ex));
        }
    }

    public async Task<(Expense? Expense, string? Error)> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_GetExpenseById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (MapExpense(reader), null);
            }

            return (null, "Expense not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense {ExpenseId}", expenseId);
            return (null, FormatError(ex));
        }
    }

    public async Task<(int? ExpenseId, string? Error)> CreateExpenseAsync(ExpenseCreateModel model)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_CreateExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", model.UserId);
            command.Parameters.AddWithValue("@CategoryId", model.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(model.Amount * 100));
            command.Parameters.AddWithValue("@Currency", "GBP");
            command.Parameters.AddWithValue("@ExpenseDate", model.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)model.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", DBNull.Value);
            
            var outputParam = command.Parameters.Add("@NewExpenseId", SqlDbType.Int);
            outputParam.Direction = ParameterDirection.Output;

            await command.ExecuteNonQueryAsync();

            return ((int)outputParam.Value, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return (null, FormatError(ex));
        }
    }

    public async Task<(bool Success, string? Error)> UpdateExpenseAsync(ExpenseUpdateModel model)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_UpdateExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", model.ExpenseId);
            command.Parameters.AddWithValue("@CategoryId", model.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(model.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", model.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)model.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", DBNull.Value);

            await command.ExecuteNonQueryAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", model.ExpenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool Success, string? Error)> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_DeleteExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            await command.ExecuteNonQueryAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool Success, string? Error)> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_SubmitExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            await command.ExecuteNonQueryAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool Success, string? Error)> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_ApproveExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await command.ExecuteNonQueryAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(bool Success, string? Error)> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_RejectExpense", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await command.ExecuteNonQueryAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {ExpenseId}", expenseId);
            return (false, FormatError(ex));
        }
    }

    public async Task<(List<Expense> Expenses, string? Error)> GetPendingExpensesAsync()
    {
        try
        {
            var expenses = new List<Expense>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_GetPendingExpenses", connection);
            command.CommandType = CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return (expenses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending expenses");
            return (GetDummyExpenses().Where(e => e.StatusName == "Submitted").ToList(), FormatError(ex));
        }
    }

    public async Task<(ExpenseStats? Stats, string? Error)> GetExpenseStatsAsync()
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_GetExpenseStats", connection);
            command.CommandType = CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (new ExpenseStats
                {
                    TotalExpenses = reader.GetInt32(reader.GetOrdinal("TotalExpenses")),
                    PendingApprovals = reader.GetInt32(reader.GetOrdinal("PendingApprovals")),
                    TotalApprovedAmount = reader.GetInt32(reader.GetOrdinal("TotalApprovedAmountMinor")) / 100m,
                    ApprovedCount = reader.GetInt32(reader.GetOrdinal("ApprovedCount"))
                }, null);
            }

            return (null, "No stats available");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense stats");
            return (GetDummyStats(), FormatError(ex));
        }
    }

    public async Task<(List<Category> Categories, string? Error)> GetCategoriesAsync()
    {
        try
        {
            var categories = new List<Category>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_GetCategories", connection);
            command.CommandType = CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }

            return (categories, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return (GetDummyCategories(), FormatError(ex));
        }
    }

    public async Task<(List<ExpenseStatus> Statuses, string? Error)> GetStatusesAsync()
    {
        try
        {
            var statuses = new List<ExpenseStatus>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_GetStatuses", connection);
            command.CommandType = CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }

            return (statuses, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statuses");
            return (GetDummyStatuses(), FormatError(ex));
        }
    }

    public async Task<(List<User> Users, string? Error)> GetUsersAsync()
    {
        try
        {
            var users = new List<User>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_GetUsers", connection);
            command.CommandType = CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(MapUser(reader));
            }

            return (users, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return (GetDummyUsers(), FormatError(ex));
        }
    }

    public async Task<(User? User, string? Error)> GetUserByIdAsync(int userId)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("dbo.usp_GetUserById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (MapUser(reader), null);
            }

            return (null, "User not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", userId);
            return (null, FormatError(ex));
        }
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            UserEmail = reader.GetString(reader.GetOrdinal("UserEmail")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewerName")) ? null : reader.GetString(reader.GetOrdinal("ReviewerName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    private static User MapUser(SqlDataReader reader)
    {
        return new User
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
            RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
            ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
            ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? null : reader.GetString(reader.GetOrdinal("ManagerName")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    private string FormatError(Exception ex)
    {
        var message = ex.Message;
        
        // Check for managed identity issues
        if (message.Contains("managed identity", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("token", StringComparison.OrdinalIgnoreCase))
        {
            message += "\n\nManaged Identity Fix: Ensure the managed identity has been granted access to the SQL database. " +
                       "Run the script.sql to create the user and assign db_datareader, db_datawriter roles. " +
                       "Also verify AZURE_CLIENT_ID is set in App Service configuration.";
        }

        var stackFrame = new System.Diagnostics.StackTrace(ex, true).GetFrame(0);
        var fileName = stackFrame?.GetFileName() ?? "Unknown";
        var lineNumber = stackFrame?.GetFileLineNumber() ?? 0;

        return $"{message} [File: {Path.GetFileName(fileName)}, Line: {lineNumber}]";
    }

    // Dummy data for fallback when database is unavailable
    private static List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new() { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 3, StatusName = "Approved", AmountMinor = 12000, Amount = 120.00m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-5), Description = "Train tickets to London", CreatedAt = DateTime.Now.AddDays(-5) },
            new() { ExpenseId = 2, UserId = 1, UserName = "Alice Example", CategoryId = 2, CategoryName = "Meals", StatusId = 2, StatusName = "Submitted", AmountMinor = 6900, Amount = 69.00m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-3), Description = "Client lunch meeting", CreatedAt = DateTime.Now.AddDays(-3) },
            new() { ExpenseId = 3, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies", StatusId = 3, StatusName = "Approved", AmountMinor = 9950, Amount = 99.50m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-10), Description = "Office supplies", CreatedAt = DateTime.Now.AddDays(-10) },
            new() { ExpenseId = 4, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 1920, Amount = 19.20m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-1), Description = "Taxi fare", CreatedAt = DateTime.Now.AddDays(-1) }
        };
    }

    private static ExpenseStats GetDummyStats()
    {
        return new ExpenseStats
        {
            TotalExpenses = 10,
            PendingApprovals = 2,
            TotalApprovedAmount = 519.24m,
            ApprovedCount = 6
        };
    }

    private static List<Category> GetDummyCategories()
    {
        return new List<Category>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private static List<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };
    }

    private static List<User> GetDummyUsers()
    {
        return new List<User>
        {
            new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true },
            new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true }
        };
    }
}
