using DomainCopilot.Application.Adjudication;
using DomainCopilot.Application.Providers;
using DomainCopilot.Application.Tests.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace DomainCopilot.Application.Tests.Adjudication;

public class AgentRunnerTests
{
    private sealed record TestOutput(string Value);

    private static AgentRunner NewRunner(SequencedFakeCompletionService completionService, FakeTokenUsageRecorder? tokenUsageRecorder = null) =>
        new(completionService, tokenUsageRecorder ?? new FakeTokenUsageRecorder(), NullLogger<AgentRunner>.Instance);

    private static CompletionResult FinalAnswer(string content) =>
        new(content, [], TokenUsage.Zero, "fake", "fake-model");

    private static CompletionResult ToolCallTurn(params ToolCall[] toolCalls) =>
        new(null, toolCalls, TokenUsage.Zero, "fake", "fake-model");

    [Fact]
    public async Task RunAsync_NoToolCalls_ParsesFinalJsonImmediately()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => FinalAnswer("""{"value":"done"}"""));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("done", result.Output!.Value);
        Assert.Equal(1, result.IterationsUsed);
    }

    [Fact]
    public async Task RunAsync_OneToolCallThenFinalAnswer_ExecutesToolAndReturnsFinalResult()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => ToolCallTurn(new ToolCall("call-1", "my_tool", "{}")));
        completion.Enqueue(() => FinalAnswer("""{"value":"after-tool"}"""));
        var tool = new FakeToolExecutor("my_tool", _ => ToolExecutionResult.Ok("""{"result":42}"""));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [tool], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("after-tool", result.Output!.Value);
        Assert.Equal(1, tool.CallCount);
        Assert.Equal(2, result.IterationsUsed);
    }

    [Fact]
    public async Task RunAsync_MultipleToolCallsInOneTurn_ExecutesAllOfThem()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => ToolCallTurn(
            new ToolCall("call-1", "tool_a", "{}"),
            new ToolCall("call-2", "tool_b", "{}")));
        completion.Enqueue(() => FinalAnswer("""{"value":"done"}"""));
        var toolA = new FakeToolExecutor("tool_a", _ => ToolExecutionResult.Ok("{}"));
        var toolB = new FakeToolExecutor("tool_b", _ => ToolExecutionResult.Ok("{}"));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [toolA, toolB], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal(1, toolA.CallCount);
        Assert.Equal(1, toolB.CallCount);
    }

    [Fact]
    public async Task RunAsync_ExceedsMaxIterations_Fails()
    {
        var completion = new SequencedFakeCompletionService();
        for (var i = 0; i < 5; i++)
        {
            completion.Enqueue(() => ToolCallTurn(new ToolCall($"call-{i}", "my_tool", "{}")));
        }

        var tool = new FakeToolExecutor("my_tool", _ => ToolExecutionResult.Ok("{}"));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [tool], maxIterations: 3);

        Assert.False(result.Success);
        Assert.Contains("max iterations", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_MalformedFinalJson_Fails()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => FinalAnswer("this is not json"));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [], maxIterations: 5);

        Assert.False(result.Success);
        Assert.Contains("non-conforming", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_FinalAnswerWrappedInMarkdownCodeFence_StillParses()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => FinalAnswer("```json\n{\"value\":\"fenced\"}\n```"));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("fenced", result.Output!.Value);
    }

    [Fact]
    public async Task RunAsync_ToolCallForUnknownTool_FeedsErrorBackAndContinues()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => ToolCallTurn(new ToolCall("call-1", "nonexistent_tool", "{}")));
        completion.Enqueue(() => FinalAnswer("""{"value":"recovered"}"""));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("recovered", result.Output!.Value);
    }

    [Fact]
    public async Task RunAsync_ToolExecutionFails_FeedsErrorBackRatherThanAbortingTheRun()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => ToolCallTurn(new ToolCall("call-1", "my_tool", "{}")));
        completion.Enqueue(() => FinalAnswer("""{"value":"handled-the-error"}"""));
        var tool = new FakeToolExecutor("my_tool", _ => ToolExecutionResult.Failed("simulated tool failure"));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [tool], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("handled-the-error", result.Output!.Value);
    }

    [Fact]
    public async Task RunAsync_TransientCompletionFailure_RetriesAndEventuallySucceeds()
    {
        var completion = new SequencedFakeCompletionService();
        completion.EnqueueThrow(new InvalidOperationException("transient failure"));
        completion.Enqueue(() => FinalAnswer("""{"value":"succeeded-after-retry"}"""));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("succeeded-after-retry", result.Output!.Value);
        Assert.Equal(2, completion.CallCount);
    }

    [Fact]
    public async Task RunAsync_CompletionFailsEveryRetry_FailsWithClearMessage()
    {
        var completion = new SequencedFakeCompletionService();
        completion.EnqueueThrow(new InvalidOperationException("down"));
        completion.EnqueueThrow(new InvalidOperationException("still down"));
        completion.EnqueueThrow(new InvalidOperationException("still down"));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [], maxIterations: 5);

        Assert.False(result.Success);
        Assert.Contains("completion call failed", result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_ToolCallEmittedAsTextInsteadOfStructuredCall_IsRecoveredAndExecuted()
    {
        // Reproduces a real failure observed against a local Ollama model: no structured ToolCalls
        // on the completion, but the content contains prose followed by a JSON object shaped like a
        // tool call the model clearly intended to make.
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => FinalAnswer(
            "The policyholder has Collision coverage with a $500 deductible.\n\n" +
            """{"name": "my_tool", "parameters": {"query": "test"}}"""));
        completion.Enqueue(() => FinalAnswer("""{"value":"done-after-recovery"}"""));
        var tool = new FakeToolExecutor("my_tool", _ => ToolExecutionResult.Ok("""{"result":"ok"}"""));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [tool], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("done-after-recovery", result.Output!.Value);
        Assert.Equal(1, tool.CallCount);
    }

    [Fact]
    public async Task RunAsync_ToolCallEmittedAsTextForUnknownTool_FeedsErrorBackAndContinues()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => FinalAnswer("""{"name": "nonexistent_tool", "arguments": {}}"""));
        completion.Enqueue(() => FinalAnswer("""{"value":"recovered-anyway"}"""));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("recovered-anyway", result.Output!.Value);
    }

    [Fact]
    public async Task RunAsync_FinalAnswerPrecededByProseReasoning_RecoversTheTrailingJsonObject()
    {
        // Reproduces a second real failure observed against a local Ollama model: the model reasoned
        // through each field in plain prose (no code fence at all) before finally writing the
        // correct JSON answer as the very last thing in its content — direct parsing of the whole
        // content fails, but the trailing JSON object is itself perfectly valid.
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => FinalAnswer(
            "Since the policyholder has coverage, let's walk through the fields.\n\n" +
            "The value should be 'done'.\n\n" +
            "Therefore, the final output is:\n\n" +
            """{"value":"done"}"""));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("done", result.Output!.Value);
    }

    [Fact]
    public async Task RunAsync_FinalJsonWithNoNameField_IsNotMisdetectedAsATextEmbeddedToolCall()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => FinalAnswer("""{"value":"a perfectly normal final answer"}"""));
        var runner = NewRunner(completion);

        var result = await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [], maxIterations: 5);

        Assert.True(result.Success);
        Assert.Equal("a perfectly normal final answer", result.Output!.Value);
    }

    [Fact]
    public async Task RunAsync_EachCompletionCall_RecordsRealTokenUsage()
    {
        var completion = new SequencedFakeCompletionService();
        completion.Enqueue(() => new CompletionResult("""{"value":"tool-turn"}""", [new ToolCall("call-1", "my_tool", "{}")], new TokenUsage(100, 20), "fake-provider", "fake-model"));
        completion.Enqueue(() => new CompletionResult("""{"value":"done"}""", [], new TokenUsage(150, 30), "fake-provider", "fake-model"));
        var tool = new FakeToolExecutor("my_tool", _ => ToolExecutionResult.Ok("""{"result":42}"""));
        var recorder = new FakeTokenUsageRecorder();
        var runner = NewRunner(completion, recorder);

        await runner.RunAsync<TestOutput>("TestAgent", "system", "user", [tool], maxIterations: 5);

        Assert.Equal(2, recorder.RecordedEntries.Count);
        Assert.All(recorder.RecordedEntries, e => Assert.Equal("TestAgent", e.AgentName));
        Assert.Equal([(100, 20), (150, 30)], recorder.RecordedEntries.Select(e => (e.PromptTokens, e.CompletionTokens)));
    }
}
