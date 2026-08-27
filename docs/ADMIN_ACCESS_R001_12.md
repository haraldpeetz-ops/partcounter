# Partcounter R001.12 – Bediener-/Admin-Trennung

## Ziel

Der reguläre Produktionsbetrieb soll ohne Benutzeranmeldung möglich sein. Konfigurationsänderungen, die Kommunikation, Hardware, Etiketten oder externe Systeme betreffen, werden dagegen durch ein lokales Admin-Passwort geschützt.

## Frei zugängliche Betriebsbereiche

Ohne Login/Passwort nutzbar:

- Leitstand
- Artikelstamm
- VE-Historie
- Auftrag starten, pausieren, fortsetzen und beenden
- manueller VE-Wechsel / betriebliche Maschinenaktionen im Leitstand
- Anzeige des Mini-Monitors und der Produktionszustände

## Geschützte Administrationsbereiche

Admin-Freigabe erforderlich:

- Maschinen / Modbus
- Etiketteneditor einschließlich Logos/Bilder
- Inbetriebnahme / Diagnose
- Rolloutstatus 30 Maschinen
- ARBURG ALS
- Einstellungen / Druck
- Wechsel Simulation ↔ Echtbetrieb

Geschützte Tabs bleiben sichtbar und werden im gesperrten Zustand mit einem Schloss gekennzeichnet. Beim Aufruf wird die Admin-Abfrage geöffnet.

## Erstinbetriebnahme

Beim Programmstart ist kein Login notwendig. Solange noch kein Admin-Passwort existiert, wird beim ersten Zugriff auf einen geschützten Bereich die Einrichtung eines Admin-Passworts verlangt.

Mindestlänge: 8 Zeichen.

Das Passwort wird nicht im Klartext gespeichert. Partcounter speichert nur einen PBKDF2-HMAC-SHA256-Hash mit zufälligem Salt und 210000 Iterationen in:

`%LOCALAPPDATA%\Partcounter\admin_access.json`

## Admin-Sitzung

Nach erfolgreicher Entsperrung bleiben die administrativen Bereiche für die aktuelle Programmsitzung freigegeben. In der Kopfzeile steht dann `Admin sperren`.

Durch Anklicken wird die Administration sofort wieder gesperrt. Befindet sich der Benutzer beim Sperren in einem geschützten Tab, wechselt Partcounter zurück in den frei zugänglichen Betriebsbereich.

Per Rechtsklick auf die Admin-Schaltfläche kann das Admin-Passwort geändert werden.

Nach einem Neustart des Programms ist die Administration grundsätzlich wieder gesperrt.

## Ausfallsicherheit

Die Admin-Sperre betrifft ausschließlich die PC-Bedienoberfläche. Die lokale LOGO!-Zählung und der lokale VE-Wechsel werden dadurch nicht verändert. Modbus Protocol V2 und `Partcounter_LOGO_V001` bleiben unverändert.

Ein beschädigtes Admin-Zugriffsprofil blockiert nicht den normalen Produktionsbetrieb. Administrative Bereiche bleiben in diesem Fall aus Sicherheitsgründen gesperrt und melden den Pfad der betroffenen Zugriffsdatei.

## Sicherheitshinweis

Die Funktion ist eine Anwendungssperre gegen unbeabsichtigte oder unberechtigte Änderungen durch normale Bediener. Sie ersetzt keine Windows-Benutzerverwaltung, Domänenrichtlinien oder physische/IT-seitige Zugriffskontrolle auf den Rechner.
