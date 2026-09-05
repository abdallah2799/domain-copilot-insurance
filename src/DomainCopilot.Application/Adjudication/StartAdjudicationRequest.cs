namespace DomainCopilot.Application.Adjudication;

public sealed record StartAdjudicationRequest(
    string ClaimNumber,
    string PolicyNumber,
    DateOnly DateOfLoss,
    string LossType,
    string Narrative,
    string? PoliceReportText,
    decimal EstimatedDamage,
    decimal ApproximateVehicleValue);
