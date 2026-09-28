# 03 — Rep website (R-01, R-02, R-03, R-04)

**Device assumptions:** laptop, online, same person as the tablet user but in a different posture — sitting down, planning, once or twice a week.
**Consequence:** everything here is deliberately *not* on the tablet. If a frame below would work on a tablet, it's probably in the wrong file.

---

## R-01 · Planner

**Job:** turn a pile of due visits into a realistic schedule. §10 calls it the most complex single layout in the system.

```
+------------------------------------------------------------------------------------------------+
| Planner       [ Day ] [ Week ] [ Month ]     < 21-27 Sep >     [ Today ]                        |
+---------------------------------------------------------------------+--------------------------+
| MON 21        TUE 22        WED 23        THU 24        FRI 25       | UNSCHEDULED (23)         |
| 6h30 / 8h     Over by 20m   ABSENT        4h15 / 8h     2h00 / 8h    | [ List ]  [ Map ]        |
|                                                                      | Sort: deadline      v    |
| +---------+   +---------+   +---------+   +---------+   +---------+  |                          |
| |Quinn's  |   |Hickey's |   |         |   |Kelly's  |   |Byrne's  |  | RATHDRUM (4)             |
| |Rathdrum |   |Rathdrum |   | Annual  |   |Avoca    |   |Aughrim  |  | [x] Doyle's Pharmacy     |
| |45m      |   |60m  (!) |   | leave   |   |45m      |   |30m      |  |     due 25 Sep      (!)  |
| +---------+   +---------+   |         |   +---------+   +---------+  |     Autumn range deadline|
| +---------+   +---------+   |         |   +---------+                | [ ] Maher's Centra       |
| |Carey's  |   |Byrne's  |   |         |   |O'Brien's|                |     due 28 Sep           |
| |Arklow   |   |Rathdrum |   |         |   |Avoca    |                | [ ] Nolan's Gala         |
| |60m      |   |45m      |   |         |   |90m      |                |     due 2 Oct            |
| +---------+   +---------+   +---------+   +---------+                | [ ] Walsh's Spar         |
| +---------+   +---------+                                            |     due 4 Oct            |
| |Murphy's |   |Fitz's   |                                            |                          |
| |Rathdrum |   |Wicklow  |                                            | AUGHRIM (3)              |
| |45m      |   |45m      |                                            | [ ] ...                  |
| +---------+   +---------+                                            |                          |
|                                                                      +--------------------------+
|                                                                      | 2 selected               |
|                                                                      | [ Schedule on...  v ]    |
+---------------------------------------------------------------------+--------------------------+
```

**Month view — overview, not individual visit cards:**

```
+---------------------------------------------------------------------+
| September 2026                                      <      >        |
+-------------+-------------+-------------+-------------+-------------+
| Mon 21      | Tue 22      | Wed 23      | Thu 24      | Fri 25      |
| 3 visits    | 4 visits    | Absent      | 2 visits    | 1 visit     |
| Rathdrum    | Rathdrum    |             | Avoca       | Aughrim     |
| Arklow      | Wicklow     |             |             |             |
+-------------+-------------+-------------+-------------+-------------+
| Mon 28      | Tue 29      | Wed 30      |             |             |
| 5 visits    | No visits   | 2 visits    |             |             |
|             |             | Tinahely    |             |             |
+-------------+-------------+-------------+-------------+-------------+
```

**Map view of the same panel:**

```
+--------------------------+
| UNSCHEDULED (23)         |
| [ List ]  [ Map ]        |
| +----------------------+ |
| |        (M)           | |
| |   (T)      (T)       | |
| |      (o)      (W)    | |
| |   (o)   (o)          | |
| +----------------------+ |
| M Mon  T Tue  W Wed      |
| (o) not scheduled        |
|                          |
| NO MAP POSITION (3)      |
| [ ] Cullen's, Tinahely   |
| [ ] Regan's, Shillelagh  |
| [ ] Behan's, Aughrim     |
+--------------------------+
| 2 selected               |
| [ Schedule on...  v ]    |
+--------------------------+
```

**Scheduling selected visits through `Schedule on...` — duration can be reviewed before scheduling:**

```
+-----------------------------------------------------------------+
|  Schedule 2 visits on Thursday 24 Sep                      [ X ] |
+-----------------------------------------------------------------+
|  Doyle's Pharmacy, Rathdrum      duration [ 45 min  v ]         |
|  Maher's Centra, Rathdrum        duration [ 45 min  v ]         |
|                                                                 |
|  Thursday is currently 4h15 of 8h. This adds 1h30.              |
|                                                                 |
|  Your manager suggested Thursday for Doyle's Pharmacy.          |
|                                                                 |
|                        [ Cancel ]   [ Schedule 2 visits ]       |
+-----------------------------------------------------------------+
```

**Over-capacity day:**

```
| TUE 22                    |
| Over by 20m          (!)  |
```

> **DECISION R1.1 — the calendar is always on the left and never collapses.**
> The brief says the calendar is always shown. Fixing it left and letting only the right panel switch modes means the map/list toggle never disturbs the thing the rep is building. The shared selection between list and map only works if the destination stays put.

> **DECISION R1.2 — map pins carry a day letter as well as a colour, and the "no map position" list is part of the map view.**
> Two things the brief insists on, and they belong together: the map is never the only route to anything. Locations without coordinates appear *inside* the map panel, not below the whole page, so switching to map view doesn't hide three shops.

> **DECISION R1.3 — drag uses the default duration; selecting the scheduled visit allows amendment. Settled.**
> A drop schedules immediately using the resolved default duration: the longer of the recurring and campaign defaults when both exist at the Location. The calendar updates the day's load at once. Selecting the scheduled visit opens its details and allows the rep to amend duration; the revised load updates immediately. The `Schedule on...` route may still show durations before applying a multi-selection because it already needs a date-selection step.
> **Warnings follow the source stories:** an over-capacity drop succeeds and leaves the day visibly `Over by`; scheduling after the due date still requires the specified confirmation, with the Due Reason included where one exists. An absence day refuses the drop.

> **DECISION R1.4 — "Schedule on..." is always present; drag is an accelerator.**
> Checkbox-and-button is the primary, keyboard-operable route. Drag works for people who prefer it. Drawing it this way round — rather than "drag, with a fallback" — is what keeps the fallback maintained.

> **DECISION R1.5 — day load is stated in hours against capacity, not as a bar.**
> "6h30 / 8h" and "Over by 20m" are both actionable numbers. A fill bar tells the rep they're near the limit without telling them by how much, which is exactly the number needed to decide whether one more 30-minute call fits.

> **DECISION R1.6 — scheduled visits progressively disclose their Due Reason. Partly settled.**
> The unscheduled list continues to show the full reason because it affects when the visit should be placed. In Week view, a scheduled card shows a compact reason indicator without the text. In Day view, the reason text may appear when space permits. Opening the scheduled visit always shows the full reason. Month view has no individual visit cards.
> **Reason model settled:** Due Reason gains a required structured Reason Type whenever a reason is supplied, alongside the manager's free-text explanation. Week uses the type's icon and colour without the explanation; Day may add the explanation when room permits; opening always shows type and full text. An authorised manager or administrator maintains the Reason Type vocabulary and chooses each type's icon and colour from a controlled system set. Colour is paired with the icon and accessible type label; colour alone never carries the distinction.

> **DECISION R1.7 — the scheduler has Day, Week and Month views; the rep selects the timeframe. Settled.**
> This is a standard scheduling tool rather than a fixed one-week planner. Week is the detailed planning frame drawn above; Day supports sequencing a busy day, and Month supplies the longer context needed for multi-week visit cycles. Changing calendar view does not change which unscheduled visits qualify for the panel.

> **DECISION R1.8 — the unscheduled panel has a fixed four-week horizon. Settled.**
> It contains open unscheduled Visit Dues due in the next four weeks, plus anything already Overdue. Later generated visits stay out of the working pile until they enter that horizon. This resolves the inconsistency in Visit Planning US-003, whose main scenario used four weeks while its empty-state copy used two.

> **DECISION R1.9 — Week is the first-use default; the last-selected view is remembered. Settled.**
> Week gives a new user enough context to build a working schedule without Month's density or Day's narrowness. After that first visit, returning to the rep's last-selected Day, Week or Month view preserves their working context rather than making them restore it every time.

> **DECISION R1.10 — Month view shows visit counts, with towns when space allows. Settled.**
> Month is an overview of load and geography, not another rendering of the visit list. Every day always shows `X visits`; town names appear only when the cell has room. Individual visits remain in Day and Week views. The count carries the essential information, so responsive loss of town names never hides how busy the day is.

> **DECISION R1.11 — selecting a populated Month day switches to Day view. Settled.**
> The selected date becomes the active Day view, where individual visits and their details are already represented. This keeps Month lightweight and avoids a second, cramped visit-detail pattern inside a popover.

> **DECISION R1.12 — the unscheduled panel is available in every view and collapses when width is limited. Settled.**
> Day, Week and Month all keep the same scheduling source beside the calendar when space permits. At narrower laptop widths the panel becomes a collapsed `Unscheduled (n)` control that restores the panel on demand; collapsing never clears selection or changes the four-week horizon. The calendar takes the released width rather than compressing below a usable size.

**Source-confirmed drafting calls:**
- **R1.2** is confirmed by Visit Planning US-003: map pins pair day colour with a letter and Locations without coordinates remain available in the map panel.
- **R1.4** is confirmed by Visit Planning US-004: both drag and select-then-`Schedule on...` are required.
- **R1.5** is confirmed by Visit Planning US-005: day load is expressed as time against Working Day, including `Over by` text.

**Open:**
- ~~Absence days: blocked from receiving drops, or droppable with a warning?~~ **Resolved — blocked.** `visit-planning.md` explicitly defines absence days as unschedulable; affected visits use the manager's Extend / Keep / Cover decision rather than being placed onto an absent day.
- ~~Does the unscheduled panel show only visits due within some horizon, or everything outstanding?~~ **Resolved — Overdue plus due within four weeks.**
- ~~Multi-week view for a rep on a 3-week cycle.~~ **Resolved — the scheduler offers Day, Week and Month views selected by the rep.**
- ~~**Due Reason indicator:** generic indicator or structured Reason Types?~~ **Resolved — structured Reason Type plus free-text explanation, maintained by a manager/administrator, who chooses its icon and colour from a controlled system set.**

---

## R-02 · Cycle End digest

**Job:** decide what happened to visits whose cycle ended without a call. Arrives Sunday, once a week.

```
+---------------------------------------------------------------------------------------+
| Cycle end - week ending 20 Sep                                7 visits need a decision |
+---------------------------------------------------------------------------------------+
|                         [ Review Keep Overdue... ]   [ Review Mark Missed... ]        |
+---------------------------------------------------------------------------------------+
| STILL OPEN FROM EARLIER WEEKS (2)                                                     |
| Cullen's, Tinahely      due 6 Sep    2 cycles ago     ( ) Keep Overdue  ( ) Missed    |
| Regan's, Shillelagh     due 6 Sep    2 cycles ago     ( ) Keep Overdue  ( ) Missed    |
+---------------------------------------------------------------------------------------+
| THIS WEEK (5)                                                                         |
| Maher's Centra          due 18 Sep                    ( ) Keep Overdue  ( ) Missed    |
| Nolan's Gala            due 19 Sep                    ( ) Keep Overdue  ( ) Missed    |
|   Deadline was: Autumn range order                                                    |
| ...                                                                                   |
+---------------------------------------------------------------------------------------+
| Selecting Missed reveals an optional reason directly beneath that visit.              |
|                                                                 [ Save decisions ]    |
+---------------------------------------------------------------------------------------+
```

> **DECISION R2.1 — "Still open from earlier weeks" is a separate section at the top.**
> The brief says unactioned visits reappear under "Still open". Putting reappearing items *first* rather than mixed in makes the consequence of ignoring the digest visible — the section grows week on week until someone deals with it.

> **DECISION R2.2 — Mark Missed uses a shared optional reason with per-visit overrides. Settled.**
> The dedicated review page offers one optional reason applied to all selected visits. Each selected row can override that value or clear it when the cause differs. This keeps the common batch case fast without storing an inaccurate shared explanation on exceptions.

> **DECISION R2.3 — no default selection.**
> §2.6 again. Defaulting to Keep Overdue would quietly inflate the overdue list; defaulting to Missed would quietly erase work. The digest exists because neither answer is automatic.

> **DECISION R2.4 — Mark Missed closes and records only; follow-up is manual. Settled.**
> The digest asks the rep to distinguish `Keep Overdue` from `Mark Missed`. Saving Missed closes that Visit Due and records the optional reason. It does not generate a replacement, notify someone to decide, or alter the next fixed-cycle Visit Due. A rep may choose to reschedule by creating a one-off visit, and a manager reviewing missed visits may do the same through the normal planning tools.

> **DECISION R2.5 — bulk actions use a dedicated review-and-select page. Settled.**
> `Review Keep Overdue...` and `Review Mark Missed...` do not apply immediately. They navigate to a dedicated review page showing every candidate from both `Still open from earlier weeks` and `This week`, preserving those section labels. All start selected; the rep can select all, clear all, or exclude individual visits before applying the chosen outcome. This replaces an ambiguous all-seven action with an explicit candidate set and scales when unresolved visits accumulate. After a successful apply, the rep returns to the digest; excluded or otherwise undecided visits remain there.

> **DECISION R2.6 — per-visit controls remain on the digest. Settled.**
> Every row keeps `Keep Overdue / Missed` so the rep can make quick mixed decisions without entering a bulk flow. The two review routes are accelerators for a shared outcome, not replacements for row-level control. No row is preselected (R2.3).

> **DECISION R2.7 — a row-level Missed reason appears inline. Settled.**
> Selecting `Missed` reveals an optional reason field directly beneath that visit. Switching the row back to `Keep Overdue` hides the field and its value is not saved. The bulk Mark Missed page keeps the separate shared-default-with-overrides pattern from R2.2.

---

## R-03 · Rep performance

**Job:** "how am I doing?" — and nothing else. This screen's restrictions are as important as its content.

```
+---------------------------------------------------------------------------------------+
| My performance                             October 2026            [ period v ]        |
+---------------------------------------------------------------------------------------+
|                                                                                       |
|   €26,400 of €40,000            66%            5 weeks left                           |
|   [==============================------------------]                                  |
|                                                                                       |
|   €1,240 of this is on orders not yet fully despatched.                               |
+---------------------------------------------------------------------------------------+
| BY RANGE                                                                              |
| Autumn 2026          €12,100 of €18,000    67%                                        |
| Core Health          €9,800  of €14,000    70%                                        |
| Baby & Infant        €4,500  of €8,000     56%                                        |
+---------------------------------------------------------------------------------------+
| BY LOCATION          [ All ] [ With targets ] [ No target ]      Sort: lowest % first |
| Byrne's, Aughrim         €480    of €2,400     20%                                    |
| Doyle's, Tinahely        €920    of €3,200     29%                                    |
| Quinn's, Rathdrum        €3,840  of €4,000     96%                                    |
| ... 8 more targeted                                                               v    |
+---------------------------------------------------------------------------------------+
| NO TARGET SET                                                                         |
| Murphy's, Rathdrum       €1,840 · 4 accepted orders                                   |
| Kelly's, Avoca           €760   · 2 accepted orders                                   |
+---------------------------------------------------------------------------------------+
```

> **DECISION R3.1 — the unfulfilled note sits directly under the headline number, not in a footnote.**
> It qualifies the number it sits beneath. A rep who reads 66% and acts on it without knowing €1,240 of it may not land has been misled by the layout, not by the data.

> **DECISION R3.2 — a progress bar here, unlike R-01's day load.**
> Different job. On the planner the rep needs the residual in minutes to decide if a call fits; here they need a sense of progress against a deadline, and "66% with 5 weeks left" is a proportion question. The bar includes a labelled **straight-line pace** marker while retaining exact percentage and weeks remaining in text.
> **The marker is advisory, not a judgement.** Some targets are naturally end-weighted and only meaningfully move near period end. The marker therefore never drives sorting, warning colour, or `on/off track` language. It is a neutral reference point, not a claim that sales should accrue evenly. Target setup has no pacing model: the rep and manager know the commercial context and judge progress for themselves.

> **DECISION R3.3 — targeted Locations sort by lowest percentage attainment first. Settled.**
> This supersedes Targets & Performance US-004's `highest contribution first` ordering for this target view. Percentage attainment compares differently sized targets fairly: 20% of €2,400 appears before 60% of €20,000 even though the second has a larger absolute amount remaining. The rep is here to identify where target attainment needs attention, not which Location already contributes most.

> **DECISION R3.5 — untargeted Locations appear below, with a top filter. Settled.**
> `All` is the default: targeted Locations appear first in percentage-attainment order, followed by a clearly separated `No target set` section showing actual contribution and Accepted Order count. The top filter offers `All`, `With targets`, and `No target`; choosing `No target` lets the rep isolate gaps without treating missing targets as 0% attainment.

> **DECISION R3.4 — no team comparison anywhere, including as an opt-in.**
> The brief forbids showing team figures. Worth restating on the screen inventory, because "how am I doing *relative to*" is the first thing anyone will ask for and it must stay out.

**Source-confirmed drafting calls:**
- **R3.1** is confirmed by Targets & Performance US-004: the unfulfilled amount is stated without reducing actuals.
- **R3.4** is confirmed by the Targets & Performance domain rule and US-004/US-006: reps see no team or Captured By comparison.

---

## R-04 · Order & call correction

> Started 26 Sep 2026. Reached from the tablet's "Open on website" on a sent item (T-08, Area 1 US-014).

**Job:** fix a mistake after it has left the tablet: edit an order the rep took while it is still Pending, or correct a synced Call.

**From the source:** after sync the tablet shows Calls and Orders read-only, with "Open on website" (Area 1 US-014). A correction changes a value already in a saved Call (a count, the Channel, a Low mark, note text) or removes a line; additions go on a Follow-up Call (Area 1 glossary, "Corrections constrained by the interface"). Orders are Pending until the order cut-off, then accepted automatically (BR-NEW-009).

> **DECISION R4.1 — a rep edits only Pending orders they took themselves. Settled 26 Sep 2026.**
> Each person changes only their own orders, as the customer does (C4.3). A customer-placed order, or another rep's, opens read-only here even while Pending; a change the customer phones in goes back to the customer, or through the company. **Rejected:** the rep also editing customer-placed orders at their Locations (two people editing one Pending order).

> *Confirmed 26 Sep 2026 (first-iteration defaults from the stories):*
> 1. **Pending order:** the same order lines as T-07 (§2.4 order line), with "You can change this until 4pm" and Save changes / Cancel order, the 15-minute warning once editing and the refused save after the cut-off, exactly as for the customer (C4.3–C4.4). Allowance and FOC limits are enforced as on the tablet (BR-NEW-002, BR-NEW-003).
> 2. **Accepted order:** read-only with its live status, like T-08 without the "as of sync" wording, since the website is live.
> 3. **Synced Call:** values editable and lines removable; where "Add" would be, **Record a follow-up call** explains that follow-up calls are recorded on the tablet. Each correction is saved with who changed it and when.
> 4. The tablet shows the corrected version at its next sync.

```
+------------------------------------------------------------------------------------------+
| < Sent items          Order - Carey's Pharmacy, Arklow          Pending                  |
|                       Captured 26 Sep 10:40 - you can change this until 4pm today        |
+------------------------------------------------------------------------------------------+
| Hand Cream 75ml                        [ - ]  48  [ + ]    EUR 3.42  Rep's price         |
| SPF30 Sun Lotion 200ml                 [ - ]  24  [ + ]    EUR 10.08 24+ price           |
| ...                                                                                      |
+------------------------------------------------------------------------------------------+
| 18 lines   EUR 2,924.00                            [ Cancel order ]   [ Save changes ]   |
+------------------------------------------------------------------------------------------+

  Synced call
  | Call 26 Sep - Quinn's Centra - Stock check                                             |
  |   SPF30 Sun Lotion 200ml   counted [ 12 ]  [x] Low                     [ Remove ]      |
  |   Anything forgotten? Record a follow-up call on the tablet.                           |
  |                                                           [ Save correction ]          |
```

---

## Open questions from this file

1. ~~**R-01** — absence days blocked or droppable-with-warning?~~ **Resolved — blocked by the Visit Planning source stories.**
2. ~~**R-01** — unscheduled panel horizon: everything, or due-within-N?~~ **Resolved — Overdue plus due within four weeks.**
3. ~~**R-01** — is one week the right window for a 3-week visit cycle?~~ **Resolved — Day, Week and Month views are user-selectable.**
6. ~~**R-01** — generic Due Reason indicator or structured type?~~ **Resolved — manager/admin-maintained structured Reason Type plus free-text explanation; manager/admin chooses icon and colour from a controlled set.**
4. ~~**R-02** — does "apply to all" respect the two sections separately, or all seven at once?~~ **Resolved — a bulk action opens a dedicated review page with all candidates grouped by section, Select all and individual inclusion/exclusion; completion returns to the digest.**
5. ~~**R-02** — what downstream business rule runs after a visit is marked Missed?~~ **Resolved — close and record only; any replacement one-off visit is created manually by a rep or manager.**
6. ~~**R-04 (not drawn)**~~ **Resolved 26 Sep 2026 — first-pass settled (R4.1 and confirmed defaults).** Original note: the corrections screen is defined in principle only; it needs the corrections-vs-follow-up rule settled before it can be sketched.
