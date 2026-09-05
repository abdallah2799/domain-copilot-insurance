// Mirrors ObservabilityController's DTOs (Application.Observability.TokenUsageReport/TokenUsageSummary).

export interface TokenUsageSummary {
  timestampUtc: string;
  correlationId: string;
  agentName: string;
  providerName: string;
  modelName: string;
  promptTokens: number;
  completionTokens: number;
  estimatedCostUsd: number;
}

export interface TokenUsageReport {
  recentEntries: TokenUsageSummary[];
  totalPromptTokens: number;
  totalCompletionTokens: number;
  totalEstimatedCostUsd: number;
}
