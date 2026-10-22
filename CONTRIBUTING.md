# Contributing to Sweepling

Thanks for helping. Sweepling notices the processes Claude Code sessions left running on Windows and closes them only after you confirm. It stays small on purpose, so this page is short.

## Where to start

- [README.md](README.md) says what Sweepling does and what it promises. [ROADMAP.md](ROADMAP.md) holds the decisions already made, the milestones, and what's planned for later or not at all. Check it before proposing a feature: decisions stand unless the owner reopens them.
- [AGENTS.md](AGENTS.md) lists the rules every change keeps, each with its reason, and the words the code and docs use: session, agent root, stray, group, leftover and protect list mean one thing each.
- The most useful issue describes a leftover you've seen on Windows: what started it, its command line with anything private removed, and how you noticed.
- For anything bigger than a fix, open an issue first, so nobody builds something that's already decided against.

## Build, run and test

You need Windows 10 or 11 on x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/download). `global.json` asks for 10.0.300 or a later feature band.

```powershell
dotnet build Sweepling.slnx
dotnet test --solution Sweepling.slnx
dotnet format Sweepling.slnx --verify-no-changes
```

Some tests start real processes, a fake agent and its children, and stop only those, checked by PID and start time. Settings tests each write one scratch key, `HKCU\Software\Sweepling.Tests.<id>`, and delete it. Nothing else on your machine is touched.

To see what Sweepling would offer on your machine without closing anything, publish it and run `--scan`:

```powershell
dotnet publish src/Sweepling -c Release -r win-x64 -o artifacts/publish
artifacts\publish\Sweepling.exe --scan > scan.json
```

The publish is NativeAOT and needs Visual Studio's C++ workload. If linking fails with "'vswhere.exe' is not recognized", put `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on `PATH`.

**Running the tray app reviews your real processes.** The review window lists your own leftovers, ticked. When you try Close by hand, start a process of your own to close, untick every other group, and check the button says "Close 1 group" before you press it.

## Project layout

| Folder | What it holds |
|---|---|
| `src/Sweepling/Leftovers` | The classifier: rules, family trees, groups, what each one is in words, and the closed log. No Windows calls. |
| `src/Sweepling/Windows` | Win32 through CsWin32: the process snapshot, each process's environment and folder, TCP connections, closing, and the tray icon. |
| `src/Sweepling/Review` | The review window and its view model. |
| `src/Sweepling/Install` | Velopack, start at login, settings and updates. |
| `src/Sweepling/Leftovers/rules.json` | Agent roots, command-line rules, the protect list and the grace period. |
| `tests/Sweepling.Tests` | xUnit v3 tests, in the same folders. |
| `tools/Sweepling.Screenshots` | Renders the README's screenshots from made-up leftovers. |
| `scripts/` | The brand and icon generators, and `tray.ps1`, which drives the running app for hand tests. |

Package versions live in `Directory.Packages.props`.

## Hard rules

These come from [AGENTS.md](AGENTS.md), which gives the reason for each, and reviews hold every change to them.

- Nothing closes without the person pressing Close. No auto-close mode, setting or flag.
- Strays of a session that's still running are never ticked by default, and when Sweepling can't tell whether a session has ended, it counts as running.
- The PID and start time are checked again right before each close.
- Protected processes are never in a group, and a group with a windowed app is never offered.
- Targets are never picked by CPU or memory use.
- Only the current user's processes in the current Windows session, and never administrator rights.
- Memory read from another process is untrusted: every read is bounded by Sweepling's own buffer.
- Dependencies must work under NativeAOT; trim and AOT warnings are errors. Tray-only idle memory stays within the ceiling in ROADMAP.md.
- No telemetry. The update check is the only network call.
- Command lines are shortened and masked before anything is written. Never put a real token in a fixture or test: build token-shaped values at run time.
- A change in how an agent starts processes belongs in `rules.json`, not in code. Tests come first for the classifier.

## UI and copy

- Colors come from the tokens in [docs/brand/README.md](docs/brand/README.md). Every text pair there passes WCAG AA; check the pair of any new text or focus color.
- Copy is calm, short and declarative, with no exclamation marks or emoji. Say what a process is in words ("Next.js dev server"), not just its executable, and give numbers units.
- Primrose's art comes from one written spec; don't edit or redraw it here.
- After a change to the review window, re-render the README's screenshots with `dotnet run --project tools/Sweepling.Screenshots -- docs/screenshots` and attach them to the pull request.

## Code style

Match the code around you. `.editorconfig` sets the style, `.gitattributes` keeps LF line endings, and CI runs `dotnet format --verify-no-changes`. Comments explain why, not what: no boilerplate XML docs and no commented-out code.

## Commits and pull requests

- Use [Conventional Commits](https://www.conventionalcommits.org/), with a scope when it helps, such as `fix(classifier): …`. Keep each commit to one logical change.
- Commit messages read like a person wrote them: no `Co-Authored-By` trailers or "Generated with" lines for AI tools.
- Work on a branch and open a pull request into `main`. [CI](.github/workflows/ci.yml) builds, tests, checks formatting, publishes with NativeAOT and scans for secrets, and it has to pass.
- Bugs and requests go in [GitHub Issues](https://github.com/hazeliscoding/sweepling/issues). Security problems don't: see [SECURITY.md](SECURITY.md).

## How releases are cut

The owner cuts releases. A `v*` tag starts [the release workflow](.github/workflows/release.yml): it takes the version from the tag, signs the build and Setup, and opens a draft GitHub Release with the update packages and `SHA256SUMS`, which the owner checks and publishes. Installed copies pick the release up through their update check.
