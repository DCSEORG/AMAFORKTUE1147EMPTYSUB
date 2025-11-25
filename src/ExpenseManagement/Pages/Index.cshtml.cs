using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public IndexModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public ExpenseStats Stats { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();

    public async Task OnGetAsync()
    {
        var (stats, statsError) = await _expenseService.GetExpenseStatsAsync();
        if (stats != null)
        {
            Stats = stats;
        }
        if (statsError != null)
        {
            ViewData["Error"] = statsError;
        }

        var (expenses, expensesError) = await _expenseService.GetExpensesAsync();
        Expenses = expenses;
        if (expensesError != null && ViewData["Error"] == null)
        {
            ViewData["Error"] = expensesError;
        }
    }
}
