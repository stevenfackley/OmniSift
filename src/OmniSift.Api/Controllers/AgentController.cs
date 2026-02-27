// ============================================================
// OmniSift.Api — Agent Controller
// Handles research queries via Semantic Kernel agent
// ============================================================

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OmniSift.Api.Data;
using OmniSift.Api.Middleware;
using OmniSift.Api.Models;
using OmniSift.Shared.DTOs;

namespace OmniSift.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("per-tenant")]
public sealed class AgentController(
    Kernel kernel,
    OmniSiftDbContext dbContext,
    ITenantContext tenantContext,
    ILogger<AgentController> logger) : ControllerBase
{
    /// <summary>
    /// System prompt for the research agent.
    /// </summary>
    private const string SystemPrompt = """
        You are OmniSift, an AI research assistant. You help users find and analyze information
        from their uploaded documents and the web.

        Guidelines:
        - When answering questions, ALWAYS search the user's documents first using SearchDocuments.
        - If the documents don't contain sufficient information, supplement with web search using SearchWeb.
        - For questions about historical web pages, use GetArchivedPage to check the Wayback Machine.
        - Always cite your sources clearly. For document results, mention the file name and relevance.
        - For web results, include the URL.
        - Be thorough but concise. Present findings in a structured manner.
        - If you cannot find relevant information, say so honestly rather than speculating.
        - When presenting multiple pieces of information, organize them logically.
        """;

    /// <summary>
    /// Submit a research query to the AI agent.
    /// The agent will search documents, the web, and archives as needed.
    /// </summary>
    [HttpPost("query")]
    public async Task<ActionResult<AgentQueryResponse>> Query(
        [FromBody] AgentQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var sw = Stopwatch.StartNew();

        logger.LogInformation(
            "Agent query from tenant {TenantId}: {QueryPreview}",
            tenantContext.TenantId,
            request.Query.Length > 100 ? request.Query[..100] + "..." : request.Query);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        // Build chat history
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(SystemPrompt);

        // Add conversation history if provided
        if (request.ConversationHistory is not null)
        {
            foreach (var msg in request.ConversationHistory)
            {
                switch (msg.Role.ToLowerInvariant())
                {
                    case "user":
                        chatHistory.AddUserMessage(msg.Content);
                        break;
                    case "assistant":
                        chatHistory.AddAssistantMessage(msg.Content);
                        break;
                }
            }
        }

        // Add the current query
        chatHistory.AddUserMessage(request.Query);

        // Configure auto function calling
        var executionSettings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        // Execute with Semantic Kernel
        var result = await chatService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            kernel,
            cancellationToken);

        sw.Stop();

        // Extract plugins used from function call results in chat history
        var pluginsUsed = chatHistory
            .Where(m => m.Role == AuthorRole.Tool)
            .Select(m => m.Metadata?.GetValueOrDefault("ChatCompletionsFunctionToolCall.Name")?.ToString())
            .Where(name => name is not null)
            .Distinct()
            .Cast<string>()
            .ToList();

        var responseText = result.Content ?? "I was unable to generate a response.";

        // Save to query history
        var queryHistory = new QueryHistory
        {
            TenantId = tenantContext.TenantId,
            QueryText = request.Query,
            ResponseText = responseText,
            PluginsUsed = pluginsUsed,
            DurationMs = (int)sw.ElapsedMilliseconds
        };

        dbContext.QueryHistories.Add(queryHistory);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Agent query completed in {DurationMs}ms, plugins={Plugins}",
            sw.ElapsedMilliseconds, string.Join(", ", pluginsUsed));

        return Ok(new AgentQueryResponse
        {
            Response = responseText,
            PluginsUsed = pluginsUsed,
            DurationMs = (int)sw.ElapsedMilliseconds,
            Sources = [] // Sources are embedded in the response text by the agent
        });
    }
}
