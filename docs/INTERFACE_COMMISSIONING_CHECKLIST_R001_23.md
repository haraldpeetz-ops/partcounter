# Partcounter R001.23 – Schnittstellen-Inbetriebnahmecheck

Stand: 29.08.2026

Ziel dieser Checkliste ist, den ersten realen Verbindungstest zu ARBURG ALS oder proALPHA nicht wegen fehlender Zugangs-, Netzwerk- oder Mappingangaben abbrechen zu müssen. Partcounter stellt die technischen Eingabefelder bereit; die konkreten Werte müssen aus der jeweiligen Kundeninstallation, dem ARBURG ALS Support bzw. der proALPHA-Administration stammen.

## 1. Vor dem Termin allgemein beschaffen

- Ansprechpartner IT / Netzwerk
- Ansprechpartner für das Quellsystem (ALS bzw. proALPHA)
- Produktversion / Build des Quellsystems
- verwendeter Integrationsweg / Modulname / Lizenzstatus
- Test- oder Produktivumgebung
- DNS-Name oder IP des Servers
- TCP-Port
- Firewallfreigaben zwischen Partcounter-PC und Quellsystem
- Proxyvorgaben
- TLS-Zertifikatskette / interne CA, falls eingesetzt
- Client-Zertifikat (.pfx/.p12) plus Passwort, falls mTLS verlangt wird
- Testkonto bzw. technische Service-Credentials mit ausschließlich notwendigen Leserechten
- Beispielauftrag und erwartete Werte zum Gegenprüfen
- Beispielantwort / Exportdatei mit realen Feldnamen

## 2. ARBURG ALS – Datei-/Hotfolder-Zugriff

Benötigt werden, abhängig von der konkreten ALS-Ausgabe:

- konkreter Datei- oder UNC-Pfad
- Windows-Konto, unter dem Partcounter läuft, und dessen Leserechte auf dem Pfad
- Dateimuster, z. B. `*.xlsx` oder `ALS_Order_*.csv`
- Dateiformat: XLSX/XLSM/CSV/TXT/TSV
- Excel-Blattname, falls nicht das erste Blatt verwendet wird
- Nummer der Kopfzeile
- CSV-Trennzeichen
- Zeichenkodierung
- Zahlen-/Datumsformat (Culture, z. B. de-DE)
- optional Archivordner und Schreib-/Verschieberechte
- optional Fehlerordner und Schreib-/Verschieberechte
- vollständige Spaltenliste / Beispiel-Datei für Feldmapping

## 3. ARBURG ALS – REST/JSON-Zugriff

Vor dem ersten Test verbindlich klären:

- ist die REST/API-Funktion im vorhandenen ALS-Stand verfügbar und lizenziert/freigegeben?
- exakte Basis-/Ressourcen-URL für Produktions-/Auftragsdaten
- HTTP-Methode GET oder POST
- erforderliche Queryparameter
- erforderliche zusätzliche HTTP-Header
- erforderlicher POST-Body, falls POST
- Timeout-/Antwortverhalten
- Authentifizierungsart: keine, Basic, statischer Bearer Token, API-Key oder kundenspezifisch OAuth2
- bei Basic: Benutzername + Passwort
- bei Bearer: Token und Gültigkeits-/Erneuerungsregeln
- bei API-Key: Headername + Keywert
- bei OAuth2 Client Credentials: Token-URL, Client-ID, Client-Secret, Scope, optional Audience/Resource, Client-Credentials im Body oder Basic Header, zusätzliche Tokenparameter
- Client-Zertifikat und Passwort, falls mTLS
- Proxy: Systemproxy / kein Proxy / kundenspezifischer Proxy; ggf. Proxy-Credentials
- TLS-Vertrauensstellung; interne CA auf dem Partcounter-PC installieren
- JSON-Wurzelpfad des Auftragsarrays
- Pagination/Next-Link-Verhalten, falls die API nicht alle Aufträge in einer Antwort liefert
- Beispiel-JSON mit mindestens einem freigegebenen Auftrag
- Feldnamen/JSON-Pfade für Partcounter-Mapping
- eindeutige ALS-Maschinenkennung und Zuordnung auf M01–M30

Hinweis: Eine nicht vertrauenswürdige TLS-Verbindung darf nur kurzfristig zu Diagnosezwecken zugelassen werden. Für den Produktionsbetrieb muss die Zertifikatskette korrekt vertrauenswürdig eingerichtet werden.

## 4. proALPHA – Integrationsweg zuerst klären

Vor der Konfiguration muss bekannt sein, welcher proALPHA-Integrationsbaustein beim Kunden tatsächlich genutzt wird, z. B.:

- native/angebotene ERP REST-API
- Integration Workbench / kundenspezifische Services
- Proalpha Integration Platform
- freigegebener Datei-/Exportweg
- kundenspezifischer/Partner-Bridge-Endpunkt

Daraus ergeben sich Endpoint, Authentifizierung und Datenmodell. Partcounter nimmt keinen festen proALPHA-Endpunkt an.

## 5. proALPHA – Datei-/Hotfolder

Analog zum ALS-Dateiimport werden benötigt:

- Datei-/UNC-Pfad
- Zugriffsrechte des Partcounter-Windowskontos
- Dateimuster und Format
- Excel-Blatt / Kopfzeile
- CSV-Trenner / Codierung / Culture
- Archiv-/Fehlerordner und Rechte
- Beispiel-Datei mit realen Feldnamen

## 6. proALPHA – REST/JSON

Vor dem Termin vollständig beschaffen:

### Endpoint / API
- proALPHA-Version/Release
- verwendetes Integrationsmodul
- API-/Service-Basis-URL
- API-Version, falls Bestandteil des Pfades/Headers
- Ressourcen-/Endpoint für Fertigungs-/Produktionsaufträge
- HTTP-Methode
- Accept- und Content-Type
- Queryparameter und Filtersemantik
- zusätzliche Header
- POST-Body, falls erforderlich
- JSON-Wurzelpfad
- Pagination: Next-Link-Feld bzw. Seitenschema
- maximal/sinnvoll abrufbare Seitengröße

### Authentifizierung
- Authentifizierungsverfahren
- Basic: Benutzername/Passwort
- Bearer: Token + Erneuerungsverfahren
- API-Key: Headername + Wert
- OAuth2: Token-URL, Grant Type, Client-ID, Client-Secret, Scope, Audience/Resource
- bei Refresh-Token: Refresh Token
- bei Password Grant, falls kundenseitig tatsächlich eingesetzt: Benutzername/Passwort
- Vorgabe, ob Client-ID/Secret im Authorization Basic Header oder im Form-Body gesendet werden
- zusätzliche OAuth-Tokenparameter
- technische Benutzer-/Clientberechtigungen: mindestens Lesezugriff auf die benötigten Auftragsdaten

### Netzwerk / Sicherheit
- DNS/IP und Port
- Firewallregel
- Proxy-Modus, URL und ggf. Credentials
- TLS-Zertifikatskette / interne CA
- Client-Zertifikat + Passwort bei mTLS

### Fachlicher Kontext
- Firma / Mandant
- Werk / Standort
- Arbeitsplatz / Ressource / Work Center für jede Partcounter-Maschine
- Auftragsstatus, der als freigegeben/produzierbar gilt
- benötigte Filter, damit keine stornierten/erledigten/ungeplanten Aufträge angeboten werden

## 7. Partcounter Pflichtdaten eines Auftrags

Für die Übernahme eines Auftrags braucht Partcounter zwingend:

- Auftragsnummer
- Artikelnummer
- Auftrags-Sollmenge

Für eine automatische Maschinenzuordnung sollte mindestens eines vorhanden/gemappt sein:

- Partcounter-Maschinennummer
- Maschinenname
- externe Maschinen-ID
- bei proALPHA: Arbeitsplatz / Ressource / Work Center

Für das automatische Neuanlegen eines unbekannten Artikels zusätzlich:

- aktive Kavitäten 1–64
- VE-Menge > 0

Empfohlen, aber nicht zwingend:

- Artikelbezeichnung
- Werkzeugnummer
- Planstart / Planende
- Auftragsstatus
- Arbeitsgang
- Priorität
- Materialnummer/-bezeichnung
- Charge
- Farbe
- Kundenauftrag
- Zeitstempel der letzten Änderung
- Firma / Werk bei proALPHA

## 8. Mapping-Abnahme

Vor Produktivfreigabe mit einem bekannten Beispielauftrag prüfen:

1. Quell-Auftragsnummer = Partcounter-Auftragsnummer
2. Quell-Artikelnummer = Partcounter-Artikelnummer
3. Sollmenge identisch
4. richtige Maschine / richtiger Arbeitsplatz zugeordnet
5. Werkzeugnummer korrekt, falls geliefert
6. Kavitäten korrekt
7. VE-Menge korrekt
8. Auftragsstatus wird richtig interpretiert
9. Planzeiten plausibel und Zeitzone geklärt
10. Dubletten/mehrere Arbeitsgänge führen nicht zu einem falschen Auftrag

## 9. Sicherheitsregeln für den ersten Echt-Test

- `AutoStartOnApply` deaktiviert lassen.
- Auftrag zuerst nur laden und im Leitstand kontrollieren.
- nur einen freigegebenen Testauftrag verwenden.
- erst nach korrektem Mapping den Auftrag bewusst starten.
- keine TLS-Zertifikatsprüfung dauerhaft deaktivieren.
- Zugangsdaten nicht in Screenshots/Supportpakete aufnehmen.
- Servicekonten nur mit den tatsächlich benötigten Leserechten ausstatten.

## 10. Abnahmekriterium

Eine Schnittstelle gilt für Partcounter erst als bereit, wenn:

- der Konfigurations-Preflight keine Pflichtfehler meldet,
- der Verbindungs-/Quellentest erfolgreich ist,
- mindestens ein bekannter Testauftrag vollständig eingelesen wird,
- Pflichtfelder und Maschinenzuordnung fachlich geprüft sind,
- der Auftrag kontrolliert in die Partcounter-Auftragsmaske übernommen wird,
- und keinerlei automatische Produktion gestartet wird, solange die Freigabe nicht bewusst aktiviert wurde.
