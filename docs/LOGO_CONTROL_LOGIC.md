# Siemens LOGO! – Partcounter V2 Steuerlogik

**Partcounter Revision:** R001.7  
**LOGO!-Programm:** `Partcounter_LOGO_V001`  
**Protokoll:** Modbus V2

Dieses Dokument beschreibt die verbindliche Sollfunktion der standardisierten LOGO!-Steuerung. Die detaillierte I/O-, VM-, Merker- und Blockzuordnung steht in `PARTCOUNTER_LOGO_V001_IMPLEMENTATION.md`.

## Grundprinzip

Die LOGO! zählt Maschinenzyklen lokal und löst den Verpackungswechsel lokal aus. Der PC liefert Auftragsparameter, berechnet Teilemengen aus den Zykluswerten und visualisiert den Zustand. Ein kurzer PC-, LAN- oder WLAN-Ausfall darf keinen Zyklus und keinen fälligen VE-Wechsel verlieren.

## Hardwaregerechte Zählung

Die LOGO! führt keine Multiplikation `Zyklen × Kavitäten` aus. Stattdessen werden die nativen Zykluszähler übertragen:

```text
CurrentVECycles
TotalCycles
LastCompletedVECycles
```

Der PC berechnet:

```text
CurrentParts            = CurrentVECycles × ActiveCavitiesEcho
LastCompletedVEQuantity = LastCompletedVECycles × LastCompletedCavities
```

Für Partcounter V2 gelten bewusst folgende Grenzen:

- `CurrentVECycles` / `TargetCyclesPerVE`: 1…32767 je VE
- `TotalCycles`: 0…999999 je LOGO!-Auftrag
- CommandSequence / CompletionSequence / Heartbeats: 1…32767, danach Wrap auf 1

Die VE-Grenze von 32767 ermöglicht einen sicheren Snapshot des abgeschlossenen VE-Zykluswertes innerhalb der LOGO!-16-Bit-Analogwelt, während der Gesamtzykluszähler als nativer DWord-Zähler genutzt wird.

## I/O-Standard

| Signal | Richtung | Funktion |
|---|---|---|
| I1 | Eingang | gültiger Zyklus-/Auswurfimpuls |
| I2 | Eingang | optionale Endlage VE-Wechsler |
| I3 | Eingang | optionale Handquittierung |
| Q1 | Ausgang | Pneumatikventil VE-Wechsler |
| Q2 | Ausgang | optionale Wechselanzeige |
| Q3 | Ausgang | optionale Sammelstörung |

## Zyklusflanke und Zählfreigabe

Der reale Zyklusimpuls wird zuerst mit einem `AND mit Flankenauswertung` auf genau einen LOGO!-Programmdurchlauf reduziert. Erst danach wird dieser Puls mit den Betriebsfreigaben verknüpft:

```text
CycleEdge
AND AutomaticEnabled
AND NOT PauseCounting
AND NOT VeChangeActive
        ↓
CountPulse
```

Diese Reihenfolge verhindert einen falschen Zählimpuls, wenn beispielsweise eine Pause aufgehoben wird, während I1 noch HIGH ist.

## Dynamischer Zielzykluswert

`TargetCyclesPerVE` liegt als DWord auf HR10/HR11 bzw. VD18 und wird per Parameter-VM-Mapping direkt dem `On Threshold` des VE-Zählerblocks zugeordnet.

```text
TargetCyclesPerVE = ceil(TargetPartsPerVE / ActiveCavities)
```

Die PC-Anwendung ist der einzige freigegebene schreibende Modbus-Client. Eine Änderung von VD18 während einer laufenden VE ist unzulässig. Partcounter überträgt einen neuen VE-Zielwert nur unmittelbar nach einem VE-Abschluss bei `CurrentVECycles = 0` oder beim Start eines neuen Auftrags.

Beispiel:

```text
VE-Soll 1000
Kavitäten 64
TargetCyclesPerVE = 16
Effektive VE = 1024 Teile
```

## Neuer Auftrag

Ein neuer Auftrag wird nur mit neuer CommandSequence und gesetztem Reset-Bit übernommen. PC-seitig werden vor dem Modbus-Schreiben geprüft:

- ProtocolVersion = 2
- ActiveCavities = 1…64
- TargetPartsPerVE > 0
- TargetCyclesPerVE = 1…32767
- ValvePulseMs = 50…5000 ms
- ValvePulseMs ist durch 10 teilbar

Bei einem gültigen neuen Auftrag:

```text
CurrentVECycles = 0
TotalCycles = 0
CurrentVENumber = 1
CompletedVEs = 0
Pause = 0
AckSequence = CommandSequence
```

## Zykluszählung

Bei jedem freigegebenen `CountPulse`:

```text
CurrentVECycles += 1
TotalCycles += 1
```

Der automatische Abschluss wird über die Einschaltgrenze des VE-Zählers ausgelöst:

```text
CurrentVECycles >= TargetCyclesPerVE
```

## Dynamische letzte VE eines Produktionsauftrags

Ist die Restmenge kleiner als die Standard-VE-Menge, überträgt Partcounter nach Abschluss der vorherigen VE eine neue Zielzykluszahl, ohne den Gesamtauftrag zurückzusetzen:

```text
TargetPartsPerVE  = Restmenge
TargetCyclesPerVE = ceil(Restmenge / ActiveCavities)
CommandResetJob   = 0
neue CommandSequence
```

Gesamtzähler, VE-Nummer und abgeschlossene VEs bleiben erhalten.

## Automatischer VE-Abschluss

Die positive Flanke des VE-Zählerausgangs erzeugt genau einen `CompletionPulse`. Vor dem Reset von `CurrentVECycles` müssen die Abschlussdaten gespeichert werden:

```text
LastCompletedVECycles = CurrentVECycles
LastCompletedCavities = ActiveCavities
LastCompletedVENumber = CurrentVENumber
LastCompletionReason  = 1
CompletedVEs         += 1
CompletionSequence   += 1
```

Der Zyklusstand wird mit einem Analog-Watchdog als Sample-and-Hold gespeichert: Die positive Flanke von `CompletionPulse` übernimmt den aktuellen Zählerstand als gespeicherten Vergleichswert `Aen`. Dieser Wert wird auf VW56 ausgegeben. Erst ein nachgelagerter kurzer Resetimpuls löscht anschließend den VE-Zähler.

Danach:

```text
VeChangeActive = 1
Q1 = 1 für konfigurierte Impulszeit
Q1 = 0
CurrentVENumber += 1
CurrentVECycles = 0
VeChangeActive = 0
```

`CompletionSequence` erhöht sich exakt einmal pro fertiger VE und springt nach 32767 wieder auf 1.

## Manueller VE-Wechsel

Ein manueller Wechsel wird nur ausgeführt bei:

```text
NewCommand
AND ManualVeChangeBit
AND CurrentVECycles > 0
```

Er verwendet denselben Abschlussablauf, jedoch:

```text
LastCompletionReason = 2
```

Ein manueller Wechsel bei leerer VE wird nicht ausgeführt, der Befehl wird aber als bearbeitet quittiert.

## Persistenz des CompletionReason

Der aktuelle Grund 1/2 wird zunächst als analoger Kandidat erzeugt und mit einem zweiten Analog-Watchdog bei `CompletionPulse` gespeichert. Dadurch bleibt `LastCompletionReason` nach dem One-Shot-Ereignis bis zum nächsten VE-Abschluss stabil auf HR36 verfügbar.

Dasselbe Prinzip wird für `LastCompletedCavities` verwendet: Die beim Abschluss aktive Kavitätenzahl wird mit `CompletionPulse` gespeichert und auf HR37 ausgegeben.

## Ventilimpuls

Die Bedienoberfläche arbeitet in Millisekunden. Auf HR7 wird ein Wert in festen 10-ms-Einheiten übertragen:

```text
ValvePulse10Ms = ValvePulseMs / 10
```

Beispiel:

```text
750 ms → HR7 = 75
```

Der LOGO!-Zeitbaustein verwendet fest die Zeitbasis 10 ms und erhält seinen Zeitwert per Parameter-VM-Mapping aus VW12. Zulässig sind 50…5000 ms in 10-ms-Schritten.

Während der Ventilimpuls aktiv ist, ist die Zykluszählung gesperrt.

## Pause / Fortsetzen

`CommandPauseCounting` mit neuer CommandSequence setzt das Pause-Latch. Eine neue Sequenz mit `Automatic enabled`, aber ohne Pause-Bit, löscht es wieder.

Während Pause:

- keine Zykluszählung,
- kein automatischer Abschluss durch neue Zyklusflanken,
- Kommunikation, Heartbeat und Status bleiben aktiv.

## CommandSequence / AckSequence

Befehlswerte liegen im Bereich 1…32767. Nach 32767 folgt wieder 1.

Ein Befehl ist neu, wenn `CommandSequence != AckSequence`. Da die LOGO!-Analogkomparatoren Differenzen bewerten, wird Ungleichheit symmetrisch erkannt:

```text
CmdGreaterAck = CommandSequence - AckSequence > 0
AckGreaterCmd = AckSequence - CommandSequence > 0
NewCommand    = CmdGreaterAck OR AckGreaterCmd
```

Damit wird auch der Wrap 32767 → 1 erkannt.

Nach Bearbeitung:

```text
AckSequence = CommandSequence
```

Partcounter liest nach PC-Neustart oder Erstverbindung zunächst `AckSequence`, synchronisiert seinen lokalen Sequenzstand und sendet erst danach den nächsten Befehl. Dadurch kann der erste Befehl nach einem Neustart nicht als altes Duplikat verloren gehen.

## Heartbeats

- PC schreibt zyklisch Werte 1…32767 in HR12 und springt danach auf 1.
- LOGO! schreibt zyklisch Werte 1…32767 in HR34 und springt danach auf 1.
- Entscheidend ist die Änderung des Wertes innerhalb des Diagnosefensters, nicht die numerische Differenz.
- Ein stehender PC-Heartbeat setzt das Statusbit `PcHeartbeatStale`.
- Ein stehender PC-Heartbeat stoppt **nicht** die lokale Zählung oder einen fälligen VE-Wechsel.

## StatusWord HR21

| Bit | Maske | Bedeutung |
|---:|---:|---|
| 0 | 0x0001 | LOGO bereit |
| 1 | 0x0002 | Automatik aktiv |
| 2 | 0x0004 | VE-Wechsel aktiv |
| 3 | 0x0008 | Alarm |
| 4 | 0x0010 | Zykluseingang aktiv |
| 5 | 0x0020 | PC-Heartbeat steht |

## Fehlercodes

| Code | Bedeutung |
|---:|---|
| 0 | kein Fehler |
| 1 | falsche Protokollversion |
| 2 | Kavitätenzahl außerhalb 1–64 |
| 3 | TargetPartsPerVE = 0 |
| 4 | TargetCyclesPerVE außerhalb 1–32767 |
| 5 | Ventilimpuls außerhalb 50–5000 ms bzw. kein 10-ms-Raster |
| 10 | optionale Wechsler-Endlage nicht rechtzeitig erreicht |
| 30 | interner ungültiger Ablaufzustand |

## Optionaler Endlagentest

Wenn I2 physisch vorhanden und freigegeben ist, startet nach dem Ventilimpuls ein Überwachungsfenster. Wird die erwartete Endlage nicht rechtzeitig erreicht, setzt die LOGO! `ErrorCode = 10`, Alarmstatus und optional Q3. Weitere automatische Wechsel werden gesperrt, bis die Störung kontrolliert quittiert wurde.

Diese Funktion wird erst aktiviert, nachdem die reale Mechanik und das Endlagensignal an der Testmaschine validiert wurden.

## Kommunikationsausfall

Bei stehendem PC-Heartbeat fährt die LOGO! mit den zuletzt gültigen Parametern weiter:

- Zyklusimpulse zählen,
- VE-Zähler fortsetzen,
- bei Zielzyklen lokal wechseln,
- LastCompleted-Daten aktualisieren,
- CompletionSequence erhöhen.

Nach Wiederkehr liest Partcounter den aktuellen Zustand und synchronisiert den Leitstand.

## LOGO!-Neustart

Ein LOGO!-Spannungsausfall wird ausdrücklich anders behandelt als ein PC-/WLAN-Ausfall:

- Q1 muss beim Start AUS sein.
- Es darf keine selbsttätige Bewegung allein aus einem alten Ausgangszustand entstehen.
- Zählerretentivität und Wiederanlauf werden an der ersten Maschine real geprüft.
- Erst nach erfolgreichem Power-Cycle-Test wird das Wiederanlaufkonzept freigegeben.

## Pneumatik / Safety

Partcounter und die Standard-LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztür, Maschinenfreigaben und sonstige Safety-Funktionen verbleiben vollständig im dafür vorgesehenen sicheren Steuerungssystem.

## Betriebsgrenzen Partcounter V2

- Kavitäten: 1…64
- Zyklen pro VE: 1…32767
- Gesamtzyklen pro LOGO!-Auftrag: bis 999999
- VE-Abschlüsse pro LOGO!-Auftrag: bis 32767
- Ventilimpuls: 50…5000 ms in 10-ms-Schritten

Größere Produktionslose müssen in mehrere LOGO!-Aufträge segmentiert werden, solange kein erweitertes Zählkonzept freigegeben ist.

## Inbetriebnahme

Die erste reale Station wird ausschließlich anhand von `COMMISSIONING_TEST_PROTOCOL_R001_7.md` freigegeben. Insbesondere werden Zyklusflanken, Modbus-DWord-Reihenfolge, Sequenz-Wrap, Heartbeat-Wrap, Ventilzeit, Kommunikationsausfall, Power-Cycle und Etikettierung real geprüft.
