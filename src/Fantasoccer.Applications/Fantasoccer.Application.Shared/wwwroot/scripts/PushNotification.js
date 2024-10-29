let isSubscribed = false;
let swRegistration = null;

function urlB64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding)
        .replace(/\-/g, '+')
        .replace(/_/g, '/');

    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}

async function subscribeUser(applicationServerPublicKey) {
    const applicationServerKey = urlB64ToUint8Array(applicationServerPublicKey);
    let subscriptionValue = null;
    await swRegistration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: applicationServerKey
    })
        .then(function (subscription) {
            isSubscribed = true;
            subscriptionValue = subscription;
        })
        .catch(function (err) {
            subscriptionValue = null;
        });
    return JSON.stringify(subscriptionValue);
}

async function unsubscribeUser() {
    let subscriptionValue = null;
    await swRegistration.pushManager.getSubscription()
        .then(function (subscription) {
            if (subscription) {
                subscriptionValue = subscription;
                return subscription.unsubscribe();
            }
        })
        .catch(function (error) {
            console.log('Error unsubscribing', error);
        })
        .then(function () {
            isSubscribed = false;
        });
    return JSON.stringify(subscriptionValue);
}
async function isSubscribedToNotification() {
    await swRegistration.pushManager.getSubscription()
        .then(function (subscription) {
            isSubscribed = !(subscription === null);
        });
    return isSubscribed;
}

if ('serviceWorker' in navigator && 'PushManager' in window) {
    console.log('Service Worker and Push is supported');

    navigator.serviceWorker.register('../../../../../../shared/scripts/FCServiceWorker.js')
        .then(function (swReg) {
            console.log('Service Worker is registered', swReg);

            swRegistration = swReg;
        })
        .catch(function (error) {
            console.error('Service Worker Error', error);
        });
} else {
    console.warn('Push messaging is not supported');
}