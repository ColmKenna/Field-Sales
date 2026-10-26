# WI-032 increment 4b: device results on the Samsung

Run on 2026-10-26 on the Samsung SM-P610 (Android 13, serial `R52T30AZZXZ`) with the dev-trust Release APK built from commit `457faa2`
(SHA-256 `7f69e6da0488e834838225e8ea5cdbcda9caa55e3d29c4e8f579fbc93e9740d7`). The demo profile ran on the PC with
the sample seed plus the `scale` seed (`scale-v1`). Screenshots and logs are kept locally in `.artifacts/wi032/4b/` and are not committed.

## Scale used

| | Count |
|---|---|
| Colm's snapshot | 502 shops (500 scale shops, Swords sample shop, Arklow Branch override), 803 categories, 25,003 products, 75,004 prices |
| Aoife's snapshot | 1 shop (Rathdrum Branch), same catalogue |

One restricted sample product is left out of every snapshot, which is why 25,003 appears instead of 25,004.

## Scenario results

| # | Scenario | Result | Evidence |
|---|---|---|---|
| 15 (S1) | First use, connected sign-in and Sync | **Pass** | Colm signed in through Chrome, synced, and Home showed 502 shops with "Last synced 15:57" |
| 16 (S2) | Reopen offline | **Pass, approximated** | Wifi off, app force-closed and reopened: Home opened with the same 502 shops and the same Last synced time, with no sign-in prompt. The literal "signed in two hours ago" is covered by the Core tests |
| 14 | No connection on Sync | **Pass** | Sync now disabled, "No connection — your work is saved on this tablet", Last synced unchanged |
| 17 (S3) | No snapshot and no connection | **Pass** | After clearing app data and with wifi off: "A connection is needed for your first sign-in and download. No downloaded day is available yet." No shops or names |
| 18 (S4) | 24 hours elapsed | **Pass for the screen; Unsent work checked by Core tests only** | Clock moved forward 50 hours, app backgrounded and reopened: the sign-in screen with "Work saved on this tablet stays here." appeared. The app's files are not readable without root, so the kept Unsent database is covered by the Core tests (scenarios 11 and 18), not by a device check |
| 21 | Another rep on the same tablet | **Pass** | Aoife signed in on the same tablet, saw "No downloaded day yet" and then only Rathdrum Branch (1 shop). None of Colm's shops appeared |
| 22 | Account is not a Field Salesperson | **Pass** | `manager@demo.sales.local` was refused with "This app is for Field Salespeople." No Home or Sync |
| 25 | Budgets at target scale | **Refresh met; open borderline** | See below |

Scenarios 19 and 20 (reopen offline does not extend the window; clock before the last sign-in) remain Core-only, as in the plan.

## Scenario 25 timings

Taken from the `FieldSalesTiming` Android log lines.

| Measure | Readings | Budget | Result |
|---|---|---|---|
| Sync (refresh) at 25,003 products, 502 shops | 26.9 s, 26.6 s (Colm); 27.4 s (Aoife, 1 shop) | under 90 s | **Met**, about 30% of the budget |
| Post-switch cleanup | 45 ms, 7 ms, 24 ms | not budgeted | Runs in the background after the switch (D2-B) |
| Open, cold start to the shops list | 3.9 s, 2.9 s, 3.0 s | under 3 s | **Borderline**: one of three cold starts is over |
| Open, offline cold start | 2.8 s | under 3 s | Within the budget |
| Android's own launch time to first frame | 1.3 to 1.6 s | not budgeted | |

The open figure runs from Android starting the process to the shops list appearing, so it includes runtime and WebView startup. The 1C
spike measured the data open only (worst 1.1 s), which is a narrower measure, so the two are not directly comparable. The
developer decides which definition applies to the 3 s budget. The search budget (under 1 s) belongs to T-4.2.1.

The Sync time is dominated by the 25,003-product catalogue, which every rep downloads: Aoife's one-shop day took as long as Colm's 502-shop day.

## Sign-in on the device

- The ASP.NET development certificate is a plain leaf certificate (`CA:FALSE`). Android refuses to install it as a trusted CA ("private key
  required"), so it cannot be added to the user store.
- It is not needed in the user store: the app trusts it for API and token calls through its localhost-only network security config.
- Chrome needs it for the sign-in page. Chrome's insecure-localhost flag lets the page load, shown as "not secure" in the address bar.
- If the flag is unavailable, the fallback is a small dev root CA (`CA:TRUE`) plus a `localhost` certificate it signs for the demo hosts.

## Findings for later cards

- The app's "Field Sales" header is partly hidden under the Android status bar on this tablet. It is cosmetic and not part of this card's acceptance.
- Sign-in uses the system browser, as designed. Because `prompt=login` is set, the login page opened empty for the second rep.

## Tablet state after the session

Wifi on, screen timeout back to 120 s, automatic date and time on (the clock matched the PC within a second), the pushed certificate removed,
adb reverse removed, and the app's data cleared by scenario 17. The APK stays installed.
