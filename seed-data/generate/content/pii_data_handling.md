# Data Privacy and PII Handling Policy

**Document ID:** MMIC-PRIVACY-2025
**Audience:** All staff and any automated system processing policyholder or claimant data
**Effective:** June 1, 2025

## 1. Purpose

This policy governs how Meridian Mutual collects, stores, discloses, and retains personally identifiable information (PII) belonging to policyholders, claimants, and other individuals, including information processed by automated claims-adjudication tools.

## 2. What Counts as PII in This Context

Names, addresses, vehicle identification numbers (VINs), driver's license numbers, dates of birth, and any combination of data that could identify a specific individual are treated as PII. Policy numbers and claim numbers alone are not PII, but become PII when paired with the named insured's identity, which they normally are in our systems.

## 3. Minimum Necessary Access

Any system component — including an automated adjudication agent — that does not need to identify a specific individual to perform its function should not be given access to PII-bearing fields. Coverage and exclusion analysis against policy wording, for example, requires the policy's coverage selections and dates, not the named insured's identity; system design should reflect this separation where practical.

## 4. Prohibition on Real Data in Non-Production Systems

Test, training, demonstration, and development environments must never contain real policyholder or claimant PII. Synthetic data only. This is a hard requirement, not a guideline — a real-data leak into a non-production environment is treated as a reportable incident regardless of whether it was accidental.

## 5. Third-Party and AI Service Disclosure

Where policyholder or claimant data is sent to a third-party service — including a hosted large language model provider — as part of claims processing, that transmission must be documented: what data left our infrastructure, to which provider, and why. This applies equally to a hosted completion provider and a hosted embedding provider.

## 6. Retention and Deletion

PII is retained only as long as required for claims handling, applicable statutes of limitation, and regulatory recordkeeping requirements, after which it is deleted or irreversibly de-identified.

## 7. Relationship to Claims Adjudication

This policy does not change any coverage determination — it governs how claim and policyholder data is handled during adjudication, not what a policy covers. See the Claims Adjudication Guidelines for the coverage-determination process itself.
