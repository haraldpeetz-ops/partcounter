# Partcounter

**Revision:** R001 – Industrial Core  
**Plattform:** Windows / C# / .NET 8 / WPF  
**Anlage:** bis zu 30 Spritzgussmaschinen · Siemens LOGO! · Modbus TCP · WLAN/LAN

Partcounter überwacht den Füllgrad von Verpackungseinheiten (VE) an bis zu 30 Spritzgussmaschinen. Jede Maschine liefert einen Zyklusimpuls an eine Siemens LOGO!. Die LOGO! zählt lokal, berücksichtigt die aktive Kavitätenzahl und schaltet bei voller VE über ein pneumatisches Ventil den Verpackungswechsler. Die PC-Anwendung ist Leitstand, Artikel-/Auftragsverwaltung, Historie und Etikettendruck.

## Zentrales Sicherheits- und Verfügbarkeitsprinzip

**Zählen und automatischer VE-Wechsel laufen lokal in der LOGO!.** Der PC gibt Parameter vor und liest Statusdaten. Ein kurzer PC-, LAN- oder WLAN-Ausfall darf deshalb keinen Zyklus verlieren und keinen fälligen Kistenwechsel verhindern.

## Rundungsregel für Verpackungseinheiten

Es werden ausschließlich vollständige Maschinenzyklen gezählt. Ist die gewünschte VE-Menge nicht durch die Kavitätenzahl teilbar, wird auf den nächsten vollständigen Zyklus aufgerundet:

```text
Zyklen je VE        = ceil(VE-Soll / Kavitäten)
Tatsächliche VE     = Zyklen je VE × Kavitäten
Mehrmenge           = Tatsächliche VE - VE-Soll
```

Beispiel: **1.000 Stück Soll / 64 Kavitäten = 16 Zyklen = 1.024 Stück tatsächlich**.

## R001 – umgesetzt

- WPF-Leitstand für 30 Maschinen
- Simulation ohne Hardware
- Echtbetrieb über Modbus TCP
- unabhängige Kommunikationsworker pro Maschine
- feste LOGO!-IP-Adressen im Maschinenstamm
- Online-/Offline-Status
- PC- und LOGO!-Heartbeat
- Befehlssequenz/Acknowledge-Konzept
- Artikelstamm in SQLite
- Auswahl des Artikels über Artikelnummer
- Werkzeugnummer je Artikel
- Kavitätenzahl 1–64 je Artikel
- Standard-Stückzahl je Verpackungseinheit je Artikel
- automatische Berechnung von Zyklen, effektiver VE-Menge und Mehrmenge
- Auftragsnummer je Maschinenauftrag
- lokaler LOGO!-Teile-/Zykluszähler als Zielarchitektur
- automatischer VE-Wechsel
- manueller VE-Wechsel
- VE-Historie mit Soll/Ist/Mehrmenge
- eindeutige VE-ID
- automatischer Etikettentrigger nach abgeschlossenem VE-Wechsel
- Code-128 auf Basis der VE-ID
- QR-Code mit VE, Maschine, Artikel, Werkzeug, Menge und Zeitstempel
- frei konfigurierbarer Windows-Etikettendrucker
- offene Druckaufträge bleiben als `PendingPrinter` nachvollziehbar
- SQLite im WAL-Modus
- Ereignisprotokoll
- GitHub-Actions-Build auf Windows

## Architektur

```text
Maschine/Zyklussignal
       │
       ▼
 Siemens LOGO! ──────► Pneumatikventil / VE-Wechsler
       │
       │ Ethernet
       ▼
 WLAN-Client/Bridge )))) Access Point ─── LAN ─── Partcounter-PC
                                                   │
                              ┌────────────────────┼─────────────────────┐
                              ▼                    ▼                     ▼
                         WPF-Leitstand           SQLite            Etikettendruck
```

Die 30 LOGO!-Stationen sollten feste IP-Adressen erhalten, z. B. `192.168.50.101` bis `192.168.50.130`; der PC kann z. B. `192.168.50.10` verwenden.

## Datenbank

Beim ersten Start wird `%LOCALAPPDATA%\Partcounter\partcounter.db` angelegt. Enthalten sind:

- `Machines`
- `Articles`
- `PackagingUnits`
- `Settings`
- `Events`

R001 legt 30 Maschinen und zwei deutlich als `DEMO-*` gekennzeichnete Beispielartikel an. Reale Artikel werden direkt im Reiter **Artikelstamm** gepflegt.

## Etikettendruck

Beim Abschluss einer VE wird zuerst der VE-Datensatz gespeichert. Anschließend wird – sofern aktiviert – das Etikett gedruckt. Dadurch bleibt die Produktion auch bei fehlendem oder ausgeschaltetem Drucker nachvollziehbar.

Das R001-Etikett enthält:

- Maschine
- VE-Nummer
- Auftragsnummer
- Artikelnummer / Bezeichnung
- Werkzeugnummer
- Kavitäten
- Sollmenge
- tatsächliche Menge
- zyklusbedingte Mehrmenge
- Fertigstellungszeit
- eindeutige VE-ID
- QR-Code
- Code 128

## Modbus

Die verbindliche Partcounter-V1-Registerbelegung steht in [`docs/MODBUS_REGISTER_MAP.md`](docs/MODBUS_REGISTER_MAP.md). Die LOGO!-Ablauflogik steht in [`docs/LOGO_CONTROL_LOGIC.md`](docs/LOGO_CONTROL_LOGIC.md).

## Build

1. `Partcounter.sln` mit Visual Studio 2022 öffnen.
2. NuGet-Pakete wiederherstellen.
3. `Partcounter.App` starten.
4. Standardmäßig im Simulationsmodus testen.
5. Erst nach I/O- und Kommunikationsprüfung den Echtbetrieb aktivieren.

Verwendete Pakete:

- NModbus 3.0.83
- Microsoft.Data.Sqlite 8.0.30
- ZXing.Net 0.16.11

## Inbetriebnahmehinweis

Partcounter ist keine Sicherheitssteuerung. Maschinen-Sicherheitsfunktionen müssen vollständig in den vorhandenen sicheren Maschinenkreisen verbleiben. Der pneumatische Verpackungswechsler muss so ausgelegt und validiert sein, dass Neustart, Kommunikationsverlust, Spannungsausfall oder ein fehlerhaftes Datentelegramm keine gefährliche Bewegung auslösen. Vor Produktivbetrieb sind Risikobeurteilung, I/O-Prüfung und Freigabe je Maschine erforderlich.
