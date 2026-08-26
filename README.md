# Partcounter

**Revision:** R001.7 – LOGO V001 / Modbus V2  
**Plattform:** Windows / C# / .NET 8 / WPF  
**Anlage:** bis zu 30 Spritzgussmaschinen · Siemens LOGO! · Modbus TCP · WLAN/LAN

Partcounter überwacht den Füllgrad von Verpackungseinheiten (VE) an bis zu 30 Spritzgussmaschinen. Jede Maschine liefert einen Zyklusimpuls an eine Siemens LOGO!. Die LOGO! zählt lokal und schaltet bei voller VE über ein pneumatisches Ventil den Verpackungswechsler. Die PC-Anwendung ist Leitstand, Artikel-/Auftragsverwaltung, Historie, ARBURG-ALS-Integration und Etikettendruck.

## Zentrales Sicherheits- und Verfügbarkeitsprinzip

**Zählen und automatischer VE-Wechsel laufen lokal in der LOGO!.** Der PC gibt Parameter vor und liest Statusdaten. Ein kurzer PC-, LAN- oder WLAN-Ausfall darf deshalb keinen Zyklus verlieren und keinen fälligen Kistenwechsel verhindern.

Partcounter und die Standard-LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztüren, Maschinenfreigaben und weitere Safety-Funktionen verbleiben vollständig in den dafür vorgesehenen sicheren Maschinenkreisen.

## R001.7 – wichtigste Änderung

Partcounter verwendet ab R001.7 das **Modbus-Protokoll V2**. Die LOGO! überträgt die nativen VE- und Gesamtzykluszähler als DWORD-Werte. Die PC-Anwendung berechnet daraus die Teilezahl mit der aktiven Kavitätenzahl.

```text
CurrentParts            = CurrentVECycles × ActiveCavitiesEcho
LastCompletedVEQuantity = LastCompletedVECycles × LastCompletedCavities
```

Dadurch muss die LOGO! keine potenziell überlaufende 16-Bit-Analogmultiplikation `Zyklen × Kavitäten` ausführen. Außerdem wird beim PC-Neustart die lokale `CommandSequence` zuerst mit dem von der LOGO! gemeldeten `AckSequence` synchronisiert. Der erste Steuerbefehl nach einem Neustart kann dadurch nicht versehentlich als bereits bearbeitetes Duplikat verworfen werden.

**R001.7 erwartet ProtocolVersion 2 und das passende `Partcounter_LOGO_V001`. Ein älteres V1-LOGO!-Programm darf nicht produktiv mit R001.7 betrieben werden.**

## Rundungsregel für Verpackungseinheiten

Es werden ausschließlich vollständige Maschinenzyklen gezählt. Ist die gewünschte VE-Menge nicht durch die Kavitätenzahl teilbar, wird auf den nächsten vollständigen Zyklus aufgerundet:

```text
Zyklen je VE        = ceil(VE-Soll / Kavitäten)
Tatsächliche VE     = Zyklen je VE × Kavitäten
Mehrmenge           = Tatsächliche VE - VE-Soll
```

Beispiel: **1.000 Stück Soll / 64 Kavitäten = 16 Zyklen = 1.024 Stück tatsächlich**.

## Umgesetzter Funktionsstand

- WPF-Leitstand für bis zu 30 Maschinen
- Simulation ohne Hardware und Echtbetrieb über Modbus TCP
- unabhängige Kommunikationsworker pro Maschine
- feste LOGO!-IP-Adressen und Modbus-Konfiguration im Maschinenstamm
- Online-/Offline-Status sowie PC- und LOGO!-Heartbeat
- restart-sichere CommandSequence-/AckSequence-Verarbeitung
- Artikelstamm in SQLite
- Werkzeugnummer, Kavitätenzahl 1–64 und Standard-VE je Artikel
- Auftragsnummer, Auftragsmenge und Auftragsfortschritt
- dynamische letzte Teil-VE eines Produktionsauftrags
- Auftrag starten, pausieren, fortsetzen und beenden
- temporäres Deaktivieren und Reaktivieren von Maschinen
- Maschinen ohne Auftrag standardmäßig ausblendbar
- Füllgrad-Ampel mit Vorwarnung und Fokus bei voller VE
- Always-on-Top-Mini-Monitor
- automatische und manuelle VE-Wechsel
- VE-Historie mit Soll/Ist/Mehrmenge und eindeutiger VE-ID
- automatischer Etikettentrigger nach VE-Abschluss
- QR-Code und Code 128
- frei konfigurierbarer Windows-Etikettendrucker
- offene Druckaufträge bleiben nachvollziehbar
- SQLite im WAL-Modus und Ereignisprotokoll
- ARBURG-ALS-Datei-/Hotfolder-Import für XLSX/XLSM/CSV/TXT/TSV
- ARBURG-ALS-REST/JSON-Modus mit Mapping, Basic/Bearer/API-Key und optionalem Clientzertifikat
- PC-seitige Plausibilitätsprüfung der LOGO!-Auftragsparameter vor dem Modbus-Schreiben
- detaillierter LOGO!-V001-Implementierungsstandard
- detailliertes Inbetriebnahme- und Abnahmeprotokoll
- GitHub-Actions-Windows-Build mit Portable- und Single-File-Ausgabe

## Architektur

```text
Maschine / Zyklussignal
        │
        ▼
 Siemens LOGO! ──────► Pneumatikventil / VE-Wechsler
        │
        │ Ethernet
        ▼
 WLAN-Client/Bridge )))) Access Point ─── LAN ─── Partcounter-PC
                                                    │
                              ┌─────────────────────┼─────────────────────┐
                              ▼                     ▼                     ▼
                         WPF-Leitstand            SQLite             Etikettendruck
                              │
                              ▼
                         ARBURG ALS
```

Die 30 LOGO!-Stationen sollten feste IP-Adressen erhalten. Die endgültige Adressierung wird bei der Anlageninbetriebnahme festgelegt.

## Modbus V2

PC = Modbus-TCP-Client/Master, LOGO! = Server/Slave. Standardport ist TCP 502, Standard-Unit-ID ist 1.

- PC → LOGO!: HR1–HR12
- LOGO! → PC: HR20–HR37
- DWORD-Werte: High Word vor Low Word
- PC-Heartbeat und LOGO!-Heartbeat
- CompletionSequence für verlustfreie VE-Abschlusserkennung
- CommandSequence/AckSequence gegen Mehrfachauslösung
- lokale LOGO!-Zählung und VE-Wechsel auch bei PC-/WLAN-Unterbrechung

Die verbindliche Belegung steht in [`docs/MODBUS_REGISTER_MAP.md`](docs/MODBUS_REGISTER_MAP.md).

## LOGO!-Programm

Die Engineering-Vorgabe für `Partcounter_LOGO_V001` steht in:

- [`docs/PARTCOUNTER_LOGO_V001_IMPLEMENTATION.md`](docs/PARTCOUNTER_LOGO_V001_IMPLEMENTATION.md)
- [`docs/LOGO_CONTROL_LOGIC.md`](docs/LOGO_CONTROL_LOGIC.md)
- [`docs/COMMISSIONING_TEST_PROTOCOL_R001_7.md`](docs/COMMISSIONING_TEST_PROTOCOL_R001_7.md)

Für die erste reale Testmaschine müssen vor der finalen LOGO!-Datei noch die tatsächlichen elektrischen Randbedingungen festgelegt werden: LOGO!-Hardware/Versorgung, Pegel des Zyklusimpulses, Ventilspulenspannung bzw. Koppelrelais, Ausgangsart, vorhandene Endlagenrückmeldung und freizugebende Ventilimpulszeit.

## Datenbank

Beim ersten Start wird `%LOCALAPPDATA%\Partcounter\partcounter.db` angelegt. Enthalten sind unter anderem:

- `Machines`
- `Articles`
- `PackagingUnits`
- `Settings`
- `Events`

## Etikettendruck

Beim Abschluss einer VE wird zuerst der VE-Datensatz gespeichert. Anschließend wird – sofern aktiviert – das Etikett gedruckt. Dadurch bleibt die Produktion auch bei fehlendem oder ausgeschaltetem Drucker nachvollziehbar.

Das Etikett enthält unter anderem Maschine, VE-Nummer, Auftragsnummer, Artikel, Werkzeug, Kavitäten, Sollmenge, tatsächliche Menge, Mehrmenge, Fertigstellungszeit, VE-ID, QR-Code und Code 128.

## ARBURG ALS

R001.7 enthält die in R001.6 aufgebaute ALS-Anbindung:

1. Datei-/Hotfolder-Modus für XLSX/XLSM/CSV/TXT/TSV mit konfigurierbaren Importparametern.
2. REST/JSON-Modus mit frei konfigurierbarem Feldmapping, Maschinenaliasen und Authentifizierung.

Der konkrete ALS-Endpunkt beziehungsweise die realen Exportspalten müssen bei der Inbetriebnahme gegen die vorhandene ARBURG-ALS-Installation verifiziert werden.

## Build

1. `Partcounter.sln` mit Visual Studio 2022 öffnen.
2. NuGet-Pakete wiederherstellen.
3. `Partcounter.App` im Release-Modus bauen.
4. Zuerst im Simulationsmodus prüfen.
5. Für Echtbetrieb ausschließlich eine LOGO!-Station mit ProtocolVersion 2 verwenden.

Verwendete Kernpakete:

- NModbus 3.0.83
- Microsoft.Data.Sqlite 8.0.30
- ZXing.Net 0.16.11
- ClosedXML 0.105.1

GitHub Actions erzeugt für R001.7 eine selbstenthaltende Portable-Folder-Ausgabe und eine Single-File-Windows-x64-Ausgabe.

## Nächster Meilenstein

Die Software- und Protokollbasis für die erste reale LOGO!-Kopplung ist mit R001.7 festgelegt. Nächster Meilenstein ist die Umsetzung von `Partcounter_LOGO_V001` in LOGO! Soft Comfort für eine konkrete Testmaschine, gefolgt von der Prüfung nach dem R001.7-Inbetriebnahmeprotokoll.
