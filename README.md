# Partcounter

**Aktueller Engineering-Stand:** R001.25 HF6 – Modbus Transport Hardening<br>
**Version:** 0.1.25 · **FileVersion:** 0.1.25.6<br>
**Plattform:** Windows 10/11 · C# · .NET 10 LTS · WPF<br>
**Anlage:** bis zu 30 Spritzgussmaschinen · Siemens LOGO! · Modbus TCP · WLAN/LAN<br>
**LOGO-Protokoll:** Modbus TCP Protocol V3

Partcounter ist ein industrieller Leitstand für Verpackungseinheiten im Spritzguss. Die Siemens LOGO! zählt Maschinenzyklen lokal und steuert den nicht sicherheitsgerichteten VE-Wechsler; Partcounter verwaltet Aufträge, VE-Ziele, Historie, Etikettierung, Reprint, ARBURG ALS/proALPHA, Diagnose, Recovery und Inbetriebnahme.

## R001.25 HF6 – Modbus Transport Hardening

HF6 beseitigt zwei Race-Conditions im realen PC↔LOGO!-Transport:

- alle Operationen eines LOGO!-Clients einschließlich Connect/Disconnect laufen serialisiert,
- verspätete Modbus-Antworten vorheriger Retry-Versuche werden anhand ihrer Transaction-ID verworfen,
- ein fehlgeschlagener Poll darf eine parallel neu aufgebaute Befehlsverbindung nicht mehr schließen,
- automatisierte Stress-/Layoutläufe können nicht mehr durch unsichtbare Inbetriebnahme-Dialoge blockiert werden,
- Maschinen-Kontextmenüs werden bei WPF-Containeränderungen zusammengefasst und wiederverwendet statt fortlaufend neu erzeugt,
- Integrationstests decken FC03, FC06, FC16, Reconnect, Protokoll-Mismatch, Parallelzugriff und verspätete Antworten ab.

LOGO!-Programm und Protocol-V3-Registermatrix bleiben unverändert.

Details: `docs/HOTFIX_R001_25_6_MODBUS_TRANSPORT_HARDENING.md`.

## R001.25 HF5 – Operating Mode Isolation

HF5 trennt Simulation und Echtbetrieb in eigenständige Laufzeitdomänen. Simulationszustände können dadurch weder reale Recovery-Zustände blockieren noch produktive VE-Historie oder LOGO!-Sessions verändern.

Details: `docs/HOTFIX_R001_25_5_OPERATING_MODE_ISOLATION.md`.

## R001.25 HF3 – Operating Mode UI

HF3 stellt die Betriebsart-Umschaltung als eindeutig sichtbare reguläre Bedienfunktion bereit:

- dauerhaft sichtbarer Betriebsart-Balken oberhalb der Hauptreiter,
- direkter Schalter `Simulation ↔ Echtbetrieb`,
- Anzeige der aktuellen Betriebsart über `SystemStatusText`,
- keine Admin-Freigabe mehr für die reine Betriebsart-Umschaltung,
- historischer, admin-abgefangener Kopfzeilen-Schalter wird ausgeblendet,
- reale Mindestfenstergröße 800×500 passend zum bestehenden Multi-Resolution-Layoutgate,
- Modbus-/LOGO-/Drucker-/Systemkonfiguration bleibt weiterhin admin-geschützt.

Details: `docs/HOTFIX_R001_25_3_OPERATING_MODE_UI.md`.

## R001.25 HF2 – Protocol Contract

HF2 härtet das reale PC↔LOGO!-Zusammenspiel, ohne die freigegebene Protocol-V3-Registermatrix zu verschieben:

- vollständige Job-Telegramme schreiben HR12/VW22 `PcHeartbeat` nicht mehr mit 0,
- gültiger Heartbeat `1..32767` bleibt beim 13-Register-Jobwrite erhalten,
- `HoldAfterVeNumber=0` wird vor einem produktiven Modbus-Schreiben abgelehnt,
- CommandSequence und V3-Payload werden zusätzlich auf ihren freigegebenen Wertebereich geprüft,
- TCP-Verbindungsaufbau besitzt einen begrenzten Timeout mit eindeutiger IP-/Port-Diagnose,
- Protocol-Mismatch nennt Soll-/Ist-Version und HR20/VW38,
- echter TCP/NModbus-Loopback-Integrationstest prüft Connect → HR1..HR13 → HR20..HR40 → Ack/Echos → Reconnect.

Details: `docs/HOTFIX_R001_25_2_PROTOCOL_CONTRACT.md`.

## R001.25 – Final Hardening

- Migration auf .NET 10 LTS.
- Compilerwarnungen sind Buildfehler.
- NuGet-Sicherheitsaudit im Release-Gate.
- Microsoft.Data.Sqlite 10.0.11 / SQLitePCLRaw 3.0.5.
- Prozessweit koordinierte SQLite-Schreibzugriffe.
- 24/7-Tagessicherung auch ohne Programmneustart.
- CommandSequence/AckSequence mit Reconnect/Retry.
- gleicher Sequenzwert bei Retry, damit One-Shots nicht doppelt ausgelöst werden.
- ErrorCode-, Kavitäten-, Hold- und JobId-Echo-Prüfung.
- deterministische LOGO-seitige VE-Grenze mit `HoldAfterVeNumber`.
- lokale Sperre verhindert Leakage-Zyklen zwischen Grenz-VE und PC-Poll.
- eindeutige persistierte technische JobId je realer Auftragsaktivierung.
- `PendingActivation` schützt unklare Auftragsstarts nach Kommunikationsverlust.
- 24/7-Wiederanlauf: offene Echtaufträge werden nach PC-Neustart nur PAUSIERT rekonstruiert und niemals automatisch fortgesetzt.
- produktiv exklusive Auftragsquelle: ARBURG ALS **oder** proALPHA.
- DPAPI-geschützte Schnittstellen-Secrets.
- .NET-10-konformer X509/mTLS-Pfad.
- optionale Authenticode-Prüfung für künftig signierte Updatepakete.
- Unit-/Regressionstests einschließlich Recovery, VE-Grenze, JobId und Hilfe-Ressourcen.
- realer WPF-Simulations-Stresstest mit 30 Maschinen.
- Multi-Resolution-WPF-Layoutgate von 800×500 bis 1920×1080.

## Protocol V3

### PC → LOGO

`HR1…HR13 / VW0…VW24`

Neu gegenüber V2:
- HR13 / VW24 = `HoldAfterVeNumber`.

### LOGO → PC

`HR20…HR40 / VW38…VW78`

Neu gegenüber V2:
- HR38 / VW74 = `HoldAfterVeNumberEcho`,
- HR39/HR40 / VW76/VW78 = `JobIdEcho` High/Low,
- Statusbit 6 = CompletionHoldArmed,
- Statusbit 7 = CompletionHoldActive.

LOGO! ist Modbus-TCP-Server, Partcounter Client/Master. Standard TCP 502, Unit ID 1.

Die vollständige normative Belegung steht in `docs/MODBUS_REGISTER_MAP.md`.

## VE-Grenzprinzip

Mehrere gleichartige Voll-VE dürfen auch bei PC-/WLAN-Ausfall lokal weiterlaufen. Partcounter programmiert aber den nächsten kritischen Grenzpunkt vorab in die LOGO!. Wenn `CompletedVEs` diesen Punkt erreicht, blockiert die LOGO! das CountGate lokal. Erst nach bestätigter Neuparametrierung und bewusstem Resume wird weitergezählt.

Damit hängt der Schutz der letzten Teil-VE beziehungsweise des Auftragsendes nicht von der PC-Poll-Latenz ab.

## Restart Recovery

Vor jedem realen Auftragsstart persistiert Partcounter einen Checkpoint mit technischer JobId. Nach einem PC-/Programmneustart:

1. gespeicherter Auftrag wird lokal PAUSIERT geladen,
2. LOGO-Protocol V3 wird gelesen,
3. JobId/Kavitäten/Hold werden abgeglichen,
4. die bekannte LOGO-Instanz wird kontrolliert pausiert,
5. Zähler und VE-Zustand werden rekonstruiert,
6. der Auftrag bleibt PAUSIERT,
7. Fortsetzen erfolgt nur bewusst durch den Bediener.

Ein unklarer `PendingActivation`-Zustand blockiert eine neue Beauftragung, bis die reale LOGO-Identität eindeutig geklärt ist.

## Aktueller LOGO!-Werkstattstand

Für R001.25 ausschließlich verwenden:

- `docs/LOGO_CONTROL_LOGIC.md`
- `docs/MODBUS_REGISTER_MAP.md`
- `docs/logo_v001/README_R001_25.md`
- `docs/logo_v001/LOGO_V001_VM_MAP_R001_25.csv`
- `docs/logo_v001/LOGO_V001_BLOCK_CONNECTIONS_R001_25.csv`
- `docs/logo_v001/LOGO_V001_IO_WIRING_R001_25.csv`
- `docs/logo_v001/LOGO_V001_TEST_CASES_R001_25.csv`
- `docs/logo_v001/LOGO_V001_STATION01_BUILD_SHEET_R001_25.md`
- `docs/logo_v001/STATION01_PARTCOUNTER_LOGO_V001_R001_25.ini`

Aktueller transferfähiger LOGO!-Binärstand: `PARTCOUNTER_LOGO_V001_R001_25_HF3_4_TRANSFERREADY.lsc`. HF3 verändert ausschließlich die PC-Bedienoberfläche; die Protocol-V3-Registermatrix und der validierte LOGO!-FBD-/VM-Graph bleiben unverändert.

R001.24/R001.8-Dateien sind historische Revisionsunterlagen und keine aktuelle Bauvorgabe.

## Release-Gates

Ein freigabefähiger Head muss bestehen:

1. Restore + NuGet Security Audit.
2. Release-Build mit Warnings-as-Errors.
3. Unit-/Regressionstests.
4. TCP/NModbus Protocol-V3-Loopback-Integrationstest.
5. Portable win-x64.
6. SingleFile win-x64.
7. 30-Maschinen-WPF-Stresstest.
8. Multi-Resolution-WPF-Test einschließlich sichtbarer Betriebsart-Leiste.
9. statischer Final-Audit.
10. Engineering-/Updatepaketbau.

## M01 bleibt die physische Freigabestufe

Automatisierte Softwaretests ersetzen nicht die reale Maschinenabnahme. Vor `Partcounter 1.0 Production Baseline` müssen insbesondere real validiert werden:

- T01: echte LOGO! in RUN, Modbus-Server erreichbar, HR20/VW38 = 3,
- I1-Pegel, Pulsbreite und positive Flanke,
- High-/Low-Word-Reihenfolge,
- Command/Ack-Retry mit verlorener Antwort,
- HoldAfterVeNumber / HoldActive und Null Leakage-Zyklen,
- Q1/Koppelrelais/Ventil und Impulszeiten,
- PC-/WLAN-Ausfall und Wiederkehr,
- Partcounter-Neustart mit aktivem Auftrag,
- LOGO-Power-Cycle und Retentivität,
- letzte Teil-VE / Auftragsende,
- realer Etikettendruck und Reprint,
- tatsächlich eingesetzte ALS-/proALPHA-Endpunkte.

## Safety

Partcounter und die Standard-Siemens-LOGO! sind **keine Sicherheitssteuerung**. Not-Halt, Schutztüren, sichere Bewegungsfreigaben und alle sicherheitsgerichteten Funktionen verbleiben vollständig in den dafür vorgesehenen Maschinenkreisen bzw. Safety-Steuerungen.
