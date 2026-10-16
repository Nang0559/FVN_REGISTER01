let deferredInstallPrompt = null;
let installDotNetRef = null;

function isStandaloneDisplayMode() {
  return window.matchMedia?.('(display-mode: standalone)').matches === true
    || window.navigator.standalone === true;
}

function isIosSafariBrowser() {
  const ua = window.navigator.userAgent || '';
  const isIos = /iPad|iPhone|iPod/.test(ua)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

  return isIos && /Safari/i.test(ua) && !/CriOS|FxiOS|EdgiOS/i.test(ua);
}

// Capture the event as soon as the script is loaded. In Blazor Server the
// component may render after beforeinstallprompt has already fired.
window.addEventListener('beforeinstallprompt', event => {
  event.preventDefault();
  deferredInstallPrompt = event;

  if (installDotNetRef) {
    installDotNetRef.invokeMethodAsync('OnInstallAvailable');
  }
});

window.addEventListener('appinstalled', () => {
  deferredInstallPrompt = null;
  installDotNetRef?.invokeMethodAsync('OnAppInstalled');
});

window.fvnPwa = {
  initialize: function (dotNetRef) {
    installDotNetRef = dotNetRef;

    if (isStandaloneDisplayMode()) {
      dotNetRef?.invokeMethodAsync('OnAlreadyInstalled');
      return;
    }

    if (deferredInstallPrompt) {
      dotNetRef?.invokeMethodAsync('OnInstallAvailable');
    }
  },

  prompt: async function () {
    if (!deferredInstallPrompt) return false;

    const promptEvent = deferredInstallPrompt;
    deferredInstallPrompt = null;

    promptEvent.prompt();
    const result = await promptEvent.userChoice;
    return result?.outcome === 'accepted';
  },

  canPrompt: function () {
    return deferredInstallPrompt !== null;
  },

  isStandalone: function () {
    return isStandaloneDisplayMode();
  },

  isIosSafari: function () {
    return isIosSafariBrowser();
  },

  getLanguage: function () {
    return (navigator.language || 'vi').toLowerCase();
  },

  registerServiceWorker: async function () {
    if (!('serviceWorker' in navigator)) return false;

    try {
      const registration = await navigator.serviceWorker.register('/service-worker.js', { scope: '/' });
      return !!registration;
    } catch (error) {
      console.warn('FVN PWA service worker registration failed.', error);
      return false;
    }
  }
};

// Register independently of Blazor rendering so the PWA capability is ready
// even when the interactive component is rendered later.
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => window.fvnPwa?.registerServiceWorker(), { once: true });
} else {
  window.fvnPwa?.registerServiceWorker();
}
