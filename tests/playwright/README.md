# FVN_REGISTER Playwright smoke tests

These tests are isolated from the .NET application on the test/playwright-ui branch. By default, they target the official site at https://dangkyphep.fcc-vn.com.vn. The initial smoke test only opens the application root and checks that the page responds; it does not sign in, submit forms, or change business data.

## Requirements

- Node.js LTS and npm
- Network access to the official FVN_REGISTER site

## Run on Windows (PowerShell)

~~~powershell
cd tests/playwright
npm install
npx playwright install chromium
npm test
~~~

To run with a visible browser:

~~~powershell
npm run test:headed
~~~

To override the target URL (for example, to use a local or development instance):

~~~powershell
$env:PLAYWRIGHT_BASE_URL = "https://localhost:7264"
npm test
~~~

## Safety

- The default test performs only a page load and visibility/status checks; it does not log in or modify records.
- Because the default target is the official site, run only the supplied read-only smoke test unless you have explicitly prepared a safe test account and test data.
- Before adding tests for approvals, registrations, or administration, use a dedicated test account and test data; do not automate destructive actions against real records.
