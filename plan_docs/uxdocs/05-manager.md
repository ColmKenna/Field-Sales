# 05 — Manager website (M-01 … M-17)

> M-01–M-15 follow the brief. **M-16 Add a one-off Visit Due** isn't in the brief and was added in session (Visit Planning US-015). **M-17 Online ordering approvals** was added in session (26 Sep 2026) from C-08 (Self-service US-001) and replaces the brief's head office H-07.

**Device assumptions:** laptop, online, used by a Sales Manager planning and reviewing a team.
**Consequence:** exception-first overview screens may be dense, but every summary must drill into the visits, Locations or assignments behind it.

---

## M-01 · Visit Planning overview

**Job:** find which rep needs planning attention, with region as a secondary way to investigate where coverage is breaking down.

```
+------------------------------------------------------------------------------------------------+
| Visit planning                         [ By rep ] [ By region ]          Current period  v      |
+------------------------------------------------------------------------------------------------+
| REP / COVERAGE             | DUE     | OVERDUE | MISSED | UNSCHED <14D | OTHER EXCEPTIONS     |
+------------------------------------------------------------------------------------------------+
| Colm                       | 18 / 24 |       3 |      1 |            4 | 1 over day           |
| Wicklow · 42 Locations     |         |         |        |              | 2 handover pending  > |
+------------------------------------------------------------------------------------------------+
| Aoife                      | 21 / 23 |       0 |      0 |            2 | 1 conflict           |
| Rathdrum, Avoca · 31 Locs  |         |         |        |              | Covering another team|
+------------------------------------------------------------------------------------------------+
| Ciara                      | 20 / 20 |       0 |      0 |            0 | 0 absence decisions  |
| Wexford North · 28 Locs    |         |         |        |              | 0 conflicts         > |
+------------------------------------------------------------------------------------------------+
```

**By region** retains the source-story hierarchy: Region → Town → Visit Dues, with responsible rep on each visit and Unassigned surfaced beside the affected Town.

> **DECISION M1.1 — By rep is the first-use default; the last-selected view is remembered. Settled.**
> A manager normally plans for a rep, so the primary question is "who needs attention?" rather than "which geography?". `By region` remains a peer view for geographic failures and Unassigned Locations. After first use, returning to the manager's last-selected view preserves their working context.

> **DECISION M1.2 — each rep row carries a compact coverage reminder. Settled direction.**
> The manager probably knows each patch, so this is orientation rather than a full assignment inventory. The summary uses the broadest meaningful effective area first, then a carved-out or overflow count, and always ends with the effective Location count: `Wicklow County · 3 towns carved out · 119 Locations` or, where no coherent territory exists, `Rathdrum, Avoca + 4 areas · 31 Locations`. Detailed assignments remain in M-06/M-07.

**Source-confirmed behavior:**
- Every rep remains visible when all exception counts are zero.
- Cross-team cover is informational, with no Approve/Decline action.
- Handover Pending links to Coverage Management.
- By region surfaces Unassigned Locations and links to assignment.
- Opening an Overdue visit shows its window, Due Reason, last Call and Scheduled Day, and allows Mark Missed with an optional reason.

---

## Open questions from this file

1. ~~**M-01 coverage summary** — how are representative area labels chosen?~~ **Resolved — broadest meaningful area first, then carve-out/overflow count, then effective Location count.**

---

## M-02 · Absence entry & decisions

**Job:** block a rep's unavailable time immediately, then decide what happens to affected visits without making those decisions a prerequisite for recording the absence.

**Stage 1 — record the absence:**

```
+---------------------------------------------------------------------------------------+
| < Visit planning                         Record planned absence                       |
+---------------------------------------------------------------------------------------+
| Rep                 [ Colm                                             v ]            |
| From                [ 5 Oct 2026 ]     To                 [ 9 Oct 2026 ]             |
| Full / partial day  [ Full days                                         v ]            |
| Reason              [ Annual leave                                      ]            |
|                                                                                       |
| The calendar will be blocked immediately when this absence is saved.                  |
|                                                   [ Cancel ]   [ Save absence ]       |
+---------------------------------------------------------------------------------------+
```

**Stage 2 — affected visits after save:**

```
+---------------------------------------------------------------------------------------+
| Absence saved · Colm · 5-9 Oct                         14 visits need a decision       |
+---------------------------------------------------------------------------------------+
| The days are blocked now. Undecided visits keep their current window and show         |
| "Absence - decision pending" to Colm.                                                  |
|                                                                                       |
|                                  [ Return to overview ]   [ Decide affected visits ]  |
+---------------------------------------------------------------------------------------+
```

`Decide affected visits` opens the decision list required by Visit Planning US-007: deadline-marked rows, Extend / Keep / Cover per visit, Apply to all with exceptions, and covering-rep capacity context.

> **DECISION M2.1 — absence save and visit decisions are separate commit points. Settled.**
> Saving the absence blocks the rep's calendar immediately and returns already scheduled visits on those days to Unscheduled as specified by US-006. The manager may continue directly to the affected-visit decisions or return to the overview. Leaving does not roll back the absence: affected visits remain unchanged and visibly `Absence — decision pending` until decided.

> **DECISION M2.2 — no affected visits skips the decision stage. Source-confirmed.**
> The saved result says `No visits affected` and returns to the absence/overview context; it does not offer a meaningless decision step.

> **DECISION M2.3 — bulk Cover uses a select-all review with exclusions. Settled.**
> Choosing `Apply to all: Cover` opens the affected visits with all eligible rows selected. The manager chooses one covering rep as the batch default, can clear all or unselect individual visits, and may handle excluded rows separately with Extend, Keep or another covering rep. Before apply, the review shows the selected count and the covering rep's capacity warning for the affected period. Cross-team cover remains informational to the other manager, not an approval.

> **DECISION M2.4 — overlapping entries offer `Extend existing absence`; adjacent entries stay separate. Settled.**
> A genuine date/time overlap shows the existing absence and previews the combined range before offering `Extend existing absence`. The wording replaces the ambiguous `Merge`. Two absences that merely touch or sit next to each other are saved as separate entries without an extension prompt, even when their reasons look similar; the boundary may be intentional. Extending keeps the existing absence as the record being edited, with its reason still editable before save.

**Open:**
- *(none for M-02)*

---

## M-03 · Campaign creation

**Job:** create one coherent campaign across many Locations without hiding who will receive the resulting visits.

```
Filter                    Review                    Details                   Confirm
  (1)----------------------(2)------------------------(3)------------------------(4)

Customer / Profile /      All matches selected      Name and Due Window       "Create 38 visits
Region / optional Brand   Exceptions removable      Reason Type + text         across 6 reps?
                                                     Duration                   10 to specialists"
                                                     Outcome List
```

> **DECISION M3.1 — Campaign creation is a four-step wizard. Settled.**
> `Filter → Review → Details → Confirm` reflects real dependencies: the candidate set must exist before it can be reviewed; campaign details apply to the reviewed set; routing counts can only be confirmed after both are known. Back preserves every entry and selection so correction never means starting over.

> **DECISION M3.2 — each step states its result before advancing. Source-aligned.**
> Filter shows the match count; Review shows selected/excluded/unassigned counts; Details validates that at least one Outcome completes the visit; Confirm names total visits, reps and specialist routing before anything is created. A zero-match filter cannot advance.

> **DECISION M3.3 — choosing a campaign rep resolves unassigned ownership, but never replaces an existing Primary Rep. Settled.**
> Review allows the manager to choose the rep who will receive each selected campaign visit. If the Location has no Primary Rep, that choice also creates a permanent direct Primary Rep assignment as part of campaign confirmation, records it in Assignment History, and supplies future recurring visits, tablet access and attribution. If the Location already has a Primary Rep, choosing another rep affects this campaign visit only; ownership remains unchanged. Confirm separates these consequences: `38 campaign visits across 6 reps · 3 Locations will also receive a Primary Rep` and lists those three before creation.

> **DECISION M3.4 — unassigned selections support a bulk Primary Rep default with row overrides. Settled.**
> Review offers one Primary Rep choice that applies to every selected unassigned Location. The manager can override any row before advancing. The summary always reflects the effective row values, not merely the bulk default, and Confirm lists every permanent assignment separately from campaign-only routing.

**Open:**
- *(none for M-03)*

---

## M-04 · Campaign detail

**Job:** understand the campaign as one piece of work, then drill into the reps or visits causing its current state.

```
+---------------------------------------------------------------------------------------+
| < Campaigns          Autumn range launch                         [ Overall ] [ By rep ]|
+---------------------------------------------------------------------------------------+
| 22 of 38 done                            Window 5-30 Oct                              |
| [=============================---------------------]          [ Extend remaining... ] |
+---------------------------------------------------------------------------------------+
| OUTCOMES                                                                              |
| 15 ordered       7 declined       3 follow up       13 not yet visited                |
+---------------------------------------------------------------------------------------+
| VISITS                                                                                |
| Location                 Rep          Status / outcome                     Due         |
| Quinn's Centra           Colm         Pitched - ordered                    18 Oct       |
| Murphy's Pharmacy        Aoife        Follow up                            22 Oct       |
| Kelly's, Avoca           Colm         Not yet visited                      25 Oct       |
| ...                                                                                   |
+---------------------------------------------------------------------------------------+
```

> **DECISION M4.1 — Campaign detail always opens on Overall. Settled.**
> The campaign is the primary object, so the first view answers whether the launch is progressing and what outcomes it is producing. `By rep` is a secondary diagnostic pivot for finding uneven execution; it does not replace Overall as the return/default view.

> **DECISION M4.2 — progress and outcomes remain visible above either detail view. Source-aligned.**
> `22 of 38 done` and the full outcome breakdown are the campaign's stable headline. Switching to `By rep` changes the table beneath, not the campaign summary, so the manager never loses the overall context while investigating a person.

> **DECISION M4.3 — every outcome count filters the visit list. Settled.**
> Ordered, Declined, Follow up, Not yet visited and Cancelled are interactive summary filters. Selecting one shows only visits behind that count and gives the active count a selected state with a clear text label; selecting it again or choosing `All visits` clears the filter. Counts themselves never change while filtered, so the manager retains the campaign totals.

> **DECISION M4.4 — extending remaining visits uses a select-all review. Settled.**
> `Extend remaining...` opens a review containing every open visit, selected by default. The manager can exclude individual rows and sees the proposed new campaign window before applying it. Completed and cancelled visits are not included or changed. Applying the review moves only the selected open visits; excluded visits retain their existing dates.

**Open:**
- *(none for M-04)*

---

## M-05 · Schedule conflicts

**Job:** show the manager where their change overtook a rep's offline change, with enough context to settle it directly.

```
+---------------------------------------------------------------------------------------+
| Schedule conflicts                                                   3 conflicts      |
+---------------------------------------------------------------------------------------+
| Rep       Location             Affected visit       Applied manager change            |
| Colm      Murphy's Pharmacy    Thu 8 Oct            Covered by Aoife                  |
|           Rep's change: moved to Thu 8 Oct                         [ Open visit > ]    |
|                                                                                       |
| ...                                                                                   |
+---------------------------------------------------------------------------------------+
```

The manager's change remains applied. This is an exception notice, not a separate approval queue: the conflict clears when the manager amends their change or the rep reschedules the visit, as defined by US-014. Changing only Suggested Day does not create a conflict.

> **DECISION M5.1 — conflicts are ordered by the earliest affected visit. Settled.**
> Overdue affected visits appear first, followed by visits due today and then future visits in ascending scheduled order. This makes the default list reflect operational urgency rather than the time at which the conflict was detected.

> **DECISION M5.2 — entering from a rep row carries a removable rep filter. Settled.**
> Selecting a rep's conflict count on M-01 opens M-05 filtered to that rep. The page names the active rep filter in text and offers `View all conflicts`; clearing it restores the full earliest-first list. Opening M-05 directly shows all conflicts.

> **DECISION M5.3 — cleared conflicts remain accessible as resolved history. Settled.**
> When either party makes an amendment that clears a conflict, it leaves the unresolved operational list but remains available to the manager in a resolved-conflicts history. The history preserves the rep's overtaken change, the manager change that applied, how the conflict was cleared, who made that clearing amendment and when.

> **DECISION M5.4 — Open and Resolved are separate views. Settled.**
> M-05 opens on `Open`, keeping the manager's operational workload distinct from audit history. `Resolved` is an adjacent, clearly labelled view; switching views retains any active rep filter so a manager who arrived from a rep row can inspect that rep's open and historical conflicts without rebuilding context.

> **DECISION M5.5 — resolved conflicts are ordered by most recently cleared. Settled.**
> The Resolved view sorts by clearing timestamp descending so the conflict most recently discussed or amended is easiest to find. Each row still displays the affected visit date separately; sorting by clearance does not rewrite the event chronology.

**Open:**
- *(none for M-05)*

---

## M-06 · Territory assignment

**Job:** set up or hand over a rep's book of territory, most often for a new rep, and see before saving which Locations change owner.

```
+------------------------------------------------------------------------------------------+
| < Coverage              Colm                                     Reports to: M. Byrne    |
+------------------------------------------------------------------------------------------+
| 117 Locations as Primary  ·  Specialist: none                      [ Add assignment ]    |
|                                                        [ Take over from another rep... ] |
+------------------------------------------------------------------------------------------+
| ASSIGNMENTS          Filter [ Town or area...        ]                   [ Transfer... ] |
|                                                                                          |
| v Wicklow (County)                 140 Locations · 23 carved out (Rathdrum -> Aoife)     |
|     Bray                            31                                                   |
|     Greystones                      18                                                   |
|     Arklow                          22                                                   |
|     Rathdrum                        23 · carved out -> Aoife                             |
|     ...                                                                                  |
|   Byrne's Chemist (Location)         1 · assigned directly                               |
+------------------------------------------------------------------------------------------+
| History >                                                                                |
+------------------------------------------------------------------------------------------+
```

> **DECISION M6.6 — actions sit next to what they act on. Settled (23 Sep 2026).**
> The regions follow Coverage US-008. The inbound actions (`Add assignment`, `Take over from another rep...`) sit by the rep's summary. `Transfer...` sits on the Assignments list, because a transfer starts from filtering and selecting its rows (proximity). History is at the foot.
> **Rejected:** all three actions in a single bar at the top, which separates Transfer from the selection it depends on.

> **DECISION M6.1 — the page is anchored on the rep. Settled.**
> The usual triggers are about a person: most often a new rep, sometimes a rep leaving and being replaced. The page is a rep's book (their Territory Assignments by level, carved-out counts, effective Location count), and *Add assignment* starts from there. The rarer geography-led trigger, the company moving into a new territory, reaches the same result through Unassigned Locations (M-15) and Location coverage (M-07).

> **DECISION M6.2 — transfer assignments in bulk: filter, select all, exclude, choose a rep. Settled.**
> A rep's page lists their Territory Assignments. The manager can filter the list, choose `Transfer...`, and get a review with every assignment in the filtered set selected, from which they can remove individual rows before choosing the receiving rep. The impact preview then shows the assignments moving, the Locations changing owner and the open visits going to Handover (M-08). Transfer moves the **assignment itself** (Wicklow County goes from Colm to Niamh), so Locations keep resolving through the territory and future Locations in it follow the new rep. Splitting a leaving rep's patch across several reps is done by repeating the transfer with a different filter or selection. This follows the select-all-with-exclusions pattern of M2.3 and M4.4.
> Bulk reassign (M-09) stays for temporary moves: it creates direct Location assignments saved as a reversible batch.
> **Rejected:** only transferring Locations one by one, or through M-09's direct assignments, for a permanent replacement.

> **DECISION M6.3 — a County row expands to its Towns, which can be filtered and selected. Settled.**
> The assignment list is hierarchical: a County assignment shows its Towns as children, with their Location counts, and carved-out Towns are marked with their owner. The filter and the transfer selection work at Town level, so a County can be divided without first restructuring it.
> ```
> [x] Wicklow (County)               117 Locations
>     [x] Bray                        31
>     [x] Greystones                  18
>     [ ] Arklow                      22
>     [ ] Aughrim                      9
>         Rathdrum                    23 · carved out → Aoife
>     ...
> ```

> **DECISION M6.4 — partial selections become carve-outs; a County moves with its last Towns, and the preview says so. Settled.**
> Transferring some of a County's Towns creates a Town assignment for each one, owned by the receiving rep, and the County assignment stays put. Transferring the whole County moves the County assignment. When a transfer takes every remaining Town that a rep holds through a County, the County assignment moves as well, so no rep is left holding an empty County that would catch new Towns. The impact preview states it: `Wicklow (County) moves to Ciara with its last Towns.` Ciara then holds *Wicklow (County) · 3 towns carved out → Niamh*, and a new Town in Wicklow resolves to her. The same rule applies one level up: a Region moves with its last Counties (confirmed 23 Sep 2026: a Region is just the parent of its Counties).
> **Rejected:** a warning alone, which leaves an empty County with a rep who has left.

> **DECISION M6.5 — the receiving rep's page can pull assignments: in place, or a whole book. Settled.**
> On the receiving rep's page, `Add assignment` lists areas other reps hold, with their owner (`Rathdrum — Aoife's`). Choosing one asks `Transfer Rathdrum from Aoife to Niamh?` and continues to the same impact preview and handover as a push transfer. The page also offers `Take over from another rep...`, which opens the M6.2 transfer review on the chosen rep's list with the receiving rep already set. A push from the giving rep's page and a pull from the receiving rep's page produce the same transfer.
> **Superseded:** the dead-end error in Coverage US-001 scenario 3 (`remove that assignment first`), for a manager adding an area at the same level as another rep's assignment.
> **Rejected:** blocking and linking to the other rep's page.

**Open:**
- ~~Does M6.4's "moves with its last Towns" rule apply one level up, a Region moving with its last Counties?~~ **Resolved (23 Sep 2026):** yes. A Region is just the parent of its Counties, so the same rule applies.

---

## M-07 · Location coverage

**Job:** answer "who covers this shop, and why?", and act on that Location without going elsewhere.

```
+------------------------------------------------------------------------------------------+
| < Coverage              Murphy's Pharmacy · Rathdrum                                     |
+------------------------------------------------------------------------------------------+
| Primary      Aoife (via Rathdrum)                                                        |
|              [ Change just this shop to... ]   [ Transfer Rathdrum (Town, 23) to... ]    |
| Specialists  Brian (Brand: SunCo)                                                        |
+------------------------------------------------------------------------------------------+
| [ Add one-off visit ]                                                     History >      |
+------------------------------------------------------------------------------------------+
```

Unassigned (Walsh's Shop, Laragh):

```
+------------------------------------------------------------------------------------------+
| Primary      Unassigned                                                                  |
|              [ Assign Laragh (Town) to... ]   4 other Locations in Laragh are unassigned |
|              [ Assign just this shop to... ]                                             |
+------------------------------------------------------------------------------------------+
```

> **DECISION M7.5 — History opens as its own view, newest first. Settled (23 Sep 2026).**
> The page stays short with the manager's actions in view, and the full record is one click away (US-002 scenario 4).
> **Rejected:** full History inline; the latest change inline with the rest behind a link.

> **DECISION M7.6 — layout as drawn. Settled (23 Sep 2026).**
> Coverage leads (Primary with its Source and change actions, then Specialists). The Location's other manager actions sit in a footer row, with `Add one-off visit` on the left and `History >` on the right.

> **DECISION M7.1 — M-07 is the manager's Location page, with coverage first. Settled.**
> Coverage (Effective Owner with its Source, Specialists) is the headline, followed by the Location's manager actions: assign or change coverage (M-06), add a one-off Visit Due (M-16), and Assignment History. One page per Location means the manager never has to remember which screen holds which fact about a shop. The Customer Directory keeps ownership of the Location's master data (brief §10, area 8).

> **DECISION M7.2 — an unassigned Location leads with the Town assignment and keeps the single-Location one. Settled.**
> `Primary: Unassigned` offers `Assign Laragh (Town) to...` first, with the cause visible — `4 other Locations in Laragh are also unassigned` — and `Assign just this shop to...` beside it. Leading with the Town fixes the reason the Location is uncovered and avoids accumulating direct assignments that would override future territory changes; the single-Location route stays for genuine one-offs. This matches US-007 scenarios 2 and 3, which offer both from the Unassigned list.
> **Rejected:** offering only the direct Location assignment.

> **DECISION M7.3 — no visits section; the page stays about coverage. Settled.**
> M-07 shows coverage (Effective Owner with Source, Specialists), the Location's manager actions and Assignment History. Open Visit Dues, the last Call and Overdue/Missed investigation stay in Visit Planning (M-01). M7.1 adds actions for a Location, not a second place to monitor it.
> **Rejected:** a compact Visits section; a single `Last Call` line.

> **DECISION M7.4 — changing an already-covered Location leads with "just this shop". Settled.**
> From Murphy's Pharmacy (`Aoife (via Rathdrum)`), `Change just this shop to...` comes first and creates a direct Location assignment. `Transfer Rathdrum (Town, 23 Locations) to...` sits beside it and continues to the M6.5 pull transfer. Unlike M7.2, a working assignment already exists, so a single exception is the likely intent.

> **Addition from C8.4 (26 Sep 2026) — Set up online ordering… joins the footer row.** It sits beside `Add one-off visit`, and extends M7.6's footer rather than replacing it. The manager picks one of the Location's contacts, confirms the invitation email and sees "Mary Walsh will be able to order for: Hickey's Rathdrum" (Self-service US-001 S4). A manager's account needs no approval (US-001 S2), so the invitation is sent on save. Rules in `06-customer.md` C-08.
>
> ```
> | [ Add one-off visit ]  [ Set up online ordering... ]                     History >      |
> ```

**Input for M-10 (from Colm, 22 Sep 2026):** the more common change from a shop is not its Primary Rep but a **secondary (specialist) assignment**: every Location matching a profile (for example "customer campaign X") **within an area** should get a given rep as specialist. Coverage US-006 scopes are Customer, Location Profile or Brand with no geographic limit, so this needs a scope combined with an area. To be settled on M-10. A Location can hold **many** profiles (BR-NEW-005), so a profile scope matches any Location holding that profile.

**Open:**
- *(none for M-07)*

---

## M-08 · Impact preview & handover

**Job:** before a change of owner is saved, show which Locations move and settle what happens to the previous rep's open visits, without stranding any.

> **DECISION M8.1 — the transfer asks once whether the previous rep is leaving; if so, open visits go to the new owner as visits needed. Settled.**
> Starting a transfer asks `Is Colm leaving?`. **If yes**, Leave isn't offered and there's no per-visit handover: every open Visit Due at a Location that changes owner moves to that Location's new Primary Rep with its Scheduled Day cleared, so it appears as a visit needed on their planner. The new rep's schedule is built fresh around them, so the leaving rep's scheduled days aren't worth preserving. The impact preview states it: `9 open visits move to Niamh as visits needed (2 Overdue).` When a leaving rep's patch is split, each visit follows its own Location's new owner. **If no** (a carve-out while the rep stays), the source handover applies unchanged: Move or Leave per visit, Apply to all, and undecided visits become Handover Pending.
> **Rejected:** always offering Leave; inferring "leaving" from the rep's account status.

Impact preview when the previous rep is leaving:

```
+------------------------------------------------------------------------------------------+
| Transfer to Niamh                                                    Colm is leaving     |
+------------------------------------------------------------------------------------------+
| Wicklow (County) and Byrne's Chemist move to Niamh                                       |
| 117 Locations change owner                                                         v     |
|                                                                                          |
| 9 open visits move to Niamh as visits needed                                       v     |
|    2 Overdue · inherited from Colm, not counted in Niamh's performance                   |
|    3 had a Scheduled Day, now cleared                                                    |
+------------------------------------------------------------------------------------------+
|                                                      [ Back ]   [ Transfer to Niamh ]    |
+------------------------------------------------------------------------------------------+
```

> **DECISION M8.2 — inherited visits keep their windows, are marked as inherited, and don't count against the new rep. Settled.**
> Each Visit Due that was open at the handover and moves to the new rep keeps its due window. It shows `inherited from Colm` wherever it appears (planner, M-01, visit detail). It still counts in the new rep's operational figures on M-01 so it gets done, but it is excluded from their performance (R-03, M-13). That includes a Missed record if they don't reach it. The marker belongs to the visit and ends when the visit closes, completed or Missed. It is not a time-based grace period: new Visit Dues generated after the handover count normally. It applies to visits moved by M8.1's leaving path and by a manager's Move in the source handover (confirmed 23 Sep 2026: the new rep didn't set the window, whoever chose to move it).
> **Rejected:** counting inherited visits as normal; restarting windows from the handover date; a grace period covering everything at the inherited Locations.

**Open:**
- ~~Does M8.2's inherited marker also apply to visits moved by `Move` in a carve-out handover?~~ **Resolved (23 Sep 2026):** yes. It's one rule everywhere; the new rep didn't set the window.

---

## M-09 · Bulk reassign & batch reversal

**Job:** move a set of Locations to another rep for a long period, then send them back later without relying on memory.

> **DECISION M9.1 — short leave never uses bulk reassign; ownership stays with the absent rep. Settled.**
> For short leave, the manager talks to the covering reps and decides per visit whether the Location needs a visit during the leave or can wait. Visits that are needed are handled by another rep for this visit only (Cover in M-02). Visits that can wait are extended, and Colm's schedule restarts when he returns. That's the M-02 absence flow (Extend / Keep / Cover), with no change of ownership, no batch and no reversal. Bulk reassign is only for absences long enough to justify moving ownership.

> **DECISION M9.2 — M-09 is kept for long absence, as a named, reversible batch; open visits go to the covering rep as visits needed. Settled.**
> For an absence such as six months of maternity leave, the manager bulk-reassigns the Locations as a named batch (`Colm → Aoife, Maternity cover, 5 Oct 2026, 23 Locations`), creating direct assignments as Coverage US-004 specifies. The absent rep is unavailable, so there's no Leave and no per-visit handover: open visits move to the covering rep with Scheduled Day cleared and are marked `inherited from Colm`, as in M8.1 and M8.2. The batch is the record that makes the later reversal possible without relying on memory.
> **Rejected:** dropping M-09 and handling long absence as two ordinary M-06 transfers.

> **DECISION M9.3 — reversing a batch uses the per-visit handover; the manager decides. Settled.**
> On reversal both reps are working, and the returning rep may come back part-time, so the covering rep's open visits at the returning Locations go through the source handover: Move or Leave per visit, Apply to all, and undecided visits become Handover Pending. Leave lets Aoife finish visits she already has planned. Visits that Move are marked `inherited from Aoife` (M8.2). A part-time return can also use the source's partial reversal, unticking Locations to leave them with the covering rep.
> **Rejected:** mirroring the outbound move, where every open visit returns as a visit needed.

> **DECISION M9.4 — reversal restores the territory-derived owner and flags Locations where that isn't the returning rep. Settled.**
> Reversing removes the batch's direct assignments, so each Location resolves through its territory again (`Colm (via Wicklow)`). If a territory changed during the batch and a Location would now resolve to someone else, the preview says so (`3 Locations would resolve to Niamh via Wicklow`), and each of those rows offers `Assign to Colm directly` or `Leave with Niamh`. This resolves Coverage clarification 2 and US-005's open question.
> **Rejected:** always creating direct assignments to the returning rep; always restoring without flagging.

**Open:**
- *(none for M-09)*

---

## M-10 · Specialist assignments

**Job:** attach a rep to the Locations that match a rule, without making them the owner, so campaign visits and scoped products reach the right person.

> **DECISION M10.1 — scopes combine conditions with AND by default; an advanced mode allows OR and grouping. Settled.**
> The default builder takes any number of conditions (Customer, Location Profile, Brand, Area), all of which must hold: `Brian · Profile: Customer campaign X · in Wicklow`. It reads as one sentence and covers the common case from M-07, a profile within an area. `Advanced` opens a full AND/OR builder for rules the default can't express (progressive disclosure). Scopes of either kind stay live: a Location that gains the profile or opens in the area joins automatically. Before saving, the scope shows as a sentence with the number of Locations it currently matches.
> **Rejected:** a single condition with no area (the source model); an AND/OR builder as the only mode.

*Follows from the source:* a specialist sees extra products on their Order Pad only through a Brand condition in their scope. A scope with no Brand condition (for example Profile + Area) routes visits but adds no products.

> **DECISION M10.2 — campaign routing prefers the specialist matching the campaign's link; otherwise the manager chooses. Settled.**
> In campaign Review (M-03, M3.3), each row starts from the specialists at that Location whose scope matches the campaign's link (a SunCo campaign → the SunCo specialist). If the campaign has no link, every specialist at the Location is a candidate.
> - **None** → the Location's Primary Rep, as the source specifies.
> - **One** → preselected, with other specialists at the Location noted (`also matches Brian`).
> - **More than one** → no preselection. The row shows `2 specialists match — choose` and Review can't advance until the manager chooses. Bulk selection can resolve many rows at once.
>
> **Rejected:** the most specific scope winning; falling back to the Primary Rep when the link doesn't decide.

**Open:**
- *(none for M-10)*

---

## M-11 · Multi-branch order grid (laptop)

**Job:** build and adjust a whole chain's order at a desk, seeing every branch at once. Used mainly by reps at a desk (Master & Branch US-006); shared with the rep website and echoed for customers (C-06).

> **DECISION M11.1 — rows open with the chain's recently ordered products; search adds more; a toggle shows the full Agreed Range. Settled.**
> The grid opens with the products on any branch's last 3 Accepted Orders (the rule the Suggested List already uses) as rows with **empty** cells. Quantities are never pre-filled (T7.7). Search adds any other product. `Show full Agreed Range` lists the rest of the chain's agreed products. This keeps the grid near the size of a typical session instead of the whole range.
> **Rejected:** opening on the full Agreed Range; opening empty.

> **DEFERRED M11.2 — grid layout to be revisited with usage feedback.** Version 1 is the brief's flat products × branches grid. Two alternatives Colm raised are to be evaluated once the screen is in use: product rows with collapsible branch sub-rows, and branch groups with collapsible product sub-rows. The layout can be changed later without touching the split outcome, which is identical whichever layout is used.

> **DECISION M11.3 — changing `All` fills only the cells not changed by hand. Settled.**
> Typing in a row's `All` cell sets every branch cell that hasn't been edited by hand. Hand-edited cells keep their values and are marked, and the row summarises `36 × 8 branches, 2 adjusted`, as the tablet's running summary does. A `Clear adjustments` action on the row returns every cell to the `All` value when the buyer really does mean every branch.
> **Rejected:** `All` overwriting hand edits; a confirmation dialog each time.

*Follows from the source:* branch columns use the same selection as the tablet: every branch is selected by default, Closed branches are excluded, and Temporarily Closed branches are flagged but stay selected (US-005). The grid ends in the same Split Review (US-007).

> **DECISION M11.4 — with several Agreed Ranges, a range picker beside the grid replaces `Show full Agreed Range`. Settled 26 Sep 2026.**
> The grid still opens on recently ordered products (M11.1). A picker beside it lists the chain's Agreed Ranges, oldest first as on the tablet (T7.18), and adds the chosen range's products as rows with empty cells. The laptop grid deliberately differs from the tablet pad's stacked sections (T7.15): M11.1 keeps the grid near the size of one session, and a picker grows it by one range at a time.
> **Rejected:** one option adding every range; one option per range.
> *Confirmed 26 Sep 2026:* the picker's first option is "Recently ordered" (M11.1's opening, no range); rows with quantities stay at the top whatever is picked, as in the customer grid (C5.8); a product in several ranges shows once.
> *Revised 26 Sep 2026 (BR-NEW-008 rule 0):* the picker lists the catalogue Ranges assigned to the chain, by their catalogue names; the decision otherwise stands.

```
+------------------------------------------------------------------------------------------------+
| Hickey's chain order                       Range [ Recently ordered          v ]   [ Search ]    |
|                                                  |  Recently ordered           |               |
|                                                  |  Everyday (30)              |               |
|                                                  |  2026 Christmas gift packs  |               |
+------------------------------------------------------------------------------------------------+
```

**Open:**
- ~~**BR-NEW-008 follow-up (26 Sep 2026):** a chain may now hold multiple separate Agreed Ranges. M11.1's recent-order opening remains settled, but `Show full Agreed Range` needs a choice of which range to show. Do not infer the customer grid's default-range behaviour for reps. M11.2's layout remains deferred.~~ **Resolved 26 Sep 2026 — range picker (M11.4).** M11.2's layout remains deferred.

---

## M-12 · Set targets (by period / by range)

**Job:** set each rep's expectation for a period in one sitting, with figures that fit each patch.

> **DECISION M12.1 — each rep's figure opens pre-filled with the previous period's target, editable. Settled.**
> Choosing a period fills each rep's row with their target for the same subject (rep, or rep and Range/Product) in the previous period. A rep with no target in the previous period stays blank, because blank still means no target. The running total starts from the carried figures. This is a deliberate exception to *propose, don't impose*: targets usually change incrementally, and retyping every figure costs more than the anchoring risk.
> **Rejected:** empty fields; empty fields with the previous period shown alongside.

> **DECISION M12.2 — saving confirms carried figures; the confirmation says how many were carried unchanged. Settled.**
> Save makes every figure on the screen a target for the new period, whether edited or carried. The dated history records them as set by this manager on this date (US-001 scenario 5). The save confirmation then states `6 of 8 targets were carried forward from Q4 unchanged. You can update them later.` and names those reps, so an interrupted review is visible without blocking the save.
> **Rejected:** marking carried rows until touched; requiring explicit confirmation of carried rows.

**Open:**
- *(none for M-12)*

---

## M-13 · Manager performance overview

**Job:** see who's behind, early enough to act before the period ends.

> **DECISION M13.1 — "furthest behind" defaults to percentage points behind pace; the manager can re-sort by money behind pace. Settled.**
> Pace is straight-line, as in R-03. By default reps are ordered by how far their percentage of target trails the expected percentage for this point in the period (`Aoife 50% vs 75% pace · 25 pts behind`), which ranks people against their own expectation whatever its size. `Sort by € behind pace` puts the biggest holes in the team's number first (`Colm €3,600 behind`). The choice is a sort, not a filter, and every rep stays listed. Reps with no target stay in their separate list (US-005 scenario 5).
> **Rejected:** a single fixed order.

> **DECISION M13.2 — when a rep has both value and units targets, value decides the order. Settled.**
> Colm (value 8 pts ahead, units 15 pts behind) ranks as 8 pts ahead. The units figure and its pace gap are shown on his row but don't affect the order. A rep with a units-only target ranks by units, since that's the only measure they have.
> **Rejected:** ranking by the worse of the two; averaging them.

> **DECISION M13.3 — under `Sort by € behind pace`, reps with units-only targets sit in their own group. Settled (23 Sep 2026).**
> They have no € figure, so they can't be placed in a € ranking without implying something false. Listing them at the bottom would read as "least behind" even when they're the worst. They appear below the € list under their own heading, `Units targets`, ordered by points behind pace. Under the default points sort they stay interleaved (M13.2), because points are comparable.
> **Rejected:** appending them after the € rows without a heading; interleaving them by points under the € sort.

*Follows from the source:* the By Range view, the separate list of reps without targets, the team-level Unfulfilled note, and `Attributed` / `Captured` labelled as different measures (US-005, US-006).

**Open:**
- *(none for M-13)*

---

## M-14 · Rep permissions

**Job:** grant or remove a rep's access to Restriction Groups, with a record, whether setting up one rep or a whole trained cohort.

> **DECISION M14.1 — two views over one permission record: per rep and per group. Settled.**
> **Per rep** (onboarding): the rep's page lists every Restriction Group with Granted / Not granted and the last change (US-009 scenario 3). **Per group** (training cohorts): the group's page lists reps with their state. The manager ticks several and enters **one** reason (`Completed training 15 Sep 2026`) that applies to every selected rep, and each grant is recorded separately with date, actor and reason. Both views read and write the same records, and a change made in one shows immediately in the other.
> **Rejected:** a per-rep view only; a per-group view only.

*Follows from the source:* reps the manager doesn't manage are shown but can't be changed, unless the manager is a Head Office User (US-009 scenario 4). A change takes effect at the rep's next Sync, and lines already captured still send (valid when captured).

> **DECISION M14.2 — removing a permission states its consequence before saving. Settled (23 Sep 2026).**
> The effect is delayed (next Sync) and lands on someone else's tablet, so the manager would never see it. The save states it first (feedforward): `Colm will lose Pharmacy-only medicines at his next Sync. 2 lines already on In Progress orders will still send.` Removing from several reps on the group's page states it once for all of them: `3 reps will lose Pharmacy-only medicines at their next Sync. 5 lines already on In Progress orders will still send.`
> **Rejected:** a plain save with the effect explained only in help text.

**Open:**
- *(none for M-14)*

---

## M-15 · Unassigned locations

**Job:** make sure no shop is silently uncovered.

An unassigned Location has no Primary Rep, so it generates no Recurring Visit Dues. It never goes Overdue and never appears in M-01's exception columns. Without a push it is invisible.

> **DECISION M15.1 — M-01 shows a standing unassigned count whenever it is above zero. Settled.**
> The Visit Planning overview, the manager's landing page, shows `6 Locations unassigned >` in its header whenever any exist, linking to M-15. At zero the line doesn't appear. M-15 itself lists them grouped by Town (US-007), and each row leads with `Assign <Town> to...` and the Town's unassigned count, as on M-07 (M7.2).
> **Rejected:** pull only (M-15 and By region); an additional "new since" signal.

> **DECISION M15.2 — the unassigned count is scoped to the manager's area, derived from the nearest held geography. Settled (23 Sep 2026).**
> A count shown identically to every manager is owned by none of them (diffusion of responsibility), so each manager sees only the unassigned Locations nearest their own team. Nothing is set up: an unassigned Location belongs to every Sales Manager whose reps hold another Town in the same County; failing that, a Territory Assignment in the same Region; failing that, it goes to Head Office Users. When two managers' reps both hold Towns in the County, both see it. Example: Niamh (reports to M. Byrne) holds Arklow and Rathdrum; a new shop in Aughrim, a Town nobody holds, counts on M. Byrne's overview.
> **Rejected:** a company-wide count for everyone; a declared area set per manager (one more setting to keep in line with the territory).

> **DECISION M15.3 — M-15 opens on the count that was clicked. Settled (23 Sep 2026).**
> M-01's header reads `4 Locations unassigned in your area >`. A Head Office User also sees `2 with no nearby team >`. Each link opens M-15 filtered to what it counted, with `All unassigned` one click away, so the page keeps the header's promise and the ownership M15.2 created isn't diluted by the default view.
> **Rejected:** opening on every unassigned Location with the viewer's area marked or sorted first.

**Open:**
- ~~Is the unassigned count company-wide for every manager, or limited to some notion of a manager's area?~~ **Resolved (23 Sep 2026):** scoped, derived from nearest held geography (M15.2).

---

## M-16 · Add a one-off Visit Due

**Job:** ask for one additional visit at a Location, with a clear reason and due window, without changing its recurring schedule.

The form must capture the Location, due window, structured Due Reason Type, optional explanatory text and optional Suggested Day. Saving creates one Visit Due which is completed by the next recorded Call at that Location, as defined by US-015.

> **DECISION M16.1 — the flow is anchored on the Location. Settled.**
> A one-off visit is triggered by something about a Location (a complaint, a customer request, a problem account), so the manager has the Location in mind before any rep or date. Starting from the Location record carries it into the form. The rep is not the starting point: the nature of the visit decides who should go, and the manager may contact reps to see who is most available.

> **DECISION M16.2 — the Primary Rep is the default; choosing another rep affects this visit only. Settled.**
> The form opens with the Location's Primary Rep. The manager may choose another rep, usually one already assigned to the Location in a non-primary role, for one of two reasons: the Due Reason calls for particular knowledge (a specialist range, for example), or another rep will be nearer the Location in the next few days. Sending another rep is a convenience for this visit. It never changes the Location's assignment or Assignment History, which is consistent with M3.3 for campaign visits. The form states the consequence in text: `Aoife will make this visit. Colm remains Byrne's Primary Rep.`

> **DECISION M16.3 — the rep picker is a plain list; availability is confirmed with the rep directly. Settled.**
> Reps with any assignment at the Location are listed first, then the rest of the team. The picker shows no role or proximity context. A one-off visit is usually short notice, so the manager contacts the chosen rep to confirm regardless, and that conversation is the availability check. Since the picker doesn't depend on the due window, the form has no required field order.
> **Rejected:** showing assignment role beside each rep; showing each rep's nearest planned day in the due window.

> **DECISION M16.4 — no tablet delivery status for one-off visits. Settled.**
> Saving the visit is the manager's final step in the system. The phone call to the rep is the handover, and the rep syncs after that call to receive the reason and Suggested Day. Even without a sync, any recorded Call at the Location completes the visit (US-015). The saved visit and the confirmation show no `waiting for sync` or `on tablet` state.
> **Rejected:** showing whether the visit has reached the rep's tablet.

> **DECISION M16.5 — a nearby scheduled visit is shown as information only; the manager decides afterwards. Settled (revised).**
> When another visit is scheduled at the Location within a week of the one-off's due window, the form shows it with its rep and Scheduled Day: `Colm is scheduled at Byrne's on Thu 24 Sep.` The notice never blocks saving and asks for no choice on the form. The manager decides after saving what happens to the other visit (keep it, move it to another week so the customer isn't visited too often, or treat it as covered by the one-off). They have to contact the other rep anyway to tell them another visit is happening, and that conversation is where the decision gets made.
> **Rejected:** offering to attach the reason to the existing visit instead; showing nothing; any choice about the other visit on the M-16 form.

> ~~**DECISION M16.6 — when there is an overlap, the manager chooses whether the one-off's Call also completes the recurring visit.**~~ **Superseded by revised M16.5.** Colm rejected putting the choice on the form: the other visit is handled afterwards through normal planning, after speaking to its rep.

> **DECISION M16.7 — a one-off's Call completes only the one-off, whoever holds the recurring visit. Settled.**
> Aoife's Call at Byrne's completes her one-off Visit Due and leaves Colm's open recurring visit untouched. That visit stays open until the manager, Colm, or its own Call deals with it, so every option from M16.5 is still available afterwards: keep it, move it to another week, or cancel it as covered. The manager and reps agree between themselves, and the manager then updates the plan. This is an exception to "One Call clears all".
> The same rule applies when the one-off's rep also holds the recurring visit. The same rep may still need two visits, for example the complaint with the owner and a routine visit with a different contact a few days later. When one visit is enough, the manager doesn't save the one-off: they move the existing visit forward in the planner and give the rep the reason on the phone. M-16 offers no in-form "move instead" action.
> **Rejected:** a same-rep exception where one Call completes both; M-16 moving the existing visit itself.

**Open:**
- ~~From which manager contexts should this flow be available?~~ **Resolved — Location first (M16.1).**
- ~~**Does the one-off's Call complete the other rep's open recurring visit?**~~ **Resolved — no (M16.7).**
- ~~Rep choice and override scope.~~ **Resolved — Primary Rep default, visit-only override (M16.2).** Amends US-015's "effective rep follows the Location assignment rules".

---

## M-17 · Online ordering approvals

> Not in the brief as a manager screen. Added in session (26 Sep 2026) from C-08, and numbered after M-16 so it doesn't clash with the brief's IDs. **It replaces the brief's H-07 Customer user account approval**, whose story (Head Office US-006b) names the Sales Manager as approver; account requests no longer appear on H-01.

**Job:** approve or decline customer online-ordering accounts that reps have set up, and re-invitation requests from Locations with no assigned rep, without them waiting for the manager to go looking.

**From the source:** a rep-created account is Pending approval in "my manager's queue"; on approval the invitation is sent; a decline needs a reason, which the rep sees (Self-service US-001 S1, S3; Head Office US-006b S1–S3, which also asks for the contact's Locations and the "Hickey's Head Office and its 12 branches" scope line). Re-invitations normally go to the rep (C1.2), except for a Location with no assigned rep, which goes to the manager who sees it as Unassigned (M15.2).

```
+------------------------------------------------------------------------------------------------+
| Online ordering approvals (2)                                                                   |
+------------------------------------------------------------------------------------------------+
| NEW ACCOUNTS                                                                                    |
| Mary Walsh · Hickey's Rathdrum          Set up by Colm · 26 Sep                                 |
|   mary@hickeys.ie · Will be able to order for: Hickey's Rathdrum                                |
|                                                            [ Decline... ]   [ Approve ]         |
+------------------------------------------------------------------------------------------------+
| NEW INVITATIONS (UNASSIGNED LOCATIONS)                                                          |
| Tom Kelly · Byrne's Londis              Asked 26 Sep · last invitation expired                  |
|                                                                   [ Send new invitation ]       |
+------------------------------------------------------------------------------------------------+
```

```
  Decline, inline
  | Reason for Colm (required)  [ Contact has left the business          ]  |
  |                                          [ Cancel ]   [ Decline ]       |

  Empty
  | Nothing waiting for approval.                                           |
```

> **DECISION M17.1 — a dedicated approvals page, announced by email. Settled 26 Sep 2026.**
> Customer-account approvals get their own manager page instead of a count in M-01's exceptions column, which stays about visit planning. When a rep sets up an account (C8.1), or a re-invitation arrives for an unassigned Location (C1.2), the manager is emailed with a link that opens this page. The email informs and links; the approval itself happens on the page, where a decline can carry its required reason.
> **Rejected:** a count in M-01's Other exceptions column; approving from links in the email alone.
> *Confirmed 26 Sep 2026:* two groups (new accounts, then re-invitations); Approve acts at once; Decline opens an inline required reason addressed to the rep by name; approved and declined items leave the list.

**Open:**
- ~~Where does a manager create an account directly (US-001 S2, no approval)?~~ **Resolved 26 Sep 2026 — from M-07's footer row (C8.4), not from this page.**
