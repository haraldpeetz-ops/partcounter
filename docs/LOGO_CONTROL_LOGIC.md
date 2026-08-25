# Siemens LOGO! – Partcounter V1 Steuerlogik

Dieses Dokument beschreibt die Sollfunktion des standardisierten LOGO!-Programms `Partcounter_LOGO_V001`. Alle 30 Maschinen sollen dasselbe Grundprogramm verwenden; maschinenspezifisch sind primär IP-/Netzwerkparameter und die physische I/O-Zuordnung.

## I/O-Vorschlag

| Signal | Richtung | Funktion |
|---|---|---|
| I1 | Eingang | gültiger Zyklus-/Auswurfimpuls der Spritzgussmaschine |
| I2 | Eingang | optional Endlage/Bestätigung VE-Wechsler |
| I3 | Eingang | optional Handquittierung |
| Q1 | Ausgang | Pneumatikventil VE-Wechsler |
| Q2 | Ausgang | optional Lampe „VE voll / Wechsel“ |
| Q3 | Ausgang | optional Störung / Sammelmeldung |

Die endgültige I/O-Belegung ist maschinenbezogen zu validieren.

## Funktionsblöcke

Empfohlene logische Struktur:

```text
B001  Zyklus-Flankenerkennung
B002  Eingangsentprellung / Mindestimpulszeit
B003  Auftragsparameter-Latch
B004  Zykluszähler aktuelle VE
B005  Gesamtzykluszähler Auftrag
B006  Teilewert aktuelle VE
B007  Vergleich VE-Zielzyklen
B008  VE-Abschluss-Latch
B009  Ventilimpuls
B010  VE-Nummer
B011  Anzahl fertiger VE
B012  LastCompleted-Speicher
B013  CompletionSequence
B014  CommandSequence/Acknowledge
B015  PC-Heartbeat-Überwachung
B016  LOGO-Heartbeat
B017  Fehler-/Statuswort
```

## Auftragsübernahme

Ein neuer Auftrag ist gültig, wenn:

- ProtocolVersion = 1
- ActiveCavities zwischen 1 und 64
- TargetPartsPerVE > 0
- TargetCyclesPerVE > 0
- neue `CommandSequence`
- `CommandResetJob` gesetzt

Bei Übernahme werden Kavitäten, Sollmenge, Zielzyklen, Ventilimpuls und Job-ID in interne Merker übernommen. Danach:

```text
CurrentVECycles = 0
CurrentParts = 0
CurrentVENumber = 1
CompletedVEs = 0
TotalCycles = 0
AckSequence = CommandSequence
```

## Zykluszählung

Nur eine gültige positive Flanke von I1 darf einen Zyklus erzeugen.

```text
wenn Zyklusflanke UND nicht Pause:
    TotalCycles       += 1
    CurrentVECycles   += 1
    CurrentParts       = CurrentVECycles × ActiveCavities
```

Die LOGO! verwendet die vom PC bereits aufgerundete Zielzykluszahl:

```text
wenn CurrentVECycles >= TargetCyclesPerVE:
    automatische VE abschließen
```

Dadurch ist z. B. bei VE-Soll 1.000 und 64 Kavitäten das Ergebnis 16 Zyklen bzw. 1.024 Teile.

## Automatischer VE-Abschluss

Reihenfolge:

```text
LastCompletedVEQuantity = CurrentParts
LastCompletedVENumber   = CurrentVENumber
LastCompletionReason    = 1
CompletedVEs           += 1
CompletionSequence     += 1
Status.VEChangeActive   = 1
Q1                      = 1 für ValvePulseMs
Q1                      = 0
CurrentVENumber        += 1
CurrentVECycles         = 0
CurrentParts            = 0
Status.VEChangeActive   = 0
```

Falls eine Endlagenrückmeldung vorhanden ist, soll der Ablauf statt eines reinen Zeitimpulses zusätzlich plausibilisieren, ob der Wechsler seine Zielposition erreicht hat. Ein Timeout erzeugt `ErrorCode` und Alarmstatus.

## Manueller VE-Wechsel

`CommandManualVeChange` mit neuer `CommandSequence` löst denselben Abschlussablauf aus, jedoch:

```text
LastCompletionReason = 2
```

Ein manueller Wechsel darf bei leerer VE wahlweise ignoriert werden; die R001-PC-Simulation ignoriert einen manuellen Wechsel bei 0 Teilen.

## Befehlssequenz

One-Shot-Befehle werden ausschließlich bearbeitet, wenn:

```text
CommandSequence != LastProcessedCommandSequence
```

Nach Bearbeitung:

```text
LastProcessedCommandSequence = CommandSequence
AckSequence = CommandSequence
```

Dadurch löst ein dauerhaft gesetztes Reset- oder Manual-Bit nicht mehrfach aus.

## Kommunikationsausfall

Ein nicht mehr wechselnder PC-Heartbeat ist ein Diagnosefehler, **kein Produktions-Stopp-Befehl**. Die LOGO! fährt mit den zuletzt gültig übernommenen Auftragsparametern weiter:

- Zyklusimpulse zählen
- VE-Füllung fortsetzen
- VE bei Zielzyklen wechseln
- LastCompleted-Daten aktualisieren

Nach Wiederkehr des PCs liest Partcounter den aktuellen Zustand und synchronisiert die Anzeige.

## Pneumatik / sichere Auslegung

Das Ventil und die Mechanik müssen so ausgelegt sein, dass ein Neustart der LOGO!, Kommunikationsverlust oder Spannungsausfall keinen gefährlichen Bewegungszustand erzeugt. Partcounter und die Standard-LOGO! sind **keine Sicherheitssteuerung**. Schutztür, Not-Halt, Maschinenfreigaben und sonstige Safety-Funktionen verbleiben vollständig im dafür vorgesehenen sicheren Steuerungssystem.

## Inbetriebnahme je Maschine

Vor Freigabe mindestens prüfen:

1. Zyklusimpuls wird genau einmal pro Gutteilzyklus erkannt.
2. Kavitätenzahl wird korrekt übernommen.
3. VE-Soll und Zielzyklen stimmen mit PC-Anzeige überein.
4. 1-, 2-, 4-, 8-, 16-, 32- und 64-fach-Werkzeuge werden korrekt gerechnet.
5. Nicht teilbare VE-Menge wird korrekt aufgerundet.
6. Manueller Wechsel funktioniert einmal pro CommandSequence.
7. Automatischer Wechsel erzeugt genau einen Ventilimpuls.
8. LastCompleted-Menge bleibt nach Reset des aktuellen Zählers erhalten.
9. CompletionSequence erhöht sich exakt einmal je fertiger VE.
10. PC-Ausfall während Produktion verhindert den Wechsel nicht.
11. WLAN-Wiederverbindung synchronisiert den Leitstand ohne Doppelzählung.
12. Etikettendruck wird genau einmal pro neu erkannter CompletionSequence ausgelöst.
