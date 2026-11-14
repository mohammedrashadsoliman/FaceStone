/* ==========================================================================
   FACESTONE PWA SERVICE WORKER
   High-performance caching, offline shell resilience, and background sync
   ========================================================================== */

const CACHE_NAME = 'facestone-pwa-v2026.11.14.1';

const PRECACHE_ASSETS = [
  '/',
  '/index.html',
  '/manifest.json',
  '/manifest.webmanifest',
  '/images/facestone-logo-transparent.png',
  '/assets/logo.png',
  '/assets/icon-192.png',
  '/assets/icon-512.png',
  '/assets/icon-maskable-512.png',
  '/assets/apple-touch-icon.png',
  '/css/chat-app.css',
  '/css/finance.css',
  '/css/mobile-app.css',
  '/css/management.css',
  '/css/control-monitoring.css',
  '/css/storage.css',
  '/css/laboratory.css',
  '/css/production.css',
  '/js/signalr.min.js',
  '/js/api.js',
  '/js/map.js',
  '/js/realtime.js',
  '/js/chat-state.js',
  '/js/chat-app.js',
  '/js/transportation.js',
  '/js/storage.js',
  '/js/laboratory.js',
  '/js/production.js',
  '/js/management.js',
  '/js/finance.js',
  '/js/control-monitoring.js',
  '/js/attendance.js',
  '/js/mobile-app.js'
];

// Install Event — precache core app shell assets
self.addEventListener('install', (event) => {
  self.skipWaiting();
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(PRECACHE_ASSETS).catch((err) => {
        console.warn('[SW] Precache partial error (non-fatal):', err);
      });
    })
  );
});

// Activate Event — cleanup outdated caches
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => {
      return Promise.all(
        keys.map((key) => {
          if (key !== CACHE_NAME) {
            return caches.delete(key);
          }
        })
      );
    }).then(() => self.clients.claim())
  );
});

// Fetch Event — smart caching strategy:
// 1. Bypass all WebSockets (/chatHub) and live APIs (/api/) to guarantee live data
// 2. Navigation fallback to index.html for SPA routes
// 3. Stale-while-revalidate for static scripts, styles, and assets
self.addEventListener('fetch', (event) => {
  const request = event.request;
  const url = new URL(request.url);

  // Exclude non-GET requests, SignalR WebSockets, DevTools refresh, and live API endpoints
  if (request.method !== 'GET' ||
      url.protocol === 'ws:' ||
      url.protocol === 'wss:' ||
      url.pathname.startsWith('/chatHub') ||
      url.pathname.includes('aspnetcore-browser-refresh') ||
      url.pathname.includes('browserLink') ||
      url.pathname.startsWith('/api/chat/attachments') ||
      url.pathname.startsWith('/swagger')) {
    return;
  }

  // Live API requests: Network-first, do NOT cache sensitive transactions
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(
      fetch(request).catch(() => {
        return new Response(JSON.stringify({
          success: false,
          offline: true,
          message: 'أنت غير متصل بالإنترنت حالياً. يرجى التحقق من اتصال الشبكة.'
        }), {
          status: 503,
          headers: { 'Content-Type': 'application/json; charset=utf-8' }
        });
      })
    );
    return;
  }

  // HTML SPA Navigation requests (e.g. /departments, /finance, /transportation, /storage)
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(async () => {
        const cache = await caches.open(CACHE_NAME);
        const cachedIndex = await cache.match('/index.html') || await cache.match('/');
        return cachedIndex || new Response('<h1>FaceStone Offline</h1><p>يرجى الاتصال بالإنترنت.</p>', {
          headers: { 'Content-Type': 'text/html; charset=utf-8' }
        });
      })
    );
    return;
  }

  // Static Assets (CSS, JS, Images, Fonts): Stale-while-revalidate with guaranteed Response
  event.respondWith(
    caches.match(request).then((cachedResponse) => {
      const fetchPromise = fetch(request).then((networkResponse) => {
        if (networkResponse && networkResponse.status === 200 && networkResponse.type === 'basic') {
          const responseToCache = networkResponse.clone();
          caches.open(CACHE_NAME).then((cache) => {
            cache.put(request, responseToCache);
          }).catch(() => {});
        }
        return networkResponse;
      }).catch(() => {
        // Fallback to cached response or a clean 404 response so respondWith never gets undefined
        return cachedResponse || new Response('Asset not available offline', {
          status: 404,
          statusText: 'Not Found',
          headers: { 'Content-Type': 'text/plain; charset=utf-8' }
        });
      });

      return cachedResponse || fetchPromise;
    })
  );
});
