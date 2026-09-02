# Meridian Mutual Insurance Company — Claims Adjudication Guidelines

**Document ID:** MMIC-GUIDE-ADJ-2025
**Audience:** Claims adjusters and automated adjudication support tools
**Effective:** June 1, 2025

## 1. Purpose and Scope

These guidelines describe the standard sequence an adjuster (human or system-assisted) follows to adjudicate a first-party Personal Auto claim under a Meridian Mutual policy. They apply to Collision, Comprehensive, and Liability claims reported under Policy Forms PAP-2024-STD and PAP-2025-STD. They do not modify the terms of any policy; the Policy Wording and Declarations page for the specific claim always govern.

## 2. The Five-Step Adjudication Sequence

### Step 1 — Match the Governing Policy Version

Identify the Declarations page and policy form version in force **on the date of loss**, not the current date or the date the claim is filed. A policy renewed after June 1, 2025 is governed by PAP-2025-STD; a policy period that began before that date and has not yet renewed remains governed by PAP-2024-STD, even if the claim is being adjudicated after June 1, 2025.

**This step is the single most common source of adjudication error.** Using the wrong form version can silently change the applicable deductible, exclusions, and claim-handling timelines. Confirm the effective date range on the Declarations page against the date of loss before proceeding to Step 2.

### Step 2 — Evaluate Coverage, Limits, and Deductibles

Confirm that the coverage part invoked by the loss (Liability, Medical Payments, UM/UIM, Collision, or Comprehensive) is actually selected on the policyholder's Declarations page — not every policyholder carries every coverage part (for example, a liability-only policy has no Collision coverage regardless of what the Policy Wording describes as available). Identify the applicable limit and deductible from the Declarations page, and apply any deductible waiver that the governing form version provides (for example, the glass-only deductible waiver introduced in PAP-2025-STD Section 5.4).

### Step 3 — Detect Applicable Exclusions

Cross-reference the claim description against the Standard Exclusions Addendum matching the governing form version (PAP-EXCL-2024 or PAP-EXCL-2025). Pay particular attention to ride-share/TNC use, business use, and excluded-driver scenarios, since these depend on facts stated in the claim narrative rather than on the Declarations page alone. If the claim narrative does not contain enough information to confirm or rule out an exclusion, do not assume either way — flag the claim for additional information rather than adjudicating on an assumption.

### Step 4 — Compute the Payout

All limit, deductible, and payout arithmetic is performed by the deterministic claims-calculation service, never estimated or computed by narrative judgment. The computation is: `payout = min(estimated_damage, applicable_limit) - applicable_deductible`, floored at zero, with the glass-only deductible waiver (where applicable) reducing the applicable deductible to $0 for that claim. Do not round, estimate, or adjust this figure manually; if a different figure appears warranted (e.g., a disputed damage estimate), that is a basis for requesting a re-inspection or appraisal under Policy Wording Section 8.2, not for manually overriding the computed figure.

### Step 5 — Draft the Recommendation and Escalate for Approval

Every adjudication recommendation — approve, deny, or partial-approve — is drafted with its supporting citations (policy section, exclusion, Declarations page line item) and routed to a human adjuster for approval before any payout is communicated to a policyholder or any payment is issued. No recommendation is final, and no payout instruction is executed, without explicit adjuster approval. This is a structural control, not a courtesy step — the system must not be able to bypass it under any configuration.

## 3. Anomaly Indicators

Flag a claim for closer adjuster review (in addition to the standard approval requirement) when any of the following are present:

- Estimated damage exceeds 60% of the vehicle's approximate market value (possible total loss — Section 5, Total Loss handling, not covered by this document).
- The claim narrative and the police report (where one exists) describe materially different sequences of events.
- The same policy number has more than one claim within a 90-day window.
- The claim narrative mentions ride-share, delivery, or fee-for-service use and the Declarations page does not list a corresponding endorsement.
- The date of loss falls outside the policy period shown on the Declarations page.

## 4. Documentation Requirements

Every adjudication record must retain: the Declarations page and policy form version used, the specific policy and exclusion sections cited, the exact inputs to the payout computation, and the identity of the approving adjuster and timestamp of approval. This is required both for the audit trail and to support the appraisal process under Policy Wording Section 8.2 if a policyholder disputes the outcome.

## 5. What This Document Does Not Cover

This document describes first-party Collision, Comprehensive, and Liability adjudication only. It does not cover Medical Payments claims involving third-party medical records, Uninsured/Underinsured Motorist claims requiring legal liability determination against a third party, or total-loss valuation methodology — those follow separate, more specialized guidance outside the scope of this corpus.
