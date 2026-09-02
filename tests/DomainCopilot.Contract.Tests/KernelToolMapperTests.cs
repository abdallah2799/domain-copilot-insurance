using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Providers;

namespace DomainCopilot.Contract.Tests;

/// <summary>
/// Contracts for turning our provider-agnostic tool schema (JSON Schema string) into the Semantic
/// Kernel function metadata the model actually sees. A silently-dropped required parameter here
/// would mean an agent's tool call schema doesn't match what we validate before execution.
/// </summary>
public class KernelToolMapperTests
{
    [Fact]
    public void ToKernelFunction_PreservesNameAndDescription()
    {
        var tool = new ToolDefinition("lookup_policy", "Looks up a policy by id", """{"type":"object","properties":{}}""");

        var function = KernelToolMapper.ToKernelFunction(tool);

        Assert.Equal("lookup_policy", function.Name);
        Assert.Equal("Looks up a policy by id", function.Description);
    }

    [Fact]
    public void ToKernelFunction_MapsEachSchemaPropertyToAParameter()
    {
        var tool = new ToolDefinition(
            "match_policy_version",
            "Finds the policy version effective on a given date",
            """
            {
              "type": "object",
              "properties": {
                "policyNumber": { "type": "string" },
                "effectiveDate": { "type": "string", "format": "date" }
              },
              "required": ["policyNumber"]
            }
            """);

        var function = KernelToolMapper.ToKernelFunction(tool);
        var parameters = function.Metadata.Parameters;

        Assert.Equal(2, parameters.Count);
        Assert.Equal(["policyNumber", "effectiveDate"], parameters.Select(p => p.Name));
    }

    [Fact]
    public void ToKernelFunction_MarksOnlyRequiredPropertiesAsRequired()
    {
        var tool = new ToolDefinition(
            "match_policy_version",
            "d",
            """{"type":"object","properties":{"a":{"type":"string"},"b":{"type":"string"}},"required":["a"]}""");

        var function = KernelToolMapper.ToKernelFunction(tool);
        var byName = function.Metadata.Parameters.ToDictionary(p => p.Name);

        Assert.True(byName["a"].IsRequired);
        Assert.False(byName["b"].IsRequired);
    }

    [Fact]
    public void ToKernelFunction_WithNoProperties_ProducesNoParameters()
    {
        var tool = new ToolDefinition("noop", "d", """{"type":"object"}""");

        var function = KernelToolMapper.ToKernelFunction(tool);

        Assert.Empty(function.Metadata.Parameters);
    }

    [Fact]
    public void ToKernelFunction_WithMalformedJsonSchema_ThrowsRatherThanRegisteringAMangledTool()
    {
        var tool = new ToolDefinition("broken", "d", "{not valid json");

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => KernelToolMapper.ToKernelFunction(tool));
    }
}
