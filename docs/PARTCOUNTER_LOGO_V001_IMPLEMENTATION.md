# Partcounter LOGO V001 – Implementierungsstandard

**Partcounter Revision:** R001.7  
**LOGO-Programm:** `Partcounter_LOGO_V001`  
**Zielplattform:** Siemens LOGO! 8.3 / 8.4  
**Kommunikation:** Modbus TCP, LOGO! als Server, PC als Client  
**Partcounter-Protokoll:** Version 2

## 1. Ziel

Dieses Dokument ist die verbindliche Engineering-Vorgabe für das erste reale LOGO!-Programm von Partcounter. Alle Maschinen verwenden dieselbe Grundlogik. Maschinenbezogen werden nur Netzwerkparameter, reale I/O-Verdrahtung und optional die Überwachung einer Endlage angepasst.

Zentrale Regel: Die LOGO! zählt die Maschinenzyklen und löst den VE-Wechsel lokal aus. Ein Ausfall von PC, LAN oder WLAN darf weder Zyklen verlieren noch einen fälligen Verpackungswechsel verhindern.

## 2. Hardwaregerechte Zählstrategie

LOGO!-Auf/Ab-Zähler können bis 999999 zählen. Die allgemeine LOGO!-Analogarithmetik arbeitet dagegen nur mit 16-Bit-Integerwerten. Deshalb berechnet die LOGO! **keine Teilezahl durch Multiplikation `Zyklen × Kavitäten`**.

Stattdessen gilt ab Protokoll V2:

- LOGO! zählt `CurrentVECycles` und `TotalCycles` nativ.
- Zählerstände werden per Parameter-VM-Mapping als DWORD in den VM-Bereich gespiegelt.
- Der PC liest die Zyklen und multipliziert sie mit der Kavitätenzahl.
- `LastCompletedVECycles` und `LastCompletedCavities` werden getrennt gespeichert, damit auch ein späterer Artikelwechsel die abgeschlossene VE nicht verfälscht.

Damit ist die LOGO!-Logik unabhängig von der 16-Bit-Grenze der Analogrechnung.

## 3. Standard-I/O der ersten Testmaschine

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

NModbus adressiert Holding Register nullbasiert. Fachlich entspricht HR1 der PC-Adresse 0. Auf LOGO!-Seite beginnt die VM-Zuordnung bei VW0.

### 4.1 PC → LOGO!

| HR | PC Offset | LOGO VM | Inhalt |
|---:|---:|---|---|
| HR1 | 0 | VW0 | ProtocolVersion = 2 |
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

VW24 bis VW36 bleiben für Protokollerweiterungen reserviert.

### 4.2 LOGO! → PC

| HR | LOGO VM | Inhalt |
|---:|---|---|
| HR20 | VW38 | ProtocolVersion = 2 |
| HR21 | VW40 | StatusWord |
| HR22 | VW42 | CurrentVECycles High |
| HR23 | VW44 | CurrentVECycles Low |
| HR24 | VW46 | TotalCycles High |
| HR25 | VW48 | TotalCycles Low |
| HR26 | VW50 | CurrentVENumber |
| HR27 | VW52 | CompletedVEs |
| HR28 | VW54 | LastCompletedVECycles High |
| HR29 | VW56 | LastCompletedVECycles Low |
| HR30 | VW58 | AckSequence |
| HR31 | VW60 | ActiveCavitiesEcho |
| HR32 | VW62 | LastCompletedVENumber |
| HR33 | VW64 | CompletionSequence |
| HR34 | VW66 | LOGO Heartbeat |
| HR35 | VW68 | ErrorCode |
| HR36 | VW70 | LastCompletionReason |
| HR37 | VW72 | LastCompletedCavities |

Wichtig: Die DWORD-Zähler werden in Partcounter als High Word vor Low Word übertragen.

## 5. Interne Merker

Die folgenden Merkerbezeichnungen sind der Standard für `Partcounter_LOGO_V001`. Die endgültigen LOGO!-Blocknummern dürfen beim ersten Aufbau abweichen; die Signalnamen und Funktionen bleiben verbindlich.

| Merker | Name | Funktion |
|---|---|---|
| M1 | `CyclePulse` | Ein-Zyklus-Impuls nach positiver I1-Flanke |
| M2 | `AutomaticLatched` | Automatik freigegeben |
| M3 | `PauseLatched` | Zählung pausiert |
| M4 | `VeChangeActive` | VE-Wechsel läuft |
| M5 | `AlarmLatched` | technische Störung aktiv |
| M6 | `PcHeartbeatStale` | PC-Heartbeat steht |
| M7 | `EndPositionOk` | optionale Wechsler-Endlage gültig |
| M8 | `ManualChangePulse` | manueller Wechselauftrag |
| M9 | `ResetJobPulse` | neuer Auftrag / Zählerreset |
| M10 | `ParameterUpdatePulse` | neue VE-Parameter ohne Auftragsreset |
| M11 | `ValvePulseActive` | Q1-Puls aktiv |
| M12 | `CompletionPulse` | genau ein Abschlussereignis |
| M13 | `NewCommand` | CommandSequence ungleich AckSequence |
| M14 | `ConfigValid` | empfangene Parameter gültig |
| M15 | `CommandAckPulse` | Befehlsbearbeitung abgeschlossen |
| M16 | `Reserved` | Reserve |

## 6. Funktionsblöcke / logische Gruppen

Die folgende Struktur ist bei der Umsetzung in LOGO! Soft Comfort beizubehalten. Blocknummern werden nach der ersten echten Implementierung im Projekt eingefroren und dann hier ergänzt.

### Gruppe A – Zykluseingang

- positive Flankenerkennung I1
- Entprellung nur falls das reale Maschinensignal sie benötigt
- Ergebnis `CyclePulse`
- Zählen nur bei `AutomaticLatched = 1`, `PauseLatched = 0` und `VeChangeActive = 0`

### Gruppe B – VE-Zähler

- nativer Auf/Ab-Zähler `CurrentVECycles`
- Rücksetzen bei neuem Auftrag und nach abgeschlossenem VE-Wechsel
- Einschaltgrenze = gelatchtes `TargetCyclesPerVE`
- zulässiger Bereich: 1 bis 999999 Zyklen
- Counter-Actual-Value über Parameter-VM-Mapping als DWORD auf HR22/HR23

### Gruppe C – Gesamtzykluszähler

- nativer Auf/Ab-Zähler `TotalCycles`
- Rücksetzen nur bei neuem Auftrag
- nicht bei normalem VE-Wechsel zurücksetzen
- Counter-Actual-Value als DWORD auf HR24/HR25

### Gruppe D – Befehlsdecoder

Ein Befehl ist neu, wenn:

```text
CommandSequence != AckSequence
```

Verarbeitung:

1. Protokoll und Parameter prüfen.
2. Parameter in interne Arbeitswerte übernehmen.
3. CommandWord dekodieren.
4. One-Shot-Funktionen genau einmal ausführen.
5. `AckSequence = CommandSequence` setzen.

### Gruppe E – VE-Abschluss

Abschlussbedingung automatisch:

```text
CurrentVECycles >= TargetCyclesPerVE
```

Abschlussbedingung manuell:

```text
ManualChangePulse AND CurrentVECycles > 0
```

Vor dem Zählerreset müssen gespeichert werden:

```text
LastCompletedVECycles  = CurrentVECycles
LastCompletedCavities  = ActiveCavitiesLatched
LastCompletedVENumber  = CurrentVENumber
LastCompletionReason   = 1 automatisch / 2 manuell
CompletedVEs           = CompletedVEs + 1
CompletionSequence     = CompletionSequence + 1
```

Erst danach darf `CurrentVECycles` zurückgesetzt werden.

### Gruppe F – Ventilimpuls

- Q1 wird monostabil eingeschaltet.
- Impulsdauer = `ValvePulseMs`.
- zulässiger Standardbereich: 50 bis 5000 ms.
- Q2 kann parallel als Wechselanzeige geschaltet werden.
- Q1 muss bei LOGO!-Start und bei ungültiger Konfiguration AUS sein.

### Gruppe G – Heartbeat

- PC erhöht HR12 zyklisch.
- LOGO! überwacht, ob sich HR12 innerhalb eines Diagnosefensters ändert.
- Kommunikationsstillstand setzt `PcHeartbeatStale`, stoppt aber **nicht** die lokale Produktion.
- LOGO! erhöht HR34 unabhängig zyklisch.

## 7. Zustandsautomat

| Zustand | Bedeutung | Q1 | Zählen |
|---|---|---:|---:|
| S0 STARTUP | Initialisierung | 0 | 0 |
| S1 READY | gültig, kein laufender Auftrag | 0 | 0 |
| S2 COUNTING | Automatik aktiv | 0 | 1 |
| S3 PAUSED | Auftrag pausiert | 0 | 0 |
| S4 VE_CHANGE | Ventilimpuls läuft | 1 | 0 |
| S5 POST_CHANGE | Abschlussdaten stabilisieren / Zählerreset | 0 | 0 |
| S6 FAULT | technische Störung | 0 | gemäß Fehlerart |

Ein reiner PC-/WLAN-Ausfall führt nicht nach S6, sondern bleibt ein Diagnosezustand innerhalb von S2/S3.

## 8. Parameterprüfung

Ein neuer Auftrag ist nur gültig, wenn:

- ProtocolVersion = 2
- ActiveCavities = 1 bis 64
- TargetPartsPerVE > 0
- TargetCyclesPerVE = 1 bis 999999
- ValvePulseMs = 50 bis 5000

Bei ungültigem Telegramm:

- Auftrag nicht übernehmen,
- `ErrorCode` setzen,
- `StatusWord.Alarm` setzen,
- `AckSequence` trotzdem auf die bearbeitete CommandSequence setzen, damit derselbe fehlerhafte Befehl nicht endlos erneut ausgeführt wird.

## 9. Fehlercodes

| Code | Bedeutung | Verhalten |
|---:|---|---|
| 0 | kein Fehler | normal |
| 1 | falsche Protokollversion | Auftrag ablehnen |
| 2 | Kavitätenzahl außerhalb 1–64 | Auftrag ablehnen |
| 3 | TargetPartsPerVE = 0 | Auftrag ablehnen |
| 4 | TargetCyclesPerVE außerhalb 1–999999 | Auftrag ablehnen |
| 5 | Ventilimpuls außerhalb 50–5000 ms | Auftrag ablehnen |
| 10 | optionale Wechsler-Endlage nicht rechtzeitig erreicht | Alarm, weitere automatische Wechsel sperren |
| 30 | interner ungültiger Ablaufzustand | Q1 aus, Alarm |

Der Stillstand des PC-Heartbeats ist Statusbit 5 in HR21 und kein Produktionsfehlercode.

## 10. StatusWord HR21

| Bit | Maske | Bedeutung |
|---:|---:|---|
| 0 | 0x0001 | LOGO bereit |
| 1 | 0x0002 | Automatik aktiv |
| 2 | 0x0004 | VE-Wechsel aktiv |
| 3 | 0x0008 | Alarm |
| 4 | 0x0010 | Zykluseingang aktiv |
| 5 | 0x0020 | PC-Heartbeat steht |

## 11. Neustartverhalten

### PC-Neustart

Die LOGO! läuft mit den zuletzt übernommenen Parametern weiter. Partcounter liest zuerst `AckSequence` und setzt seine nächste CommandSequence auf den nachfolgenden Wert. Dadurch wird kein Befehl durch eine nach dem PC-Neustart wiederverwendete Sequenznummer verloren.

### LOGO!-Neustart

- Q1 = AUS beim Start.
- Keine selbsttätige Bewegung aus einem alten Ausgangszustand.
- Retentive Zähler dürfen nur verwendet werden, wenn das Verhalten an der realen Maschine bewusst validiert wurde.
- Für die erste Testmaschine wird ein kontrollierter Neustarttest mit laufendem Auftrag durchgeführt.

## 12. Abnahmekriterien für die erste Testmaschine

1. Genau eine I1-Flanke ergibt genau einen `CurrentVECycles`-Schritt.
2. 1-, 2-, 4-, 8-, 16-, 32- und 64-fach-Werkzeuge liefern korrekte PC-Teilezahlen.
3. VE 1000 / 64 Kavitäten ergibt 16 Zyklen und 1024 effektive Teile.
4. `CurrentVECycles` kann über 32767 laufen, ohne dass die PC-Anzeige fehlerhaft wird.
5. `TotalCycles` wird als DWORD korrekt gelesen.
6. `CompletionSequence` erhöht sich genau einmal pro abgeschlossener VE.
7. `LastCompletedVECycles` und `LastCompletedCavities` bleiben bis zum nächsten Abschluss stabil.
8. Manueller Wechsel reagiert nur einmal auf eine neue CommandSequence.
9. PC-Neustart erzeugt keinen verlorenen ersten Befehl.
10. WLAN-Unterbrechung stoppt die lokale Zählung und den fälligen VE-Wechsel nicht.
11. Q1 liefert genau einen Impuls der parametrierten Länge.
12. Ein ungültiger Auftrag setzt den definierten Fehlercode und aktiviert Q1 nicht.

## 13. Noch offen für die reale Inbetriebnahme

Vor Erstellung der finalen LOGO!-Datei müssen an der ersten Maschine nur noch die realen elektrischen Randbedingungen festgelegt werden:

- Signalart und Pegel des Zyklusimpulses,
- LOGO!-Hardwarevariante und Versorgungsspannung,
- Ventilspulenspannung / notwendiges Koppelrelais,
- tatsächliche Q1-Ausgangsart,
- vorhandene oder nicht vorhandene Endlagenrückmeldung I2,
- gewünschte Impulsdauer des Wechslers.

Erst danach wird aus dieser Spezifikation das freizugebende `Partcounter_LOGO_V001` in LOGO! Soft Comfort aufgebaut und an einer Testmaschine validiert.
