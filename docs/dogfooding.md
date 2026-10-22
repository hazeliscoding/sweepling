# Dogfooding log

How we'll know Sweepling works: what it found on the owner's machine, which groups were closed, and anything it offered that was still in use. That last count must stay zero. Sweepling keeps its own record of every close in `%APPDATA%\Sweepling\closed.log`; this log is the summary, written by hand.

Entries name the kind of process and the project, never command lines, tokens or private paths.

| Date | Leftovers offered | Groups closed | Offered but still in use | Notes |
|---|---|---|---|---|
| 2026-10-21 | 11 to 12 | 2 | 0 | First run of the M2 build. Only two fake dev servers, started for the test, were closed; the real leftovers were left for the owner to close. |
