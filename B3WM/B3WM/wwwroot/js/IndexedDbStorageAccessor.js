let dbInstance = null;
let DATABASE_NAME = null;
let CURRENT_VERSION = null;

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

            stores.forEach(storeName => {

                if (!db.objectStoreNames.contains(storeName)) {
                    db.createObjectStore(storeName, {
                        keyPath: "id"
                    });
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
