# Änderungen

## 2.2.0.1 – Oktober 2026: Nachtrag zum Kontaktformular

- Datenschutzerklärung um den Abschnitt „Kontaktformular“ ergänzt
- Absender und Text im Protokoll werden nach 90 Tagen von einem Hintergrundlauf gelöscht; Prüfung dazu im Systemzustand
- Der Besucherzähler speichert die E-Mail-Adresse des Absenders nicht mehr

## 2.2.0.0 – Oktober 2026: Kontaktformular

- Protokoll aller Nachrichten aus dem Kontaktformular; Auswertung auf der Admin-Seite (Zahlen je Abteilung und Zeitraum, Verlauf)
- Besserer Spam-Schutz ohne Captcha: Zeit bis zum Absenden, Inhalt (Links), Anzahl der Nachrichten
- Die Bestätigung an den Absender enthält den Text der Nachricht nicht mehr

## 2.1.0.0 – Oktober 2026: Modul „Veranstaltungen“

- Veranstaltungen (ein- und mehrtägig) anlegen, veröffentlichen, absagen; Eintrag im Kalender
- Anmeldung ohne Benutzerkonto nur per E-Mail (mit Bestätigungslink), Begleitpersonen, Info-Adressen, Anmeldung für einzelne Tage einstellbar
- Selbstverwaltung über einen persönlichen Link: Daten ändern, umbuchen, abmelden
- Übersicht für Organisatoren: Teilnehmerliste mit Änderungen, ablehnen, manuell anmelden, CSV, Rundmails, Tag absagen
- Benachrichtigungen an Organisatoren und weitere Adressen (sofort oder als Tageszusammenfassung)
- Admins verwalten alle Veranstaltungen, Abteilungsleiter die ihrer Abteilung und die des Gesamtvereins
- Eingeschaltet wird das Modul auf dem Server mit `Veranstaltungen:Aktiviert = true` in der `appsettings.json` (Standard: aus)

## 20.08.2026

- Statische Bilder aktualisiert (neues Dojo, Eingang, Trainingsfläche)
- Logging erweitert
- „Aktuelles“ eingebaut: Blog-System, u. a. für SEO-Content
- „Wissen“ (Themen)
