# AI Proposal Evaluation System

host

## Problem

This year IOM issued an RFQ to revamp two live regional websites — the MRP site and the DoMV Toolbox — plus a DoMV application feasibility assessment. About **21 vendors** submitted proposals.

Internal process requires **three evaluators**, all IOM staff. For each bid they must:

1. Read the full proposal package (technical plan, CVs, portfolio, permit, financials).
2. Judge how well it fits the TOR (platforms, feasibility study, WCAG, CMS, 3-month output-based delivery, and the rest of Section J).
3. Score each criterion on a 0–1 quality scale, apply weights, and produce a technical total.

That is slow. Staff time is spent on first-pass reading and scoring that could be drafted by a system, then checked.

## Goal

Build a well-engineered application that prepares the evaluation, then agents that score it on request.

**The app** (not the agents) owns intake:

- Create a **profile** for each vendor.
- Load whatever evidence they submitted (technical plan, CVs, portfolio, permit, financials, and anything else in the pack).
- Hold the TOR and scoring strategy in a known place so evaluators and agents use the same files.
- Mark profiles ready when the pack is loaded.

**The agents** start only after a person **requests evaluation** (once all profiles are ready, or for a chosen vendor). They already know where to find each vendor’s documents and the TOR. They score, justify, and rank. They do not ingest folders or invent profiles.

**A human stays in the loop:** an evaluator can open any company, inspect scores and evidence, adjust if needed, and **approve the final result**.

The system does not replace the evaluation committee. Staff spend time on setup, review, and decision — not on first reading of 21 packs.

## This assignment (context for the POC)


| Item                  | Detail                                                                                                |
| --------------------- | ----------------------------------------------------------------------------------------------------- |
| Buyer                 | IOM (BMM Programme / Regional MRP)                                                                    |
| Solicitation          | RFQ 30000027877                                                                                       |
| Scope                 | Redevelop MRP (`mrp-easternroute.com`) and DoMV Toolbox (`domvtoolbox.iom.int`); DoMV app feasibility |
| Delivery              | Output-based, 3 months, home-based                                                                    |
| Combined method       | Technical **70%** / Financial **30%**                                                                 |
| Official TOR scale    | Technical 1000 points (Forms 1–3)                                                                     |
| This evaluation scale | Technical **70** points (same 70%)                                                                    |


People write the scoring strategy as a text file **outside** `POC/`. The one provided so far is `../SCORING_STRATEGY.md`. Staff load that file onto the RFQ. This POC must use that model, not a new one.

## Scoring model

Each criterion is scored on a **0.00–1.00** quality scale, then multiplied by its **weight**. These three criteria are the technical 70 points (TOR technical 70%).


| ID                  | Criterion (this evaluation)                                                                           | Maps to TOR                                                                     | Weight | Max points |
| ------------------- | ----------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- | ------ | ---------- |
| **C1**              | Proposal plan and fit                                                                                 | Form 2 — Methodology, approach and implementation plan (400 pts)                | 30     | 30         |
| **C2**              | Qualifications package: government permit + specific experience/expertise + organization and staffing | Form 3 — Management structure and key personnel (300 pts), plus permit evidence | 20     | 20         |
| **C3**              | Experience of the firm (relevant experience)                                                          | Form 1 — Expertise of the firm/organization (300 pts)                           | 20     | 20         |
| **Total technical** |                                                                                                       |                                                                                 | **70** | **70**     |


**Weighted points** = `score (0–1) × weight`  
**Total Weighted Score (TWS)** = C1 + C2 + C3 points (0–70)  
**Normalized technical %** = `TWS / 70 × 100`  
**TOR-equivalent technical points** = `TWS / 70 × 1000` (for comparison with the TOR 1000-point scale)

Ranking of *eligible* bids is by TWS. Financial score, if evaluated later: `FS = lowest evaluated price / this bidder’s price`; **Final Score = (TWS/70 × 70) + (FS × 30)**.

### The five quality scores

C2 is one weighted criterion. It is the **average** of three 0–1 items. The system records and displays all five scores (as in `processed/SCORES.md`):


| Display name                         | ID  | Role in TWS        |
| ------------------------------------ | --- | ------------------ |
| 1. Proposal plan                     | C1  | `C1 × 30`          |
| 2. Valid government permit           | C2a | part of C2 average |
| 3. Specific experience and expertise | C2b | part of C2 average |
| 4. Organization and staffing         | C2c | part of C2 average |
| 5. Relevant experience               | C3  | `C3 × 20`          |


```
C2_score = (C2a + C2b + C2c) / 3
C2 points = C2_score × 20
TWS = (C1 × 30) + (C2_score × 20) + (C3 × 20)
```

Technical pass line: **TWS ≥ 49.00 / 70** (70% of technical points). Mandatory fail (incomplete Section J, no relevant portfolio, no signed TOR / 3-month timeline) still gets a score for the record but is **not eligible** to rank.

## What the system must do



### 1. The application — profiles and evidence

Ordinary product engineering. No agents at this stage.

- Create and store a **vendor profile** (name, project ID, status).
- Attach and store the files they provided; map them to TOR Section J where that is obvious from the upload (technical, financial, CVs, portfolio, permit).
- Store the TOR and scoring strategy once for the solicitation.
- Show what is present vs missing on each profile.
- Evaluation is **not** started automatically. A person requests it when the profiles they care about are ready.

Agents never crawl a raw drop folder. They read from the profiles and document store the app already built.

### 2. Agents — evaluation on request

Triggered only after “evaluate” (one vendor or the full ready set). Agents read from the profile, attached evidence, TOR, and scoring strategy — paths the app already knows.

**First step, for each vendor: summarize the proposal.** Write a readable brief of what they actually submitted — understanding of MRP/DoMV, approach, team, portfolio, permit, financials, and obvious gaps — before any scores are assigned.

**Summary gate.** An evaluator accepts or rejects each summary before scoring starts. Scoring uses only an **accepted** summary plus the source documents. A rejected summary is not scored (see below). Accept/reject is per vendor; other vendors continue.

Then, per vendor with an accepted summary:

1. **Fit** — compare the summary and plan to the TOR (both sites, DoMV feasibility, WCAG, CMS, 3-month calendar).
2. **Score** — assign C1, C2a, C2b, C2c, C3 with a short justification against the strategy bands (0.00 missing → 1.00 excellent).
3. **Compute** — C2 average, TWS, pass/fail, recommendation.
4. **Compare** — update a master score table and eligible ranking.

Agents score only what is on the profile. Missing or unreadable evidence is 0.00, not assumed.

### If a summary is not approved

The vendor status is **summary rejected**. They get no C1–C3 scores and are not in the ranking. The evaluator leaves a short reason. Then: fix the profile and request a new summary; re-run the summary agent on the same documents; or leave them out of this run.

### 3. Human in the loop — review and approval

Two checkpoints:

1. **Summary** — accept or reject before scoring (per vendor).
2. **Scores** — after scoring, an evaluator sees the ranked list, TWS, and notes; can open any company, read the proposal summary, and spot-check the same evidence the app loaded; can change a score (TWS recalculates); then **approve** the final result (per company and/or the whole evaluation).

Three IOM evaluators remain the accountable committee. The AI output is a draft until a human approves it.

## Flow

```
Load TOR + scoring strategy
        ↓
Create vendor profiles and attach evidence
        ↓
Profiles marked ready
        ↓
Person requests evaluation
        ↓
Agents summarize each vendor’s proposal
        ↓
Evaluator accepts or rejects each summary
        ↓
Accepted → agents score using the summary, profile, and TOR
Rejected → no scores; fix profile, re-summarize, or leave out
        ↓
Evaluator reviews scores, edits if needed, approves
```



## Why this is worth building


| Today                                         | With the system                                                                             |
| --------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Packs sit in email / shared folders           | App holds a profile and evidence per vendor                                                 |
| Three staff read ~21 packs against the TOR    | When ready, agents summarize each proposal; scoring starts only after a summary is accepted |
| Scoring is slow and hard to keep consistent   | Same strategy applied to every ready profile                                                |
| Comparison lives in spreadsheets / notes      | One score table and ranking, recalculated on edit                                           |
| Review happens only after all reading is done | Review starts after the requested evaluation run                                            |


The POC in this folder is the place to design and build that pipeline. Existing scored bids under `processed/` are the reference set for checking that agent scores stay close to the agreed model.