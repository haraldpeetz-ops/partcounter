# Partcounter LOGO V001 – Station 01 Build Sheet

**Revision:** R001.8  
**LOGO!-Programm:** `Partcounter_LOGO_V001`  
**Referenzhardware:** Siemens LOGO! 12/24RCEo, MLFB `6ED1052-2MD08-0BA2`  
**Versorgung:** 24 V DC  
**Kommunikation:** Modbus TCP, ProtocolVersion 2

## 1. Zweck

Dieses Dokument ist die reproduzierbare Aufbauvorgabe für die erste reale Partcounter-LOGO!-Station. Es ist **kein automatischer Import für LOGO! Soft Comfort**, sondern eine eindeutige Engineering-Netzliste: Blocktypen, Signalnamen, Verbindungen, VM-/Modbus-Adressen, Hardwareparameter und Prüfreihenfolge sind festgelegt.

## 2. Station-01-Hardware

| Funktion | Festlegung |
|---|---|
| LOGO! | 6ED1052-2MD08-0BA2 / LOGO! 12/24RCEo |
| Betriebsspannung | 24 V DC |
| Zyklussignal | I1, 24 V DC |
| Zyklusauswertung | positive Flanke = genau 1 Maschinenzyklus |
| Ventilausgang | Q1 |
| Ventil | kleines handelsübliches Festo-Pneumatikventil, 24 V DC |
| Standard-Ausgangskonzept | Q1 schaltet ein 24-V-DC-Koppel-/Interface-Relais; dessen Kontakt schaltet das Ventil |
| Endlagensensor | nicht vorhanden |
| optionale Endlage | I2 bleibt im Standardprogramm vorbereitet |
| Endlagenüberwachung Station 01 | OFF |
| Ventilimpuls | 50…5000 ms |
| Raster | 10 ms |
| Standardwert | 750 ms |

### 2.1 I1-Verdrahtung

Das 24-V-Zyklussignal darf direkt auf I1 geführt werden, **wenn** Signalquelle und LOGO! eine zulässige gemeinsame 0-V-Referenz besitzen. Bei galvanisch getrennten oder unklaren Maschinenkreisen wird ein geeignetes Koppelrelais oder Optokoppler-Interface zwischen Maschine und I1 eingesetzt.

### 2.2 Q1-Verdrahtung

Für die Referenzmaschine wird nicht auf die unbekannte Festo-Spulenleistung gewartet. Der Standard lautet:

```text
LOGO! Q1 Relaiskontakt
        │
        └── 24-V-DC-Interface-/Koppelrelais
                     │
                     └── potentialfreier Arbeitskontakt
                                  │
                                  └── 24-V-DC-Festo-Ventil
```

- Q1-Zweig extern absichern; Sicherungswert erst bei realer Verdrahtung dimensionieren.
- Koppelrelais-Spule mit geeigneter DC-Entstörung verwenden.
- Ventilspule ebenfalls entstören, sofern Stecker/Ventil keine integrierte Schutzbeschaltung besitzt.
- Die unbekannten Ventildaten sind **kein Entwicklungsblocker**.

## 3. Modbus-/VM-Grundlage

### PC → LOGO!

| HR | VM | Inhalt |
|---:|---|---|
| HR1 | VW0 | ProtocolVersion = 2 |
| HR2 | VW2 | CommandSequence |
| HR3 | VW4 | CommandWord |
| HR4 | VW6 | ActiveCavities |
| HR5/HR6 | VD8 | TargetPartsPerVE |
| HR7 | VW12 | ValvePulse10Ms |
| HR8/HR9 | VD14 | JobId |
| HR10/HR11 | VD18 | TargetCyclesPerVE |
| HR12 | VW22 | PC Heartbeat |

### LOGO! → PC

| HR | VM | Inhalt |
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

## 4. CommandWord HR3

Die unteren Bits werden verwendet:

| Bit | Funktion |
|---:|---|
| 0 | AutomaticEnabled |
| 1 | ResetJob / neuer Auftrag |
| 2 | ManualVEChange |
| 3 | AcknowledgeAlarm |
| 4 | PauseCounting |

One-Shot-Funktionen `ResetJob`, `ManualVEChange` und `AcknowledgeAlarm` dürfen nur bei einer neuen CommandSequence ausgewertet werden.

## 5. Verbindliche Betriebsgrenzen

```text
ActiveCavities:       1 … 64
TargetCyclesPerVE:    1 … 32767
TotalCycles:          0 … 999999 pro LOGO!-Auftrag
CommandSequence:      1 … 32767
PC Heartbeat:         1 … 32767
LOGO Heartbeat:       1 … 32767
ValvePulseMs:         50 … 5000 ms
ValvePulse Raster:    10 ms
```

Partcounter rechnet die Ventilzeit vor der Übertragung um:

```text
ValvePulse10Ms = ValvePulseMs / 10
```

Beispiele: 50 ms → 5, 750 ms → 75, 5000 ms → 500.

## 6. LOGO!-Funktionsblöcke

Die Blocknummern sind Zielnummern. Falls LOGO! Soft Comfort beim Einfügen andere Nummern vergibt, sind **Signalname und Funktion** maßgeblich; die reale Nummernliste wird anschließend zurückdokumentiert.

| Zielblock | Blocktyp | Signalname | Aufgabe |
|---|---|---|---|
| B001 | AND mit Flankenauswertung | CycleEdge | positive Flanke von I1 auf genau einen Programmdurchlauf reduzieren |
| B002 | AND | CountGate | CycleEdge nur bei Automatic, nicht Pause und nicht VE-Wechsel durchlassen |
| B003 | Vor-/Rückwärtszähler | CurrentVECycles | aktuelle VE-Zyklen zählen; Schwellwert aus VD18 |
| B004 | Vor-/Rückwärtszähler | TotalCycles | Gesamtzyklen des LOGO!-Auftrags zählen |
| B005 | AND mit Flankenauswertung | AutoCompletionPulse | steigende Flanke von B003.Q = VE automatisch voll |
| B006 | Analog-Schwellwertschalter | CurrentVeNonZero | TRUE bei CurrentVECycles > 0 |
| B007 | AND | ManualCompletionPulse | neuer Befehl + Manual-Bit + CurrentVECycles > 0 |
| B008 | OR | CompletionPulse | B005 oder B007 |
| B009 | Analog-Watchdog | LastCompletedCycles | CurrentVECycles beim CompletionPulse speichern |
| B010 | verzögerter Impuls | ResetCurrentVE | CurrentVECycles erst nach Snapshot zurücksetzen |
| B011 | Impuls-/Wischrelais | ValvePulse | Q1 für ValvePulse10Ms schalten |
| B012 | Vor-/Rückwärtszähler | CompletedVEs | pro CompletionPulse +1; Reset bei neuem Auftrag |
| B013 | Analogverstärker | CompletedVEsVM | B012-Zählerstand → VW52 |
| B014 | Analogberechnung | CurrentVENumber | CompletedVEs + 1 → VW50 |
| B015 | Analogverstärker | LastCompletedVENumber | CompletedVEs → VW62 |
| B016 | Analogverstärker | CompletionSequence | Completion-Zähler → VW64 |
| B017 | Analog-MUX | CompletionReasonCandidate | automatisch=1, manuell=2 |
| B018 | Analog-Watchdog | LastCompletionReason | Grund beim CompletionPulse speichern → VW70 |
| B019 | Analog-Watchdog | LastCompletedCavities | ActiveCavities beim CompletionPulse speichern → VW72 |
| B020 | Analogverstärker | ActiveCavitiesEcho | aktive Kavitäten → VW60 |
| B021 | Analog-Watchdog | AckSequenceStore | neue CommandSequence als Ack speichern → VW58 |
| B022 | Analogkomparator | CmdGreaterAck | CommandSequence > AckSequence |
| B023 | Analogkomparator | AckGreaterCmd | AckSequence > CommandSequence |
| B024 | OR | NewCommand | B022 OR B023 |
| B025 | Taktgeber | LogoHeartbeatClock | Diagnose-Takt erzeugen |
| B026 | Vor-/Rückwärtszähler | LogoHeartbeatCounter | LOGO-Heartbeat zählen |
| B027 | Diagnosegruppe | PcHeartbeatWatch | Änderung von VW22 überwachen |
| B028 | Logikgruppe | ConfigAndFaultLogic | Protokoll-/Parameter-/optionale Endlagenfehler |

## 7. Kernverbindungen

### Netzwerk 1 – reale Zyklusflanke

```text
I1 → B001 CycleEdge
B001.Q → B002.CountPulseInput
AutomaticEnabled → B002
NOT PauseCounting → B002
NOT VeChangeActive → B002
B002.Q → B003.Cnt
B002.Q → B004.Cnt
```

**Wichtig:** Die Flankenauswertung liegt vor der Freigabelogik. Dadurch erzeugt das Aufheben einer Pause bei noch HIGH liegendem I1 keinen falschen Zyklus.

### Netzwerk 2 – automatischer Abschluss

```text
B003.OnThreshold ← VD18
B003.Q → B005 Edge
B005.Q → B008
```

### Netzwerk 3 – manueller Abschluss

```text
CommandSequence / AckSequence → B022/B023
B022.Q OR B023.Q → B024 NewCommand
B024 + ManualVEChangeBit + B006(CurrentVECycles>0) → B007
B007.Q → B008
```

### Netzwerk 4 – Abschlussdaten

```text
B008 CompletionPulse → B009.En
CurrentVECycles → B009.Ax
B009.Aen → VW56

B008 → B018.En
B017 CompletionReasonCandidate → B018.Ax
B018.Aen → VW70

B008 → B019.En
ActiveCavities → B019.Ax
B019.Aen → VW72

B008 → B012.Cnt
B012.Cnt → VW52
B012.Cnt → VW62
B012.Cnt → VW64
B012.Cnt + 1 → VW50
```

### Netzwerk 5 – Ventil und Reset

```text
B008 CompletionPulse → B011 Trigger
VW12 → B011 Zeitparameter, Zeitbasis fest 10 ms
B011.Q → Q1
B011.Q = VeChangeActive

B008 CompletionPulse → B010
B010.Q → B003.Reset
```

ResetCurrentVE muss erst nach dem Snapshot ausgelöst werden. Die Verzögerung bleibt kürzer als der kleinste Ventilimpuls von 50 ms.

### Netzwerk 6 – neuer Auftrag

```text
NewCommand AND ResetJobBit → ResetJobPulse
ResetJobPulse → B003.Reset
ResetJobPulse → B004.Reset
ResetJobPulse → B012.Reset
```

Ein Parameterupdate ohne ResetJob darf B004/B012 nicht zurücksetzen.

## 8. Endlagenüberwachung

### Station 01

```text
EndPositionMonitoring = OFF
I2 = nicht ausgewertet
```

### Spätere Stationen

Die Struktur bleibt vorbereitet:

```text
EndPositionMonitoring = ON
I2 = Endlagenbestätigung
kein I2 innerhalb Timeout → ErrorCode 10
```

Die Endlagenfunktion ist nicht Bestandteil des ersten realen Q1-Tests und darf Station 01 nicht blockieren.

## 9. StatusWord HR21

| Bit | Bedeutung |
|---:|---|
| 0 | LOGO ready |
| 1 | Automatic active |
| 2 | VE change active |
| 3 | Alarm |
| 4 | Cycle input active |
| 5 | PC heartbeat stale |

## 10. ErrorCode

| Code | Bedeutung |
|---:|---|
| 0 | kein Fehler |
| 1 | falsche ProtocolVersion |
| 2 | ActiveCavities außerhalb 1…64 |
| 3 | TargetPartsPerVE = 0 |
| 4 | TargetCyclesPerVE außerhalb 1…32767 |
| 5 | Ventilzeit außerhalb 50…5000 ms / falsches 10-ms-Raster |
| 10 | optionale Endlage nicht erreicht |
| 30 | interner ungültiger Ablaufzustand |

## 11. Erste Simulationswerte in LOGO! Soft Comfort

### Test A – 4 Kavitäten, VE 20

```text
ActiveCavities = 4
TargetPartsPerVE = 20
TargetCyclesPerVE = 5
ValvePulseMs = 750
ValvePulse10Ms = 75
```

Erwartung: Nach fünf I1-Flanken genau ein CompletionPulse, Q1 = 750 ms, LastCompletedVECycles = 5, LastCompletedCavities = 4, PC rekonstruiert 20 Teile.

### Test B – 64 Kavitäten, VE 1000

```text
ActiveCavities = 64
TargetPartsPerVE = 1000
TargetCyclesPerVE = 16
```

Erwartung: Nach 16 I1-Flanken VE-Abschluss; PC berechnet 1024 Teile und 24 Teile zyklusbedingte Mehrmenge.

### Test C – manueller Wechsel

Nach drei Zyklen `ManualVEChange` mit neuer CommandSequence setzen.

Erwartung: genau ein Abschluss, LastCompletionReason = 2, danach CurrentVECycles = 0.

### Test D – Pause

I1 HIGH halten, Pause aktivieren und anschließend lösen.

Erwartung: Beim Lösen der Pause **kein künstlicher Zählimpuls**; erst die nächste reale 0→1-Flanke von I1 zählt.

## 12. Freigabereihenfolge an Station 01

1. LOGO!-Projekt offline vollständig erstellen.
2. Simulation der Tests A–D durchführen.
3. LOGO! auf 24-V-Versorgung anschließen, Q1 zunächst ohne Ventil testen.
4. I1 mit definiertem 24-V-Testsignal prüfen.
5. Q1 zunächst nur auf Koppelrelais prüfen.
6. Impulszeiten 50 / 250 / 750 / 2500 / 5000 ms messen.
7. Erst danach Ventil über Koppelrelais anschließen.
8. Mechanischen Wechsel bei stillstehender Maschine prüfen.
9. Anschließend Maschinenzyklus ankoppeln.
10. Modbus-PC-Kopplung und Wiederverbindung testen.
11. Mehrfach-VE-Test durchführen.
12. Station nach Inbetriebnahmeprotokoll freigeben.

## 13. Safety-Grenze

Partcounter und LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztür, Maschinenfreigaben und alle sicherheitsgerichteten Funktionen verbleiben außerhalb dieser Schaltung in den dafür vorgesehenen sicheren Maschinenkreisen.

## 14. R001.8-Ergebnis

Mit diesem Build Sheet sind für Station 01 keine weiteren Ventildaten erforderlich, um die LOGO!-Software aufzubauen und offline zu simulieren. Spulenstrom, Spulenleistung und vorhandene Ventilentstörung werden erst bei der physischen Inbetriebnahme dokumentiert und beeinflussen nur die Dimensionierung des Koppelrelais-/Ventilzweigs, nicht die LOGO!-Programmlogik.
