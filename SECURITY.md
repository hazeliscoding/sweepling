# Security

Report a vulnerability privately with **Report a vulnerability** on the [Security tab](https://github.com/hazeliscoding/sweepling/security), not in a public issue. Fixes go into the latest release.

Sweepling has no accounts or servers. It runs on your PC as you, without administrator rights, and sees only your own processes in your Windows session. It reads their command lines, folders and environment to tell which Claude Code session started them, and ends a process only after you press Close in its review window.

What it keeps, all on your PC:

- `%APPDATA%\Sweepling\closed.log`, a record of what it closed, with command lines shortened and token-like strings masked
- `%APPDATA%\Sweepling\rules.json`, if you add rules of your own
- its update setting under `HKCU\Software\Sweepling`, and its value under your Run key when it starts at login

Its only network call is the update check against this repository's GitHub Releases, which **Update automatically** in the tray menu turns off. There is no telemetry.

Reports we especially want: anything that makes Sweepling end a process you didn't confirm, offer another user's process or one that's still in use, write a token or private path to its log, or follow sizes and pointers it reads from another process's memory beyond its own buffer.

Releases are code-signed, and each one lists SHA-256 checksums in `SHA256SUMS`.
