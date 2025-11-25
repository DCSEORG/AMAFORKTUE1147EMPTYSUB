using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class NewExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public NewExpenseModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Category> Categories { get; set; } = new();
    public List<User> Users { get; set; } = new();

    [BindProperty]
    public decimal Amount { get; set; }

    [BindProperty]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [BindProperty]
    public int CategoryId { get; set; }

    [BindProperty]
    public int UserId { get; set; } = 1;

    [BindProperty]
    public string? Description { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadDataAsync();

        var model = new ExpenseCreateModel
        {
            UserId = UserId,
            CategoryId = CategoryId,
            Amount = Amount,
            ExpenseDate = ExpenseDate,
            Description = Description
        };

        var (expenseId, error) = await _expenseService.CreateExpenseAsync(model);

        if (expenseId.HasValue)
        {
            SuccessMessage = $"Expense created successfully! (ID: {expenseId.Value})";
            // Reset form
            Amount = 0;
            ExpenseDate = DateTime.Today;
            CategoryId = 0;
            Description = null;
        }
        else
        {
            ErrorMessage = error ?? "Failed to create expense";
        }

        return Page();
    }

    private async Task LoadDataAsync()
    {
        var (categories, _) = await _expenseService.GetCategoriesAsync();
        Categories = categories;

        var (users, _) = await _expenseService.GetUsersAsync();
        Users = users;
    }
}
