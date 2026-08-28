# Partcounter R001.17 – Etiketten-Nachdruck und Nachdruckjournal

Stand: 28.08.2026

## Zweck

R001.17 ergänzt die VE-Historie um einen kontrollierten Nachdruck bereits gedruckter VE-Etiketten. Der Nachdruck erzeugt **keine neue Verpackungseinheit**, verändert **keine Produktionsmenge** und vergibt **keine neue VE-ID**.

## Bedienung

1. Register **VE-Historie** öffnen.
2. Die gewünschte abgeschlossene und bereits gedruckte VE markieren.
3. **Etikett nachdrucken…** wählen.
4. Nachdruckgrund auswählen und optional eine Bemerkung erfassen.
5. **Nachdruck ausführen** bestätigen.
6. Partcounter übergibt das Etikett an den aktuell in den Druckeinstellungen hinterlegten Windows-Drucker.

Alternativ stehen die Funktionen über das Kontextmenü der VE-Historie zur Verfügung.

## Rückverfolgbarkeit

Jeder Nachdruckversuch wird in `LabelReprintJournal` protokolliert mit:

- VE-ID
- laufender Nachdrucknummer
- Datum/Uhrzeit
- Windows-Druckername
- Nachdruckgrund
- Erfolg/Fehler
- Fehlermeldung bei nicht erfolgreicher Übergabe

Zusätzlich wird ein Event `LABEL_REPRINT_OK` bzw. `LABEL_REPRINT_ERROR` im allgemeinen Partcounter-Ereignisprotokoll gespeichert.

## Originaldaten und Layout

Der Nachdruck verwendet den **ursprünglichen VE-Datensatz** aus der Historie:

- Maschine
- VE-Nummer und VE-ID
- Auftrag
- Artikel und Artikelbezeichnung
- Werkzeug
- Kavitäten
- Soll-/Ist-Menge und Mehrmenge
- Abschlussgrund
- ursprünglicher VE-Abschlusszeitpunkt

Das Etikett wird mit dem **aktuell für diesen Datensatz aufgelösten Etikettenlayout** gerendert. R001.17 archiviert noch keinen historischen Layout-Snapshot des Originaldrucks. Wurde das Layout nach dem Originaldruck geändert, können sich daher Gestaltung oder statische Layoutinhalte beim Nachdruck unterscheiden, obwohl die historischen VE-Daten unverändert bleiben.

## Schutz gegen Fehlbedienung

- Nachdruck ist nur aktiv, wenn der VE-Datensatz als bereits gedruckt markiert ist (`PrintedAtUtc` vorhanden oder `LabelStatus = Printed`).
- Vor jedem Nachdruck wird ein Grund erfasst.
- Fehlgeschlagene Druckversuche werden ebenfalls protokolliert.
- Die bestehende `PackagingUnits`-Historie bleibt unverändert.
- Der Nachdruck greift nicht auf Modbus, LOGO!, Zähler oder VE-Wechsler zu.

## Datenbankmigration

Die Tabelle `LabelReprintJournal` wird beim Start der R001.17-Funktion mit `CREATE TABLE IF NOT EXISTS` angelegt. Bestehende Partcounter-Datenbanken werden nicht gelöscht oder neu erstellt.

## Empfohlener Funktionstest

1. Test-VE erzeugen und Originaletikett erfolgreich drucken.
2. VE in der Historie markieren.
3. Nachdruckgrund `Etikett verloren` auswählen.
4. Nachdruck ausführen und Druckbild gegen Originaldaten prüfen.
5. Druckjournal öffnen: Nachdruck #1, Drucker, Zeitpunkt, Grund und Erfolg müssen vorhanden sein.
6. Einen absichtlich falschen Druckernamen testen: Fehler muss im Journal erscheinen, ohne die VE-Historie zu verändern.
7. Drucker korrigieren und erneut drucken: neuer Nachdruckversuch wird separat protokolliert.

## Abgrenzung

R001.17 ist ein dokumentierter Ersatzdruck vorhandener VE-Etiketten. Für eine vollständig pixelidentische historische Reproduktion müsste Partcounter zukünftig zusätzlich das beim Originaldruck verwendete Layout inklusive eingebetteter Bilder/Logos als unveränderlichen Layout-Snapshot archivieren.
