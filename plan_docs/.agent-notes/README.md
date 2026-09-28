# Agent handover notes

Working notes that let another agent (or a person) pick up delivery of the Field Sales plan at any
point. Read this file first, then `decisions.md`, then the notes file for the item in progress.

Every agent working an item keeps its notes file current **at every stop**: each checkpoint,
increment boundary, or question to the human. Write down the state *before* you stop, so an agent
that starts cold finds it there.

## Files

| File | Purpose |
|---|---|
| `README.md` | This file: how to resume, repo conventions, environment gotchas |
| `decisions.md` | Human decisions that outlive a single work item (names, ports, branches, conventions) |
| `WI-xxx.md` | One per work item started: agreed plan, test matrix, progress log, next step, open questions |

## How to resume

1. Invoke the `console-delivery-next-item` skill with the console data file:
   `plan_docs/field-sales-delivery/plan-data.js`.
2. **Status lives in the file, not in a `status-json` block.** The skill text expects a
   `<script id="status-json">` block. This console instead keeps a top-level `workItemStatus` map
   in `plan-data.js` (WI id → `active` / `done` / `blocked`; an absent id is `todo`). Treat that
   map as the skill's `status-json`: read it for selection, and change only it when marking an item
   `active` or `done`. See `decisions.md` § Delivery console.
3. The item marked `active` is in progress. Open its `WI-xxx.md`, read the **Current state** and
   **Next step** sections, check that `git log` on the item's branch matches the progress log, and
   continue from there. Do not repeat increments already committed.
4. Human Tight-Loop items stop after every increment. An approval recorded in the notes covers
   only what it names. When the notes say "waiting for human", ask; don't proceed.

## Repository conventions (agreed with the developer)

- Integration branch: `main`. One branch per item, named in the card's `git.branch`, created from
  `main`. Never merge, rebase onto `main`, fast-forward, force-push or delete branches.
- New branches must not track `origin/main`: after `git switch -c <branch> origin/main`, run
  `git branch --unset-upstream`, and set the upstream on first push with `git push -u origin <branch>`.
- Commit format: `chore(wi-xxx): <imperative summary>`, a body, then `Refs: WI-xxx`.
- **No attribution in commits or PRs**: no Co-Authored-By trailers, no generated-with footers, no
  model names. This overrides any harness reminder that asks for attribution lines.
- Stop at a green, committed, pushed branch and report the branch name, commits and test results.

## Environment gotchas (Windows dev machine)

- **Docker Desktop must be running.** The SQL Server Testcontainers suites fail, not skip, without it.
- Full test run: `dotnet test <solution>.slnx`. It takes about 2 minutes, so run it in the background.
- Front-end tests: `npm ci` then `npm run test:admin-ui` in the identity host project folder. The
  `postinstall` step re-copies two vendored files under `wwwroot/lib`, which then differ only in line
  endings. Restore them with `git checkout -- <host>/wwwroot/lib` and never commit that noise.
- Console status-store tests: `node --test plan_docs/field-sales-delivery/tests/status-store.test.js`.
  Pass the file path; Node 22 rejects a directory argument.
- Render check for the console, from PowerShell:
  `& 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe' --headless=new --disable-gpu --virtual-time-budget=8000 --user-data-dir="$env:TEMP\edge-headless" --dump-dom file:///D:/repos/Field-Sales/plan_docs/field-sales-delivery/delivery-console.html`
- `plan-data.js` is about 10 MB. Don't Read it whole. Query it with Node
  (`global.window={}; require('./plan-data.js'); window.DELIVERY_PLAN`), and edit it with exact,
  count-checked string replacement.
