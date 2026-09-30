let deferredInstallPrompt = null;
let installDotNetRef = null;

window.fvnPwa = {
  initialize: function (dotNetRef) {
    installDotNetRef = dotNetRef;

    window.addEventListener('beforeinstallprompt', event => {
      event.preventDefault();
      deferredInstallPrompt = event;
      installDotNetRef?.invokeMethodAsync('OnInstallAvailable');
    });

    window.addEventListener('appinstalled', () => {
      deferredInstallPrompt = null;
      installDotNetRef?.invokeMethodAsync('OnAppInstalled');
    });

    if (window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true) {
      installDotNetRef?.invokeMethodAsync('OnAlreadyInstalled');
    }
  },

  prompt: async function () {
    if (!deferredInstallPrompt) return false;

    deferredInstallPrompt.prompt();
    const result = await deferredInstallPrompt.userChoice;
    deferredInstallPrompt = null;
    return result?.outcome === 'accepted';
  },

  canPrompt: function () {
    return deferredInstallPrompt !== null;
  },

  isStandalone: function () {
    return window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
  },

  isIosSafari: function () {
    const ua = window.navigator.userAgent || '';
    const isIos = /iPad|iPhone|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    return isIos && /Safari/i.test(ua) && !/CriOS|FxiOS|EdgiOS/i.test(ua);
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
