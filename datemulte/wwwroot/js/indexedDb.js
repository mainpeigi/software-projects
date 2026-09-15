const DB_NAME = 'CsvPlotterDB';
const DB_VERSION = 3;
const STORE_NAME = 'sessions';
const FILE_STORE_NAME = 'files';

let db = null;

window.indexedDbStorage = {
    init: async function() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(DB_NAME, DB_VERSION);

            request.onerror = () => reject(request.error);
            request.onsuccess = () => {
                db = request.result;
                resolve(true);
            };

            request.onupgradeneeded = (event) => {
                const database = event.target.result;
                if(!database.objectStoreNames.contains(STORE_NAME)) {
                    const store = database.createObjectStore(STORE_NAME, { keyPath: 'id' });
                    store.createIndex('name', 'name', { unique: false });
                    store.createIndex('lastModified', 'lastModified', { unique: false});
                }
                if(!database.objectStoreNames.contains(FILE_STORE_NAME)) {
                    const fileStore = database.createObjectStore(FILE_STORE_NAME, { keyPath: 'sessionId' });
                }
            };
        });
    },

    saveSession: async function(sessionJson) {
        if (!db) await this.init();
        const session = JSON.parse(sessionJson);
        
        // Convert PascalCase keys to camelCase for IndexedDB
        if (session.Id && !session.id) {
            session.id = session.Id;
        }
        if (session.Name && !session.name) {
            session.name = session.Name;
        }
        if (session.FileName && !session.fileName) {
            session.fileName = session.FileName;
        }
        if (session.CreatedAt && !session.createdAt) {
            session.createdAt = session.CreatedAt;
        }
        if (session.Data && !session.data) {
            session.data = session.Data;
        }
        
        session.lastModified = new Date().toISOString();

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readwrite');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.put(session);
            request.onsuccess = () => resolve(session.id);
            request.onerror = () => reject(request.error);
        });
    },
    
    loadSession: async function(id) {
        if (!db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readonly');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.get(id);
            request.onsuccess = () => {
                if (!request.result) {
                    resolve(null);
                    return;
                }
                const session = request.result;
                if (session.id && !session.Id) session.Id = session.id;
                if (session.name && !session.Name) session.Name = session.name;
                if (session.fileName && !session.FileName) session.FileName = session.fileName;
                if (session.createdAt && !session.CreatedAt) session.CreatedAt = session.createdAt;
                if (session.data && !session.Data) session.Data = session.data;
                resolve(JSON.stringify(session));
            };
            request.onerror = () => reject(request.error);
        });
    },

    getAllSessions: async function() {
        if (!db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readonly');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.getAll();
            request.onsuccess = () => {
                const sessions = request.result.map(s => ({
                    Id: s.id || s.Id,
                    Name: s.name || s.Name,
                    FileName: s.fileName || s.FileName,
                    CreatedAt: s.createdAt || s.CreatedAt,
                    LastModified: s.lastModified || s.LastModified,
                    DataRowCount: (s.data || s.Data) ? (s.data || s.Data).length : 0
                }));
                resolve(JSON.stringify(sessions));
            };
            request.onerror = () => reject(request.error);
        });
    },

    deleteSession: async function (id) {
        if (!db) await this.init();

        return new Promise ((resolve, reject) => {
            const transaction = db.transaction([STORE_NAME], 'readwrite');
            const store = transaction.objectStore(STORE_NAME);
            const request = store.delete(id);
            request.onsuccess = () => resolve(true);
            request.onerror = () => reject(request.error);
        });
    },

    saveFile: async function(sessionId, fileName, csvText) {
        if(!db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([FILE_STORE_NAME], 'readwrite');
            const store = transaction.objectStore(FILE_STORE_NAME);
            const fileData = {
                sessionId : sessionId,
                fileName : fileName,
                csvText : csvText,
                savedAt: new Date().toISOString()
            };
            const request = store.put(fileData);
            request.onsuccess = () => resolve(true);
            request.onerror = () => reject(request.error);
        });
    },

    loadFile: async function (sessionId) {
        if (!db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([FILE_STORE_NAME], 'readonly');
            const store = transaction.objectStore(FILE_STORE_NAME);
            const request = store.get(sessionId);
            request.onsuccess = () => {
                if (!request.result) {
                    resolve(null);
                    return;
                }
                resolve(request.result);
            };
            request.onerror = () => reject(request.error);
        });
    },

    deleteFile: async function (sessionId) {
        if (!db) await this.init();

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([FILE_STORE_NAME], 'readwrite');
            const store = transaction.objectStore(FILE_STORE_NAME);
            const request = store.delete(sessionId);
            request.onsuccess = () => resolve(true);
            request.onerror = () => reject(request.error);
        });
    },

    parseCsvText: function(csvText) {
        const lines = csvText.split('\n').filter(line => line.trim().length > 0);
        if(lines.length === 0) return { headers: [], rows: [] };

        // Helper function to parse a CSV line handling quoted fields
        const parseCsvLine = (line) => {
            const result = [];
            let current = '';
            let inQuotes = false;

            for (let i = 0; i < line.length; i++) {
                const char = line[i];
                const nextChar = line[i + 1];

                if (inQuotes) {
                    if (char === '"' && nextChar === '"') {
                        // Escaped quote
                        current += '"';
                        i++; // Skip next quote
                    } else if (char === '"') {
                        // End of quoted field
                        inQuotes = false;
                    } else {
                        current += char;
                    }
                } else {
                    if (char === '"') {
                        // Start of quoted field
                        inQuotes = true;
                    } else if (char === ',') {
                        // End of field
                        result.push(current.trim());
                        current = '';
                    } else {
                        current += char;
                    }
                }
            }
            // Don't forget the last field
            result.push(current.trim());
            return result;
        };

        const headers = parseCsvLine(lines[0]);
        const rows = [];

        for(let i=1;i<lines.length; i++)
        {
            const values = parseCsvLine(lines[i]);
            const row = {};
            headers.forEach((header, index) => {
                row[header] = values[index] || '';
            });
            rows.push(row);
        }
        return { headers, rows };
    },

    loadSessionWithFile: async function (sessionId) {
        if (!db) await this.init();

        const sessionJson = await this.loadSession(sessionId);
        if(!sessionJson) return null;

        const session = JSON.parse(sessionJson);

        // For sessions with stored files, try to load the CSV data from the file store
        if(session.hasStoredFile || session.HasStoredFile) {
            console.log('Session has stored file. Attempting to load CSV data from IndexedDB...');

            try {
                const fileData = await this.loadFile(sessionId);
                if (fileData && fileData.csvText) {
                    // Parse the CSV and restore data
                    const parsed = this.parseCsvText(fileData.csvText);
                    if (parsed.rows && parsed.rows.length > 0) {
                        // Convert to GenericCsvRow format (Data property with key-value pairs)
                        session.Data = parsed.rows.map(row => ({ Data: row }));
                        session.AvailableColumns = parsed.headers;
                        console.log(`Restored ${session.Data.length} rows from stored CSV file.`);
                    } else {
                        console.warn('Stored CSV file was empty or could not be parsed.');
                        session.Data = [];
                    }
                } else {
                    console.warn('No stored CSV file found for this session.');
                    session.Data = [];
                    // Clear the flag since file is missing
                    session.hasStoredFile = false;
                    session.HasStoredFile = false;
                }
            } catch (error) {
                console.error('Error loading stored CSV file:', error);
                session.Data = [];
            }
        }

        return JSON.stringify(session);
    },

    // New function to get the stored CSV text for a session
    // This can be used to download the file or process it differently
    getStoredCsvText: async function(sessionId) {
        if (!db) await this.init();

        const fileData = await this.loadFile(sessionId);
        return fileData ? fileData.csvText : null;
    },

    // Download the stored CSV file to user's computer
    downloadStoredCsv: async function(sessionId, fileName) {
        const csvText = await this.getStoredCsvText(sessionId);
        if (!csvText) {
            console.error('No CSV data found for session:', sessionId);
            return false;
        }

        const blob = new Blob([csvText], { type: 'text/csv' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName || 'session_data.csv';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        return true;
    }
};  