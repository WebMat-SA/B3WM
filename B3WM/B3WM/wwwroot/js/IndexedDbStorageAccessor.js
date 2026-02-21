let dbInstance = null;
let DATABASE_NAME = null;
let CURRENT_VERSION = null;

// Web Worker para putBatch e getChunk (não bloqueia main thread)
let _ticksWorker = null;
let _getChunkRequestId = 0;
const _getChunkPending = new Map();

function getTicksWorker() {
    if (!_ticksWorker) {
        _ticksWorker = new Worker(new URL('./IndexedDbWorker.js', import.meta.url));
        _ticksWorker.onmessage = (e) => {
            const msg = e.data;
            if (msg && msg.type === 'getChunkResponse' && msg.requestId != null) {
                const pending = _getChunkPending.get(msg.requestId);
                _getChunkPending.delete(msg.requestId);
                if (pending) {
                    if (msg.error) pending.reject(new Error(msg.error));
                    else pending.resolve({ items: msg.items ?? [], lastKey: msg.lastKey ?? null });
                }
            }
        };
    }
    return _ticksWorker;
}

/**
 * Envia ticks para o worker salvar no IndexedDB. Retorna imediatamente (fire-and-forget).
 * A escrita pesada ocorre em thread separada.
 */
export function postTicksToWorker(storeName, values) {
    if (!values || values.length === 0) return;
    getTicksWorker().postMessage({ type: 'putBatch', store: storeName, data: values });
}

/**
 * Lê um chunk do IndexedDB via Web Worker (thread separada).
 * Retorna Promise<{ items, lastKey }>.
 * @param {string} storeName - Nome da store
 * @param {number} chunkSize - Tamanho do bloco
 * @param {string} afterKey - Para paginação: id (modo normal) ou time ISO (modo minDate)
 * @param {object} options - { minDate?: string (ISO) - só ticks com time >= minDate }
 */
export function getChunkFromWorker(storeName, chunkSize, afterKey = '', options = {}) {
    return new Promise((resolve, reject) => {
        const requestId = ++_getChunkRequestId;
        _getChunkPending.set(requestId, { resolve, reject });
        getTicksWorker().postMessage({
            type: 'getChunk',
            requestId,
            store: storeName,
            chunkSize: chunkSize ?? 5000,
            afterKey: afterKey || '',
            minDate: options.minDate ?? null
        });
    });
}

// ------------------------------
// Initialize Database
// stores: array of store names
// version: database version
// ------------------------------
export function initialize(databaseName, version, stores) {

    DATABASE_NAME = databaseName;
    CURRENT_VERSION = version;

    return new Promise((resolve, reject) => {

        let request = indexedDB.open(DATABASE_NAME, CURRENT_VERSION);

        request.onupgradeneeded = function (event) {

            let db = event.target.result;
            let tx = event.target.transaction;

            stores.forEach(storeName => {

                let store;
                if (!db.objectStoreNames.contains(storeName)) {
                    store = db.createObjectStore(storeName, { keyPath: "id" });
                } else {
                    store = tx.objectStore(storeName);
                }
                if (storeName === 'Ticks' && !store.indexNames.contains('byTime')) {
                    store.createIndex('byTime', 'time', { unique: false });
                }
            });
        };

        request.onsuccess = function (event) {
            dbInstance = event.target.result;
            resolve(true);
        };

        request.onerror = function (event) {
            reject(event);
        };
    });
}

// ------------------------------
// Internal DB Getter
// ------------------------------
function getDb() {

    return new Promise((resolve, reject) => {

        if (dbInstance) {
            resolve(dbInstance);
            return;
        }

        let request = indexedDB.open(DATABASE_NAME, CURRENT_VERSION);

        request.onsuccess = function (event) {
            dbInstance = event.target.result;
            resolve(dbInstance);
        };

        request.onerror = reject;
    });
}

// ------------------------------
// ADD (fails if exists)
// ------------------------------
export async function add(storeName, value) {

    let db = await getDb();

    return new Promise((resolve, reject) => {

        let tx = db.transaction(storeName, "readwrite");
        let store = tx.objectStore(storeName);

        let request = store.add(value);

        request.onsuccess = () => resolve(true);
        request.onerror = reject;
    });
}

// ------------------------------
// PUT (AddOrUpdate)
// ------------------------------
export async function put(storeName, value) {

    let db = await getDb();

    return new Promise((resolve, reject) => {

        let tx = db.transaction(storeName, "readwrite");
        let store = tx.objectStore(storeName);

        let request = store.put(value);

        request.onsuccess = () => resolve(true);
        request.onerror = reject;
    });
}

// ------------------------------
// PUT BATCH (uma transação, vários puts – add/update por keyPath "id", sem duplicar)
// KeyPath é case-sensitive: objeto DEVE ter propriedade "id" (lowercase).
// ------------------------------
export async function putBatch(storeName, values) {

    if (!values || values.length === 0) {
        return Promise.resolve(true);
    }

    let db = await getDb();

    return new Promise((resolve, reject) => {

        let tx = db.transaction(storeName, "readwrite");
        let store = tx.objectStore(storeName);

        tx.oncomplete = () => resolve(true);
        tx.onerror = () => reject(tx.error);

        for (let i = 0; i < values.length; i++) {
            const obj = values[i];
            // IndexedDB keyPath "id" é case-sensitive; normaliza Id/ID -> id
            if (obj && obj.id === undefined) {
                obj.id = obj.Id ?? obj.trydID ?? String(i);
            }
            store.put(obj);
        }
    });
}

// ------------------------------
// GET by key
// ------------------------------
export async function get(storeName, key) {

    let db = await getDb();

    return new Promise((resolve, reject) => {

        let tx = db.transaction(storeName, "readonly");
        let store = tx.objectStore(storeName);

        let request = store.get(key);

        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = reject;
    });
}

// ------------------------------
// GET ALL
// ------------------------------
export async function getAll(storeName) {

    let db = await getDb();

    return new Promise((resolve, reject) => {

        let tx = db.transaction(storeName, "readonly");
        let store = tx.objectStore(storeName);

        let request = store.getAll();

        request.onsuccess = () => resolve(request.result ?? []);
        request.onerror = reject;
    });
}

// ------------------------------
// GET CHUNK (paginação para evitar congelar UI com muitos dados)
// Retorna { items, lastKey } - lastKey para passar em afterKey na próxima chamada.
// ------------------------------
export async function getChunk(storeName, chunkSize, afterKey = null) {

    let db = await getDb();

    return new Promise((resolve, reject) => {

        let tx = db.transaction(storeName, "readonly");
        let store = tx.objectStore(storeName);

        let range = (afterKey != null && afterKey !== '') ? IDBKeyRange.lowerBound(afterKey, true) : null;
        let request = store.getAll(range, chunkSize);

        request.onsuccess = () => {
            let items = request.result ?? [];
            let lastKey = items.length > 0 ? (items[items.length - 1].id ?? null) : null;
            resolve({ items, lastKey });
        };
        request.onerror = () => reject(request.error);
    });
}

// ------------------------------
// DELETE by key
// ------------------------------
export async function remove(storeName, key) {

    let db = await getDb();

    return new Promise((resolve, reject) => {

        let tx = db.transaction(storeName, "readwrite");
        let store = tx.objectStore(storeName);

        let request = store.delete(key);

        request.onsuccess = () => resolve(true);
        request.onerror = reject;
    });
}

// ------------------------------
// CLEAR store
// ------------------------------
export async function clear(storeName) {

    let db = await getDb();

    return new Promise((resolve, reject) => {

        let tx = db.transaction(storeName, "readwrite");
        let store = tx.objectStore(storeName);

        let request = store.clear();

        request.onsuccess = () => resolve(true);
        request.onerror = reject;
    });
}

// ------------------------------
// DELETE DATABASE
// ------------------------------
export function deleteDatabase(databaseName) {

    return new Promise((resolve, reject) => {

        let request = indexedDB.deleteDatabase(databaseName);

        request.onsuccess = () => resolve(true);
        request.onerror = reject;
    });
}
