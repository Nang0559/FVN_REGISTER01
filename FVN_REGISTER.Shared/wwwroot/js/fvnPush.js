// Web Push + app-icon badge helpers (called from Blazor through IJSRuntime).
// Works in an installed PWA (Windows/macOS/Android Chrome & Edge, iOS 16.4+ Home Screen app).
window.fvnPush = (function () {
  let pendingPermission = null;

  function isSupported() {
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
  }

  function permission() {
    return isSupported() ? Notification.permission : 'unsupported';
  }

  function urlBase64ToUint8Array(base64) {
    const padding = '='.repeat((4 - (base64.length % 4)) % 4);
    const b64 = (base64 + padding).replace(/-/g, '+').replace(/_/g, '/');
    const raw = atob(b64);
    const out = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
    return out;
  }

  function toBase64Url(buffer) {
    if (!buffer) return '';
    const bytes = new Uint8Array(buffer);
    let s = '';
    for (let i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i]);
    return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }

  function sameKey(buffer, bytes) {
    if (!buffer) return false;
    const a = new Uint8Array(buffer);
    if (a.length !== bytes.length) return false;
    for (let i = 0; i < a.length; i++) if (a[i] !== bytes[i]) return false;
    return true;
  }

  // Safari/iOS only shows the permission prompt when it is requested synchronously inside the
  // user's tap. Blazor Server handles @onclick on the server (gesture lost), so the button is
  // marked with data-fvn-push-request and we request permission here, in the real click.
  document.addEventListener('click', function (e) {
    const el = e.target && e.target.closest && e.target.closest('[data-fvn-push-request]');
    if (el && isSupported() && Notification.permission === 'default') {
      pendingPermission = Notification.requestPermission();
    }
  }, true);

  async function subscribe(publicKey) {
    if (!isSupported()) return null;

    if (pendingPermission) {
      try { await pendingPermission; } finally { pendingPermission = null; }
    }
    if (Notification.permission === 'default') {
      await Notification.requestPermission();
    }
    if (Notification.permission !== 'granted') return null;

    const reg = await navigator.serviceWorker.ready;
    const key = urlBase64ToUint8Array(publicKey);

    let sub = await reg.pushManager.getSubscription();
    if (sub && !sameKey(sub.options && sub.options.applicationServerKey, key)) {
      await sub.unsubscribe();            // VAPID key rotated
      sub = null;
    }
    if (!sub) {
      sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });
    }

    return {
      endpoint: sub.endpoint,
      p256dh: toBase64Url(sub.getKey('p256dh')),
      auth: toBase64Url(sub.getKey('auth')),
      userAgent: navigator.userAgent
    };
  }

  async function currentEndpoint() {
    if (!isSupported()) return null;
    const reg = await navigator.serviceWorker.getRegistration();
    const sub = reg ? await reg.pushManager.getSubscription() : null;
    return sub ? sub.endpoint : null;
  }

  async function unsubscribe() {
    if (!isSupported()) return false;
    const reg = await navigator.serviceWorker.getRegistration();
    const sub = reg ? await reg.pushManager.getSubscription() : null;
    return sub ? sub.unsubscribe() : false;
  }

  async function setBadge(count) {
    try {
      const n = Number(count) || 0;
      if (n > 0 && 'setAppBadge' in navigator) await navigator.setAppBadge(n);
      else if ('clearAppBadge' in navigator) await navigator.clearAppBadge();
    } catch (e) { /* badge is cosmetic */ }
  }

  return { isSupported, permission, subscribe, currentEndpoint, unsubscribe, setBadge };
})();
