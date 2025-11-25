using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses with optional filtering
    /// </summary>
    /// <param name="status">Filter by status: Draft, Submitted, Approved, Rejected</param>
    /// <param name="category">Filter by category: Travel, Meals, Supplies, Accommodation, Other</param>
    /// <param name="userId">Filter by user ID</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<Expense>), 200)]
    public async Task<IActionResult> GetExpenses([FromQuery] string? status = null, [FromQuery] string? category = null, [FromQuery] int? userId = null)
    {
        var (expenses, error) = await _expenseService.GetExpensesAsync(status, category, userId);
        
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        
        return Ok(expenses);
    }

    /// <summary>
    /// Get a specific expense by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Expense), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetExpense(int id)
    {
        var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
        
        if (expense == null)
        {
            return NotFound(new ApiError { Message = error ?? "Expense not found" });
        }
        
        return Ok(expense);
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), 201)]
    [ProducesResponseType(typeof(ApiError), 400)]
    public async Task<IActionResult> CreateExpense([FromBody] ExpenseCreateModel model)
    {
        var (expenseId, error) = await _expenseService.CreateExpenseAsync(model);
        
        if (!expenseId.HasValue)
        {
            return BadRequest(new ApiError { Message = "Failed to create expense", Details = error });
        }
        
        return CreatedAtAction(nameof(GetExpense), new { id = expenseId.Value }, new { expenseId = expenseId.Value });
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiError), 400)]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] ExpenseUpdateModel model)
    {
        model.ExpenseId = id;
        var (success, error) = await _expenseService.UpdateExpenseAsync(model);
        
        if (!success)
        {
            return BadRequest(new ApiError { Message = "Failed to update expense", Details = error });
        }
        
        return NoContent();
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiError), 400)]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var (success, error) = await _expenseService.DeleteExpenseAsync(id);
        
        if (!success)
        {
            return BadRequest(new ApiError { Message = "Failed to delete expense", Details = error });
        }
        
        return NoContent();
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiError), 400)]
    public async Task<IActionResult> SubmitExpense(int id)
    {
        var (success, error) = await _expenseService.SubmitExpenseAsync(id);
        
        if (!success)
        {
            return BadRequest(new ApiError { Message = "Failed to submit expense", Details = error });
        }
        
        return NoContent();
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiError), 400)]
    public async Task<IActionResult> ApproveExpense(int id, [FromQuery] int reviewerId = 2)
    {
        var (success, error) = await _expenseService.ApproveExpenseAsync(id, reviewerId);
        
        if (!success)
        {
            return BadRequest(new ApiError { Message = "Failed to approve expense", Details = error });
        }
        
        return NoContent();
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiError), 400)]
    public async Task<IActionResult> RejectExpense(int id, [FromQuery] int reviewerId = 2)
    {
        var (success, error) = await _expenseService.RejectExpenseAsync(id, reviewerId);
        
        if (!success)
        {
            return BadRequest(new ApiError { Message = "Failed to reject expense", Details = error });
        }
        
        return NoContent();
    }

    /// <summary>
    /// Get pending expenses awaiting approval
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<Expense>), 200)]
    public async Task<IActionResult> GetPendingExpenses()
    {
        var (expenses, error) = await _expenseService.GetPendingExpensesAsync();
        
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        
        return Ok(expenses);
    }

    /// <summary>
    /// Get expense statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ExpenseStats), 200)]
    public async Task<IActionResult> GetStats()
    {
        var (stats, error) = await _expenseService.GetExpenseStatsAsync();
        
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        
        return Ok(stats);
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<Category>), 200)]
    public async Task<IActionResult> GetCategories()
    {
        var (categories, error) = await _expenseService.GetCategoriesAsync();
        
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        
        return Ok(categories);
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    [HttpGet("statuses")]
    [ProducesResponseType(typeof(List<ExpenseStatus>), 200)]
    public async Task<IActionResult> GetStatuses()
    {
        var (statuses, error) = await _expenseService.GetStatusesAsync();
        
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        
        return Ok(statuses);
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public UsersController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<User>), 200)]
    public async Task<IActionResult> GetUsers()
    {
        var (users, error) = await _expenseService.GetUsersAsync();
        
        if (error != null)
        {
            Response.Headers.Append("X-Error-Message", error);
        }
        
        return Ok(users);
    }

    /// <summary>
    /// Get a specific user by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(User), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetUser(int id)
    {
        var (user, error) = await _expenseService.GetUserByIdAsync(id);
        
        if (user == null)
        {
            return NotFound(new ApiError { Message = error ?? "User not found" });
        }
        
        return Ok(user);
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Send a message to the AI chat assistant
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), 200)]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        var response = await _chatService.SendMessageAsync(request);
        return Ok(response);
    }
}
