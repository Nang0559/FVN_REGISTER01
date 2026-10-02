const CACHE_NAME = 'fvn-register-pwa-v2';
const APP_SHELL = [
  '/',
  '/manifest.json',
  '/pwa/icon-192.svg',
  '/pwa/icon-512.svg'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(APP_SHELL))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(
        keys
          .filter(key => key !== CACHE_NAME)
          .map(key => caches.delete(key))
      ))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') return;

  const url = new URL(event.request.url);
  if (url.origin !== self.location.origin) return;

  // Never cache API/auth/Blazor responses. This application is a Blazor
  // Server app and caching those responses can expose stale or user-specific
  // data after a login/session change.
  if (url.pathname.startsWith('/api/') ||
      url.pathname.startsWith('/_blazor') ||
      url.pathname.startsWith('/_framework/')) {
    return;
  }

  // Static PWA assets use cache-first.
  const isPwaAsset = url.pathname === '/manifest.json' || url.pathname.startsWith('/pwa/');
  if (isPwaAsset) {
    event.respondWith(
      caches.match(event.request).then(cached => cached || fetch(event.request))
    );
    return;
  }

  // Navigations stay network-first so the installed app receives the current
  // Blazor application. The cached root is only an offline fallback.
  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request).catch(() => caches.match('/'))
    );
  }
});

// ---------------------------------------------------------------------------
// Web Push: update the app-icon badge and show a notification, even when the
// app is closed or the user is signed out. The payload only carries the
// absolute unread count (and a relative URL); text is localized here.
// ---------------------------------------------------------------------------
self.addEventListener('push', event => {
  let data = {};
  try { data = event.data ? event.data.json() : {}; } catch (e) { data = {}; }

  const badge = Number.isFinite(data.badge) ? Math.max(0, data.badge) : 0;

  event.waitUntil((async () => {
    try {
      if ('setAppBadge' in self.navigator) {
        if (badge > 0) await self.navigator.setAppBadge(badge);
        else await self.navigator.clearAppBadge();
      }
    } catch (e) { /* badge is cosmetic */ }

    // If the app is on screen the in-app bell (SignalR) already shows it:
    // skip the system notification and just let the page know.
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    if (windows.some(c => c.visibilityState === 'visible')) {
      windows.forEach(c => c.postMessage({ type: 'fvn-push', badge }));
      return;
    }

    const ja = (self.navigator.language || 'vi').toLowerCase().startsWith('ja');
    const title = data.title || 'e-Approval';
    const body = data.body || (ja
      ? `未読の通知が ${badge} 件あります`
      : `Bạn có ${badge} thông báo chưa đọc`);

    await self.registration.showNotification(title, {
      body,
      tag: 'fvn-unread',
      renotify: true,
      data: { url: data.url || '/' }
    });
  })());
});

self.addEventListener('notificationclick', event => {
  event.notification.close();

  event.waitUntil((async () => {
    let target = self.location.origin + '/';
    try {
      const u = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin);
      if (u.origin === self.location.origin) target = u.href;
    } catch (e) { /* keep default */ }

    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    for (const client of windows) {
      if ('focus' in client) {
        await client.focus();
        if ('navigate' in client && client.url !== target) {
          try { await client.navigate(target); } catch (e) { /* ignore */ }
        }
        return;
      }
    }
    await self.clients.openWindow(target);
  })());
});
