# FVN_REGISTER Playwright UI capture tests

This test suite lives only on `test/playwright-ui`. It targets `https://dangkyphep.fcc-vn.com.vn` by default and supports `PLAYWRIGHT_BASE_URL` to test another environment.

## GitHub Actions setup

In **Settings → Secrets and variables → Actions → Repository secrets**, configure a dedicated test account:

- `PLAYWRIGHT_USERNAME`: test username / employee code
- `PLAYWRIGHT_PASSWORD`: test password

The workflow also accepts the alternative names `TEST_USERNAME` + `TEST_PASSWORD` or `USERNAME` + `PASSWORD`. Do not put credentials in source code, workflow inputs, commits, screenshots, or logs. Use an account that has only the permissions needed for visual testing.

## Run in GitHub Actions

1. Open **Actions → Playwright UI Smoke Test**.
2. Select **Run workflow** and branch `test/playwright-ui`.
3. Optionally enter `base_url`; leave it blank to use the default website.
4. Download artifact `playwright-smoke-results` after the run.
5. Extract the ZIP and look under `tests/playwright/test-results/screenshots/`.

The authenticated capture saves a login-page image and screenshots of internal pages reachable through visible links, up to 80 URLs. Increase `PLAYWRIGHT_MAX_PAGES` in the workflow if necessary. Files use route-derived names; routes that normalize to the same filename can overwrite one another.

## Scope and safety

- The crawler follows internal links and takes screenshots; it does not click submit buttons, approve/reject items, create registrations, or deliberately modify business records.
- It can capture only pages reachable to the configured account. Screens and menu items depend on that account's roles and permissions.
- This is broad link-based coverage, **not a guarantee that every feature or every tab/dialog is captured**. Features accessible only through buttons, tabs, or dynamic menus need targeted tests.
- Use a dedicated test account and a non-production test environment where possible. The default URL is the official site, so the crawler is intentionally read-only.
- The existing unauthenticated smoke test still saves `homepage.png`.
