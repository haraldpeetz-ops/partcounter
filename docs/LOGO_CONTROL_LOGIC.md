# Siemens LOGO! – Partcounter V2 Steuerlogik

**Partcounter Revision:** R001.7  
**LOGO!-Programm:** `Partcounter_LOGO_V001`  
**Protokoll:** Modbus V2

Dieses Dokument beschreibt die Sollfunktion des standardisierten LOGO!-Programms. Die detaillierte I/O-, VM- und Merkerzuordnung steht in `PARTCOUNTER_LOGO_V001_IMPLEMENTATION.md`.

## Grundprinzip

Die LOGO! zählt Maschinenzyklen lokal und löst den Verpackungswechsel lokal aus. Der PC liefert Auftragsparameter und visualisiert den Zustand. Ein kurzer PC-, LAN- oder WLAN-Ausfall darf keinen Zyklus und keinen fälligen VE-Wechsel verlieren.

## Hardwaregerechte V2-Zählung

Die LOGO! führt keine Multiplikation `Zyklen × Kavitäten` aus. Stattdessen werden die nativen Zykluszähler als DWORD übertragen:

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

Der VE-Zähler und der Gesamtzykluszähler werden als LOGO!-Auf/Ab-Zähler realisiert und per Parameter-VM-Mapping als DWORD in den für Modbus freigegebenen VM-Bereich gespiegelt.

## I/O-Standard

| Signal | Richtung | Funktion |
|---|---|---|
| I1 | Eingang | gültiger Zyklus-/Auswurfimpuls |
| I2 | Eingang | optionale Endlage VE-Wechsler |
| I3 | Eingang | optionale Handquittierung |
| Q1 | Ausgang | Pneumatikventil VE-Wechsler |
| Q2 | Ausgang | optionale Wechselanzeige |
| Q3 | Ausgang | optionale Sammelstörung |

## Auftragsübernahme

Ein neuer Auftrag ist gültig, wenn:

- `ProtocolVersion = 2`
- `ActiveCavities` zwischen 1 und 64
- `TargetPartsPerVE > 0`
- `TargetCyclesPerVE` zwischen 1 und 999999
- `ValvePulseMs` zwischen 50 und 5000 ms
- neue `CommandSequence`
- `CommandResetJob` gesetzt

Bei Übernahme werden die relevanten Parameter gelatcht. Danach:

```text
CurrentVECycles = 0
CurrentVENumber = 1
CompletedVEs = 0
TotalCycles = 0
Pause = 0
AckSequence = CommandSequence
```

## Zykluszählung

Nur eine gültige positive Flanke von I1 erzeugt einen Zyklus.

```text
wenn Zyklusflanke UND Automatik UND nicht Pause UND nicht VE-Wechsel:
    TotalCycles      += 1
    CurrentVECycles  += 1
```

Automatischer Abschluss:

```text
wenn CurrentVECycles >= TargetCyclesPerVE:
    VE abschließen
```

Beispiel: VE-Soll 1.000 / 64 Kavitäten → 16 Zyklen → PC zeigt 1.024 Teile.

## Dynamische letzte VE eines Produktionsauftrags

Wenn die Restmenge kleiner als die Standard-VE-Menge wird, überträgt der PC nach dem vorherigen VE-Abschluss neue VE-Parameter ohne Auftragsreset:

```text
TargetPartsPerVE  = Restmenge
TargetCyclesPerVE = ceil(Restmenge / ActiveCavities)
CommandResetJob   = 0
neue CommandSequence
```

Die LOGO! übernimmt die neue Zielzykluszahl nur bei `CurrentVECycles = 0`. Gesamtzähler, VE-Nummer und abgeschlossene VEs bleiben erhalten.

## Pause / Fortsetzen

`CommandPauseCounting` mit neuer `CommandSequence` setzt das Pause-Latch. Eine neue Sequenz mit `Automatic enabled`, aber ohne Pause-Bit, löscht es wieder.

Während Pause:

- keine Zykluszählung,
- kein automatischer Abschluss durch neue Zyklusflanken,
- Kommunikation, Heartbeat und Status bleiben aktiv.

## Automatischer VE-Abschluss

Die Abschlussdaten müssen **vor** dem Reset des VE-Zählers gespeichert werden:

```text
LastCompletedVECycles = CurrentVECycles
LastCompletedCavities = ActiveCavitiesLatched
LastCompletedVENumber = CurrentVENumber
LastCompletionReason  = 1
CompletedVEs          += 1
CompletionSequence    += 1
VeChangeActive         = 1
Q1                     = 1 für ValvePulseMs
Q1                     = 0
CurrentVENumber       += 1
CurrentVECycles        = 0
VeChangeActive         = 0
```

`CompletionSequence` erhöht sich exakt einmal pro fertiger VE.

## Manueller VE-Wechsel

`CommandManualVeChange` mit neuer `CommandSequence` löst denselben Abschlussablauf aus, jedoch mit:

```text
LastCompletionReason = 2
```

Ein manueller Wechsel bei `CurrentVECycles = 0` wird ignoriert und trotzdem als bearbeiteter Befehl quittiert.

## Befehlssequenz

Ein Befehl wird nur bearbeitet, wenn:

```text
CommandSequence != AckSequence
```

Nach vollständiger Bearbeitung:

```text
AckSequence = CommandSequence
```

Auch ein syntaktisch empfangener, aber wegen ungültiger Parameter abgelehnter Befehl wird quittiert; zusätzlich wird der passende `ErrorCode` gesetzt. Dadurch wird derselbe fehlerhafte Befehl nicht endlos erneut ausgeführt.

Partcounter R001.7 synchronisiert seine lokale Befehlssequenz beim ersten Kontakt mit dem aktuellen `AckSequence`-Wert. Das verhindert eine Sequenzkollision nach PC-Neustart.

## Heartbeat

- PC erhöht `HR12` zyklisch.
- LOGO! erhöht `HR34` zyklisch.
- Wenn der PC-Heartbeat stehen bleibt, setzt die LOGO! Statusbit `PcHeartbeatStale`.
- Dieser Zustand ist Diagnose und **stoppt nicht** die lokale Zählung oder den automatischen VE-Wechsel.

## Fehlercodes

| Code | Bedeutung |
|---:|---|
| 0 | kein Fehler |
| 1 | falsche Protokollversion |
| 2 | Kavitätenzahl außerhalb 1–64 |
| 3 | TargetPartsPerVE = 0 |
| 4 | TargetCyclesPerVE außerhalb 1–999999 |
| 5 | ValvePulseMs außerhalb 50–5000 ms |
| 10 | optionale Wechsler-Endlage nicht rechtzeitig erreicht |
| 30 | interner ungültiger Ablaufzustand |

## Optionaler Endlagentest

Wenn I2 physisch vorhanden und aktiviert ist, startet nach dem Ventilimpuls ein Zeitfenster. Wird die erwartete Endlage nicht erreicht, setzt die LOGO! `ErrorCode = 10`, Alarmstatus und Q3. Weitere automatische Wechsel werden gesperrt, bis die Störung quittiert wurde.

Diese Funktion darf erst aktiviert werden, nachdem die reale Endlagenlogik der Mechanik bekannt und getestet ist.

## Kommunikationsausfall

Bei stehendem PC-Heartbeat fährt die LOGO! mit den zuletzt gültig übernommenen Parametern weiter:

- Zyklusimpulse zählen,
- VE-Zähler fortsetzen,
- bei Zielzyklen lokal wechseln,
- LastCompleted-Daten aktualisieren,
- CompletionSequence erhöhen.

Nach Wiederkehr liest Partcounter den aktuellen Zustand und synchronisiert die Anzeige.

## Pneumatik / sichere Auslegung

Partcounter und die Standard-LOGO! sind keine Sicherheitssteuerung. Schutztür, Not-Halt, Maschinenfreigaben und sonstige Safety-Funktionen verbleiben vollständig im dafür vorgesehenen sicheren Steuerungssystem.

Q1 muss bei LOGO!-Start und bei ungültiger Konfiguration ausgeschaltet sein. Ventil und Mechanik müssen so ausgelegt werden, dass Neustart, Kommunikationsverlust oder Spannungsausfall keinen gefährlichen Bewegungszustand erzeugen.

## Inbetriebnahme je Maschine

Vor Freigabe mindestens prüfen:

1. I1 erzeugt exakt einen Zählschritt je Gutteilzyklus.
2. Kavitätenzahl wird korrekt übernommen.
3. 1-, 2-, 4-, 8-, 16-, 32- und 64-fach-Werkzeuge ergeben korrekte PC-Teilezahlen.
4. Nicht teilbare VE-Mengen werden korrekt auf volle Werkzeugzyklen aufgerundet.
5. `CurrentVECycles` und `TotalCycles` werden als DWORD korrekt gelesen.
6. Zählerstände über 32767 bleiben korrekt.
7. Manueller Wechsel wird nur einmal je CommandSequence ausgeführt.
8. Automatischer Wechsel erzeugt genau einen Ventilimpuls.
9. `LastCompletedVECycles` und `LastCompletedCavities` bleiben stabil bis zum nächsten Abschluss.
10. `CompletionSequence` erhöht sich exakt einmal je fertiger VE.
11. Pause stoppt die Zählung, aber nicht die Kommunikation.
12. Letzte VE kann ohne Auftragsreset eine neue Zielzykluszahl übernehmen.
13. PC-Neustart verursacht keine CommandSequence-Kollision.
14. PC-/WLAN-Ausfall verhindert einen fälligen VE-Wechsel nicht.
15. Wiederverbindung synchronisiert den Leitstand ohne Doppelabschluss.
16. Etikettendruck wird genau einmal pro neu erkannter CompletionSequence ausgelöst.
