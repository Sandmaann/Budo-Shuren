
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

window.neuigkeitenDialogLifecycle = window.neuigkeitenDialogLifecycle || {
    visibilityHandler: null,
    pageHideHandler: null,

    register: function (dotNetHelper) {
        this.unregister();

        this.visibilityHandler = function () {
            if (document.hidden) {
                dotNetHelper.invokeMethodAsync("HandlePageHiddenAsync");
            }
        };

        this.pageHideHandler = function () {
            dotNetHelper.invokeMethodAsync("HandlePageHiddenAsync");
        };

        document.addEventListener("visibilitychange", this.visibilityHandler);
        window.addEventListener("pagehide", this.pageHideHandler);
    },

    unregister: function () {
        if (this.visibilityHandler) {
            document.removeEventListener("visibilitychange", this.visibilityHandler);
            this.visibilityHandler = null;
        }

        if (this.pageHideHandler) {
            window.removeEventListener("pagehide", this.pageHideHandler);
            this.pageHideHandler = null;
        }
    }
};