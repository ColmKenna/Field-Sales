# Handover — implementation plan

**For:** any agent or person picking up this work: planning, settling open questions, or implementing tasks.
**State as of:** 28 September 2026. The plan incorporates staff and tablet sign-in; these revisions have not been committed.
**Read first:** [00-index.md](00-index.md). This note says how to work with the plan; the index says what is in it.

---

## 1. Where things stand

- **Planning is complete; nothing is built.** There is no application code, no stack, and no test framework in this repository. No task has started and no scenario list has been agreed.
- **The plan:** 10 iterations → 29 epics → 224 tasks, covering all 150 stories in `plan_docs/stories/`. Each iteration is a folder here; each epic is a file `NN-slug.md`.
- **Its sources**, which win over the plan wherever they disagree:
  - `plan_docs/stories/` — 14 original area story documents plus `staff-and-tablet-sign-in.md`, already amended from the UX sessions.
  - `plan_docs/uxdocs/` — screen designs (00–07), including `07-access-and-sign-in.md`, the amendments record (`04-user-stories-amendments.md`, which holds the BR-NEW / RC-NEW / EC-NEW items), and the UX handover (`handover.md`).
- **How it was made:** the plan was generated with the `story-implementation-planner` skill. No delegation-triage report existed, so every ownership tier was classified from the stories alone and is marked `(inferred)`.

## 2. What to do next, in order

1. **Settle the Iteration 0 blockers**, MI-01 to MI-08 in the index's Missing Information Report:
   - the stack
   - an offline snapshot size spike
   - the remaining identity choices: credential and recovery method, administrator treatment, and the relationship to customer identity (MI-03); one staff sign-in across held roles and last-used permitted landing are settled
   - how released orders reach the warehouse
   - where the order cut-off is set
   - the go-live data load
   - the Eircode source
   - confirming the 14 original drafted MVP screens and reviewing the new I-01–I-05 access layouts (MI-08)

   These are decisions for the product owner (Colm), not for an agent to make. An agent can prepare options, but should not choose.
2. **Replace `{{STACK}}`** once the stack is chosen. Code locations stay `{{PLACEHOLDER}}` until the code they point at exists. Fill each one in when the task it names has landed.
3. **Write the five missing acceptance criteria** (`{{NEEDS ACCEPTANCE CRITERIA}}`) before their tasks start:

   | Task | What needs criteria | MI |
   |---|---|---|
   | T-7.4.2 | Rep-flag control | MI-09 |
   | T-9.8.1 | Range assignment for reps | MI-17 |
   | T-23.1.1 | Range assignment for chains | MI-17 |
   | T-26.2.1 | Range assignment for customers | MI-17 |
   | T-19.1.1 | Commercial policy setup | MI-24 |

   Add each as a story, or as a UX amendment in `plan_docs/stories/`, then update the task's parent-story quote.
4. **Start Iteration 1 at T-1.0.1** for staff website sign-in, then T-1.0.2 for live role checks, before catalogue work. Follow the critical path in the index:

   `T-1.0.1 → T-1.0.2 → T-1.1.1 → T-2.1.1 → T-2.3.1 → T-3.1.1 → T-4.1.1 → T-4.2.1 → T-4.2.2 → T-5.1.1 → T-4.1.2 → T-7.1.1 → T-8.2.1 → T-8.4.1`

   T-4.1.1 includes tablet sign-in and the 24-hour offline return. T-4.6.1 resumes an interrupted Sync automatically after sign-in; T-4.7.1 warns on manual sign-out with Unsent work and preserves that work through automatic logout. MI-62 records the still-open outcome when a rep account is fully disabled before reconnecting.

   Iteration 1's epics are groupings, not a sequence. Several MVP tasks depend on tasks in later MVP epics, and each task's `Depends on` line is authoritative.

## 3. How to execute a task

Every task section opens with the same header: parent story, slice, spec source, depends on, pattern to follow, ownership, provisional commit message, and capability class. Then:

1. **Check `Depends on`.** Every task listed there must have landed.
2. **Scenarios first.** If the section is preceded by `T-<id>-S`, that scenario list must be agreed before implementation starts.
   - *Human-Led* — a person writes it, guided by the section's questions.
   - *Scenario Review* — run the section's agent prompt to draft it, and a person approves it.

   Scenario names follow the repo convention `Should_Outcome_When_Condition`, with a `→` line giving the intent (`uxdocs/handover.md` §11).
3. **Follow the body for the task's tier:**
   - **Agent-Autonomous** — paste the prompt and run it.
   - **Agent-Assisted** — paste the prompt. The agent stops at the checkpoint, a person reviews it, and work resumes only on the literal reply `Continue T-<id>`.
   - **Human Tight-Loop** — no single prompt. A person works through the increments and settles the listed decision points, and delegates only the named slivers, using their micro-prompts.
4. **Gates in every prompt:** the agent stays read-only until the reply `Approved` to its plan, and modifies no existing file until `Apply`.
5. **Δ tasks** change existing behaviour. Write characterisation tests pinning the current behaviour first, and keep them passing.
6. **↓** marks a task moved one tier more cautious because classification confidence was Low. Do not upgrade it without resolving the MI it cites.
7. **Capability classes, not models.** Sections name a class, such as "Frontier workhorse". The class-to-model table exists only in the index; check it is still current before use (it was verified on 2026-09-14).
8. **Commit** using the task's provisional commit message, reconciled with the actual diff.

## 4. Rules for changing the plan itself

- **Keep headers uniform.** Tools and agents read the header block mechanically, so don't reword the field labels.
- **Model names** appear only in the index's model table.
- **New gaps** get the next free MI number (**MI-63**). Add each one to the index's Missing Information Report under the iteration it blocks, and cite it where it is used. Mark each assumption `(inferred)` at the point of use.
- **Splitting a story across epics:** keep one home epic and add a "(part)" row to the delivering epic's story table. Update the index's "Stories delivered across epics" table.
- **Changing a tier or adding a task:** update that epic's self-verification table and the index's counts (executive summary, epic table, where-the-split-matters lists).
- **Dependencies must point backwards** in file order within an epic. Across epics they may point forward only within Iterations 1 and 2, where the index names each case.
- **Shared surfaces** (the index's parallel-lanes table): only one task at a time may change staff identity and roles, tablet sessions and Unsent storage, the snapshot contract, the price engine, acceptance at the cut-off, the order line and provenance sheet, the Order Pad, tablet Home, the M-07 Location page, or the H-01 Worklist — even across lanes.

## 5. Things that aren't obvious

- **Superseded material is deliberate.** Head Office US-001 and US-003 and Pricing US-008 are listed with no tasks, and superseded scenarios are named in each epic's story table. Don't rebuild them.
- **Orders are never decided by a person.** They are Pending until a per-weekday cut-off, then accepted automatically (BR-NEW-009). Rep discounts and free goods are limits enforced at capture (BR-NEW-002, BR-NEW-003), not approvals. Anything that looks like an "approve order" feature is out of date.
- **Drafted screens.** Many screens are drafts "awaiting confirmation" (MI-08 for the MVP set, including the new access layouts; MI-32 and MI-61 for the rest). A task built on one says what to do until it is confirmed.
- **Staff and tablet login.** Staff use one sign-in for their held roles and open their last-used permitted area; a removed website role stops on the next request. Tablet work remains available offline for 24 hours after the last connected sign-in. Earlier valid work uploads at the next Sync even if a role was then restricted, with the change flagged. The new login tasks are in E1 and E4; customer login remains in E25.
- **T-23.1.1 now points at H-26.** It was corrected after the commit: H26.3 drafts an "Agreed ranges" section on a master's Location record. MI-17 still stands, because no story defines the assignment.
- **T-1.5.3's mention of T-3.4.1 is a note, not a dependency.** Permissions register as a usage source when T-3.4.1 lands.
- **Epic files were written in batches.** If one reads thinner than the others, compare it against E1 or E18.

## 6. Repository conventions

- **Commits:** commit straight to `main`, with **no `Co-Authored-By` trailer** (the owner's standing instruction). Only commit when asked.
- **Line endings:** Git warns that LF will be replaced by CRLF on these files. That is the repo's Windows line-ending setting and needs no action.
- **Scope of writes:** planning work writes only under `plan_docs/`. Implementation work will introduce the codebase; where it lives is part of the stack decision (MI-01).

## 7. Checking the plan after an edit

These shell checks, run from `plan_docs/iterations/`, were used to verify the plan. They should keep passing:

```sh
# every Human-Led or Scenario Review task has a -S section (both should print the same number)
grep -c '^### T-[0-9.]*-S — ' */*.md | awk -F: '{s+=$2} END {print s}'
grep -h '^\*\*Ownership\*\* — Impl:' */*.md | grep -c 'Test: Scenario Review\|Test: Human-Led'

# every Agent-Assisted task has a resume trigger (both the same)
grep -c '^Resume trigger — `Continue T-' */*.md | awk -F: '{s+=$2} END {print s}'
grep -h '^\*\*Ownership\*\* — Impl: Agent-Assisted' */*.md | wc -l

# every tight-loop task has a work package (both the same)
grep -c '^\*\*Work package\*\*' */*.md | awk -F: '{s+=$2} END {print s}'
grep -h '^\*\*Ownership\*\* — Impl: Human Tight-Loop' */*.md | wc -l

# no model names outside the index (should print nothing)
grep -l -i 'opus\|sonnet\|haiku\|gpt-\|gemini' */*.md
```

For the revised plan, these should give 202/202, 151/151, 52/52, and no model-name matches.
