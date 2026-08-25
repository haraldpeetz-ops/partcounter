# Siemens LOGO! – Partcounter V1 Steuerlogik

Dieses Dokument beschreibt die Sollfunktion des standardisierten LOGO!-Programms `Partcounter_LOGO_V001`.
Alle 30 Maschinen verwenden dasselbe Grundprogramm; maschinenspezifisch sind primär IP-/Netzwerkparameter und die physische I/O-Zuordnung.

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

## Grundprinzip

Die LOGO! zählt und wechselt Verpackungseinheiten lokal. Der PC liefert die Auftrags- und VE-Parameter und visualisiert den Zustand.
Ein kurzer PC-/LAN-/WLAN-Ausfall darf deshalb keinen Zyklus und keinen fälligen VE-Wechsel verlieren.

## Auftragsübernahme

Ein neuer Auftrag ist gültig, wenn:

- `ProtocolVersion = 1`
- `ActiveCavities` zwischen 1 und 64
- `TargetPartsPerVE > 0`
- `TargetCyclesPerVE > 0`
- neue `CommandSequence`
- `CommandResetJob` gesetzt

Bei Übernahme werden Kavitäten, VE-Sollmenge, Zielzyklen, Ventilimpuls und Job-ID gelatcht. Danach:

```text
CurrentVECycles = 0
CurrentParts = 0
CurrentVENumber = 1
CompletedVEs = 0
TotalCycles = 0
Pause = 0
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

Beispiel: VE-Soll 1.000 / 64 Kavitäten → 16 Zyklen → 1.024 Teile.

## Dynamische letzte VE eines Produktionsauftrags

Partcounter R001.5 verwaltet die Gesamt-Auftragsmenge auf dem PC.
Wenn die Restmenge kleiner als die Standard-VE-Menge wird, überträgt der PC nach dem vorherigen VE-Abschluss eine neue VE-Konfiguration:

```text
TargetPartsPerVE  = Restmenge
TargetCyclesPerVE = ceil(Restmenge / ActiveCavities)
CommandResetJob   = 0
neue CommandSequence
```

Bei `CommandResetJob = 0` muss die LOGO! die neue VE-Sollmenge und Zielzykluszahl übernehmen, **ohne**
`TotalCycles`, `CompletedVEs`, `CurrentVENumber` oder andere Auftragszähler zurückzusetzen.
Der aktuelle VE-Zähler muss zu diesem Zeitpunkt 0 sein.

Dadurch kann z. B. bei Standard-VE 1.000, Restmenge 260 und 64 Kavitäten die letzte VE mit
5 Zyklen bzw. 320 tatsächlichen Teilen beendet werden.

## Pause / Fortsetzen

`CommandPauseCounting` mit neuer `CommandSequence` setzt ein internes Pause-Latch:

```text
Pause = 1
```

Eine neue Befehlssequenz mit `Automatic enabled`, aber ohne `CommandPauseCounting`, löscht das Pause-Latch:

```text
Pause = 0
```

Während Pause:

- keine Zykluszählung,
- kein automatischer VE-Abschluss aus neuen Zyklusflanken,
- Kommunikation, Heartbeat und Status bleiben aktiv.

Ein manuell beendeter Auftrag wird vom PC pausiert. Der nächste neue Auftrag verwendet `CommandResetJob` und initialisiert die Zähler neu.

## Temporär deaktivierte Maschine

Wenn eine Maschine in Partcounter temporär deaktiviert wird, pausiert der PC einen ggf. laufenden Auftrag und beendet anschließend das Polling zu dieser Station.
Beim Reaktivieren wird die Kommunikation wieder aufgenommen. Ein zuvor pausierter Auftrag wird **nicht automatisch** fortgesetzt; dies erfordert eine bewusste Bedienaktion.

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

Falls eine Endlagenrückmeldung vorhanden ist, soll der Ablauf zusätzlich plausibilisieren, ob der Wechsler seine Zielposition erreicht hat.
Ein Timeout erzeugt `ErrorCode` und Alarmstatus.

## Manueller VE-Wechsel

`CommandManualVeChange` mit neuer `CommandSequence` löst denselben Abschlussablauf aus, jedoch:

```text
LastCompletionReason = 2
```

Ein manueller Wechsel bei leerer VE darf ignoriert werden.

## Befehlssequenz

One-Shot-Befehle und Parameterupdates werden nur bearbeitet, wenn:

```text
CommandSequence != LastProcessedCommandSequence
```

Nach Bearbeitung:

```text
LastProcessedCommandSequence = CommandSequence
AckSequence = CommandSequence
```

Damit lösen dauerhaft gesetzte Bits keine mehrfachen Aktionen aus.

## Kommunikationsausfall

Ein nicht mehr wechselnder PC-Heartbeat ist ein Diagnosefehler, **kein Produktions-Stopp-Befehl**.
Die LOGO! fährt mit den zuletzt gültig übernommenen Auftragsparametern weiter:

- Zyklusimpulse zählen,
- VE-Füllung fortsetzen,
- VE bei Zielzyklen wechseln,
- LastCompleted-Daten aktualisieren.

Nach Wiederkehr des PCs liest Partcounter den aktuellen Zustand und synchronisiert die Anzeige.

## Pneumatik / sichere Auslegung

Partcounter und die Standard-LOGO! sind **keine Sicherheitssteuerung**.
Schutztür, Not-Halt, Maschinenfreigaben und sonstige Safety-Funktionen verbleiben vollständig im dafür vorgesehenen sicheren Steuerungssystem.
Ventil und Mechanik müssen so ausgelegt sein, dass Neustart, Kommunikationsverlust oder Spannungsausfall keinen gefährlichen Bewegungszustand erzeugen.

## Inbetriebnahme je Maschine

Vor Freigabe mindestens prüfen:

1. Zyklusimpuls wird genau einmal pro Gutteilzyklus erkannt.
2. Kavitätenzahl wird korrekt übernommen.
3. VE-Soll und Zielzyklen stimmen mit PC-Anzeige überein.
4. 1-, 2-, 4-, 8-, 16-, 32- und 64-fach-Werkzeuge werden korrekt gerechnet.
5. Nicht teilbare VE-Menge wird korrekt aufgerundet.
6. Manueller Wechsel funktioniert einmal pro `CommandSequence`.
7. Automatischer Wechsel erzeugt genau einen Ventilimpuls.
8. `LastCompletedVEQuantity` bleibt für den PC lesbar.
9. `CompletionSequence` erhöht sich exakt einmal je fertiger VE.
10. Pause stoppt die Zykluszählung, ohne Kommunikation zu stoppen.
11. Fortsetzen setzt die Zählung ohne Reset fort.
12. Parameterupdate für die letzte VE verändert Zielzyklen ohne Auftragsreset.
13. PC-Ausfall während Produktion verhindert den VE-Wechsel nicht.
14. WLAN-Wiederverbindung synchronisiert den Leitstand ohne Doppelzählung.
15. Etikettendruck wird genau einmal pro neu erkannter `CompletionSequence` ausgelöst.
