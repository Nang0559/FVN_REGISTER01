# Web Push + app-icon unread badge (PWA)

## One-time setup
1. Run `SQL/64_WebPushSubscriptions.sql`.
2. Generate VAPID keys once (keep the private key secret):
   `npx web-push generate-vapid-keys`
3. Configure the API (environment variables or `web.config`, NOT git):
   `WebPush__Subject=mailto:it@company.com`, `WebPush__PublicKey=...`, `WebPush__PrivateKey=...`
4. The site must be served over HTTPS and the PWA installed ("Install app").

## Behaviour
- User signs in and taps the bell-with-sparkle button once -> device is bound to that user.
- New notification -> API pushes the absolute unread count to all devices of that user -> the
  service worker sets the icon badge (works with the app closed / signed out).
- Logout calls `unsubscribe`, so the next person on a shared PC does not see the previous user's count.
- Push text is generic ("N unread") by default. `WebPush__ShowContent=true` shows title/body.

## Known limits
- Reading notifications on one device does not clear the badge on another until that device opens the app.
- iOS needs 16.4+ and "Add to Home Screen". Android launchers may show a dot instead of a number.
- Notification icons: browsers ignore SVG; add PNG icons if you want a custom icon in the toast.
