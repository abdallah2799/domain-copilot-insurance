using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DomainCopilot.Contract.Tests;

/// <summary>
/// Verifies <c>SemanticKernelCompletionAdapter.BuildHistory</c> represents a multi-turn tool-calling
/// loop's own prior tool-call requests correctly — before this, an Assistant message's
/// <c>ToolCalls</c> had no way to reach Semantic Kernel's <c>ChatHistory</c> at all, so a follow-up
/// call would send tool results with no matching prior tool-call entry, which a
/// standards-compliant chat API can reject. This is exactly the shape an agent's tool-calling loop
/// depends on (send tool calls → append tool results → call again), so it's checked directly here
/// rather than only implicitly through a live call.
/// </summary>
public class SemanticKernelCompletionAdapterHistoryTests
{
    [Fact]
    public void BuildHistory_AssistantMessageWithToolCalls_PreservesFunctionCallIdAndName()
    {
        var request = new CompletionRequest([
            ChatMessage.System("system prompt"),
            ChatMessage.User("do the thing"),
            ChatMessage.Assistant("", [new ToolCall("call-1", "calculate_standard_payout", """{"estimatedDamage":3000}""")]),
            ChatMessage.ToolResult("call-1", """{"payout":2500}"""),
        ]);

        var history = SemanticKernelCompletionAdapter.BuildHistory(request);

        var assistantMessage = history.Single(m => m.Role == AuthorRole.Assistant);
        var functionCall = assistantMessage.Items.OfType<FunctionCallContent>().Single();
        Assert.Equal("call-1", functionCall.Id);
        Assert.Equal("calculate_standard_payout", functionCall.FunctionName);
    }

    [Fact]
    public void BuildHistory_AssistantMessageWithNoToolCalls_AddsPlainTextMessage()
    {
        var request = new CompletionRequest([
            ChatMessage.System("system prompt"),
            ChatMessage.Assistant("the final answer"),
        ]);

        var history = SemanticKernelCompletionAdapter.BuildHistory(request);

        var assistantMessage = history.Single(m => m.Role == AuthorRole.Assistant);
        Assert.Equal("the final answer", assistantMessage.Content);
        Assert.Empty(assistantMessage.Items.OfType<FunctionCallContent>());
    }

    [Fact]
    public void BuildHistory_ToolResultMessage_CarriesTheMatchingCallId()
    {
        var request = new CompletionRequest([
            ChatMessage.Assistant("", [new ToolCall("call-1", "lookup_declarations", "{}")]),
            ChatMessage.ToolResult("call-1", """{"formVersion":"PAP-2024-STD"}"""),
        ]);

        var history = SemanticKernelCompletionAdapter.BuildHistory(request);

        var toolMessage = history.Single(m => m.Role == AuthorRole.Tool);
        var functionResult = toolMessage.Items.OfType<FunctionResultContent>().Single();
        Assert.Equal("call-1", functionResult.CallId);
    }

    [Fact]
    public void BuildHistory_MultipleToolCallsInOneAssistantTurn_PreservesAllOfThem()
    {
        var request = new CompletionRequest([
            ChatMessage.Assistant("", [
                new ToolCall("call-1", "determine_total_loss", "{}"),
                new ToolCall("call-2", "search_knowledge_base", "{}"),
            ]),
        ]);

        var history = SemanticKernelCompletionAdapter.BuildHistory(request);

        var assistantMessage = history.Single(m => m.Role == AuthorRole.Assistant);
        var functionCalls = assistantMessage.Items.OfType<FunctionCallContent>().ToList();
        Assert.Equal(2, functionCalls.Count);
        Assert.Contains(functionCalls, f => f.Id == "call-1" && f.FunctionName == "determine_total_loss");
        Assert.Contains(functionCalls, f => f.Id == "call-2" && f.FunctionName == "search_knowledge_base");
    }
}
