# Partcounter R001.25 – Final Hardening

Branch: `r001.25-final-hardening`  
Version: `0.1.25`  
Runtime: `.NET 10 LTS`  
LOGO-Protokoll: `Protocol V3`

## Freigabeziele
R001.25 schließt die bei der Gesamtprüfung von R001.24 gefundenen Software-Restpunkte: bestätigter Command/Ack-Handshake, Retry/Reconnect, deterministische VE-Grenzen, 24/7-Wiederanlauf, global koordinierte SQLite-Schreiber, echte Tagessicherung im Dauerbetrieb, produktiv exklusive Auftragsquelle ALS/proALPHA, .NET-10-Lifecycle, Warnungen als Buildfehler, Unit-/Regressionstests und konsistente Engineering-Unterlagen.

## Security-/Runtime-Härtung
- Microsoft.Data.Sqlite 10.0.11.
- SQLitePCLRaw.bundle_e_sqlite3 3.0.5 als explizite native SQLite-Basis.
- Unter .NET 10 überflüssige Framework-PackageReferences entfernt.
- ALS/proALPHA-Clientzertifikate werden über `X509CertificateLoader` geladen; mTLS verlangt einen privaten Schlüssel.
- Authenticode-Signer-Lesen ist eng gekapselt; eine spätere Unternehmenssignatur kann per Manifest/Thumbprint erzwungen werden.

## Datenbank-Härtung
Alle produktiven Schreibpfade der gemeinsamen `partcounter.db` laufen über den prozessweiten `SqliteWriteCoordinator`: Kern-/VE-Daten, Settings/Events, Maschinen-/Modbus-Konfiguration, Etikettenvorlagen, historische Druck-Snapshots, Reprintjournal, Commissioning sowie Active-Order-Recovery. Leser bleiben parallel möglich; Writer sind bewusst serialisiert und besitzen einen begrenzten Busy-Timeout.

## LOGO Protocol V3
Die vorhandenen V2-Register wurden nicht verschoben. V3 ergänzt die deterministische VE-Grenze und Recovery-Identität:
- HR13 / VW24: `HoldAfterVeNumber`.
- HR38 / VW74: `HoldAfterVeNumberEcho`.
- HR39/HR40 / VW76/VW78: `JobIdEcho` High/Low.
- Statusbit 6: Completion-Hold geplant/armed.
- Statusbit 7: Completion-Hold aktiv.
- Gesamtzyklen je LOGO-Auftrag: maximal 999.999.
- VE-Zyklen und Sequenzwerte bleiben im freigegebenen LOGO-Bereich.

Ein Befehl gilt erst nach passender AckSequence, `ErrorCode = 0` und – bei Parametertelegrammen – bestätigtem Kavitäten-, Hold- und JobId-Echo als erfolgreich. Wiederholungen verwenden denselben Sequenzwert und lösen One-Shots deshalb nicht doppelt aus.

## Deterministische VE-Grenze
Partcounter plant den nächsten kritischen Grenzpunkt bereits vorab in der LOGO!. Gleichartige Voll-VE können dadurch bei PC-/WLAN-Ausfall autonom weiterlaufen. Vor einer Teil-VE beziehungsweise am Auftragsende blockiert die LOGO! lokal weitere Zählimpulse. Erst nach bestätigter Neuplanung und bewusstem Resume wird wieder gezählt.

## 24/7-Wiederanlauf
Offene Echtaufträge werden persistent gespeichert. Nach einem Partcounter-Neustart werden sie lokal ausschließlich PAUSIERT geladen. Beim Aktivieren des Echtbetriebs prüft Partcounter die reale LOGO! gegen Checkpoint und Protocol V3, pausiert die bekannte Produktionsinstanz nochmals kontrolliert und rekonstruiert erst dann den Leitstand. Ein automatisches Resume findet nicht statt.

Unsichere Auftragsstarts werden als `PendingActivation` persistiert. Eine solche Aktivierung darf nur verworfen werden, wenn die LOGO! nachweislich leer/inaktiv ist; andernfalls bleibt die Maschine für eine neue Beauftragung gesperrt.

## Eindeutige Produktionsinstanz
Die sichtbare Auftragsnummer ist nicht mehr die technische Recovery-Identität. Jeder neue Echtauftrag erhält vor dem ersten LOGO-Schreiben eine eigene kryptografisch erzeugte, persistierte `JobId`. Beide 16-Bit-Wörter bleiben im Bereich 0…32767, sodass die LOGO!-Analog-/Netzwerkbausteine sie eindeutig spiegeln können. Erst wenn der PendingActivation-Checkpoint sicher gespeichert wurde, darf der Modbus-Auftragsstart erfolgen.

## Software-Gates
Jeder freigabefähige Head muss vollständig bestehen:
1. NuGet Restore + Security Audit.
2. Release-Build mit Compilerwarnungen als Fehler.
3. Unit-/Regressionstests.
4. Portable- und SingleFile-Publish win-x64.
5. Realer WPF-Simulations-Stresstest mit 30 Maschinen.
6. Multi-Resolution-WPF-Test von 800×500 bis 1920×1080.
7. Statischer Final-Audit.
8. Engineering-/Update-Paketbau.

## Bewusste externe Freigabebedingung
Softwaretests können die reale M01-Abnahme nicht ersetzen. Vor `Partcounter 1.0 Production Baseline` müssen mindestens LOGO!-Scanlogik, I1-Zyklusimpuls, Q1/Koppelrelais/Ventil, Modbus-Wortreihenfolge, Command/Ack-Retry, Completion-Hold, PC-/WLAN-Ausfall, Partcounter-Neustart, LOGO!-Power-Cycle, Teil-VE, Etikettendruck sowie die tatsächlich eingesetzten ALS/proALPHA-Endpunkte real geprüft werden.

Partcounter/LOGO ist keine Sicherheitssteuerung. Not-Halt, Schutztür und sonstige Maschinen-Sicherheitsfunktionen bleiben vollständig im dafür vorgesehenen sicheren Steuerungssystem.

## Release-Regel
Der Branch wird erst nach einem vollständig grünen finalen Industrial-Gate und konsistentem V3-Engineeringstand auf `main` übernommen. Erst die reale M01-Abnahme hebt den Status von „Production Release Candidate“ auf „Production Baseline“.
