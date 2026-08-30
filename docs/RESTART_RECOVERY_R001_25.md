# Partcounter R001.25 – 24/7 Neustart-Recovery

## Ziel
Ein PC-/Partcounter-Neustart darf einen real in der Siemens LOGO! laufenden Auftrag nicht als neuen oder unbekannten Auftrag behandeln. Der PC-Zustand wird deshalb zusätzlich als SQLite-Recovery-Checkpoint persistiert.

## Persistierter Zustand
Pro Maschine wird maximal ein offener Echtauftrag gespeichert, u. a. mit Auftragsnummer, stabiler JobId, Artikel/Werkzeug, Kavitäten, Standard-VE, Auftragsmenge, geplanter Hold-Grenze, manueller-VE-Pending-Status sowie den zuletzt bekannten Zählerständen. Simulation wird nicht als Echtauftrag persistiert.

## Crashfenster beim Auftragsstart
Vor dem Modbus-Auftragswrite wird `PendingActivation` persistiert.
- Wird die LOGO!-Übernahme bestätigt, wird daraus `Active`.
- Scheitert die Übertragung kontrolliert, wird der Checkpoint gelöscht.
- Nach einem Crash wird ein Pending-Checkpoint nur übernommen, wenn `JobIdEcho` der realen LOGO! exakt passt. Passt er nicht, wird der Pending-Checkpoint verworfen, ohne einen fremden LOGO!-Auftrag zu verändern.

## Programmstart
Recovery-Aufträge werden lokal ausschließlich als **PAUSIERT** geladen. Im Simulationsmodus sind Resume, Reset, manueller VE-Wechsel und andere zustandsverändernde Aktionen für diese Aufträge gesperrt. Dadurch kann die Simulationsuhr niemals einen echten Recovery-Auftrag fortzählen.

## Umschalten in Echtbetrieb
Für jede Recovery-Maschine:
1. frischen Protocol-V3-Snapshot lesen,
2. `JobIdEcho` und `ActiveCavitiesEcho` prüfen,
3. bei passender Identität `PauseCounting` senden und Ack abwarten,
4. neuen Snapshot lesen,
5. HoldAfterVE gegen Checkpoint bzw. deterministische aktuelle Planung prüfen,
6. MachineState aus dem echten LOGO-Zählerstand rekonstruieren,
7. Auftrag absichtlich pausiert lassen.

Erst danach kann der Bediener `Fortsetzen` wählen; die bestehende Boundary-Recovery aktualisiert bei bereits erreichtem Hold zuerst Ziel/Hold und gibt erst anschließend die Zählung frei.

## Offline produzierte VEs
Wenn die LOGO! während des PC-Ausfalls weitere Voll-VE produziert hat, wird der echte Zählerstand übernommen. Partcounter erzeugt **keine erfundenen Abschlusszeitpunkte**. Stattdessen wird `RECOVERY_OFFLINE_PROGRESS` protokolliert; fehlende historische Zeitstempel bleiben transparent als Recovery-Lücke erkennbar.

## Manueller VE-Wechsel während Neustart
War ein manueller VE-Wechsel pending und die LOGO! zeigt über `CompletedVEs` plus `LastCompletionReason=Manual` eindeutig dessen Abschluss, wird die nächste VE unter bestätigter Pause neu geplant. Ist der Abschluss nicht eindeutig, bleibt normales Resume gesperrt.

## Freigabegrenze
CI prüft Persistenz, Rekonstruktion und Doppel-Completion-Schutz. Die reale Wiederanlaufwirkung wird zusätzlich an M01 mit echtem Partcounter-Abbruch/Neustart und LOGO!-Weiterlauf abgenommen.


## Unklare Auftragsübernahme
Wenn ein realer Auftragswrite wegen Verbindungsabbruch nicht eindeutig bestätigt werden kann, bleibt `PendingActivation` absichtlich erhalten. Partcounter darf auf dieser Maschine keinen neuen Auftrag starten, weil die LOGO! den Write möglicherweise bereits verarbeitet hat. Der Recovery-Abgleich entscheidet später anhand von `JobIdEcho`.

Ein Pending-Checkpoint mit abweichender JobId wird nur dann automatisch verworfen, wenn die LOGO! **nachweislich leer/inaktiv** ist: JobIdEcho=0, TotalCycles=0, CurrentParts=0, CompletedVEs=0 und AutomaticEnabled=0. Jeder andere fremde oder unklare LOGO!-Zustand blockiert die Echtbetriebsaktivierung.
