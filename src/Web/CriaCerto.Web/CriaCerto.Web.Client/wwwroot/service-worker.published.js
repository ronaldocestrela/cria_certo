// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts("./service-worker-assets.js");

const CACHE_NAME_PREFIX = "criacerto-offline-cache-";
const CACHE_NAME = `${CACHE_NAME_PREFIX}${self.assetsManifest?.version || "v2"}`;
const OFFLINE_URL = "/offline.html";

const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm$/, /\.html$/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.woff2$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

const CORE_ASSETS = [
  "/",
  OFFLINE_URL,
  "/manifest.webmanifest",
  "/icon-192.png",
  "/icon-512.png",
  "/favicon.png",
  "/app.css",
  "/js/offlineSync.js",
  "/js/downloadHelper.js"
];

self.addEventListener("install", event => {
  self.skipWaiting();
  event.waitUntil(onInstall(event));
});

self.addEventListener("activate", event => {
  event.waitUntil(onActivate(event));
});

self.addEventListener("fetch", event => {
  if (event.request.method !== "GET") {
    return;
  }

  const url = new URL(event.request.url);

  // Nunca interceptar origens externas nem endpoints de API / Health
  if (url.origin !== self.location.origin ||
      url.pathname.startsWith("/api/") ||
      url.pathname.startsWith("/health")) {
    return;
  }

  // Navegação de documento HTML (SPA / Blazor App Shell)
  if (event.request.mode === "navigate") {
    event.respondWith(
      fetch(event.request)
        .then(response => {
          if (response.ok) {
            const clone = response.clone();
            caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
          }
          return response;
        })
        .catch(async () => {
          const cachedPage = await caches.match(event.request, { ignoreSearch: true });
          if (cachedPage) return cachedPage;

          const cachedRoot = await caches.match("/");
          if (cachedRoot) return cachedRoot;

          return caches.match(OFFLINE_URL);
        })
    );
    return;
  }

  // Ativos estáticos publicados
  event.respondWith(
    caches.match(event.request).then(cachedResponse => {
      if (cachedResponse) {
        return cachedResponse;
      }

      return fetch(event.request)
        .then(response => {
          if (response.ok) {
            const clone = response.clone();
            caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
          }
          return response;
        })
        .catch(() => new Response("Offline asset unavailable", { status: 503, statusText: "Service Unavailable" }));
    })
  );
});

async function onInstall() {
  const cache = await caches.open(CACHE_NAME);

  for (const asset of CORE_ASSETS) {
    try {
      await cache.add(asset);
    } catch (err) {
      console.warn("[SW Published] Falha ao fazer pre-cache do ativo base:", asset, err);
    }
  }

  if (self.assetsManifest && Array.isArray(self.assetsManifest.assets)) {
    const manifestAssets = self.assetsManifest.assets
      .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
      .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)));

    for (const asset of manifestAssets) {
      try {
        const request = new Request(asset.url, { cache: "no-cache" });
        await cache.add(request);
      } catch (err) {
        console.warn("[SW Published] Falha ao fazer pre-cache do ativo de manifesto:", asset.url, err);
      }
    }
  }
}

async function onActivate() {
  const cacheKeys = await caches.keys();
  await Promise.all(
    cacheKeys
      .filter(key => key !== CACHE_NAME && (key.startsWith(CACHE_NAME_PREFIX) || key.startsWith("criacerto-shell-")))
      .map(key => caches.delete(key))
  );
  await self.clients.claim();
}
