using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public ApprovalsModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Expense> PendingExpenses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    public async Task OnGetAsync()
    {
        var (expenses, error) = await _expenseService.GetPendingExpensesAsync();
        
        if (!string.IsNullOrEmpty(Filter))
        {
            expenses = expenses.Where(e => 
                e.UserName.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                e.CategoryName.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                (e.Description?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }
        
        PendingExpenses = expenses;
        
        if (error != null)
        {
            ViewData["Error"] = error;
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        await _expenseService.ApproveExpenseAsync(id, 2); // Bob Manager as reviewer
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        await _expenseService.RejectExpenseAsync(id, 2); // Bob Manager as reviewer
        return RedirectToPage();
    }
}
