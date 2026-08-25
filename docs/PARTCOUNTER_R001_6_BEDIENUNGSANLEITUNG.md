# Partcounter R001.6 – Bedienungs-, Inbetriebnahme- und Schnittstellenhandbuch

Dieses Repository-Dokument ergänzt das ausführliche DOCX/PDF-Handbuch. Maßgeblich für produktive Inbetriebnahmen sind immer die tatsächlich eingesetzte LOGO!Soft-Comfort-/Firmware-/ALS-Version und die Partcounter-Protokollversion.

## 1. Grundprinzip

Partcounter ist der zentrale Leitstand für bis zu 30 Spritzgussmaschinen. Artikel, Auftrag, Kavitäten, VE-Soll, Auftragsmenge, Historie und Etikett werden am PC verwaltet. **Zykluszählung und physischer VE-Wechsel laufen lokal in der Siemens LOGO!**. Damit darf ein kurzer PC-/WLAN-Ausfall keine Zyklen verlieren und keinen fälligen Behälterwechsel verhindern.

VE werden auf ganze Werkzeugzyklen aufgerundet:

```text
TargetCyclesPerVE = ceil(VE-Soll / aktive Kavitäten)
Tatsächliche VE    = TargetCyclesPerVE * aktive Kavitäten
```

## 2. Maschinenkachel / Rechtsklick

R001.6 besitzt für jede sichtbare Maschinenkachel ein Kontextmenü. Rechtsklick auf die Kachel:

- **Auftrag pausieren** – nur bei laufendem Auftrag.
- **Auftrag fortsetzen** – nur bei pausiertem Auftrag.
- **Auftrag beenden** – beendet den aktuellen Auftrag bewusst.
- **Temporär deaktivieren** – pausiert einen laufenden Auftrag und entfernt die Maschine aus aktivem Monitoring; beim Reaktivieren wird ein pausierter Auftrag nicht unbeabsichtigt automatisch fortgesetzt.

Maschinen ohne aktiven Auftrag sind standardmäßig ausgeblendet. Der Always-on-Top-Mini-Monitor zeigt beim Minimieren des Hauptfensters die aktiven/pausierten Maschinen in kompakter Form.

## 3. Partcounter – Maschinen / Modbus

Im Reiter **Maschinen / Modbus** werden je M01–M30 konfiguriert:

| Feld | Bedeutung | Standard/Empfehlung |
|---|---|---|
| Maschinenname | sichtbarer Name im Leitstand | eindeutig |
| LOGO! IP-Adresse | IPv4-Adresse der LOGO!/WLAN-Bridge | M01 `192.168.50.101` … M30 `192.168.50.130` |
| TCP-Port | Modbus-TCP-Server-Port der LOGO! | `502` |
| Unit-ID | Modbus Unit Identifier | `1` |
| Aktiv | Kommunikationsworker beim Programmstart erzeugen | nur installierte Stationen |

Empfohlener isolierter Partcounter-PC: `192.168.50.10/24`. DHCP für LOGO!-Endpunkte vermeiden. Nach Änderungen an Maschinen-/Modbus-Endpunkten **Partcounter neu starten**.

## 4. Siemens LOGO! – Modbus-Server

In LOGO!Soft Comfort für jede Station eine Modbus-Serververbindung anlegen. Siemens dokumentiert für LOGO! BM einen Server-Portbereich **502–510**. Partcounter verwendet im Standard `502`. Für den Produktionsbetrieb sollte die Serververbindung – wenn die eingesetzte LOGO!Soft-Comfort-Version dies unterstützt – auf die feste IP des Partcounter-PCs beschränkt werden, statt alle Clients zu akzeptieren.

Variable Memory `VW` der LOGO! wird als Modbus Holding Register angesprochen. Für Partcounter gilt verbindlich:

- NModbus ist **nullbasiert**.
- Partcounter `HR1` entspricht NModbus Startadresse `0` und LOGO! `VW0`.
- Jedes Word belegt 2 Byte: HR2 → VW2, HR3 → VW4 usw.
- 32-Bit-Werte werden **High Word, dann Low Word** übertragen.

### 4.1 PC → LOGO! Konfiguration

| HR | NModbus | LOGO! VW | Feld |
|---:|---:|---:|---|
| HR1 | 0 | VW0 | ProtocolVersion |
| HR2 | 1 | VW2 | CommandSequence |
| HR3 | 2 | VW4 | CommandWord |
| HR4 | 3 | VW6 | ActiveCavities |
| HR5 | 4 | VW8 | TargetPartsPerVE High |
| HR6 | 5 | VW10 | TargetPartsPerVE Low |
| HR7 | 6 | VW12 | ValvePulseMs |
| HR8 | 7 | VW14 | JobId High |
| HR9 | 8 | VW16 | JobId Low |
| HR10 | 9 | VW18 | TargetCyclesPerVE High |
| HR11 | 10 | VW20 | TargetCyclesPerVE Low |
| HR12 | 11 | VW22 | PC Heartbeat |

HR13–HR19 / VW24–VW36 bleiben in Protokollversion 1 reserviert.

### 4.2 LOGO! → PC Status

| HR | NModbus | LOGO! VW | Feld |
|---:|---:|---:|---|
| HR20 | 19 | VW38 | ProtocolVersion |
| HR21 | 20 | VW40 | StatusWord |
| HR22 | 21 | VW42 | CurrentParts High |
| HR23 | 22 | VW44 | CurrentParts Low |
| HR24 | 23 | VW46 | TotalCycles High |
| HR25 | 24 | VW48 | TotalCycles Low |
| HR26 | 25 | VW50 | CurrentVENumber |
| HR27 | 26 | VW52 | CompletedVEs |
| HR28 | 27 | VW54 | LastCompletedVEQuantity High |
| HR29 | 28 | VW56 | LastCompletedVEQuantity Low |
| HR30 | 29 | VW58 | AckSequence |
| HR31 | 30 | VW60 | ActiveCavitiesEcho |
| HR32 | 31 | VW62 | LastCompletedVENumber |
| HR33 | 32 | VW64 | CompletionSequence |
| HR34 | 33 | VW66 | LOGO Heartbeat |
| HR35 | 34 | VW68 | ErrorCode |
| HR36 | 35 | VW70 | LastCompletionReason |

### 4.3 CommandWord HR3/VW4

| Bit | Hex | Funktion |
|---:|---:|---|
| 0 | `0x0001` | Automatikfreigabe |
| 1 | `0x0002` | Reset / neuer Auftrag |
| 2 | `0x0004` | manueller VE-Wechsel |
| 3 | `0x0008` | Alarm quittieren |
| 4 | `0x0010` | Zählung pausieren |

`CommandSequence` muss für jeden neuen Befehl/Parameterupdate geändert werden. Die LOGO! verarbeitet One-Shot-Funktionen nur bei neuer Sequenz und bestätigt in `AckSequence`.

### 4.4 StatusWord HR21/VW40

| Bit | Hex | Status |
|---:|---:|---|
| 0 | `0x0001` | LOGO bereit |
| 1 | `0x0002` | Automatik aktiv |
| 2 | `0x0004` | VE-Wechsel läuft |
| 3 | `0x0008` | Alarm |
| 4 | `0x0010` | Zykluseingang aktiv |

## 5. LOGO!-Ablauflogik

Empfohlen: positive Flanke des gültigen Maschinenzyklus erfassen. Während Pause nicht zählen. Pro gültigem Zyklus `TotalCycles += 1`, aktuellen VE-Zykluszähler erhöhen und `CurrentParts = CurrentVECycles * ActiveCavities` setzen. Bei `CurrentVECycles >= TargetCyclesPerVE`:

1. LastCompletedVEQuantity sichern.
2. LastCompletedVENumber sichern.
3. LastCompletionReason setzen.
4. CompletedVEs erhöhen.
5. CompletionSequence genau einmal erhöhen.
6. Q1 für `ValvePulseMs` schalten bzw. Endlagenablauf ausführen.
7. CurrentVENumber erhöhen.
8. aktuellen VE-Zähler zurücksetzen.

Die Änderung der `CompletionSequence` ist der robuste PC-Trigger für VE-Historie und Etikettendruck.

## 6. ARBURG ALS – R001.6

ARBURG beschreibt ALS als MES mit Auftragsplanung/-steuerung und standardisierten Schnittstellen. Öffentlich dokumentiert sind u. a. OPC UA auf Produktionsseite sowie Connectivity-Module für MQTT/REST-API und ERP/MES-Kopplung. **Ein universeller REST-Endpunkt und ein universelles Kundenschema sind öffentlich nicht festgelegt.** Deshalb ist R001.6 absichtlich frei konfigurierbar.

R001.6 unterstützt zwei kontrollierte Eingangswege:

1. **Datei-/Hotfolder**: XLSX, XLSM, CSV, TXT, TSV.
2. **REST/JSON**: vom ALS-Administrator/ARBURG bereitgestellter HTTP(S)-Endpunkt.

Direktes MQTT ist in R001.6 noch nicht implementiert.

### 6.1 Datei-/Hotfolder-Einstellungen

- Datei oder Hotfolder/UNC-Pfad.
- Dateimuster, z. B. `ALS_Order_*.xlsx`.
- Excel-Blattname; leer = erstes Blatt.
- Kopfzeilen-Nummer.
- CSV/TSV-Trennzeichen.
- Dateicodierung, z. B. `utf-8` oder `windows-1252`.
- Culture, z. B. `de-DE`.
- optional nach Erfolg archivieren.
- Archivordner.
- Fehlerordner.
- optional periodischer Auto-Abruf.

### 6.2 REST/JSON-Einstellungen

Vom ALS-Administrator/ARBURG werden benötigt:

- vollständige HTTPS-URL des Auftragsendpunkts,
- GET oder POST,
- Authentifizierung: None / Basic / Bearer / API-Key,
- Benutzer/Passwort bzw. Token/API-Key,
- API-Key-Headername,
- optionale zusätzliche HTTP-Header,
- optionaler POST-JSON-Body,
- JSON-Wurzelpfad zur Auftragsliste, z. B. `data.orders`,
- optional Client-Zertifikat `.pfx/.p12` + Passwort,
- Timeout,
- Abrufintervall/Rate Limit,
- Zeit- und Zahlenformat.

Passwort, Bearer Token, API-Key-Wert und Zertifikatspasswort werden von Partcounter per Windows DPAPI für den aktuellen Windows-Benutzer geschützt gespeichert. `AllowUntrustedTls` ist ausschließlich für kontrollierte Tests gedacht und sollte im Produktionsbetrieb aus bleiben.

### 6.3 Feldmapping

Pflichtfelder:

- `OrderNumber`
- `ArticleNumber`
- `OrderQuantity`
- mindestens eines aus `MachineNumber`, `MachineName`, `MachineExternalId`

Zusätzlich unterstützt R001.6:

`ArticleDescription`, `ToolNumber`, `Cavities`, `PackagingQuantity`, `PlannedStart`, `PlannedEnd`, `OrderStatus`, `OperationNumber`, `Priority`, `MaterialNumber`, `MaterialDescription`, `Batch`, `Color`, `CustomerOrder`, `LastChanged`.

Bei XLSX/CSV ist `SourceField` der Spaltenname. Bei REST ist es ein punktgetrennter JSON-Pfad relativ zum einzelnen Auftragsobjekt, z. B. `order.number` oder `article.number`.

Maschinen-Alias je Zeile:

```text
ARB-0470-07=M07
ALLROUNDER_07=7
```

Standardverhalten: ALS-Auftrag wird **nur in die Partcounter-Auftragsmaske übernommen**. Automatischer Start ist eine gesonderte Option und sollte erst nach validierter Zuordnung aktiviert werden.

## 7. Erstinbetriebnahme – Mindesttest

Vor Rollout auf weitere Maschinen mindestens eine Teststation vollständig prüfen: IP/Ping, Port, Protokollversion, 1/8/64 Kavitäten, nicht teilbare VE, Q1-Impuls, manueller Wechsel, Pause/Resume, WLAN-Ausfall, CompletionSequence, einmaliger Etikettendruck und ALS-Testauftrag. Safety-Funktionen bleiben unabhängig von Partcounter/Modbus.

## Herstellerquellen

- Siemens LOGO!Soft Comfort Online Help – Modbus server connections / VM mapping: https://cache.industry.siemens.com/dl/files/807/100782807/att_924632/v1/Help_en-US_en-US.pdf
- Siemens Application Example 109813923 – LOGO! 8.3 Modbus/TCP: https://support.industry.siemens.com/cs/attachments/109813923/109813923_7KN_Powercenter1000_LOGO_DOC_V1_0_en.pdf
- ARBURG ALS Whitepaper: https://www.arburg.com/media/daten/other/682735_Whitepaper_ALS_en.pdf
- ARBURG ALS module functions: https://www.arburg.com/media/daten/publications/technical_data/als/ARBURG_ALS_MODULE_TD_681700_en_GB.pdf
- ARBURG Kontakt / ALS Support: https://www.arburg.com/de/kontakt/
