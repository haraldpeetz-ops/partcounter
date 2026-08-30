# Partcounter R001.25 HF1 – Session Checkpoint 2026-08-30

## Softwarestand
- Hauptstand: R001.25 HF1 / FileVersion 0.1.25.1
- InformationalVersion: 0.1.25-r001.25-hf1-live-mode
- HF1 behebt den Wechsel Simulation -> Echtbetrieb.
- Administrativ deaktivierte Maschinen werden beim Fleet-Aufbau ignoriert.
- Reine Simulationsauftraege werden beim bewussten Wechsel in den Echtbetrieb kontrolliert verworfen.
- Aktivierungsfehler fuehren zu vollstaendigem Rollback in den Simulationsmodus.
- Auftraege koennen im Echtbetrieb nicht auf administrativ deaktivierten Stationen gestartet werden.

## Technischer Stand fuer M01
HF1 veraendert keine LOGO!-Verdrahtung und keine Protocol-V3-Registerbelegung. Es handelt sich ausschliesslich um einen PC-seitigen Betriebsarten-/Fleet-Hotfix.

### Unveraendert durch HF1
- Zyklussignal / physische Eingangsverdrahtung
- Ventil-/Relaisausgang
- Ethernet/WLAN-Topologie
- LOGO!-IP-Adresse pro Station
- Modbus TCP Port 502
- Unit-ID gemaess Maschinenkonfiguration (Standard 1)
- Protocol V3 Registermapping

### Protocol V3 bleibt zwingend
- ProtocolVersion = 3
- HR13 / VW24: HoldAfterVeNumber
- HR38 / VW74: HoldAfterVeNumberEcho
- HR39 / VW76: JobIdEcho High Word
- HR40 / VW78: JobIdEcho Low Word
- Statusbit 6: CompletionHoldArmed
- Statusbit 7: CompletionHoldActive
- bestehende HR1-HR12 und HR20-HR37 wurden durch V3 nicht verschoben

## Naechster Schritt
Realer M01-Test mit Siemens LOGO!:
1. Partcounter starten ohne Simulationsauftrag.
2. Echtbetrieb aktivieren.
3. M01 Online/Offline beobachten.
4. Kleinen Testauftrag starten.
5. Command/Ack, JobId-Echo, Kavitaeten-Echo und Hold-Echo pruefen.
6. I1 zyklisch takten und Zaehler beobachten.
7. VE-Grenzhalt, Q-Ausgang/Ventil, Pause/Resume testen.
8. Netzwerkunterbrechung und Recovery pruefen.
9. Partcounter-Neustart waehrend pausiertem Echtauftrag pruefen.

Erst nach bestandener M01-Hardwareabnahme wird R001.25 HF1 als Production Baseline betrachtet.
