# Partcounter R001.25 – Siemens LOGO! V001 Complete Engineering Guide

**Dokumentrevision:** R001.25  
**LOGO!-Programm:** `Partcounter_LOGO_V001`  
**Partcounter-Protokoll:** Modbus TCP Protocol V3  
**Referenzstation:** M01  
**Referenzhardware:** Siemens LOGO! 24-V-DC-Variante mit Ethernet/Modbus-Unterstützung  
**Referenzsoftware:** LOGO! Soft Comfort passend zum eingesetzten LOGO!-Firmwarestand

---

## 1. Zweck

Dieses Dokument beschreibt den vollständigen technischen Sollaufbau der LOGO!-Seite für Partcounter R001.25. Es ist die lesbare Engineering-Fassung; die verbindlichen Detailtabellen sind:

- `MODBUS_REGISTER_MAP.md`
- `LOGO_CONTROL_LOGIC.md`
- `logo_v001/LOGO_V001_VM_MAP_R001_25.csv`
- `logo_v001/LOGO_V001_BLOCK_CONNECTIONS_R001_25.csv`
- `logo_v001/LOGO_V001_IO_WIRING_R001_25.csv`
- `logo_v001/LOGO_V001_TEST_CASES_R001_25.csv`
- `logo_v001/LOGO_V001_STATION01_BUILD_SHEET_R001_25.md`

Bei einem Widerspruch gilt zuerst der aktuelle PC-Code `ModbusRegisterMap.cs`, danach `MODBUS_REGISTER_MAP.md`. Ein solcher Widerspruch ist vor Inbetriebnahme als Engineering-Fehler zu behandeln und zu korrigieren.

---

## 2. Architekturentscheidung

### LOGO! lokal verantwortlich

Die LOGO! arbeitet für die zeitkritische Maschinenebene:
- positive I1-Zyklusflanke erkennen,
- Werkzeugzyklen zählen,
- aktuelle VE überwachen,
- VE lokal abschließen,
- Abschlussdaten vor Reset puffern,
- VE-Wechsler Q1 pulsen,
- CompletedVEs / CompletionSequence fortschreiben,
- vorab programmierten Completion-Hold lokal durchsetzen,
- Status/Diagnose über VM bereitstellen.

### PC verantwortlich

Partcounter übernimmt:
- fachliche Auftragsdaten,
- Artikel/Kavitäten/VE-Soll,
- Berechnung der Zyklusziele,
- Planung kritischer VE-Grenzen,
- technische Produktionsinstanz-JobId,
- Command/Ack-Transaktionen,
- Restart Recovery,
- Historie,
- Etikettierung/Reprint,
- ALS/proALPHA,
- Diagnose/Backup/Update.

### Warum die kritische Grenzlogik lokal ist

Eine ausschließlich PC-seitige Reaktion auf `CompletionSequence` wäre bei kurzen Zykluszeiten nicht deterministisch: zwischen lokalem Abschluss und dem nächsten PC-Poll könnte eine weitere I1-Flanke auftreten. Protocol V3 programmiert deshalb `HoldAfterVeNumber` vorab in die LOGO!.

---

## 3. Safety-Grenze

Partcounter und diese Standard-LOGO!-Applikation sind **keine Sicherheitssteuerung**.

Nicht über Partcounter/LOGO lösen:
- Not-Halt,
- Schutztür,
- sichere Antriebsabschaltung,
- sichere Pneumatikentlüftung,
- sonstige PL/SIL-Funktionen.

Der Kisten-/VE-Wechsler ist als normale Applikationsbewegung zu bewerten. Die mechanische und elektrische Anlage muss so ausgelegt sein, dass Kommunikations- oder Spannungsverlust keinen gefährlichen Zustand verursacht.

---

## 4. Referenzhardware M01

### Versorgung
- LOGO!: 24 V DC.
- definierte 0-V-Referenz.
- abgesicherter Steuerkreis.

### Eingang I1
I1 erhält ein gültiges 24-V-Zyklussignal der Spritzgussmaschine. Eine positive Flanke entspricht exakt einem Werkzeugzyklus.

Wenn Signalreferenz oder galvanische Trennung unklar sind, nicht direkt verbinden, sondern geeignetes Koppelrelais/Optokoppler vorsehen.

### Ausgang Q1
Empfohlene Struktur:

```text
LOGO Q1 potentialfreier Kontakt
        ↓
24-V-Interface-/Koppelrelais
        ↓
abgesicherter Relaiskontakt
        ↓
24-V-Magnetventil
```

Die reale Ventilspule ist anhand Typenschild/Datenblatt auf Strom, Leistung, Einschaltstrom und Schutzbeschaltung zu prüfen.

### Optionale Signale
- I2 Endlagensensor VE-Wechsler: M01 zunächst deaktiviert, bis real validiert.
- I3 lokale technische Quittierung.
- Q2 optionale Wechselanzeige.
- Q3 optionale technische Sammelstörung.

---

# TEIL A – NETZWERK / MODBUS

## 5. Referenz-IP-Konzept

```text
Partcounter PC     192.168.50.10
Subnetz            255.255.255.0
M01 LOGO           192.168.50.101
M02 LOGO           192.168.50.102
...
M30 LOGO           192.168.50.130
Modbus TCP         502
Unit ID            1
```

Jede LOGO! kann Port 502 verwenden, weil jede Station eine eigene IP besitzt.

Im Regelbetrieb Netzwerkzugriff soweit möglich auf den Partcounter-PC beschränken und Produktionsnetz segmentieren.

## 6. Modbus-Server aktivieren

In LOGO! Soft Comfort / Geräte-Ethernetkonfiguration:
1. feste IP einstellen,
2. Subnetz einstellen,
3. Gateway nur bei Bedarf,
4. Modbus-Zugriff erlauben,
5. LOGO! als Server,
6. Port 502,
7. Projekt in LOGO! übertragen,
8. Erreichbarkeit PC↔LOGO prüfen.

---

## 7. Protocol V3 Registerbereiche

### PC → LOGO

```text
Remote HR1…HR13
Local  VW0…VW24
13 Words
```

| HR | VM | Inhalt |
|---:|---|---|
| 1 | VW0 | ProtocolVersion=3 |
| 2 | VW2 | CommandSequence |
| 3 | VW4 | CommandWord |
| 4 | VW6 | ActiveCavities |
| 5/6 | VD8 | TargetPartsPerVE |
| 7 | VW12 | ValvePulse10Ms |
| 8/9 | VD14 | JobId |
| 10/11 | VD18 | TargetCyclesPerVE |
| 12 | VW22 | PcHeartbeat |
| 13 | VW24 | HoldAfterVeNumber |

### LOGO → PC

```text
Remote HR20…HR40
Local  VW38…VW78
21 Words
```

| HR | VM | Inhalt |
|---:|---|---|
| 20 | VW38 | ProtocolVersion=3 |
| 21 | VW40 | StatusWord |
| 22/23 | VD42 | CurrentVECycles |
| 24/25 | VD46 | TotalCycles |
| 26 | VW50 | CurrentVENumber |
| 27 | VW52 | CompletedVEs |
| 28/29 | VW54/VW56 | LastCompletedVECycles |
| 30 | VW58 | AckSequence |
| 31 | VW60 | ActiveCavitiesEcho |
| 32 | VW62 | LastCompletedVENumber |
| 33 | VW64 | CompletionSequence |
| 34 | VW66 | LogoHeartbeat |
| 35 | VW68 | ErrorCode |
| 36 | VW70 | LastCompletionReason |
| 37 | VW72 | LastCompletedCavities |
| 38 | VW74 | HoldAfterVeNumberEcho |
| 39 | VW76 | JobIdEcho High |
| 40 | VW78 | JobIdEcho Low |

### Interner VM

```text
VD80 CompletedVEsScratch
VD84 LogoHeartbeatScratch
VW88 StoredPcHeartbeat
```

---

## 8. 32-Bit-Reihenfolge

Partcounter verwendet:

```text
High Word zuerst
Low Word danach
value = (high << 16) | low
```

Beispiel `VD14 JobId`:
- HR8 / VW14 = High Word,
- HR9 / VW16 = Low Word.

Diese Reihenfolge ist an M01 mit bekannten Testwerten real zu beweisen.

---

# TEIL B – COMMAND / STATUS

## 9. CommandWord VW4

Niederwertige Bits liegen in `V5.x`:

| Bit | VM | Funktion |
|---:|---|---|
| 0 | V5.0 | AutomaticEnabled |
| 1 | V5.1 | ResetJob |
| 2 | V5.2 | ManualVEChange |
| 3 | V5.3 | AcknowledgeAlarm |
| 4 | V5.4 | PauseCounting |

ResetJob, ManualVEChange, AcknowledgeAlarm sind One-Shots über die CommandSequence.

## 10. StatusWord VW40

| Bit | VM | Funktion |
|---:|---|---|
| 0 | V41.0 | Ready |
| 1 | V41.1 | AutomaticEnabled |
| 2 | V41.2 | VEChangeActive |
| 3 | V41.3 | Alarm |
| 4 | V41.4 | CycleInputActive |
| 5 | V41.5 | PcHeartbeatStale |
| 6 | V41.6 | CompletionHoldArmed |
| 7 | V41.7 | CompletionHoldActive |

---

## 11. Command/Ack-Handschlag

Ein Befehl ist neu, wenn:

```text
CommandSequence != AckSequence
```

Um den Wrap 32767→1 sicher zu erkennen, werden zwei gerichtete Analogvergleiche OR-verknüpft:

```text
Cmd > Ack
OR
Ack > Cmd
```

Nach vollständig bearbeitetem Befehl übernimmt B027 die CommandSequence in VW58/AckSequence.

### PC-Bestätigung

Partcounter akzeptiert einen Parameterbefehl erst bei:

```text
AckSequence == gesendete Sequence
AND ErrorCode == 0
AND ActiveCavitiesEcho == Soll
AND HoldAfterVeNumberEcho == Soll
AND JobIdEcho == Soll
```

### Retry

Bei verlorener Verbindung benutzt der PC **dieselbe Sequence** erneut. Wenn die LOGO! bereits bestätigt hat, darf der One-Shot nicht wiederholt werden.

---

# TEIL C – FBD HAUPTNETZWERKE

## 12. Netzwerk 1: B001 CycleEdge

I1 → AND mit positiver Flankenauswertung.

Nur LOW→HIGH erzeugt einen Zykluspuls.

## 13. Netzwerk 2: B002 CountGate

```text
CycleEdge
AND AutomaticEnabled
AND NOT PauseCounting
AND NOT TargetReached
AND NOT ValveActive
AND ConfigValid
AND NOT CompletionHoldActive
```

B002.Q zählt B003 und B004.

## 14. B003 CurrentVECycles

- Cnt = B002.Q.
- Reset = VE-Reset oder ResetJob.
- Threshold = VD18.
- Counter = VD42.

## 15. B004 TotalCycles

- Cnt = B002.Q.
- Reset nur ResetJob.
- Counter = VD46.
- maximal 999999 im freigegebenen Auftrag.

## 16. B005/B006 AutoCompletion

B003.Q wird 50 ms stabilisiert und anschließend auf eine positive Flanke reduziert. Resultat: exakt ein `AutoCompletionPulse`.

## 17. B007/B008 ManualCompletion

ManualCompletion nur bei:

```text
NewCommand
AND ManualVEChangeBit
AND CurrentVECycles > 0
AND ConfigValid
```

Der PC pausiert vor einem manuellen VE-Wechsel zusätzlich.

## 18. B009 CompletionPulse

OR aus AutoCompletion und ManualCompletion.

Dieser Puls ist der zentrale Trigger für Snapshot, VE-Zähler und Wechsler.

## 19. B010…B013 Snapshot

Vor dem Reset stabil speichern:
- LastCompletedVECycles,
- LastCompletionReason,
- LastCompletedCavities,
- LastCompletedVENumber.

## 20. B014 Ventil

Wischrelais/Pulsglied mit Parameter aus VW12.

```text
50 ms <= Q1 <= 5000 ms
```

## 21. B015/B016 Resetfolge

Nach Snapshot und gestarteter Ventilphase kurzer Delay → ein Resetpuls für B003.

## 22. B017…B021 VE-Zähler/Sequenz

CompletionPulse erhöht CompletedVEs. Daraus werden CurrentVENumber und CompletionSequence bereitgestellt. Die Freigabegrenze 32767 VEs pro Auftrag muss eingehalten werden.

---

# TEIL D – PROTOCOL V3 COMPLETION HOLD

## 23. Warum HoldAfterVeNumber

Die LOGO! muss wissen, **bevor** die kritische VE fertig wird, wann sie lokal stoppen soll.

Partcounter plant deshalb einen Grenzpunkt auf Basis:
- Restmenge,
- Standard-VE,
- Kavitäten,
- effektiver zyklusbedingter Überfüllung.

Nicht die theoretische Verpackungszahl, sondern die tatsächlich nach vollständigen Zyklen produzierte Menge entscheidet über die nächste Grenze.

## 24. NAI15 / B043 HoldConfigured

```text
NAI15 = VW24
B043: HoldAfterVE > 0
```

## 25. B044 HoldDueOrPassed

Inputs:

```text
Ax = CompletedVEs (VW82)
Ay = HoldAfterVE (VW24)
```

Analogkomparator:

```text
Gain = 1
Offset = 0
On = -1
Off = -1
```

Für ganzzahlige Werte gilt dadurch:

```text
Q = 1 wenn Ax - Ay > -1
=> CompletedVEs >= HoldAfterVE
```

## 26. B045 CompletionHoldActive

```text
HoldConfigured AND HoldDueOrPassed
```

Ausgänge:
- invertiert in B002 CountGate,
- Statusbit V41.7.

## 27. B046 CompletionHoldArmed

```text
HoldAfterVE - CompletedVEs > 0
```

Ausgabe V41.6.

## 28. Zentraler Race-Test

Bei M01 muss bewiesen werden:

```text
VE N erreicht Ziel
CompletionPulse
CompletedVEs = N
HoldActive = 1
sofort danach I1 LOW→HIGH
B002 bleibt 0
```

Kein einziger Zyklus darf in die nächste VE leaken.

---

# TEIL E – JOBID / RECOVERY

## 29. Technische JobId

Die sichtbare Auftragsnummer ist keine ausreichende Restart-Identität. R001.25 erzeugt für jede reale Auftragsaktivierung eine neue zufällige 32-Bit-JobId.

Beide 16-Bit-Wörter bleiben im positiven LOGO-Analogbereich:

```text
JobIdHigh 0…32767
JobIdLow  1…32767
```

Die JobId wird **vor** dem ersten LOGO-Schreiben persistiert.

## 30. Echos

```text
VW74 Hold echo
VW76 JobId high echo
VW78 JobId low echo
```

Diese Echos müssen den lokal übernommenen Zustand repräsentieren.

## 31. PendingActivation

Reihenfolge eines neuen Echtauftrags:

```text
JobId erzeugen
↓
PendingActivation in SQLite persistieren
↓
erst jetzt LOGO Start schreiben
↓
Ack + Echos prüfen
↓
Checkpoint Active setzen
```

Bricht die Verbindung nach Write ab, darf kein zweiter neuer Auftrag über den unklaren Zustand geschrieben werden.

## 32. Restart Recovery

Nach Partcounter-Neustart:

```text
Checkpoint laden
↓
lokal PAUSIERT
↓
Echtbetrieb bewusst aktivieren
↓
LOGO Snapshot lesen
↓
JobId/Cavities prüfen
↓
bekannte LOGO Instanz pausieren
↓
Snapshot erneut lesen
↓
Zähler/VE/Hold rekonstruieren
↓
lokal weiter PAUSIERT
↓
Bediener entscheidet Resume
```

Bei JobId-Mismatch keine automatische Übernahme.

### PendingActivation verwerfen

Nur erlaubt, wenn die LOGO! nachweislich keinen aktiven Produktionszustand besitzt. Andernfalls Maschine gesperrt lassen und technisch klären.

---

# TEIL F – HEARTBEAT / DIAGNOSE

## 33. LOGO Heartbeat

B032/B033/B034/B035 erzeugen zyklisch 1…32767 und Wrap auf 1. Ausgabe HR34/VW66.

## 34. PC Heartbeat

HR12/VW22 wird gegen VW88 verglichen. Jede Änderung setzt das Alive-Fenster zurück. Ausbleibende Änderung setzt V41.5.

Heartbeat stale ist Diagnose, keine Produktionssperre vor der geplanten Hold-Grenze.

---

# TEIL G – CONFIGVALID

## 35. Mindestbedingungen

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

Ungültige Konfiguration:
- Ready=0,
- CountGate=0,
- keine neue VE-Completion aus ungültiger Parametrierung.

Der PC validiert zusätzlich Teileziel und Auftragskapazität.

---

# TEIL H – FEHLERCODES

## 36. Freigegebene Codes

| Code | Bedeutung |
|---:|---|
| 0 | kein Fehler |
| 1 | Protocol falsch |
| 2 | Kavitäten ungültig |
| 3 | Teileziel ungültig / reservierte Validierung |
| 4 | Zielzyklen ungültig |
| 5 | Ventilimpuls ungültig |
| 10 | optionale Endlage Timeout |
| 30 | interner Ablaufzustand ungültig |

Neue Codes nur synchron in PC-Code, LOGO-FBD und Testmatrix ergänzen.

---

# TEIL I – M01 INBETRIEBNAHME

## 37. Phase 1 – ohne Ventilmechanik

1. 24-V-Versorgung prüfen.
2. IP/Ping prüfen.
3. TCP 502/Modbus prüfen.
4. HR20 muss 3 sein.
5. High-/Low-Word-Testmuster prüfen.
6. AckSequence testen.
7. JobId/Hold-Echo testen.
8. I1 positive Flanke prüfen.
9. Pause/Resume prüfen.
10. automatische VE prüfen.
11. manuellen VE-Wechsel prüfen.
12. Q1 nur elektrisch messen.

## 38. Phase 2 – Boundary/Recovery

1. HoldArmed prüfen.
2. HoldActive exakt an Grenze prüfen.
3. unmittelbar nach Grenze schnelle I1-Flanken einspeisen: 0 Leakage.
4. Ziel am Hold neu schreiben und Ack/Echos prüfen.
5. erst danach Resume.
6. WLAN vor Hold trennen: Voll-VE dürfen bis Hold weiterlaufen.
7. WLAN am Hold wiederherstellen: kein spontanes Resume.
8. Partcounter während Auftrag neu starten: Recovery endet PAUSIERT.
9. JobId-Mismatch kontrolliert prüfen.
10. PendingActivation-Fälle prüfen.

## 39. Phase 3 – Q1 / Mechanik

Erst nach Phase 1/2:
- Koppelrelais anschließen,
- 50/250/750/2500/5000-ms-Impulse messen,
- danach Ventil anschließen,
- Bewegungsrichtung/Grundstellung prüfen,
- optional I2 validieren.

## 40. Phase 4 – Ende-zu-Ende

- realer Auftrag,
- mehrere Voll-VE,
- Teil-VE,
- Auftragsende,
- Originaletikett,
- Reprint,
- PC-/WLAN-Ausfall,
- Partcounter-Neustart,
- LOGO-Power-Cycle in kontrollierter Situation,
- reale ALS-/proALPHA-Schnittstelle sofern verfügbar.

Die detaillierte Sollmatrix ist `LOGO_V001_TEST_CASES_R001_25.csv`.

---

# TEIL J – ROLLOUT M02…M30

## 41. Referenzprinzip

Erst wenn M01 vollständig abgenommen ist, wird das Programm als Referenz dupliziert.

Pro Station ändern:
- IP-Adresse,
- Maschinenname/-nummer,
- gegebenenfalls Hardwarebesonderheiten.

Nicht individuell ändern:
- Protocol-Version,
- VM-Map,
- Command/Ack-Regeln,
- Hold-Netzwerke,
- JobId-Echos,
- FBD-Grundstruktur.

Jede kopierte Station erhält mindestens einen verkürzten Stations-Abnahmetest; kritische elektrische Unterschiede erfordern vollständige Abnahme.

---

## 42. Änderungsdisziplin

Ab Production Release Candidate gilt:

Eine Änderung an einer der folgenden Komponenten ist eine Protokoll-/Engineeringänderung:
- `ModbusRegisterMap.cs`,
- HR-/VM-Adressen,
- Status-/Command-Bits,
- Wortreihenfolge,
- Hold-Logik,
- JobId-Echo,
- Completion-/Ack-Sequenzen.

Solche Änderungen dürfen nur mit neuer Revision, aktualisierten CSVs/Guide/Testfällen und erneutem PC-CI + M01-Regressionsnachweis freigegeben werden.

---

## 43. Native LOGO!-Projektdatei

Die hier enthaltenen Unterlagen sind ein exakter, reproduzierbarer FBD-Bauplan. Eine native Siemens-LOGO!-Soft-Comfort-Projektdatei (`.lsc/.lscx` bzw. formatabhängig) soll nur aus LOGO! Soft Comfort selbst erzeugt und danach unter Versionskontrolle archiviert werden. Eine proprietäre Projektdatei wird nicht künstlich vorgetäuscht.

Nach dem ersten erfolgreichen Aufbau von M01 sollte die tatsächlich in Soft Comfort gespeicherte und auf die Hardware übertragene Projektdatei als **M01 Golden Master** archiviert und mit Software-/Dokumentrevision R001.25 verknüpft werden.

---

## 44. Freigabestatus

R001.25 kann nach vollständig grünem Software-Industrial-Gate als **Production Release Candidate** gelten.

Erst nach erfolgreicher realer M01-Abnahme darf der kombinierte PC-/LOGO-/Hardwarestand als **Production Baseline** bezeichnet und auf die weiteren Maschinen ausgerollt werden.
