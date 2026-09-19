// Scrim Web Player - Progressive Web App Service Worker
const CACHE_NAME = 'scrim-pwa-v2';
const PRECACHE_ASSETS = [
    '/',
    '/index.html',
    '/assets/player.css',
    '/assets/player.js',
    '/assets/manifest.webmanifest',
    '/assets/icon.svg',
    '/assets/icon-192.png',
    '/assets/icon-512.png'
];

// Install Event: Pre-cache core shell
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => {
            return cache.addAll(PRECACHE_ASSETS).catch((err) => {
                console.warn('[PWA SW] Pre-cache error:', err);
            });
        }).then(() => self.skipWaiting())
    );
});

// Activate Event: Clear stale caches
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((cacheNames) => {
            return Promise.all(
                cacheNames.map((cache) => {
                    if (cache !== CACHE_NAME) {
                        return caches.delete(cache);
                    }
                })
            );
        }).then(() => self.clients.claim())
    );
});

// Fetch Event: Pass-through real-time streams and API, network-first for static shell
self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    // Bypass cache completely for live audio streaming, SSE events, and dynamic API endpoints
    if (url.pathname.startsWith('/stream') || url.pathname.startsWith('/api/') || event.request.method !== 'GET') {
        return;
    }

    // Network-first strategy with cache fallback
    event.respondWith(
        fetch(event.request).then((networkResponse) => {
            if (networkResponse && networkResponse.status === 200) {
                const responseToCache = networkResponse.clone();
                caches.open(CACHE_NAME).then((cache) => cache.put(event.request, responseToCache));
            }
            return networkResponse;
        }).catch(() => {
            return caches.match(event.request).then((cachedResponse) => {
                if (cachedResponse) {
                    return cachedResponse;
                }
                if (event.request.mode === 'navigate') {
                    return caches.match('/');
                }
            });
        })
    );
});
