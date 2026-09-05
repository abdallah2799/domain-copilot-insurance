// Mirrors the API's DTOs exactly (System.Text.Json's ASP.NET Core default: camelCase properties,
// enums as strings via JsonStringEnumConverter). Kept in one file since these types only ever
// travel together through the adjudication workflow endpoints.

export type AdjudicationRunStatus =
  | 'Pending'
  | 'MatchingCoverage'
  | 'DetectingAnomalies'
  | 'AnalyzingExclusions'
  | 'Drafting'
  | 'AwaitingApproval'
  | 'Approved'
  | 'Rejected'
  | 'EditedAndApproved'
  | 'Failed';

export interface AdjudicationCase {
  id: string;
  claimNumber: string;
  policyNumber: string;
  dateOfLoss: string;
  status: AdjudicationRunStatus;
  coverageMatchResultJson: string | null;
  anomalyFindingsJson: string | null;
  exclusionAnalysisResultJson: string | null;
  recommendationJson: string | null;
  approvedBy: string | null;
  approvedAtUtc: string | null;
  adjusterComments: string | null;
  failureReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface StartAdjudicationRequest {
  claimNumber: string;
  policyNumber: string;
  dateOfLoss: string;
  lossType: string;
  narrative: string;
  policeReportText: string | null;
  estimatedDamage: number;
  approximateVehicleValue: number;
}

export interface CoverageMatchResult {
  formVersion: string;
  formVersionEffectiveDate: string;
  coveragePart: string;
  coveragePartSelected: boolean;
  applicableLimit: number | null;
  applicableDeductible: number | null;
  glassOnlyDeductibleWaiverApplies: boolean;
  endorsementsHeld: string[];
  citations: string[];
  notes: string | null;
}

export interface AnomalyFindings {
  damageToValueRatioExceeds60Percent: boolean;
  duplicateClaimsWithin90Days: boolean;
  duplicateClaimNumbers: string[];
  dateOfLossBeforePolicyEffectiveDate: boolean;
  narrativePoliceReportMismatch: boolean;
  gigEconomyUseMentioned: boolean;
  gigEconomyEndorsementPresent: boolean;
  summary: string;
  citations: string[];
}

export interface ExclusionAnalysisResult {
  exclusionsApply: boolean;
  applicableExclusions: string[];
  insufficientInformation: boolean;
  reasoning: string;
  citations: string[];
}

export interface Recommendation {
  recommendationType: string;
  payoutAmount: number | null;
  payoutToolUsed: string | null;
  summary: string;
  citations: string[];
}

export interface ApprovalRequest {
  actor: string;
  comments?: string | null;
}

export interface EditAndApproveRequest {
  actor: string;
  comments: string;
  editedRecommendationJson: string;
}
