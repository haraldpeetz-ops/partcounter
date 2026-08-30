# Siemens LOGO! – Partcounter Protocol V3 Steuerlogik

**Partcounter Revision:** R001.25  
**LOGO!-Programm:** `Partcounter_LOGO_V001`  
**Protokoll:** Modbus TCP Protocol V3

Dieses Dokument beschreibt die verbindliche Sollfunktion der LOGO!-Applikationslogik für Partcounter R001.25. Die normative Registerbelegung steht in `MODBUS_REGISTER_MAP.md`. PC-Software und LOGO!-Programm müssen denselben V3-Stand verwenden.

## 1. Systemgrenze

Die LOGO! zählt Maschinenzyklen lokal und löst den Verpackungswechsel lokal aus. Der PC liefert Auftragsparameter, verwaltet Auftragsidentität/Recovery, berechnet Teilemengen aus den Zykluswerten, dokumentiert VEs und visualisiert den Zustand.

Ein PC-/LAN-/WLAN-Ausfall darf gleichartige Voll-VE nicht künstlich stoppen. An einer vorab geplanten kritischen VE-Grenze blockiert die LOGO! jedoch lokal weitere Zählimpulse, bis Partcounter die nächste Zielkonfiguration bestätigt hat.

Partcounter/LOGO ist **keine Safety-Steuerung**. Not-Halt, Schutztüren und sichere Bewegungsfreigaben verbleiben vollständig im Maschinen-Sicherheitskreis.

## 2. I/O-Standard

| Signal | Richtung | Funktion |
|---|---|---|
| I1 | Eingang | gültiger Zyklus-/Auswurfimpuls 24 V DC |
| I2 | Eingang | optionale Endlage VE-Wechsler |
| I3 | Eingang | optionale lokale Quittierung |
| Q1 | Ausgang | Ansteuerung Koppelrelais / Pneumatikventil VE-Wechsler |
| Q2 | Ausgang | optionale Wechselanzeige |
| Q3 | Ausgang | optionale Sammelstörung |

Q1 soll im industriellen Aufbau ein geeignetes 24-V-Koppelrelais ansteuern; das Ventil wird über den Relaiskontakt geschaltet. Spulenstrom, Absicherung und Freilauf-/Suppressorbeschaltung sind anhand der realen Hardware zu dimensionieren.

## 3. Hardwaregerechte Zählung

Die LOGO! führt keine Teilemultiplikation aus. Sie stellt native Zykluswerte bereit:

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

Freigegebene Grenzen:
- Target/CurrentVECycles: 1…32767 je VE.
- TotalCycles: 0…999999 je LOGO!-Auftrag.
- VE-Nummern und Sequenzen: maximal 32767.

Aufträge oberhalb dieser Grenzen werden PC-seitig vor Start abgewiesen und müssen segmentiert werden.

## 4. Zyklusflanke und CountGate

I1 wird zuerst auf eine **positive Flanke** reduziert. Erst dieser One-Shot wird durch die Betriebsbedingungen geführt.

```text
I1
 ↓ positive Flanke
CycleEdge
 AND AutomaticEnabled
 AND NOT PauseCounting
 AND NOT TargetReached
 AND NOT ValvePulseActive
 AND ConfigValid
 AND NOT CompletionHoldActive
 ↓
CountPulse
```

`CountPulse` speist gleichzeitig `CurrentVECycles.Cnt` und `TotalCycles.Cnt`.

Die Flankenauswertung vor allen Gates verhindert einen künstlichen Zählerimpuls, wenn eine Sperre aufgehoben wird, während I1 noch HIGH ist.

## 5. Dynamischer VE-Zielwert

`TargetCyclesPerVE` wird über HR10/HR11 als `VD18` bereitgestellt und dem On-Threshold des VE-Zählers zugeordnet.

```text
TargetCyclesPerVE = ceil(TargetPartsPerVE / ActiveCavities)
```

Partcounter berücksichtigt die dadurch mögliche Kavitäten-Überfüllung bereits in der Auftragsplanung.

## 6. Neuer Auftrag / technische Produktionsinstanz

Ein realer Auftrag besitzt zwei verschiedene Identitäten:
- sichtbare Auftragsnummer aus Bedienung/ERP;
- technische, zufällige `JobId` als konkrete Produktionsinstanz.

Partcounter persistiert vor dem ersten LOGO-Schreibvorgang einen `PendingActivation`-Recovery-Checkpoint. Erst wenn dieser sicher in SQLite gespeichert ist, darf der Auftrag an die LOGO! gesendet werden.

Der Starttelegramm enthält mindestens:
- ProtocolVersion = 3,
- neue CommandSequence,
- ResetJob-Bit,
- ActiveCavities,
- TargetPartsPerVE,
- TargetCyclesPerVE,
- ValvePulse10Ms,
- JobId,
- HoldAfterVeNumber.

Bei gültigem ResetJob setzt die LOGO! die lokalen Auftragszähler auf den definierten Startzustand und übernimmt die neue JobId. Danach wird `AckSequence = CommandSequence` gesetzt.

## 7. CommandSequence / AckSequence

One-Shot-Befehle werden nur verarbeitet, wenn:

```text
CommandSequence != AckSequence
```

Die Ungleichheit muss auch beim Wrap 32767 → 1 erkannt werden. Nach der Verarbeitung:

```text
AckSequence = CommandSequence
```

Der PC verwendet bei einem Retry **denselben** Sequenzwert. Dadurch darf ein bereits bearbeiteter Reset oder manueller VE-Wechsel nicht ein zweites Mal ausgelöst werden, nur weil die TCP-Antwort verloren ging.

Partcounter akzeptiert einen Parameterbefehl erst nach:
1. passender AckSequence,
2. ErrorCode = 0,
3. ActiveCavitiesEcho = Soll,
4. HoldAfterVeNumberEcho = Soll,
5. JobIdEcho = Soll.

## 8. Automatischer VE-Abschluss

Der VE-Zähler erreicht den dynamischen On-Threshold. Nach kurzer Stabilisierung wird genau ein `CompletionPulse` erzeugt.

Vor dem VE-Zählerreset müssen gespeichert werden:

```text
LastCompletedVECycles
LastCompletedCavities
LastCompletedVENumber
LastCompletionReason = 1
CompletedVEs += 1
CompletionSequence += 1
```

Anschließend wird der VE-Wechslerimpuls ausgeführt und der aktuelle VE-Zähler zurückgesetzt. `CompletionSequence` ändert sich exakt einmal pro abgeschlossener VE.

## 9. Deterministischer Completion-Hold

Protocol V3 führt `HoldAfterVeNumber` ein. Dieser Grenzpunkt wird **vorab** in der LOGO! gespeichert.

Grundregel:

```text
CompletionHoldArmed  = HoldAfterVeNumber > CompletedVEs
CompletionHoldActive = HoldAfterVeNumber > 0
                       AND CompletedVEs >= HoldAfterVeNumber
```

Wenn `CompletionHoldActive = 1`, muss `CountGate` lokal geschlossen bleiben. Ein nachfolgender I1-Puls darf nicht mehr in die nächste VE eingehen.

Damit können gleichartige Voll-VE autonom laufen. Erst an einem echten Zielwechsel oder Auftragsende entsteht eine lokale Haltegrenze.

Beispiel:
```text
9500 Teile Auftrag
1000 Teile Standard-VE
8 Kavitäten

VE 1…9 autonom
nach VE 9 Completion-Hold
PC bestätigt Rest-VE-Konfiguration
Resume
VE 10 läuft
nach VE 10 finaler Hold
```

Wichtig: Dieser Completion-Hold ist eine **Applikationsverriegelung**, keine Safety-Funktion.

## 10. Neuparametrierung an der Grenze

Wenn der PC eine CompletionSequence an der geplanten Grenze erkennt:
1. HoldActive prüfen.
2. nächste VE aus Restmenge und Kavitäten berechnen.
3. neue Zielparameter mit gleicher JobId und neuem Hold schreiben.
4. Ack, ErrorCode, Kavitäten-, Hold- und JobId-Echo prüfen.
5. erst danach ResumeCounting senden und bestätigen lassen.

Bei jeder Unklarheit bleibt die Zählung gesperrt und Partcounter protokolliert einen Recovery-/Boundary-Fehler.

## 11. Manueller VE-Wechsel

Der manuelle Wechsel wird nur mit neuer CommandSequence und nichtleerem CurrentVECycles ausgeführt. Partcounter pausiert vor dem manuellen Wechsel und lässt die Zählung bis zur eindeutigen Completion-/Neuplanungsbestätigung gesperrt.

Der Abschluss verwendet denselben Snapshot-/Ventil-/Resetpfad wie der automatische Abschluss, aber:

```text
LastCompletionReason = 2
```

Ein manueller Wechsel darf bei Retry derselben CommandSequence nicht doppelt ausgelöst werden.

## 12. Ventilimpuls

```text
ValvePulse10Ms = ValvePulseMs / 10
```

Zulässig: 50…5000 ms in 10-ms-Schritten. Während des Impulses ist `CountGate` gesperrt.

Der reale Q1-Impuls sowie die pneumatische Bewegung werden an M01 gemessen und abgenommen.

## 13. Heartbeat

- PC schreibt HR12 zyklisch 1…32767 und wrappt auf 1.
- LOGO! schreibt HR34 zyklisch 1…32767 und wrappt auf 1.
- Ein stehender PC-Heartbeat setzt `PcHeartbeatStale`.
- Heartbeat stale stoppt normale Voll-VE **nicht**.
- Ein bereits geplanter Completion-Hold bleibt unabhängig vom PC-Heartbeat wirksam.

## 14. StatusWord

| Bit | Bedeutung |
|---:|---|
| 0 | Ready |
| 1 | AutomaticEnabled |
| 2 | VE-Wechslerimpuls aktiv |
| 3 | Alarm |
| 4 | I1 aktiv |
| 5 | PC Heartbeat stale |
| 6 | CompletionHoldArmed |
| 7 | CompletionHoldActive |

## 15. JobId- und Parameter-Echos

Die LOGO! muss die übernommenen Auftragswerte stabil zurückmelden:
- ActiveCavitiesEcho → HR31,
- HoldAfterVeNumberEcho → HR38,
- JobIdEcho High/Low → HR39/HR40.

Das Echo darf nicht aus dem kurzfristigen PC-Telegramm stammen, sondern muss den tatsächlich für die lokale Ablaufsteuerung übernommenen Zustand repräsentieren.

## 16. PC-/Partcounter-Neustart

Offene Echtaufträge werden persistent gespeichert. Nach Neustart:
1. Partcounter lädt sie ausschließlich PAUSIERT.
2. Echtbetrieb muss bewusst aktiviert werden.
3. LOGO-Snapshot wird gelesen.
4. JobIdEcho und Kavitäten werden gegen den Checkpoint geprüft.
5. bekannte LOGO-Instanz wird nochmals kontrolliert pausiert.
6. Snapshot wird erneut gelesen.
7. Zähler/VE/Hold werden rekonstruiert.
8. Auftrag bleibt PAUSIERT.
9. Bediener entscheidet über Resume.

Bei `PendingActivation` darf ein Checkpoint nur verworfen werden, wenn die LOGO! nachweislich keinen aktiven Produktionszustand besitzt. Eine unklare Identität sperrt die neue Beauftragung dieser Maschine.

## 17. PC-/Netzwerkausfall während Produktion

Ohne PC dürfen bis zum bereits programmierten Hold weiterlaufen:
- I1-Zählung,
- CurrentVECycles,
- TotalCycles,
- automatische VE-Abschlüsse,
- VE-Wechslerimpulse,
- Abschluss-Snapshots,
- CompletionSequence.

Erreicht die LOGO! `HoldAfterVeNumber`, bleibt die folgende Zählung lokal blockiert. Dadurch wird ein veraltetes Sollziel nicht unbemerkt weiterproduziert.

## 18. LOGO!-Power-Cycle

Ein LOGO!-Spannungsausfall ist gesondert zu behandeln:
- Q1 muss beim Wiederanlauf AUS sein.
- Keine selbsttätige Bewegung aus altem Ausgangszustand.
- Retentivität von Zählern, AckSequence, JobId, Hold und Abschlussdaten wird an M01 real geprüft.
- Bei nicht eindeutigem Zustand bleibt die Produktionsfreigabe gesperrt.

## 19. ErrorCode

Freigegebene Codes:
- 0: kein Fehler,
- 1: Protocol-Version falsch,
- 2: Kavitäten ungültig,
- 3: Teileziel ungültig,
- 4: Zielzyklen ungültig,
- 5: Ventilimpuls ungültig,
- 10: optionale Wechsler-Endlage Timeout,
- 30: interner Ablaufzustand ungültig.

Neue Codes dürfen nur synchron in LOGO-Engineering, PC-Code und Testmatrix eingeführt werden.

## 20. Abnahmeregel

Software-CI beweist nicht die reale LOGO-Scanreihenfolge oder die physische Ventilwirkung. Vor Produktionsfreigabe von M01 müssen mindestens real geprüft werden:
- I1 positive Flanke,
- CountGate-Sperren,
- High-/Low-Word-Reihenfolge,
- Command/Ack-Retry,
- Reset-One-Shot,
- manueller VE-Wechsel ohne Doppelimpuls,
- HoldAfterVeNumber und HoldActive im selben Abschlussablauf,
- verlorene PC-Verbindung vor/nach einer Grenz-VE,
- Partcounter-Neustart mit aktivem Auftrag,
- LOGO-Power-Cycle,
- Q1/Koppelrelais/Ventil,
- Teil-VE und Auftragsende.

Erst nach bestandenem M01-Protokoll wird R001.25 von „Production Release Candidate“ zur „Production Baseline“.
