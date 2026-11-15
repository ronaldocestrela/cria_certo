// offlineSync.js - IndexedDB & Network Listener helper for CriaCerto WASM PWA

const DB_NAME = "CriaCertoDb";
const DB_VERSION = 1;
const STORE_NAME = "offlineQueue";

let dotNetRefInstance = null;

function openDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME, { keyPath: "id" });
            }
        };

        request.onsuccess = (event) => resolve(event.target.result);
        request.onerror = (event) => reject(event.target.error);
    });
}

window.criaCertoOfflineSync = {
    init: async function (dotNetRef) {
        dotNetRefInstance = dotNetRef;

        window.addEventListener("online", () => {
            if (dotNetRefInstance) {
                dotNetRefInstance.invokeMethodAsync("OnNetworkStatusChanged", true);
            }
        });

        window.addEventListener("offline", () => {
            if (dotNetRefInstance) {
                dotNetRefInstance.invokeMethodAsync("OnNetworkStatusChanged", false);
            }
        });

        // Garantir que a store existe
        await openDatabase();
        return navigator.onLine;
    },

    isOnline: function () {
        return navigator.onLine;
    },

    enqueueOperation: async function (operation) {
        const db = await openDatabase();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], "readwrite");
            const store = transaction.objectStore(STORE_NAME);
            const req = store.put(operation);

            req.onsuccess = () => resolve(true);
            req.onerror = () => reject(req.error);
        });
    },

    getPendingOperations: async function () {
        const db = await openDatabase();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], "readonly");
            const store = transaction.objectStore(STORE_NAME);
            const req = store.getAll();

            req.onsuccess = () => resolve(req.result || []);
            req.onerror = () => reject(req.error);
        });
    },

    removeOperation: async function (id) {
        const db = await openDatabase();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], "readwrite");
            const store = transaction.objectStore(STORE_NAME);
            const req = store.delete(id);

            req.onsuccess = () => resolve(true);
            req.onerror = () => reject(req.error);
        });
    },

    clearQueue: async function () {
        const db = await openDatabase();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], "readwrite");
            const store = transaction.objectStore(STORE_NAME);
            const req = store.clear();

            req.onsuccess = () => resolve(true);
            req.onerror = () => reject(req.error);
        });
    }
};
