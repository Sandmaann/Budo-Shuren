
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

// Seitenleiste zum Bearbeiten von Galeriebildern (GalerieVerwalten.razor, ImageDetail.razor)
window.toggleSlideover = function () {
    document.getElementById('slideover-container').classList.toggle('invisible');
    document.getElementById('slideover-bg').classList.toggle('opacity-0');
    document.getElementById('slideover-bg').classList.toggle('opacity-50');
    document.getElementById('slideover').classList.toggle('translate-x-full');
};

// Galerie: meldet der Seite Beginn und Ende des Scrollens (Galerie.razor, GalerieVerwalten.razor).
// Steht hier und nicht in den Komponenten: ein <script> dort läuft zweimal (vorgerendert und interaktiv).
(function () {
    var scrollTimeout;
    var dotNetObject = null;

    window.addEventListener('scroll', function () {
        clearTimeout(scrollTimeout);
        if (dotNetObject) {
            dotNetObject.invokeMethodAsync('OnScrollStart');
        }

        scrollTimeout = setTimeout(function () {
            if (dotNetObject) {
                dotNetObject.invokeMethodAsync('OnScrollEnd');
            }
        }, 100);
    });

    window.registerScrolling = function (obj) {
        dotNetObject = obj;
    };
})();

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

// Großansicht für Bilder in Veranstaltungen, Aktuelles und Themen: ein Klick auf ein Element mit data-grossbild
// zeigt das Bild über der Seite, mit Knopf zum Schließen (wie das Popup der kleinen Galerie). Der Wert von
// data-grossbild ist die Bildunterschrift (darf leer sein). Als Skript und nicht als Komponente, weil die
// Veranstaltungsseiten statisch gerendert sind. Ohne JavaScript öffnet ein Link das Bild wie bisher.
(function () {
    var ansicht = null;

    function schliessen() {
        if (ansicht) {
            ansicht.remove();
            ansicht = null;
            document.removeEventListener("keydown", taste);
        }
    }

    function taste(e) {
        if (e.key === "Escape") {
            schliessen();
        }
    }

    function oeffnen(url, alt, unterschrift) {
        schliessen();

        ansicht = document.createElement("div");
        ansicht.className = "grossbild";
        ansicht.setAttribute("role", "dialog");
        ansicht.setAttribute("aria-modal", "true");
        ansicht.setAttribute("aria-label", "Bild in voller Größe");

        var rahmen = document.createElement("div");
        rahmen.className = "grossbild-rahmen";

        var knopf = document.createElement("button");
        knopf.type = "button";
        knopf.className = "grossbild-schliessen";
        knopf.title = "Schließen";
        knopf.setAttribute("aria-label", "Schließen");
        knopf.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">close</span>';
        knopf.addEventListener("click", schliessen);

        var bild = document.createElement("img");
        bild.src = url;
        bild.alt = alt;

        rahmen.appendChild(knopf);
        rahmen.appendChild(bild);

        if (unterschrift) {
            var text = document.createElement("p");
            text.className = "grossbild-text";
            text.textContent = unterschrift;
            rahmen.appendChild(text);
        }

        // Ein Klick neben das Bild schließt ebenfalls
        ansicht.addEventListener("click", function (e) {
            if (e.target === ansicht) {
                schliessen();
            }
        });

        ansicht.appendChild(rahmen);
        document.body.appendChild(ansicht);
        document.addEventListener("keydown", taste);
        knopf.focus();
    }

    document.addEventListener("click", function (e) {
        var ausloeser = e.target.closest ? e.target.closest("[data-grossbild]") : null;
        // Mit Strg/Umschalt/mittlerer Taste bleibt es beim normalen Verhalten des Links (neuer Tab)
        if (!ausloeser || e.button || e.ctrlKey || e.metaKey || e.shiftKey) {
            return;
        }
        var bild = ausloeser.tagName === "IMG" ? ausloeser : ausloeser.querySelector("img");
        var url = ausloeser.getAttribute("href") || (bild && (bild.currentSrc || bild.src));
        if (!url) {
            return;
        }
        e.preventDefault();
        oeffnen(url, bild ? bild.alt : "", ausloeser.getAttribute("data-grossbild"));
        // In der Capture-Phase: sonst ist Blazors eigene Link-Navigation (auch am document) vorher dran und
        // wechselt zum Bild, bevor preventDefault gesetzt ist
    }, true);
})();

// Karussell (SfCarousel, z. B. Neuigkeiten): beim Wischen verschiebt Syncfusion die Folien mit einem transform im
// style-Attribut und nimmt es wieder weg, sobald es die Geste als Wischen erkannt hat. Erkennt es sie nicht (oder
// kommt die Antwort des Servers nicht), bliebe das Karussell zwischen zwei Folien stehen. Dann hier einrasten:
// ohne das transform gilt wieder die Position der aktuellen Folie.
(function () {
    var wartend = [];

    function einrasten(karussell) {
        var folien = karussell.querySelector(".e-carousel-items");
        if (folien && folien.style.transform) {
            // Syncfusion räumt die Dauer nach dem Übergang selbst wieder ab (transitionend)
            folien.style.transitionDuration = "0.3s";
            folien.style.transform = "";
            karussell.classList.remove("e-translate");
        }
    }

    function abbrechen() {
        wartend.forEach(clearTimeout);
        wartend = [];
    }

    function losgelassen(e) {
        var karussell = e.target.closest ? e.target.closest(".e-carousel") : null;
        if (!karussell || e.touches.length > 0) {
            return;
        }
        abbrechen();
        // Kurz warten, bis Syncfusion die Geste ausgewertet hat. "e-translate" heißt: als Wischen erkannt,
        // der Wechsel läuft. Fehlt die Klasse, wurde nichts erkannt.
        wartend.push(setTimeout(function () {
            if (!karussell.classList.contains("e-translate")) {
                einrasten(karussell);
            }
        }, 60));
        // Wechsel erkannt, aber nicht abgeschlossen
        wartend.push(setTimeout(function () {
            einrasten(karussell);
        }, 2000));
    }

    // Ein neuer Griff ins Karussell: nicht mitten in die Bewegung einrasten
    document.addEventListener("touchstart", function (e) {
        if (e.target.closest && e.target.closest(".e-carousel")) {
            abbrechen();
        }
    }, { capture: true, passive: true });
    document.addEventListener("touchend", losgelassen, { capture: true, passive: true });
    document.addEventListener("touchcancel", losgelassen, { capture: true, passive: true });
})();

// Verbindung zum Server (Blazor) unterbrochen, z. B. nach einem Tab-Wechsel auf dem Handy: Blazor meldet den Stand
// am Element components-reconnect-modal (App.razor). Statt eines Dialogs, der die Seite sperrt:
// - sofort neu verbinden, sobald der Tab wieder sichtbar oder das Netz wieder da ist (nicht erst beim nächsten Versuch)
// - kennt der Server die Sitzung nicht mehr, die Seite neu laden: sie ist vorgerendert und damit gleich wieder da
(function () {
    var hinweis = document.getElementById("components-reconnect-modal");
    if (!hinweis) {
        return;
    }

    var getrennt = false;
    var laeuft = false;

    async function verbinden() {
        if (!getrennt || laeuft || document.visibilityState !== "visible") {
            return;
        }
        laeuft = true;
        try {
            var verbunden = await Blazor.reconnect();
            // false: der Server ist erreichbar, hat die Sitzung aber verworfen
            if (!verbunden && getrennt) {
                var fortgesetzt = typeof Blazor.resumeCircuit === "function" && await Blazor.resumeCircuit();
                if (!fortgesetzt) {
                    location.reload();
                }
            }
        } catch (fehler) {
            // Server nicht erreichbar: der Hinweis bleibt stehen, der nächste Anlass versucht es wieder
        } finally {
            laeuft = false;
        }
    }

    hinweis.addEventListener("components-reconnect-state-changed", function (e) {
        var stand = e.detail.state;
        if (stand === "hide") {
            getrennt = false;
        } else if (stand === "rejected" || stand === "resume-failed") {
            location.reload();
        } else {
            getrennt = true;
        }
    });

    document.addEventListener("visibilitychange", verbinden);
    window.addEventListener("online", verbinden);
})();
