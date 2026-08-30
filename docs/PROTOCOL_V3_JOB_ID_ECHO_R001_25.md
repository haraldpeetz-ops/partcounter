# Partcounter R001.25 – Protocol V3 JobId-Echo

## Zweck
Für einen sicheren Wiederanlauf nach PC-/Partcounter-Neustart muss der PC eindeutig erkennen können, welcher Produktionsauftrag tatsächlich in der Siemens LOGO! aktiv ist. Kavitäten- und Hold-Echo allein reichen dafür nicht.

## Additive Register
Alle bestehenden V3-Adressen bleiben unverändert. Ergänzt werden am Ende des Statusbereichs:

| HR | PC-Offset | LOGO VM | Bedeutung |
|---:|---:|---|---|
| HR39 | 38 | VW76 | JobIdEcho High Word |
| HR40 | 39 | VW78 | JobIdEcho Low Word |

`StatusLength` steigt damit von 19 auf 21 Register.

Die LOGO! übernimmt die bei einem gültigen neuen Auftrag empfangene `JobId` aus HR8/HR9 in einen lokalen, stabilen Auftragsidentitätswert und gibt diesen auf HR39/HR40 zurück. Zielupdates innerhalb desselben Auftrags dürfen die JobId nicht verändern.

## PC-Prüfung
Ein Auftrag oder VE-Zielupdate gilt nur dann als bestätigt, wenn gleichzeitig gelten:

- AckSequence = gesendete CommandSequence
- ErrorCode = 0
- ActiveCavitiesEcho = Soll
- HoldAfterVeNumberEcho = Soll
- JobIdEcho = Soll
- bei Hold > 0: CompletionHoldArmed ist aktiv

Eine Abweichung wird als Kommunikations-/Protokollfehler behandelt; Partcounter gibt die Zählung nicht frei.

## Gesamtzyklusgrenze
Die bereits implementierte und getestete Grenze von 999.999 LOGO!-Gesamtzyklen je Auftrag bleibt unverändert. Werte oberhalb der freigegebenen Grenze werden vor Auftragsstart abgewiesen/segmentiert.

## Reale Abnahme M01
Vor Echtfreigabe muss an der realen LOGO! geprüft werden, dass JobIdEcho nach Reset/Auftragsstart korrekt gesetzt wird, über normale VE-Wechsel und Zielupdates stabil bleibt und nach LOGO-Neustart dem freigegebenen Retentivitätskonzept entspricht.
