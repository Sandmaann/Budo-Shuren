
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
};

// Neuigkeiten auf der Startseite: lange Texte sind per CSS gekürzt (siehe Home/Neuigkeiten.razor).
// Wo nichts abgeschnitten ist, wird data-passt gesetzt; dort blendet das CSS Verlauf und "Weiterlesen" aus.
// Gemessen wird neu, sobald sich die Seite ändert (Karussell kommt, Folie auf- oder zugeklappt, Fensterbreite).
(function () {
    var geplant = false;

    function pruefen() {
        geplant = false;
        document.querySelectorAll(".neuigkeit-inhalt:not(.offen)").forEach(function (inhalt) {
            var text = inhalt.querySelector(".neuigkeit-text");
            // Nicht im Layout (z. B. ausgeblendet): Markierung so lassen
            if (!text || inhalt.clientHeight === 0) {
                return;
            }
            var gekuerzt = inhalt.scrollHeight > inhalt.clientHeight + 1 || text.scrollHeight > text.clientHeight + 1;
            inhalt.toggleAttribute("data-passt", !gekuerzt);
        });
    }

    // Höchstens einmal je Bild messen, egal wie viele Änderungen zusammenkommen
    function planen() {
        if (!geplant) {
            geplant = true;
            requestAnimationFrame(pruefen);
        }
    }

    new MutationObserver(planen).observe(document.documentElement, { subtree: true, childList: true, attributes: true, attributeFilter: ["class"] });
    window.addEventListener("resize", planen);
    if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(planen);
    }
    pruefen();
})();

// Bearbeitungsseiten mit Entwurf (EntwurfHinweis.razor): Blazor übernimmt eine Eingabe erst beim Verlassen des Felds.
// Wer mitten im Feld den Tab wechselt, hätte sie nach einem Neuladen verloren. Deshalb beim Verlassen des Tabs melden.
document.addEventListener("visibilitychange", function () {
    if (document.visibilityState !== "hidden" || !document.querySelector("[data-entwurf-sicherung]")) {
        return;
    }
    var feld = document.activeElement;
    // Nicht bei der Dateiauswahl: dort würde "change" denselben Upload noch einmal starten
    if (feld && ((feld.tagName === "INPUT" && feld.type !== "file") || feld.tagName === "TEXTAREA")) {
        feld.dispatchEvent(new Event("change", { bubbles: true }));
    }
});
