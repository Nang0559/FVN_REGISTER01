# FVN_REGISTER Playwright smoke tests

These tests are kept separate from the .NET application and are intended to run against a local/development instance. The initial smoke test only opens the application root and checks that the page responds; it does not sign in, submit forms, or change business data.

## Requirements

- Node.js LTS and npm
- FVN_REGISTER running locally, by default at `https://localhost:7264`

## Run on Windows (PowerShell)

```powershell
cd tests/playwright
npm install
npx playwright install chromium
npm test
```

To run with a visible browser:

```powershell
npm run test:headed
```

To use a different local/dev URL:

```powershell
$env:PLAYWRIGHT_BASE_URL = "https://localhost:7264"
npm test
```

## Safety

- Run against local/development data first, not the production IIS site.
- The initial smoke test is read-only and does not log in or modify records.
- Before adding tests for approvals, registrations, or administration, use a dedicated test account and test data; do not automate destructive actions against real records.
