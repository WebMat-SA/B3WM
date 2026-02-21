/**
 * IndexedDbWorker.js - Web Worker para escrita no IndexedDB em thread separada.
 * Evita competir com a main thread (renderização ECharts, Blazor, etc).
 * KeyPath "id" (case-sensitive).
 */
const DB_NAME = 'B3WM.Database';
const DB_VERSION = 1;
const STORES = ['Ticks'];

let dbInstance = null;

function getDb() {
    return new Promise((resolve, reject) => {
        if (dbInstance) {
            resolve(dbInstance);
            return;
        }
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = (e) => {
            const db = e.target.result;
            STORES.forEach(name => {
                if (!db.objectStoreNames.contains(name)) {
                    db.createObjectStore(name, { keyPath: 'id' });
                }
            });
        };
        req.onsuccess = (e) => {
            dbInstance = e.target.result;
            resolve(dbInstance);
        };
        req.onerror = () => reject(req.error);
    });
}

function putBatch(storeName, values) {
    if (!values || values.length === 0) return Promise.resolve();
    return getDb().then(db => new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, 'readwrite');
        const store = tx.objectStore(storeName);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
        for (let i = 0; i < values.length; i++) {
            const obj = values[i];
            if (obj && obj.id === undefined) {
                obj.id = obj.Id ?? obj.trydID ?? String(i);
            }
            store.put(obj);
        }
    }));
}

self.onmessage = (e) => {
    const { type, store, data } = e.data || {};
    if (type === 'putBatch' && store && data) {
        putBatch(store, data)
            .then(() => self.postMessage({ ok: true }))
            .catch(err => self.postMessage({ ok: false, error: String(err) }));
    }
};
