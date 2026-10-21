# Roadmap

Sweepling is a Windows tray app that notices processes your Claude Code sessions left running, and closes them only after you confirm. This file tracks the decisions already made, what gets built in what order, and what is out of scope.

## Architecture characteristics

What the design protects, in order:

1. **Safety.** Never offer to close something still in use. Re-check the start time right before closing, because Windows reuses process IDs.
2. **Low cost while idle.** Sweepling must cost less than the leftovers it finds.
3. **Rules that are easy to change.** Agent tools change how they spawn processes often.

Given up for them: features that need administrator rights, cross-platform support, and a rich main window at first.

## Decisions (2026-10-21)

- **Name: Sweepling.** Free and open source (Apache-2.0, like Gil Sweep, keytriage, tracewash and chrontide), in the public repo `hazeliscoding/sweepling`. It gave up Strayhound, which sounded harsher than the tool is.
- **A tray app, not a command you run.** A dev server held a port for 35 hours without being noticed, so a tool you have to remember to run would have missed it. This gives up the simplicity of a one-shot CLI; M1's `--scan` flag stays as a developer tool.
- **.NET 10 + Avalonia 12 with NativeAOT**, Win32 calls through CsWin32, Velopack for installs and updates. Recent desktop apps are Avalonia, and Prompuff idles at about 7.7 MB. It is a single process with no WebView2 helpers, and it reuses Gil Sweep's Velopack, update and signing setup. This gives up Tauri + Angular + Rust (keytriage's stack), the first recommendation.
- **Codex in version one.** *Superseded the same day by the next decision.*
- **Claude Code CLI only in version one.** There were no Codex leftovers to test against, Codex runs inside the ChatGPT desktop app here, and its sandbox accounts may need admin rights to see. This gives up Codex users at launch; Cursor, other agents and the Claude desktop app come later too.
- **Strays of a session that's still running are shown** in their own section, labelled "Session still running", never ticked by default. With ended sessions only, about 4 of the 22 leftovers the first scan found would have been offered. This gives up the simplest possible window.
- **Updates apply on their own.** A utility like this should take updates as soon as they're out. Velopack checks GitHub Releases at start and every few hours, downloads in the background, and restarts into the update whenever the review window isn't open. A setting turns it off. The update check is the only network call. This gives up asking before each update.
- **Idle aim and ceiling.** Aim at Prompuff's idle memory, measured the same way side by side. If tray-only idle measures more than twice Prompuff's, or memory doesn't come back under that after the review window closes, stop and rethink before M1, for example a plain Win32 tray icon with Avalonia loaded only for the window. This gives up a free hand with UI dependencies.
- **Defaults:** signed through the existing Azure Artifact Signing setup; starts at login, with a setting to turn that off; winget later.
- **Watching.** Every few seconds Sweepling takes a Toolhelp32 snapshot and keeps an in-memory family tree of each Claude Code session.
  - Agent roots are matched by path and command line, not by name: `%USERPROFILE%\.local\bin\claude.exe`, or `node.exe` running `@anthropic-ai/claude-code`. Other apps also have `claude` in their process names.
  - A parent counts as gone if its PID doesn't exist, or belongs to a process that started after the child. Windows doesn't reparent orphans, so a stale parent PID is the only trace.
  - A stray Sweepling never saw born falls back to command-line rules: Claude temp paths (`%TEMP%\claude\<project>\<session id>\...`), or `npm`, `ng serve`, `next dev` or `vite` started from Git Bash.
  - It never picks targets by CPU use: cc-reaper's history shows that rule killing people's background builds.
- **Ended or running.** A session has ended when its agent root is gone. When Sweepling can't tell, it treats the session as running. A wrong "running" only delays an offer; a wrong "ended" offers a live session's processes.
- **Offered only when all of these hold:**
  - the parent has been gone for a grace period;
  - it's tied to a Claude Code session;
  - no established TCP connection exists on it or anywhere in its group;
  - it's not protected. The protect list starts with Orca's daemon-host and crash handler, MSBuild nodes, the .NET build servers and anything under `%WINDIR%`. Protected processes are never part of a group.
  - Only the current user's processes in the current Windows session count, and it never needs admin rights.
- **Review window and closing.** The tray icon counts ended-session leftovers and notifies when the count rises. The window has two sections, Leftovers and Session still running. Each group shows what it is in words, its project folder, age, port and memory. Closing takes the whole group, and each process's PID and start time are re-checked right before it closes.
- **Rules as data.** Agent roots, command-line rules, the protect list and grace periods live in a shipped rules file, and a user file can add to it. New agent behaviour should need a rules change, not a release of new code.
- **Privacy.** No telemetry. A local log of what was closed, with command lines shortened and token-like strings masked. This gives up any usage data beyond GitHub's download counts.
- **Brand: option 1A, "Pebble",** a round mint critter holding a tiny broom, picked from four takes on the claude.ai design canvas "Sweepling logo". It has the cleanest silhouette at 16 px in the tray, and a count badge sits clear of its face. The wordmark is Fredoka SemiBold, outlined. This gives up the fluffier Puff and the story of the mid-sweep option. Assets and the review window's starting tokens are in `docs/brand/`.
- **The review window uses the system font,** Segoe UI Variable, with Cascadia Mono for command lines. Bundling fonts would grow the install for no gain in a utility window. This gives up the mascot's rounded type inside the app.
- **Left to the builder,** to settle with tests and record here: the snapshot interval and grace periods; the rules file's format and versioning, which must work under NativeAOT; the review window layout and the notification mechanism, with NativeAOT-compatible dependencies only; whether the family tree persists across restarts; how to tell that an untraced stray's session has ended; close order, and whether to try a gentle close first.

## M0: Placeholder

- [ ] .NET 10 solution with central package versions and an Avalonia 12 tray-only app: an icon and Quit, no window.
- [ ] NativeAOT publish, with trim and AOT warnings treated as errors.
- [ ] CI on `windows-latest`: build, test and NativeAOT publish.
- [ ] Secret scanning: the gitleaks release binary, checked against its published checksum, run with `--log-opts="--full-history HEAD"`.
- [ ] Measure tray-only idle memory next to Prompuff's, with the same counter for both, and record the numbers and the counter here.

**Done when:** CI passes, a throwaway pull request with a fake token fails the gitleaks check, and tray-only idle memory is measured next to Prompuff's and recorded, within the ceiling.

## M1: Find leftovers

- [ ] Process snapshot through Toolhelp32 and the family tree of each session.
- [ ] Ended-or-running check for each session.
- [ ] Established TCP connections for every process in a group.
- [ ] The rules file, shipped and user, and the classifier.
- [ ] A `--scan` flag that prints what it found as JSON and closes nothing.

**Done when:** tests with synthetic process tables cover every rule: orphan, PID reuse, protect list, connections, grace period, ended vs running, and unknown treated as running. A real-process test, where a parent starts a child that listens on a port and then exits, must flag the child, and must not flag it while it has a connection.

## M2: Review and close

- [ ] Tray count and a notification when it rises.
- [ ] Review window with the Leftovers and Session still running sections.
- [ ] Closing a group, with the PID and start-time re-check right before each close.
- [ ] The local closed log, with command lines masked.

**Done when:** an integration test closes a fake orphaned dev server and its port frees, a PID-reuse test refuses to close, and a week of the owner's dogfooding log exists.

## M3: Release

- [ ] Signed Velopack installer with automatic updates, and start at login.
- [ ] README: install steps, a screenshot, and the comparison with today's alternatives checked again.
- [ ] `CONTRIBUTING.md` and `SECURITY.md`.
- [ ] The OSS launch gates:
  - [ ] the core journey works end to end: notice, review, close;
  - [ ] a fresh install works, and supported Windows versions are documented;
  - [ ] the README quick start is accurate;
  - [ ] CI is green and there are no known critical bugs;
  - [ ] the release artifact installs, updates itself and uninstalls cleanly;
  - [ ] error messages are understandable;
  - [ ] the issue tracker is ready for reports.

**Done when:** an installed release updates itself to a later test release without leaving a Sweepling process behind, Setup and `Sweepling.exe` carry a valid signature, and every gate above is ticked.

## How we'll know it works

A dogfooding log in `docs/dogfooding.md`, kept from M2 on: leftovers found, groups closed, and anything offered that was still in use. That last count must stay zero. After release, public signals: issues, pull requests and downloads.

## Later

- Codex, once a Codex leftover shows up in the dogfooding log.
- Cursor and other agents.
- The Claude desktop app.
- A winget package.

## Not planned

- macOS and Linux: cc-reaper covers them.
- Cleaning disks, caches, temp folders or worktrees.
- Closing anything without confirmation, or anything still in use, especially an agent session or Orca's daemon.
- Idling heavier than the leftovers it finds, or leaving its own processes behind, including across an update restart.
- Telemetry, accounts or any network call besides the update check.
