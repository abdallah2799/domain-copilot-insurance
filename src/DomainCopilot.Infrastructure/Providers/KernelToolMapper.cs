using System.Text.Json;
using DomainCopilot.Application.Providers;
using Microsoft.SemanticKernel;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>
/// Maps our provider-agnostic <see cref="ToolDefinition"/> (name, description, raw JSON Schema)
/// onto Semantic Kernel's <see cref="KernelFunction"/> metadata, so the model sees the tool without
/// Semantic Kernel ever being allowed to invoke it — invocation always goes through the approval
/// gate in Application, never automatically (see <c>FunctionChoiceBehavior.Auto(autoInvoke: false)</c>
/// at the call site).
/// </summary>
internal static class KernelToolMapper
{
    public static KernelFunction ToKernelFunction(ToolDefinition tool)
    {
        var parameters = new List<KernelParameterMetadata>();

        using var schemaDoc = JsonDocument.Parse(tool.JsonSchemaParameters);
        var root = schemaDoc.RootElement;

        var required = new HashSet<string>();
        if (root.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in requiredElement.EnumerateArray())
            {
                var name = item.GetString();
                if (name is not null)
                {
                    required.Add(name);
                }
            }
        }

        if (root.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                parameters.Add(new KernelParameterMetadata(property.Name)
                {
                    Schema = KernelJsonSchema.Parse(property.Value.GetRawText()),
                    IsRequired = required.Contains(property.Name)
                });
            }
        }

        return KernelFunctionFactory.CreateFromMethod(
            NeverInvoked,
            new KernelFunctionFromMethodOptions
            {
                FunctionName = tool.Name,
                Description = tool.Description,
                Parameters = parameters
            });
    }

    // Never called: tools are declared with autoInvoke: false so we can inspect/gate calls
    // ourselves before anything side-effecting runs.
    private static string NeverInvoked() =>
        throw new InvalidOperationException("Semantic Kernel should never auto-invoke a declared tool; invocation is gated in Application.");
}
