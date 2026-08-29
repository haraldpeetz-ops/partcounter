# Partcounter R001.24 - Siemens LOGO! V001 Complete Engineering Guide

**Dokumentrevision:** R001.24  
**LOGO!-Programm:** `Partcounter_LOGO_V001`  
**Partcounter-Protokoll:** Modbus TCP Protocol V2  
**Referenzhardware:** Siemens LOGO! 12/24RCEo, 24 V DC, Ethernet  
**Kompatible Zielgeneration:** LOGO! 8 mit Modbus-Unterstützung (FS:04) sowie LOGO! 8.3/8.4  
**Referenz-Software:** LOGO! Soft Comfort V8.3/V8.4

---

## 1. Zweck und wichtigste Grundentscheidung

Dieses Dokument beschreibt einen reproduzierbaren LOGO!-Funktionsplan, der exakt zur aktuellen Partcounter-PC-Software passt. Die LOGO! ist **Modbus-TCP-Server**, der Partcounter-PC ist **Modbus-TCP-Client/Master**.

Die LOGO! übernimmt lokal und unabhängig vom PC:

- Erfassung einer positiven Maschinenzyklusflanke an I1,
- Zählen der Werkzeugzyklen,
- Erreichen der vorgegebenen Zyklen pro Verpackungseinheit,
- Auslösen des pneumatischen VE-Wechslers über Q1,
- Rücksetzen des aktuellen VE-Zählers nach sauberem Snapshot,
- Zählen der abgeschlossenen VEs,
- Bereitstellung von Status-/Diagnosedaten für Partcounter.

Der PC übermittelt lediglich Auftrag/Parameter/Befehle. Ein WLAN-/PC-Ausfall soll einen bereits laufenden lokalen Zählprozess **nicht künstlich stoppen**. Der PC-Heartbeat ist deshalb Diagnose, keine Produktionsfreigabe.

> **Safety-Grenze:** Partcounter und LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztür, Maschinenfreigaben, sichere Stillsetzung und andere sicherheitsgerichtete Funktionen bleiben vollständig in den dafür vorgesehenen sicheren Maschinenkreisen.

---

## 2. Siemens-Herstellergrundlage, die für diesen Plan relevant ist

Siemens dokumentiert für LOGO! 8.FS4 und spätere Geräte Modbus über Ethernet. LOGO! kann dabei Modbus-Server oder -Client sein. Für einen LOGO!-Modbus-Server liegt der zulässige Server-Portbereich bei 502 bis 510. Partcounter verwendet standardmäßig **TCP Port 502**.

Siemens dokumentiert außerdem:

- lokale LOGO!-Variable-Memory-Wörter `VW` können über Modbus auf Holding Register `HR` abgebildet werden,
- `VW` ist 16 Bit, `VD` ist 32 Bit,
- VM-Adressen sind byteorientiert: `VW0` benutzt VB0/VB1, `VW2` VB2/VB3 usw.,
- Netzwerk-Eingänge können lokale VM-Bits lesen,
- Netzwerk-Analog-Eingänge können lokale `VW` lesen,
- Netzwerk-Ausgänge/Netzwerk-Analog-Ausgänge können Werte in lokale VM-Adressen schreiben,
- Blockparameter wie der aktuelle Wert und der Schwellwert eines Up/Down-Counters können per Parameter-VM-Mapping als `VD` abgebildet werden.

Damit lässt sich der hier verwendete Partcounter-Speicherplan ohne zusätzliche SPS oder Gateway direkt in LOGO! abbilden.

### 2.1 Wichtige Versionsprüfung

Vor Beginn in LOGO! Soft Comfort prüfen:

1. tatsächlicher LOGO!-Typ,
2. Firmware-/FS-Stand,
3. Modbus-Zugriff im Ethernet-Dialog vorhanden,
4. LOGO! Soft Comfort mindestens passend zur Hardware.

Für die Partcounter-Referenzstation werden ausschließlich klassische VM-Adressen bis etwa `VW88` verwendet. Damit ist **kein LOGO!-8.4-VX-Erweiterungsspeicher** erforderlich; der Plan bleibt damit auch für LOGO! 8.3 geeignet.

---

## 3. Hardware der Referenzstation

| Funktion | Festlegung |
|---|---|
| Versorgung | 24 V DC |
| Maschinenzyklus | I1, 24 V DC |
| Zyklusbewertung | ausschließlich positive Flanke |
| optionaler Endlagensensor | I2, bei Station 01 zunächst deaktiviert |
| optional lokale Quittierung | I3 |
| VE-Wechsler | Q1 |
| Q1-Standard | Q1 -> 24-V-Koppelrelais -> Ventil |
| Ventil | 24 V DC |
| Standard-Ventilimpuls | 750 ms |
| zulässiger Ventilimpuls | 50...5000 ms, Raster 10 ms |

### 3.1 I1 anschließen

```text
Spritzgussmaschine Zyklussignal 24 V DC
                 |
                 +----> I1 LOGO!

0 V Maschine ----+----> M / 0 V LOGO!
```

Nur bei sicher gemeinsamer 0-V-Referenz direkt verbinden. Bei galvanisch getrennten, fremdgespeisten oder unklaren Maschinensignalen ein geeignetes Koppelrelais/Optokoppler verwenden.

I1 muss pro Werkzeugzyklus genau eine 0->1-Flanke liefern. Ein dauerhaftes HIGH zählt nicht mehrfach.

### 3.2 Q1 / Ventil anschließen

Bevorzugte industrielle Ausführung:

```text
+24 V DC
  |
  +-- Sicherung Steuerkreis
  |
  +-- LOGO! Q1 Relaiskontakt
  |
  +-- Spule Koppel-/Interface-Relais 24 V DC
  |        |
  |        +-- geeignete DC-Entstörung
  |
  +---------------- 0 V

Potentialfreier Arbeitskontakt Koppelrelais
  |
  +-- abgesicherte 24 V DC
  +-- Festo-Magnetventil 24 V DC
  +-- 0 V
```

Die Ventilspule ist bei der Inbetriebnahme anhand Typenschild/Datenblatt auf Strom, Leistung und vorhandene Schutzbeschaltung zu prüfen.

---

# TEIL A - LOGO! NETZWERK UND MODBUS EINRICHTEN

## 4. IP-Konzept

Für 30 Stationen empfiehlt sich ein statisches, dokumentiertes Produktionsnetz. Beispiel:

```text
Partcounter-PC:  192.168.50.10
Subnetz:         255.255.255.0
Gateway:         nur wenn benötigt

M01 LOGO!:       192.168.50.101
M02 LOGO!:       192.168.50.102
...
M30 LOGO!:       192.168.50.130

Modbus TCP:      Port 502
Partcounter Unit ID: 1
```

Jede LOGO! darf Port 502 verwenden, weil jede Station eine eigene IP-Adresse besitzt.

## 5. LOGO! Soft Comfort - IP-Adresse einstellen

Je nach V8.3/V8.4-Oberfläche sind die Bezeichnungen geringfügig unterschiedlich. Das Prinzip ist identisch.

1. LOGO!-Projekt öffnen.
2. **Datei / Einstellungen / Allgemein** beziehungsweise **Settings -> Offline settings -> General -> IP settings** öffnen.
3. Statische IP-Adresse der Station eintragen.
4. Subnetzmaske eintragen.
5. Gateway nur eintragen, wenn Routing tatsächlich benötigt wird.
6. Änderungen übernehmen.
7. PC und LOGO! müssen sich gegenseitig per IP erreichen können.

Für die erste Station z. B.:

```text
LOGO M01 IP: 192.168.50.101
Mask:        255.255.255.0
PC IP:       192.168.50.10
```

## 6. Modbus-Zugriff aktivieren

In LOGO! Soft Comfort unter Ethernet-/Online-/Offline-Einstellungen:

- **Allow Modbus access / Modbus-Zugriff erlauben** aktivieren.
- Wenn ein separater Modbus-Verbindungseintrag verlangt wird: unter **Extras -> Ethernet-Verbindungen** eine Modbus-Verbindung anlegen und LOGO! als **Server** konfigurieren.
- Server-Port auf **502** setzen.

### 6.1 Client-Zugriff beschränken

Für die Inbetriebnahme kann vorübergehend "Alle Verbindungsanforderungen akzeptieren" genutzt werden. Für den Regelbetrieb ist besser:

```text
Nur diese Verbindung / zugelassener Client:
192.168.50.10   (Partcounter-PC)
```

Das reduziert ungewollte Zugriffe im Produktionsnetz.

## 7. Modbus-Datenbereiche anlegen

Partcounter arbeitet ausschließlich mit Holding Registern. Die VM-Zuordnung ist:

### 7.1 PC -> LOGO! Konfiguration/Befehle

| HR | NModbus Offset | LOGO VM | Datentyp | Inhalt |
|---:|---:|---|---|---|
| HR1 | 0 | VW0 | UINT16 | ProtocolVersion = 2 |
| HR2 | 1 | VW2 | UINT16 | CommandSequence |
| HR3 | 2 | VW4 | UINT16 | CommandWord |
| HR4 | 3 | VW6 | UINT16 | ActiveCavities |
| HR5/6 | 4/5 | VD8 | UINT32 | TargetPartsPerVE |
| HR7 | 6 | VW12 | UINT16 | ValvePulse10Ms |
| HR8/9 | 7/8 | VD14 | UINT32 | JobId |
| HR10/11 | 9/10 | VD18 | UINT32 | TargetCyclesPerVE |
| HR12 | 11 | VW22 | UINT16 | PC Heartbeat |

### 7.2 LOGO! -> PC Status

| HR | NModbus Read Offset | LOGO VM | Datentyp | Inhalt |
|---:|---:|---|---|---|
| HR20 | 19 | VW38 | UINT16 | ProtocolVersion = 2 |
| HR21 | 20 | VW40 | UINT16 | StatusWord |
| HR22/23 | 21/22 | VD42 | UINT32 | CurrentVECycles |
| HR24/25 | 23/24 | VD46 | UINT32 | TotalCycles |
| HR26 | 25 | VW50 | UINT16 | CurrentVENumber |
| HR27 | 26 | VW52 | UINT16 | CompletedVEs |
| HR28 | 27 | VW54 | UINT16 | LastCompletedVECycles High = 0 |
| HR29 | 28 | VW56 | UINT16 | LastCompletedVECycles Low |
| HR30 | 29 | VW58 | UINT16 | AckSequence |
| HR31 | 30 | VW60 | UINT16 | ActiveCavitiesEcho |
| HR32 | 31 | VW62 | UINT16 | LastCompletedVENumber |
| HR33 | 32 | VW64 | UINT16 | CompletionSequence |
| HR34 | 33 | VW66 | UINT16 | LOGO Heartbeat |
| HR35 | 34 | VW68 | UINT16 | ErrorCode |
| HR36 | 35 | VW70 | UINT16 | LastCompletionReason |
| HR37 | 36 | VW72 | UINT16 | LastCompletedCavities |

### 7.3 Empfohlene Datenübertragungszeilen im Modbus-Server

Wenn LOGO! Soft Comfort die Zuordnung als lokale/remote Datenbereiche abfragt, zwei zusammenhängende Holding-Registerbereiche konfigurieren:

```text
Bereich A:
Remote Modbus HR1 ... HR12
<->
Local LOGO VW0 ... VW22
12 Words

Bereich B:
Remote Modbus HR20 ... HR37
<->
Local LOGO VW38 ... VW72
18 Words
```

Für Partcounter muss Bereich A schreibbar und Bereich B lesbar sein. LOGO! unterstützt bei `VW` Holding-Register-Zugriffe für Read/Write; Partcounter selbst schreibt nur in den Konfigurationsbereich und liest den Statusbereich.

## 8. 16-/32-Bit-Reihenfolge

LOGO!-VM ist byteorientiert. Für ein DWord gilt:

```text
VD8 = VB8 VB9 VB10 VB11
       MSB          LSB

HR5 = oberes 16-Bit-Wort
HR6 = unteres 16-Bit-Wort
```

Partcounter schreibt die 32-Bit-Werte genau in dieser Reihenfolge. Die HR-Zuordnung darf daher nicht vertauscht werden.

---

# TEIL B - COMMANDWORD UND STATUSWORD

## 9. CommandWord HR3 / VW4

Der 16-Bit-Wert liegt in `VW4`. Die verwendeten Bits liegen im **Low Byte VB5**:

| CommandWord Bit | LOGO VM Bit | Funktion | Art |
|---:|---|---|---|
| 0 | V5.0 | AutomaticEnabled | Pegel |
| 1 | V5.1 | ResetJob / neuer Auftrag | One-shot über neue CommandSequence |
| 2 | V5.2 | ManualVEChange | One-shot über neue CommandSequence |
| 3 | V5.3 | AcknowledgeAlarm | One-shot über neue CommandSequence |
| 4 | V5.4 | PauseCounting | Pegel |

**Wichtig:** V4.x ist das obere Byte von VW4. Die niedrigen CommandWord-Bits liegen wegen der Word-/Byte-Anordnung in **V5.x**.

## 10. StatusWord HR21 / VW40

Die unteren Statusbits werden in **VB41** geschrieben:

| Status Bit | LOGO VM Bit | Bedeutung |
|---:|---|---|
| 0 | V41.0 | Ready |
| 1 | V41.1 | Automatic active |
| 2 | V41.2 | VE change active |
| 3 | V41.3 | Alarm |
| 4 | V41.4 | Cycle input active |
| 5 | V41.5 | PC heartbeat stale |

VB40 bleibt für diese Version 0.

---

# TEIL C - LOKALE VM-EIN-/AUSGÄNGE IM FUNKTIONSPLAN

## 11. Netzwerk-Eingänge aus lokalem VM

In Soft Comfort die **Netzwerk-Eingang**-Bausteine so konfigurieren, dass sie **Local variable memory (VM)** lesen.

### Digitale NI-Bausteine

| Connector | Local VM | Name |
|---|---|---|
| NI1 | V5.0 | AutomaticEnabled |
| NI2 | V5.1 | ResetJobBit |
| NI3 | V5.2 | ManualVEChangeBit |
| NI4 | V5.3 | AcknowledgeAlarmBit |
| NI5 | V5.4 | PauseCountingBit |

### Analoge NAI-Bausteine

| Connector | Local VM | Name |
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

Die Scratch-Adressen `VD80`/`VD84`/`VW88` liegen außerhalb des Partcounter-Protokollbereichs und werden nur intern im LOGO!-Programm verwendet.

## 12. Netzwerk-Ausgänge in lokales VM

### Digitale NQ-Bausteine

| Connector | Local VM | Quelle |
|---|---|---|
| NQ1 | V41.0 | Ready |
| NQ2 | V41.1 | AutomaticEnabled |
| NQ3 | V41.2 | ValvePulseActive |
| NQ4 | V41.3 | AlarmLatched |
| NQ5 | V41.4 | I1 Rohstatus |
| NQ6 | V41.5 | PcHeartbeatStale |

### Analoge NAQ-Bausteine

| Connector | Local VM | Wert |
|---|---|---|
| NAQ1 | VW38 | ProtocolVersionOut = 2 |
| NAQ2 | VW50 | CurrentVENumber |
| NAQ3 | VW52 | CompletedVEs |
| NAQ4 | VW54 | Konstante 0 |
| NAQ5 | VW60 | ActiveCavitiesEcho |
| NAQ6 | VW62 | LastCompletedVENumber |
| NAQ7 | VW64 | CompletionSequence |
| NAQ8 | VW66 | LogoHeartbeat |
| NAQ9 | VW68 | ErrorCode |

---

# TEIL D - PARAMETER-VM-MAPPING

## 13. Tools -> Parameter VM Mapping

Folgende Blockparameter müssen direkt in VM synchronisiert werden:

| Block | Parameter | VM | Zugriff | Zweck |
|---|---|---|---|---|
| B003 CurrentVECycles | Counter | VD42 | R | HR22/23 Istzyklen aktuelle VE |
| B003 CurrentVECycles | On Threshold | VD18 | R/W | PC-Vorgabe TargetCyclesPerVE |
| B004 TotalCycles | Counter | VD46 | R | HR24/25 Gesamtzyklen |
| B010 LastCompletedCycles | Aen | VW56 | R | Snapshot letzter VE-Zyklen |
| B017 CompletedVEs | Counter | VD80 | intern | Scratch, Low Word = VW82 |
| B027 AckSequenceStore | Aen | VW58 | R | letzte bearbeitete CommandSequence |
| B012 LastCompletionReason | Aen | VW70 | R | 1 auto / 2 manuell |
| B013 LastCompletedCavities | Aen | VW72 | R | Kavitäten zum Abschlusszeitpunkt |
| B038 PcHeartbeatStore | Aen | VW88 | intern | letzter beobachteter PC-Heartbeat |
| B033 LogoHeartbeatCounter | Counter | VD84 | intern | Scratch, Low Word = VW86 |

Hinweis: Siemens unterstützt beim Up/Down-Counter den Counterwert und On-/Off-Schwellwerte als `VD`-Parameter im VM-Mapping. Beim Analog-Watchdog ist `Aen` als lesbares `VW`-Mapping verfügbar.

---

# TEIL E - REALER FBD-PROGRAMMPLAN

## 14. Netzwerk 1 - Zyklusflanke erfassen

### B001 - AND with edge evaluation / AND mit Flankenauswertung

```text
I1 ----------------------> B001
                            AND + positive edge
B001.Q = CycleEdge
```

Zweck: Auch wenn I1 mehrere hundert Millisekunden HIGH bleibt, entsteht genau ein Zyklusimpuls.

## 15. Netzwerk 2 - Zählfreigabe

### B002 - AND CountGate

Eingänge:

```text
B001.Q CycleEdge ---------> B002 IN1
NI1 AutomaticEnabled -----> B002 IN2
NI5 PauseCounting --------> B002 IN3 invertiert
B003.Q TargetReached -----> B002 IN4 invertiert
B014.Q ValveActive -------> B002 IN5 invertiert
ConfigValid --------------> B002 IN6

B002.Q -------------------> B003.Cnt
B002.Q -------------------> B004.Cnt
```

**Warum B003.Q zusätzlich sperrt:** Sobald das Ziel erreicht wurde, darf selbst während der kurzen Snapshot-Verzögerung kein weiterer Zyklus gezählt werden.

## 16. Netzwerk 3 - aktuelle VE zählen

### B003 - Up/Down Counter CurrentVECycles

```text
Cnt   <- B002.Q
Dir   <- 0 / Up
R     <- ResetCurrentVEPulse OR ResetJobPulse
On    <- VM-Mapping VD18
Off   <- 0
Retentivity <- ON empfohlen
Counter -> VM-Mapping VD42
```

Der PC berechnet `TargetCyclesPerVE = ceil(TargetParts / ActiveCavities)` und schreibt diesen Wert nach VD18 / HR10-HR11.

## 17. Netzwerk 4 - Gesamtzyklen zählen

### B004 - Up/Down Counter TotalCycles

```text
Cnt <- B002.Q
Dir <- 0 / Up
R   <- ResetJobPulse
Retentivity <- ON empfohlen
Counter -> VD46
```

B004 wird bei jedem neuen Auftrag zurückgesetzt, nicht bei jedem VE-Wechsel.

## 18. Netzwerk 5 - automatisches Ziel sicher erkennen

Um sicherzustellen, dass der gemappte aktuelle Counterwert vor dem Snapshot stabil in `VD42` steht:

### B005 - On-delay AutoReachedStable

```text
Trg <- B003.Q
T   = 50 ms
```

### B006 - AND with edge evaluation AutoCompletionPulse

```text
B005.Q -> B006
B006.Q = AutoCompletionPulse
```

Da B002 durch `NOT B003.Q` sofort sperrt, können während der 50-ms-Stabilisierungszeit keine zusätzlichen Zyklen gezählt werden.

## 19. Netzwerk 6 - manuelle VE nur bei Inhalt abschließen

### B007 - Analog threshold trigger CurrentVeNonZero

```text
Ax <- NAI10 (VW44 / Low Word CurrentVECycles)
On = 1
Off = 0
```

### B008 - AND ManualCompletionPulse

```text
B026 NewCommand ----------> B008 IN1
NI3 ManualVEChangeBit ----> B008 IN2
B007.Q CurrentVeNonZero --> B008 IN3
ConfigValid --------------> B008 IN4
```

Leere VE wird durch manuellen Wechsel nicht als fertige VE verbucht.

## 20. Netzwerk 7 - gemeinsamer VE-Abschluss

### B009 - OR CompletionPulse

```text
B006.Q AutoCompletionPulse ----> B009
B008.Q ManualCompletionPulse --> B009

B009.Q = CompletionPulse
```

## 21. Netzwerk 8 - Snapshot der letzten VE

### B010 - Analog watchdog LastCompletedCycles

```text
En <- B009.Q
Ax <- NAI10 (CurrentVECycles Low)
Gain 1.00
Offset 0
Aen -> VM-Mapping VW56
```

Durch die 50-ms-Stabilisierung im Automatikpfad ist bei automatischem Abschluss der aktuelle Zählerwert bereits in VW44 verfügbar. Beim manuellen Abschluss ist der Zähler ohnehin stabil.

`VW54` wird über NAQ4 konstant auf 0 gehalten, weil eine VE in V2 maximal 32767 Zyklen hat.

### B011 - Analog multiplexer CompletionReasonCandidate

Konfiguration:

```text
S1 <- B008.Q ManualCompletionPulse
S2 <- 0
V1 = 1   (automatisch)
V2 = 2   (manuell)
V3 = 1
V4 = 2
```

### B012 - Analog watchdog LastCompletionReason

```text
En <- B009.Q
Ax <- B011.AQ
Aen -> VW70
```

### B013 - Analog watchdog LastCompletedCavities

```text
En <- B009.Q
Ax <- NAI3 ActiveCavities
Aen -> VW72
```

## 22. Netzwerk 9 - Ventilimpuls Q1

### B014A - Analog amplifier ValveTimeReference

```text
Ax <- NAI6 ValvePulse10Ms
Gain = 1.00
Offset = 0
```

### B014 - Wiping relay ValvePulse

```text
Trg <- B009.Q
T   <- tatsächlicher Wert von B014A
Time base = 10 ms
Q   -> Q1
Q   -> NQ3 / V41.2
```

Beispiele:

```text
VW12=5   -> 50 ms
VW12=75  -> 750 ms
VW12=500 -> 5000 ms
```

## 23. Netzwerk 10 - aktuellen VE-Zähler erst nach Snapshot zurücksetzen

### B015 - On-delay ResetAfterSnapshot

```text
Trg <- B014.Q ValvePulseActive
T = 20 ms
```

### B016 - AND with edge evaluation ResetCurrentVEPulse

```text
B015.Q -> B016
B016.Q -> B003.R
```

Damit ist die Reihenfolge eindeutig:

```text
1. Completion erkannt
2. Zyklen/Kavitäten/Grund gesichert
3. Ventil läuft an
4. 20 ms später CurrentVECycles reset
```

## 24. Netzwerk 11 - abgeschlossene VE zählen

### B017 - Up/Down Counter CompletedVEs

```text
Cnt <- B009.Q
Dir <- Up
R   <- ResetJobPulse
Retentivity <- ON empfohlen
Counter -> Scratch VD80
```

### NAI12

```text
NAI12 liest VW82 = Low Word des Scratch-Counters VD80
```

### B018 - Analog amplifier CurrentVENumber

```text
Ax <- NAI12
Gain = 1
Offset = +1
AQ -> NAQ2 -> VW50
```

### B019 - Analog amplifier CompletedVEsOut

```text
Ax <- NAI12
Gain=1 Offset=0
AQ -> NAQ3 -> VW52
```

### B020 - Analog amplifier LastCompletedVENumber

```text
Ax <- NAI12
Gain=1 Offset=0
AQ -> NAQ6 -> VW62
```

### B021 - Analog amplifier CompletionSequence

```text
Ax <- NAI12
Gain=1 Offset=0
AQ -> NAQ7 -> VW64
```

Die CompletionSequence ändert sich damit genau einmal pro abgeschlossener VE.

## 25. Netzwerk 12 - CommandSequence/AckSequence

### B022 - Analog comparator CmdGreaterAck

```text
Ax <- NAI2 CommandSequence
Ay <- NAI11 AckSequence
Q = CommandSequence > AckSequence
```

### B023 - Analog comparator AckGreaterCmd

```text
Ax <- NAI11 AckSequence
Ay <- NAI2 CommandSequence
Q = AckSequence > CommandSequence
```

### B024 - OR NewCommand

```text
B022.Q OR B023.Q -> B024
```

Damit wird Ungleichheit ohne problematische Subtraktion erkannt, auch beim Sequenz-Wrap 32767 -> 1.

### B025 - AND ResetJobPulse

```text
B024.Q NewCommand AND NI2 ResetJobBit
```

B025.Q resettiert:

```text
B003 CurrentVECycles
B004 TotalCycles
B017 CompletedVEs
```

### B026 - Durchleitung NewCommand

B024.Q wird als benanntes Signal `NewCommand` weiterverwendet, unter anderem für B008 ManualCompletionPulse.

### B027 - Analog watchdog AckSequenceStore

```text
En <- B024.Q
Ax <- NAI2 CommandSequence
Aen -> VW58
```

Damit wird jeder neue Befehl einmal bearbeitet. Solange CommandSequence = AckSequence ist, werden Reset/Manual/Acknowledge nicht erneut ausgeführt.

## 26. Netzwerk 13 - Alarm quittieren

### B028 - AND AcknowledgeAlarmPulse

```text
B024.Q NewCommand AND NI4 AcknowledgeAlarmBit
```

B028 wird auf den Reset-Eingang des Alarm-Latches geführt, falls die optionale Fehler-/Endlagenlogik einen latched Alarm verwendet.

## 27. Netzwerk 14 - LOGO-Heartbeat

### B032 - Symmetrical pulse generator LogoHeartbeatClock

Empfehlung:

```text
High = 0.5 s
Low  = 0.5 s
```

Positive Flanke ca. einmal pro Sekunde zählt weiter.

### B033 - Up/Down Counter LogoHeartbeatCounter

```text
Cnt <- positive Flanke B032
On threshold = 32766
Counter -> scratch VD84
```

### B034 - AND with edge evaluation HeartbeatWrap

```text
B033.Q -> B034
B034.Q -> B033.R
```

### B035 - Analog amplifier LogoHeartbeatOut

```text
Ax <- NAI13 (VW86, Low Word scratch VD84)
Gain=1
Offset=1
AQ -> NAQ8 -> VW66
```

Dadurch liegt der sichtbare Heartbeat in der Regel im Bereich 1...32767 und ändert sich zyklisch.

## 28. Netzwerk 15 - PC-Heartbeat überwachen, Produktion aber nicht stoppen

### B036/B037 - Ungleichheitsvergleich PC heartbeat

```text
B036: NAI9 PcHeartbeat > NAI14 StoredPcHeartbeat
B037: NAI14 StoredPcHeartbeat > NAI9 PcHeartbeat
```

### B038 - OR PcHeartbeatChanged

```text
B036.Q OR B037.Q
```

### B039 - Analog watchdog PcHeartbeatStore

```text
En <- B038.Q
Ax <- NAI9 PcHeartbeat
Aen -> VW88
```

### B040 - Off-delay PcHeartbeatAlive

```text
Trg <- B038.Q
T = 5 s
```

### PcHeartbeatStale

```text
NOT B040.Q -> NQ6 -> V41.5
```

**Wichtig:** `PcHeartbeatStale` darf **nicht** in B002 CountGate eingehen. Ein PC-/WLAN-Ausfall wird gemeldet, aber ein bereits laufender lokaler Auftrag zählt weiter und kann weiterhin VE wechseln.

---

# TEIL F - STATUS UND VALIDIERUNG

## 29. ProtocolVersion ausgeben

### B041 - Analog multiplexer ProtocolVersionConst

Alle vier Werte auf 2 setzen, S1=S2=0.

```text
B041.AQ -> NAQ1 -> VW38 -> HR20
```

Partcounter verweigert Statusdaten, wenn HR20 nicht ProtocolVersion 2 enthält.

## 30. ActiveCavitiesEcho

### B042 - Analog amplifier CavitiesEcho

```text
Ax <- NAI3
Gain=1 Offset=0
AQ -> NAQ5 -> VW60
```

## 31. Statusbits

```text
NQ1 V41.0 Ready             <- ConfigValid AND NOT AlarmLatched
NQ2 V41.1 AutomaticActive   <- NI1 AutomaticEnabled
NQ3 V41.2 VEChangeActive    <- B014.Q
NQ4 V41.3 Alarm             <- AlarmLatched
NQ5 V41.4 CycleInputActive  <- I1
NQ6 V41.5 PcHeartbeatStale  <- NOT B040.Q
```

## 32. Empfohlene Parameterprüfung

Vor Zählfreigabe `ConfigValid` bilden. Mindestens prüfen:

```text
ProtocolVersion == 2
ActiveCavities >= 1
ActiveCavities <= 64
TargetCyclesHigh == 0
TargetCyclesLow >= 1
TargetCyclesLow <= 32767
ValvePulse10Ms >= 5
ValvePulse10Ms <= 500
TargetPartsPerVE != 0
```

Empfohlene ErrorCodes:

| Code | Bedeutung |
|---:|---|
| 0 | kein Fehler |
| 1 | ProtocolVersion != 2 |
| 2 | ActiveCavities außerhalb 1...64 |
| 3 | TargetPartsPerVE = 0 |
| 4 | TargetCyclesPerVE außerhalb 1...32767 |
| 5 | Ventilwert außerhalb 5...500 (= 50...5000 ms) |
| 10 | optionaler Endlagentimeout |
| 30 | interner ungültiger Ablaufzustand |

Die genaue Fehlerpriorisierung kann mit Analog-Multiplexer/Mathematikblöcken aufgebaut werden. Für den ersten I/O-/Modbus-Test darf `ErrorCode` zunächst fest 0 sein; **vor Produktionsfreigabe** sollte die Parameterdiagnose vollständig eingebaut sein.

---

# TEIL G - PARTCOUNTER-SEITIGE MASCHINENEINSTELLUNG

## 33. Maschinenparameter in Partcounter

Für M01 passend zum obigen Beispiel:

```text
Maschine: M01
IP:       192.168.50.101
Port:     502
Unit ID:  1
Enabled:  Ja
```

M02 entsprechend 192.168.50.102 usw.

Die PC-Software verwendet beim Schreiben/Lesen **zero-based NModbus offsets**, während die Bedien-/Dokumentationsbezeichnung HR1, HR2 usw. one-based ist. Beispiel:

```text
HR1  = NModbus address 0
HR20 = NModbus address 19
```

Das ist bereits in Partcounter korrekt implementiert und darf nicht zusätzlich um 1 verschoben werden.

---

# TEIL H - START-/BEFEHLSABLAUF

## 34. Neuer Auftrag

Partcounter schreibt HR1...HR12 zusammenhängend. Typisches Beispiel:

```text
ProtocolVersion       HR1  = 2
CommandSequence       HR2  = 101
CommandWord           HR3  = bit0 + bit1 = 0x0003
ActiveCavities        HR4  = 8
TargetPartsPerVE      HR5/6 = 8000
ValvePulse10Ms        HR7  = 75
JobId                 HR8/9 = Hash/Auftrags-ID
TargetCyclesPerVE     HR10/11 = 1000
PcHeartbeat           HR12 = laufender Wert
```

LOGO erkennt:

```text
CommandSequence 101 != AckSequence 100
=> NewCommand
=> ResetJobBit aktiv
=> Zähler B003/B004/B017 reset
=> AckSequenceStore übernimmt 101
=> danach NewCommand wieder FALSE
```

## 35. Pause

PC sendet neue Sequence, CommandWord:

```text
bit0 Automatic = 1
bit4 Pause = 1
```

B002 wird gesperrt. I1-Flanken werden nicht gezählt.

Wichtig: Weil B001 die Flanke **vor** B002 erzeugt, erzeugt das spätere Aufheben der Pause bei weiterhin HIGH liegendem I1 keinen künstlichen Zählerimpuls.

## 36. Fortsetzen

Neue Sequence, CommandWord nur bit0 = 1. Erst die nächste echte 0->1-Flanke an I1 wird gezählt.

## 37. Manueller VE-Wechsel

Neue Sequence mit:

```text
bit0 = 1
bit2 = 1
```

Nur wenn CurrentVECycles > 0:

```text
B008 -> B009 CompletionPulse
Snapshot -> Ventil -> ResetCurrentVE
```

Gesamtzähler B004 wird nicht zurückgesetzt.

---

# TEIL I - VERHALTEN BEI PC-/WLAN-AUSFALL

## 38. Gewollte Architektur

Wenn Partcounter oder WLAN während eines laufenden Auftrags ausfällt:

- LOGO behält die zuletzt gültigen Parameter,
- AutomaticEnabled bleibt auf dem zuletzt geschriebenen Pegel,
- CurrentVECycles zählt lokal weiter,
- VE-Abschluss funktioniert lokal,
- Q1 funktioniert lokal,
- PcHeartbeatStale wird gesetzt,
- keine künstliche Pause allein aufgrund fehlenden PC-Heartbeats.

Das ist absichtlich so gewählt, damit ein Netzwerkproblem nicht die Spritzgussmaschine oder den Verpackungswechsel unnötig blockiert.

**Einschränkung:** Partcounter kann bei fehlender Verbindung das Ziel für eine speziell verkleinerte letzte Auftrags-VE nicht nachführen. Bei einer langen PC-Störung läuft die LOGO daher mit dem zuletzt gültigen VE-Ziel weiter. Das ist betrieblich sicherer als ein unkontrollierter Produktionsstopp, muss aber in der Betriebsanweisung berücksichtigt werden.

---

# TEIL J - TESTEN OHNE MASCHINE

## 39. Test 1 - Modbus-Grundverbindung

1. LOGO-Programm laden.
2. LOGO RUN.
3. PC per Ethernet verbinden.
4. Ping LOGO-IP prüfen.
5. Partcounter -> Echtbetrieb nur in kontrollierter Testumgebung aktivieren.
6. Maschine M01 beobachten.

Erwartung:

```text
HR20 = 2
HR34 verändert sich
ConnectionState = Online
ErrorCode = 0
```

## 40. Test 2 - 4 Kavitäten / VE 20

```text
Cavities = 4
TargetParts = 20
TargetCycles = 5
Valve = 750 ms
```

I1 fünfmal 0->1 schalten.

Erwartung:

```text
VD42 erreicht 5
Q1 einmal ca. 750 ms
VW56 = 5
VW72 = 4
VW70 = 1
CompletedVEs +1
CompletionSequence +1
CurrentVECycles nach Snapshot = 0
PC berechnet 5 * 4 = 20 Teile
```

## 41. Test 3 - 64 Kavitäten / VE 1000

Partcounter berechnet:

```text
ceil(1000 / 64) = 16 Zyklen
16 * 64 = 1024 reale Teile
Mehrmenge = 24
```

Nach 16 I1-Flanken muss genau ein VE-Wechsel erfolgen.

## 42. Test 4 - Pause bei I1 HIGH

1. I1 auf HIGH halten.
2. Pause senden.
3. Pause aufheben, I1 weiterhin HIGH.
4. Zähler beobachten.

Erwartung: kein zusätzlicher Zyklus. Erst I1 LOW und danach neue steigende Flanke zählt.

## 43. Test 5 - manueller Wechsel

Nach drei Zyklen manuellen VE-Wechsel senden.

Erwartung:

```text
VW56 = 3
VW70 = 2
Q1 Impuls
CurrentVECycles -> 0
TotalCycles bleibt erhalten
```

## 44. Test 6 - PC-Verbindung abziehen

Während eines laufenden Testauftrags Ethernet trennen.

Erwartung:

- nach ca. 5 s PcHeartbeatStale = 1,
- lokales Zählen bleibt aktiv,
- VE-Wechsel bleibt aktiv,
- LOGO stürzt nicht ab und setzt Zähler nicht zurück.

Nach Wiederverbindung synchronisiert Partcounter zunächst die AckSequence mit HR30, bevor ein neuer Befehl gesendet wird.

---

# TEIL K - INBETRIEBNAHMEREIHENFOLGE AN DER REALEN MASCHINE

## 45. Verbindliche Reihenfolge

1. LOGO offline vollständig erstellen.
2. Parameter-VM-Mapping kontrollieren.
3. Modbus-Server/Port/IP kontrollieren.
4. Soft-Comfort-Simulation der Logik durchführen.
5. LOGO real versorgen, Q1 noch ohne Ventil.
6. I1 mit definiertem Testtaster/24-V-Impuls testen.
7. Q1 zunächst nur mit Messgerät/Koppelrelais testen.
8. 50/250/750/2500/5000-ms-Impulse messen.
9. Modbus-Verbindung vom PC testen.
10. HR1...HR12 online beobachten.
11. HR20...HR37 online beobachten.
12. Koppelrelais und Ventil anschließen.
13. Mechanischen Wechsel ohne laufende Maschine testen.
14. Maschinenzyklussignal I1 ankoppeln.
15. Langsamen Testauftrag durchführen.
16. Pause/Resume testen.
17. manuellen VE-Wechsel testen.
18. Ethernet/WLAN gezielt unterbrechen und Wiederverbindung testen.
19. mehrere VEs hintereinander produzieren.
20. erst danach Produktionsfreigabe dokumentieren.

---

# TEIL L - FEHLERSUCHE

## 46. Partcounter bleibt Offline

Prüfen:

```text
LOGO RUN?
richtige IP?
Subnetz korrekt?
Port 502?
Modbus-Zugriff erlaubt?
Serververbindung vorhanden?
PC-IP zugelassen?
Windows-Firewall/Netzsegment?
```

## 47. Verbindung da, aber ProtocolVersion-Fehler

Prüfen:

```text
HR20 wirklich auf VW38 gemappt?
NAQ1 liefert konstant 2?
kein HR/VW Offset um 1 verschoben?
```

## 48. Parameter kommen verschoben an

Typische Ursache: Word-/Byteadresse verwechselt.

Richtig:

```text
HR1 -> VW0
HR2 -> VW2
HR3 -> VW4
HR4 -> VW6
HR5/HR6 -> VD8
```

Nicht VW0, VW1, VW2 ... verwenden. Ein `VW` benötigt zwei Bytes.

## 49. Zähler zählt beim Resume einen falschen Zyklus

B001 Flankenauswertung wurde wahrscheinlich hinter die Freigabelogik gesetzt. Die richtige Reihenfolge ist:

```text
I1 -> positive Flanke -> CountGate
```

nicht:

```text
I1 -> CountGate -> positive Flanke
```

## 50. Letzte VE-Zyklen sind um 1 falsch

Prüfen:

- B002 sperrt bei B003.Q,
- B005 50-ms-Stabilisierungszeit vorhanden,
- B010 snapshot liest VW44,
- B016 reset erst nach Snapshot.

## 51. Ventilimpuls stimmt nicht

Prüfen:

```text
HR7 / VW12 = Millisekunden / 10
Timerbasis = 10 ms
750 ms -> Wert 75
```

---

# TEIL M - ABNAHMEKRITERIEN

## 52. LOGO!-Station gilt erst als freigegeben, wenn

- [ ] alle VM-/HR-Adressen stimmen,
- [ ] ProtocolVersion HR1/HR20 = 2,
- [ ] CommandSequence/AckSequence funktionieren,
- [ ] 1000 Testzyklen ohne Doppel-/Fehlzählung durchlaufen wurden,
- [ ] Pause/Resume keinen Phantomzyklus erzeugt,
- [ ] automatischer VE-Wechsel reproduzierbar ist,
- [ ] manueller VE-Wechsel korrekt protokolliert wird,
- [ ] Ventilimpuls gemessen und dokumentiert ist,
- [ ] PC-Ausfall den lokalen Zähler nicht stoppt,
- [ ] Wiederverbindung ohne unerwarteten Reset funktioniert,
- [ ] Koppelrelais/Ventil elektrisch korrekt abgesichert sind,
- [ ] Safety-Funktionen vollständig unabhängig bleiben.

---

## 53. Die drei wichtigsten Regeln

1. **LOGO zählt Werkzeugzyklen, nicht Teile.** Partcounter berechnet Teile = Zyklen x aktive Kavitäten.
2. **Flankenerkennung vor Freigabelogik.** Sonst kann Resume einen Phantomzyklus erzeugen.
3. **Snapshot vor Reset.** Sonst verliert Partcounter die Stückzahl der gerade abgeschlossenen VE.

Dieses Dokument supersediert für den Neuaufbau die vereinfachten R001.8-Aufbauhinweise; die Modbus-V2-Adressen selbst bleiben unverändert.
