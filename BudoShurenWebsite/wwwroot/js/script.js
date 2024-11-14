
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


//window.addCapsLockEventListener = function () {
//    document.addEventListener('keydown', function (e) {
//        var isCapsLockEnabled = e.getModifierState && e.getModifierState('CapsLock');
//        var warningElement = document.getElementById('capsLockWarning');
//        if (isCapsLockEnabled) {
//            warningElement.style.display = 'block';
//        } else {
//            warningElement.style.display = 'none';
//        }
//        // Call the .NET method to update the Caps Lock state
//        DotNet.invokeMethodAsync('BudoShurenWebsite', 'UpdateCapsLockState', isCapsLockEnabled);
//    });
//};

//e.keyCode is deprecated, test above code
//window.addCapsLockEventListener = function () {
//    document.addEventListener('keydown', function (e) {
//        var isCapsLockEnabled = e.getModifierState ? e.getModifierState('CapsLock') : e.keyCode === 20;
//        var warningElement = document.getElementById('capsLockWarning');
//        if (isCapsLockEnabled) {
//            warningElement.style.display = 'block';
//        } else {
//            warningElement.style.display = 'none';
//        }
//    });
//};


    

//lazy-loading für Bilder. Verwende ich aktuell NICHT
//document.addEventListener("DOMContentLoaded", function () {
//    var lazyloadImages = document.querySelectorAll("img.lazyload");
//    var lazyloadThrottleTimeout;

//    function lazyload() {
//        if (lazyloadThrottleTimeout) {
//            clearTimeout(lazyloadThrottleTimeout);
//        }

//        lazyloadThrottleTimeout = setTimeout(function () {
//            var scrollTop = window.pageYOffset;
//            lazyloadImages.forEach(function (img) {
//                if (img.offsetTop < (window.innerHeight + scrollTop)) {
//                    img.src = img.dataset.src;
//                    img.classList.remove('lazyload');
//                }
//            });
//            if (lazyloadImages.length == 0) {
//                document.removeEventListener("scroll", lazyload);
//                window.removeEventListener("resize", lazyload);
//                window.removeEventListener("orientationChange", lazyload);
//            }
//        }, 20);
//    }

//    document.addEventListener("scroll", lazyload);
//    window.addEventListener("resize", lazyload);
//    window.addEventListener("orientationChange", lazyload);
//});