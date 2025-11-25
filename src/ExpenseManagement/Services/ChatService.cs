using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using ExpenseManagement.Models;
using System.Text.Json;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request);
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
    {
        var endpoint = _configuration["OpenAI:Endpoint"];
        var deploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";

        // Check if GenAI is configured
        if (string.IsNullOrEmpty(endpoint))
        {
            return new ChatResponse
            {
                Response = "🤖 **AI Chat is not configured.**\n\n" +
                          "The GenAI services (Azure OpenAI) were not deployed with this application.\n\n" +
                          "To enable AI-powered chat features:\n" +
                          "1. Run `deploy-with-chat.sh` instead of `deploy.sh`\n" +
                          "2. This will deploy Azure OpenAI and AI Search resources\n" +
                          "3. The chat will then be able to help you interact with expenses using natural language\n\n" +
                          "In the meantime, you can use the regular UI to manage expenses.",
                Success = true
            };
        }

        try
        {
            // Use ManagedIdentityCredential with explicit client ID
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            TokenCredential credential;
            
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var client = new OpenAIClient(new Uri(endpoint), credential);

            // Build conversation history
            var messages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(GetSystemPrompt())
            };

            // Add history if present
            if (request.History != null)
            {
                foreach (var msg in request.History)
                {
                    if (msg.Role == "user")
                        messages.Add(new ChatRequestUserMessage(msg.Content));
                    else if (msg.Role == "assistant")
                        messages.Add(new ChatRequestAssistantMessage(msg.Content));
                }
            }

            // Add current message
            messages.Add(new ChatRequestUserMessage(request.Message));

            // Define function tools for database operations
            var options = new ChatCompletionsOptions(deploymentName, messages)
            {
                Temperature = 0.7f,
                MaxTokens = 2000,
                Tools = { 
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "get_expenses",
                        Description = "Retrieves expenses from the database with optional filters",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                statusFilter = new { type = "string", description = "Filter by status: Draft, Submitted, Approved, Rejected" },
                                categoryFilter = new { type = "string", description = "Filter by category: Travel, Meals, Supplies, Accommodation, Other" }
                            }
                        })
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "get_pending_expenses",
                        Description = "Retrieves all expenses that are pending approval (Submitted status)"
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "get_expense_stats",
                        Description = "Retrieves expense statistics including total count, pending approvals, and approved amounts"
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "create_expense",
                        Description = "Creates a new expense in the system",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                userId = new { type = "integer", description = "The ID of the user creating the expense (default: 1 for Alice)" },
                                categoryId = new { type = "integer", description = "Category ID: 1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other" },
                                amount = new { type = "number", description = "Amount in GBP (e.g., 25.50)" },
                                expenseDate = new { type = "string", description = "Date in YYYY-MM-DD format" },
                                description = new { type = "string", description = "Description of the expense" }
                            },
                            required = new[] { "categoryId", "amount", "expenseDate" }
                        })
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "approve_expense",
                        Description = "Approves a submitted expense",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                expenseId = new { type = "integer", description = "The ID of the expense to approve" },
                                reviewerId = new { type = "integer", description = "The ID of the manager approving (default: 2 for Bob Manager)" }
                            },
                            required = new[] { "expenseId" }
                        })
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "reject_expense",
                        Description = "Rejects a submitted expense",
                        Parameters = BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                expenseId = new { type = "integer", description = "The ID of the expense to reject" },
                                reviewerId = new { type = "integer", description = "The ID of the manager rejecting (default: 2 for Bob Manager)" }
                            },
                            required = new[] { "expenseId" }
                        })
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "get_categories",
                        Description = "Retrieves all expense categories"
                    }),
                    new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
                    {
                        Name = "get_users",
                        Description = "Retrieves all users in the system"
                    })
                }
            };

            // Function calling loop
            var response = await client.GetChatCompletionsAsync(options);
            var choice = response.Value.Choices[0];

            while (choice.FinishReason == CompletionsFinishReason.ToolCalls)
            {
                // Process tool calls
                var assistantMessage = new ChatRequestAssistantMessage(choice.Message);
                messages.Add(assistantMessage);

                foreach (var toolCall in choice.Message.ToolCalls.OfType<ChatCompletionsFunctionToolCall>())
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.Name, toolCall.Arguments);
                    messages.Add(new ChatRequestToolMessage(functionResult, toolCall.Id));
                }

                options = new ChatCompletionsOptions(deploymentName, messages)
                {
                    Temperature = 0.7f,
                    MaxTokens = 2000
                };

                response = await client.GetChatCompletionsAsync(options);
                choice = response.Value.Choices[0];
            }

            return new ChatResponse
            {
                Response = choice.Message.Content ?? "I couldn't generate a response.",
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return new ChatResponse
            {
                Response = "Sorry, I encountered an error processing your request.",
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var args = JsonDocument.Parse(arguments).RootElement;

            switch (functionName)
            {
                case "get_expenses":
                    var statusFilter = args.TryGetProperty("statusFilter", out var sf) ? sf.GetString() : null;
                    var categoryFilter = args.TryGetProperty("categoryFilter", out var cf) ? cf.GetString() : null;
                    var (expenses, _) = await _expenseService.GetExpensesAsync(statusFilter, categoryFilter);
                    return JsonSerializer.Serialize(expenses);

                case "get_pending_expenses":
                    var (pending, _) = await _expenseService.GetPendingExpensesAsync();
                    return JsonSerializer.Serialize(pending);

                case "get_expense_stats":
                    var (stats, _) = await _expenseService.GetExpenseStatsAsync();
                    return JsonSerializer.Serialize(stats);

                case "create_expense":
                    var createModel = new ExpenseCreateModel
                    {
                        UserId = args.TryGetProperty("userId", out var uid) ? uid.GetInt32() : 1,
                        CategoryId = args.GetProperty("categoryId").GetInt32(),
                        Amount = args.GetProperty("amount").GetDecimal(),
                        ExpenseDate = DateTime.Parse(args.GetProperty("expenseDate").GetString()!),
                        Description = args.TryGetProperty("description", out var desc) ? desc.GetString() : null
                    };
                    var (newId, createError) = await _expenseService.CreateExpenseAsync(createModel);
                    return newId.HasValue 
                        ? JsonSerializer.Serialize(new { success = true, expenseId = newId.Value })
                        : JsonSerializer.Serialize(new { success = false, error = createError });

                case "approve_expense":
                    var approveId = args.GetProperty("expenseId").GetInt32();
                    var approverId = args.TryGetProperty("reviewerId", out var ar) ? ar.GetInt32() : 2;
                    var (approveSuccess, approveError) = await _expenseService.ApproveExpenseAsync(approveId, approverId);
                    return JsonSerializer.Serialize(new { success = approveSuccess, error = approveError });

                case "reject_expense":
                    var rejectId = args.GetProperty("expenseId").GetInt32();
                    var rejecterId = args.TryGetProperty("reviewerId", out var rr) ? rr.GetInt32() : 2;
                    var (rejectSuccess, rejectError) = await _expenseService.RejectExpenseAsync(rejectId, rejecterId);
                    return JsonSerializer.Serialize(new { success = rejectSuccess, error = rejectError });

                case "get_categories":
                    var (categories, _) = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(categories);

                case "get_users":
                    var (users, _) = await _expenseService.GetUsersAsync();
                    return JsonSerializer.Serialize(users);

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static string GetSystemPrompt()
    {
        return @"You are an AI assistant for the Expense Management System. You help users manage their expenses by:

1. Viewing expenses (all, filtered by status or category, or pending approvals)
2. Creating new expenses
3. Approving or rejecting submitted expenses
4. Getting expense statistics

When showing lists of expenses, format them in a clear, readable way with:
- Date
- Category
- Amount (in £GBP)
- Status
- Description

Available categories: Travel, Meals, Supplies, Accommodation, Other
Available statuses: Draft, Submitted, Approved, Rejected

Users in the system:
- Alice Example (ID: 1) - Employee who submits expenses
- Bob Manager (ID: 2) - Manager who approves/rejects expenses

When creating expenses, default to Alice (userId: 1) unless specified otherwise.
When approving/rejecting, default to Bob Manager (reviewerId: 2) unless specified otherwise.

Be helpful and concise in your responses. Format currency as £XX.XX.";
    }
}
