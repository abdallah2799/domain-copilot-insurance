# Claims Adjudication Training Scenarios

**Document ID:** MMIC-TRAIN-SCEN-2025
**Audience:** New adjuster training
**Effective:** June 1, 2025

These worked scenarios walk a trainee through the five-step adjudication sequence from the Claims Adjudication Guidelines. They use illustrative facts, not the corpus's actual named policyholders, so trainees can be tested against the real claim file set separately.

## Scenario 1 — Straightforward Collision Claim

A policyholder with Collision coverage and a $500 deductible reports $3,200 in damage from a rear-end collision they caused. **Walkthrough:** (1) confirm the Declarations page and form version match the date of loss; (2) confirm Collision coverage is selected and note the $500 deductible; (3) check the Standard Exclusions Addendum — nothing in the narrative suggests racing, intentional damage, or business use; (4) compute payout = $3,200 − $500 = $2,700 via the deterministic calculation service; (5) draft a recommendation citing Policy Wording Section 5.1 and the Declarations page, and route to the adjuster for approval.

## Scenario 2 — Wrong-Form-Version Trap

A claim is reported in September 2025 for a loss that occurred in April 2025, on a policy that renewed onto PAP-2025-STD in July 2025. **Walkthrough:** a trainee who uses "today's" form version (PAP-2025-STD, since that's what the policy shows now) would incorrectly apply the $45/day rental limit and the ride-share exclusion language that did not exist at the time of loss. The correct approach is to determine which form version was in force in April 2025 — likely still PAP-2024-STD, if the policy had not yet renewed by then — and adjudicate under that version's terms, per Adjudication Guidelines Step 1 and Underwriting Guidelines Section 6.

## Scenario 3 — Glass Claim, Version-Dependent Outcome

Two policyholders each file a $600 windshield-only Comprehensive claim with a $500 deductible. One is governed by PAP-2024-STD, the other by PAP-2025-STD. **Walkthrough:** under PAP-2024-STD, no glass waiver exists, so payout = max($600 − $500, 0) = $100. Under PAP-2025-STD, the glass-only waiver (Section 5.4) applies since the loss is under $1,500 and glass-only, so the deductible is $0 and payout = $600. Identical facts, different governing form, different payout — this is exactly the scenario the version-matching step exists to get right.

## Scenario 4 — Ambiguous Ride-share Exclusion

A policyholder without the Ride-share/TNC Endorsement reports a collision and mentions, almost in passing, that they had been driving for a ride-share app that day. **Walkthrough:** the correct action is not to deny the claim outright, and not to approve it while ignoring the mention. Per Claims FAQ and Adjudication Guidelines Step 3, flag the claim for trip-status confirmation from the transportation network company before determining whether Exclusion 9 (PAP-EXCL-2025) applies. A trainee should recognize that "mentioned in the narrative" is not the same as "confirmed" — this is exactly the kind of ambiguous case that belongs in an evaluation set's adversarial category, not a case to guess on.

## Scenario 5 — Anomaly: Loss Predates the Policy

A claim's stated date of loss is before the policy's effective date shown on the Declarations page. **Walkthrough:** this is a hard stop, not a judgment call — per Adjudication Guidelines Section 3, this is an anomaly indicator requiring escalation to a supervisor rather than adjudication under the assumption that coverage was somehow in force. A trainee should never compute a payout for a claim in this state, even if all other facts look otherwise straightforward.

## Assessment Note for Trainers

These five scenarios map directly onto the kinds of cases a golden evaluation set should include: a clean case (Scenario 1), a version-sensitive case (Scenarios 2 and 3), an ambiguous case requiring escalation rather than a guess (Scenario 4), and a structural anomaly (Scenario 5). A system that handles all five correctly is doing the actual job this corpus exists to test.
