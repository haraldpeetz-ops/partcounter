# Partcounter R001.23 – Projektcheckpoint

Stand: 29.08.2026  
Branch: `r001.23-stress-proalpha`  
Getesteter Programm-Commit: `afb4bb98ba4ed9e04baaa846414445feadfb07a3`  
Version: `0.1.23` / FileVersion `0.1.23.0`

## Ziel dieser Revision

R001.23 hat zwei Schwerpunkte:

1. belastbare Laufzeit-/Datenbankprüfung des realen WPF-Programms im beschleunigten Simulationsbetrieb,
2. proALPHA als wählbare Auftragsquelle neben ARBURG ALS sowie ein möglichst vollständiger Inbetriebnahme-Preflight für beide Schnittstellen.

## Automatisierter realer Programmlauf-Stresstest

Der CI-Test startet die tatsächlich veröffentlichte Windows-WPF-Anwendung `Partcounter.exe` mit einem isolierten Stressmodus. Es handelt sich nicht um einen Mock der Geschäftslogik. Verwendet werden der reale `MainWindow`-/`MainViewModel`-/`MachineState`-/SQLite-Pfad und die realen ALS-/proALPHA-Dateiparser.

Sicherheitsgrenze des Tests:

- ausschließlich Simulation,
- kein Wechsel in den Modbus-Echtbetrieb,
- keine Modbus-Schreibbefehle,
- keine reale LOGO!-Station und kein reales Ventil werden angesprochen.

### Erster Extremtest – wichtiger gefundener Engpass

Die erste Lastversion erzeugte 7.680 VE-Persistierungen in sehr kurzer Zeit. Build und Publish waren erfolgreich, der Programmlauf überschritt jedoch das 240-Sekunden-Stressfenster. Die Ursache war eine für diesen Extremfall ungünstige Konkurrenz vieler gleichzeitiger SQLite-Schreibzugriffe.

Daraufhin wurde der Datenbankpfad produktionsgerecht gehärtet:

- zentrales `WriteGate` für Partcounter-`DatabaseService`-Schreiboperationen,
- SQLite `busy_timeout=15000`,
- `Default Timeout=15`,
- Connection Pooling aktiviert,
- Schreibpfade für VE, Labelstatus, Artikel, Settings und Events kontrolliert serialisiert,
- parallele Lesezugriffe bleiben möglich.

## Erfolgreicher Stresstest nach Härtung

GitHub Actions Run: `33277096482`  
Stressreport: `STRESS_REPORT_R001_23.txt`  
Ergebnis: **PASS**

Gemessene Last:

- 30 Maschinen,
- 4 Belastungsrunden,
- 64 Kavitäten im Stressartikel,
- 1.920.000 simulierte Sollteile,
- 1.920 ausgelöste VE-Abschlüsse,
- 1.920 / 1.920 VE-Datensätze erfolgreich in SQLite persistiert,
- SQLite `quick_check: ok`,
- 150 zusätzliche Datenbank-Lesedurchläufe,
- proALPHA-Parser: 5.000 Auftragsdatensätze × 3 Durchläufe = 15.000 Datensätze,
- ALS-Parser: 5.000 Auftragsdatensätze,
- keine Parserzählabweichung,
- keine protokollierten Testfehler,
- kein Programmabsturz.

Dauer des erfolgreichen Stresslaufs: `00:03:46.2145317`.

### Speicherbeobachtung

Unter dieser künstlich stark beschleunigten WPF-/Datenbanklast wurden gemessen:

- Working Set Ende: ca. 2.124 MiB,
- Peak Working Set: ca. 2.596 MiB,
- höchster während der Last beobachteter Managed-Memory-Wert: ca. 1.668 MiB.

Diese Werte stammen aus einem absichtlich unrealistisch stark beschleunigten UI-/VE-Sturm und belegen für sich allein kein Memory Leak; der Working-Set-Wert kann nach einer .NET-GC weiterhin bereits zugesicherte Speicherseiten enthalten. Der hohe Peak wird deshalb als Beobachtung dokumentiert und nicht verschwiegen. Vor der endgültigen Produktfreigabe wird zusätzlich ein längerer, realistisch getakteter Hardware-/Soak-Test empfohlen, bei dem Speicherentwicklung über Stunden betrachtet wird.

## Neue Auftragsquellen-Architektur

Unter Administration wird die bisherige ALS-Funktion zu einem gemeinsamen Bereich:

`Auftragsquellen · ARBURG ALS / proALPHA`

Führende Quelle ist wählbar und persistent:

- `ARBURG ALS`
- `proALPHA`

Beide Profile können separat konfiguriert und getestet werden. Die Auswahl bestimmt die führende Quelle für den Regelbetrieb. Das reine Öffnen eines inaktiven Profils zu Inbetriebnahme-/Testzwecken ändert die führende Quelle nicht.

## proALPHA – unterstützte Quellwege

### Datei / Hotfolder

- XLSX / XLSM,
- CSV / TXT / TSV,
- Datei oder Verzeichnis/Hotfolder,
- Dateimuster,
- Excel-Blatt,
- Kopfzeile,
- Trennzeichen,
- Encoding,
- Culture,
- optionale Archiv-/Fehlerordner.

### REST / JSON

Konfigurierbar sind:

- vollständige REST-URL,
- GET/POST,
- Accept-/Content-Type,
- Queryparameter,
- zusätzliche HTTP-Header,
- POST-Body,
- JSON-Wurzelpfad,
- Next-Link-/Pagination-Pfad,
- MaxPages,
- Timeout,
- RetryCount / RetryDelay,
- TLS-Verhalten,
- Client-Zertifikat,
- Proxy System/None/Custom,
- Proxy-Credentials.

Authentifizierung:

- None,
- Basic,
- statischer Bearer Token,
- API-Key,
- OAuth2 Client Credentials,
- OAuth2 Password Grant, falls die konkrete Kundeninstallation dies tatsächlich verwendet,
- OAuth2 Refresh Token.

OAuth2-Felder:

- Token-URL,
- Client-ID,
- Client-Secret,
- Scope,
- Audience/Resource,
- Refresh Token,
- Client-Credentials im Body oder HTTP-Basic-Header,
- zusätzliche Tokenparameter.

Fachlicher proALPHA-Kontext:

- Umgebung/Systemname,
- Firma/Mandant,
- Werk/Standort,
- Arbeitsplatz/Ressource/Work Center,
- Auftragsstatus-Filter.

Diese Werte können über Platzhalter `{Company}`, `{Plant}`, `{Resource}`, `{Status}` in URL, Queryparametern, Headern oder POST-Body eingefügt werden, ohne Partcounter für eine kundenspezifische Parameternamenskonvention neu kompilieren zu müssen.

## proALPHA – Feldmapping

Zwingend für die Partcounter-Auftragsübernahme:

- OrderNumber,
- ArticleNumber,
- OrderQuantity.

Für automatische Maschinenzuordnung empfohlen:

- MachineNumber oder
- MachineName oder
- MachineExternalId oder
- WorkCenter.

Zusätzlich unterstützt:

- OperationNumber,
- ArticleDescription,
- ToolNumber,
- Cavities,
- PackagingQuantity,
- PlannedStart,
- PlannedEnd,
- OrderStatus,
- Priority,
- MaterialNumber,
- MaterialDescription,
- Batch,
- Color,
- CustomerOrder,
- CompanyCode,
- PlantCode,
- LastChanged.

Fehlende Artikel werden nur dann automatisch angelegt, wenn gültige Kavitäten (1–64) und eine VE-Menge > 0 geliefert wurden.

## ARBURG ALS – Zugangsprofil gehärtet

Der vorhandene ALS-Import bleibt kompatibel und unterstützt weiterhin Datei-/Hotfolder sowie REST/JSON. Zusätzlich stehen für kundenspezifische ALS-REST-Installationen zur Verfügung:

- OAuth2 Client Credentials,
- Token-URL,
- Client-ID / Client-Secret,
- Scope,
- Audience/Resource,
- zusätzliche Tokenparameter,
- Client-ID/Secret wahlweise im Body oder Basic Header,
- System-/kein-/Custom-Proxy,
- Proxy-Credentials,
- erweiterter Zugangs-Preflight.

Bei OAuth2 holt Partcounter vor dem ALS-REST-Abruf automatisch ein Access Token und verwendet dieses nur für den laufenden Zugriff als Bearer Token.

## Geheimnis-/Credential-Schutz

Sensible Werte werden nicht in der normalen Settings-JSON abgelegt. Windows-DPAPI wird verwendet für u. a.:

- Passwörter,
- Bearer Tokens,
- API Keys,
- OAuth Client Secrets,
- Refresh Tokens,
- Client-Zertifikat-Passwörter,
- Proxy-Passwörter.

## Inbetriebnahme-Checkliste

Engineering-Dokument:

`INTERFACE_COMMISSIONING_CHECKLIST_R001_23.md`

Die Checkliste verlangt vor dem ersten realen Verbindungstest insbesondere:

- genaue ALS-/proALPHA-Version,
- tatsächlich vorhandenes/lizenziertes Integrationsmodul,
- exakten Endpoint bzw. Datei-/UNC-Pfad,
- Authentifizierungsverfahren,
- Credentials/OAuth-Daten,
- Firma/Mandant/Werk/Ressource,
- DNS/IP/Port/Firewall,
- Proxy,
- TLS-Zertifikatskette / Client-Zertifikat,
- Beispiel-JSON bzw. Beispiel-Exportdatei,
- reales Feldmapping,
- Maschinen-/Work-Center-Zuordnung,
- einen bekannten Testauftrag zur fachlichen Gegenprüfung.

## Wichtig zur Herstellerabhängigkeit

Partcounter stellt die technischen Eingabefelder und flexiblen Mappingmöglichkeiten bereit. Ein universeller proALPHA- oder ALS-Endpunkt bzw. ein universelles Authentifizierungsverfahren kann nicht seriös fest im Programm angenommen werden, weil diese Werte von Version, lizenziertem Integrationsbaustein und Kundeninstallation abhängen. Deshalb sind diese Angaben explizit konfigurierbar und im Preflight dokumentiert.

## Noch ausstehende reale Abnahme

Der erfolgreiche automatisierte Test ersetzt nicht die physische Produktionsabnahme. Vor endgültiger Freigabe bleiben zu prüfen:

1. echte Siemens LOGO!-Modbus-Kommunikation,
2. WLAN-/Netzwerkausfall und Recovery,
3. reales Q1-/Ventil-/Kistenwechsler-Verhalten,
4. realer Etikettendruck über längere Produktionsdauer,
5. echter ARBURG-ALS-Endpunkt mit Kunden-Credentials und bekanntem Testauftrag,
6. echter proALPHA-Endpunkt bzw. Exportweg mit Kunden-Credentials und bekanntem Testauftrag,
7. längerer, realistisch getakteter Soak-Test mit Speichertrend über mehrere Stunden.

Beim ersten Schnittstellentest bleibt `AutoStartOnApply` deaktiviert: Auftrag zuerst nur laden, Mapping und Maschine prüfen, anschließend bewusst im Leitstand starten.

## Weiterhin bewusst zurückgestellt

Bis Funktions- und Hardwareabnahme abgeschlossen sind:

- finales Programm-Icon,
- gemeinsamer Auswahl-Installer `Portable / Einzelplatz / Engineering`.
