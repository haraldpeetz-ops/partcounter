# Partcounter R001.25 Hotfix 1 – Echtbetrieb-Aktivierung

## Anlass

Im finalen R001.25-Stand konnten administrativ deaktivierte Maschinen den Wechsel von Simulation in den Echtbetrieb abbrechen. `MachineFleetService.StartAsync()` erzeugt Kommunikationssessions ausschließlich für `MachineConfiguration.Enabled == true`; der anschließende Betriebsartwechsel konfigurierte jedoch alle 30 Maschinen über Fleet-Methoden. Für eine deaktivierte Station existierte dadurch keine Session.

Zusätzlich blockierten aktive reine Simulationsaufträge den Wechsel vollständig, obwohl diese Zustände keine reale Produktionswirkung besitzen.

## Hotfix

- Fleet-Operationen beim Betriebsartwechsel werden ausschließlich für administrativ aktivierte Stationen ausgeführt.
- Administrativ deaktivierte Stationen blockieren den Echtbetrieb nicht mehr.
- Liegt auf einer administrativ deaktivierten Station ein echter Recovery-Checkpoint, wird der Wechsel weiterhin bewusst gesperrt und die betroffene Maschine eindeutig genannt.
- Reine Simulationsaufträge werden beim bewussten Wechsel in den Echtbetrieb kontrolliert verworfen; Recovery-Aufträge werden niemals verworfen.
- `IsSimulationMode` wird erst nach vollständig erfolgreicher Initialisierung auf `false` gesetzt.
- Jeder Fehler während der Aktivierung führt zu sauberem Fleet-Rollback, Rückkehr in Simulation und einer verständlichen Statusmeldung statt einer unbehandelten UI-Exception.
- Der Echtbetrieb darf auch bei zunächst nicht erreichbaren LOGO!-Stationen aktiviert werden; die Polling-Worker melden deren realen Online/Offline-Zustand anschließend einzeln. Das ist für Inbetriebnahme und schrittweisen Rollout notwendig.

## Sicherheitsgrenze

Die Recovery-, Protocol-V3-, JobId-, AckSequence- und Completion-Hold-Sicherheitslogik wird durch diesen Hotfix nicht gelockert.
