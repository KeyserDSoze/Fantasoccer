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