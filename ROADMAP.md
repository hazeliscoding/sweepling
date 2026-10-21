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
  - A stray Sweepling never saw born falls back to command-line rules: Claude temp paths (`%TEMP%\claude\<project>\<session id>\...`), or `npm`, `ng serve`, `next dev` or `vite` started from Git Bash. *Refined in M1: `CLAUDE_PID` in the stray's environment comes before these rules. See "Strays are tied to a session three ways" below.*
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
- **Idle memory is compared on three counters, and the ceiling holds on each:** working set, private bytes, and private working set, which is Task Manager's Memory column. Prompuff's 7.7 MB wasn't reproduced: closed to the tray, it measured 44.1 MB private working set after 60 s, and nobody recorded which counter the 7.7 MB came from. Reading all three for both apps at the same moment doesn't depend on that number. Prompuff's desktop build is self-contained without NativeAOT, so it sets a cost to beat, not a like-for-like baseline. This gives up quoting one idle number.
- **The Fluent theme loads from M0.** Avalonia draws the tray menu itself, and with no theme its items have no template, so the menu opened empty. Fluent is the theme Gil Sweep and Prompuff use, and idle memory moved by under 3 MB on every counter. This gives up a 10 MB exe: it is 17 MB now.
- **Strays are tied to a session three ways, strongest first.** First the family tree Sweepling watched form. Then `CLAUDE_PID`, which Claude Code puts in the environment of everything it starts, next to `CLAUDE_CODE_SESSION_ID`. Then the command-line rules. A traced stray's session has ended when its agent root is gone. An environment-tied stray's has ended when its `CLAUDE_PID` is gone or now belongs to a newer process. A rule-tied stray's session is unknown, so it counts as running unless every live agent root started after the stray. This settles how to tell that an untraced stray's session has ended. It gives up precision for rule-tied strays, which stay under Session still running while any older session lives.
- **Git Bash processes are tied by command line only.** They hand Windows a 7-variable environment with no `CLAUDE_*` variables, while native programs started from Git Bash, like `node`, carry the whole environment. So a shipped rule ties Git's `grep --line-buffered`, the other half of a `tail -f | grep` watcher, which has no other marker. This gives up any Git Bash stray that no rule matches.
- **A group is the strays that shared one dead parent and started within 10 s of each other, plus their descendants.** Protected processes are left out one by one, so a dev server under `cmd.exe` still closes with its tree, and `cmd.exe` ends once its child does. A live agent root is left out with everything under it. This gives up closing a protected process inside a leftover's tree.
- **A group with a windowed app is never offered.** The executable's PE header tells a windowed app from a console program, and an image that can't be read counts as windowed. The first scan of a real machine offered a desktop pet and Docker Desktop: both were started from Claude Code sessions, and both were in use with no TCP connection. Protected processes such as `conhost.exe` don't count. This gives up headless browsers left by Playwright or Puppeteer, whose images are windowed.
- **Timing and memory:** the M2 loop takes a snapshot every 5 s, and the grace period is 60 s, as in cc-reaper's proc-janitor. The family tree lives in memory only: after a restart, `CLAUDE_PID` and the rules cover what was learned. This gives up tracing strays born before Sweepling started.
- **The rules file** is JSON with comments and `"version": 1`, read with source-generated System.Text.Json so it works under NativeAOT. An unknown version or key, or a rule with no pattern, is refused. Patterns are case-insensitive regular expressions, and `%NAME%` expands to that environment variable's path, matching either slash. The user's file is `%APPDATA%\Sweepling\rules.json`: its lists add to the shipped ones and its settings win. This gives up removing a shipped rule.
- **`--scan` ignores the grace period** and prints every group as JSON, with command lines masked, so it works as a one-off check. A WinExe has no console of its own, so it attaches to the one it was started from unless output is redirected.
- **Reading another process's environment is bounded by Sweepling's own buffer.** The size comes from the other process's memory, which that process controls. Found by an automated security review of the M1 code.
- **Left to the builder,** to settle with tests and record here: the review window layout and the notification mechanism, with NativeAOT-compatible dependencies only; close order, and whether to try a gentle close first. The snapshot interval, grace period, rules file format, family tree persistence and untraced sessions were settled in M1, above.

## M0: Placeholder

- [x] .NET 10 solution with central package versions and an Avalonia 12 tray-only app: an icon and Quit, no window.
- [x] NativeAOT publish, with trim and AOT warnings treated as errors.
- [x] CI on `windows-latest`: build, test and NativeAOT publish.
- [x] Secret scanning: the gitleaks release binary, checked against its published checksum, run with `--log-opts="--full-history HEAD"`.
- [x] A test that fails when Sweepling's own code references a networking assembly, with a positive control.
- [x] Measure tray-only idle memory next to Prompuff's, with the same counters for both, and record the numbers and the counters here.

**Done when:** CI passes, a throwaway pull request with a fake token fails the gitleaks check, and tray-only idle memory is measured next to Prompuff's and recorded, within the ceiling.

**Done on 2026-10-21:**

- CI passed on `main`: [run 39640909172](https://github.com/hazeliscoding/sweepling/actions/runs/39640909172).
- Throwaway pull request #1 planted a fake GitHub token, and its secrets job failed on the history scan: [run 39641685650](https://github.com/hazeliscoding/sweepling/actions/runs/39641685650). It was closed without merging.
- Trim and AOT warnings fail the build: a `Type.GetType(string)` call added to `Program.cs` stopped it with `error IL2057`.
- Quit, tested through the real Windows 11 tray with UI Automation, which found the icon by name before each click. The icon sits in the hidden-icons overflow, named "Sweepling". Right-clicking it opens a menu holding exactly "Quit", and Quit ended the process within the wait, left no child process, and removed the icon. The first try opened an empty menu: with no theme, Avalonia's tray menu items have no template. Adding the Fluent theme fixed it.
- The tray icon's broom handle is brown, so it shows on a dark taskbar as well as a light one.
- Idle memory, both in the tray with no window open, read at the same moment. Prompuff is the installed 1.1.1 release on a scratch data folder with `keepRunningInTray` on, 60 s after its window closed. Sweepling is the NativeAOT build with the Fluent theme, 74 s after it started. Before the theme, Sweepling read 38.9, 63.5 and 7.4 MB.

  | | Working set | Private bytes | Private working set |
  |---|---|---|---|
  | Prompuff 1.1.1 | 149.3 MB | 131.1 MB | 46.3 MB |
  | Sweepling | 40.5 MB | 65.9 MB | 8.0 MB |
  | Sweepling as a share of Prompuff | 27% | 50% | 17% |

  Within the ceiling, twice Prompuff's, on every counter.

## M1: Find leftovers

- [x] Process snapshot through Toolhelp32 and the family tree of each session.
- [x] Ended-or-running check for each session.
- [x] Established TCP connections for every process in a group.
- [x] The rules file, shipped and user, and the classifier.
- [x] A `--scan` flag that prints what it found as JSON and closes nothing.

**Done when:** tests with synthetic process tables cover every rule: orphan, PID reuse, protect list, connections, grace period, ended vs running, and unknown treated as running. A real-process test, where a parent starts a child that listens on a port and then exits, must flag the child, and must not flag it while it has a connection.

**Done on 2026-10-21:**

- Synthetic process tables cover every rule in the Done line: orphan, PID reuse of a parent and of an agent, the protect list, connections, the grace period, ended and running, and unknown treated as running. They also cover the environment and command-line ties, strays that shared a parent, a live session under a stray, and windowed apps. Each rule's test was proven by breaking that rule in `Tracker.cs` or `rules.json` and watching the test fail.
- The real-process test passes locally and on CI ([run 39653269155](https://github.com/hazeliscoding/sweepling/actions/runs/39653269155)). A PowerShell parent stands in for an agent: it starts a child that listens on a free port, then exits. The child is offered as a leftover of an ended, traced session, with its port and the `CLAUDE_PID` read from its environment. Once a client connects, it isn't offered.
- `--scan` on the owner's machine, with 647 processes in the session, found 22 groups. Under Leftovers were `next dev` on port 3001, `npm start` and `ng serve` on port 4300, a scratchpad `sh` script, and 8 watcher halves. Under Session still running were 10 watcher halves, which can't be told apart from a live session, and the `vctip.exe` a NativeAOT publish had just left. Every leftover the planning scan found was there, except one `tail` that had exited since. A scan took 160–300 ms, including start-up.

## M2: Review and close

- [ ] A snapshot every 5 s that stays cheap: cache each process's image, command line and windowed flag by PID and start time, and read only new processes. A full snapshot took 160–300 ms with 647 processes.
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
- Tie Git Bash strays to their session through the session ID in Claude Code's temp paths, matched against the `CLAUDE_CODE_SESSION_ID` of a live session's native children, so ended-session watchers move to Leftovers.
- Headless browsers left by Playwright or Puppeteer, which the windowed-app rule keeps out today.
- Cursor and other agents.
- The Claude desktop app.
- A winget package.

## Not planned

- macOS and Linux: cc-reaper covers them.
- Cleaning disks, caches, temp folders or worktrees.
- Closing anything without confirmation, or anything still in use, especially an agent session or Orca's daemon.
- Idling heavier than the leftovers it finds, or leaving its own processes behind, including across an update restart.
- Telemetry, accounts or any network call besides the update check.
