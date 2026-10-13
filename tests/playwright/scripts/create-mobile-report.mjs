import { readFile, writeFile } from 'node:fs/promises';

const inputPath = new URL('../test-results/results.json', import.meta.url);
const outputPath = new URL('../mobile-report.html', import.meta.url);

let report;
try {
  report = JSON.parse(await readFile(inputPath, 'utf8'));
} catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  report = { errors: [{ message: `Could not read Playwright results: ${message}` }], suites: [] };
}

const tests = [];
function walk(suites, parents = []) {
  for (const suite of suites ?? []) {
    const nextParents = suite.title ? [...parents, suite.title] : parents;
    for (const spec of suite.specs ?? []) {
      for (const test of spec.tests ?? []) {
        const results = test.results ?? [];
        const latest = results.at(-1) ?? {};
        tests.push({
          title: [...nextParents, spec.title].filter(Boolean).join(' › '),
          status: test.status ?? latest.status ?? 'unknown',
          duration: latest.duration ?? 0,
          error: latest.error?.message ?? latest.errors?.map(item => item.message).filter(Boolean).join('\n') ?? ''
        });
      }
    }
    walk(suite.suites, nextParents);
  }
}
walk(report.suites);

const counts = tests.reduce((all, test) => {
  const key = test.status === 'expected' ? 'passed'
    : test.status === 'unexpected' ? 'failed'
    : test.status === 'flaky' ? 'flaky'
    : test.status === 'skipped' ? 'skipped'
    : test.status;
  all[key] = (all[key] ?? 0) + 1;
  return all;
}, {});
const esc = value => String(value ?? '').replace(/[&<>"]/g, char => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;'
}[char]));
const cards = tests.map(test => {
  const status = test.status === 'expected' ? 'PASSED'
    : test.status === 'unexpected' ? 'FAILED'
    : test.status.toUpperCase();
  const cls = status === 'PASSED' ? 'passed' : status === 'FAILED' ? 'failed' : 'other';
  return `<article class="test"><div class="test-head"><span class="status ${cls}">${esc(status)}</span><span class="duration">${(test.duration / 1000).toFixed(1)}s</span></div><h2>${esc(test.title)}</h2>${test.error ? `<pre>${esc(test.error)}</pre>` : ''}</article>`;
}).join('\n');

const total = tests.length;
const summary = [
  ['Total', total],
  ['Passed', counts.passed ?? 0],
  ['Failed', counts.failed ?? 0],
  ['Flaky', counts.flaky ?? 0],
  ['Skipped', counts.skipped ?? 0]
].map(([label, value]) => `<div class="metric"><span>${esc(label)}</span><strong>${value}</strong></div>`).join('');

const html = `<!doctype html>
<html lang="vi">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<meta name="color-scheme" content="light">
<title>FVN_REGISTER – Playwright Test Report</title>
<style>
:root{color-scheme:light;background:#f4f6fa;color:#172033;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Arial,sans-serif}
*{box-sizing:border-box}body{margin:0;background:#f4f6fa;color:#172033;font-size:16px;line-height:1.5}
main{max-width:900px;margin:0 auto;padding:24px 16px 48px}.hero{background:#fff;border:1px solid #dce2ec;border-radius:16px;padding:22px;margin-bottom:16px}
h1{font-size:24px;line-height:1.25;margin:0 0 8px;color:#111827}.subtitle{margin:0;color:#475569;overflow-wrap:anywhere}
.metrics{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:8px;margin-top:20px}.metric{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:10px;min-width:0}.metric span{display:block;font-size:12px;color:#475569}.metric strong{display:block;font-size:22px;color:#111827}
.test{background:#fff;border:1px solid #dce2ec;border-radius:12px;padding:16px;margin:10px 0;overflow-wrap:anywhere}.test-head{display:flex;align-items:center;justify-content:space-between;gap:12px}.status{font-size:12px;font-weight:800;border-radius:999px;padding:4px 9px}.passed{background:#dcfce7;color:#166534}.failed{background:#fee2e2;color:#991b1b}.other{background:#e2e8f0;color:#334155}.duration{font-size:13px;color:#64748b}h2{font-size:16px;line-height:1.45;margin:12px 0 0;color:#111827}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#fff1f2;color:#881337;border:1px solid #fecdd3;border-radius:8px;padding:12px;font:13px/1.5 ui-monospace,SFMono-Regular,Menlo,monospace}
.empty{background:#fff;border:1px solid #dce2ec;border-radius:12px;padding:18px;color:#475569}.footer{font-size:12px;color:#64748b;margin-top:18px}
@media(max-width:520px){main{padding:14px 10px 32px}.hero{padding:17px}h1{font-size:21px}.metrics{grid-template-columns:repeat(2,minmax(0,1fr))}.metric:last-child{grid-column:span 2}}
</style>
</head>
<body><main>
<header class="hero"><h1>FVN_REGISTER – Báo cáo kiểm thử</h1><p class="subtitle">Báo cáo HTML độc lập, tối ưu cho iPhone/iPad. Có thể mở trực tiếp từ ứng dụng Tệp sau khi giải nén ZIP.</p><div class="metrics">${summary}</div></header>
<section aria-label="Test results">${cards || '<div class="empty">Không tìm thấy kết quả test. Kiểm tra log của GitHub Actions để biết vì sao báo cáo chưa được tạo.</div>'}</section>
<footer class="footer">Báo cáo tĩnh được tạo từ kết quả Playwright. Với trace/video/screenshot chi tiết, dùng thư mục test-results trong ZIP.</footer>
</main></body></html>`;

await writeFile(outputPath, html, 'utf8');
console.log(`Created mobile-friendly report: ${outputPath.pathname}`);
