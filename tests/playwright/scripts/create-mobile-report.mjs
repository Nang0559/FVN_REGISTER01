import { readdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const resultsDir = path.resolve(scriptDir, '../test-results');
const outputPath = path.resolve(scriptDir, '../mobile-report.html');
let report;
try { report = JSON.parse(await readFile(path.join(resultsDir, 'results.json'), 'utf8')); }
catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  report = { errors: [{ message: 'Could not read Playwright results: ' + message }], suites: [] };
}
const tests = [];
function walk(suites, parents = []) {
  for (const suite of suites ?? []) {
    const next = suite.title ? [...parents, suite.title] : parents;
    for (const spec of suite.specs ?? []) for (const test of spec.tests ?? []) {
      const latest = (test.results ?? []).at(-1) ?? {};
      tests.push({
        title: [...next, spec.title].filter(Boolean).join(' › '),
        status: test.status ?? latest.status ?? 'unknown',
        duration: latest.duration ?? 0,
        error: latest.error?.message ?? latest.errors?.map(x => x.message).filter(Boolean).join('\n') ?? ''
      });
    }
    walk(suite.suites, next);
  }
}
walk(report.suites);

async function findPngFiles(directory) {
  const files = [];
  async function visit(dir) {
    let entries;
    try { entries = await readdir(dir, { withFileTypes: true }); } catch { return; }
    for (const entry of entries) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) await visit(full);
      else if (entry.isFile() && entry.name.toLowerCase().endsWith('.png')) files.push(full);
    }
  }
  await visit(directory);
  return files.sort((a, b) => a.localeCompare(b));
}
const esc = value => String(value ?? '').replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const screenshots = await Promise.all((await findPngFiles(resultsDir)).map(async file => {
  const bytes = await readFile(file);
  return { name: path.relative(resultsDir, file).split(path.sep).join('/'), uri: 'data:image/png;base64,' + bytes.toString('base64'), bytes: bytes.length };
}));
const counts = tests.reduce((all, test) => {
  const key = test.status === 'expected' ? 'passed' : test.status === 'unexpected' ? 'failed' : test.status;
  all[key] = (all[key] ?? 0) + 1; return all;
}, {});
const cards = tests.map(test => {
  const status = test.status === 'expected' ? 'PASSED' : test.status === 'unexpected' ? 'FAILED' : String(test.status).toUpperCase();
  const cls = status === 'PASSED' ? 'passed' : status === 'FAILED' ? 'failed' : 'other';
  return '<article class="test"><div class="test-head"><span class="status ' + cls + '">' + esc(status) + '</span><span class="duration">' + (test.duration / 1000).toFixed(1) + 's</span></div><h3>' + esc(test.title) + '</h3>' + (test.error ? '<pre>' + esc(test.error) + '</pre>' : '') + '</article>';
}).join('\n');
const gallery = screenshots.map((shot, index) =>
  '<button class="shot" type="button" data-image="' + index + '" aria-label="Mở ảnh ' + esc(shot.name) + '"><img loading="lazy" src="' + shot.uri + '" alt="Ảnh chụp ' + esc(shot.name) + '"><span>' + esc(shot.name) + '</span><small>' + (shot.bytes / 1024).toFixed(0) + ' KB</small></button>'
).join('\n');
const metrics = [['Tests', tests.length], ['Passed', counts.passed ?? 0], ['Failed', counts.failed ?? 0], ['Flaky', counts.flaky ?? 0], ['Screenshots', screenshots.length]]
  .map(([label, value]) => '<div class="metric"><span>' + esc(label) + '</span><strong>' + value + '</strong></div>').join('');
const html = \`<!doctype html>
<html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover"><meta name="color-scheme" content="light"><title>FVN_REGISTER – Playwright Test Report</title>
<style>
:root{color-scheme:light;background:#f4f6fa;color:#172033;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Arial,sans-serif}*{box-sizing:border-box}body{margin:0;background:#f4f6fa;color:#172033;font-size:16px;line-height:1.5}main{max-width:1100px;margin:auto;padding:22px 15px 44px}.hero,.panel{background:#fff;border:1px solid #dce2ec;border-radius:16px;padding:20px;margin-bottom:16px}h1{font-size:24px;margin:0 0 8px}h2{font-size:19px;margin:0 0 14px}h3{font-size:16px;overflow-wrap:anywhere}.subtitle{color:#475569;margin:0}.metrics{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:8px;margin-top:18px}.metric{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:10px}.metric span{display:block;font-size:12px;color:#475569}.metric strong{font-size:22px}.gallery{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.shot{display:flex;flex-direction:column;gap:7px;min-width:0;text-align:left;padding:8px;border:1px solid #dce2ec;border-radius:12px;background:white;color:#172033;font:inherit;cursor:pointer}.shot img{width:100%;height:190px;object-fit:contain;background:#eef2f7;border-radius:7px}.shot span{font-size:13px;font-weight:650;overflow-wrap:anywhere}.shot small{font-size:11px;color:#64748b}.test{border:1px solid #dce2ec;border-radius:12px;padding:14px;margin:10px 0;overflow-wrap:anywhere}.test-head{display:flex;justify-content:space-between}.status{font-size:12px;font-weight:800;border-radius:999px;padding:4px 9px}.passed{background:#dcfce7;color:#166534}.failed{background:#fee2e2;color:#991b1b}.other{background:#e2e8f0;color:#334155}.duration{font-size:13px;color:#64748b}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#fff1f2;color:#881337;border-radius:8px;padding:12px;font-size:13px}.empty{color:#475569;padding:12px}.footer{font-size:12px;color:#64748b}.viewer{position:fixed;inset:0;z-index:10;display:none;align-items:center;justify-content:center;background:rgba(15,23,42,.95);padding:16px}.viewer.open{display:flex}.viewer img{max-width:100%;max-height:calc(100dvh - 90px);object-fit:contain}.viewer-inner{width:100%;text-align:center;color:white}.viewer-close{position:fixed;right:12px;top:12px;min-width:44px;min-height:44px;border:0;border-radius:50%;font-size:24px}.viewer-caption{overflow-wrap:anywhere}@media(max-width:650px){main{padding:12px 10px 28px}.hero,.panel{padding:15px}h1{font-size:21px}.metrics{grid-template-columns:repeat(2,minmax(0,1fr))}.gallery{grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}.shot{padding:6px}.shot img{height:145px}}@media(max-width:360px){.gallery{grid-template-columns:1fr}.shot img{height:210px}}
</style></head><body><main>
<header class="hero"><h1>FVN_REGISTER – Báo cáo kiểm thử</h1><p class="subtitle">Báo cáo độc lập cho iPhone/iPad. Ảnh được nhúng trong tệp HTML; chạm ảnh để phóng to.</p><div class="metrics">${metrics}</div></header>
<section class="panel"><h2>Ảnh chụp giao diện (${screenshots.length})</h2><div class="gallery">${gallery || '<p class="empty">Không tìm thấy PNG trong test-results.</p>'}</div></section>
<section class="panel"><h2>Kết quả kiểm thử</h2>${cards || '<p class="empty">Không tìm thấy kết quả test. Kiểm tra log GitHub Actions.</p>'}</section>
<footer class="footer">Trace, video và tệp chẩn đoán vẫn có trong artifact. Kiểm thử chỉ đọc không thể xác nhận mọi luồng nghiệp vụ.</footer></main>
<div class="viewer" id="viewer" role="dialog" aria-modal="true" aria-label="Xem ảnh phóng to"><button class="viewer-close" id="closeViewer" type="button" aria-label="Đóng">×</button><div class="viewer-inner"><img id="largeImage" alt=""><p class="viewer-caption" id="caption"></p></div></div>
<script>
const shots = Array.from(document.querySelectorAll('.shot')).map(button => ({name: button.querySelector('span').textContent, uri: button.querySelector('img').src}));
const viewer=document.getElementById('viewer'), largeImage=document.getElementById('largeImage'), caption=document.getElementById('caption');
function closeViewer(){viewer.classList.remove('open');largeImage.removeAttribute('src')}
document.querySelectorAll('.shot').forEach(button=>button.addEventListener('click',()=>{const shot=shots[Number(button.dataset.image)];if(!shot)return;largeImage.src=shot.uri;largeImage.alt=shot.name;caption.textContent=shot.name;viewer.classList.add('open')}));
document.getElementById('closeViewer').addEventListener('click',closeViewer);
viewer.addEventListener('click',event=>{if(event.target===viewer)closeViewer()});
document.addEventListener('keydown',event=>{if(event.key==='Escape')closeViewer()});
</script></body></html>`;
await writeFile(outputPath, html, 'utf8');
const totalBytes = screenshots.reduce((sum, shot) => sum + shot.bytes, 0);
console.log('Created mobile report with ' + screenshots.length + ' embedded screenshots (' + (totalBytes / 1024 / 1024).toFixed(1) + ' MB raw PNG): ' + outputPath);
