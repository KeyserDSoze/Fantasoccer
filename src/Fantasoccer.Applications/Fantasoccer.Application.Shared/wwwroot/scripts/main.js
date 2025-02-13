function getUseragent() {
    return navigator.userAgent;
}
function setClass(id, className) {
    document.getElementById(id).classList.add(className);
    return true;
}
function removeClass(id, className) {
    document.getElementById(id).classList.remove(className);
    return true;
}

function checkConnection(dotNetHelper) {
    document.addEventListener("visibilitychange", async function () {
        if (document.visibilityState === 'visible') {
            try {
                await dotNetHelper.invokeMethodAsync('CheckConnection');
            } catch (error) {
                window.location.reload();
            }
        }
    });
}

window.setInterval(() => {
    const reconnectElement = document.getElementById("components-reconnect-modal");
    const uiError = document.getElementById("blazor-error-ui");
    if (reconnectElement != null) {
        if (reconnectElement.style.display == 'block') {
            window.location.reload();
        }
    }
    if (uiError != null) {
        if (uiError.style.display == 'block') {
            window.location.reload();
        }
    }
}, 1000);

window.intervalManager = {
    intervals: [],
    addInterval: function (dotNetHelper, methodToCall, milliseconds) {
        const interval = window.setInterval(() => {
            return dotNetHelper.invokeMethodAsync(methodToCall);
        }, milliseconds);
        this.intervals.push(interval);
        return this.intervals.length - 1;
    },
    removeInterval: function (index) {
        const interval = this.intervals.splice(index, 1);
        window.clearInterval(interval);
    }
}

window.downloadFile = (filename, contentType, content) => {
    const file = new Blob([content], { type: contentType });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(file);
    a.download = filename;
    a.click();
    URL.revokeObjectURL(a.href);
};