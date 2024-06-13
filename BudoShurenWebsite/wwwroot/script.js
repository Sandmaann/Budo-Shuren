
window.getTitle = () => {
    return document.title;
};


window.showSpinner = function () {
    document.getElementById("loading-spinner").style.display = "block";
};

window.hideSpinner = function () {
    document.getElementById("loading-spinner").style.display = "none";
};

window.setScrollPosition = function (position) {
    window.scrollTo(0, position);
}

window.getScrollPosition = function () {
    return window.scrollY;
}

window.saveScrollPositionOnUnload = function (dotnetHelper) {
    window.addEventListener("beforeunload", function () {
        var scrollPosition = window.scrollY;
        dotnetHelper.invokeMethodAsync("SaveScrollPosition", scrollPosition);
    });
}

window.addCapsLockEventListener = function () {
    document.addEventListener('keydown', function (e) {
        var isCapsLockEnabled = e.getModifierState ? e.getModifierState('CapsLock') : e.keyCode === 20;
        var warningElement = document.getElementById('capsLockWarning');
        if (isCapsLockEnabled) {
            warningElement.style.display = 'block';
        } else {
            warningElement.style.display = 'none';
        }
    });
};