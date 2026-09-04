# Claims Data Retention and Access Control Guide

**Document ID:** MMIC-RETENTION-2025
**Audience:** Claims operations and IT governance
**Effective:** June 1, 2025

*This guide expands on the PII Data Handling Standard with the specific retention schedule and access-control rules for claims records.*

## 1. Retention Schedule

A closed claim file is retained for the period required by Ohio insurance record-retention regulation, referenced in the Ohio Auto Insurance Regulatory Compendium, measured from the claim's closure date rather than the date of loss — a claim that remained open for an extended period (for example, due to litigation under the Litigation and Legal Referral Guide) has its retention clock start later than a claim of the same loss date that closed quickly.

## 2. Litigation Hold Exception

Where a claim is subject to a litigation hold (active or reasonably anticipated litigation, per the Litigation and Legal Referral Guide), the standard retention schedule in Section 1 is suspended and the file is retained until the hold is released by legal counsel, regardless of how much time has passed since closure.

## 3. Access Control Principle

Access to a claim file is governed by role and need, not by general employee status — an adjuster has access to claims assigned to them and, subject to supervisor approval, claims within their team; claims operations leadership has broader access for audit and reporting purposes described in the Claims Quality Assurance and Audit Program; access outside these roles requires a specific, documented business reason.

## 4. Access Logging

Access to a claim file's sensitive components (medical records, SIU referral details, financial account information collected for subrogation or lienholder payoff) is logged, consistent with the audit-trail expectation in the PII Data Handling Standard, so that an access pattern inconsistent with an employee's role is detectable during a periodic access review.

## 5. Vendor Access

A third-party vendor (a repair shop, a total-loss valuation vendor, a legal counsel firm) receives only the specific claim data necessary for their role in the claim, not full file access — a repair shop, for example, receives vehicle and damage information but not the policyholder's financial account details collected for other purposes on the same claim.

## 6. De-Identification for Aggregate Reporting

Where claim data is used for aggregate reporting (the quality audit program's finding-rate trends, the reserving guide's portfolio-level adequacy review), individual policyholder-identifying fields are not required and are excluded from the aggregate dataset, consistent with the minimization principle in the PII Data Handling Standard.

## 7. Data Deletion at End of Retention

At the end of the applicable retention period (Section 1), and where no litigation hold applies (Section 2), claim file data is deleted or archived according to Meridian Mutual's records management schedule; deletion is documented so that the retention program itself is auditable.

## 8. Training Corpus Exclusion

Consistent with the knowledge-versus-case-data architectural separation used throughout this corpus, individual claim files — regardless of retention status — are never included in any knowledge base used for policy interpretation or general claims guidance; only the reference documents in this corpus (policy wordings, guides, manuals) serve that function, keeping policyholder-specific data out of any system that could surface it in an unrelated context.
