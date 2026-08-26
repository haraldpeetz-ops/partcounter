# Partcounter LOGO V001 – Implementierungsstandard

**Partcounter Revision:** R001.7  
**LOGO-Programm:** `Partcounter_LOGO_V001`  
**Zielplattform:** Siemens LOGO! 8.3 / 8.4  
**Kommunikation:** Modbus TCP, LOGO! als Server, PC als Client  
**Partcounter-Protokoll:** Version 2

## 1. Ziel

Dieses Dokument ist die verbindliche Engineering-Vorgabe für das erste reale LOGO!-Programm von Partcounter. Alle Maschinen verwenden dieselbe Grundlogik. Maschinenbezogen werden nur Netzwerkparameter, reale I/O-Verdrahtung und optional die Endlagenüberwachung angepasst.

Die LOGO! zählt Maschinenzyklen und löst den VE-Wechsel lokal aus. Ein Ausfall von PC, LAN oder WLAN darf weder Zyklen verlieren noch einen fälligen Verpackungswechsel verhindern.

## 2. Hardwaregerechte Zählstrategie

Die LOGO!-Vor-/Rückwärtszähler unterstützen Zählerstände und Schwellwerte bis 999999 und stellen diese über Parameter-VM-Mapping als DWord bereit. Allgemeine LOGO!-Analogwerte und Analogberechnungen liegen jedoch im signed-16-Bit-Bereich.

Daraus folgt für Partcounter V2:

- `TotalCycles` wird nativ in einem LOGO!-Zähler geführt und als DWord übertragen.
- `CurrentVECycles` wird nativ gezählt.
- die LOGO! multipliziert **nicht** `Zyklen × Kavitäten`; das macht der PC.
- eine einzelne VE ist auf maximal **32767 Zyklen** begrenzt.
- der Zyklusstand der abgeschlossenen VE wird vor dem Reset mit einem Analog-Watchdog gespeichert.
- CommandSequence, CompletionSequence und Heartbeats bleiben im Bereich 1…32767.

Der Gesamtzykluszähler bleibt davon unabhängig und kann bis zum nativen LOGO!-Grenzwert 999999 zählen.

## 3. Standard-I/O

| Signal | Standard | Funktion | Bemerkung |
|---|---|---|---|
| Zyklusimpuls | I1 | gültiger Maschinenzyklus / Auswurf | positive Flanke = genau 1 Zyklus |
| Wechsler-Endlage | I2 | optionale Rückmeldung | nur aktivieren, wenn real vorhanden |
| Handquittierung | I3 | lokale Quittierung | optional |
| Ventil VE-Wechsler | Q1 | Pneumatikventil | monostabiler Impuls |
| Wechselanzeige | Q2 | Lampe „VE-Wechsel“ | optional |
| Störung | Q3 | Sammelstörung | optional |

Safety-Signale wie Not-Halt, Schutztür oder Maschinenfreigabe werden nicht durch Partcounter geführt.

## 4. Modbus-/VM-Zuordnung

### PC → LOGO!

| HR | LOGO VM | Inhalt |
|---:|---|---|
| HR1 | VW0 | ProtocolVersion = 2 |
| HR2 | VW2 | CommandSequence 1…32767 |
| HR3 | VW4 | CommandWord |
| HR4 | VW6 | ActiveCavities 1…64 |
| HR5/HR6 | VD8 | TargetPartsPerVE |
| HR7 | VW12 | ValvePulse10Ms, 5…500 |
| HR8/HR9 | VD14 | JobId |
| HR10/HR11 | VD18 | TargetCyclesPerVE, 1…32767 |
| HR12 | VW22 | PC Heartbeat 1…32767 |

`VD18` wird per Parameter-VM-Mapping direkt dem `On Threshold` des VE-Zählers zugeordnet. Der PC schreibt den DWord-Wert High Word vor Low Word; durch die 32767-Grenze ist das High Word im V2-Betrieb immer 0.

Der Ventilwert wird in 10-ms-Einheiten übertragen. Beispiel: 750 ms → HR7 = 75. Der Zeitbaustein verwendet fest die Zeitbasis 10 ms und sein Zeitparameter wird auf VW12 gemappt.

### LOGO! → PC

| HR | LOGO VM | Inhalt |
|---:|---|---|
| HR20 | VW38 | ProtocolVersion = 2 |
| HR21 | VW40 | StatusWord |
| HR22/HR23 | VD42 | CurrentVECycles |
| HR24/HR25 | VD46 | TotalCycles |
| HR26 | VW50 | CurrentVENumber |
| HR27 | VW52 | CompletedVEs |
| HR28 | VW54 | LastCompletedVECycles High = 0 |
| HR29 | VW56 | LastCompletedVECycles Low |
| HR30 | VW58 | AckSequence |
| HR31 | VW60 | ActiveCavitiesEcho |
| HR32 | VW62 | LastCompletedVENumber |
| HR33 | VW64 | CompletionSequence |
| HR34 | VW66 | LOGO Heartbeat |
| HR35 | VW68 | ErrorCode |
| HR36 | VW70 | LastCompletionReason |
| HR37 | VW72 | LastCompletedCavities |

Verbindliches Parameter-/VM-Mapping:

- `CurrentVECycles.Counter` → VD42
- `TotalCycles.Counter` → VD46
- `CurrentVECycles.On Threshold` ← VD18
- `LastCompletedCycles.Aen` → VW56
- `AckSequenceStore.Aen` → VW58
- `LastCompletionReasonStore.Aen` → VW70
- `LastCompletedCavitiesStore.Aen` → VW72

HR28/VW54 bleibt in V2 auf 0, da `LastCompletedVECycles` maximal 32767 beträgt. Die DWord-Struktur bleibt trotzdem erhalten, damit die PC-Struktur einheitlich und erweiterbar bleibt.

## 5. CommandWord und StatusWord

Die unteren Bits des Modbus-Wortes HR3/VW4 liegen aufgrund der VM-Byteordnung im Low Byte `VB5`. In der LOGO!-Schaltung werden daher lokale VM-Bits verwendet:

| Funktion | CommandWord-Bit | lokales VM-Bit |
|---|---:|---|
| Automatic enabled | 0 | V5.0 |
| Reset / neuer Auftrag | 1 | V5.1 |
| manueller VE-Wechsel | 2 | V5.2 |
| Alarm quittieren | 3 | V5.3 |
| Zählung pausieren | 4 | V5.4 |

StatusWord HR21 liegt in VW40; die unteren Statusbits werden auf V41.0…V41.5 ausgegeben.

## 6. Funktionsblockplan V001

Die folgende Nummerierung ist die Zielnummerierung für den ersten Aufbau in LOGO! Soft Comfort. Falls LSC beim Einfügen andere Nummern vergibt, werden die realen Nummern nach Fertigstellung einmalig nachgezogen und danach eingefroren.

| Block | Typ | Name / Aufgabe | Kernparameter |
|---|---|---|---|
| B001 | AND mit Flankenauswertung | `CycleEdge` | I1 an Eingang 1; reale positive Maschinenflanke |
| B002 | AND | `CountGate` | B001 AND Automatic AND NOT Pause AND NOT VeChangeActive |
| B003 | Vor-/Rückwärtszähler | `CurrentVECycles` | Cnt=B002, Dir=0, R=ResetCurrentVE, On=VD18, Off=0, retentiv |
| B004 | Vor-/Rückwärtszähler | `TotalCycles` | Cnt=B002, Dir=0, R=ResetJob, retentiv |
| B005 | AND mit Flankenauswertung | `AutoCompletionPulse` | positive Flanke von B003.Q |
| B006 | Analog-Schwellwertschalter | `CurrentVeNonZero` | Ax=B003.Cnt, Q=1 bei Wert > 0 |
| B007 | AND | `ManualCompletionPulse` | NewCommand AND V5.2 AND B006.Q |
| B008 | OR | `CompletionPulse` | B005 OR B007 |
| B009 | Analog-Watchdog | `LastCompletedCycles` | En=B008, Ax=B003.Cnt; Aen → VW56 |
| B010 | Impuls-/Wischrelais | `ResetCurrentVeDelay` | Trigger=B008; Reset erst nach Snapshot |
| B011 | Impuls-/Wischrelais | `ValvePulse` | Trigger=B008; feste Zeitbasis 10 ms; Zeitwert via VW12 |
| B012 | Vor-/Rückwärtszähler | `CompletedVEs` | Cnt=B008, R=ResetJob; 0…32767 |
| B013 | Analogverstärker | `CompletedVEs_VM` | B012.Cnt → VW52 |
| B014 | Analogberechnung | `CurrentVENumber` | B012.Cnt + 1 → VW50 |
| B015 | Analogverstärker | `LastCompletedVENumber_VM` | B012.Cnt → VW62 |
| B016 | Analogverstärker | `CompletionSequence_VM` | B012.Cnt → VW64 |
| B017 | Analog-MUX | `CompletionReasonCandidate` | automatisch=1, manuell=2; Auswahl durch B007 |
| B018 | Analog-Watchdog | `LastCompletionReasonStore` | En=B008, Ax=B017; Aen → VW70 |
| B019 | Analog-Watchdog | `LastCompletedCavitiesStore` | En=B008, Ax=ActiveCavities; Aen → VW72 |
| B020 | Analogverstärker | `ActiveCavitiesEcho` | ActiveCavities aus VW6 → VW60 |
| B021 | Analog-Watchdog | `AckSequenceStore` | En=NewCommand, Ax=CommandSequence; Aen → VW58 |
| B022 | Analogkomparator | `CmdGreaterAck` | CommandSequence - AckSequence > 0 |
| B023 | Analogkomparator | `AckGreaterCmd` | AckSequence - CommandSequence > 0 |
| B024 | OR | `NewCommand` | B022 OR B023 |
| B025 | Impulsgeber / Takt | `LogoHeartbeatClock` | Diagnose-Takt |
| B026 | Vor-/Rückwärtszähler | `LogoHeartbeatCounter` | 1…32767; kontrollierter Wrap auf 1 |
| B027 | Heartbeat-Überwachung | `PcHeartbeatWatch` | Änderung von VW22 überwachen |
| B028 | Alarm-/Freigabelogik | `ConfigAndFaultLogic` | Parameter-/Endlagenfehler |

Zusätzliche Network-/Analog-Network-Inputs lesen die relevanten VW-/V-Bereiche. Die endgültige LSC-Blockliste wird beim Aufbau des realen Projekts exportiert und gegen diese Tabelle geprüft.

## 7. Zyklusflanke vor CountGate

Der Zyklusimpuls wird **vor** Auto/Pause/Wechsel-Freigaben auf eine echte positive Flanke reduziert. Würde zuerst ein normales AND aus I1 und der Freigabe gebildet, könnte das Aufheben einer Pause bei noch anstehendem I1-Pegel eine künstliche 0→1-Flanke und damit einen falschen Zählimpuls erzeugen.

B001 isoliert deshalb die reale Maschinenflanke. B002 darf diesen bereits erzeugten Ein-Zyklus-Puls anschließend nur noch freigeben oder sperren.

## 8. Snapshot der abgeschlossenen VE

Der Analog-Watchdog B009 wird als Sample-and-Hold verwendet. Bei einer positiven Flanke an `En` speichert er den aktuellen analogen Eingang als Vergleichswert `Aen`.

Ablauf:

```text
B003 erreicht Soll / manueller Wechsel
        ↓
CompletionPulse B008
        ├─> B009 speichert CurrentVECycles
        ├─> B018 speichert CompletionReason
        ├─> B019 speichert ActiveCavities
        ├─> B012 zählt CompletedVEs hoch
        ├─> B011 startet Ventilimpuls
        └─> B010 setzt B003 erst danach zurück
```

Damit bleiben `LastCompletedVECycles`, `LastCompletionReason` und `LastCompletedCavities` bis zum nächsten Abschluss stabil im Status verfügbar.

`LastCompletedVENumber` entspricht nach einem Abschluss dem aktualisierten Zähler `CompletedVEs` und wird deshalb über B015 aus B012.Cnt ausgegeben. `CurrentVENumber` ist `CompletedVEs + 1`.

## 9. Abschlussgrund dauerhaft speichern

B017 erzeugt für den aktuellen Abschluss einen Kandidatenwert:

```text
automatischer Abschluss → 1
manueller Abschluss     → 2
```

B018 speichert diesen Kandidaten nur auf der positiven Flanke von `CompletionPulse`. Dadurch fällt HR36 nach dem kurzzeitigen Manual-Signal nicht wieder auf 1 zurück, sondern behält den tatsächlichen Grund der zuletzt abgeschlossenen VE.

## 10. Befehlssequenz ohne 16-Bit-Überlauf

`CommandSequence` und `AckSequence` liegen zwischen 1 und 32767. Nach 32767 folgt wieder 1.

Ungleichheit wird mit zwei Komparatoren erkannt:

```text
CmdGreaterAck = CommandSequence - AckSequence > 0
AckGreaterCmd = AckSequence - CommandSequence > 0
NewCommand    = CmdGreaterAck OR AckGreaterCmd
```

Damit wird auch der Wrap 32767 → 1 erkannt. B021 übernimmt den neuen Wert auf `NewCommand` als AckSequence.

Beim PC-Neustart liest Partcounter zunächst HR30 und synchronisiert den lokalen Sequenzstand mit diesem Wert. Der nächste Befehl verwendet den darauffolgenden Wert.

## 11. Zähl- und VE-Ablauf

Zählen nur bei:

```text
CycleEdge
AND AutomaticEnabled
AND NOT PauseCounting
AND NOT VeChangeActive
```

Automatischer Abschluss:

```text
CurrentVECycles >= TargetCyclesPerVE
```

Manueller Abschluss:

```text
NewCommand AND ManualVeChangeBit AND CurrentVECycles > 0
```

Der aktuelle VE-Zähler wird erst nach allen Abschluss-Snapshots zurückgesetzt. Q1 wird monostabil für die konfigurierte Zeit eingeschaltet. Während Q1 aktiv ist, ist `CountGate` gesperrt.

## 12. Dynamischer Zielwert

Der Ein-Schwellwert von B003 wird direkt aus VD18 gespeist. Deshalb gilt als verbindliche Systemregel:

- Partcounter ist der einzige schreibende Modbus-Client.
- `TargetCyclesPerVE` wird nur bei neuem Auftrag oder unmittelbar nach einem VE-Abschluss geändert.
- Ein Parameterupdate während `CurrentVECycles > 0` ist unzulässig.

Für die letzte Teil-VE eines Auftrags schreibt der PC den kleineren Zielwert erst nach dem vorherigen Completion-Ereignis.

## 13. Parameterprüfung

PC-seitig wird vor jedem Auftrags-Telegramm geprüft:

- ActiveCavities = 1…64
- TargetPartsPerVE > 0
- TargetCyclesPerVE = 1…32767
- ValvePulseMs = 50…5000
- ValvePulseMs muss durch 10 teilbar sein

LOGO!-seitig werden mindestens Protokollversion, Kavitätenbereich, Zielzyklen, Zeitwert und zulässige Steuerbits plausibilisiert. Ein ungültiger Befehl darf Q1 nicht aktivieren.

## 14. Ventilzeit

Partcounter überträgt HR7 in Einheiten von 10 ms:

```text
ValvePulse10Ms = ValvePulseMs / 10
```

Beispiele:

```text
50 ms   → 5
750 ms  → 75
5000 ms → 500
```

B011 ist fest auf die Zeitbasis 10 ms eingestellt. Der Zeitparameter wird auf VW12 gemappt.

## 15. Heartbeats

PC und LOGO! verwenden Werte 1…32767 und springen danach wieder auf 1. Entscheidend ist die Änderung des jeweiligen Wertes innerhalb des Diagnosefensters.

Ein stehender PC-Heartbeat setzt lediglich den Kommunikationsstatus. Die lokale Zählung und ein fälliger automatischer VE-Wechsel laufen weiter.

## 16. Fehlercodes

| Code | Bedeutung | Verhalten |
|---:|---|---|
| 0 | kein Fehler | normal |
| 1 | falsche Protokollversion | Auftrag ablehnen |
| 2 | Kavitätenzahl außerhalb 1–64 | Auftrag ablehnen |
| 3 | TargetPartsPerVE = 0 | Auftrag ablehnen |
| 4 | TargetCyclesPerVE außerhalb 1–32767 | Auftrag ablehnen |
| 5 | Ventilimpuls außerhalb 50–5000 ms bzw. kein 10-ms-Raster | Auftrag ablehnen |
| 10 | optionale Wechsler-Endlage nicht rechtzeitig erreicht | Alarm, weitere automatische Wechsel sperren |
| 30 | interner ungültiger Ablaufzustand | Q1 aus, Alarm |

## 17. Optionaler Endlagentest

Falls I2 vorhanden und aktiviert ist, wird nach dem Ventilimpuls die erwartete Endlage innerhalb eines definierten Fensters geprüft. Bei Timeout:

```text
ErrorCode = 10
Alarm = 1
Q1 = 0
weitere automatische Wechsel sperren
```

Diese Funktion wird erst nach realer Prüfung der Mechanik freigegeben.

## 18. Neustartverhalten

### PC-Neustart

Die LOGO! läuft mit den zuletzt gültigen Parametern weiter. Partcounter liest AckSequence und sendet danach mit der nächsten Sequenz weiter.

### LOGO!-Neustart

- Q1 muss beim Start AUS sein.
- kein selbsttätiger Ventilimpuls allein aus einem alten Ausgangszustand.
- Zählerretentivität wird an der Testmaschine bewusst geprüft.
- ein LOGO!-Power-Cycle ist nicht mit einem reinen WLAN-/PC-Ausfall gleichzusetzen.
- das Wiederanlaufkonzept wird erst nach realem Power-Cycle-Test freigegeben.

## 19. Betriebsgrenzen V2

- 1…64 Kavitäten
- 1…32767 Zyklen pro VE
- max. 999999 Gesamtzyklen pro LOGO!-Auftrag
- max. 32767 VE-Abschlüsse pro LOGO!-Auftrag
- 50…5000 ms Ventilimpuls in 10-ms-Schritten

Größere Produktionslose werden in mehrere LOGO!-Aufträge segmentiert, solange kein erweitertes Zählkonzept freigegeben ist.

## 20. Noch offen für die reale Testmaschine

Vor Erstellung der finalen LOGO!-Datei müssen die realen elektrischen Randbedingungen festgelegt werden:

- exakter LOGO!-Typ / Firmware / Versorgungsspannung,
- Signalart und Pegel des Zyklusimpulses,
- Ventilspulenspannung und notwendiges Koppelrelais,
- tatsächliche Ausgangsart Q1,
- Endlagenrückmeldung I2 vorhanden ja/nein,
- freizugebende Ventilimpulszeit.

Danach wird diese Spezifikation in LOGO! Soft Comfort als `Partcounter_LOGO_V001` umgesetzt, simuliert und an einer Testmaschine anhand des R001.7-Abnahmeprotokolls validiert.
