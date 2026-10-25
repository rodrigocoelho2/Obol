/* Obol — the service worker. It only makes Obol installable as an app (Add to Home Screen); nothing is cached, every
   page comes straight from the server so the numbers are always the latest. */
'use strict';

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { /* network only */ });
