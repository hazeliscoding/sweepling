# Dogfooding log

How we'll know Sweepling works: what it found on the owner's machine, which groups were closed, and anything it offered that was still in use. That last count must stay zero. Sweepling keeps its own record of every close in `%APPDATA%\Sweepling\closed.log`; this log is the summary, written by hand.

Entries name the kind of process and the project, never command lines, tokens or private paths.

| Date | Leftovers offered | Groups closed | Offered but still in use | Notes |
|---|---|---|---|---|
| 2026-10-21 | 11 to 12 | 2 | 0 | First run of the M2 build. Only two fake dev servers, started for the test, were closed; the real leftovers were left for the owner to close. |
| 2026-10-22 | 22 | 22 | 0 | Installed the 0.0.1 build at midday, so it now starts at login; it updated itself to the signed 0.1.0 that afternoon. In the evening the owner closed all 21 groups from midday at once: 9 task output watchers, 9 watcher filters, a Next.js dev server on port 3001 (pdfcraft), the Angular dev server on port 4300 (portfolio) and a shell script. 25 processes closed; 2 had already exited. Still listed: an Angular dev server on port 4321 (portfolio), started this evening; the owner had forgotten it and isn't using it, and closed it later that night from Session still running. None reported in use. |
| 2026-10-23 | 0 | 0 | 0 | A quiet day: no closes, and nothing listed at the evening check, in either section. Sweepling has run since the update to 0.1.0 without a restart. None reported in use. |
| 2026-10-24 | 0 | 0 | 0 | Another quiet day: no closes, and nothing listed at the evening check, in either section. Still the same 0.1.0 process, up for two days. None reported in use. |
