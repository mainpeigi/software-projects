window.downloadFile = function (filename, contentType, base64Content) {
    const linkSource = `data:${contentType};base64,${base64Content}`;
    const downloadLink = document.createElement('a');
    downloadLink.href = linkSource;
    downloadLink.download = filename;
    downloadLink.click();
}

window.getLocalStorageSize = function () {
    try {
        return JSON.stringify(localStorage);
    } catch (e) {
        return "{}";
    }
}

window.readClipboardText = async function () {
    try {
        if (!navigator.clipboard) {
            return { success: false, error: "Clipboard API not available. Make sure you're using HTTPS." };
        }
        
        // Check if document is focused
        if (!document.hasFocus()) {
            return { success: false, error: "Please click on the page first, then try again." };
        }
        
        const text = await navigator.clipboard.readText();
        return { success: true, text: text };
    } catch (err) {
        if (err.name === 'NotAllowedError') {
            return { success: false, error: "Clipboard access denied. Click on the page and try again." };
        }
        return { success: false, error: err.message || "Failed to read clipboard" };
    }
}

window.setupPasteHandler = function (dotNetRef) {
    document.addEventListener('paste', async function(e) {
        const text = e.clipboardData.getData('text/plain');
        if (text) {
            await dotNetRef.invokeMethodAsync('OnPasteFromJs', text);
        }
    });
}

window.triggerPasteCapture = function() {
    return new Promise((resolve) => {
        const textarea = document.createElement('textarea');
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        textarea.style.top = '0';
        document.body.appendChild(textarea);
        textarea.focus();
        
        textarea.addEventListener('paste', function(e) {
            const text = e.clipboardData.getData('text/plain');
            document.body.removeChild(textarea);
            resolve({ success: true, text: text });
        });
        
        const result = document.execCommand('paste');
        
        setTimeout(() => {
            if (document.body.contains(textarea)) {
                document.body.removeChild(textarea);
                resolve({ success: false, error: "Press Ctrl+V now to paste" });
            }
        }, 3000);
    });
}

window.getTextAreaValue = function(element) {
    if (element && element.value !== undefined) {
        return element.value;
    }
    return '';
}

window.clearTextArea = function(element) {
    if (element && element.value !== undefined) {
        element.value = '';
    }
}
window.setupDropZone = function(dropZoneId, inputId) {
    const dropZone = document.getElementById(dropZoneId);
    if (!dropZone) return;

    dropZone.addEventListener('drop', function(e) {
        e.preventDefault();
        if(e.dataTransfer.files.length > 0) {
            const inputFile = document.getElementById(inputId);
            inputFile.files = e.dataTransfer.files;
            inputFile.dispatchEvent(new Event('change', { bubbles: true }));
        }
    });
};
