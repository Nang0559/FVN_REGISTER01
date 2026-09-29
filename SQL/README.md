# FVN_REGISTER SQL deployment

## Canonical deployment

`00_Deploy_All.sql` is the canonical full deployment chain. It owns the execution order for the repository SQL migrations.

For an existing installation, use the appropriate baseline runner only when its starting schema is already present:

- `00_Deploy_From16.sql` — continue from Equipment/SQL 16.
- `00_Deploy_From48.sql` — continue from SQL 48.
- `00_Deploy_Run.sql` — full runner using the configured local SQL root.

All runners keep the Endpoint Governance sequence consistent:

`53 -> 54 -> 54 -> 55 -> 55 -> 56 -> 57 -> 58 -> 59 -> 60`

`60_EndpointAgentDataHardening.sql` is therefore included in every supported deployment runner.

## Legacy FunctionKey patches — manual only

The following scripts are **not** part of the normal deployment chain:

- `06A_FunctionKeyCompatibility.sql`
- `06B_FunctionKeyBackfill.sql`

They exist only to repair databases that were previously initialized with an older `06_Seed.sql` implementation that did not populate/require `FunctionKey` consistently.

Run them manually, in order, only when the target database is an affected legacy installation:

`06A_FunctionKeyCompatibility.sql` -> `06B_FunctionKeyBackfill.sql`

Do **not** add them to the normal `00_Deploy_*.sql` chain. `06_Seed.sql` is intentionally excluded from normal deployment, so these compatibility patches must remain an explicit legacy-migration step.

## SSMS

Enable **Query -> SQLCMD Mode** before running any `00_Deploy_*.sql` file containing `:r` directives.
