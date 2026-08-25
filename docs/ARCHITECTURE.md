# Partcounter R001 – Systemarchitektur

## Zielbild

Partcounter trennt bewusst die zeitkritische Maschinenfunktion von der übergeordneten PC-Funktion.

### LOGO!-Ebene je Maschine

- Erfassung des Zyklusimpulses
- lokaler Zyklus- und Teilezähler
- Speicherung der gültigen Auftragsparameter
- Vergleich mit Zielzyklen pro VE
- pneumatischer VE-Wechsel
- Last-Completed-Datensatz für den PC
- Kommunikations-/Fehlerstatus

### PC-Ebene

- Leitstand für 30 Maschinen
- Artikelstamm
- Auftragsparameter
- Berechnung Zielzyklen / Mehrmenge
- Modbus-TCP-Kommunikation
- VE-Historie
- SQLite-Persistenz
- automatischer Etikettendruck
- QR-/Barcode-Erzeugung

## Netzwerk

Empfohlene Trennung in ein eigenes Produktions-/Automatisierungs-VLAN bzw. logisch abgegrenztes Subnetz. Beispiel:

```text
Partcounter PC       192.168.50.10
Access Point         192.168.50.1
LOGO M01             192.168.50.101
...
LOGO M30             192.168.50.130
```

Die WLAN-Geräte an den Maschinen sollten als transparente WLAN-Client-Bridges arbeiten, sodass die Ethernet-Schnittstelle der LOGO! unverändert über IP erreichbar bleibt.

## Kommunikationsmodell

Partcounter startet je freigegebener Maschine einen unabhängigen Worker. Ein Verbindungsfehler an M07 darf dadurch M01–M06 und M08–M30 nicht blockieren.

Pro Worker:

```text
connect/reconnect
  ↓
PC heartbeat schreiben
  ↓
Statusblock lesen
  ↓
Snapshot an UI
  ↓
750 ms Pollintervall
```

Befehle und Polling derselben Maschine werden über ein Semaphore serialisiert, damit auf einer Modbus-Verbindung keine konkurrierenden Telegramme kollidieren.

## Persistenz

SQLite läuft im WAL-Modus. VE-Abschluss und Etikettendruck sind logisch getrennt:

```text
LOGO meldet CompletionSequence neu
        ↓
VE-Datensatz in SQLite speichern
        ↓
UI-Historie aktualisieren
        ↓
falls AutoPrint = true
        ↓
Etikett erzeugen / Druckauftrag senden
        ↓
LabelStatus aktualisieren
```

Dadurch bleibt ein fertiger Behälter auch bei Druckerfehler dokumentiert.

## Etikettenidentität

R001 erzeugt eine eindeutige VE-ID nach dem Schema:

```text
PC-<UTC timestamp>-M<Maschine>-VE<Nummer>
```

Diese ID ist Code-128-Inhalt und Teil des QR-Codes. Sie dient später als stabiler Schlüssel für Nachdruck, Chargenrückverfolgung und ERP/MES-Anbindung.

## Erweiterungspunkte nach R001

- Druckwarteschlange/Nachdruckcenter
- Benutzer/Rollen und Audit-Trail
- Schichtmodelle
- Ausschusskorrekturen
- Gut-/Schlechtteil-Signal statt reinem Zyklusimpuls
- Mehrfachwerkzeuge mit deaktivierten Kavitäten
- ERP/proALPHA-Auftragsimport
- Etikettenvorlagen pro Kunde/Artikel
- ZPL-Direktdruck für Zebra/Druckersprachen
- SQL-Server-Netzwerkbetrieb
- OEE/Stillstandsgrund-Erfassung
- Export CSV/Excel/PDF
- zentrale LOGO!-Konfigurations-/Diagnoseseite

## Safety-Abgrenzung

Partcounter ist Betriebs-/Produktionssoftware und kein Safety-System. Der Verpackungswechsler darf nur in einem hardware- und risikotechnisch zulässigen Maschinenzustand bewegbar sein. Erforderliche sichere Freigaben müssen außerhalb von Partcounter in der dafür geeigneten Maschinen-/Sicherheitssteuerung realisiert werden.
