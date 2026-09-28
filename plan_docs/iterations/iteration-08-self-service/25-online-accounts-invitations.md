# E25 — Online accounts and invitations

**Iteration** — 8, Customer self-service
**Outcome** — A rep sets up online ordering for a shop contact at the counter, a manager approves it on a page they're emailed about, and the contact sets a password from an invitation and signs in; expired invitations and declines loop back to the right person the same day, without waiting for a sync.
**Stories**

| # | Story | Priority | Delivered here |
|---|---|---|---|
| 1 | Self-service US-001 — Create a Customer User account | Must | S1–S10 |
| 2 | Self-service US-002 — Accept an invitation and sign in | Must (Draft) | S1–S6 |
| 3 | Head Office US-006b — Approve a Customer User account | Must | S1–S3, on M-17 as amended |

Also delivered here: Coverage Management US-002's "Set up online ordering" action on M-07's footer row (deferred from E3).

**Exit criterion** — A rep opens a contact from the tablet's Location screen and sets up online ordering, seeing what the contact will be able to order for; the request reaches the manager's M-17 page within minutes of signal, with an email linking to it. The manager approves (the invitation is sent) or declines with a reason the rep sees on Home the same day. A manager can set up an account directly from M-07 with no approval. The contact sets a password from the invitation and signs in, and resets a forgotten one. An expired invitation offers a request for a new one, which the Location's rep approves from Home (or the manager, for an unassigned Location). An Inactive contact's login is suspended.

**Capability-class stamp** — Frontier + extended reasoning for customer identity and the background channel (T-25.1.1, T-25.2.1); Frontier workhorse for other tasks and scenario drafting. Concrete models: see [../00-index.md](../00-index.md).

**Ownership source** — classified in this plan from the stories alone; every tier is `(inferred)`.

**Spec sources** — [self-service.md](../../stories/self-service.md) (US-001, US-002, glossary, Requires Clarification 1, 5, 6), [head-office-order-processing.md](../../stories/head-office-order-processing.md) (US-006b and amendment), [06-customer.md](../../uxdocs/06-customer.md) (C-01 C1.1–C1.7; C-08 C8.1–C8.4), [05-manager.md](../../uxdocs/05-manager.md) (M-07 footer; M-17 M17.1), [01-tablet-day.md](../../uxdocs/01-tablet-day.md) (T-01 T1.3; T-02 T2.5, T2.6; T-05 T5.2).

---

### T-25.1.1-S — Test scenarios for customer identity and first sign-in

**Owner** — Human-Led
**Gates** — T-25.1.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-33: invitation delivery channel (email assumed), lifetime, single use, password policy.
- MI-03: is a Customer User a separate identity population from staff, with its own sign-in and session rules?
- Following the invitation and setting a password makes the account Active; one shop goes to C-09, several to C-02 (US-002 S1; C-01).
- Forgotten password: a reset goes to the email on the Contact record (S3). What if the Contact's email changed after the invitation?
- One login per Contact; a second attempt reads "Mary Walsh already has a login (Active)" (US-001 S5).
- A Contact marked Inactive suspends the login at once, including open sessions (US-001 S6). Reactivated: back to Active, or re-invited?
- A Prospect has no Customer Users (assumption).
- MI-35: app and website identical and online-only; any offline expectation?

---

### T-25.1.1 — Give invited contacts a login: set a password, sign in, reset it

**Parent story**

> As a Customer User, I want to set my password from the invitation so that I can start ordering without filling in a registration form.
>
> Acceptance criteria:
> - Following the invitation and setting a password makes my account Active and I land on my Locations (S1)
> - Forgotten password sends a reset to the email on my Contact record (S3)
> - A second account for the same Contact is refused with "Mary Walsh already has a login (Active)" (US-001 S5)
> - An Inactive Contact's login is suspended and can't sign in (US-001 S6)

**Slice** — A Customer User record tied to one Contact moves through Pending approval, Invited, Active and Suspended; an invitation lets the contact set a password and sign in to the customer site, a forgotten password is reset by email, and making the Contact Inactive suspends the login.
**Spec source** — Self-service US-002 S1, S3; US-001 S5, S6; glossary (Customer User, Invitation); design decision "No self-registration; created and approved"; Requires Clarification 1, 5; uxdocs 06 C-01
**Depends on** — T-2.4.1, T-2.5.1, T-1.1.1
**Pattern to follow** — T-4.1.1 (sign-in for reps), T-1.1.1 (staff website foundation)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: L
  (inferred) — a new external identity surface (Blast Radius High, security); invitation mechanics and identity model are open (MI-33, MI-03).

**Provisional commit message**

```
feat(self-service): give invited contacts a login

- Online ordering access is a commercial decision, so there is no signup
  form; an invitation is the only way in
- The login follows the contact: an inactive contact can't sign in
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
Security-sensitive identity for outsiders.

**Work package**

Increments:
1. Resolve MI-33 and MI-03 for customers (channel, lifetime, password policy, separate or shared identity store) and record them.
2. Customer User record: one per Contact, states Pending approval, Invited, Active, Suspended; the second-account guard.
3. Invitation token: issue, expire, single use; set password; Active; land on C-09 or C-02.
4. Sign-in, sign-out, forgotten password to the Contact's email.
5. Suspension when the Contact goes Inactive (T-2.5.1), ending sessions; behaviour on reactivation.
6. The customer site shell (MI-35: same features on app and website, online only).

Decision points:
- The MI-33 and MI-03 answers.
- Reactivation behaviour.
- Rate limiting and lockout on sign-in and reset.

Delegable slivers:
- **Invitation email** — Once channel and lifetime are agreed, compose and send the invitation and reset emails from templates with the link and expiry stated. No token logic.
- **Second-account guard** — Refuse creating a Customer User for a Contact that already has one, with "Mary Walsh already has a login (Active)" showing the existing state. No other logic.

---

### T-25.2.1-S — Test scenarios for the customer-request background channel

**Owner** — Human-Led
**Gates** — T-25.2.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- MI-34: what carries requests to and from the tablet without a manual sync (push, polling while in range), and what does "has signal" mean on a flaky connection?
- Only customer requests travel this way (set-up requests out, declines and re-invitation requests in, approvals out); every other item still waits for a manual sync (C1.5, T1.3). How is that boundary enforced?
- Exactly-once delivery each way; a request sent in the background and also present at the next manual sync.
- "Will send when in range" with no signal; "Sent 14:20" after sending (C1.7).
- Home's exception strip counts don't change from background items (C1.4).
- A rep signed out, or the tablet off for days: queued items expire?

---

### T-25.2.1 — Carry customer requests to and from the tablet whenever it has signal

**Parent story**

> As a Field Salesperson or Sales Manager, I want to set up online ordering for a contact so that they can order between my visits.
>
> Acceptance criteria:
> - The set-up request reaches my manager's approval queue as soon as the tablet has signal, without a manual sync (S7)
> - A decline shows on Home's Customer requests section when the tablet has signal, without syncing (S9)
> - Re-invitation requests appear on Home when the tablet has signal, and "Send new invitation" goes out the same way (US-002 S6; C1.5, C1.7)

**Slice** — A narrow background channel moves customer requests and their outcomes between server and tablet whenever the tablet has signal, exactly once, while all other work keeps waiting for the rep's manual sync.
**Spec source** — Self-service US-001 S7, S9; US-002 S6; uxdocs 06 C1.5, C1.7, C8.1, C8.3; uxdocs 01 T-01 (T1.3 manual sync)
**Depends on** — T-4.1.1, T-4.1.2, T-4.5.1
**Pattern to follow** — T-4.1.2 (exactly-once upload)
**Ownership** — Impl: Human Tight-Loop | Test: Human-Led | Complexity: C | Confidence: L
  (inferred) — the first exception to manual sync, touching the tablet's sync contract (Blast Radius High); the mechanism is undecided (MI-34).

**Provisional commit message**

```
feat(tablet): carry customer requests in the background when in range

- Someone outside is waiting on these, so they can't wait for the rep's
  end-of-day sync; everything else still syncs only when the rep chooses
```

**Capability class** — Frontier + extended reasoning · effort: Anthropic high budget / OpenAI xhigh / Google high
A change to the sync contract.

**Work package**

Increments:
1. Decide the mechanism (MI-34) and the allow-list of item types it may carry.
2. Outbound queue on the tablet for set-up requests and re-invitation approvals: send when in range, exactly once, with "Will send when in range" and "Sent 14:20" states.
3. Inbound delivery of declines and re-invitation requests into Home's Customer requests section only.
4. Reconciliation with manual sync so nothing is applied twice.
5. Tests for loss of signal mid-send and long offline periods.

Decision points:
- Mechanism and allow-list.
- Expiry of queued items.
- Battery and data budget on the tablet.

Delegable slivers:
- **Outbound queue states** — Once the mechanism exists, show "Will send when in range" and "Sent 14:20" on queued customer-request items, following C1.7. No transport.
- **Allow-list guard** — Reject any item type other than the agreed customer-request types on the background channel, with a test per type. No transport.

---

### T-25.2.2-S — Test scenarios for setting up online ordering on the tablet

**Owner** — Scenario Review
**Gates** — T-25.2.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for a rep setting up online ordering from a
contact on the tablet (task T-25.2.2). Read
plan_docs/stories/self-service.md US-001 S1, S4, S5, S7 and glossary
(Ordering Scope), and plan_docs/uxdocs/06-customer.md C-08 (C8.1). Output one
line per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Start with characterisation scenarios pinning T-4.2.2's Location
screen and contact display. Cover the story scenarios, then derivable edges:
a contact with no email, a contact at a master and at a branch, a contact
whose account is Pending approval or Suspended, a prospect's contact. Mark
undecided cases as "Needs a decision". Write no test code; change no files.
```

---

### T-25.2.2 — Set up online ordering for a contact from the tablet

**Parent story**

> As a Field Salesperson or Sales Manager, I want to set up online ordering for a contact so that they can order between my visits.
>
> Acceptance criteria:
> - A rep creating an account for Mary Walsh at Hickey's Rathdrum makes it Pending approval in the manager's queue (S1)
> - The screen states what she can order for: "Hickey's Rathdrum, Hickey's Arklow" or "Hickey's Head Office and its 12 branches" (S4)
> - A second account is refused with "Mary Walsh already has a login (Active)" (S5)
> - From Mary Walsh on the Location screen, "Set up online ordering" confirms the email with Mary and shows her scope; the request reaches the manager as soon as the tablet has signal (S7)

**Slice** — On the tablet, the rep opens a contact from the Location screen, chooses Set up online ordering, confirms the email with the contact and sees what they'll be able to order for, and the request leaves for the manager's approval as soon as there is signal; a contact with a login shows its status instead.
**Spec source** — Self-service US-001 S1, S4, S5, S7; glossary (Ordering Scope); uxdocs 06 C-08 (C8.1 and confirmed contact-details frame); uxdocs 01 T-05 (T5.2)
**Depends on** — T-25.1.1, T-25.2.1, T-4.2.2, T-2.5.2, T-12.2.1
**Pattern to follow** — T-4.2.2 (Location screen)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — modifies the Location screen's contact display (characterisation first); C8.1 is settled.

**Provisional commit message**

```
feat(tablet): set up online ordering for a contact at the shop

- The request usually comes up at the counter, so the rep can confirm the
  email with the contact there and then
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A tablet flow behind a characterisation pass.

**Agent prompt**

```
Role: You are adding "Set up online ordering" to a contact's details on the
tablet in the Field Sales Management System.

Context:
- Slice: on the tablet, the rep opens a contact from the Location screen,
  chooses Set up online ordering, confirms the email with the contact and
  sees what they'll be able to order for, and the request leaves for the
  manager's approval as soon as there is signal; a contact with a login shows
  its status instead.
- Specs: plan_docs/stories/self-service.md US-001 S1, S4, S5, S7 and glossary
  (Ordering Scope); plan_docs/uxdocs/06-customer.md C-08 (C8.1);
  plan_docs/uxdocs/01-tablet-day.md T-05 (T5.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Customer User records
  (T-25.1.1), background channel (T-25.2.1), Location screen (T-4.2.2),
  contacts on the tablet (T-2.5.2), master relationship (T-12.2.1).
- Pattern to follow: T-4.2.2.

Acceptance criteria:
1. Tapping a contact on the Location screen opens their details, where "Set
   up online ordering" sits; a contact with a login shows "Online ordering:
   Active" (or its state) instead.
2. The set-up step shows the email to confirm with the contact and the scope
   line: "Mary Walsh will be able to order for: Hickey's Rathdrum, Hickey's
   Arklow", or "Hickey's Head Office and its 12 branches" for a master's
   contact.
3. Saving creates a request that is Pending approval and leaves through the
   background channel as soon as there is signal.
4. A contact with an existing login can't get a second: "Mary Walsh already
   has a login (Active)".

Constraints:
- Use the project's existing conventions and test framework.
- Scope is computed on the server as well; the tablet's line is for
  information.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
the Location screen's contact display; stop and show them passing. Resume
only on "Continue T-25.2.2".

Steps: 1. characterisation tests; 2. contact details; 3. set-up step and
scope line; 4. send; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-25.2.2-S; do not design
your own.

Definition of done: on the tablet, the rep opens a contact from the Location
screen, chooses Set up online ordering, confirms the email with the contact
and sees what they'll be able to order for, and the request leaves for the
manager's approval as soon as there is signal; a contact with a login shows
its status instead.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: approval (T-25.3.1), contact self-edit (MI-35).
```

**Checkpoint**

Produces before pausing — characterisation tests of the contact display, passing.
Human reviews — Do the tests pin today's Location screen exactly?
Resume trigger — `Continue T-25.2.2`

---

### T-25.3.1-S — Test scenarios for approving accounts on M-17

**Owner** — Scenario Review
**Gates** — T-25.3.1
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for approving and declining customer accounts
on M-17 (task T-25.3.1). Read plan_docs/stories/self-service.md US-001 S1,
S3, S8, plan_docs/stories/head-office-order-processing.md US-006b S1–S3 and
amendment, and plan_docs/uxdocs/05-manager.md M-17 (M17.1). Output one line
per scenario as Should_Outcome_When_Condition, then "→" and a one-line
intent. Cover the story scenarios, then derivable edges: two managers acting
on one request, a contact made Inactive while pending, a request from a rep
who has left, the empty state. Mark undecided cases as "Needs a decision".
Write no test code; change no files.
```

---

### T-25.3.1 — Approve or decline account requests on M-17, prompted by email

**Parent story**

> As a Sales Manager, I want to approve or decline online ordering access a rep has set up so that access is given deliberately.
>
> Acceptance criteria:
> - A rep's account for Mary Walsh appears showing her Contact, her Locations, and what she could order for; on approval an invitation is sent (S1; Self-service US-001 S1)
> - Declining with a reason leaves no account and the rep sees the reason (S2; US-001 S3)
> - A Contact at a Master Location reads "Hickey's Head Office and its 12 branches" (S3)
> - The manager is emailed with a link to "Online ordering approvals"; approving there sends the invitation; declining requires a reason Colm sees (US-001 S8)

**Slice** — When a rep sets up an account, the manager is emailed a link to M-17, where new account requests list the contact, Locations and scope; Approve sends the invitation at once and Decline takes a required reason addressed to the rep, and decided items leave the list.
**Spec source** — Head Office US-006b S1–S3 and amendment; Self-service US-001 S1, S3, S8; uxdocs 05 M-17 (M17.1 and confirmed layout)
**Depends on** — T-25.1.1, T-25.2.2, T-14.1.1
**Pattern to follow** — T-14.1.1 (manager website pages)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: M | Confidence: M
  (inferred) — grants access to outsiders, but the flow is settled.

**Provisional commit message**

```
feat(manager): approve customer accounts on their own page

- The email informs and links; the decision happens on the page, where a
  decline can carry the reason the rep will pass on
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A settled approvals page with a review pause.

**Agent prompt**

```
Role: You are building Online ordering approvals (M-17) on the Field Sales
Management System's manager website.

Context:
- Slice: when a rep sets up an account, the manager is emailed a link to
  M-17, where new account requests list the contact, Locations and scope;
  Approve sends the invitation at once and Decline takes a required reason
  addressed to the rep, and decided items leave the list.
- Specs: plan_docs/stories/head-office-order-processing.md US-006b S1–S3 and
  amendment; plan_docs/stories/self-service.md US-001 S1, S3, S8;
  plan_docs/uxdocs/05-manager.md M-17 (M17.1).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Customer Users and
  invitations (T-25.1.1), set-up requests (T-25.2.2), manager website
  (T-14.1.1).
- Pattern to follow: T-14.1.1.

Acceptance criteria:
1. A new request emails the rep's manager with a link to M-17.
2. M-17 lists new accounts (and, from T-25.4.1, re-invitations as a second
   group), each with the Contact, Locations and the scope line, e.g.
   "Hickey's Head Office and its 12 branches".
3. Approve sends the invitation at once and the item leaves the list.
4. Decline opens an inline required reason addressed to the rep by name;
   no account exists afterwards; the outcome is queued for the rep.
5. Empty: "Nothing waiting for approval."
6. Account requests never appear on the head office Worklist.

Constraints:
- Use the project's existing conventions and test framework.
- One decision per request, whoever acts first.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after approve and decline work end to end (email, page,
invitation or queued decline), stop and show them. Resume only on
"Continue T-25.3.1".

Steps: 1. manager email; 2. M-17 list; 3. Approve; 4. Decline with reason;
5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-25.3.1-S; do not design
your own.

Definition of done: when a rep sets up an account, the manager is emailed a
link to M-17, where new account requests list the contact, Locations and
scope; Approve sends the invitation at once and Decline takes a required
reason addressed to the rep, and decided items leave the list.

Self-verification (report each line pass or fail with evidence):
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: the decline on the rep's Home (T-25.3.2), re-invitations
(T-25.4.1).
```

**Checkpoint**

Produces before pausing — approve and decline working end to end.
Human reviews — Is access granted only by an explicit Approve?
Resume trigger — `Continue T-25.3.1`

---

### T-25.3.2-S — Test scenarios for a decline on the rep's Home

**Owner** — Scenario Review
**Gates** — T-25.3.2
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for showing a declined account on the rep's
Home (task T-25.3.2). Read plan_docs/stories/self-service.md US-001 S9 and
plan_docs/uxdocs/06-customer.md C1.4, C8.3. Output one line per scenario as
Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning T-4.5.1's Home sections and exception
strip. Cover S9, then derivable edges: several notices, a notice arriving
while Home is open, the strip counts unchanged. Mark undecided cases as
"Needs a decision". Write no test code; change no files.
```

---

### T-25.3.2 — Tell the rep on Home when an account is declined

**Parent story**

> As a Field Salesperson or Sales Manager, I want to set up online ordering for a contact so that they can order between my visits.
>
> Acceptance criteria:
> - Declined with "Contact has left the business": when Colm's tablet has signal, Home's Customer requests section shows "Mary Walsh: online ordering declined: Contact has left the business" without syncing; OK removes it (S9)

**Slice** — A declined account arrives in a Customer requests section on Home, first among the collapsed sections and shown only when something is there, reading as a plain sentence with one OK that removes it.
**Spec source** — Self-service US-001 S9; uxdocs 06 C1.4, C8.3
**Depends on** — T-25.2.1, T-25.3.1, T-4.5.1
**Pattern to follow** — T-4.5.1 (Home sections)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies Home (characterisation first); wording settled.

**Provisional commit message**

```
feat(tablet): tell the rep on home when an account is declined

- The rep is the one who tells the contact, so the decline and its
  reason reach them the same day
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A Home section behind a characterisation pass.

**Agent prompt**

```
Role: You are adding the Customer requests section to the tablet's Home in
the Field Sales Management System.

Context:
- Slice: a declined account arrives in a Customer requests section on Home,
  first among the collapsed sections and shown only when something is there,
  reading as a plain sentence with one OK that removes it.
- Specs: plan_docs/stories/self-service.md US-001 S9;
  plan_docs/uxdocs/06-customer.md C1.4, C8.3.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — background channel
  (T-25.2.1), declines (T-25.3.1), Home (T-4.5.1).
- Pattern to follow: T-4.5.1's sections.

Acceptance criteria:
1. "CUSTOMER REQUESTS (1)" appears as the first collapsed section below Today
   only when an item is waiting.
2. A decline reads "Mary Walsh: online ordering declined: Contact has left
   the business" with one action, OK, which removes it.
3. It arrives without a manual sync; the exception strip's counts don't
   change.

Constraints:
- Use the project's existing conventions and test framework.
- The section is shared with re-invitation requests (T-25.4.1).
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
Home's sections and strip; stop and show them passing. Resume only on
"Continue T-25.3.2".

Steps: 1. characterisation tests; 2. section; 3. decline notice and OK;
4. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-25.3.2-S; do not design
your own.

Definition of done: a declined account arrives in a Customer requests section
on Home, first among the collapsed sections and shown only when something is
there, reading as a plain sentence with one OK that removes it.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–3 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: re-invitation requests (T-25.4.1).
```

**Checkpoint**

Produces before pausing — characterisation tests of Home's sections and strip, passing.
Human reviews — Do the tests pin today's Home exactly?
Resume trigger — `Continue T-25.3.2`

---

### T-25.3.3-S — Test scenarios for a manager setting up an account from M-07

**Owner** — Scenario Review
**Gates** — T-25.3.3
**Produces** — a scenario list: names plus one-line intent, no code

**Agent prompt**

```
Draft the test scenario list for a manager setting up online ordering from
the Location page (task T-25.3.3). Read plan_docs/stories/self-service.md
US-001 S2, S10, plan_docs/uxdocs/06-customer.md C8.4, and
plan_docs/uxdocs/05-manager.md M-07 (footer row). Output one line per scenario
as Should_Outcome_When_Condition, then "→" and a one-line intent. Start with
characterisation scenarios pinning M-07's footer row. Cover the story
scenarios, then derivable edges: a contact with a Pending request from a rep,
a contact already Active. Mark undecided cases as "Needs a decision". Write no
test code; change no files.
```

---

### T-25.3.3 — Let a manager set up an account from the Location page

**Parent story**

> As a Field Salesperson or Sales Manager, I want to set up online ordering for a contact so that they can order between my visits.
>
> Acceptance criteria:
> - A manager-created account needs no approval and the invitation is sent immediately (S2)
> - On Hickey's Rathdrum's Location page, "Set up online ordering…" and Mary Walsh shows "Mary Walsh will be able to order for: Hickey's Rathdrum"; saving sends the invitation with no approval (S10)

**Slice** — On M-07, a manager chooses Set up online ordering… from the footer row, picks a contact, confirms the email and sees the scope line, and saving sends the invitation straight away.
**Spec source** — Self-service US-001 S2, S10; uxdocs 06 C8.4; uxdocs 05 M-07 footer row; Coverage US-002 ("Set up online ordering" deferred from E3)
**Depends on** — T-25.1.1, T-3.2.1, T-14.4.1
**Pattern to follow** — T-14.4.1 (an action on M-07's footer row)
**Ownership** — Impl: Agent-Assisted | Test: Scenario Review | Complexity: S | Confidence: M
  (inferred) — modifies M-07 (characterisation first); settled.

**Provisional commit message**

```
feat(manager): set up online ordering from the location page

- A manager's own set-up needs no approval, so it starts from the shop
  like the manager's other location actions
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
A footer action behind a characterisation pass.

**Agent prompt**

```
Role: You are adding "Set up online ordering…" to the Location page (M-07) of
the Field Sales Management System's manager website.

Context:
- Slice: on M-07, a manager chooses Set up online ordering… from the footer
  row, picks a contact, confirms the email and sees the scope line, and
  saving sends the invitation straight away.
- Specs: plan_docs/stories/self-service.md US-001 S2, S10;
  plan_docs/uxdocs/06-customer.md C8.4; plan_docs/uxdocs/05-manager.md M-07.
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — Customer Users and
  invitations (T-25.1.1), M-07 (T-3.2.1), footer actions (T-14.4.1).
- Pattern to follow: T-14.4.1.

Acceptance criteria:
1. M-07's footer row has "Set up online ordering…" beside Add one-off visit.
2. It asks for a contact at this Location and shows "Mary Walsh will be able
   to order for: Hickey's Rathdrum" with the email to confirm.
3. Saving sends the invitation immediately with no approval step.
4. A contact with an existing login or pending request shows that state
   instead.

Constraints:
- Use the project's existing conventions and test framework.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: before changing anything, write characterisation tests pinning
M-07's footer row; stop and show them passing. Resume only on
"Continue T-25.3.3".

Steps: 1. characterisation tests; 2. footer action; 3. contact and scope;
4. send; 5. tests from agreed scenarios.

Test expectations: implement the scenarios agreed in T-25.3.3-S; do not design
your own.

Definition of done: on M-07, a manager chooses Set up online ordering… from
the footer row, picks a contact, confirms the email and sees the scope line,
and saving sends the invitation straight away.

Self-verification (report each line pass or fail with evidence):
- [ ] Characterisation tests passed before and after
- [ ] Criteria 1–4 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: rep set-up (T-25.2.2).
```

**Checkpoint**

Produces before pausing — characterisation tests of M-07's footer, passing.
Human reviews — Do the tests pin today's footer exactly?
Resume trigger — `Continue T-25.3.3`

---

### T-25.4.1-S — Test scenarios for expired invitations

**Owner** — Human-Led
**Gates** — T-25.4.1
**Produces** — a scenario list: names plus one-line intent, no code

Questions to answer while writing it:
- The expired page offers "Request a new invitation"; after requesting, it says a new one will follow once approved; nothing is sent until then (S2; C1.1).
- Following the old link again shows the request is awaiting approval and creates no duplicate (S4).
- The rep responsible for the Location approves, with no manager step (S5; C1.2). Which Location, for a contact at several with different reps? A master's contact?
- No assigned rep: the manager who would see it as Unassigned gets it on M-17 (S5; M15.2).
- The rep is emailed (C1.6) and sees it on Home under Customer requests without syncing, with name, Location, date and "Send new invitation" (S6; C1.3–C1.5).
- "Send new invitation" goes out in the background; the row greys "Sent 14:20" until the rep leaves Home; "Will send when in range" offline (C1.7).
- The contact made Inactive while the request waits.

---

### T-25.4.1 — Let a contact request a new invitation, approved by their rep

**Parent story**

> As a Customer User, I want to set my password from the invitation so that I can start ordering without filling in a registration form.
>
> Acceptance criteria:
> - An expired invitation shows that it has expired and "Request a new invitation"; the request goes to my rep or manager, and no new invitation is sent until they approve (S2)
> - Following the expired link again shows my request is awaiting approval, with no duplicate (S4)
> - The rep responsible for Hickey's Rathdrum approves, and a new invitation is sent with no manager step; with no assigned rep, the manager sees it as Unassigned (S5)
> - The rep is emailed; the request appears on Home under "Customer requests" without syncing, with my name, Location, the date and "Send new invitation", which goes out without syncing (S6)

**Slice** — An expired invitation lets the contact ask for a new one; the Location's rep is emailed and sees the request on Home within minutes of signal, sends the new invitation from there, and a Location with no rep sends the request to the manager's M-17 instead.
**Spec source** — Self-service US-002 S2, S4, S5, S6; uxdocs 06 C1.1–C1.7; uxdocs 05 M-17 (re-invitations group), M-15 (M15.2)
**Depends on** — T-25.1.1, T-25.2.1, T-25.3.1, T-25.3.2, T-3.1.1, T-3.3.1
**Pattern to follow** — T-25.3.1 (approval), T-25.3.2 (Home section)
**Ownership** — Impl: Agent-Assisted | Test: Human-Led | Complexity: M | Confidence: M
  (inferred) — routing by coverage with several edge cases; flows are settled.

**Provisional commit message**

```
feat(self-service): let a contact request a new invitation

- An expired link shouldn't be a dead end, nor enough to reissue access;
  the rep who knows the shop approves it, the same day
```

**Capability class** — Frontier workhorse · effort: Anthropic standard budget / OpenAI high / Google high
Routing across three surfaces against a human oracle.

**Agent prompt**

```
Role: You are adding re-invitation requests to the Field Sales Management
System's customer site, tablet Home and manager approvals page.

Context:
- Slice: an expired invitation lets the contact ask for a new one; the
  Location's rep is emailed and sees the request on Home within minutes of
  signal, sends the new invitation from there, and a Location with no rep
  sends the request to the manager's M-17 instead.
- Specs: plan_docs/stories/self-service.md US-002 S2, S4, S5, S6;
  plan_docs/uxdocs/06-customer.md C1.1–C1.7; plan_docs/uxdocs/05-manager.md
  M-17, M-15 (M15.2).
- Stack: {{STACK}}. Code locations: {{PLACEHOLDER}} — invitations
  (T-25.1.1), background channel (T-25.2.1), M-17 (T-25.3.1), Home Customer
  requests (T-25.3.2), effective owner (T-3.1.1), unassigned (T-3.3.1).
- Pattern to follow: T-25.3.1 and T-25.3.2.

Acceptance criteria:
1. The expired page shows that it has expired and "Request a new
   invitation"; after requesting, it confirms a new one will follow once
   approved.
2. Following the old link again shows "awaiting approval"; no duplicate.
3. The request goes to the Location's responsible rep: an email, and a row on
   Home's Customer requests with name, Location, date and "Send new
   invitation", arriving without a manual sync.
4. "Send new invitation" sends it in the background; the row greys with "Sent
   14:20" until the rep leaves Home; offline it shows "Will send when in
   range".
5. With no assigned rep, the request goes to the manager on M-17's
   re-invitations group, with an email.
6. Routing edges behave as agreed in T-25.4.1-S.

Constraints:
- Use the project's existing conventions and test framework.
- No new invitation is sent without an approval.
- No tests of framework internals or trivial members.
- Irreversible or schema-altering operations are out of scope.

Gates: stay read-only until I reply "Approved" to your plan; do not modify
existing files until I reply "Apply".
Checkpoint: after routing passes every agreed scenario, stop and show the
flow on all three surfaces. Resume only on "Continue T-25.4.1".

Steps: 1. expired page; 2. request and routing; 3. Home row and send;
4. M-17 group; 5. emails; 6. tests.

Test expectations: implement exactly the scenarios agreed in T-25.4.1-S. You
are forbidden from designing your own test cases.

Definition of done: an expired invitation lets the contact ask for a new one;
the Location's rep is emailed and sees the request on Home within minutes of
signal, sends the new invitation from there, and a Location with no rep sends
the request to the manager's M-17 instead.

Self-verification (report each line pass or fail with evidence):
- [ ] Every agreed scenario passes
- [ ] Criteria 1–6 demonstrated
- [ ] Provisional commit message reconciled with the actual diff

Out of scope: invitation lifetime policy (MI-33).
```

**Checkpoint**

Produces before pausing — routing passing every agreed scenario, shown on the customer site, Home and M-17.
Human reviews — Does every request reach exactly one approver, and is nothing sent without approval?
Resume trigger — `Continue T-25.4.1`

---

## Epic self-verification

| Check | Result | Evidence |
|---|---|---|
| Every story in this epic appears and none is dropped | Pass | SS-001 (T-25.1.1, T-25.2.2, T-25.3.1, T-25.3.2, T-25.3.3; S7/S9 channel T-25.2.1), SS-002 (T-25.1.1, T-25.4.1), HO-006b (T-25.3.1); CV-002 footer (T-25.3.3) |
| Every task satisfies the three slice criteria | Pass | 7 of 7 |
| Every task carries a tier with a rationale citing dimensions | Pass | 7 of 7 |
| Unscoreable dimensions scored High, marked (inferred), reported | Pass | MI-03, MI-33 (T-25.1.1), MI-34 (T-25.2.1), MI-35 |
| Tasks modifying existing behaviour order characterisation first | Pass | T-25.2.2, T-25.3.2, T-25.3.3 |
| No Agent-Autonomous body where Blast, Taste or Test Safety Net is High | Pass | None in this epic |
| No agent prompt designs tests where Oracle Ambiguity or Edge-Case Discovery is High | Pass | T-25.1.1, T-25.2.1, T-25.4.1 are Human-Led |
| Every Agent-Assisted body has a checkpoint with an exact resume trigger | Pass | T-25.2.2, T-25.3.1, T-25.3.2, T-25.3.3, T-25.4.1 |
| Every Human Tight-Loop body has increments, decision points and slivers | Pass | T-25.1.1, T-25.2.1 |
| Every scenario task precedes the task it gates | Pass | 7 `-S` sections |
| Every agent prompt is self-contained | Pass | Each names specs, criteria, constraints, gates, out of scope |
| No concrete model name in any task section | Pass | Classes only |
| Every placeholder and inferred marker is reported | Pass | `{{STACK}}`, `{{PLACEHOLDER}}`, MI-03, 33, 34, 35 |
