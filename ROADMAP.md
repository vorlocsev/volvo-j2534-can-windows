## Dvinholm v0.2.0 stable release — Windows launch confirmed — 2026-10-18

- [x] User manually confirmed that the exact published v0.2.0 Windows archive opens on Windows 11 LTSC IoT 26100.9550.
- [x] Stable release remains available at [v0.2.0](https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.0). Asset SHA-256: `c80e56a8cf594485cfeb82a1003907cff22c52716abf2e60e518d290b4f3deec`.
- [x] Automated release pipeline passed 479 tests; latest main CI on `f889482e99565ae4093de2da3cc3c4bbe75308a2` passed: [run #39048794190](https://github.com/vorlocsev/dvinholm/actions/runs/39048794190).
- [ ] Continue manual UI verification: updater button/state and retry flows; inspect disconnected/no-adapter behavior and monitor/UDS controls as applicable. The app launch alone does not verify all UI flows.
- [ ] Physical J2534 adapter and vehicle testing remain deferred to a future release; do not claim hardware validation.

---

## Post-PR #275 main CI passed — 2026-10-17

- [x] Documentation checkpoint PR [#275](https://github.com/vorlocsev/dvinholm/pull/275) is merged as `9d8dcbf9d7797b5a65d7fd9461c6472ba34ff931`; the complete existing `ROADMAP.md` and `AI_WORKLOG.md` content was preserved.
- [x] Post-merge [CI run #39045906011](https://github.com/vorlocsev/dvinholm/actions/runs/39045906011) passed on the exact `main` SHA. Both `build` and `audit-v0-1-1-release` jobs completed successfully.
- [x] The published stable v0.1.1 release ZIP audit remains green; this CI result does not constitute manual verification of updater behavior in the installed Windows UI.
- [ ] Windows manual updater/button-state verification remains outstanding. Physical J2534 adapter/vehicle testing remains deferred.
- [ ] Stable v0.2.0 remains unpublished and requires explicit user authorization. Do not merge PR #228 or PR #225 without explicit approval; the digest metadata fix from #225 is already present on `main`.

---

## PR #274 merged; updater CI run pagination — 2026-10-17

- [x] PR [#274](https://github.com/vorlocsev/dvinholm/pull/274) merged into `main` as `53d32922ac4d198d80efc7a4c2d2a6b2d2266c82`.
- [x] Both pre-merge CI runs on head `01d20a0a79c4ef5530b9df92dd6f6967591769f4` passed: [push run #39044585178](https://github.com/vorlocsev/dvinholm/actions/runs/39044585178) and [PR run #39044588419](https://github.com/vorlocsev/dvinholm/actions/runs/39044588419).
- [x] Post-merge [CI run #39044984373](https://github.com/vorlocsev/dvinholm/actions/runs/39044984373) passed on the exact merge SHA; build/tests and audit of the published v0.1.1 ZIP succeeded.
- [x] `LatestBuildUpdater.GetLatestSuccessfulBuildAsync` now paginates through GitHub Actions workflow runs (100 per page) until an eligible published build is found or a short page indicates there are no more runs. Pull-request events remain excluded because their SHA can identify a synthetic merge commit.
- [x] Added `LookupContinuesToSecondPageWhenFirstPageHasNoEligibleBuild`, covering a full page of PR runs followed by an eligible push build on page 2. The test asserts page progression and expected build metadata.
- [ ] Windows manual updater/button verification remains outstanding; no physical J2534 adapter/vehicle was available or needed for this API lookup change.
- [ ] Stable v0.2.0 remains unpublished and requires explicit user authorization. PR #228 remains open and must not be merged without explicit user approval.

## Post-PR #272 main CI and UDS source-audit checkpoint — 2026-10-17

- [x] PR [#272](https://github.com/vorlocsev/dvinholm/pull/272) merged as `8eb69feabc652984c4deaa6236efff249a70bd47`.
- [x] Post-merge [CI run #38997313752](https://github.com/vorlocsev/dvinholm/actions/runs/38997313752) passed on the exact merge SHA; build/tests and audit of the published v0.1.1 ZIP both succeeded.
- [x] Re-audited `MainWindow.xaml.cs`: UDS operations are serialized through `_udsGate`, cancel tokens are signalled on window close, and session disposal is gated on tracked task completion and gate availability. This is a source review, not a new WinUI runtime test.
- [x] Reviewed the test inventory; existing tests already cover UDS cancellation, late-response correlation, ISO-TP cancellation/recovery, and virtual J2534 flows. No duplicate test was added.
- [ ] Manual Windows updater/button-state confirmation remains outstanding. User has no physical adapter; active-session CAN/UDS UI behavior and hardware/vehicle validation remain deferred.
- PR [#228](https://github.com/vorlocsev/dvinholm/pull/228) and stale updater PR [#225](https://github.com/vorlocsev/dvinholm/pull/225) remain unmerged; do not merge either without explicit approval. Stable v0.2.0 remains unauthorized.

---

## Current main checkpoint — PR #271 merged; post-merge CI passed (2026-10-17)

- [x] PR [#271](https://github.com/vorlocsev/dvinholm/pull/271) was squash-merged as `bffd39ded30af567089dacab1cbdf95f1347912a`; it records the updater forward-port and its CI results in this roadmap and the AI work log.
- [x] Post-merge CI [run #38990733864](https://github.com/vorlocsev/dvinholm/actions/runs/38990733864) passed on this exact `main` commit: build, tests, publish pipeline, and audit of the published v0.1.1 ZIP all succeeded.
- [x] Current `main` includes the updater hardening for malformed run metadata, validated GitHub release asset URLs/digests, binary release-asset download, and user-initiated update discovery with SHA-based update-state handling.
- [ ] Manual Windows runtime confirmation of updater startup/button behavior remains outstanding; CI does not substitute for this check.
- PR [#228](https://github.com/vorlocsev/dvinholm/pull/228) remains open and unmerged. Do not merge it without separate explicit approval.
- Physical J2534 adapter/vehicle testing remains deferred. Stable v0.2.0 remains unpublished and requires explicit user authorization.

---

## Updater-state forward-port merged — post-merge CI passed (2026-10-17)

- [x] PR [#270](https://github.com/vorlocsev/dvinholm/pull/270) was squash-merged into `main` as `b2a4661aee05251472ee93f78e1b0ea815dcd33f`.
- [x] Post-merge CI [run #38989890059](https://github.com/vorlocsev/dvinholm/actions/runs/38989890059) passed on the exact merge commit. The build/test/publish pipeline and audit of the exact published v0.1.1 ZIP both succeeded.
- [x] The forward-port preserves SHA-based update comparison already on main, makes update discovery user-initiated instead of automatic at startup, normalizes the button to **Check update**, adds same-SHA/different-SHA regression coverage, and skips CI for worklog-only pushes.
- [ ] User should manually confirm the updater startup/button behavior in the Windows app when convenient; CI is not a substitute for runtime UI verification.
- PR [#228](https://github.com/vorlocsev/dvinholm/pull/228) remains open and unmerged. Do not merge it without separate explicit approval.
- Physical J2534 adapter/vehicle testing remains deferred. Stable v0.2.0 remains unpublished and requires explicit user authorization.

---

## Latest CI checkpoint — main CI #2296 retry passed (2026-10-17)

- Main commit `024addce1dbe68a6e573f7e67748e1946f0d4128` has a successful second attempt of [CI #2296](https://github.com/vorlocsev/dvinholm/actions/runs/38984726872): build/tests and v0.1.1 release archive audit passed.
- The first attempt had 475/476 tests passing; `LatestBuildUpdaterFailureTests.ReplacementScript_RestoresPreviousBuildWhenReplacementFailsAtRuntime` failed while deleting its temporary directory because Windows reported it was still in use. The rerun passed, so this is currently non-reproducible and should not be described as a confirmed updater defect or a proven fix.
- Continue investigating only if the cleanup failure recurs; do not weaken rollback assertions or hide cleanup failures. Hardware validation remains deferred; v0.2.0 requires explicit user authorization.

# Dvinholm development roadmap

Updated: 2026-10-17  
Current main checkpoint: `8eb69feabc652984c4deaa6236efff249a70bd47`  
Latest stable release: [v0.1.1](https://github.com/vorlocsev/dvinholm/releases/tag/v0.1.1)

This roadmap separates software/CI verification from physical adapter validation. Passing tests do not establish compatibility with a real interface or vehicle.

## Completed / verified

- [x] Stable portable Windows x86 release v0.1.1 published.
- [x] CI build, test, publish, and artifact packaging passed for release v0.1.1.
- [x] Main-branch warning cleanup merged and post-merge CI passed.
- [x] Updater metadata hardening merged in PR #233; PR CI and post-merge main CI passed.
- [x] Updater checks release URLs against the expected GitHub project path and HTTPS host.
- [x] Updater validates SHA-256 metadata when supplied and skips malformed metadata instead of trusting it.
- [x] Updater can request the release asset API's binary response; tests cover this download path.
- [x] PR #235 adds a regression test proving a mid-stream download I/O failure cleans up both the temporary ZIP and staging directory; PR and post-merge CI passed.
- [x] PR #237 adds a regression test for cancellation after the first archive chunk; PR, branch, and post-merge main CI passed.
- [x] PR #239 strengthens SHA-256 mismatch cleanup coverage by asserting both temporary ZIP and staging directory removal; PR and branch CI passed. Post-merge main CI [#38916569004](https://github.com/vorlocsev/dvinholm/actions/runs/38916569004) passed.
- [x] PR #240 records the digest-mismatch test and roadmap checkpoint; PR and branch CI passed. Post-merge main CI [#38917668043](https://github.com/vorlocsev/dvinholm/actions/runs/38917668043) passed.
- [x] PR #241 strengthens corrupt-ZIP cleanup assertions for the temporary archive and staging directory; PR/branch CI and post-merge main CI [#38920429003](https://github.com/vorlocsev/dvinholm/actions/runs/38920429003) passed.
- [x] PR #242 adds missing-executable cleanup assertions; PR CI passed and the transient post-merge public CI asset publish HTTP 500 passed on retry [#38921421584](https://github.com/vorlocsev/dvinholm/actions/runs/38921421584).
- [x] PR #243 adds updater replacement backup/rollback behavior; PR/branch CI and post-merge main CI [#38922622760](https://github.com/vorlocsev/dvinholm/actions/runs/38922622760) passed.
- [x] PR #246 executes the generated replacement PowerShell script in isolated Windows temp directories, injects a partial replacement failure, and verifies the old executable/marker are restored, the partial marker is absent, the rollback is logged, and relaunch is requested. PR CI [#38925619261](https://github.com/vorlocsev/dvinholm/actions/runs/38925619261) and branch CI [#38925610701](https://github.com/vorlocsev/dvinholm/actions/runs/38925610701) passed; merged as `91f000014e7d09546f868c12299d2235f13d1f3f`.
- [x] PR #248 prevents J2534 disconnect from racing a still-unwinding CAN Monitor worker: it requests monitor cancellation and awaits the monitor task before disconnecting the session. PR CI [#38927452306](https://github.com/vorlocsev/dvinholm/actions/runs/38927452306), branch CI [#38927445038](https://github.com/vorlocsev/dvinholm/actions/runs/38927445038), and post-merge main CI [#38928179572](https://github.com/vorlocsev/dvinholm/actions/runs/38928179572) passed; merged as `6804b8828879df51f24f10a837cb9808b6989f68`.
- [x] PR #250 preserves the CAN Monitor diagnostic when disconnect cleanup begins; PR CI [#38928375204](https://github.com/vorlocsev/dvinholm/actions/runs/38928375204) passed and it was merged as `c38e2e1de7c37c0fc9cfff26c7051ca74100a39b`. Post-merge main CI on that commit [#38929247943](https://github.com/vorlocsev/dvinholm/actions/runs/38929247943) passed.
- [x] PR #249 records the monitor/disconnect lifecycle checkpoint and was merged as `6ef6046bfd819b436772c8731495d7cc3cc5fb3b`. Its first PR CI attempt had one ISO-TP receive-queue overflow test failure (475/476); retry [#38928314219](https://github.com/vorlocsev/dvinholm/actions/runs/38928314219) passed. The test uses a virtual adapter; no hardware was involved.
- [x] PR #251 updates the monitor/disconnect checkpoint and UDS lifecycle audit; merged as `09a68831ceb855a582c9808f7ac3ce56a7562bbf`. PR-head CI [#38930299279](https://github.com/vorlocsev/dvinholm/actions/runs/38930299279) and branch CI [#38930277558](https://github.com/vorlocsev/dvinholm/actions/runs/38930277558) passed.
- [x] PR #252 records the PR #251 merge checkpoint; merged as `afef97288c1f9456130f2311b02181ab3b7ffca2`. PR-head CI [#38931312837](https://github.com/vorlocsev/dvinholm/actions/runs/38931312837) and branch CI [#38931290345](https://github.com/vorlocsev/dvinholm/actions/runs/38931290345) passed. Post-merge main CI [#38932062825](https://github.com/vorlocsev/dvinholm/actions/runs/38932062825) is still running.
- [x] Automated coverage exists for CAN receive-dispatcher lifecycle/races, J2534 session lifecycle/read errors, ISO-TP malformed frames/timeouts/cancellation/recovery, UDS VIN/DTC parsing and response handling, and virtual J2534 adapter scenarios.
- [x] User manually confirmed the updater success path and error/retry handling on Windows 11 LTSC IoT.
- [x] User manually confirmed ECU profile controls and CAN Monitor remain visible in the compact layout, including a narrower window width.

## Next software-only work

- [x] Monitor/disconnect lifecycle fixes are merged: #248 awaits CAN Monitor shutdown before J2534 disconnect, and #250 preserves a monitor warning during disconnect. PR, branch, and post-merge main CI checks passed for these source changes.
- [x] Documentation checkpoint after PR #249 passed post-merge main CI [#38930123098](https://github.com/vorlocsev/dvinholm/actions/runs/38930123098). PR #251 and PR #252 documentation updates passed PR-head/branch CI. Main checkpoint is now `afef97288c1f9456130f2311b02181ab3b7ffca2`; post-merge main CI [#38932062825](https://github.com/vorlocsev/dvinholm/actions/runs/38932062825) is running. The preceding main run on `09a68831ceb855a582c9808f7ac3ce56a7562bbf` failed in an updater rollback runtime test cleanup, not in compilation.

1. [ ] Review the current-main test inventory and CI test report to identify remaining untested release-critical paths; avoid duplicating already-covered lifecycle/timeout tests.
2. [x] Updater failure-path cleanup covers partial-download I/O failure (#235), cancellation after partial content (#237), SHA-256 mismatch (#239), corrupt ZIP (#241), and missing executable (#242). Replacement backup/rollback is implemented in #243 and runtime-tested on Windows in #246 using isolated temporary directories and stubbed copy/process commands. This verifies the scripted rollback scenario without touching an installed application; it does not replace manual in-place updater testing on the user's Windows installation.
3. [x] Reviewed monitor/disconnect ordering and merged PR #248 so the app waits for the monitor worker to exit before disconnecting the J2534 session; PR #250 ensures monitor warnings survive into disconnect status. PR/branch CI passed, and post-merge main CI for #250 passed. The later documentation-only main checkpoint still awaits CI.
4. [x] Reviewed UDS read-only command gating in `MainWindow.xaml.cs`: `TryGetUdsClient` refuses requests when `_connected` is false or CAN IDs are invalid; Read VIN/DTC/DID routes through this guard, and UDS/discovery share `_udsGate`. No guard was bypassed. Continue automated state-transition coverage if gaps are found.
5. [ ] Verify UDS read-only command gating and UI state transitions in code: Read VIN and Start Monitor should become enabled only when the required J2534/CAN session is active; do not bypass these safety gates.
6. [ ] Review cancellation/disconnect behavior while VIN/DTC reads are active; add deterministic virtual-adapter tests for any uncovered race. The close handler cancels UDS/discovery, waits up to two seconds, and only disposes the J2534 session when tracked operations have stopped; test this boundary more directly.
7. [ ] Re-run full CI after each change; update this roadmap and `AI_WORKLOG.md` only with confirmed results.
8. [ ] After the software-only checklist is complete, prepare a release-candidate checklist and confirm the exact published release archive separately from CI artifacts.

## Deferred to a future hardware-validation cycle

- [ ] Test with physical Volvo DiCE and/or Mongoose J2534 hardware.
- [ ] Confirm actual device open/close, CAN traffic, VIN/DTC responses, hardware filters, and disconnect/reconnect behavior against a real ECU/vehicle.
- [ ] Record adapter model, driver version, vehicle/ECU details, and observed limitations before claiming hardware compatibility.

## Release gates

- All required CI checks pass on the exact merge commit.
- Software-only regression tests pass, including updater integrity and failure recovery.
- User-facing updater errors are recoverable and leave the installed application in a consistent state.
- Read-only UDS functions remain explicitly gated by connection state.
- Release notes clearly distinguish software/virtual testing from physical adapter verification.


## CI checkpoint — PR #253 rollback-test cleanup (2026-10-16)

- [x] PR [#253](https://github.com/vorlocsev/dvinholm/pull/253) fixes the Windows updater rollback test cleanup by disposing the PowerShell process before deleting its temporary root and draining redirected stdout/stderr concurrently. Existing rollback assertions remain intact.
- [x] Latest PR-head CI [#38932272236](https://github.com/vorlocsev/dvinholm/actions/runs/38932272236) passed; PR #253 merged to `main` as `db3e8607ec0c28057649f7f616056a71b9bc991e`.
- [x] Post-merge main CI [#38933065485](https://github.com/vorlocsev/dvinholm/actions/runs/38933065485) passed on merge commit `db3e8607ec0c28057649f7f616056a71b9bc991e` (build, test, publish, and artifact steps succeeded).
- [x] Software-only UDS lifecycle audit reviewed the window-close handler and existing cancellation/session tests. Active and queued UDS cancellation, ISO-TP cancellation/recovery, and J2534 session lifecycle already have automated coverage. The UI `Closed` event itself is coupled to WinUI and does not have a direct unit-test seam; no duplicate or speculative test was added. A future targeted refactor may extract a small shutdown coordinator if a reliable regression test is needed.
- Physical J2534 adapter and vehicle checks remain deferred to a future release.


## UDS lifecycle audit checkpoint (2026-10-16)

- [x] Confirmed active/queued UDS cancellation and ISO-TP cancellation/recovery tests exist; reviewed the MainWindow.Closed shutdown sequence. It cancels discovery/UDS, waits for tracked workers, and only schedules session disposal when discovery, monitor, connect/disconnect, and UDS gate are all stopped.
- [x] No uncovered race was demonstrated in the testable transport/session layers, so no redundant test was added. The UI close handler remains a review-only boundary because it is coupled to WinUI; consider a focused shutdown coordinator extraction only if further changes make a deterministic test seam valuable.
- [x] Main CI after PR #253 passed: [run #38933065485](https://github.com/vorlocsev/dvinholm/actions/runs/38933065485).
- Physical J2534 adapter and vehicle testing remain deferred.

## Manual UI verification — connection retry (2026-10-16)

- [x] User confirmed the manual Windows check for retrying after a failed J2534 connection attempt succeeded; the application remained usable and the retry path behaved as expected.
- This is a UI/error-recovery confirmation only. No physical J2534 adapter or vehicle was used, and hardware compatibility remains unverified.
- [ ] Continue software-only verification of connection-state controls and error recovery.

## Manual UI verification — diagnostic controls require connection (2026-10-16)

- [x] User confirmed Read VIN, Read DTC, and Start Monitor remain unavailable without an active J2534 connection; failed connection attempts do not hang the app.
- This confirms UI gating only; no live CAN/UDS transaction or hardware compatibility was tested.
- [ ] Continue software-only release checks. Physical adapter and vehicle testing remains deferred.


## Manual UI verification — profile persistence across restart (2026-10-16)

- [x] User confirmed that a previously saved ECU profile remains present after closing Dvinholm normally and launching it again.
- This confirms local profile persistence across an ordinary restart only; it does not validate ECU communication or physical adapter compatibility.
- Physical J2534 adapter and vehicle testing remains deferred to a future hardware-validation cycle.
- [ ] Continue software-only release checks and record only confirmed outcomes.


## Manual UI verification — edited profile persists across restart (2026-10-16)

- [x] User confirmed that changing a safe profile field (profile name), saving it, then closing and relaunching Dvinholm preserves the updated name.
- This confirms edited profile data persists in local storage across restart; no ECU communication was involved.
- Physical J2534 adapter and vehicle testing remains deferred to a future hardware-validation cycle.
- [ ] Continue with a non-destructive software-only profile check; protect the user's only useful profile.


## Manual UI verification — edited profile JSON export (2026-10-16)

- [x] User confirmed exporting the edited ECU profile succeeds and the JSON includes the updated profile name with the expected fields/structure intact.
- This verifies local profile export/serialization only; no ECU communication was involved.
- Physical J2534 adapter and vehicle testing remains deferred to a future hardware-validation cycle.
- [ ] Verify import round-trip with a separate copy of the exported JSON, keeping the original profile untouched.


## Manual UI verification — profile JSON import round-trip (2026-10-16)

- [x] User confirmed importing the exported ECU profile JSON succeeds and the imported profile retains the expected name and fields.
- [x] Original profile remains available; local export/import round-trip verified without ECU communication.
- Physical J2534 adapter and vehicle testing remains deferred to a future hardware-validation cycle.
- [ ] Check profile isolation by switching between profiles once a second disposable/test profile is available; protect the user's only useful profile.


## Manual UI verification — profile isolation/switching (2026-10-16)

- [x] User confirmed the original ECU profile and a separate `TEST-ECU` profile are both available and open independently.
- [x] Switching profiles does not mix their names or data.
- This verifies local profile selection/data isolation only; physical J2534 adapter and vehicle testing remains deferred.
- [ ] Verify safe deletion behavior using only disposable `TEST-ECU`; preserve the original useful profile.


## Manual UI verification — safe profile deletion (2026-10-16)

- [x] User confirmed deleting disposable `TEST-ECU` removes it from the list.
- [x] Original useful profile remains intact and available.
- [x] After app restart, deleted `TEST-ECU` does not reappear.
- This verifies local profile deletion/persistence only; physical J2534 adapter and vehicle testing remains deferred.
- [ ] Finish final software-only profile workflow check, then review release-gate items and current PR/CI state before deciding on release 0.2.0.


## Manual UI verification — complete profile workflow (2026-10-16)

- [x] Opened the original profile, changed its name temporarily, saved, exported JSON, restarted the app and confirmed the edited name persisted.
- [x] Restored the original profile name and saved successfully.
- This confirms the local edit/save/export/restart workflow only; physical J2534 adapter and vehicle testing remains deferred.
- [ ] Review remaining software-only release gates plus current PR #255 and CI state before deciding whether Dvinholm 0.2.0 is ready.


## Manual UI verification — malformed profile JSON rejected safely (2026-10-16)

- [x] User confirmed importing a deliberately malformed JSON copy (missing closing brace) reports an error without hanging the app.
- [x] Existing original profile remains available and unchanged.
- This verifies local malformed-input handling only; physical J2534 adapter and vehicle testing remains deferred.
- [ ] Continue software-only release gates and confirm PR #255 CI/mergeability before recommending release 0.2.0.


## Manual UI verification — valid JSON missing profile name (2026-10-16)

- [x] Confirmed safe handling when importing a syntactically valid profile JSON copy with the profile-name field omitted.
- Exact UI behavior (rejection versus safe fallback) was not specified; record only the successful test outcome, without assuming implementation details.
- This is local profile-import validation only. No physical J2534 adapter or vehicle was used; hardware validation remains deferred.
- [ ] Next: test an invalid field value/type in a disposable, syntactically valid profile JSON copy and confirm safe handling without changing the original profile.


## Manual UI verification — profile name has wrong JSON type (2026-10-16)

- [x] Confirmed successful handling of a syntactically valid profile JSON copy whose profile-name value was changed from a string to a number.
- The test used a copy and the original saved profile was preserved. Exact UI behavior was not specified, so no particular error message or fallback is claimed.
- This covers local profile-import handling only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: test an unknown/extra JSON property in a disposable profile copy and confirm safe handling without altering the original profile.


## Manual UI verification — extra unknown JSON property (2026-10-16)

- [x] Confirmed successful handling of a valid profile JSON copy containing an extra property (`test_extra_field`); the original profile remained preserved.
- This confirms safe import handling for an unknown/extra property, but does not claim whether that property is persisted or discarded.
- Local profile-import check only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: import the same valid profile JSON twice and check for unintended duplicate profiles or corruption of the saved profile list.


## Manual UI verification — repeated import of same profile JSON (2026-10-16)

- [x] Confirmed that importing the same profile JSON twice and restarting Dvinholm left the saved profile list correct, with no observed unintended duplicates or corruption.
- This verifies the repeated-import/persistence workflow for the tested profile only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: cancel the profile-import file picker and verify that the profile list remains unchanged and no false import-success message appears.


## Manual UI verification — cancel profile import (2026-10-16)

- [x] Confirmed cancelling the profile-import file picker leaves the profile list unchanged and does not report a false successful import.
- Local profile-import UI check only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: if export/save offers a file dialog, cancel it and verify no file is falsely reported as saved and the existing profile remains intact.


## Manual UI verification — cancel profile export (2026-10-16)

- [x] Confirmed cancelling the profile export/save dialog does not show a false successful-save result and leaves the original profile unchanged.
- Local profile-export UI check only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: export to a new temporary filename, verify the file exists and is valid JSON, then exercise the export/import round trip without overwriting the original profile.


## Manual UI verification — profile export/import round trip (2026-10-16)

- [x] Confirmed export to a new temporary filename produced a file containing valid JSON, and importing it without overwriting the original profile succeeded.
- This validates the tested local profile export/import round trip only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: repeat required-field validation with a disposable JSON copy missing a required profile field, confirming the original remains intact and the app stays responsive.


## Manual UI verification — required profile field missing (2026-10-16)

- [x] User confirmed successful handling of a syntactically valid profile JSON copy with another required field omitted.
- Exact field and UI behavior were not specified; no specific error message or fallback is claimed. The original profile was kept separate.
- Local profile-import validation only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: inspect the repository's profile JSON schema and validation rules before choosing further field-specific tests; do not guess required fields.


## Manual UI verification — required profile field missing (2026-10-16)

- [x] User confirmed successful handling of a syntactically valid profile JSON copy with another required profile field omitted.
- The exact field and UI behavior were not specified; no particular error message or fallback is claimed. The original profile was kept separate.
- Local profile-import validation only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: inspect the profile JSON schema/validation rules before further field-specific tests; then check empty-catalog import behavior using a backed-up catalog.


## Manual UI verification — import empty profile catalog (2026-10-16)

- [x] User confirmed the empty JSON array (`[]`) import test succeeded after backing up the existing profile catalog.
- This records the empty-catalog import path only; do not assume the original profiles have been restored or remained in the active list. Restore from the backup and verify integrity before further destructive-state tests.
- Local profile UI check only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: confirm the empty catalog displays correctly, restore the saved catalog backup, and verify the original profiles return intact.


## Manual UI verification — restore catalog after empty import (2026-10-16)

- [x] Confirmed that restoring the saved JSON catalog after importing `[]` returned the original profiles, preserved names and main fields, and persisted after app restart.
- This closes the empty-catalog recovery workflow. No physical J2534 adapter or vehicle was used; hardware validation remains deferred.
- [ ] Next: test a catalog containing duplicate profile IDs in a disposable JSON copy; verify rejection is safe and the current catalog remains intact.


## Manual UI verification — duplicate profile IDs (2026-10-16)

- [x] Confirmed that importing a catalog with duplicate profile IDs is rejected/handled as an error without hanging or damaging the existing profiles.
- No ECU communication or physical J2534 adapter/vehicle was used; hardware validation remains deferred.
- [ ] Next: verify invalid CAN identifier validation using a disposable profile/catalog; confirm invalid values are not accepted and existing profiles remain intact.


## Manual UI verification — invalid CAN identifiers (2026-10-16)

- [x] Confirmed that out-of-range CAN IDs for the selected identifier format are rejected/not accepted on save and existing profiles remain intact.
- This was a profile validation check only; no ECU communication or physical adapter/vehicle was used. Hardware validation remains deferred.
- [ ] Next: verify request and response CAN IDs cannot be identical in a disposable profile; confirm existing profiles remain intact.


## Manual UI verification — identical request/response CAN IDs (2026-10-16)

- [x] Confirmed that a profile with identical request and response CAN IDs is rejected/not accepted on save and other profiles remain intact.
- This was a profile validation check only; no ECU communication or physical adapter/vehicle was used. Hardware validation remains deferred.
- [ ] Next: verify invalid/malformed diagnostic identifier (DID) validation using a disposable profile, without modifying the existing catalog.


## Manual UI verification — malformed DID input (2026-10-16)

- [x] User confirmed malformed DID input was handled safely without hanging or damaging existing profiles.
- No ECU communication or physical J2534 adapter/vehicle was used; hardware validation remains deferred.
- [ ] Next: test duplicate DID handling in a disposable profile/catalog if the UI supports DID editing. If not, record the UI limitation and proceed to another software-only check.


## Manual UI verification — duplicate DID entries (2026-10-16)

- [x] User confirmed duplicate DID entries were handled safely without an app hang or damage to existing profiles.
- No ECU communication or physical J2534 adapter/vehicle was used; hardware validation remains deferred.
- [ ] Next: export the restored original catalog to a new JSON filename and verify valid JSON, expected profile names, and key fields without overwriting the only backup.


## Manual UI verification — export catalog after validation tests (2026-10-16)

- [x] Confirmed export to a new JSON file succeeds; JSON is valid and contains expected original profile names/key fields with no unintended temporary test profiles.
- The original backup was preserved; no ECU communication or physical adapter/vehicle was used. Hardware validation remains deferred.
- [ ] Next: import the newly exported JSON through the safe test workflow and compare profile count, names, and key fields with the exported source, keeping the original backup intact.


## Manual UI verification — catalog export/import round trip (2026-10-16)

- [x] Confirmed re-import of the exported catalog preserves profile count, names, and key fields; no unintended duplicates or missing settings; data persists after restart.
- Original backup kept separately. No ECU communication or physical adapter/vehicle was used; hardware validation remains deferred.
- [ ] Next: launch the current Windows candidate, confirm the main window loads without an unhandled error, and close normally. This is not release approval; exact-head CI and all remaining release gates must still be checked.


## Manual Windows verification — candidate startup and normal exit (2026-10-16)

- [x] User confirmed the current Windows candidate starts with the main window fully loaded, without an unhandled error/crash, and closes normally.
- Startup/shutdown smoke check only; no ECU communication or physical adapter/vehicle was used. Hardware validation remains deferred.
- [ ] Next: inspect the current PR head and CI/check status to identify remaining release gates. Do not merge or publish a release without explicit user authorization.


## Manual Windows verification — startup without physical J2534 adapter (2026-10-16)

- [x] Confirmed current candidate reports the missing physical J2534 device, does not hang, keeps diagnostics unavailable while disconnected, and closes/relaunches normally.
- This is a no-device software behavior check only. Physical adapter/vehicle verification remains deferred to a future release.
- [ ] Next: inspect the latest CI result for the current PR head before evaluating release readiness.


## UDS UI state and cancellation audit — 2026-10-16

- [x] Source-audited UDS UI gating: diagnostic commands require an active connection and valid selected CAN-ID format/IDs; UDS requests and discovery are serialized; controls are disabled during requests and restored based on connection state.
- [x] Reviewed close/disposal ordering and existing tests; current coverage already includes active/queued UDS cancellation, ISO-TP disposal/cancellation, dispatcher/session disposal races, CAN ID parsing, and virtual-adapter read-only UDS flows. No redundant test added.
- [ ] Manually verify after a failed/no-device connection that Start Monitor and Read VIN/DTC/DID remain disabled and Connect is re-enabled for retry.
- [ ] Review any concrete uncovered close/disconnect race before adding a new deterministic virtual-adapter test. Physical J2534 adapter and vehicle testing remain deferred.


## Manual Windows UI verification — failed J2534 path (2026-10-16)

- [x] A nonexistent J2534 DLL path produces a handled connection error.
- [x] Connect is available for retry after the failure.
- [x] Start Monitor, Read VIN, Read DTC, and Read DID remain disabled while disconnected.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — valid J2534 DLL path, no device (2026-10-16)

- [x] A valid/detected J2534 DLL path with no physical adapter produces a handled missing-device result without hanging.
- [x] Connect is available again after the attempt.
- [x] Start Monitor, Read VIN, Read DTC, and Read DID remain disabled while disconnected.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical adapter and vehicle checks remain deferred to a future release.


## Manual Windows UI verification — repeated no-adapter attempts (2026-10-16)

- [x] Three consecutive Connect attempts with no physical J2534 adapter complete without a hang or crash.
- [x] The app remains responsive and Monitor/UDS actions remain disabled while disconnected.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — disconnected state after restart (2026-10-16)

- [x] App closes and relaunches normally after failed no-adapter connection attempts.
- [x] Relaunch does not incorrectly report an active connection; Monitor and UDS actions remain disabled without an adapter.
- [x] App closes normally after the check.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — ECU profile switching (2026-10-16)

- [x] Switching between saved ECU profiles shows the corresponding CAN request/response IDs.
- [x] Returning to the primary profile restores its original values; no profile edits were made.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — ECU profile persistence (2026-10-16)

- [x] A non-primary saved ECU profile retains its name and CAN request/response IDs after app restart.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — cancel ECU profile selection (2026-10-16)

- [x] Cancelling/closing profile selection leaves the current ECU profile and CAN IDs unchanged.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — CAN ID validation after profile switch (2026-10-16)

- [x] An out-of-range CAN ID is rejected when saving a non-primary ECU profile.
- [x] The profile's original settings remain intact after rejection.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — recovery after invalid CAN ID (2026-10-16)

- [x] After rejecting an out-of-range CAN ID, the original profile values remain intact and are displayed again after switching away and back.
- [x] No false persistent error or app malfunction observed.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — repeated ECU profile switching (2026-10-16)

- [x] Five switches between two saved ECU profiles do not mix their names or CAN request/response IDs.
- [x] Both profiles retain their respective values after restarting the application.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — startup profile consistency (2026-10-16)

- [x] After restart, the selected ECU profile's displayed CAN IDs match that profile.
- [x] Switching between the primary and second profiles displays the corresponding CAN IDs correctly.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — cancel unsaved ECU profile edit (2026-10-16)

- [x] Cancelling/closing an ECU profile editor after changing a CAN ID without saving preserves the original persisted value.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — ECU profile rename persistence (2026-10-16)

- [x] A temporary profile-name change saves and persists after switching away and back.
- [x] CAN IDs remain unchanged, and the original profile name was restored and saved.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — export profile after name restoration (2026-10-16)

- [x] Export to a separate temporary JSON file is valid and contains the expected original profile name and CAN IDs.
- [x] Temporary `-TEST` suffix is absent; backup and source profile were not overwritten.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import exported profile safely (2026-10-16)

- [x] Import of the exported ECU profile JSON was handled correctly.
- [x] Imported profile details were checked and the original profile was not overwritten.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — repeat JSON import (2026-10-16)

- [x] Re-import the same temporary ECU-profile JSON; original profile and CAN IDs remain unchanged, with no hang or false success.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import name collision (2026-10-16)

- [x] Import JSON using an existing profile name and cancel overwrite; original profile and CAN IDs remain unchanged, and the app stays responsive.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — dismiss overwrite dialog (2026-10-16)

- [x] Dismiss the profile overwrite dialog; app stays responsive and the existing profile/CAN IDs remain unchanged after restart.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — cancel import file picker (2026-10-16)

- [x] Cancel the import file picker; profile list and selection stay unchanged, with no false success notification.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — cancel profile export (2026-10-16)

- [x] Cancel the JSON export/save dialog; no false success message, source profile unchanged, app remains responsive.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — export non-primary ECU profile (2026-10-16)

- [x] Export a non-primary ECU profile and verify the JSON contains its own name and CAN IDs; source and backup files remain untouched.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — profile switching after export and restart (2026-10-16)

- [x] Confirm primary and non-primary profiles retain their own CAN IDs across switching and app restart.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import malformed CAN ID (2026-10-16)

- [x] Import a copied profile JSON with a malformed CAN ID; app remains responsive and existing profiles/CAN IDs are preserved.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import empty profile name (2026-10-16)

- [x] Import copied profile JSON with an empty name; app remains responsive and existing profiles stay unchanged.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import JSON missing profile name field (2026-10-16)

- [x] Import copied profile JSON without a profile-name field; app remains responsive and existing profiles remain unchanged.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — restart after import edge-case series (2026-10-16)

- [x] Restart after import edge-case checks; original profiles, names, and CAN IDs remain correct, and the app closes normally.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — rename profile, restart, restore name (2026-10-16)

- [x] Verify profile rename persists across restart without changing CAN IDs, then restore and save the original name.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — CAN IDs after repeated profile switching (2026-10-16)

- [x] Verify primary profile CAN IDs remain correct after repeated switching to another saved profile and back.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — discard unsaved CAN ID edit (2026-10-16)

- [x] Verify an unsaved CAN ID edit is discarded on profile switch and the persisted value is restored when returning.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — save and restore CAN ID (2026-10-16)

- [x] Verify a valid CAN ID change persists after profile switching and the original value can be restored and saved.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — restart after CAN ID save/restore (2026-10-16)

- [x] Restart after CAN ID save/restore; primary profile name and CAN IDs are correct, and switching profiles displays each profile's own values.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — export profile and compare after restart (2026-10-16)

- [x] Export the primary profile, restart the app, and verify the profile name and CAN IDs match the exported JSON.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — CAN Monitor unavailable without adapter (2026-10-16)

- [x] Confirmed CAN Monitor remains unavailable without a physical J2534 adapter and no false active-monitoring status is shown.
- [x] Confirmed the app closes and relaunches normally after this check.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — UDS controls across ECU profiles while disconnected (2026-10-16)

- [x] Read VIN, Read DTC, and Read DID stay unavailable when switching between saved ECU profiles without an adapter.
- [x] No false connected state appears; returning to the primary profile and closing the app work normally.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — repeated CAN Monitor attempts while disconnected (2026-10-16)

- [x] Repeated attempts to start CAN Monitor without an adapter do not hang or crash the app and do not show false active monitoring.
- [x] App closes and relaunches normally after the check.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — disconnected state after restart (2026-10-16)

- [x] After restart without a physical J2534 adapter, no active connection is reported.
- [x] Start Monitor, Read VIN, Read DTC, and Read DID remain unavailable while disconnected.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — repeated ECU profile switching while disconnected (2026-10-16)

- [x] Repeated profile switching displays the correct CAN IDs for each saved ECU profile.
- [x] Monitor and UDS actions remain unavailable without an adapter; UI stays responsive and closes normally.
- [ ] Finish remaining pre-release checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — failed connection on non-primary ECU profile (2026-10-16)

- [x] Failed connection without an adapter is handled without hanging; selected secondary profile and its CAN IDs are preserved.
- [x] Monitor and UDS actions remain unavailable while disconnected.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — repeated Connect retries on secondary ECU profile (2026-10-16)

- [x] Three connection attempts without an adapter are handled without hanging or crashing.
- [x] The secondary ECU profile and CAN IDs remain unchanged; Monitor and UDS actions remain unavailable while disconnected.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — secondary profile survives failed connection (2026-10-16)

- [x] Confirmed secondary ECU profile and CAN IDs persist after a failed connection attempt without a physical J2534 adapter and an application restart.
- [x] Confirmed no false connected state; Start Monitor, Read VIN, Read DTC, and Read DID stay unavailable while disconnected.
- [ ] Continue disconnected-state UI checks when switching ECU profiles.
- [ ] Verify CI for the current branch head and review release readiness before considering release 0.2.0.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — disconnected profile switching (2026-10-16)

- [x] Confirmed switching between primary and secondary ECU profiles while disconnected preserves each profile's CAN IDs and keeps Start Monitor, Read VIN, Read DTC, and Read DID unavailable.
- [x] Confirmed the application remains responsive and does not show a false active connection.
- [ ] Verify canceling/closing the profile selector leaves the current profile and disconnected control state unchanged.
- [ ] Verify CI for the current branch head and review release readiness before considering release 0.2.0.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — cancel profile selection (2026-10-16)

- [x] Confirmed canceling/closing the profile selector without choosing another profile preserves the selected ECU profile and CAN IDs.
- [x] Confirmed disconnected state remains accurate and Monitor/UDS controls stay unavailable.
- [ ] Verify canceling/discarding an unsaved CAN ID edit restores the persisted value.
- [ ] Verify CI for the current branch head and review release readiness before considering release 0.2.0.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — discard unsaved CAN ID edit (2026-10-16)

- [x] Confirmed discarding an unsaved CAN ID edit restores the persisted value; other settings remain intact and Monitor/UDS controls stay unavailable while disconnected.
- [ ] Verify a valid CAN ID edit persists across profile switching and can be restored to its original value.
- [ ] Verify CI for the current branch head and review release readiness before considering release 0.2.0.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — save and restore CAN ID (2026-10-16)

- [x] Confirmed a valid CAN ID edit persists across profile switching; the primary profile remains unchanged.
- [x] Confirmed restoring the original CAN ID and restarting the application preserves the restored value.
- [ ] Verify CI and required checks for the current branch head / PR #255.
- [ ] Review remaining release 0.2.0 gates after checking current CI results.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — export restored ECU profile (2026-10-16)

- [x] Confirmed exported secondary-profile JSON contains the expected profile name and restored request/response CAN IDs.
- [x] Confirmed no temporary test name/test CAN ID appears and backup files remain untouched.
- [ ] Import the exported JSON as a separate profile and verify its name and CAN IDs without overwriting the source profile.
- [ ] Verify CI for the current branch head and review remaining release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — import export as separate profile (2026-10-16)

- [x] Confirmed exported JSON imports as a separate profile without overwriting the source profile.
- [x] Confirmed imported profile name/CAN IDs match the JSON and source/imported settings remain independent after switching.
- [ ] Export the full profile catalog and verify expected profile count, names, and CAN IDs; keep the backup untouched.
- [ ] Verify CI for the current PR #255 head and review remaining release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — full profile catalog export (2026-10-16)

- [x] Confirmed the full catalog JSON has the expected profile count, names, and CAN IDs for primary, secondary, and imported profiles.
- [x] Confirmed no unexpected test values and no backup overwrite.
- [ ] Verify full-catalog import round trip without unintended duplicates or missing/changed profiles; preserve a separate backup.
- [ ] Verify CI for the current PR #255 head and review remaining release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — profile catalog survives restart (2026-10-16)

- [x] Confirmed after normal close/relaunch that the profile count, names, and CAN IDs match the exported catalog.
- [x] Confirmed no profiles were missing or duplicated and no false active connection was shown without an adapter.
- [ ] Verify CI and required checks for the latest PR #255 head, then review remaining software-only release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — repeat full-catalog import (2026-10-16)

- [x] Confirmed re-importing the exported profile catalog did not create unexpected duplicates or alter existing profile settings unexpectedly.
- [x] Confirmed profile names/CAN IDs remain correct and the separate backup is preserved.
- [ ] Final disconnected-state smoke test after catalog import: restart, confirm no active connection, and verify Monitor/UDS controls remain disabled.
- [ ] Verify terminal CI result for latest PR #255 head and review software-only release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — post-import disconnected-state smoke test (2026-10-16)

- [x] After catalog import, restarted the app and confirmed profiles and CAN IDs remain correct.
- [x] Without a physical J2534 adapter, confirmed no false active connection and Monitor / VIN / DTC / DID actions remain unavailable.
- [x] Confirmed the app stays responsive and closes normally.
- [ ] Verify CI for the latest PR #255 head and review remaining software-only release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## CI verification — PR #255 (run 2214, 2026-10-16)

- [x] CI run 2214 passed for commit `220dee77d3f59ba7b4dd3075c2f9b9ec237bd09b`: restore, build, tests, publish, and artifact upload succeeded.
- [ ] Re-check CI for the current head after these documentation updates.
- [ ] Inspect PR #255 review requirements and remaining software-only release 0.2.0 gates before making any release-readiness decision.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## CI verification — PR #255 latest documentation head (run 2218, 2026-10-16)

- [x] CI run 2218 passed for head `6ec1af6551842a21a338ceab0c5e7bbbf8ac665c`: restore, build, automated tests, publish, and CI artifact upload succeeded.
- [x] Confirmed PR #255 is documentation-only (`AI_WORKLOG.md`, `ROADMAP.md`) and remains open/unmerged.
- [ ] Verify UDS/Monitor controls transition to enabled only after a valid active J2534/CAN session; this requires a suitable adapter/session and is not validated by the disconnected-state smoke test.
- [ ] Complete the release-candidate checklist: review release notes and verify the exact release archive separately from CI artifacts.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## CI verification — PR #255 documentation head (run 2222, 2026-10-16)

- [x] CI run 2222 passed for head `8928a4dbef800ac19d32d6ec6b54cd8d439fa73a`: restore, build, automated tests, publish, and CI artifact upload succeeded.
- [ ] Re-check CI on the new documentation head after recording this result.
- [ ] Review release notes and verify the exact release archive separately from CI artifacts.
- [ ] Verify Monitor/UDS controls become enabled only after a valid active J2534/CAN session; this is not verifiable in the current no-adapter setup.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish release 0.2.0 without explicit user authorization.


## CI verification — PR #255 updated documentation head (run 2225, 2026-10-16)

- [x] CI run 2225 passed for head `be269fd3849588ce7fe777b9b997c2a9dd1eee88`.
- [ ] Re-check CI on the new documentation head after recording this result.
- [ ] Review release notes and verify the exact release archive separately from CI artifacts.
- [ ] Verify Monitor/UDS controls become enabled only after a valid active J2534/CAN session; this remains unverified without a suitable active session.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish release 0.2.0 without explicit user authorization.


## CI verification — PR #255 documentation head (2026-10-16)

- [x] CI run #2227 (push) passed for `f0239cb37d0c6b12eeeb9dbb3aa1cd372d51f8fe`.
- [x] CI run #2228 (pull request) passed for the same head, including build, automated tests, publish and CI asset steps.
- [ ] Re-check CI on the new documentation head after recording this result.
- [ ] Review release notes and verify the exact release archive separately from CI artifacts.
- [ ] Verify Monitor/UDS controls become enabled only after a valid active J2534/CAN session; remains unverified without a suitable active session.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish release 0.2.0 without explicit user authorization.


## CI verification — PR #255 (2026-10-16)

- [x] PR CI run #2232 passed for `577c85223f11cc692ae1da8ea8c09b10929dbd83`.
- [ ] Inspect CI workflow behavior for automatically published updater prereleases on documentation-only pushes.
- [ ] Verify the exact stable release archive separately from CI artifacts.
- [ ] Validate Monitor/UDS controls with an active J2534/CAN session when suitable hardware is available.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish release 0.2.0 without explicit user authorization.


## CI updater-prerelease publication guard (2026-10-16)

- [x] PR #256 workflow change passed CI run #2239.
- [x] Confirmed workflow still builds/tests PRs and branch pushes, while public updater ZIP/prerelease steps are restricted to pushes to `main`.
- [ ] Review and merge PR #256; the fix is not active on `main` before merge.
- [ ] After merge, confirm main CI and ensure no public `ci-*` prerelease is created for documentation/feature-branch pushes.
- [ ] Verify stable release archive and release notes before considering release `0.2.0`.
- Physical J2534 adapter and vehicle verification remain deferred. Do not merge PRs or publish release `0.2.0` without explicit user authorization.


### Rollback test CI follow-up (2026-10-16)
- [x] Diagnose CI #2243: one rollback runtime test failed while cleaning up a temp directory after PowerShell used a synthetic PID without a `Wait-Process` mock.
- [x] Open PR #257 to mock `Wait-Process` in the isolated test.
- [x] CI #2246 for PR #257 passed, including all tests and publish/artifact steps.
- [ ] Confirm CI on docs PR #255 after the latest work-log/roadmap update.
- [ ] Keep PRs unmerged until explicitly authorized; verify main-branch CI and stable release archive before v0.2.0. Physical adapter/vehicle testing remains deferred.


## Post-merge CI checkpoint — 2026-10-16

- [x] PR #256 merged; main CI #2251 passed, including the main-only updater prerelease packaging/publication path.
- [x] PR #257 merged; main CI #2252 passed on retry (attempt 2). Initial attempt had 475/476 tests pass and failed during temporary-directory cleanup in the rollback runtime test; monitor for recurrence rather than treating the first run as green.
- [x] PR #255 merged; main CI #2253 passed on commit `a47679bdb049fe6814751c319707e4119fa56c7c`.
- [x] Current stable release remains `v0.1.1`; `0.2.0` is not published.
- [ ] Review release notes and inspect the exact stable release archive separately from CI artifacts.
- [ ] Validate Monitor/UDS controls becoming enabled only after a valid active J2534/CAN session; not verifiable with the current no-adapter setup.
- Physical J2534 adapter and vehicle validation remain deferred to a future release. Do not publish `0.2.0` until release gates are reviewed and the user authorizes publication.


## Post-PR #258 merge checkpoint — 2026-10-16

- [x] PR #258 merged; `AI_WORKLOG.md` and `ROADMAP.md` now record the CI results for PRs #255–#257.
- [x] Main CI #2258 passed on merge commit `5d70fe220fc73852a417c45bce730a1181b9dfbe` ([run #2258](https://github.com/vorlocsev/dvinholm/actions/runs/38977361452)).
- [x] Reviewed v0.1.1 release notes and asset metadata: `Dvinholm-Windows-x86.zip`, 66,766,108 bytes; published SHA-256 `3822d13e5a137bd92bf12d35d2a5794053481ef28873d2a08657beb756f3a53a`.
- [ ] Exact stable archive is not independently inspected yet; direct download attempt was blocked by the available access path. Metadata alone is not a binary integrity verification.
- [ ] Confirm Monitor/UDS controls become enabled only after a valid active J2534/CAN session; current no-adapter setup cannot validate this state transition.
- Stable `0.2.0` is not published; do not publish without explicit user authorization. Physical J2534 adapter and vehicle checks remain deferred to a future release.


## Post-PR #259 main CI and UI gating review — 2026-10-17

- [x] PR #259 merged; main CI #2263 passed on merge commit `1935d56751453a5feeaa599f63302f6523717918` ([run #2263](https://github.com/vorlocsev/dvinholm/actions/runs/38978437672)).
- [x] Source review confirms UDS controls are initially disabled, enabled after a successful connection, and guarded by connection/CAN-ID validation when commands execute. Disconnect disables Monitor/UDS controls before session teardown and resets the state if disconnect fails.
- [ ] Confirm the UI state transition against a valid active J2534/CAN session when suitable hardware is available; this remains unverified in the current no-adapter setup.
- [ ] Independently download and inspect the exact v0.1.1 release archive; published metadata and SHA-256 are recorded but the archive bytes have not been inspected independently.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not publish stable `0.2.0` without explicit user authorization.


## Post-PR #260 main CI verification — 2026-10-17

- [x] PR [#260](https://github.com/vorlocsev/dvinholm/pull/260) merged as `23e12e0f188178d469112df44fc1138e26100cab`.
- [x] Main CI [#2268](https://github.com/vorlocsev/dvinholm/actions/runs/38979739897) completed successfully on that exact merge commit; the build job passed with no failed steps.
- [x] Current main checkpoint updated to the exact merge SHA.
- [ ] Independently inspect the exact v0.1.1 release archive. GitHub release metadata lists the published SHA-256, but the archive bytes have not yet been independently downloaded/inspected; metadata alone is not a binary verification.
- [ ] Active-session UI transition for Monitor/UDS remains unverified because no physical J2534 adapter is available. Disconnected-state gating and source guards have been reviewed; no live CAN/UDS session was tested.
- Stable v0.2.0 remains unpublished and requires explicit user authorization. Physical J2534 adapter and vehicle checks remain deferred to a future hardware-validation cycle.


## Post-PR #261 main CI verification — 2026-10-17

- [x] PR [#261](https://github.com/vorlocsev/dvinholm/pull/261) merged as `6620ad2bf9b4e3daa3b6e27b2347181e9f80b6c2`.
- [x] Main CI [#2273](https://github.com/vorlocsev/dvinholm/actions/runs/38980902038) completed successfully on that exact commit; build, test, publish, and artifact packaging passed.
- [x] Updated the roadmap header to the exact current `main` SHA.
- [x] Exact published v0.1.1 release ZIP independently audited by GitHub Actions; see the audit checkpoint below.
- [ ] Active-session Monitor/UDS enablement still cannot be manually verified without a J2534 adapter. Physical adapter/vehicle validation remains deferred.
- Stable v0.2.0 remains unpublished and requires explicit user authorization.


## Published v0.1.1 release ZIP audit — 2026-10-17

- [x] PR [#263](https://github.com/vorlocsev/dvinholm/pull/263) added a read-only GitHub Actions audit for the exact published asset [Dvinholm-Windows-x86.zip](https://github.com/vorlocsev/dvinholm/releases/download/v0.1.1/Dvinholm-Windows-x86.zip).
- [x] Audit run [#2282](https://github.com/vorlocsev/dvinholm/actions/runs/38982113636) passed: GitHub Actions downloaded the published ZIP, verified SHA-256 `3822d13e5a137bd92bf12d35d2a5794053481ef28873d2a08657beb756f3a53a`, validated ZIP integrity, and confirmed a Dvinholm executable is present. The archive file inventory is published in the Actions job summary.
- [x] PR #263 merged as `d113d6536cdd6d2c76bd5ecfbd4b1a858a955922`. The audit checks the actual release asset, not a newly generated CI artifact.
- [x] Post-merge main CI [#2286](https://github.com/vorlocsev/dvinholm/actions/runs/38983168717) passed on `d113d6536cdd6d2c76bd5ecfbd4b1a858a955922`, including the published ZIP audit and build/test/publish pipeline.
- [ ] Post-merge main CI for documentation checkpoint `1749b56d826523bf382f2d287e8f1a30a791286f` is still running: [#2291](https://github.com/vorlocsev/dvinholm/actions/runs/38983741022).
- [ ] Active-session Monitor/UDS control transition remains unverified because no physical J2534 adapter is available.
- Physical adapter/vehicle validation remains deferred. Stable v0.2.0 remains unpublished and must not be published without explicit user authorization.


## Window shutdown lifecycle fix — PR #267 (2026-10-17)

- [x] PR [#267](https://github.com/vorlocsev/dvinholm/pull/267) merged as `78c555df959129a7a743ac8c34156703ca584bce`.
- [x] Final PR-head CI [run #38986409454](https://github.com/vorlocsev/dvinholm/actions/runs/38986409454) passed: build/tests and the published v0.1.1 ZIP audit succeeded.
- [x] Post-merge main CI [run #38986840493](https://github.com/vorlocsev/dvinholm/actions/runs/38986840493) passed on the exact merge commit; build/tests and published release ZIP audit both succeeded.
- [x] Shutdown cleanup no longer synchronously waits on tracked operations or the UDS gate on the UI thread. Monitor stopping requests cancellation without synchronously waiting; connect completion is tracked as the actual connect task, and the continuation avoids session access after window closing.
- [ ] A dedicated WinUI window-close regression test/manual close stress test has not been run. CI confirms compilation and existing test suite only; do not claim direct runtime verification of the window-close behavior.
- Physical J2534 adapter and vehicle checks remain deferred. Stable v0.2.0 remains unpublished and requires explicit user authorization.


## Post-merge CI checkpoint — PR #268 (2026-10-17)

- [x] PR [#268](https://github.com/vorlocsev/dvinholm/pull/268) merged by squash as `08015d0f6f8808422fe3dc01c29c05c540445fe6` after its PR-head CI [#38987327051](https://github.com/vorlocsev/dvinholm/actions/runs/38987327051) passed build/tests and the exact published v0.1.1 ZIP audit.
- [x] Post-merge main CI [#38987746187](https://github.com/vorlocsev/dvinholm/actions/runs/38987746187) passed on the exact merge commit; build/tests and published v0.1.1 ZIP audit succeeded.
- [x] Roadmap and AI worklog now record PR #267's non-blocking shutdown cleanup, its CI verification, and the limitation that no dedicated WinUI close-handler regression test/manual close stress test was run.
- [ ] PR #228 (updater button state) remains open and must not be merged without explicit user approval. It is now behind current main and GitHub reports merge conflicts; rebase/conflict resolution and fresh CI would be required before considering it further.
- Physical J2534 adapter/vehicle validation remains deferred. Stable v0.2.0 remains unpublished and requires explicit user authorization.
