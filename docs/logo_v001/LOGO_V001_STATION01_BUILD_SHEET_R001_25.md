# LOGO V001 – Station M01 Build Sheet R001.25

**Programm:** `Partcounter_LOGO_V001`  
**Protokoll:** Modbus TCP Protocol V3  
**Referenz:** M01 / 192.168.50.101 / TCP 502 / Unit ID 1  
**Partcounter-PC Beispiel:** 192.168.50.10

Dieses Blatt ist die praktische Aufbaufolge für eine neue Referenzstation in Siemens LOGO! Soft Comfort. Normative Registerquelle ist `../MODBUS_REGISTER_MAP.md`; die vollständige Verbindungszeilenliste steht in `LOGO_V001_BLOCK_CONNECTIONS_R001_25.csv`.

> Partcounter und die Standard-LOGO! sind keine Sicherheitssteuerung. Vor dem Anschluss der realen Pneumatik muss die Maschinen-Safety unabhängig geprüft sein.

---

## A. Projekt und Ethernet anlegen

1. Neues LOGO!-Projekt anlegen und das tatsächlich eingesetzte LOGO!-Basismodul auswählen.
2. Projekt als `Partcounter_LOGO_V001` speichern.
3. M01 statisch konfigurieren:
   - IP `192.168.50.101`
   - Subnetz `255.255.255.0`
   - Gateway nur wenn tatsächlich erforderlich.
4. `Allow Modbus access / Modbus-Zugriff erlauben` aktivieren.
5. LOGO! als Modbus-TCP-Server betreiben, Port `502`.
6. Wenn die Firmware Clientbeschränkung unterstützt, im Regelbetrieb nur den Partcounter-PC zulassen.
7. Noch **kein Ventil mechanisch anschließen**. Q1 zunächst nur gegen Koppelrelais/Prüflast beziehungsweise Messgerät testen.

---

## B. Modbus-/VM-Bereiche

### PC → LOGO

```text
HR1 ... HR13
VW0 ... VW24
13 Words
```

### LOGO → PC

```text
HR20 ... HR40
VW38 ... VW78
21 Words
```

Interne Scratch-Adressen:

```text
VD80  CompletedVEsScratch
VD84  LogoHeartbeatScratch
VW88  StoredPcHeartbeat
```

Die vorhandenen V2-Adressen werden in V3 nicht verschoben. V3 ergänzt VW24 sowie VW74/VW76/VW78.

---

## C. Digitale Netzwerk-Eingänge NI

Fünf digitale Netzwerk-Eingänge auf **Local VM** konfigurieren:

| Baustein | VM | Signal |
|---|---|---|
| NI1 | V5.0 | AutomaticEnabled |
| NI2 | V5.1 | ResetJobBit |
| NI3 | V5.2 | ManualVEChangeBit |
| NI4 | V5.3 | AcknowledgeAlarmBit |
| NI5 | V5.4 | PauseCountingBit |

Die Low-Bits von `VW4` liegen in `VB5`, daher V5.x und nicht V4.x.

---

## D. Analoge Netzwerk-Eingänge NAI

| Baustein | VM | Signal |
|---|---|---|
| NAI1 | VW0 | ProtocolVersionIn |
| NAI2 | VW2 | CommandSequence |
| NAI3 | VW6 | ActiveCavities |
| NAI4 | VW8 | TargetPartsHigh |
| NAI5 | VW10 | TargetPartsLow |
| NAI6 | VW12 | ValvePulse10Ms |
| NAI7 | VW18 | TargetCyclesHigh |
| NAI8 | VW20 | TargetCyclesLow |
| NAI9 | VW22 | PcHeartbeat |
| NAI10 | VW44 | CurrentVECyclesLow |
| NAI11 | VW58 | AckSequence |
| NAI12 | VW82 | CompletedVEsScratchLow |
| NAI13 | VW86 | LogoHeartbeatScratchLow |
| NAI14 | VW88 | StoredPcHeartbeat |
| NAI15 | VW24 | HoldAfterVeNumber |
| NAI16 | VW14 | JobIdHigh |
| NAI17 | VW16 | JobIdLow |

---

## E. Netzwerk-Analog-Ausgänge NAQ / VM-Status

| Ausgang | Ziel-VM | Inhalt |
|---|---|---|
| NAQ1 | VW38 | ProtocolVersion = 3 |
| NAQ2 | VW50 | CurrentVENumber |
| NAQ3 | VW52 | CompletedVEs |
| NAQ4 | VW54 | 0 / LastCompletedVECycles High |
| NAQ5 | VW60 | ActiveCavitiesEcho |
| NAQ6 | VW62 | LastCompletedVENumber |
| NAQ7 | VW64 | CompletionSequence |
| NAQ8 | VW66 | LogoHeartbeat |
| NAQ9 | VW68 | ErrorCode |
| NAQ10 | VW74 | HoldAfterVeNumberEcho |
| NAQ11 | VW76 | JobIdEcho High |
| NAQ12 | VW78 | JobIdEcho Low |

Zusätzliche Werte werden per Parameter-VM-Mapping geschrieben:
- B010 Aen → VW56,
- B027 Aen → VW58,
- B012 Aen → VW70,
- B013 Aen → VW72.

---

## F. Netzwerk 1 – Zyklusflanke B001

Baustein: **AND mit Flankenauswertung / positive Flanke**.

```text
I1 ──> B001
       positive Flankenauswertung
B001.Q = CycleEdge
```

Prüfung: I1 zwei Sekunden HIGH halten. B001 darf nur beim Übergang LOW→HIGH einmal pulsen.

---

## G. Netzwerk 2 – CountGate B002

B002 als AND mit folgenden Bedingungen aufbauen:

```text
B001.Q CycleEdge -----------> IN1
NI1 AutomaticEnabled -------> IN2
NI5 PauseCounting ----------> IN3 invertiert
B003.Q TargetReached -------> IN4 invertiert
B014.Q ValvePulseActive ----> IN5 invertiert
ConfigValid ----------------> IN6
B045.Q CompletionHoldActive -> IN7 invertiert

B002.Q -> B003.Cnt
B002.Q -> B004.Cnt
```

Die neue V3-Bedingung `NOT CompletionHoldActive` ist für die sichere VE-Grenze zwingend.

---

## H. Netzwerk 3 – CurrentVECycles B003

Baustein: **Vor-/Rückwärtszähler**.

```text
Cnt <- B002.Q
Dir = aufwärts
R   <- B016.Q OR B025.Q
On Threshold <- Parameter-VM VD18
Counterwert   -> Parameter-VM VD42
```

Freigegebener Threshold: 1…32767 Zyklen.

---

## I. Netzwerk 4 – TotalCycles B004

Vor-/Rückwärtszähler:

```text
Cnt <- B002.Q
Dir = aufwärts
R   <- B025.Q ResetJobPulse
Counterwert -> VD46
```

Freigegebener PC-Auftrag: maximal 999999 Gesamtzyklen.

---

## J. Netzwerk 5 – automatische Completion B005/B006

```text
B003.Q -> B005 On-Delay 50 ms
B005.Q -> B006 positive Flankenauswertung
B006.Q = AutoCompletionPulse
```

Die 50 ms dienen dem stabilen Snapshot des gemappten Zählerwertes; während B003.Q HIGH ist, blockiert B002 bereits weitere Zyklen.

---

## K. Netzwerk 6 – manueller VE-Wechsel B007/B008

B007 Analog-Schwellwert:

```text
Ax = NAI10 / VW44 CurrentVECyclesLow
On = 1
```

B008 AND:

```text
NewCommand ----------> IN1
NI3 ManualVEChange --> IN2
B007 CurrentNonZero -> IN3
ConfigValid ---------> IN4
```

B008.Q = ManualCompletionPulse.

Im PC-Regelbetrieb wird vor manuellem VE-Wechsel zusätzlich pausiert. Dadurch kann kein neuer I1-Zyklus zwischen manuellem Befehl und Neuparametrierung in eine falsche VE gelangen.

---

## L. Netzwerk 7 – CompletionPulse B009

```text
B006 AutoCompletion ----┐
                        ├─> B009 OR -> CompletionPulse
B008 ManualCompletion --┘
```

CompletionPulse speist Snapshot, Abschlusszähler, Ventil und Resetfolge.

---

## M. Netzwerk 8 – Abschluss-Snapshots B010…B013

### B010 LastCompletedCycles
Analog-Watchdog / Sample-and-Hold:

```text
En <- CompletionPulse
Ax <- NAI10 CurrentVECyclesLow
Aen parameter mapping -> VW56
```

VW54 bleibt 0.

### B011 CompletionReasonCandidate
Analog-Multiplexer:
- Auto = 1,
- Manual = 2.

### B012 LastCompletionReason
Analog-Watchdog:

```text
En <- CompletionPulse
Ax <- B011.AQ
Aen -> VW70
```

### B013 LastCompletedCavities

```text
En <- CompletionPulse
Ax <- NAI3 ActiveCavities
Aen -> VW72
```

Alle drei Werte müssen **vor** dem aktuellen VE-Zählerreset stabil sein.

---

## N. Netzwerk 9 – Ventil B014A/B014

B014A Analogverstärker:

```text
Ax = NAI6 ValvePulse10Ms
Gain = 1.00
Offset = 0
```

B014 Wischrelais/Pulsglied:

```text
Trg <- CompletionPulse
Zeit <- B014A
Q -> Q1
Q -> Status VEChangeActive
```

Zulässige Zeitwerte: 5…500 = 50…5000 ms.

---

## O. Netzwerk 10 – Reset nach Snapshot B015/B016

```text
B014.Q -> B015 On-Delay 20 ms
B015.Q -> B016 positive Flanke
B016.Q -> B003.R
```

Der Reset erfolgt erst, nachdem die Abschlussdaten übernommen wurden.

---

## P. Netzwerk 11 – VE-Zähler/Sequenz B017…B021

B017 CompletedVEs:

```text
Cnt <- CompletionPulse
R   <- ResetJobPulse
Counter -> VD80
```

Aus `VW82` werden abgeleitet:
- B018 CurrentVENumber = CompletedVEs + 1 → VW50,
- B019 CompletedVEs → VW52,
- B020 LastCompletedVENumber → VW62,
- B021 CompletionSequence → VW64.

Bei einem realen Projekt ist zu prüfen, dass CompletionSequence nach 32767 definiert auf 1 zurückgeführt wird. Falls B021 direkt aus CompletedVEs abgeleitet wird, ist die freigegebene Auftragsgrenze 32767 VE einzuhalten.

---

## Q. Netzwerk 12 – Command/Ack B022…B027

B022 Analogkomparator:

```text
Ax = CommandSequence
Ay = AckSequence
On = 0
Off = 0
Q wenn Cmd > Ack
```

B023 umgekehrt:

```text
Ax = AckSequence
Ay = CommandSequence
On = 0
Off = 0
Q wenn Ack > Cmd
```

B024 OR:

```text
B022.Q OR B023.Q = NewCommand
```

Damit wird jede Ungleichheit einschließlich 32767→1 erkannt.

B025 ResetJobPulse:

```text
NewCommand AND NI2 ResetJobBit
```

B027 AckSequenceStore:

```text
En <- NewCommand nach abgeschlossener Befehlsverarbeitung
Ax <- CommandSequence
Aen -> VW58
```

**Wichtig:** Ack erst setzen, wenn der Befehl und die zugehörige lokale Parameterübernahme vollständig abgeschlossen sind.

---

## R. Netzwerk 13 – Alarmquittierung B028

```text
NewCommand AND NI4 AcknowledgeAlarmBit
```

Nur technische Applikationsalarme quittieren. Keine Safety-Funktion daran koppeln.

---

## S. Netzwerk 14 – LOGO-Heartbeat B032…B035

- B032 symmetrischer Takt ca. 0,5 s HIGH / 0,5 s LOW.
- positive Flanke zählt B033.
- B033 Counter → VD84.
- B034 erzeugt Wrapreset bei 32766.
- B035 gibt `VW86 + 1` auf VW66 aus.

Sichtbarer Bereich: 1…32767.

---

## T. Netzwerk 15 – PC-Heartbeat B036…B040

B036/B037 vergleichen aktuellen HR12/VW22 mit gespeichertem VW88 in beide Richtungen. B038 OR erkennt jede Änderung einschließlich Wrap. B039 speichert den neuen Wert. B040 Off-Delay z. B. 5 s bildet `PcHeartbeatAlive`.

```text
Status V41.5 = NOT PcHeartbeatAlive
```

Dieser Status ist Diagnose und darf normale Produktion vor der geplanten Hold-Grenze nicht allein stoppen.

---

## U. Netzwerk 16 – Protocol-/Cavity-Echos B041/B042

### B041
Konstante / Analog-Multiplexer alle Werte = **3** → VW38.

### B042

```text
Ax = NAI3 ActiveCavities
Gain 1
Offset 0
Output -> VW60
```

Das Echo muss den tatsächlich übernommenen lokalen Wert darstellen.

---

## V. Netzwerk 17 – neue V3-Eingänge

Anlegen:

```text
NAI15 = VW24 HoldAfterVeNumber
NAI16 = VW14 JobIdHigh
NAI17 = VW16 JobIdLow
```

Echo-Ausgänge:

```text
NAQ10 -> VW74 HoldAfterVeNumberEcho
NAQ11 -> VW76 JobIdEchoHigh
NAQ12 -> VW78 JobIdEchoLow
```

Wenn die Werte intern zunächst gelatcht werden, müssen die NAQ-Ausgänge **vom gelatchten Zustand** gespeist werden, nicht von einem transienten Rohsignal.

---

## W. Netzwerk 18 – Completion-Hold B043…B046

### B043 HoldConfigured
Analogkomparator:

```text
Ax = NAI15 HoldAfterVE
Ay = 0
Gain 1
Offset 0
On = 0
Off = 0
```

Q = 1 für HoldAfterVE > 0.

### B044 HoldDueOrPassed
Analogkomparator:

```text
Ax = NAI12 / VW82 CompletedVEs
Ay = NAI15 / VW24 HoldAfterVE
Gain = 1
Offset = 0
On = -1
Off = -1
```

Für ganzzahlige Werte setzt der Siemens-Analogkomparator bei `Ax−Ay > On`. Mit `On = -1` bedeutet das:

```text
CompletedVEs - HoldAfterVE > -1
=> CompletedVEs >= HoldAfterVE
```

### B045 CompletionHoldActive

```text
B043 HoldConfigured
AND
B044 HoldDueOrPassed
```

B045.Q geht:
- invertiert in B002 CountGate,
- auf Status V41.7.

### B046 CompletionHoldArmed
Analogkomparator:

```text
Ax = HoldAfterVE
Ay = CompletedVEs
On = 0
Off = 0
```

Q = 1 solange HoldAfterVE > CompletedVEs. Ausgabe auf V41.6.

### Kritische Scanprüfung

Bei der M01-Abnahme muss mit sehr kurzen Testabständen gezeigt werden:

```text
letzter Zyklus VE N
-> CompletionPulse
-> CompletedVEs wird N
-> B045 HoldActive wird wahr
-> nachfolgender I1-Puls darf B002 nicht mehr passieren
```

Das ist der zentrale V3-Race-Case.

---

## X. StatusWord V41.x

| VM-Bit | Status |
|---|---|
| V41.0 | Ready |
| V41.1 | AutomaticEnabled |
| V41.2 | ValvePulseActive |
| V41.3 | Alarm |
| V41.4 | I1 raw |
| V41.5 | PcHeartbeatStale |
| V41.6 | CompletionHoldArmed |
| V41.7 | CompletionHoldActive |

---

## Y. ConfigValid

Mindestens folgende Bedingungen müssen vor CountGate/Ready gelten:

```text
ProtocolVersion == 3
1 <= ActiveCavities <= 64
TargetCyclesHigh == 0
1 <= TargetCyclesLow <= 32767
5 <= ValvePulse10Ms <= 500
1 <= HoldAfterVeNumber <= 32767
0 <= JobIdHigh <= 32767
1 <= JobIdLow <= 32767
```

`TargetPartsPerVE` wird vom PC validiert und für die lokale Zählung nicht benötigt; für eine optionale LOGO-Diagnose kann zusätzlich ein DWord-nonzero-Test aufgebaut werden.

Bei ungültiger Konfiguration:
- Ready = 0,
- CountGate geschlossen,
- kein automatischer/manueller VE-Wechsel aus der ungültigen Konfiguration.

---

## Z. Reihenfolge der ersten Inbetriebnahme M01

1. Versorgung und Ethernet ohne Q1-Last prüfen.
2. ProtocolVersion HR20 = 3 prüfen.
3. Bekannte High-/Low-Word-Testmuster lesen/schreiben.
4. CommandSequence/AckSequence ohne Resetaktion prüfen.
5. Testauftrag mit kleinen Zahlen senden und Echo von Kavitäten/Hold/JobId prüfen.
6. I1 mit langsamem Taster/Prüfsignal testen: exakt ein Count pro positiver Flanke.
7. Pause/Resume inklusive I1-HIGH-Test.
8. automatischen VE-Abschluss ohne Ventilmechanik testen.
9. Q1-Zeitmessung 50/250/750/2500/5000 ms.
10. CompletionHold mit Grenz-VE testen; direkt danach zusätzliche I1-Pulse einspeisen und **Null Leakage** beweisen.
11. Grenz-Neuparametrierung und bewusstes Resume testen.
12. TCP/WLAN während Voll-VE trennen: lokale Produktion bis zum geplanten Hold muss weiterlaufen.
13. WLAN am Hold wiederherstellen: kein spontanes Resume.
14. Partcounter während aktivem Auftrag neu starten; Recovery muss PAUSIERT enden.
15. JobId-Mismatch kontrolliert simulieren; keine automatische Übernahme.
16. LOGO-Power-Cycle ohne angeschlossene Mechanik prüfen; Q1 darf nicht unerwartet anziehen.
17. Koppelrelais anschließen und erneut Zeiten messen.
18. Ventil/Pneumatik erst nach Freigabe anschließen.
19. Teil-VE/Auftragsende real testen.
20. Etikett Original + Reprint prüfen.
21. Ergebnisse in `LOGO_V001_TEST_CASES_R001_25.csv` dokumentieren.

M01 wird erst danach zur Referenzstation für M02…M30.
