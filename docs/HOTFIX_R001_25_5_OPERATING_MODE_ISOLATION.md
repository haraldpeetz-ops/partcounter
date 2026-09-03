# Partcounter R001.25 HF5 – Operating Mode Isolation

## Anlass

HF4 hat den globalen Echtbetrieb zwar stabilisiert, Simulation und Echtbetrieb nutzten intern jedoch weiterhin dieselben `MachineState`-Objekte und dieselben aktiven Recovery-/Job-/Hold-Sammlungen. Dadurch konnten reale Recovery-Zustände Simulationsaufträge blockieren und Simulationsdaten in produktive Ablaufpfade gelangen.

## HF5-Grundsatz

Simulation und Echtbetrieb sind ab HF5 getrennte Laufzeitdomänen.

### Simulation

- eigener `MachineState` je Maschine,
- eigene Auftrags-/Zählerstände nur im Arbeitsspeicher,
- keine Modbus-Sessions,
- keine LOGO!-Snapshots,
- keine Echtbetrieb-Recovery-Sperren,
- keine `PackagingUnits`-Persistenz,
- kein automatischer Produktionsetikettendruck.

Ein vorhandener Echtbetrieb-Recovery-Auftrag wird während der Simulation ausschließlich separat geparkt. Er ist kein Simulationsauftrag und blockiert keinen Simulationsstart.

### Echtbetrieb

- separater `MachineState` je Maschine,
- Siemens LOGO! Modbus TCP Protocol V3,
- CommandSequence/AckSequence,
- JobId,
- HoldAfterVeNumber / CompletionHold,
- Recovery/PendingActivation,
- produktive VE-Historie und Etikettendruck.

### Betriebsartenwechsel

Simulation → Echtbetrieb:

1. Simulationszustand wird eingefroren, nicht in Live-Objekte kopiert.
2. Geparkte Live-Recovery-/Job-/Hold-Daten werden aktiviert.
3. Modbus-Fleet wird aufgebaut.
4. Die UI wechselt auf die separaten Live-Maschinenobjekte.
5. Recovery wird stationsbezogen abgeglichen.

Echtbetrieb → Simulation:

1. Verifizierte laufende/pausierte Echtaufträge müssen zuerst kontrolliert beendet werden.
2. Ungeklärte Recovery-Aufträge dürfen sicher geparkt bleiben.
3. Modbus-Fleet wird gestoppt.
4. Live-Recovery-/Job-/Hold-Daten werden geparkt.
5. Die UI stellt exakt den zuvor eingefrorenen Simulationszustand wieder her.

## Schutz gegen verzögerte Live-Ereignisse

HF5 entfernt die bisherigen generischen Fleet-Handler und verwendet live-spezifische Handler. Ein verspäteter Dispatcher-Callback wird verworfen, sobald Simulation aktiv ist. Ein LOGO!-Snapshot kann dadurch nicht mehr auf ein Simulationsobjekt angewendet werden.

## Release-Gate

Der WPF-Stresstest wurde fachlich umgestellt. PASS erfordert jetzt zusätzlich:

- Simulationsauftrag startet über den regulären `ApplyArticleCommand`,
- 30 Maschinen / 1.920 simulierte VE-Ereignisse,
- **0 neue `PackagingUnits`-Datensätze aus der Simulation**,
- Simulation und Echtbetrieb verwenden unterschiedliche `MachineState`-Instanzen,
- Rückkehr aus Echtbetrieb stellt dieselbe unveränderte Simulationsinstanz wieder her,
- SQLite-Integrität und bestehende Parser-/Layouttests bleiben grün.

## Version

- Revision: R001.25 HF5
- Product Version: 0.1.25
- FileVersion: 0.1.25.5
- InformationalVersion: `0.1.25-r001.25-hf5-operating-mode-isolation`
- LOGO!-Schnittstelle: Modbus TCP Protocol V3
- LOGO!-Programm: `PARTCOUNTER_LOGO_V001_R001_25_HF3_4_TRANSFERREADY.lsc` unverändert

## Datenhinweis zu älteren Testständen

HF5 verhindert neue Vermischungen. Bereits vor HF5 in `PackagingUnits` gespeicherte Test-/Simulationsdaten besitzen historisch kein verlässliches Betriebsartkennzeichen. Vor einer späteren Produktionsfreigabe muss die bisherige Inbetriebnahme-Datenbank deshalb archiviert bzw. fachlich bereinigt werden; sie darf nicht ungeprüft als Produktionshistorie übernommen werden.
