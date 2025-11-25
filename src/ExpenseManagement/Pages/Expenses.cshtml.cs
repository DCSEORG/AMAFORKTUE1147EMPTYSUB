using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public ExpensesModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Expense> Expenses { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CategoryFilter { get; set; }

    public async Task OnGetAsync()
    {
        var (expenses, error) = await _expenseService.GetExpensesAsync(StatusFilter, CategoryFilter);
        Expenses = expenses;
        if (error != null)
        {
            ViewData["Error"] = error;
        }

        var (categories, _) = await _expenseService.GetCategoriesAsync();
        Categories = categories;

        var (statuses, _) = await _expenseService.GetStatusesAsync();
        Statuses = statuses;
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        await _expenseService.SubmitExpenseAsync(id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await _expenseService.DeleteExpenseAsync(id);
        return RedirectToPage();
    }
}
