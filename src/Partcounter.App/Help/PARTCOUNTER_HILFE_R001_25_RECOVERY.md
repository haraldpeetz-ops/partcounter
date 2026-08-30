# PARTCOUNTER R001.25 – Ergänzung Wiederanlauf und Protocol V3

## [RECOVERY-01] Wiederanlauf nach Partcounter- oder PC-Neustart
Kategorie: Wiederanlauf
Abhängigkeiten: ORDER-01, MODBUS-02, SAFETY-01
Folgewirkungen: RECOVERY-02, RECOVERY-03
Schlagwörter: Recovery, Wiederanlauf, Neustart, Stromausfall PC, Auftrag wiederherstellen, pausiert
Screenshot: 80_recovery_hinweis.png
Screenshot-Hinweis: Leitstand direkt nach gefundenem Recovery-Auftrag mit sichtbarem Pausen-/Wiederanlaufhinweis aufnehmen.
---
### Grundsatz
Ein vor dem Neustart laufender Echtauftrag wird nach dem Neustart **niemals automatisch fortgesetzt**. Partcounter lädt den gespeicherten Auftrag lokal ausschließlich im Zustand `PAUSIERT`.

### Ablauf
1. Partcounter erkennt beim Start persistierte Echtaufträge.
2. Der Leitstand zeigt den Wiederanlaufbedarf an.
3. Beim bewussten Aktivieren des Echtbetriebs liest Partcounter zunächst den realen LOGO!-Status.
4. Die technische JobId und die Kavitäten werden gegen den gespeicherten Checkpoint geprüft.
5. Die bekannte LOGO!-Produktionsinstanz wird kontrolliert pausiert.
6. Partcounter liest den Status erneut und rekonstruiert Zähler, VE-Nummer und Grenzhalt.
7. Der Auftrag bleibt danach **PAUSIERT**.
8. Erst der Bediener entscheidet über **Fortsetzen**.

[WICHTIG] Ein Wiederanlauf ist keine neue Beauftragung. Die bestehende technische Produktionsinstanz wird wieder identifiziert; die LOGO! darf dabei nicht stillschweigend zurückgesetzt werden.

[WARNUNG] Meldet Partcounter eine nicht eindeutige LOGO!-Identität, darf der Auftrag nicht durch einen neuen Auftrag überschrieben werden. Ursache zuerst technisch klären.

## [RECOVERY-02] PendingActivation – unklarer Auftragsstart
Kategorie: Wiederanlauf
Abhängigkeiten: RECOVERY-01, MODBUS-03
Folgewirkungen: ORDER-01
Schlagwörter: PendingActivation, unklar, Ack verloren, Auftrag gesperrt, Modbus Write uncertain
Screenshot: 81_pending_activation.png
Screenshot-Hinweis: Statusmeldung eines absichtlich kontrolliert erzeugten PendingActivation-Falls ohne Zugangsdaten aufnehmen.
---
### Was bedeutet PendingActivation?
Partcounter speichert vor jedem realen LOGO!-Auftragsstart einen Recovery-Checkpoint. Erst danach wird das Reset-/Starttelegramm gesendet. Geht nach dem Schreiben die Netzwerkantwort verloren, ist zunächst nicht eindeutig, ob die LOGO! den Auftrag bereits übernommen hat.

Dieser Zustand heißt `PendingActivation`.

### Schutzverhalten
Solange die Situation nicht eindeutig geklärt ist, wird auf dieser Maschine **keine neue Beauftragung zugelassen**. Damit kann ein möglicherweise bereits laufender LOGO!-Auftrag nicht durch einen zweiten Start überschrieben werden.

### Nach Neustart
Partcounter vergleicht die gespeicherte JobId mit dem realen JobIdEcho der LOGO!:
- stimmt die Identität, wird der bestehende Auftrag kontrolliert recovered;
- ist die LOGO! nachweislich vollständig leer/inaktiv, darf der PendingActivation-Checkpoint verworfen werden;
- ist die LOGO! nicht nachweislich leer und die Identität passt nicht, bleibt die Maschine gesperrt.

[WARNUNG] Einen PendingActivation-Zustand niemals durch Löschen der Datenbank oder manuelles Ändern von VM-Registern „beheben“.

## [RECOVERY-03] Technische JobId und sichtbare Auftragsnummer
Kategorie: Wiederanlauf
Abhängigkeiten: RECOVERY-01
Folgewirkungen: MODBUS-03
Schlagwörter: JobId, Auftragsnummer, Instanz, Korrelation, HR39, HR40
Screenshot: 82_jobid_diagnose.png
Screenshot-Hinweis: Diagnoseansicht mit JobId-Echo und Auftragsnummer aufnehmen; keine Zugangsdaten zeigen.
---
### Zwei verschiedene Identitäten
Die sichtbare Auftragsnummer ist die fachliche Nummer aus Bedienung/ERP. Für die technische Wiederanlauferkennung besitzt jede neue reale Auftragsaktivierung zusätzlich eine eigene zufällige `JobId`.

Die JobId wird vor dem ersten LOGO-Schreibvorgang persistent gespeichert und während des gesamten Auftrags nicht geändert.

### Warum ist das wichtig?
Eine Auftragsnummer kann später erneut verwendet werden. Außerdem dürfen theoretische Hash-Kollisionen niemals dazu führen, dass ein alter LOGO!-Auftrag als neuer erkannt wird. Deshalb verwendet R001.25 eine eigene Produktionsinstanz-ID.

### LOGO!-Echo
Die LOGO! meldet die übernommene JobId auf HR39/HR40 zurück. Partcounter verlangt beim Parameter-Ack und beim Wiederanlauf ein passendes Echo.

[WICHTIG] Die JobId ist ein technischer Korrelationswert und kein Passwort oder Sicherheitsgeheimnis.

## [BOUNDARY-01] Completion-Hold – sichere VE-Grenze Protocol V3
Kategorie: Modbus / LOGO
Abhängigkeiten: ORDER-01, MODBUS-02
Folgewirkungen: RECOVERY-01
Schlagwörter: HoldAfterVeNumber, CompletionHold, Grenzhalt, HR13, HR38, Statusbit 6, Statusbit 7, Teil-VE
Screenshot: 83_completion_hold.png
Screenshot-Hinweis: Diagnose/Leitstand an einer kontrollierten VE-Grenze mit Hold-Armed beziehungsweise Hold-Active aufnehmen.
---
### Problem, das der Hold löst
Bei einer sehr schnellen Maschine könnte nach einer fertigen VE bereits der nächste Zyklus eintreffen, bevor der PC den Abschluss über Modbus gepollt und eine neue Teil-VE übertragen hat.

Protocol V3 plant deshalb die nächste kritische Grenze **vorab in der LOGO!**.

### Status
- `CompletionHoldArmed`: ein zukünftiger Grenzhalt ist geplant.
- `CompletionHoldActive`: die geplante VE ist abgeschlossen; weitere Zählpulse sind lokal gesperrt.

### Ablauf an der Grenze
1. LOGO! schließt VE N lokal ab.
2. CompletedVEs erreicht HoldAfterVeNumber.
3. CompletionHoldActive wird aktiv.
4. CountGate blockiert weitere I1-Zyklen.
5. Partcounter liest den Abschluss.
6. Nächstes Ziel, gleicher JobId und neuer Hold werden übertragen und bestätigt.
7. Erst danach wird bewusst fortgesetzt.

### Offline-Autonomie
Der Hold wird nicht nach jeder VE gesetzt. Mehrere gleichartige Voll-VE können deshalb auch bei PC-/WLAN-Ausfall autonom laufen. Die LOGO! hält nur an dem vorab geplanten Punkt an, an dem ein Zielwechsel oder das Auftragsende bevorsteht.

[WARNUNG] Completion-Hold ist eine Applikationsverriegelung und keine sicherheitsgerichtete Funktion.

## [MODBUS-03] Protocol V3 – wann gilt ein Befehl als bestätigt?
Kategorie: Modbus / LOGO
Abhängigkeiten: MODBUS-02
Folgewirkungen: ORDER-01, RECOVERY-02, BOUNDARY-01
Schlagwörter: AckSequence, Retry, ErrorCode, Echo, CommandSequence, Protocol V3
Screenshot: 84_modbus_v3_ack.png
Screenshot-Hinweis: Inbetriebnahme-/Diagnoseansicht mit CommandSequence, AckSequence, ErrorCode und Echo-Werten aufnehmen.
---
### Bestätigungskriterien
Ein Parameterbefehl gilt erst als erfolgreich, wenn alle folgenden Punkte erfüllt sind:
- AckSequence entspricht der gesendeten CommandSequence,
- ErrorCode ist 0,
- ActiveCavitiesEcho entspricht dem Soll,
- HoldAfterVeNumberEcho entspricht dem Soll,
- JobIdEcho entspricht der aktuellen Produktionsinstanz.

### Retry ohne Doppelaktion
Wenn die TCP-Verbindung nach dem Schreiben abbricht, wiederholt Partcounter denselben Befehl mit **derselben CommandSequence**. Hat die LOGO! den Befehl bereits bearbeitet, erkennt Partcounter die schon passende AckSequence und validiert nur noch den Zustand. Ein ResetJob oder ManualVEChange darf dadurch nicht doppelt ausgeführt werden.

### Sequenz-Wrap
CommandSequence arbeitet von 1 bis 32767 und springt danach wieder auf 1. Beim Verbindungsaufbau liest Partcounter zuerst die letzte AckSequence der LOGO! und synchronisiert sich damit.

[PRAXIS] Bei einem Ack-Fehler nicht nur die TCP-Verbindung prüfen. Auch ProtocolVersion, ErrorCode sowie Kavitäten-, Hold- und JobId-Echo kontrollieren.

## [RECOVERY-04] LOGO!-Spannungsausfall und Power-Cycle
Kategorie: Wiederanlauf
Abhängigkeiten: RECOVERY-01, SAFETY-01, COMMISSION-01
Folgewirkungen: -
Schlagwörter: LOGO Neustart, Power Cycle, Retentivität, Stromausfall, Q1
Screenshot: 85_logo_powercycle.png
Screenshot-Hinweis: M01-Inbetriebnahmeprotokoll beziehungsweise LOGO-Diagnose nach kontrolliertem Power-Cycle aufnehmen.
---
### Unterschied zum PC-Neustart
Ein LOGO!-Spannungsausfall betrifft die lokale Zähl-/Ausgangssteuerung selbst. Deshalb gelten dafür strengere reale Abnahmebedingungen.

### Pflichtprüfungen an M01
- Q1 muss beim Wiederanlauf AUS bleiben.
- Es darf keine selbsttätige pneumatische Bewegung allein aus einem alten Ausgangszustand entstehen.
- Retentivität von Zählern, AckSequence, JobId, Hold und Abschlussdaten muss dem freigegebenen LOGO!-Programm entsprechen.
- Partcounter darf nach Wiederverbindung nur bei eindeutiger Identität rekonstruieren.
- Fortsetzen bleibt eine bewusste Bedienerentscheidung.

[WARNUNG] Das gewünschte Retentivitätsverhalten erst nach realem Power-Cycle-Test an M01 auf weitere Stationen übertragen.
