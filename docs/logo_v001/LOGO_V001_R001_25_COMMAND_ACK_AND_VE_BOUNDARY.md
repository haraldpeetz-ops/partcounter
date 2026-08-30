# LOGO V001 – R001.25 Command/Ack-, JobId- und VE-Grenzregel

R001.25 verwendet **Modbus TCP Protocol V3**. Die bisherigen V2-Adressen bleiben an ihrer Position; V3 ergänzt HR13 `HoldAfterVeNumber` sowie HR38 `HoldAfterVeNumberEcho` und HR39/40 `JobIdEcho`.

## Command/Ack

Die LOGO! verarbeitet One-Shot-Bits nur, wenn:

```text
CommandSequence != AckSequence
```

Nach vollständig abgeschlossener Verarbeitung übernimmt sie die neue CommandSequence in AckSequence. Ein wiederholtes Telegramm mit **derselben Sequenz** darf ResetJob, ManualVEChange oder AcknowledgeAlarm nicht erneut auslösen.

Partcounter wiederholt bei unklarem TCP-Ergebnis denselben Sequenzwert. Meldet die LOGO! diesen bereits als Ack, wird der Vorgang nur noch anhand der Status-/Echo-Werte validiert.

Ein Parametertelegramm ist PC-seitig erst bestätigt, wenn:
- AckSequence passt,
- ErrorCode = 0,
- ActiveCavitiesEcho passt,
- HoldAfterVeNumberEcho passt,
- JobIdEcho passt.

## Technische JobId

Jede reale Auftragsaktivierung erhält eine eigene persistierte technische Produktionsinstanz-ID. Die sichtbare Auftragsnummer darf wiederverwendet werden; die JobId darf dabei nicht wiederverwendet werden.

Der PendingActivation-Checkpoint wird **vor** dem ersten Modbus-Auftragsstart gespeichert. Ist der Schreibstatus anschließend unklar, bleibt die Maschine für eine neue Beauftragung gesperrt, bis Recovery die LOGO-Identität eindeutig aufgelöst hat.

## Deterministische VE-Grenze

Die reine V2-Folge „PC erkennt Abschluss → sendet Pause“ war für sehr schnelle Maschinen nicht deterministisch, weil zwischen lokalem VE-Abschluss und PC-Poll bereits ein weiterer I1-Zyklus auftreten konnte.

V3 löst das lokal:

```text
PC plant HoldAfterVeNumber voraus
        ↓
LOGO produziert Voll-VE autonom
        ↓
CompletedVEs erreicht HoldAfterVeNumber
        ↓
CompletionHoldActive = 1
        ↓
CountGate lokal gesperrt
        ↓
kein weiterer I1-Zyklus wird gezählt
        ↓
PC replant/prüft Echo
        ↓
bewusstes Resume
```

Der Hold wird nur an realen Zielgrenzen geplant; gleichartige Voll-VE bleiben damit auch bei PC-/WLAN-Ausfall autonom.

## Restart Recovery

Nach Partcounter-Neustart wird ein gespeicherter Echtauftrag niemals automatisch fortgesetzt. Partcounter vergleicht JobId/Kavitäten/Hold mit der realen LOGO!, pausiert die bekannte Instanz nochmals kontrolliert, rekonstruiert die Zähler und lässt den Auftrag PAUSIERT. Resume ist eine bewusste Bedieneraktion.

## Reale Abnahme

Mindestens gezielt testen:
- verlorene TCP-Antwort nach bereits verarbeitetem Write,
- kein Doppel-One-Shot bei Retry,
- AckSequence-Wrap 32767 → 1,
- falsches Cavities-/Hold-/JobId-Echo,
- schneller I1-Puls unmittelbar nach Grenz-Completion: **0 Leakage-Zyklen**,
- Netzwerkverlust vor Grenz-VE,
- Netzwerk-Wiederkehr am aktiven Hold,
- Partcounter-Neustart während laufendem Auftrag,
- JobId-Mismatch beim Recovery,
- PendingActivation mit eindeutig leerer und mit nicht eindeutig leerer LOGO!.
