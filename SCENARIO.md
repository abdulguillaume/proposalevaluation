# Scenario and validation criteria

## Scenario

An IOM staff member must evaluate vendor proposals for **RFQ 30000027877**: redesign of the MRP and DoMV Toolbox websites, plus a DoMV app feasibility study. About 21 vendors submitted packs. Three internal evaluators would normally read every pack against the TOR and score it. This system drafts that work.

**Actors**

- **Staff** — creates the RFQ, loads the TOR and scoring strategy, creates vendor profiles, attaches evidence.
- **Evaluator** — requests evaluation, accepts or rejects each summary, reviews scores, approves the result.
- **Agents** (not in the current mock) — summarize first, then score only after a summary is accepted.

**Story**

1. Staff creates RFQ `30000027877` (title: redesign of MRP and DoMV Toolbox websites).
2. People write the scoring strategy as a **text file outside `POC/`**. The one provided so far is `SCORING_STRATEGY.md` at the repo root. Staff then **load that file** onto the RFQ, together with the TOR (`solicitation_attachments/TOR.txt`). The app does not invent the strategy. Those two uploaded files are the only rules agents may use.
3. Staff adds vendor profiles (name + project ID) and loads whatever each vendor sent: technical proposal, financials, CVs, portfolio, permit, other.
4. The profile shows what is present vs missing (TOR Section J). Missing evidence is not invented later.
5. When the profiles they care about are ready, staff (or the evaluator) clicks **Start evaluation**. Nothing starts before that.
6. The app writes an evaluation run and one **Summarize** job per vendor. The UI shows the job status and *The agent has started working.*
7. When agents exist: the summary agent writes a proposal summary per vendor. The evaluator accepts or rejects each summary. Rejected vendors are not scored. Accepted vendors go to the scoring agent (summary + documents + TOR + strategy).
8. The evaluator can change a score; TWS is recalculated in the app. They approve the final result. The committee remains accountable.

**Example vendors for a first pass**

| Project ID | Vendor | Why they are useful |
|------------|--------|---------------------|
| 228711 | Vardot | Strong pack; eligible in the gold set |
| 228869 | Maison Interactive | Eligible; cheaper than Vardot |
| 228841 | YAIDEV Limited | Weak / non-responsive pack |
| 230588 | Argusoft India Limited | High technical score but mandatory fail (no price) |

Gold scores and notes live in `processed/SCORES.md`. Use those packs when checking that later agents stay close to the agreed model.

---

## Validation criteria

### A. Application (current mock — no agents)

| # | Criterion | Pass |
|---|-----------|------|
| A1 | Staff can create an RFQ (number, title, description) | RFQ appears in the list with status **Draft** |
| A2 | Staff can load a TOR and a scoring strategy written outside `POC/` (text file; current example `SCORING_STRATEGY.md`) | Both show as loaded; each kind can be replaced; strategy content is the uploaded file, not something the app generated |
| A3 | Evaluation cannot start without TOR, strategy, and at least one vendor | Button stays disabled; a clear message if forced |
| A4 | Staff can add a vendor profile (name, project ID) under the RFQ | Profile opens; RFQ becomes **Ready** once TOR + strategy + a vendor exist |
| A5 | Staff can load typed vendor documents (technical, financial, CVs, portfolio, permit, other) | Checklist shows present vs missing; files can be downloaded and removed |
| A6 | Evaluation does not start by itself | No jobs until **Start evaluation** |
| A7 | Start evaluation creates work the agent will read | One **EvaluationRun** (Running, *The agent has started working.*); one **Summarize** job per vendor, status **Queued**; vendor status **Summarizing**; RFQ status **Evaluating** |
| A8 | Starting again while a run is active does not duplicate jobs | Same run and message; no second queue |
| A9 | UI status matches the job table | What you see on the RFQ page is `tbl_EvaluationRuns` / `tbl_EvaluationJobs` / vendor status |
| A10 | Agents do not ingest drop folders | Jobs only point at RFQ + vendor IDs and stored files |

### B. Summary gate (when the summary agent exists)

| # | Criterion | Pass |
|---|-----------|------|
| B1 | First agent step is a summary, not a score | `tbl_ProposalSummaries` row exists before any `tbl_VendorEvaluations` / `tbl_VendorScores` row |
| B2 | Summary is readable and grounded | Covers approach, team, portfolio, permit, financials, and gaps; no documents that are not on the profile |
| B3 | Evaluator must accept or reject before scoring | No **Score** job until status is **Accepted** |
| B4 | Accept/reject is per vendor | Other vendors continue if one is rejected |
| B5 | Rejected summary is not scored | Vendor status **summary rejected**; no C1–C3; not in the ranking; reason stored |
| B6 | After reject, staff can fix the profile and re-summarize, re-run summary, or leave the vendor out | Chosen path is visible on the profile |

### C. Scoring and ranking (when the scoring agent exists)

| # | Criterion | Pass |
|---|-----------|------|
| C1 | Scorer uses the accepted summary **and** the stored documents, TOR, and strategy | Each of C1, C2a, C2b, C2c, C3 cites a file or “missing” |
| C2 | Missing or unreadable evidence is 0.00 | Not assumed from marketing text |
| C3 | App computes the math | `C2 = (C2a+C2b+C2c)/3`; `TWS = C1×30 + C2×20 + C3×20`; pass line **49.00 / 70** |
| C4 | Mandatory fail is separate from TWS | Incomplete Section J / no portfolio / no 3-month TOR acknowledgement → FAIL, still scored, not eligible |
| C5 | Eligible ranking is by TWS | Mandatory FAIL bids listed but not ranked as eligible |
| C6 | Evaluator can edit a 0–1 score | TWS and ranking update without the agent |
| C7 | Final result is a draft until approved | Approved flag / timestamp on the company and/or the run |

### D. Fit to the gold set

Score the same gold vendors from `processed/` against the scoring strategy file that was loaded (the one written outside `POC/`, currently `SCORING_STRATEGY.md`). Compare to `processed/SCORES.md`.

| # | Criterion | Pass |
|---|-----------|------|
| D1 | Same five 0–1 scores are produced | C1, C2a, C2b, C2c, C3 present for each gold vendor that was scored |
| D2 | Scores stay close to the gold set | Each criterion within **0.15** of `SCORES.md`; TWS within **5.00** points |
| D3 | Eligibility matches the gold notes | Vardot and Maison Interactive eligible; Argusoft high TWS but mandatory fail; YAIDEV non-responsive / not eligible |
| D4 | Permit rule is enforced | ISO / UNGM / tax ID / “permit being processed” is not C2a credit; only a real government registration copy counts |

### E. Out of scope for this POC slice

- Three-evaluator committee workflow and disagreement
- Financial 30% in the final combined score
- Agents writing summaries or scores (mock only queues the job)
