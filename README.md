# Partcounter

**Revision:** R001.15 – Production Readiness  
**Plattform:** Windows / C# / .NET 8 / WPF  
**Anlage:** bis zu 30 Spritzgussmaschinen · Siemens LOGO! · Modbus TCP · WLAN/LAN

Partcounter überwacht den Füllgrad von Verpackungseinheiten (VE) an bis zu 30 Spritzgussmaschinen. Jede Maschine liefert einen Zyklusimpuls an eine Siemens LOGO!. Die LOGO! zählt lokal und schaltet bei voller VE über ein pneumatisches Ventil den Verpackungswechsler. Die PC-Anwendung ist Leitstand, Artikel-/Auftragsverwaltung, VE-Historie, ARBURG-ALS-Integration, Etikettendruck, Diagnose- und Updateplattform.

## Aktueller Revisionsstand R001.15

R001.15 baut auf dem stabilen Modbus-V2-/LOGO-V001-Kern auf und ergänzt die PC-Anwendung um Funktionen für den produktionsreifen Dauerbetrieb:

- automatische tägliche SQLite-Datensicherung
- maximal 30 Sicherungsgenerationen
- WAL-sichere Sicherung über die SQLite-Backup-API
- automatische `PRAGMA quick_check`-Prüfung jeder erzeugten Sicherung
- manuelle Datenbankprüfung mit `quick_check` und `foreign_key_check`
- manuelle Sicherung aus dem Admin-geschützten Einstellungsbereich
- Support-/Diagnosepaket ohne Settings-Export und ohne Datenbanksicherung
- direkter Zugriff auf Sicherungs- und Diagnoseordner
- Versionsstand 0.1.15 / R001.15 in Anwendung, About-Dialog, Build und Updatepaket

Dokumentation: [`docs/PRODUCTION_READINESS_R001_15.md`](docs/PRODUCTION_READINESS_R001_15.md)

## Funktionsausbau R001.10 bis R001.14

Die unmittelbar vorherigen Revisionen sind vollständig enthalten:

- **R001.10:** WYSIWYG-Etiketteneditor
- **R001.11:** Bilder und Logos im Etiketteneditor
- **R001.12:** Trennung Produktionsbetrieb / Administration; Leitstand, Artikelstamm und VE-Historie frei, Einstellungen geschützt
- **R001.13:** konfigurierbares Firmenlogo im Leitstand
- **R001.14:** freie Über-Funktion, integrierte Detailhilfe mit F1 und Software-Update-Center für Netzwerk/UNC, USB und lokale ZIP-Pakete mit Manifest- und SHA-256-Prüfung

## Referenzmaschine 01

Die erste reale Teststation ist hardwareseitig festgelegt:

- Siemens LOGO! 12/24RCEo, Bestellnummer `6ED1052-2MD08-0BA2`, LOGO! 8.4 ohne Display
- Versorgung 24 V DC
- I1 = 24-V-DC-Zyklusimpuls
- Q1 = Relaisausgang für 24-V-DC-Pneumatikventil
- keine Endlagenrückmeldung an der ersten Maschine; I2 bleibt optional vorbereitet
- Ventilimpuls einstellbar von 50 bis 5000 ms im 10-ms-Raster, Standard 750 ms
- externe Q1-Absicherung erforderlich
- bevorzugte Industrieausführung: Q1 schaltet ein 24-V-Koppel-/Interface-Relais, das die Ventilspule schaltet

Die verwendete LOGO!-Variante besitzt vier Relaisausgänge; das Datenblatt nennt maximal 10 A ohmsche bzw. 3 A induktive Kontaktlast und keinen internen Kurzschlussschutz. Vor Freigabe einer direkten Ventilansteuerung müssen Nennstrom/Nennleistung und Entstörbeschaltung der realen 24-V-Ventilspule bekannt sein.

Die vollständige Hardwarefestlegung steht in [`docs/REFERENCE_MACHINE_01_HARDWARE.md`](docs/REFERENCE_MACHINE_01_HARDWARE.md).

## Zentrales Sicherheits- und Verfügbarkeitsprinzip

**Zählen und automatischer VE-Wechsel laufen lokal in der LOGO!.** Der PC gibt Parameter vor und liest Statusdaten. Ein kurzer PC-, LAN- oder WLAN-Ausfall darf deshalb keinen Zyklus verlieren und keinen fälligen Kistenwechsel verhindern.

Partcounter und die Standard-LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztüren, Maschinenfreigaben und weitere Safety-Funktionen verbleiben vollständig in den dafür vorgesehenen sicheren Maschinenkreisen.

## Modbus Protocol V2 / LOGO V001

Partcounter verwendet verbindlich **ProtocolVersion 2**. Die LOGO! führt die Zykluszähler nativ; die PC-Anwendung berechnet daraus die Teilezahl mit der aktiven Kavitätenzahl:

```text
CurrentParts            = CurrentVECycles × ActiveCavitiesEcho
LastCompletedVEQuantity = LastCompletedVECycles × LastCompletedCavities
```

Dadurch muss die LOGO! keine potenziell überlaufende 16-Bit-Analogmultiplikation `Zyklen × Kavitäten` ausführen.

Verbindliche Grenzen:

- Kavitäten: 1…64
- Zyklen je VE: 1…32767
- Gesamtzyklen je LOGO!-Auftrag: bis 999999
- CommandSequence / CompletionSequence / Heartbeats: 1…32767
- Ventilimpuls: 50…5000 ms in 10-ms-Schritten

Die PC-Anwendung überträgt die Ventilzeit auf HR7 in 10-ms-Einheiten. Beispiel: **750 ms → HR7 = 75**.

Beim PC-Neustart wird die lokale `CommandSequence` zuerst mit dem von der LOGO! gemeldeten `AckSequence` synchronisiert. Der erste Steuerbefehl nach einem Neustart kann dadurch nicht versehentlich als bereits bearbeitetes Duplikat verworfen werden. Sequenzen und Heartbeats springen nach 32767 wieder auf 1.

**R001.15 erwartet weiterhin ProtocolVersion 2 und das passende `Partcounter_LOGO_V001`. Ein älteres V1-LOGO!-Programm darf nicht produktiv eingesetzt werden.**

## Rundungsregel für Verpackungseinheiten

Es werden ausschließlich vollständige Maschinenzyklen gezählt. Ist die gewünschte VE-Menge nicht durch die Kavitätenzahl teilbar, wird auf den nächsten vollständigen Zyklus aufgerundet:

```text
Zyklen je VE        = ceil(VE-Soll / Kavitäten)
Tatsächliche VE     = Zyklen je VE × Kavitäten
Mehrmenge           = Tatsächliche VE - VE-Soll
```

Beispiel: **1000 Stück Soll / 64 Kavitäten = 16 Zyklen = 1024 Stück tatsächlich**.

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
- WYSIWYG-Etiketteneditor mit Bildern und Logos
- konfigurierbares Firmenlogo im Leitstand
- offene Druckaufträge bleiben nachvollziehbar
- SQLite im WAL-Modus und Ereignisprotokoll
- automatische tägliche SQLite-Sicherung mit 30 Generationen
- manuelle Integritätsprüfung und manuelle Sicherung
- datensparsames Diagnosepaket für Service/Support
- ARBURG-ALS-Datei-/Hotfolder-Import für XLSX/XLSM/CSV/TXT/TSV
- ARBURG-ALS-REST/JSON-Modus mit Mapping, Basic/Bearer/API-Key und optionalem Clientzertifikat
- PC-seitige Plausibilitätsprüfung vor jedem LOGO!-Auftragstelegramm
- geschützte administrative Bereiche; Produktionsgrundfunktionen frei zugänglich
- integrierte Hilfe mit F1
- Über-Funktion mit Revision, Programmierer, Lizenz- und Systeminformationen
- Update-Center für Netzwerk/UNC, USB und lokale ZIP-Pakete
- SHA-256-Prüfung und manifestbasierte Updatevalidierung
- GitHub-Actions-Windows-Build mit Portable-, Single-File-, Engineering- und UpdatePackage-Artefakten

## Architektur

```text
Maschine / 24-V-Zyklussignal
        │
        ▼
 Siemens LOGO! 12/24RCEo ──► Q1 / Koppelrelais ──► 24-V-Pneumatikventil
        │
        │ Ethernet
        ▼
 WLAN-Client/Bridge )))) Access Point ─── LAN ─── Partcounter-PC
                                                    │
                 ┌──────────────────────────────────┼─────────────────────────────┐
                 ▼                                  ▼                             ▼
            WPF-Leitstand                        SQLite                      Etikettendruck
                 │                                  │
                 ▼                                  ├── Backups
            ARBURG ALS                             └── Diagnose / Ereignisse
```

## Modbus V2

PC = Modbus-TCP-Client/Master, LOGO! = Server/Slave. Standardport ist TCP 502, Standard-Unit-ID ist 1.

- PC → LOGO!: HR1–HR12 / VW0–VW22
- LOGO! → PC: HR20–HR37 / VW38–VW72
- DWord-Werte: High Word vor Low Word
- TargetCyclesPerVE wird über VD18 direkt dem VE-Zähler-Schwellwert zugeordnet
- CurrentVECycles und TotalCycles werden über Parameter-VM-Mapping bereitgestellt
- LastCompletedVECycles, LastCompletionReason und LastCompletedCavities werden beim Abschluss gespeichert
- CompletionSequence ermöglicht eine verlustfreie VE-Abschlusserkennung
- CommandSequence/AckSequence verhindert Mehrfachauslösungen
- lokale LOGO!-Zählung und VE-Wechsel laufen bei PC-/WLAN-Unterbrechung weiter

Die verbindliche Belegung steht in [`docs/MODBUS_REGISTER_MAP.md`](docs/MODBUS_REGISTER_MAP.md).

## LOGO!-Programm

Die Engineering-Vorgabe für `Partcounter_LOGO_V001` steht in:

- [`docs/PARTCOUNTER_LOGO_V001_IMPLEMENTATION.md`](docs/PARTCOUNTER_LOGO_V001_IMPLEMENTATION.md)
- [`docs/LOGO_CONTROL_LOGIC.md`](docs/LOGO_CONTROL_LOGIC.md)
- [`docs/COMMISSIONING_TEST_PROTOCOL_R001_7.md`](docs/COMMISSIONING_TEST_PROTOCOL_R001_7.md)
- [`docs/REFERENCE_MACHINE_01_HARDWARE.md`](docs/REFERENCE_MACHINE_01_HARDWARE.md)

Der Blockplan enthält unter anderem die reale Zyklus-Flankenerkennung vor der Zählfreigabe, native VE-/Gesamtzähler, Sample-and-Hold-Speicher für die abgeschlossene VE, einen restart-sicheren Befehlsdecoder, Heartbeat-Überwachung und den zeitparametrierten Ventilimpuls.

Die Endlagenüberwachung ist Bestandteil des Standardkonzepts, für Referenzmaschine 01 jedoch deaktiviert. I2 bleibt reserviert und kann bei späteren Maschinen als bestätigte Wechsler-Endlage aktiviert werden.

## Datenbank, Sicherung und Etikettierung

Beim ersten Start wird `%LOCALAPPDATA%\Partcounter\partcounter.db` angelegt. Enthalten sind unter anderem `Machines`, `Articles`, `PackagingUnits`, `Settings` und `Events`.

Beim Abschluss einer VE wird zuerst der VE-Datensatz gespeichert. Anschließend wird – sofern aktiviert – das Etikett gedruckt. Dadurch bleibt die Produktion auch bei fehlendem oder ausgeschaltetem Drucker nachvollziehbar.

R001.15 legt konsistente Sicherungen unter `%LOCALAPPDATA%\Partcounter\Backups` ab. Die Sicherungen werden über die SQLite-Backup-API erstellt und anschließend mit `PRAGMA quick_check` geprüft.

Das Etikett enthält unter anderem Maschine, VE-Nummer, Auftragsnummer, Artikel, Werkzeug, Kavitäten, Sollmenge, tatsächliche Menge, Mehrmenge, Fertigstellungszeit, VE-ID, QR-Code und Code 128.

## ARBURG ALS

Partcounter enthält zwei vorbereitete ALS-Anbindungswege:

1. Datei-/Hotfolder-Modus für XLSX/XLSM/CSV/TXT/TSV mit konfigurierbaren Importparametern.
2. REST/JSON-Modus mit frei konfigurierbarem Feldmapping, Maschinenaliasen und Authentifizierung.

Der konkrete ALS-Endpunkt beziehungsweise die realen Exportspalten müssen bei der Inbetriebnahme gegen die vorhandene ARBURG-ALS-Installation verifiziert werden.

## Build

1. `Partcounter.sln` mit Visual Studio 2022 öffnen.
2. NuGet-Pakete wiederherstellen.
3. `Partcounter.App` im Release-Modus bauen.
4. Zuerst im Simulationsmodus prüfen.
5. Für Echtbetrieb ausschließlich eine LOGO!-Station mit ProtocolVersion 2 verwenden.

Kernpakete:

- NModbus 3.0.83
- Microsoft.Data.Sqlite 8.0.30
- ZXing.Net 0.16.11
- ClosedXML 0.105.1

GitHub Actions erzeugt für R001.15:

- `Partcounter_R001_15_Portable_Folder_win-x64`
- `Partcounter_R001_15_SingleFile_win-x64`
- `Partcounter_R001_15_Engineering`
- `Partcounter_R001_15_UpdatePackage`

## Nächster Meilenstein

Nach dem R001.15-Produktionsschutz liegt der Schwerpunkt auf der **realen Maschinenabnahme**: LOGO!-V001 an Referenzmaschine 01 aufbauen, Zyklusimpuls und Ventilausgang elektrisch prüfen, Modbus-V2-Register gegen die reale LOGO! verifizieren und anschließend den vollständigen Abnahmelauf mit dem vorhandenen Inbetriebnahmeprotokoll durchführen.
