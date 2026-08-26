# Partcounter LOGO V001 – Implementierungsstandard

**Partcounter Revision:** R001.7  
**LOGO-Programm:** `Partcounter_LOGO_V001`  
**Zielplattform:** Siemens LOGO! 8.3 / 8.4  
**Kommunikation:** Modbus TCP, LOGO! als Server, PC als Client  
**Partcounter-Protokoll:** Version 2

## 1. Ziel

Dieses Dokument ist die verbindliche Engineering-Vorgabe für das erste reale LOGO!-Programm von Partcounter. Alle Maschinen verwenden dieselbe Grundlogik. Maschinenbezogen werden nur Netzwerkparameter, reale I/O-Verdrahtung und optional die Endlagenüberwachung angepasst.

Zentrale Regel: Die LOGO! zählt Maschinenzyklen und löst den VE-Wechsel lokal aus. Ein Ausfall von PC, LAN oder WLAN darf weder Zyklen verlieren noch einen fälligen Verpackungswechsel verhindern.

## 2. Hardwaregerechte Zählstrategie

Die LOGO!-Vor-/Rückwärtszähler unterstützen Zählerstände und Schwellwerte bis 999999 und stellen diese über Parameter-VM-Mapping als DWord bereit. Allgemeine LOGO!-Analogwerte und Analogberechnungen liegen jedoch im signed-16-Bit-Bereich.

Daraus folgt für Partcounter V2:

- `TotalCycles` wird nativ in einem LOGO!-Zähler geführt und als DWord übertragen.
- `CurrentVECycles` wird ebenfalls nativ gezählt.
- die LOGO! multipliziert **nicht** `Zyklen × Kavitäten`; das macht der PC.
- eine einzelne VE wird bewusst auf **maximal 32767 Zyklen** begrenzt.
- der letzte abgeschlossene VE-Zykluswert kann dadurch innerhalb der LOGO! mit einem Analog-Watchdog sicher als 16-Bit-Snapshot gespeichert werden.
- CommandSequence, CompletionSequence und Heartbeats bleiben ebenfalls im Bereich 1…32767.

Diese Begrenzung reduziert LOGO!-Sonderlogik und ist für reale Verpackungseinheiten ausreichend. Der Gesamtzykluszähler bleibt davon unabhängig und kann bis zum nativen LOGO!-Grenzwert 999999 zählen.

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

`VD18` wird per Parameter-VM-Mapping direkt dem `On Threshold` des VE-Zählers zugeordnet. Der PC schreibt den DWord-Wert High Word vor Low Word; in V2 ist wegen der 32767-Grenze das High Word immer 0.

Der Ventilwert wird in 10-ms-Einheiten übertragen. Beispiel: 750 ms → HR7 = 75. Der Zeitbaustein wird fest auf die Zeitbasis 10 ms eingestellt und sein Zeitparameter auf VW12 gemappt.

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

Parameter-VM-Mapping:

- `CurrentVECycles.Counter` → VD42
- `TotalCycles.Counter` → VD46
- `LastCompletedVECycles` wird über den gespeicherten Vergleichswert `Aen` eines Analog-Watchdog auf VW56 ausgegeben.

## 5. CommandWord

Die unteren Bits des Modbus-Wortes liegen aufgrund der VM-Byteordnung im Low Byte `VB5`. In der LOGO!-Schaltung werden daher lokale VM-Bits verwendet:

| Funktion | CommandWord-Bit | lokales VM-Bit |
|---|---:|---|
| Automatic enabled | 0 | V5.0 |
| Reset / neuer Auftrag | 1 | V5.1 |
| manueller VE-Wechsel | 2 | V5.2 |
| Alarm quittieren | 3 | V5.3 |
| Zählung pausieren | 4 | V5.4 |

StatusWord HR21 liegt in VW40; die unteren Statusbits werden entsprechend auf V41.0…V41.5 ausgegeben.

## 6. Funktionsblockplan V001

Die folgenden Blocknummern sind die Zielnummerierung für den ersten Aufbau in LOGO! Soft Comfort. Falls LSC beim Einfügen automatisch andere Nummern vergibt, werden die Nummern nach Fertigstellung einmalig im Dokument nachgezogen und danach eingefroren.

| Block | Typ | Name / Aufgabe | Kernparameter |
|---|---|---|---|
| B001 | AND mit Flankenauswertung | `CycleEdge` | I1 an Eingang 1; übrige Eingänge unbenutzt |
| B002 | AND | `CountGate` | CycleEdge AND Automatic AND NOT Pause AND NOT VeChangeActive |
| B003 | Vor-/Rückwärtszähler | `CurrentVECycles` | Cnt=B002, Dir=0, R=ResetCurrentVE, On=VD18, Off=0, retentiv |
| B004 | Vor-/Rückwärtszähler | `TotalCycles` | Cnt=B002, Dir=0, R=ResetJob, retentiv |
| B005 | AND mit Flankenauswertung | `AutoCompletionPulse` | Flanke von B003.Q |
| B006 | Analog-Schwellwertschalter | `CurrentVeNonZero` | Ax=B003.Cnt, On=0, Off=0 |
| B007 | AND | `ManualCompletionPulse` | NewCommand AND V5.2 AND B006.Q |
| B008 | OR | `CompletionPulse` | B005 OR B007 |
| B009 | Analog-Watchdog | `LastCompletedCycles` | En=B008, Ax=B003.Cnt; gespeichertes Aen → VW56 |
| B010 | Impuls-/Wischrelais | `ResetCurrentVeDelay` | Trigger=B008; kurzer verzögerter Resetimpuls |
| B011 | Impuls-/Wischrelais | `ValvePulse` | Trigger=B008; Zeitbasis 10 ms; Zeitwert via VW12 |
| B012 | Vor-/Rückwärtszähler | `CompletedVEs` | Cnt=B008, R=ResetJob; max. 32767 |
| B013 | Analogverstärker | `CompletedVEs_VM` | Referenz B012.Cnt → VW52 |
| B014 | Analogberechnung | `CurrentVENumber` | B012.Cnt + 1 → VW50 |
| B015 | Analogverstärker | `CompletionSequence_VM` | Referenz B012.Cnt → VW64 |
| B016 | Analog-MUX | `CompletionReason` | 1=automatisch, 2=manuell → VW70 |
| B017 | Analog-Watchdog | `AckSequenceStore` | En=NewCommand, Ax=CommandSequence → VW58 |
| B018 | Analogkomparator | `CmdGreaterAck` | CommandSequence - AckSequence > 0 |
| B019 | Analogkomparator | `AckGreaterCmd` | AckSequence - CommandSequence > 0 |
| B020 | OR | `NewCommand` | B018 OR B019 |
| B021 | Impulsgeber / Takt | `LogoHeartbeatClock` | Diagnose-Takt |
| B022 | Vor-/Rückwärtszähler | `LogoHeartbeatCounter` | 1…32767, danach Reset/Wrap |
| B023 | Heartbeat-Überwachung | `PcHeartbeatWatch` | Änderung von VW22 überwachen |
| B024 | Alarm-/Freigabelogik | `ConfigAndFaultLogic` | Parameter-/Endlagenfehler |

Zusätzliche Network-/Analog-Network-Inputs lesen die relevanten VW-/V-Bereiche. Die endgültige LSC-Blockliste wird beim Aufbau des realen Projekts exportiert und mit dieser Tabelle abgeglichen.

## 7. Warum B001 vor dem CountGate liegt

Der Zyklusimpuls wird **vor** Auto/Pause/Wechsel-Freigaben auf eine echte positive Flanke reduziert. Würde zuerst ein normales AND aus I1 und der Freigabe gebildet, könnte das Aufheben einer Pause bei noch anstehendem I1-Pegel eine künstliche 0→1-Flanke und damit einen falschen Zählimpuls erzeugen.

Das LOGO!-`AND mit Flankenauswertung` liefert dagegen nur für einen Programmzyklus ein Signal, wenn seine Eingangskombination neu von 0 nach 1 wechselt. B001 isoliert damit die reale Maschinenflanke; B002 darf diesen bereits erzeugten Ein-Zyklus-Puls anschließend nur noch freigeben oder sperren.

## 8. Snapshot der abgeschlossenen VE

Der Analog-Watchdog B009 wird als Sample-and-Hold verwendet. Bei einer positiven Flanke an `En` speichert dieser Block seinen analogen Eingang als Vergleichswert `Aen`. Als Eingang wird der aktuelle Wert des VE-Zählers B003 referenziert.

Ablauf:

```text
B003 erreicht Soll / manueller Wechsel
        ↓
CompletionPulse B008
        ├─> B009 speichert CurrentVECycles als Aen
        ├─> CompletedVEs +1
        ├─> LastCompletionReason setzen
        ├─> B011 Ventilimpuls starten
        └─> B010 erzeugt erst danach ResetCurrentVE
```

Damit ist `LastCompletedVECycles` stabil im Status verfügbar, obwohl B003 für die nächste VE zurückgesetzt wurde.

## 9. Befehlssequenz ohne 16-Bit-Überlauf

`CommandSequence` und `AckSequence` liegen zwischen 1 und 32767. Nach 32767 folgt wieder 1.

Da ein einzelner Analogkomparator nur `Ax - Ay` bewertet, wird Ungleichheit symmetrisch mit zwei Komparatoren erkannt:

```text
CmdGreaterAck = CommandSequence - AckSequence > 0
AckGreaterCmd = AckSequence - CommandSequence > 0
NewCommand    = CmdGreaterAck OR AckGreaterCmd
```

Beim Wrap 32767 → 1 erkennt der zweite Zweig die Änderung. B017 übernimmt den neuen Wert anschließend als AckSequence.

## 10. Zähl- und VE-Ablauf

Zählen nur wenn:

```text
CycleEdge AND AutomaticEnabled AND NOT PauseCounting AND NOT VeChangeActive
```

Automatischer Abschluss:

```text
CurrentVECycles >= TargetCyclesPerVE
```

Manueller Abschluss:

```text
NewCommand AND ManualVeChangeBit AND CurrentVECycles > 0
```

Der aktuelle VE-Zähler wird erst nach dem Snapshot zurückgesetzt. Q1 wird monostabil für die konfigurierte Zeit eingeschaltet. Während Q1 aktiv ist, ist der CountGate gesperrt.

## 11. Parameterprüfung

PC-seitig wird vor jedem Auftrag geprüft:

- ActiveCavities = 1…64
- TargetPartsPerVE > 0
- TargetCyclesPerVE = 1…32767
- ValvePulseMs = 50…5000
- ValvePulseMs muss durch 10 teilbar sein

LOGO!-seitig werden Protokoll, Kavitäten, zulässige Steuerbits und die plausiblen Zeit-/Zielwerte soweit mit einfachen 16-Bit-Vergleichen abgesichert. Der PC ist der einzige freigegebene schreibende Modbus-Client.

## 12. Fehlercodes

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

## 13. Heartbeats

PC und LOGO! verwenden Werte 1…32767 und springen danach wieder auf 1. Entscheidend ist nicht die numerische Differenz, sondern dass sich der jeweilige Wert innerhalb des Diagnosefensters ändert.

Ein stehender PC-Heartbeat setzt nur den Kommunikationsstatus. Die lokale Zählung und ein fälliger automatischer VE-Wechsel laufen weiter.

## 14. Neustartverhalten

### PC-Neustart

Die LOGO! läuft mit den zuletzt gültigen Parametern weiter. Partcounter liest `AckSequence`, synchronisiert seine lokale Sequenz und sendet erst danach einen neuen Befehl.

### LOGO!-Neustart

- Q1 muss beim Start AUS sein.
- kein selbsttätiger Ventilimpuls allein aufgrund eines alten Ausgangszustands.
- Zählerretentivität wird an der Testmaschine bewusst geprüft.
- nach realem LOGO!-Spannungswiederkehrtest ist das Wiederanlaufkonzept freizugeben; ein LOGO!-Power-Cycle ist nicht mit einem reinen WLAN-/PC-Ausfall gleichzusetzen.

## 15. Betriebsgrenzen V2

- 1…64 Kavitäten
- 1…32767 Zyklen pro VE
- max. 999999 Gesamtzyklen pro LOGO!-Auftrag
- max. 32767 VE-Abschlüsse pro LOGO!-Auftrag
- 50…5000 ms Ventilimpuls in 10-ms-Schritten

Größere Produktionslose werden in mehrere LOGO!-Aufträge segmentiert, solange kein erweitertes Zählkonzept freigegeben ist.

## 16. Noch offen für die reale Testmaschine

Vor Erstellung der finalen LOGO!-Datei müssen nur noch die realen elektrischen Randbedingungen festgelegt werden:

- exakter LOGO!-Typ / Firmware / Versorgungsspannung,
- Signalart und Pegel des Zyklusimpulses,
- Ventilspulenspannung und notwendiges Koppelrelais,
- tatsächliche Ausgangsart Q1,
- Endlagenrückmeldung I2 vorhanden ja/nein,
- freizugebende Ventilimpulszeit.

Danach wird diese Spezifikation in LOGO! Soft Comfort als `Partcounter_LOGO_V001` umgesetzt, simuliert und an einer Testmaschine anhand des R001.7-Abnahmeprotokolls validiert.
