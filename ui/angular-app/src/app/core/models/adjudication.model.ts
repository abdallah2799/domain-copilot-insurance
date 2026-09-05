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

export const TERMINAL_RUN_STATUSES: ReadonlySet<AdjudicationRunStatus> = new Set([
  'Approved',
  'Rejected',
  'EditedAndApproved',
  'Failed',
]);

// Mirrors AdjudicationController's PipelineInProgressStatuses: once a run leaves this set (either
// a hard-terminal status, or AwaitingApproval, where the four-agent pipeline has nothing further
// to report), AdjudicationService.streamRun stops the connection itself rather than waiting for
// the server to end the response and risking EventSource's default auto-reconnect kicking in.
export const PIPELINE_IN_PROGRESS_STATUSES: ReadonlySet<AdjudicationRunStatus> = new Set([
  'Pending',
  'MatchingCoverage',
  'DetectingAnomalies',
  'AnalyzingExclusions',
  'Drafting',
]);

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
