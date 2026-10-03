# WI-020 — Main-contact replacement checkpoint

The user approved the plan/scenarios and supplied Continue T-2.5.1 together
on 2026-10-03, authorizing continuation after recording this checkpoint.

## Characterisation evidence

Before domain changes, all 26 WI-018 ContactEndToEndTests pass. Two newly
authored MainContactCharacterisationTests also pass, covering independent
Main choices across shops, confirmation retaining the outgoing link, and
blocking implicit retirement when a person is Main at another shop.
TRX files: .artifacts/wi020/wi020-{characterisation,new-characterisation}_*.trx.
Build: zero warnings/errors. Existing tests remain unchanged.

## Durable gap design

Keep MainContactId exclusively for a linked Active Main. Add nullable
RetiredMainContactId, a restrictive reference to that shop's retained link.
ReplacementNeeded is a query over that reference. Main and retired references
are mutually exclusive. Retirement records the outgoing Main and clears Active
Main in every affected shop, then makes the Contact globally Inactive, all in
one serializable transaction. No Contact is deleted.

A derived query over current Contact.Status would lose the gap on reactivation.
The durable reference keeps the gap until an explicit Main choice clears it.
Automatic first-Main repair skips flagged shops, even on reactivation or new
links. Existing unflagged empty/legacy shops keep WI-018's first-Active rule.

Extend domain rules and SQL guards together. A new gap must refer to the previous
Main; a gap cannot be cleared without a linked Active replacement. Composite
foreign keys retain Main and retired links. Status updates cannot bypass the
explicit retirement transaction. Existing generic edits still block implicit
Main retirement, preserving the characterised protection.

Existing/new replacement saves the new Main before deleting the outgoing link,
inside the same transaction. New Contact creation/link/replacement is atomic.
Retirement checks the Contact version and all linked Location versions shown
to the user; replacement/unlink checks Contact and target Location versions.
Stale or invalid requests save nothing.

H-27 opens an inline named question with existing Active contacts, Add a new
contact, and No replacement yet. The last choice states the global effect and
lists affected shops. A separate explicit global retirement action is shown
on the same Contact record. H-26 displays the outgoing inactive person and gap
even when ordinary inactive Contacts are hidden.

The gap is available through the per-Location roster contract and queryable in
DirectoryDb for WI-144. Tablet UI, the gap-list screen and login suspension are
out of scope. No unresolved business decisions.
