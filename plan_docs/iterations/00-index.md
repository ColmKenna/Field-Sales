# Field Sales Management System — Implementation Plan

**Sources** — the 14 original area story documents plus [staff-and-tablet-sign-in.md](../stories/staff-and-tablet-sign-in.md) in [plan_docs/stories/](../stories/) (150 stories, including the UX amendments folded into them) and the UX design set in [plan_docs/uxdocs/](../uxdocs/) (00–07 and the handover).
**Generated** — 28 September 2026.
**Provenance** — *content* (what to build, acceptance criteria, examples) comes from the stories and UX documents. *Ownership* (who builds each task, how it is tested, and in what order) was **classified in this plan** from those documents alone: no delegation-triage report was supplied. Every tier therefore carries `(inferred)`, and dimensions that couldn't be scored were scored High and reported below.
**Picking this up?** Read [HANDOVER.md](HANDOVER.md) first: current state, what to do next, how to execute a task, and the rules for editing the plan.
**Location** — the plan lives in `plan_docs/iterations/`, one folder per iteration, as requested. (The planner's default would have been `docs/plans/<source-slug>/`.) The linked stories and UX designs live beside it in `plan_docs/`.

## How to read this plan

- **Iteration → epic → task.** Ten iterations run from an MVP to the full application. Each holds epic files (`NN-slug.md`), and each epic holds tasks `T-<epic>.<story>.<n>`.
- **Every task is a vertical slice**: it can be merged alone, it can be tested, and it can be demonstrated.
- **Scenario first.** A task whose tests are Human-Led or Scenario Review has a `T-<id>-S` section before it, and its scenario list must be agreed before implementation starts. Human-Led: a person writes the list, guided by the questions in that section. Scenario Review: an agent drafts the list from the prompt given and a person approves it.
- **The body follows the implementation tier:**
  - **Agent-Autonomous**: a complete prompt to paste and run.
  - **Agent-Assisted**: the same kind of prompt, plus a checkpoint. The agent stops at the checkpoint and resumes only on the literal reply `Continue T-<id>`.
  - **Human Tight-Loop**: a work package (increments, the decisions that made it tight-loop, and narrow delegable slivers), not a single prompt.
- **Gates inside every prompt**: the agent stays read-only until you reply `Approved` to its plan, and modifies no existing files until you reply `Apply`.
- **Code locations** are `{{PLACEHOLDER}}` throughout, because no code exists yet. **Stack** is `{{STACK}}`.

## Scan and triage

| Item | Verdict |
|---|---|
| Repository scan | Scanned read-only. The repository holds planning documents only (stories, UX designs, handover); there is no application code, test framework or convention to follow. Every path, pattern and convention in task sections is `{{PLACEHOLDER}}` or refers to an earlier task in this plan. |
| Triage report | None supplied. Tiers were classified here with the ownership rubric. Where confidence is Low, the tier was treated one step more conservatively (marked ↓). |
| Stack | `{{STACK}}` — not chosen (MI-01). |

## Capability classes and models

This is **the only place concrete model names appear**. Task sections name a capability class and an effort setting, so a provider release changes this table, not 224 sections.

> Verified 2026-09-14 against provider documentation. Verify again before use.

| Class | Used for in this plan | Anthropic | OpenAI | Google |
|---|---|---|---|---|
| Frontier + extended reasoning | Complex tight-loop work: price engine, sync and snapshot, acceptance at the cut-off, ownership resolution, attribution, allocation | Claude Opus 5, extended thinking, high budget | GPT-6 Astra (`gpt-6-astra`), reasoning `xhigh` or `max` | Gemini 3.1 Pro, thinking high |
| Frontier workhorse | Agent-Assisted tasks, Moderate/Simple tight-loop tasks, scenario drafting under Scenario Review | Claude Opus 5 or Sonnet 5, extended thinking, standard budget | GPT-5.6 Sol (`gpt-5.6-sol`), reasoning `high` | Gemini 3.8 Flash (`gemini-3.8-flash`), thinking high |
| Fast mid-tier | Agent-Autonomous tasks; typing an agreed scenario list into tests | Claude Sonnet 5, no extended thinking | GPT-5.6 Sol, reasoning `medium` | Gemini 3.8 Flash or 3.7 Flash, thinking low |
| High-volume / trivial | Not used by any task | Claude Haiku 4.5 | GPT-5.6 family, reasoning `none` or `low` | Gemini 3.5 Flash-Lite |

Escalate one class at a time if results are weak. Running tests against an agreed scenario list never needs more than Fast mid-tier.

---

## Executive summary

**10 iterations · 29 epics · 224 tasks · 150 stories** (3 superseded stories carry no tasks). There are **202 scenario sections** (`-S`) and **5 tasks carry `{{NEEDS ACCEPTANCE CRITERIA}}`** because no story exists for part of what they build (T-7.4.2, T-9.8.1, T-19.1.1, T-23.1.1, T-26.2.1). Staff website sign-in starts Iteration 1; tablet sign-in and recovery sit with the first Sync work.

| Implementation tier | Tasks | Simple | Moderate | Complex |
|---|---|---|---|---|
| Human Tight-Loop (5 of them ↓ downgraded from Agent-Assisted for Low confidence) | 52 | 3 | 17 | 32 |
| Agent-Assisted | 151 | 31 | 110 | 10 |
| Agent-Autonomous | 21 | 15 | 6 | 0 |
| **Total** | **224** | **49** | **133** | **42** |

| Test tier | Tasks |
|---|---|
| Human-Led (a person writes the scenarios) | 77 |
| Scenario Review (an agent drafts, a person approves) | 125 |
| Agent-Autonomous (the agent selects scenarios from the criteria) | 22 |

Classification confidence: 1 High, 195 Moderate, 28 Low. The Low ones sit almost entirely on gaps in the MIR below: undecided rules, missing stories, and the stack.

**Where risk concentrates.** Iteration 1 carries more than a third of all tasks (81) and most of the system's shared machinery:
- staff website sign-in and live role checks (T-1.0.1, T-1.0.2)
- tablet sign-in, expired-Sync recovery and safe sign-out (T-4.1.1, T-4.6.1, T-4.7.1)
- the offline snapshot and exactly-once upload (T-4.1.1, T-4.1.2)
- the price engine (T-6.4.1)
- acceptance at the cut-off and release to the warehouse (T-7.1.1)
- the most-specific-rule ownership resolution (T-3.1.1)

Every later iteration extends at least one of these. After Iteration 1, risk sits on:
- money rules: promotions, the rep allowance, prospect and chain pricing
- things that change what an order is: shortfall routing, conversion, attribution
- the first exceptions to manual sync: customer requests in the background, and multi-branch sessions shared between tablet and laptop

**Available parallelism.**
- Iteration 2 and Iteration 3 run in parallel after Iteration 1.
- After Iteration 4, up to five lanes can run at once (see *Parallel lanes*): promotions and rep discounts; campaigns through to chain ordering; customer accounts; target setting; stock allocation.
- The limit is not dependencies but a handful of shared surfaces that must change one task at a time.

---

## Iterations

| # | Iteration | What becomes true | Epics | Tasks | Needs |
|---|---|---|---|---|---|
| 1 | [MVP: orders from the field reach the warehouse](iteration-01-mvp/) | Staff sign in to permitted areas; a rep works offline from a morning snapshot and takes calls, stock checks and correctly priced orders. Orders sync once, wait until the cut-off, are accepted automatically and released, and despatch comes back to the tablet. | E1–E8 | 81 | — |
| 2 | [Catalogue lifecycle](iteration-02-catalogue-lifecycle/) | Ranges guide the pad. Products are discontinued, run out and replaced with the tablet explaining why. Categories can be restructured safely. | E9–E11 | 19 | I1 |
| 3 | [Visit rhythm](iteration-03-visit-rhythm/) | Servicing profiles generate a rep's visits. Reps plan the week and handle the Cycle End digest. Managers see exceptions and add one-off visits, and handovers move visits on an ownership change. | E12–E14 | 27 | I1 |
| 4 | [Team changes and location upkeep](iteration-04-team-changes/) | Absences, cover and conflicts; reassignment batches with history; closures, GPS capture and gap lists. | E15–E17 | 16 | I3 |
| 5 | [Commercial pushes](iteration-05-commercial-pushes/) | Promotions in four shapes. The rep discount allowance and free goods within policy. Specialists and visit campaigns. | E18–E20 | 23 | I2 (E18, E19); I3, I4 (E20) |
| 6 | [New business](iteration-06-new-business/) | Leads, prospects and conversion. Duplicate review on a typed Worklist. | E21–E22 | 15 | I3, I4, T-18.2.1 |
| 7 | [Chains](iteration-07-chains/) | Agreed ranges, Range Reviews and proposals. Multi-branch ordering on tablet and laptop. | E23–E24 | 11 | I2, I3, I4, T-20.1.2, T-22.2.1 |
| 8 | [Customer self-service](iteration-08-self-service/) | Invited customers order online, edit until the cut-off, repeat past orders, and order for a chain's branches. | E25–E26 | 17 | E25: I3; E26: I7, E18 |
| 9 | [Targets and performance](iteration-09-targets-performance/) | Targets per rep, Range and chain. Attributed actuals, rep and manager views, and the Large baseline. | E27–E28 | 8 | E27: I3; E28: I7, I8, E18 |
| 10 | [Stock allocation](iteration-10-stock-allocation/) | Shortfalls wait as Outstanding. Head office allocates short stock (complete-what-you-can) and releases it. | E29 | 7 | I2, T-17.2.1 |

## Epics

Status is the plan's status: every epic file is written and no task has started.

| Epic | Title | File | Tasks | TL / AS / AA | Status |
|---|---|---|---|---|---|
| E1 | Staff website access and catalogue essentials | [01-catalogue-essentials.md](iteration-01-mvp/01-catalogue-essentials.md) | 15 | 5 / 7 / 3 | Planned |
| E2 | Customers, locations and contacts | [02-customers-locations-contacts.md](iteration-01-mvp/02-customers-locations-contacts.md) | 8 | 1 / 4 / 3 | Planned |
| E3 | Rep coverage and product permissions | [03-rep-coverage-permissions.md](iteration-01-mvp/03-rep-coverage-permissions.md) | 11 | 3 / 5 / 3 | Planned |
| E4 | Tablet sign-in, sync and Home | [04-tablet-sync-home.md](iteration-01-mvp/04-tablet-sync-home.md) | 10 | 6 / 4 / 0 | Planned |
| E5 | Calls, stock checks and orders | [05-calls-stock-checks-orders.md](iteration-01-mvp/05-calls-stock-checks-orders.md) | 14 | 1 / 12 / 1 | Planned |
| E6 | Customer pricing on the order line | [06-customer-pricing.md](iteration-01-mvp/06-customer-pricing.md) | 8 | 1 / 7 / 0 | Planned |
| E7 | Order processing and fulfilment | [07-order-processing-fulfilment.md](iteration-01-mvp/07-order-processing-fulfilment.md) | 8 | 3 / 4 / 1 | Planned |
| E8 | Taking products off sale | [08-taking-products-off-sale.md](iteration-01-mvp/08-taking-products-off-sale.md) | 7 | 1 / 4 / 2 | Planned |
| E9 | Ranges | [09-ranges.md](iteration-02-catalogue-lifecycle/09-ranges.md) | 8 | 2 / 4 / 2 | Planned |
| E10 | Product lifecycle and replacements | [10-product-lifecycle-replacements.md](iteration-02-catalogue-lifecycle/10-product-lifecycle-replacements.md) | 7 | 1 / 5 / 1 | Planned |
| E11 | Category restructuring | [11-category-restructuring.md](iteration-02-catalogue-lifecycle/11-category-restructuring.md) | 4 | 0 / 4 / 0 | Planned |
| E12 | Servicing profiles | [12-servicing-profiles.md](iteration-03-visit-rhythm/12-servicing-profiles.md) | 6 | 0 / 6 / 0 | Planned |
| E13 | The rep's week | [13-the-reps-week.md](iteration-03-visit-rhythm/13-the-reps-week.md) | 13 | 3 / 10 / 0 | Planned |
| E14 | Manager's view, one-off visits and handover | [14-manager-view-one-offs-handover.md](iteration-03-visit-rhythm/14-manager-view-one-offs-handover.md) | 8 | 1 / 7 / 0 | Planned |
| E15 | Absence, cover and conflicts | [15-absence-cover-conflicts.md](iteration-04-team-changes/15-absence-cover-conflicts.md) | 6 | 2 / 3 / 1 | Planned |
| E16 | Reassignment batches and coverage history | [16-reassignment-history.md](iteration-04-team-changes/16-reassignment-history.md) | 4 | 1 / 2 / 1 | Planned |
| E17 | Location upkeep | [17-location-upkeep.md](iteration-04-team-changes/17-location-upkeep.md) | 6 | 1 / 4 / 1 | Planned |
| E18 | Promotions | [18-promotions.md](iteration-05-commercial-pushes/18-promotions.md) | 9 | 2 / 6 / 1 | Planned |
| E19 | Rep discounts and free goods | [19-rep-discounts-free-goods.md](iteration-05-commercial-pushes/19-rep-discounts-free-goods.md) | 5 | 2 / 2 / 1 | Planned |
| E20 | Specialists and campaigns | [20-specialists-campaigns.md](iteration-05-commercial-pushes/20-specialists-campaigns.md) | 9 | 2 / 7 / 0 | Planned |
| E21 | Leads and prospects | [21-leads-prospects.md](iteration-06-new-business/21-leads-prospects.md) | 10 | 2 / 8 / 0 | Planned |
| E22 | Duplicate review and the Worklist | [22-duplicate-review-worklist.md](iteration-06-new-business/22-duplicate-review-worklist.md) | 5 | 2 / 3 / 0 | Planned |
| E23 | Master view and agreed ranges | [23-master-view-agreed-ranges.md](iteration-07-chains/23-master-view-agreed-ranges.md) | 6 | 0 / 6 / 0 | Planned |
| E24 | Multi-branch ordering | [24-multi-branch-ordering.md](iteration-07-chains/24-multi-branch-ordering.md) | 5 | 2 / 3 / 0 | Planned |
| E25 | Online accounts and invitations | [25-online-accounts-invitations.md](iteration-08-self-service/25-online-accounts-invitations.md) | 7 | 2 / 5 / 0 | Planned |
| E26 | Ordering online | [26-ordering-online.md](iteration-08-self-service/26-ordering-online.md) | 10 | 1 / 9 / 0 | Planned |
| E27 | Setting targets | [27-setting-targets.md](iteration-09-targets-performance/27-setting-targets.md) | 3 | 0 / 3 / 0 | Planned |
| E28 | Seeing performance | [28-seeing-performance.md](iteration-09-targets-performance/28-seeing-performance.md) | 5 | 2 / 3 / 0 | Planned |
| E29 | Allocating short stock | [29-allocating-short-stock.md](iteration-10-stock-allocation/29-allocating-short-stock.md) | 7 | 3 / 4 / 0 | Planned |

---

## Dependency ordering

**Tasks, not epics, are the unit of ordering.** Each task's `Depends on` line is authoritative. Epic and iteration order is a convenient grouping that respects those lines, with the exceptions below.

**Iteration 1 is ordered across epics, not within them.** Several MVP tasks depend on tasks in later-numbered MVP epics:
- T-1.0.1 → T-1.0.2 → T-1.1.1; staff sign-in and live role checks precede catalogue work
- T-1.0.1 → T-4.1.1; the tablet reuses the staff identity model
- T-4.1.1, T-4.1.2, T-4.1.3 → T-4.6.1; expired-Sync continuation follows the upload protocol
- T-4.1.1 → T-4.7.1; sign-out retention follows local Unsent storage
- T-1.3.2 → T-4.1.1, T-6.4.1
- T-1.4.2 → T-7.1.1
- T-1.6.2 → T-4.1.1
- T-2.5.2 → T-4.2.2
- T-4.1.2 and T-4.4.1 → T-5.1.1
- T-4.5.1 → T-5.3.1

Build Iteration 1 along its critical path, pulling other tasks in as their dependencies land:

`T-1.0.1 → T-1.0.2 → T-1.1.1 → T-2.1.1 → T-2.3.1 → T-3.1.1 → T-4.1.1 → T-4.2.1 → T-4.2.2 → T-5.1.1 → T-4.1.2 → T-7.1.1 → T-8.2.1 → T-8.4.1`

- T-1.2.1 (products) also feeds T-4.1.1.
- T-4.6.1 and T-4.7.1 complete tablet sign-in recovery and safe sign-out after the Sync and local storage slices land; neither blocks T-4.2.1.
- The price engine (T-6.4.1, after T-1.3.1 and T-1.4.1) runs beside the path and must land before orders need resolved prices (T-6.4.2).

The epics in Iteration 1 are a way to find tasks, not a sequence.

**Within Iteration 2**, T-9.6.1 (archive a Range) needs T-10.1.1 (replacements), so E10's first task comes before E9's archive task.

**Across iterations** the order is 1 → {2, 3} → 4 → 5 → 6 → 7 → 8 → 9, with four relaxations:

| Epic | Can start once | Instead of waiting for |
|---|---|---|
| E18, E19 | Iteration 2 is done | Iterations 3–4 |
| E25 | E14 (T-14.1.1, T-14.4.1) and T-3.2.1 are done | Iterations 5–7 |
| E27 | E14 is done | Iterations 5–8 |
| E29 | E10 and T-17.2.1 are done | Iterations 5–9 |

## Parallel lanes

After Iteration 4, five lanes can run side by side:

| Lane | Sequence | Starts after |
|---|---|---|
| A — Money | E18 → E19 | Iteration 2 |
| B — Field work to chains | E20 → E21 → E22 → E23 → E24 → E26 → E28 | Iteration 4, and T-18.2.1 from lane A for E21 |
| C — Customer accounts | E25 → (joins B at E26) | E14 |
| D — Target setting | E27 → (joins B at E28) | E14 |
| E — Stock | E29 | E10 and T-17.2.1 |

**No dependency between two tasks does not make them safe to run together.** These surfaces are shared, and changes to each must be made one task at a time, whichever lane the task sits in:

| Shared surface | Tasks that change it | Rule |
|---|---|---|
| Staff identity and role boundary (T-1.0.1, T-1.0.2) | T-1.1.1, T-3.4.1, T-4.1.1, T-25.1.1 | Settle MI-03 first; one owner reviews role and session changes across staff and customer surfaces |
| Snapshot contract and upload (T-4.1.1, T-4.1.2) | T-1.3.2, T-1.6.2, T-4.4.1, T-9.8.1, T-12.4.1, T-13.6.1, T-18.2.1, T-19.1.1, T-20.1.2, T-20.3.1, T-21.1.1, T-21.3.1, T-23.2.1, T-23.5.1, T-24.1.1, T-24.3.2, T-25.2.1 | One snapshot version change at a time, reviewed by T-4.1.1's owner |
| Tablet session and Unsent storage (T-4.1.1) | T-4.6.1, T-4.7.1 | Complete one authentication or logout change at a time; keep the exact-once and no-loss scenarios passing |
| Price engine and shared test vectors (T-6.4.1) | T-18.1.1, T-18.4.1, T-18.5.1, T-18.6.1, T-19.1.2, T-21.3.2, T-24.2.1 | Serialise; all tablet/server vectors must pass after each |
| Acceptance at the cut-off and release (T-7.1.1) | T-8.4.1, T-10.6.1, T-21.6.1, T-28.1.1, T-28.4.1, T-29.1.1, T-29.4.1 | Serialise; lanes B and E both touch it |
| Order line and provenance sheet (T-6.4.3) | T-18.3.1, T-19.1.2, T-19.5.1 | Serialise (lane A) |
| Order Pad membership (T-5.1.2, T-9.8.1) | T-20.1.2, T-23.5.1 | Serialise (lane B) |
| Tablet Home (T-4.5.1) | T-8.5.1, T-13.9.1, T-21.1.1, T-22.4.1, T-25.3.2 | Serialise; lanes B and C both touch it |
| Location page M-07 (T-3.2.1) | T-14.4.1, T-20.1.1, T-25.3.3, T-27.3.1 | Serialise; lanes B, C and D touch it |
| Head office Worklist H-01 (T-7.1.2) | T-22.2.1, T-23.4.1 | Serialise (lane B) |

---

## Where the split matters

These tasks have implementation and test ownership that diverge. They are the most actionable lines in the plan and the most often mis-executed.

**Agent implements, people write the scenarios (Agent-Assisted / Human-Led) — 28 tasks.** Hold the agent until the `-S` list is written by a person. The agent implements exactly that list and designs no tests of its own; its prompt says so.

| Iteration | Tasks |
|---|---|
| 1 | T-1.3.2, T-3.1.4, T-5.1.6 |
| 2 | T-9.6.1, T-11.3.1 |
| 3 | T-12.2.2, T-14.2.1, T-14.4.2, T-14.5.2 |
| 4 | T-15.2.1, T-16.2.1, T-16.4.1 |
| 5 | T-18.4.1, T-18.5.1, T-18.7.1, T-19.2.1, T-20.3.1 |
| 6 | T-21.2.1, T-21.3.2, T-22.3.1 |
| 7 | T-23.1.1, T-23.4.1 |
| 8 | T-25.4.1, T-26.2.1, T-26.3.1, T-26.4.2 |
| 10 | T-29.3.2, T-29.3.3 |

**People implement, an agent drafts the scenarios (Human Tight-Loop / Scenario Review) — 3 tasks.** The agent's scenario draft is reviewed like any other. The implementation is not delegated beyond the named slivers.
- T-1.3.1
- T-2.3.2
- T-13.2.2

**Agent runs unattended, but only on an approved scenario list (Agent-Autonomous / Scenario Review) — 2 tasks.** These modify existing behaviour, so the list starts with characterisation scenarios.
- T-8.3.2
- T-8.6.1

**Agent picks its own tests, but implementation stops at a checkpoint (Agent-Assisted / Agent-Autonomous) — 3 tasks.** The checkpoint is about the screen or data shape, not the test design.
- T-1.6.2
- T-3.1.3
- T-4.5.1

## Stories delivered across epics

A story's *home* epic delivers most of it. The parts listed here are delivered elsewhere and appear as "(part)" rows in the delivering epic's story table.

| Story | Home | Delivered elsewhere |
|---|---|---|
| Coverage US-002 — who covers a Location | E3 | specialists line → E20; Add one-off visit → E14; Set up online ordering → E25 |
| Coverage US-006 — specialists by scope | E20 | S4's reporting flag → not delivered (MI-41) |
| Coverage US-007 — unassigned Locations | E3 | overview count CV007-A, B, D–H → E14 |
| Coverage US-008 — a rep's coverage and history | E16 | specialist scopes → E20 |
| Customer Directory US-001 — create a Customer | E2 | No Visit Schedule list → E17; Unassigned list → E3 |
| Customer Directory US-007 — geography | E2 | S3 move a Town → E16 |
| Head Office US-002 — why an order is flagged | E7 | S5 → E10; S6 → E21; S1 Large, S3, S4 → E28; S6a superseded |
| Head Office US-006 — held orders | E7 | S3 → E17; S1 allocation link → E29 |
| Head Office US-008 — orders go through without acceptance | E7 | S3 → E8; S4 → E22; S2 → E29 |
| Rep at a Location US-003 — today and what's at risk | E4 | S1, S3, S4, S6 → E13; S7a → E14; S7 → E15; S8–S16 → E8 |
| Rep at a Location US-006 — record a call | E5 | visit completion → E13; S6, S7 → E20 |
| Rep at a Location US-007 — stock check | E5 | S2 → E8 |
| Rep at a Location US-008 — mark Low | E5 | S5 → E9; S9 → E6 |
| Rep at a Location US-010 — availability states | E8 | replacements, S4, S5 → E10; S1's range reason → E9 |
| Rep at a Location US-012 — build an order | E5 | ranges and "Outside your ranges" → E9; S4b, S4c → E6; S4d → E18; S4e → E19; S10 → E10; S11 → E8 |
| Rep at a Location US-014 — check a sent item | E7 | S2, A1014-C → E13; A1014-A → E8; A1014-B → E19 |
| Rep at a Location US-015 — correct a call | E5 | website correction → E13 |
| Rep at a Location US-016 — follow-up call | E5 | visit completion → E13 |
| Rep at a Location US-022 — prospects | E21 | S5 → E22 |
| Rep at a Location US-023 — master Location | E23 | S3, S4 → E24 |
| Range Lifecycle US-009 — retire a product | E8 | replaced-by → E10; S2 → E9 |
| Product Management US-010 — find products | E1 | archived-category products → E11 |
| Targets & Performance US-003 — chain target | E27 | S3, S4 measured in E28 |
| Visit Planning US-004 — schedule visits | E13 | S3, VP004-E → E15 |
| Visit Planning US-005 — day load | E13 | S3 → E15; S4, S5 campaign duration → E20 |
| Visit Planning US-010 — manager overview | E14 | S1b cross-team cover → E15 |
| Visit Planning US-011 — create a campaign | E20 | S3 single one-off → E14 |

**Superseded stories, listed with no tasks:**
- Head Office US-001 and US-003 (in E7; replaced by US-008).
- Pricing US-008 (in E19; overrides are applied within the allowance).

**Superseded scenarios** are named in each epic's story table (for example Rep at a Location US-014 S4c and Head Office US-002 S6a).

---

## Missing Information Report

The items marked **Iteration 0** block Iteration 1 from starting and should be settled first. "Blocks" names the tasks that can't be finished, or can't start, without an answer.

### Plan-wide

| MI | Gap | Blocks |
|---|---|---|
| MI-01 | **Iteration 0.** Stack not chosen; every prompt carries `{{STACK}}` | All tasks; first T-1.0.1, T-4.1.1 |
| MI-02 | **Iteration 0.** Offline snapshot size and storage (Rep at a Location RC 5): spike needed before the snapshot contract is fixed | T-4.1.1 and every snapshot change |
| MI-03 | **Iteration 0.** Identity and roles: staff credential and recovery method, administrator treatment, customer identity relationship. One sign-in for held staff roles, last-used permitted landing, immediate website role removal and next-Sync tablet changes are settled in Sign-in US-001–US-004 | T-1.0.1, T-1.0.2, T-3.4.1, T-4.1.1, T-25.1.1 |
| MI-04 | **Iteration 0.** How released orders reach the warehouse, and what happens on failure | T-7.1.1, T-29.4.1 |
| MI-05 | **Iteration 0.** Where the per-weekday order cut-off is set (no screen designed) | T-7.1.1 |
| MI-06 | **Iteration 0.** Go-live data load: customers, Locations, contacts, ranges, chains' agreements | T-2.3.1, T-23.1.1 |
| MI-07 | **Iteration 0.** Eircode lookup source and precision | T-2.3.2 |
| MI-08 | **Iteration 0.** 14 original MVP screens are drafts awaiting confirmation (H-03, H-12–H-15, H-17, H-20–H-22, H-25–H-27, H-29, H-31); the new I-01–I-05 access layouts are also proposals for review | E1, E2, E4, E6, E7 tasks that build them |
| MI-11 | Tax: prices assumed exclusive, handled by the external system | T-6.4.1 |
| MI-12 | Settings: where thresholds, periods, the expected-delivery days and the Large multiplier are set, and by whom | T-5.4.1, T-6.5.1, T-7.4.1, T-8.2.1, T-21.2.1, T-26.3.1, T-28.4.1 |
| MI-42 | Epics are inferred; the sources define areas, not epics | Plan structure |
| MI-43 | Promotions' source IDs use `PM007-A`, clashing with Product Management's `PM`; the plan uses PRO | Plan references |
| MI-53 | Business time zone and day boundary (dated prices, the cut-off, promotions, stale leads, monthly allowances) | T-1.3.2, T-7.1.1, T-18.2.1, T-19.2.1, T-21.2.1, T-21.2.3, T-26.4.2 |
| MI-61 | Drafted or undesigned screens outside the MVP set: T-09, T-10, H-05, H-08, H-09, H-11, H-23, H-24 await confirmation; manager lead capture and Location target placement are undrawn | E18, E21, E22, E29; T-21.1.2, T-27.3.1 |

### Iterations 1–2

| MI | Gap | Blocks |
|---|---|---|
| MI-09 | Rep-flag control: placement, and whether it carries a note (`{{NEEDS ACCEPTANCE CRITERIA}}`) | T-7.4.2 |
| MI-10 | How staff change or cancel an accepted order | T-26.4.2, T-28.1.1 |
| MI-13 | Product code format and uniqueness | T-1.2.1 |
| MI-14 | What happens silently when an Expected Back date passes | T-8.1.1 |
| MI-15 | Head Office US-004's dependency points at superseded US-003; re-pointed to US-008 | T-7.3.1 |
| MI-16 | Geography seed data (Regions, Counties, Towns) | T-2.1.1 |
| MI-17 | **No story for assigning Ranges** to reps, chains or customers (`{{NEEDS ACCEPTANCE CRITERIA}}`) | T-9.8.1, T-23.1.1, T-26.2.1 |
| MI-18 | Do Range assignments survive archive and return on un-archive? | T-9.7.1, T-9.8.1, T-23.1.1, T-26.2.1 |
| MI-19 | Run-out Remaining countdown under concurrent orders (spike) | T-10.3.1 |
| MI-44 | The server can't count lines that exist only on unsynced tablets, so impact previews undercount | T-3.4.3, T-8.2.1, T-9.6.1 |
| MI-45 | Semantics of an archived Restriction Group | T-1.5.3, T-3.4.1, T-4.4.1 |
| MI-46 | Temporarily Unavailable lines at the cut-off | T-8.4.1 |
| MI-47 | Category-level tier overrides | T-6.1.1, T-6.4.1 |
| MI-48 | Rounding rules for percentages, splits and allowances | T-6.4.1, T-18.1.1, T-18.4.1, T-18.5.1, T-19.1.2 |
| MI-49 | Out-of-pattern quantity threshold | T-5.1.6 |
| MI-50 | "Open on website" target before R-04 exists (resolved by T-13.10.1) | T-7.5.1 |
| MI-51 | Whether rep, manager and head-office areas share one site or several (inferred); the user journey requires one sign-in across held roles either way | T-1.0.1, T-1.0.2 |
| MI-62 | Disabled rep account on tablet reconnect: whether work captured before disablement uploads, remains for manager recovery, or waits for account restoration (Sign-in US-003 Draft) | T-4.1.1 and E4 exit criterion |
| MI-52 | Rep at a Location US-001 S4's rejection example is superseded by BR-NEW-006 | T-4.1.2 |
| MI-54 | Runner-up price on the line (Pricing US-005) versus only in the provenance sheet (UX DECISION 1.1) | T-6.4.3 |
| MI-55 | Type-to-confirm text: "Suncare" (story) versus "ARCHIVE SUNCARE" (H-16 frame) | T-11.3.1 |
| MI-56 | Never-ranged versus de-ranged availability (Range Lifecycle US-004 S2 versus glossary) | T-9.2.1 |

### Iterations 3–4

| MI | Gap | Blocks |
|---|---|---|
| MI-20 | Geocoding and map provider | T-13.2.2 |
| MI-21 | Visit planning settings: working day, travel allowance, digest day | T-13.1.1, T-13.4.1, T-13.5.1, T-13.8.1 |
| MI-22 | Per-Location threshold overrides | T-12.3.1, T-12.4.1 |
| MI-23 | GPS distance rule (2 km) and moving a Location between Customers | T-17.3.1 |
| MI-57 | Which call counts as "for" a one-off visit | T-14.4.2 |
| MI-58 | Pricing and limits of lines edited on R-04 | T-13.10.1 |
| MI-59 | The administrator role that maintains Reason Types (and commercial policies) | T-14.3.1, T-19.1.1 |

### Iteration 5

| MI | Gap | Blocks |
|---|---|---|
| MI-24 | **Commercial policy model** (RC-NEW-009) and **no setup story** (`{{NEEDS ACCEPTANCE CRITERIA}}`) | T-19.1.1, and so T-19.1.2, T-19.2.1 |
| MI-25 | Spend-threshold discount base (RC-NEW-006) | T-18.6.1 |
| MI-26 | Editing a live promotion (only End early assumed) | T-18.2.1 |
| MI-27 | Free-goods reporting by rep or period is undesigned | T-19.2.1 (out of scope) |
| MI-60 | Is a reason required on a free-of-charge line? (Pricing US-009 S4 versus its superseded note and the T-07 picker) | T-19.2.1 |

### Iterations 6–7

| MI | Gap | Blocks |
|---|---|---|
| MI-28 | Prospect pricing (base price plus all-customer promotions assumed) | T-21.3.2 |
| MI-29 | Duplicate matching rules; a prospect's Customer and Primary Rep before and after conversion | T-21.3.1, T-21.6.1, T-22.1.1 |
| MI-30 | Per-branch agreed-range variations; chain pricing | T-23.1.1, T-23.5.1, T-24.2.1 |
| MI-31 | Master & Branch US-003 (Draft) duplicates Head Office US-005 | T-23.4.1 |
| MI-32 | Drafts: T-13, T-14, H-04 screens; M-11 grid layout deferred; multi-branch session recovery and cross-surface editing | T-23.4.1, T-24.1.1, T-24.2.1, T-24.3.1, T-24.3.2 |

### Iteration 8

| MI | Gap | Blocks |
|---|---|---|
| MI-33 | Invitation channel, lifetime, password policy | T-25.1.1 |
| MI-34 | The background channel to the tablet for customer requests | T-25.2.1 |
| MI-35 | App and website identical and online-only; can customers edit their own contact details? | T-25.1.1, T-26.6.2 |

### Iterations 9–10

| MI | Gap | Blocks |
|---|---|---|
| MI-36 | Large comparison rule (pace or whole period) and multiplier | T-28.4.1 |
| MI-37 | Period definitions; visit-activity measures; whether progress shows on the tablet | T-27.1.1, T-28.2.1 |
| MI-38 | Warehouse stock feed, and what "available" means at the cut-off without one | T-29.1.1, T-29.2.1 |
| MI-39 | Is an allocation history kept? | T-29.4.1, T-29.3.2 |
| MI-40 | Passed Over threshold | T-29.3.2 |
| MI-41 | Reporting is undesigned (promotion performance, brand totals, conversions) | T-18.8.1, E28 (Coverage US-006 S4) |

## Open assumptions

- **Ownership (all 224 tasks).** Every ownership line reads `(inferred) — …`, because tiers were classified here from the stories. Revisit them if a triage report is produced.
- **T-20.4.1** — the campaign detail is reached from a campaigns list that no story defines; the list is kept to name, window and progress.
- **Assumptions adopted from the stories, recorded where used:**
  - prospect pricing (T-21.3.2)
  - spend threshold on the final total (T-18.6.1)
  - one reason per rejected proposal line (T-23.4.1)
  - performance on the website only (T-28.2.1)
  - calendar periods (T-27.1.1)
  - an unfinished multi-branch session stays open with a count on Home (T-24.1.1)
  - Passed Over at twice, or short on more than one product (T-29.3.2)
- **Placeholders:**
  - `{{STACK}}` and `{{PLACEHOLDER}}` appear in every prompt.
  - `{{NEEDS ACCEPTANCE CRITERIA}}` appears in five tasks: T-7.4.2 (the rep-flag control, MI-09), T-9.8.1, T-23.1.1 and T-26.2.1 (Range assignment, MI-17), and T-19.1.1 (commercial policy setup, MI-24).

---

## Self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story appears in an epic; none added, none dropped | Pass | All 150 story IDs in the 15 source documents appear in the epics' story tables, and no table names an ID the sources don't have. Split stories have one home epic plus "(part)" rows (table above). The 3 superseded stories are listed with no tasks. |
| Every task satisfies the three slice criteria | Pass | 224 of 224; each epic's self-verification records it |
| Every task carries a tier with a rationale citing dimensions | Pass | 224 `(inferred) — …` rationale lines, one per task |
| Every unscoreable dimension was scored High, marked `(inferred)`, and reported | Pass | MI-01 to MI-62; 28 Low-confidence tasks, 5 downgraded (↓): T-2.3.2, T-7.4.2, T-13.2.2, T-18.6.1, T-28.4.1 |
| Every Sequencing Note from a triage report is honoured | Not applicable | No triage report supplied |
| Every task modifying existing behaviour has characterisation ordered first | Pass | Δ tasks carry characterisation as their first step or checkpoint; each epic's table lists them |
| No Agent-Autonomous body for a High Blast Radius, Taste or Test Safety Net item | Pass | 21 Agent-Autonomous tasks. Each rationale says why the work is low-impact (read-only, a repeated reviewed pattern, or a settled small addition), and 18 state Blast Radius Low explicitly. The four built on draft screens (T-1.1.2, T-1.7.1, T-9.3.1, T-18.8.1) say to treat them as Agent-Assisted until the screen is confirmed. |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | 77 Human-Led test tasks; the 28 Agent-Assisted ones among them forbid the agent to design tests |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | 151 Agent-Assisted tasks, 151 `Resume trigger — Continue T-<id>` lines |
| Every Human Tight-Loop body has increments, decision points and delegable slivers | Pass | 52 tight-loop tasks, 52 work packages |
| Every `T-<id>-S` scenario task precedes the implementation it gates | Pass | 202 `-S` sections for the 202 Human-Led or Scenario Review tasks; each precedes its task (checked by line order; E20 was reordered so T-20.1.3 follows T-20.2.2) |
| Every agent prompt is self-contained | Pass | Each names its spec paths, criteria, constraints, gates, steps, test expectations, definition of done and out-of-scope |
| No concrete model name appears in any task section | Pass | 0 matches for model names across all 29 epic files; names appear only in this index's model table |
| Every placeholder and `(inferred)` marker appears in the MIR | Pass | `{{STACK}}` (MI-01), `{{PLACEHOLDER}}` (MI-01, no code yet), `{{NEEDS ACCEPTANCE CRITERIA}}` (MI-09, MI-17, MI-24), T-20.4.1's campaign list (open assumptions) |
| Plan lives in the requested directory | Pass | The implementation plan is in `plan_docs/iterations/`; it references the sign-in stories and UX designs in their existing `plan_docs/` directories. |
