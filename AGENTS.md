# AGENTS.md

These are the working rules for agents in this repo. Sweepling (Apache-2.0) is a Windows tray app that notices processes your Claude Code sessions left running, and closes them only after you confirm. It is .NET 10 + Avalonia 12, published with NativeAOT, a sibling of [Gil Sweep](https://github.com/hazeliscoding/gil-sweep) and [Prompuff](https://github.com/hazeliscoding/prompuff).

## Sources of truth

- `README.md`: what Sweepling does and its promise.
- `ROADMAP.md`: the architecture characteristics, decisions already made, the milestones, and what is out of scope. Work from the next unchecked item, tick it off when done, and record new decisions there, dated, with why and what they gave up. When a decision changes, mark the old one superseded instead of deleting it. Respect existing decisions unless the owner reopens them, and don't build past the current milestone without asking.
- The rules file (from M1): agent roots, command-line rules, the protect list and grace periods. A change in how an agent spawns processes belongs there, not in code.

## Hard rules

Each rule has its reason, so it can be applied to cases it didn't foresee. When one becomes code, give it a test that fails when the rule is broken.

- **Never close without confirmation.** A wrong close ends someone's work, and that costs more than any leftover. No auto-close mode, setting or flag, and `--scan` only reports.
- **Re-check PID and start time right before closing.** Windows reuses process IDs, so the PID you saw seconds ago can belong to a new process. If either has changed, don't close it and drop it from the group.
- **Unknown means running.** When Sweepling can't tell whether a session has ended, it treats the session as running. A wrong "running" only delays an offer; a wrong "ended" offers a live session's processes.
- **Protected processes are never in a group.** Orca's daemon-host detaches by design and hosts the live `claude.exe` sessions, so a group that reached it would end every session. The protect list wins over every other rule, including a user's additions.
- **Never offer a group with a windowed app.** An app an agent opened, like a desktop pet or Docker Desktop, is in use even with no TCP connection; a real scan offered both before this rule. An image whose header can't be read counts as windowed.
- **Match agent roots by path and command line, never by name alone.** Other apps carry `claude` in their process names.
- **Don't rely on the environment for Git Bash processes.** They hand Windows only a handful of variables, never `CLAUDE_PID`, so their command lines must tie them. Native programs started from Git Bash do carry the whole environment.
- **Treat what another process's memory says as untrusted.** Sizes and pointers read from its PEB are its to forge, so every read is bounded by Sweepling's own buffer.
- **Never pick targets by CPU or memory use.** A busy process is often a build someone is waiting for; cc-reaper's history shows that rule killing background builds.
- **Only the current user's processes in the current Windows session, and never admin rights.** Sweepling must not reach other people's work, and it must run with nothing more than a normal sign-in.
- **NativeAOT-compatible dependencies only.** Low idle cost depends on a single NativeAOT process. Use source-generated System.Text.Json and compiled bindings; a reflection-based library breaks the publish or fails at run time after trimming. Trim and AOT warnings are errors.
- **Stay under the idle ceiling.** Tray-only idle memory must stay within twice Prompuff's, measured the same way, and must come back under it after the review window closes. Sweepling must cost less than the leftovers it finds. A change that grows idle memory records the new number in `ROADMAP.md`.
- **Leave nothing behind.** Sweepling's own processes end when it quits, including across an update restart.
- **No telemetry. The update check is the only network call.** Sweepling sees every command line on the machine, so nothing about them leaves it.
- **Mask command lines in logs.** Command lines carry tokens, keys and private paths. Shorten them and mask token-like strings before anything is written, and never put a real token in a fixture or test: build token-shaped values at run time.

## Glossary

One meaning per term. Code, docs, UI copy and agents use these words and no synonyms.

- **Session:** one run of the Claude Code CLI. It has ended when its agent root is gone.
- **Agent root:** the process that is the session itself: `%USERPROFILE%\.local\bin\claude.exe`, or `node.exe` running `@anthropic-ai/claude-code`.
- **Stray:** a process whose parent is gone, meaning the parent's PID doesn't exist or belongs to a process that started after the stray, and that is tied to a session: by the family tree Sweepling watched, by `CLAUDE_PID` in its environment, or by a command-line rule, in that order.
- **Group:** the strays that shared one dead parent and started within 10 s of each other, plus their descendants. Sweepling shows and closes a group as one. Protected processes are never part of one, and neither is a live agent root or anything under it.
- **Leftover:** a stray of an ended session whose group passes every offer check: its parent has been gone for the grace period, nothing in the group has an established TCP connection or is a windowed app, and something in it isn't protected. Only leftovers are counted on the tray icon. Strays that pass the same checks while their session is still running are listed under "Session still running".
- **Protect list:** processes Sweepling never offers or closes: Orca's daemon-host and crash handler, MSBuild nodes, the .NET build servers, anything under `%WINDIR%`, and what a user's rules file adds.

## Dependencies

- The stack is decided: Avalonia, CommunityToolkit.Mvvm, Velopack and CsWin32. Versions live in `Directory.Packages.props`.
- An agent may add a special-purpose library on its own, such as a parser, when it is NativeAOT-compatible. Give the reason in the commit.
- A general-purpose helper only after checking that nothing in the project, the .NET base library or CsWin32 already does the job.
- Never a framework, or anything that adds a process, a WebView or a network call, without asking.

## Brand

- The assets are in `docs/brand/`. `-dark` files are for dark backgrounds.
- The wordmark is outlined Fredoka SemiBold. Use the SVGs; don't re-typeset it with a web font. `scripts/make_brand.py` regenerates them.
- UI colors come from the tokens in `docs/brand/README.md`. Every text pair there passes WCAG AA; a new text or focus color on a surface needs its pair checked before it ships. Mascot colors are never text colors.

## Commands

- `dotnet build Sweepling.slnx` and `dotnet test --solution Sweepling.slnx` from the repo root.
- `dotnet format Sweepling.slnx --verify-no-changes` is the CI style check. Run `dotnet format Sweepling.slnx` before committing.
- `dotnet publish src/Sweepling -c Release -r win-x64 -o artifacts/publish` makes the NativeAOT build. It needs Visual Studio's C++ workload. If linking fails with "'vswhere.exe' is not recognized", put `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on PATH: VS 18's `vcvarsall.bat` calls `vswhere.exe` by bare name, and the error text lands in the linker path.
- Idle memory is measured on the NativeAOT build with no window open, next to Prompuff closed to the tray, both read at the same moment: working set, private bytes, and private working set (Task Manager's Memory column), from `Get-Process` and `Win32_PerfFormattedData_PerfProc_Process`. Run Prompuff on a scratch `PROMPUFF_DATA_DIR` whose `settings.json` turns `keepRunningInTray` on, so the owner's library and settings are never touched.
- `artifacts\publish\Sweepling.exe --scan > scan.json` prints the groups Sweepling would offer right now, as JSON with command lines masked, ignoring the grace period, and closes nothing. Redirect or pipe it: a WinExe doesn't make the shell wait.
- The real-process tests in `tests/Sweepling.Tests/Windows` start their own processes and stop only those, checked by PID and start time. Their children redirect output and time out on their own, because a child holding the test runner's output pipe hangs `dotnet test`.
- To check the tray by hand, use UI Automation: a new icon sits in the hidden-icons overflow (window class `TopLevelWindowForOverflowXamlIsland`), as a button named "Sweepling". Its menu opens as a `TrayPopupRoot` window in Sweepling's process. Find each element by name right before clicking it, and restore the cursor afterwards.
- `dotnet run scripts/make-icons.cs` rebuilds `docs/brand/sweepling.ico` from `docs/brand/tray.svg`; `scripts/make_brand.py` regenerates the SVGs (see its header).

## Working style

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/), atomic, with a scope when it helps (`feat(classifier): …`).
- **No AI attribution** in commits or PRs: no `Co-Authored-By` trailers, no "Generated with" lines, no session links.
- **Tests first for the classifier.** Rules are tested against synthetic process tables. Real-process tests start their own parent and child processes, and never touch anything else on the machine.
- **Checks:** automate acceptance checks instead of handing manual steps to the owner. A check that tests for an absence needs a positive control: prove a CI guardrail with a throwaway pull request that must fail.
- **Validation:** evidence comes from the dogfooding log and public signals (issues, PRs, downloads). Don't plan interviews or outreach.
- **Docs:** short. Prefer editing `ROADMAP.md` over new planning documents.
- **Code comments:** explain why, not what. No boilerplate XML docs, no commented-out code.
- **UI copy:** calm, short, declarative. No exclamation marks or emoji. Say what a process is in words ("Next.js dev server"), not just its executable. Numbers carry units.
