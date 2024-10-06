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