const CACHE_NAME = 'fvn-register-pwa-v2';
const APP_SHELL = [
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

  // Static PWA assets use cache-first; navigations stay network-first so the
  // installed app always receives the current Blazor application.
  const isPwaAsset = url.pathname === '/manifest.json' || url.pathname.startsWith('/pwa/');
  if (isPwaAsset) {
    event.respondWith(
      caches.match(event.request).then(cached => cached || fetch(event.request))
    );
    return;
  }

  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request).catch(() => caches.match('/manifest.json'))
    );
  }
});
