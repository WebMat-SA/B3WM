/**
 * IndexedDbWorker.js - Web Worker para escrita no IndexedDB em thread separada.
 * Evita competir com a main thread (renderização ECharts, Blazor, etc).
 * KeyPath "id" (case-sensitive).
 */
const DB_NAME = 'B3WM.Database';
const DB_VERSION = 3;
const STORES = ['Ticks', 'Bars', 'Bubbles', 'VolumeLevels'];

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
            const tx = e.target.transaction;
            STORES.forEach(name => {
                let store;
                if (!db.objectStoreNames.contains(name)) {
                    store = db.createObjectStore(name, { keyPath: 'id' });
                } else {
                    store = tx.objectStore(name);
                }
                if (name === 'Ticks' && !store.indexNames.contains('byTime')) {
                    store.createIndex('byTime', 'time', { unique: false });
                }
                if (name === 'Bars' && !store.indexNames.contains('byTimeframe')) {
                    store.createIndex('byTimeframe', 'timeframe', { unique: false });
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

function getChunk(storeName, chunkSize, afterKey, options = {}) {
    const { minDate } = options;
    const useTimeIndex = storeName === 'Ticks' && minDate != null && minDate !== '';

    return getDb().then(db => new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, 'readonly');
        const store = tx.objectStore(storeName);

        if (useTimeIndex) {
            const idx = store.index('byTime');
            let range;
            if (afterKey != null && afterKey !== '') {
                range = IDBKeyRange.lowerBound(afterKey, true);
            } else {
                range = IDBKeyRange.lowerBound(minDate);
            }
            const items = [];
            const req = idx.openCursor(range, 'next');
            req.onsuccess = () => {
                const cursor = req.result;
                if (cursor && items.length < chunkSize) {
                    items.push(cursor.value);
                    cursor.continue();
                } else {
                    const lastKey = items.length > 0 ? (items[items.length - 1].time ?? null) : null;
                    resolve({ items, lastKey });
                }
            };
            req.onerror = () => reject(req.error);
        } else {
            const range = (afterKey != null && afterKey !== '') ? IDBKeyRange.lowerBound(afterKey, true) : null;
            const request = store.getAll(range, chunkSize);
            request.onsuccess = () => {
                const items = request.result ?? [];
                const lastKey = items.length > 0 ? (items[items.length - 1].id ?? null) : null;
                resolve({ items, lastKey });
            };
            request.onerror = () => reject(request.error);
        }
    }));
}

self.onmessage = (e) => {
    const { type, requestId, store, chunkSize, afterKey, data } = e.data || {};
    if (type === 'putBatch' && store && data) {
        putBatch(store, data)
            .then(() => self.postMessage({ ok: true }))
            .catch(err => self.postMessage({ ok: false, error: String(err) }));
    } else if (type === 'getChunk' && requestId != null && store) {
        const opts = { minDate: e.data?.minDate ?? null };
        getChunk(store, chunkSize ?? 5000, afterKey || '', opts)
            .then(result => self.postMessage({ type: 'getChunkResponse', requestId, ...result }))
            .catch(err => self.postMessage({ type: 'getChunkResponse', requestId, items: [], lastKey: null, error: String(err) }));
    }
};
