# Partcounter R001.25 – Final Hardening

Branch: `r001.25-final-hardening`  
Version: `0.1.25`  
Runtime: `.NET 10 LTS`

## Freigabeziele
R001.25 schließt die bei der Gesamtprüfung von R001.24 gefundenen Software-Restpunkte: bestätigter Command/Ack-Handshake, Retry/Reconnect, sichere VE-Grenztransaktion, global koordinierte SQLite-Schreiber, echte 24/7-Tagessicherung, produktiv exklusive Auftragsquelle ALS/proALPHA, .NET-10-Lifecycle, Warnungen als Buildfehler, Unit-/Regressionstests und aktuelle Hilfe/README.

## Security-/Runtime-Härtung
- Microsoft.Data.Sqlite 10.0.11.
- SQLitePCLRaw.bundle_e_sqlite3 3.0.5 als explizite sichere native SQLite-Basis.
- Unter .NET 10 überflüssige Framework-PackageReferences entfernt.
- ALS/proALPHA-Clientzertifikate werden über X509CertificateLoader geladen; mTLS verlangt einen privaten Schlüssel.
- Authenticode-Signer-Lesen ist eng gekapselt; eine zukünftige Unternehmenssignatur kann per Manifest/Thumbprint erzwungen werden.

## Datenbank-Härtung
Alle produktiven Schreibpfade der gemeinsamen `partcounter.db` laufen über den prozessweiten `SqliteWriteCoordinator`: Kern-/VE-Daten, Settings/Events, Maschinen-/Modbus-Konfiguration, Etikettenvorlagen, historische Druck-Snapshots, Reprintjournal sowie Commissioning-Profile und -Checks. Leser bleiben parallel möglich; Writer sind bewusst serialisiert und besitzen einen begrenzten Busy-Timeout.

## Kommunikations-Härtung
Ein LOGO!-Befehl gilt erst nach passender AckSequence, ErrorCode 0 und – bei Parametertelegrammen – plausiblem Kavitäten-Echo als bestätigt. Wiederholungen verwenden denselben Sequenzwert. Der Online-VE-Grenzwechsel arbeitet nach `Pause → Ziel bestätigen → Resume`, damit ein Verbindungsfehler nicht unbemerkt mit einem veralteten VE-Ziel weiterzählt.

## Bewusste externe Freigabebedingungen
Eine reale Maschinenabnahme kann Softwareautomation nicht ersetzen. R001.25 ist erst nach bestandenem M01-Prüfprotokoll als Produktionsbaseline zu markieren.

## Release-Regel
Der Branch darf erst auf `main` übernommen werden, wenn Restore/Security-Audit, warnings-as-errors Build, Unit Tests, WPF-Stresstest, Multi-Resolution-Layouttest und statischer Final-Audit vollständig PASS melden.

Validierungscheckpoint: vollständiger Industrial-Gate nach finaler SQLite-Writer-Vereinheitlichung.
