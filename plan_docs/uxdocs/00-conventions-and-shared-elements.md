# 00 — Conventions & shared elements

**Companion to:** Field Sales Management System — Screen Inventory & UI Brief (19 Sep 2026)
**Status:** living wireframes. Individual DECISION blocks state whether they are settled, confirmed or still drafts.

---

## Document set

| File | Covers | Brief's priority |
|---|---|---|
| `00-conventions-and-shared-elements.md` | §2 global patterns, drawn | prerequisite to all |
| `01-tablet-day.md` | T-01, T-02, T-03, T-04, T-05, T-06, T-07, T-08 | 1, 2, 3 |
| `02-head-office.md` | H-01, H-02, H-03, H-10, H-16 | 4, 6 |
| `03-rep-planner.md` | R-01, R-02, R-03 | 5 |
| `04-user-stories-amendments.md` | settled story and business-rule amendments | companion |
| `05-manager.md` | M-01–M-17 | manager area |
| `06-customer.md` | C-01–C-09 | customer area |
| `07-access-and-sign-in.md` | I-01–I-05 shared staff and tablet access screens | added after brief |

The `I-` prefix identifies shared Identity and Access screens added after the original T/R/M/H/C inventory. The original screen tally in the 26 Sep handover is a snapshot from before this addition.

**Notation:** `[ Button ]` · `[x]` checkbox · `( )` radio · `>` navigates · `v` expands · `(!)` warning marker · `(i)` opens detail.
Tablet frames are drawn at ~64 characters; laptop frames at ~96. Proportions are indicative of *hierarchy*, not pixels.

---

## 1. The order line (§2.4)

The densest element in the system, on three surfaces. Drawn here at three densities to show it degrades gracefully — if only the fully-loaded version reads well, the component is wrong.

### 1a. Plain — Active product, tier price, nothing special

```
+--------------------------------------------------------------+
| Sudocrem Antiseptic Cream 125g            [ - ]   0   [ + ]   |
| Health > Skincare > Baby                                      |
| EUR 4.85                                              (i)     |
+--------------------------------------------------------------+
```

### 1b. Typical — promotion price, break prompt, quantity entered

```
+--------------------------------------------------------------+
| SPF30 Sun Lotion 200ml                    [ - ]   8   [ + ]   |
| Health > Skincare > Suncare                                   |
| EUR 9.99   Autumn promotion                           (i)     |
| 2 more for EUR 2.00 each - save EUR 4.00 on 10                |
+--------------------------------------------------------------+
```

### 1c. Fully loaded — plus availability warning

```
+--------------------------------------------------------------+
| SPF30 Sun Lotion 200ml                    [ - ]   8   [ + ]   |
| Health > Skincare > Suncare                                   |
| EUR 9.99   Autumn promotion                           (i)     |
| 2 more for EUR 2.00 each - save EUR 4.00 on 10                |
| (!) Being discontinued - replaced by SPF30 v2 200ml     >     |
+--------------------------------------------------------------+
```

### 1d. Measure-based product, mid-validation

```
+--------------------------------------------------------------+
| Loose Porridge Oats (per kg)             [ - ]  3.5  [ + ] kg |
| Grocery > Cereals > Hot cereals                               |
| EUR 2.10 / kg   Your price                            (i)     |
| (!) Enter at least 1.0 kg in steps of 0.5 kg                  |
+--------------------------------------------------------------+
```

### 1e. Unorderable — found by search, shown with a reason (§2.3 rule 2)

```
+--------------------------------------------------------------+
| SPF50 Kids Sun Spray 150ml                    NOT ORDERABLE   |
| Health > Skincare > Suncare                                   |
| (!) Back in stock around 25 October                           |
|                                  [ Find a replacement ]       |
+--------------------------------------------------------------+
```

The quantity control is *absent*, not disabled. A disabled stepper invites tapping; an absent one plus a stated reason plus an alternative action doesn't.

### Price provenance sheet — what `(i)` opens

```
+--------------------------------------------------------------+
|  Price for SPF30 Sun Lotion 200ml                       [ X ] |
+--------------------------------------------------------------+
|  APPLIED                                                      |
|  EUR  9.99   Autumn promotion (ends 31 Oct)                   |
|                                                               |
|  ALSO CONSIDERED                                              |
|  EUR 11.20   Your tier - Symbol Group A (-20%)                |
|  EUR 12.60   Quantity break from 24                           |
|  EUR 14.00   List price                                       |
+--------------------------------------------------------------+
```

With a rep discount applied, the price is a calculation rather than one competing candidate — the rep's discount stacks on promotions smaller than the allowance (01, T7.9) — so the sheet shows its working:

```
+--------------------------------------------------------------+
|  Price for Hand Cream 75ml                             [ X ] |
+--------------------------------------------------------------+
|  APPLIED                                                     |
|  EUR 3.42   Your price                                       |
|             Spring offer EUR 3.80, less 10% rep discount     |
|             "Matching competitor quote"                      |
|                                                              |
|  ALSO CONSIDERED                                             |
|  EUR 4.00   Your tier - Symbol Group A                       |
|  EUR 4.40   List price                                       |
+--------------------------------------------------------------+
```

> **DECISION 1.1 — the runner-up price is not on the row.**
> The brief lists "a runner-up price" as something the row may need. I've moved it into this sheet. A shop-floor row that already carries name, breadcrumb, price, source, break prompt and an availability warning cannot also carry a second price without the primary price losing its dominance. *Aesthetic and minimalist design* — every element on the row competes with the one the rep actually acts on. If the runner-up must be visible without a tap, the break prompt is the line I'd sacrifice first, because the break prompt recurs on every qualifying line while the runner-up only matters when someone queries the price.

> **DECISION 1.2 — `(i)` is an explicit target, not "tap the price".**
> The brief says tapping the price opens the candidates. A price that is also a button has no affordance and no discoverable boundary, and in a shop the rep's thumb lands near it constantly. A separate marker on the right of the price line is a real target with a real edge (*Fitts's law* — a target you can't see the edges of is a target you'll miss).

> **DECISION 1.3 — quantity stepper sits top-right, aligned with the product name.**
> Rationale: the name is what the rep reads, the stepper is what the rep touches, and putting them on the same baseline means one eye movement. Risk: for a left-handed rep holding a tablet one-handed, the right edge is the furthest reach on the row. Worth testing before it's locked.

**Open:** free-of-charge lines and price-override lines aren't drawn here — they're in `01-tablet-day.md` under T-07, because they only exist during order entry.

---

## 2. Availability labels (§2.3)

One label component, five states, never colour alone.

```
   (no label)                        Active
   (!) Being discontinued - replaced by SPF30 v2 200ml
   (!) Limited stock, no restock - about 120 left
       Not guaranteed until confirmed by head office
   (x) Back in stock around 25 October
   (x) Unavailable - no longer in an active range
   (x) Unavailable - retired
```

`(!)` = orderable with a caveat. `(x)` = not orderable. Shape carries the meaning; colour reinforces it. The distinction a rep must make in one glance under supermarket lighting is *can I sell this or not*, so that is the distinction the glyph encodes — not severity, not lifecycle stage.

> **DECISION 2.1 — run-out wording splits across two lines.**
> The brief's single string is 97 characters. On a tablet order row that wraps to three lines and swamps the product name. Split: the quantity fact on the label line, the provisional caveat beneath in secondary weight. On the customer site (C-03) both lines stay, because there is no rep standing there to explain it.

---

## 3. "As of sync" (§2.1) — tablet only

A single chip, used wherever server-derived data appears on the tablet.

```
   Synced today (normal)        Accepted, partly sent
                                About 120 left

   No sync yet today            Accepted, partly sent   as of Mon 21 Sep
                                About 120 left          as of Mon 21 Sep
```

Rules for the component:
1. It attaches to the **fact**, not to the screen. One stale figure gets one chip; it does not license a page-level "this page may be out of date" banner, which teaches people to distrust everything and therefore nothing.
2. It never appears on R, M, H or C screens.
3. It does not appear on data the rep captured on this device today — the rep's own unsent order is not stale, it is unsent, which is a different thing and has its own vocabulary (§4).
4. **It appears only when the last sync was before today** *(settled 24 Sep 2026, BR-NEW-007)*. Reps sync every morning, so "as of this morning" is the normal state and an always-on chip becomes wallpaper (*signal by exception*). When it does appear it names the **date** of the last sync — "as of Mon 21 Sep" — because a time alone would hide that it's yesterday's. **Supersedes** brief §2.1's always-on "as of 07:42 sync".

---

## 4. Unsent work (§2.2)

Three states, one counter, one vocabulary. The words are the design here.

| State | Means | Rep's next move |
|---|---|---|
| In Progress | still building it | keep working, or mark Ready |
| Ready to Send | finished, waiting for signal | nothing — it goes at next sync |
| Needs Attention | server rejected it | fix, re-mark, or delete |

Home counter, normal:

```
   3 to send                                               >
```

Home counter, after the soft-reminder hour:

```
   (!) 3 items still to send today - sync before you finish >
```

> **DECISION 4.1 — the soft reminder changes the row's prominence, not its position.**
> The brief says "more prominent in text (not a notification)". Moving the row would break the rep's spatial memory of Home late in the day, which is exactly when they're tired and rushing. Weight and wording change; the row stays put. *Consistency and standards.*

---

## 5. Impact preview (§2.5)

One component. Counts first, detail on demand, plain sentences.

```
+------------------------------------------------------------------+
|  Archive the Autumn 2026 range?                             [ X ] |
+------------------------------------------------------------------+
|                                                                   |
|  38 products become Unavailable                              v    |
|  20 stay on sale via other ranges                            v    |
|   3 open orders will still be processed                      v    |
|                                                                   |
+------------------------------------------------------------------+
|                                  [ Cancel ]   [ Archive range ]   |
+------------------------------------------------------------------+
```

Expanded (highest impact first, per H-19):

```
|  38 products become Unavailable                              ^    |
|    +-----------------------------------------------------------+  |
|    | SPF30 Sun Lotion 200ml    stocked in 87   [ Set replace ] |  |
|    | SPF30 Kids 150ml          stocked in 64   [ Set replace ] |  |
|    | Aftersun Gel 200ml        stocked in 61   [ Set replace ] |  |
|    | ... 35 more                                          v    |  |
|    +-----------------------------------------------------------+  |
```

> **DECISION 5.1 — the confirm button names the act, never "Confirm" or "OK".**
> *Error prevention.* "Archive range" read alone still tells you what the button does; "Confirm" read alone tells you nothing, and the muscle memory of clicking it is exactly what the preview exists to interrupt.

> **DECISION 5.2 — the preview is a dialog, not an interstitial page.**
> Rationale: it's a checkpoint on an action already begun, and the mental model is "am I sure", not "here is a new task". Cost: dialogs are cramped when 38 rows expand inside them. If the expanded lists routinely need scrolling within a scroll, this becomes a page. H-16 (category archive) is already a page for exactly that reason.

---

## 6. Filter -> review -> apply (§2.7)

Four regions, always in this order, on every bulk screen.

```
+------------------------------------------------------------------------+
|  1  FILTER                                                             |
|     Category [ Suncare      v]   Brand [ any        v]                 |
|     Profile  [ Pharmacy     v]   Region [ Leinster  v]                 |
|                                                    [ Find matches ]    |
+------------------------------------------------------------------------+
|  2  REVIEW - 41 matches                                                |
|                                                                        |
|     NEEDS A LOOK (3)                                                   |
|     [ ] Byrne's, Aughrim          no assigned rep                      |
|     [ ] Doyle's, Tinahely         no assigned rep                      |
|     [ ] Kavanagh's, Shillelagh    no assigned rep                      |
|                                                                        |
|     READY (38)                                   [ Untick all ]        |
|     [x] Quinn's Centra, Rathdrum          Colm                         |
|     [x] Hickey's Pharmacy, Rathdrum       Colm                         |
|     [x] ... 36 more                                     v              |
+------------------------------------------------------------------------+
|  3  SET ONCE                                                           |
|     Window  [ 1 Oct ] to [ 31 Oct ]    Duration [ 45 min v]            |
|     Reason  [ Autumn range launch                             ]        |
+------------------------------------------------------------------------+
|  4  [ Create 38 visits across 5 reps ]                                 |
+------------------------------------------------------------------------+
```

> **DECISION 6.1 — problem rows are unticked, not excluded.**
> The brief says problem rows are "separated out". Unticked-but-present means the operator sees the three shops with no rep and can choose to include them anyway; excluded-and-hidden means they silently never got a visit and nobody finds out for a cycle. *Visibility of system status.*

> **DECISION 6.2 — "set once" comes after review, not before.**
> You can only pick a sensible duration once you know you're looking at 38 pharmacies rather than 200 mixed locations.

---

## 7. Archive, never delete (§2.8)

```
   In use:        [ Archive ]                     <- no Delete control at all
   Not in use:    [ Archive ]   [ Delete ]
```

Archive dialog states the references:

```
   Archive "Suncare" attribute?
   Used by 24 products and 1 specialist assignment.
   It stays on those records and stops appearing in pickers.
```

List toggle, off by default:

```
   [ ] Show archived (3)
```

> **DECISION 7.1 — the sentence explains what archiving *does*, not just what it references.**
> "Used by 24 products" alone leaves the operator guessing whether those 24 products break. Saying it stays on existing records and disappears from pickers is the whole mental model in one line.

---

## 8. Gap lists (§2.9) and empty states (§2.10)

Gap list row — fixable in place, never a report:

```
+------------------------------------------------------------------------+
|  Quinn's Centra, Rathdrum                                              |
|  No location profile          [ Pharmacy v ]            [ Save ]       |
+------------------------------------------------------------------------+
|  Byrne's, Aughrim                                                      |
|  Main contact inactive        [ Choose replacement... v ]  [ Save ]    |
+------------------------------------------------------------------------+
```

Two empty-state shapes, and they are not interchangeable:

```
   GOOD NEWS                          NOTHING HERE YET
   +-----------------------------+    +-----------------------------+
   |                             |    |                             |
   |  All Locations have a       |    |  No leads yet.              |
   |  responsible rep.           |    |                             |
   |                             |    |  [ Add a lead ]             |
   +-----------------------------+    +-----------------------------+
```

The good-news state offers **no action**, because there is nothing to do and a button would imply otherwise. The genuinely-empty state offers exactly one.

---

## 9. Open questions raised by these conventions

1. **Runner-up price** — row or sheet? (Decision 1.1 chose sheet.)
2. **Price provenance affordance** — dedicated `(i)` target vs. tappable price? (1.2 chose `(i)`.)
3. **Stepper placement** for one-handed and left-handed use. (1.3 unresolved, needs a device in hand.)
4. **Run-out wording** — one line or two, and does the caveat survive onto the tablet row? (2.1 chose two.)
5. **Impact preview as dialog vs. page** — where's the row-count threshold? (5.2 unresolved.)
6. Carried forward from the brief §11: tablet snapshot size, soft-reminder hour, prospect pricing.
