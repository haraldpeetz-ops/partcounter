# Partcounter

**Aktueller Engineering-Stand:** R001.25 – Final Hardening  
**Version:** 0.1.25  
**Plattform:** Windows 10/11 · C# · .NET 10 LTS · WPF  
**Anlage:** bis zu 30 Spritzgussmaschinen · Siemens LOGO! · Modbus TCP · WLAN/LAN  
**LOGO-Protokoll:** Modbus TCP Protocol V3

Partcounter ist ein industrieller Leitstand für Verpackungseinheiten im Spritzguss. Die Siemens LOGO! zählt Maschinenzyklen lokal und steuert den nicht sicherheitsgerichteten VE-Wechsler; Partcounter verwaltet Aufträge, VE-Ziele, Historie, Etikettierung, Reprint, ARBURG ALS/proALPHA, Diagnose, Recovery und Inbetriebnahme.

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

R001.24/R001.8-Dateien sind historische Revisionsunterlagen und keine aktuelle Bauvorgabe.

## Release-Gates

Ein freigabefähiger Head muss bestehen:

1. Restore + NuGet Security Audit.
2. Release-Build mit Warnings-as-Errors.
3. Unit-/Regressionstests.
4. Portable win-x64.
5. SingleFile win-x64.
6. 30-Maschinen-WPF-Stresstest.
7. Multi-Resolution-WPF-Test.
8. statischer Final-Audit.
9. Engineering-/Updatepaketbau.

## M01 bleibt die physische Freigabestufe

Automatisierte Softwaretests ersetzen nicht die reale Maschinenabnahme. Vor `Partcounter 1.0 Production Baseline` müssen insbesondere real validiert werden:

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
