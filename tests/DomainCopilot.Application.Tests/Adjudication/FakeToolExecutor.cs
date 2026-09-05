using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Tests.Adjudication;

internal sealed class FakeToolExecutor(string name, Func<string, ToolExecutionResult> execute) : IToolExecutor
{
    public int CallCount { get; private set; }

    public ToolDefinition Definition { get; } = new(name, "A fake tool for AgentRunner tests.", """{"type":"object","properties":{}}""");

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(execute(argumentsJson));
    }
}
