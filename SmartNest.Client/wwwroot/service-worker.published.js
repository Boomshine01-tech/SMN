self.addEventListener('install', e => e.waitUntil(self.skipWaiting()));
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
self.addEventListener('fetch', e => {
  if (e.request.method !== 'GET') return;
  e.respondWith(caches.open('smn-v1').then(c =>
    c.match(e.request).then(r => r || fetch(e.request).then(res => {
      c.put(e.request, res.clone()); return res;
    }))
  ));
});
