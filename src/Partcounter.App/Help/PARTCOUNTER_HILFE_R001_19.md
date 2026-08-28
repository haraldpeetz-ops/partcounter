# PARTCOUNTER R001.19 – Professionelle integrierte Hilfe

Diese Datei wird von Partcounter direkt als Hilfedatenbank eingelesen. Jedes Thema enthält Abhängigkeiten, Suchbegriffe und einen vorgesehenen Screenshot-Slot.

## [HELP-01] Hilfe bedienen und schnell zum richtigen Thema springen
Kategorie: Erste Schritte
Abhängigkeiten: -
Folgewirkungen: START-01, DASH-01, ARTICLE-01, HISTORY-01, MACHINE-01, LABEL-01, COMMISSION-01, ALS-01, SETTINGS-01
Schlagwörter: Hilfe, F1, Suche, Kategorien, Kontext, Handbuch
Screenshot: 00_hilfezentrum.png
Screenshot-Hinweis: Hilfezentrum vollständig aufnehmen; links Suche/Kategorie/Themenliste, rechts Hilfetext und Screenshotbereich sichtbar.
---
### Zweck
Das Hilfezentrum ist die zentrale Bedienungs- und Inbetriebnahmedokumentation von Partcounter. Es beschreibt nicht nur Schaltflächen, sondern auch die technische Wirkung, Abhängigkeiten und typische Fehlerbilder.

### Schnellzugriff
1. Drücke **F1**. Partcounter öffnet automatisch das Hilfethema zum aktuell gewählten Hauptbereich.
2. Nutze die Schaltfläche **Hilfe** im Kopfbereich, wenn du das komplette Hilfezentrum öffnen möchtest.
3. Suche links nach Begriffen wie `VE`, `Modbus`, `Etikett`, `Heartbeat`, `ALS`, `Nachdruck` oder einer Fehlermeldung.
4. Über die Kategorie lässt sich die Themenliste auf einen Funktionsbereich begrenzen.
5. Schaltflächen unter **Funktionsabhängigkeiten** springen direkt zu zusammenhängenden Themen.

### Suchlogik
Die Suche berücksichtigt Titel, Beschreibung, Schlagwörter, Abhängigkeiten und Themen-ID. Mehrere Suchwörter werden UND-verknüpft. Die Suche `etikett nachdruck` zeigt deshalb nur Themen, in denen beide Begriffe vorkommen.

[PRAXIS] Wenn du eine konkrete Störung bearbeitest, suche zuerst nach dem sichtbaren Begriff oder Fehlertext und arbeite danach die verknüpften Themen durch.

[WICHTIG] Die Hilfe ersetzt keine elektrische Gefährdungsbeurteilung, keine Maschinen-Safety-Dokumentation und keine Freigabe durch befähigte Personen.

## [START-01] Schnellstart – vom Programmstart bis zur ersten produktiven VE
Kategorie: Erste Schritte
Abhängigkeiten: SAFETY-01, MACHINE-01, ARTICLE-01, PRINT-01
Folgewirkungen: ORDER-01, HISTORY-01, LABEL-01
Schlagwörter: Schnellstart, erster Auftrag, erste VE, Inbetriebnahme, Start
Screenshot: 01_schnellstart_leitstand.png
Screenshot-Hinweis: Leitstand direkt nach Programmstart mit Maschinenwahl, Artikelwahl, Auftragsnummer, Auftragsmenge und Betriebsmodus aufnehmen.
---
### Vor dem ersten produktiven Einsatz
- Siemens LOGO! und PC müssen im vorgesehenen Netzwerk erreichbar sein.
- Die reale Maschine muss im Bereich **Maschinen / Modbus** korrekt angelegt sein.
- Artikelnummer, Werkzeug, Kavitätenzahl und VE-Menge müssen im **Artikelstamm** stimmen.
- Der Windows-Drucker muss unter **Einstellungen / Druck** hinterlegt und per Testetikett geprüft sein.
- Die LOGO!-Station muss das freigegebene Modbus-Protokoll V2 verwenden.

### Ersten Auftrag starten
1. Im **Leitstand** die gewünschte Maschine auswählen.
2. Artikel auswählen.
3. Auftragsnummer kontrollieren oder eintragen.
4. Auftragsmenge eingeben.
5. Ventilimpuls kontrollieren.
6. Bei realem Betrieb den geschützten Betriebsmodus bewusst auf **Echtbetrieb** umstellen.
7. **Auftrag starten** wählen.

Partcounter berechnet aus VE-Menge und aktiven Kavitäten die erforderliche Zahl vollständiger Werkzeugzyklen. Es werden niemals Teilzyklen angenommen. Beispiel: 1000 Teile bei 64 Kavitäten ergeben 16 Zyklen und damit effektiv 1024 Teile.

### Danach beobachten
Im Leitstand müssen Verbindungsstatus, Füllgrad, aktuelle VE, Auftragsfortschritt und Restzyklen plausibel sein. Bei voller VE löst die LOGO! lokal den vorgesehenen VE-Wechsel aus. Partcounter übernimmt die abgeschlossene VE in die Historie und startet bei aktivierter Funktion den Etikettendruck.

[WARNUNG] Vor der ersten realen Produktion immer die geführte Inbetriebnahme an einer Referenzmaschine durchführen.

## [SAFETY-01] Sicherheitsgrenzen von Partcounter und Siemens LOGO!
Kategorie: Sicherheit
Abhängigkeiten: -
Folgewirkungen: START-01, MACHINE-01, COMMISSION-01, ORDER-01
Schlagwörter: Safety, Not-Halt, Schutztür, Gefährdung, Sicherheit, LOGO
Screenshot: 02_safety_hinweis.png
Screenshot-Hinweis: Inbetriebnahme- oder Hilfeseite mit sichtbarem Hinweis auf nicht sicherheitsgerichtete Funktionen aufnehmen.
---
### Grundsatz
Partcounter, WLAN, Modbus TCP und die Siemens LOGO! übernehmen **keine sicherheitsgerichtete Funktion**. Not-Halt, Schutztüren, sichere Bewegungsfreigaben, Druckentlastung und andere Safety-Funktionen müssen in der dafür vorgesehenen sicheren Maschinensteuerung verbleiben.

### Zulässige Aufgaben von Partcounter
Partcounter darf Auftragsparameter verwalten, Zählwerte anzeigen, VE-Zielwerte übertragen, Druckvorgänge auslösen und Betriebsdaten dokumentieren. Die LOGO! darf die lokale Zählung und den vorgesehenen, nicht sicherheitsgerichteten Kistenwechsler-Ablauf übernehmen.

### Mechanische Auslegung
Der Kistenwechsler muss so ausgelegt sein, dass Kommunikationsausfall, Spannungsverlust oder Neustart keinen gefährlichen Zustand erzeugen. Ein monostabiles Ventil und eine definierte Grundstellung sind dafür in vielen Fällen sinnvoll; die konkrete Ausführung ist jedoch durch die reale Anlage zu bewerten.

[WARNUNG] Niemals eine sicherheitsrelevante Verriegelung durch eine Partcounter-Softwarebedingung ersetzen.

## [UI-01] Programmoberfläche, Hauptnavigation und Statusanzeigen
Kategorie: Bedienoberfläche
Abhängigkeiten: HELP-01
Folgewirkungen: DASH-01, ADMIN-01
Schlagwörter: Tabs, Navigation, Statusleiste, Kopfzeile, Simulation, Echtbetrieb
Screenshot: 03_hauptnavigation.png
Screenshot-Hinweis: Gesamtes Hauptfenster mit allen Hauptreitern und rechter Kopfzeile aufnehmen.
---
### Hauptbereiche
Die obere Reiterleiste trennt Produktionsfunktionen und administrative Funktionen. Frei zugänglich bleiben der Leitstand, Artikelstamm und die VE-Historie. Konfigurations- und Engineering-Bereiche sind durch die Administration geschützt.

### Kopfzeile
Rechts oben zeigt Partcounter die Zahl aktiver Maschinen, den Systemzustand und den aktuellen Betriebsmodus. Der Betriebsmodus darf nur bewusst und mit administrativer Berechtigung gewechselt werden.

### Statusleiste
Die untere Statusleiste zeigt zuletzt ausgeführte Aktionen, Warnungen und Fehler. Diese Meldungen sind bei einer Fehlersuche immer mitzulesen.

### Skalierung
Partcounter ist für Windows 10/11 und skalierbare Fenster ausgelegt. Bei sehr hoher Windows-DPI-Skalierung sollte geprüft werden, ob alle Spalten und Schaltflächen sichtbar bleiben.

## [ADMIN-01] Bedienerbetrieb und geschützte Administration
Kategorie: Bedienoberfläche
Abhängigkeiten: UI-01
Folgewirkungen: MACHINE-01, LABEL-01, SETTINGS-01, ALS-01
Schlagwörter: Admin, Passwort, geschützt, Bediener, Rechte, Schloss
Screenshot: 04_admin_schutz.png
Screenshot-Hinweis: Hauptnavigation mit Schloss-Symbolen und Admin-Schaltfläche aufnehmen.
---
### Bedienphilosophie
Für den normalen Produktionsbetrieb sollen Bediener keine Administrationsmaske öffnen müssen. Deshalb sind Leitstand, Artikelstamm und VE-Historie frei zugänglich; technische Einstellungen bleiben geschützt.

### Geschützte Bereiche
Dazu gehören insbesondere Maschinen-/Modbus-Konfiguration, Etiketteneditor, Inbetriebnahme/Diagnose, Rolloutstatus, ARBURG ALS und Einstellungen/Druck.

### Erstmalige Einrichtung
Wenn noch kein Admin-Passwort vorhanden ist, führt Partcounter bei der ersten geschützten Aktion durch die Einrichtung. Danach muss das Passwort für administrative Funktionen eingegeben werden.

[WICHTIG] Das Admin-Passwort sollte nicht als allgemeines Bedienerpasswort verteilt werden.

## [DASH-01] Leitstand – Maschinenkacheln und Produktionsübersicht
Kategorie: Leitstand
Abhängigkeiten: UI-01, ARTICLE-01
Folgewirkungen: ORDER-01, ORDER-02, FILL-01
Schlagwörter: Leitstand, Maschinenkachel, Füllgrad, Fortschritt, aktiv, offline
Screenshot: 10_leitstand_uebersicht.png
Screenshot-Hinweis: Leitstand mit mindestens drei aktiven Maschinenkacheln in unterschiedlichen Füllständen aufnehmen.
---
### Bedeutung der Maschinenkachel
Jede sichtbare Maschine zeigt Auftragsstatus, Artikel, Werkzeug, Kavitäten, aktuelle VE, Füllgrad, Restzyklen und Auftragsfortschritt. Maschinen ohne aktiven Auftrag können ausgeblendet bleiben, damit der Leitstand produktionsrelevant bleibt.

### Verbindungszustand
`ONLINE` bedeutet, dass die laufende Modbus-Kommunikation Antworten erhält. `OFFLINE` oder `FEHLER` muss vor einer produktiven Aktion geklärt werden. In Simulation werden keine realen Modbus-Schreibbefehle ausgeführt.

### VE-Aufmerksamkeit
Kurz vor Erreichen des VE-Ziels wechselt die visuelle Darstellung in Vorwarn- und Wechselzustände. Eine abgeschlossene VE erhält eine deutliche Aufmerksamkeit, bis der Vorgang verarbeitet ist.

[PRAXIS] Bei einer scheinbar stehengebliebenen Maschine zuerst Verbindung, letzten Zykluszeitpunkt, Auftragsstatus und temporäre Deaktivierung prüfen.

## [ORDER-01] Produktionsauftrag starten und Parameter übertragen
Kategorie: Leitstand
Abhängigkeiten: DASH-01, MACHINE-01, ARTICLE-01
Folgewirkungen: FILL-01, HISTORY-01, PRINT-01
Schlagwörter: Auftrag starten, Sollmenge, Artikel, Ventilimpuls, Job, Start
Screenshot: 11_auftrag_starten.png
Screenshot-Hinweis: Oberes Auftragsformular im Leitstand mit ausgefüllter Maschine, Artikel, Auftrag, Menge und Ventilimpuls aufnehmen.
---
### Eingaben
Ein Auftrag benötigt mindestens Maschine, Artikel, Auftragsnummer und Auftragsmenge. Der Artikel liefert Werkzeugnummer, aktive Kavitäten und Standard-VE-Menge.

### Übertragung im Echtbetrieb
Beim Start schreibt Partcounter die freigegebenen Auftragsparameter über Modbus V2 an die ausgewählte LOGO!-Station. Erst wenn die Übertragung erfolgreich war, wird der Auftrag lokal als gestartet behandelt.

### Erste und letzte VE
Die erste VE verwendet die Standard-VE-Menge, solange die Auftragsrestmenge größer ist. Die letzte VE wird automatisch auf die verbleibende Auftragsmenge begrenzt und anschließend auf vollständige Werkzeugzyklen aufgerundet.

[WICHTIG] Ein Schreibfehler an die LOGO! darf nicht durch manuelles Setzen eines Softwarestatus umgangen werden. Ursache der Kommunikation zuerst beheben.

## [ORDER-02] Auftrag pausieren, fortsetzen, beenden und Maschine deaktivieren
Kategorie: Leitstand
Abhängigkeiten: ORDER-01
Folgewirkungen: HISTORY-01, MODBUS-02
Schlagwörter: pausieren, fortsetzen, beenden, deaktivieren, rechtsklick
Screenshot: 12_auftrag_kontextmenu.png
Screenshot-Hinweis: Rechtsklick-Kontextmenü einer Maschinenkachel mit Pause/Fortsetzen/Beenden/Deaktivieren aufnehmen.
---
### Pausieren
Pausieren stoppt die Zählfreigabe, ohne den bestehenden Auftrag zu verwerfen. Die vorhandenen Zähler bleiben erhalten.

### Fortsetzen
Fortsetzen aktiviert die Zählung wieder. Der Ablauf ist so ausgelegt, dass kein künstlicher zusätzlicher Zyklus allein durch das Fortsetzen entsteht.

### Beenden
Beenden schließt den Auftrag administrativ ab. Eine noch nicht abgeschlossene Teil-VE wird dadurch nicht automatisch zu einer fiktiven vollen VE.

### Temporär deaktivieren
Eine deaktivierte Maschine wird aus aktiven Ansichten entfernt und im Echtbetrieb nicht weiter gepollt. Ein pausierter Auftrag wird nach Reaktivierung nicht unbeabsichtigt automatisch fortgesetzt.

## [FILL-01] Füllgrad, Werkzeugzyklen und zyklusbedingte Mehrmenge
Kategorie: Leitstand
Abhängigkeiten: ARTICLE-01, ORDER-01
Folgewirkungen: HISTORY-01
Schlagwörter: Füllgrad, Rundung, Kavitäten, Zyklen, Mehrmenge, VE
Screenshot: 13_fuellgrad_ampel.png
Screenshot-Hinweis: Ampellegende und eine Maschinenkachel mit sichtbarem Füllgrad und Restzyklen aufnehmen.
---
### Berechnung
Partcounter zählt vollständige Maschinenzyklen. Die erforderliche Zykluszahl lautet `ceil(VE-Soll / aktive Kavitäten)`. Die tatsächliche VE-Menge ist `Zyklen × Kavitäten`.

### Beispiel
Bei 64 Kavitäten und VE-Soll 1000 sind 16 vollständige Zyklen erforderlich. Daraus entstehen 1024 Teile, also 24 Teile zyklusbedingte Mehrmenge.

### Ampellogik
Der Leitstand unterscheidet Normalbereich, Vorwarnung, unmittelbar bevorstehenden VE-Wechsel und abgeschlossene VE. Die genaue Farbdarstellung dient der schnellen visuellen Priorisierung, ersetzt aber nicht die numerischen Zähler.

## [ARTICLE-01] Artikelstamm anlegen und pflegen
Kategorie: Artikelstamm
Abhängigkeiten: SAFETY-01
Folgewirkungen: ORDER-01, LABEL-01, ALS-03
Schlagwörter: Artikel, Werkzeug, Kavitäten, Verpackungsmenge, Stammdaten
Screenshot: 20_artikelstamm.png
Screenshot-Hinweis: Artikelstamm mit Eingabeformular links und Artikeltabelle rechts aufnehmen.
---
### Pflichtdaten
Ein Artikel benötigt Artikelnummer, Bezeichnung, Werkzeugnummer, aktive Kavitäten zwischen 1 und 64 und eine VE-Menge größer 0.

### Effektive VE
Partcounter zeigt zusätzlich die berechnete Zahl der Werkzeugzyklen, effektive VE-Menge und erwartete Mehrmenge. Dadurch ist sofort erkennbar, ob die gewünschte Verpackungsmenge mit der Kavitätenzahl teilbar ist.

### Änderung bestehender Artikel
Beim Speichern einer vorhandenen Artikelnummer werden die Stammdaten aktualisiert. Laufende Produktionsaufträge sollten nicht durch ungeprüfte Stammdatenänderungen beeinflusst werden.

[PRAXIS] Vor Serienstart Artikelstamm gegen Arbeitsplan, Werkzeugkarte und Verpackungsvorschrift prüfen.

## [HISTORY-01] VE-Historie und Rückverfolgbarkeit
Kategorie: VE-Historie
Abhängigkeiten: ORDER-01, FILL-01
Folgewirkungen: REPRINT-01, REPRINT-02
Schlagwörter: Historie, Verpackungseinheit, VE-ID, Abschluss, Rückverfolgung
Screenshot: 30_ve_historie.png
Screenshot-Hinweis: VE-Historie mit mehreren abgeschlossenen VEs und Nachdruckleiste aufnehmen.
---
### Inhalt eines VE-Datensatzes
Die Historie dokumentiert Abschlusszeit, Maschine, VE-Nummer, Auftrag, Artikel, Werkzeug, Kavitäten, Sollmenge, Istmenge, Mehrmenge, Abschlussgrund, Etikettenstatus und eindeutige VE-ID.

### Eindeutige VE-ID
Die VE-ID dient als technische Identität der Verpackungseinheit und wird auch in QR-/Barcode-Inhalten verwendet. Ein Nachdruck erzeugt **keine neue VE-ID**.

### Abschlussgrund
Automatischer Abschluss und manueller Abschluss werden getrennt dokumentiert. Damit lässt sich später nachvollziehen, ob eine VE regulär voll oder bewusst manuell beendet wurde.

## [REPRINT-01] Bereits gedrucktes Etikett nachdrucken
Kategorie: VE-Historie
Abhängigkeiten: HISTORY-01, PRINT-01
Folgewirkungen: REPRINT-02
Schlagwörter: Reprint, Nachdruck, Etikett verloren, Ersatzetikett, Druckjournal
Screenshot: 31_reprint_dialog.png
Screenshot-Hinweis: Nachdruckdialog mit ausgewählter VE, Grundauswahl und sichtbarer VE-ID aufnehmen.
---
### Wann verwenden?
Die Nachdruckfunktion ist für verlorene, beschädigte oder unleserliche Etiketten vorgesehen. Sie ist nur für VE-Datensätze verfügbar, die bereits als gedruckt protokolliert wurden.

### Ablauf
1. **VE-Historie** öffnen.
2. Gewünschte VE-Zeile markieren.
3. **Etikett nachdrucken…** wählen oder das Kontextmenü verwenden.
4. Nachdruckgrund auswählen und bei Bedarf eine Bemerkung ergänzen.
5. Druck bestätigen.

### Datenintegrität
Der Nachdruck verwendet dieselbe VE-ID, dieselbe VE-Nummer, dieselben Mengen und denselben ursprünglichen Abschlusszeitpunkt. Es wird keine zweite Verpackungseinheit erzeugt.

### Journal
Jeder Nachdruckversuch erhält eine laufende Nachdrucknummer und protokolliert Zeitpunkt, Drucker, Grund, Erfolg oder Fehler sowie die verwendete Layoutquelle.

## [REPRINT-02] Historischer Etiketten-Layout-Snapshot
Kategorie: VE-Historie
Abhängigkeiten: REPRINT-01, LABEL-01
Folgewirkungen: TROUBLE-04
Schlagwörter: Snapshot, Original-Layout, SHA-256, historisch, Reprint
Screenshot: 32_reprint_snapshot_status.png
Screenshot-Hinweis: VE-Historie nach Auswahl einer VE mit sichtbarem Hinweis 'Original-Layout-Snapshot vorhanden' aufnehmen.
---
### Prinzip ab R001.18
Beim regulären Erst-Druck einer VE archiviert Partcounter automatisch die vollständig aufgelöste Etikettenvorlage. Eingebettete Logos und Bilder befinden sich bereits in der Vorlagendefinition und werden damit ebenfalls gesichert.

### Integritätsprüfung
Der Snapshot erhält einen SHA-256-Prüfwert. Beim Reprint wird die gespeicherte JSON-Definition erneut gehasht. Stimmen die Werte nicht überein, wird der Snapshot als beschädigt erkannt.

### Ältere VEs
VE-Datensätze, die vor Einführung der Snapshot-Funktion gedruckt wurden, besitzen keinen historischen Layout-Snapshot. Partcounter weist diesen Fall ausdrücklich aus und verwendet als Fallback das aktuell aufgelöste Layout. Die historischen VE-Daten bleiben trotzdem unverändert.

[WICHTIG] Für revisionssichere Reprints sollte bei neuen VEs immer ein Snapshot vorhanden sein.

## [MACHINE-01] Maschinen / Modbus konfigurieren
Kategorie: Maschinen / Modbus
Abhängigkeiten: SAFETY-01
Folgewirkungen: ORDER-01, MODBUS-01, COMMISSION-01
Schlagwörter: Maschine, IP, Port, Unit-ID, Modbus, LOGO
Screenshot: 40_maschinen_modbus.png
Screenshot-Hinweis: Maschinen-/Modbus-Maske mit M01, IP, Port 502 und Unit-ID sichtbar aufnehmen.
---
### Maschinenidentität
Jede Station besitzt eine Partcounter-Maschinennummer, einen Anzeigenamen, IP-Adresse, TCP-Port und Modbus Unit-ID.

### Netzwerk
Für LOGO!-Stationen werden feste IP-Adressen empfohlen. DHCP kann zu unerwarteten Adresswechseln führen. Der Standard-Port ist TCP 502.

### Typischer Adressplan
Ein mögliches Schema ist PC `192.168.50.10` und M01 bis M30 `192.168.50.101` bis `192.168.50.130`. Der tatsächliche Anlagenstandard hat Vorrang.

### WLAN-Bridge
Die WLAN-Strecke sollte transparent arbeiten. AP-/Client-Isolation und unnötiges NAT zwischen PC und LOGO! können Modbus-Verbindungen verhindern.

## [MODBUS-01] Modbus V2 – Register, Handshake und Heartbeats
Kategorie: Maschinen / Modbus
Abhängigkeiten: MACHINE-01
Folgewirkungen: MODBUS-02, COMMISSION-02
Schlagwörter: ProtocolVersion, HR20, VW, Heartbeat, CommandSequence, AckSequence
Screenshot: 41_modbus_livewerte.png
Screenshot-Hinweis: Live-Diagnose mit Protocol/Heartbeat/Command-Ack/StatusWord aufnehmen.
---
### Protokollversion
Partcounter und LOGO! müssen dieselbe Protokollversion verwenden. R001.18/R001.19 basieren auf Modbus-Protokoll V2.

### CommandSequence / AckSequence
Jeder neue Befehl erhält eine Sequenznummer. Die LOGO! quittiert die verarbeitete Sequenz. So wird verhindert, dass derselbe Befehl bei Kommunikationswiederkehr mehrfach ausgeführt wird.

### Heartbeats
PC und LOGO! führen eigene Heartbeat-Zähler. Bleibt ein Wert unerwartet stehen, ist dies ein wichtiger Diagnosehinweis.

### Adressierung
NModbus verwendet nullbasierte Registeradressen. Die fachliche Dokumentation unterscheidet deshalb bewusst zwischen HR-Nummer und Client-Offset.

## [MODBUS-02] Kommunikationsdiagnose – ONLINE, OFFLINE und Fehlerzustände
Kategorie: Maschinen / Modbus
Abhängigkeiten: MODBUS-01
Folgewirkungen: COMMISSION-01, TROUBLE-03
Schlagwörter: offline, online, timeout, Diagnose, Ping, Fehlercode
Screenshot: 42_modbus_diagnose.png
Screenshot-Hinweis: Inbetriebnahme-Live-Diagnose mit Verbindung, StatusWord und ErrorCode aufnehmen.
---
### ONLINE
Eine Station gilt als online, wenn die laufende Fleet-Kommunikation erfolgreich Statusdaten lesen kann.

### OFFLINE
Typische Ursachen sind falsche IP/Port-Konfiguration, ausgeschaltete LOGO!, WLAN-/LAN-Unterbrechung, Firewall, AP-Isolation oder falsche Serverkonfiguration in LOGO!Soft Comfort.

### Direkte Leseprobe
Im Inbetriebnahmezentrum kann eine separate read-only Modbus-Leseprobe ausgeführt werden. Sie liest Statusdaten, ohne Auftragsparameter oder Ausgänge zu schreiben.

[PRAXIS] Wenn Ping funktioniert, Modbus aber nicht, Port, Modbus-Serververbindung, erlaubte Client-IP und Unit-ID prüfen.

## [LABEL-01] Etiketteneditor – Vorlagen anlegen und zuordnen
Kategorie: Etiketteneditor
Abhängigkeiten: ARTICLE-01
Folgewirkungen: LABEL-02, LABEL-03, LABEL-04, REPRINT-02
Schlagwörter: Etiketteneditor, Vorlage, WYSIWYG, Layout, Artikelzuordnung
Screenshot: 50_etiketteneditor_gesamt.png
Screenshot-Hinweis: Etiketteneditor vollständig mit Vorlagenliste, Zeichenfläche und Eigenschaftsbereich aufnehmen.
---
### Vorlagenprinzip
Partcounter verwaltet eine Standardvorlage und optional artikelbezogene Vorlagen. Für einen Druck wird zuerst eine explizit dem Artikel zugeordnete Vorlage gesucht; sonst die Standardvorlage.

### WYSIWYG
Position und Größe werden in Millimetern verwaltet. Die Vorschau entspricht dem vorgesehenen Etikettenformat und erlaubt eine visuelle Gestaltung ohne Quellcode.

### Elemente
Unterstützt werden statischer Text, Datenfelder, QR-Code, Code128, Rechteck, Linie und Bild.

### Speichern
Vorlagenänderungen wirken auf zukünftige Drucke. Bereits ab R001.18 mit Snapshot gedruckte VEs behalten für Reprints ihre historische Vorlagendefinition.

## [LABEL-02] Dynamische Datenfelder, QR-Code und Barcode
Kategorie: Etiketteneditor
Abhängigkeiten: LABEL-01
Folgewirkungen: LABEL-04
Schlagwörter: Token, VE_ID, QR, Code128, Datenfeld, Barcode
Screenshot: 51_etiketteneditor_token.png
Screenshot-Hinweis: Datenfeld-Auswahl oder Element mit Token {{ArticleNumber}} bzw. {{VE_ID}} aufnehmen.
---
### Daten-Tokens
Dynamische Felder werden mit Tokens wie `{{ArticleNumber}}`, `{{OrderNumber}}`, `{{ActualQuantity}}`, `{{CompletedAt}}` oder `{{VE_ID}}` definiert.

### QR-Nutzlast
Der Partcounter-QR enthält eine kompakte technische Nutzlast mit VE-ID, Maschine, Artikel, Werkzeug, Menge und Zeitstempel.

### Code128
Der Standard-Barcode kann die eindeutige VE-ID codieren. Dadurch kann eine VE über Scanner eindeutig referenziert werden.

[PRAXIS] Vorlagen nach Änderungen immer mit realistischen Testdaten und anschließend auf dem realen Drucker prüfen.

## [LABEL-03] Firmenlogo und Bilder in Etiketten verwenden
Kategorie: Etiketteneditor
Abhängigkeiten: LABEL-01
Folgewirkungen: REPRINT-02
Schlagwörter: Logo, Bild, PNG, JPG, Grafik, eingebettet
Screenshot: 52_etiketteneditor_bild.png
Screenshot-Hinweis: Etiketteneditor mit eingefügtem Firmenlogo und sichtbaren Bild-Eigenschaften aufnehmen.
---
### Bilddaten
Bilder und Logos werden direkt in der Vorlagendefinition eingebettet. Die Vorlage bleibt dadurch funktionsfähig, auch wenn die ursprüngliche Bilddatei später verschoben oder gelöscht wird.

### Seitenverhältnis
Für Logos sollte **Seitenverhältnis beibehalten** aktiviert bleiben. Eine zu kleine Quelldatei kann beim Druck unscharf erscheinen.

### Historischer Reprint
Da die Bilddaten Teil des Vorlagen-Snapshots sind, kann ein späterer Reprint ab R001.18 auch das damalige Logo reproduzieren.

## [LABEL-04] Etikett drucken, Testdruck und Druckfehler
Kategorie: Etiketteneditor
Abhängigkeiten: LABEL-01, PRINT-01
Folgewirkungen: HISTORY-01, TROUBLE-04
Schlagwörter: Drucken, Testetikett, Drucker, Queue, Windows
Screenshot: 53_testetikett.png
Screenshot-Hinweis: Einstellungen/Druck mit Druckername und Testetikett-Schaltfläche aufnehmen.
---
### Testdruck
Vor Produktionsfreigabe muss der konfigurierte Windows-Drucker über **Testetikett drucken** geprüft werden.

### Automatischer Druck
Ist Auto-Druck aktiviert, wird nach erfolgreichem VE-Abschluss ein Etikett erzeugt und an die Windows-Druckerwarteschlange übergeben. Der VE-Datensatz erhält den entsprechenden Druckstatus.

### Fehler
Bei fehlendem Druck zuerst Druckername, Windows-Queue, Treiber, Netzwerkdrucker-Erreichbarkeit und Etikettenformat prüfen.

## [COMMISSION-01] Inbetriebnahme / Diagnose – geführte Maschinenfreigabe
Kategorie: Inbetriebnahme
Abhängigkeiten: MACHINE-01, MODBUS-01, SAFETY-01
Folgewirkungen: COMMISSION-02, ROLLOUT-01
Schlagwörter: Inbetriebnahme, Prüfschritte, Freigabe, Hardwareprofil, Abnahme
Screenshot: 60_inbetriebnahme_gesamt.png
Screenshot-Hinweis: Inbetriebnahmezentrum mit Maschinenwahl und den drei Unterreitern aufnehmen.
---
### Ziel
Das Inbetriebnahmezentrum dokumentiert Hardwaredaten, Kommunikationswerte und definierte Prüfschritte je Maschine.

### Hardwareprofil
LOGO!-Bestellnummer, Versorgung, Zykluseingang, Ventilausgang, Koppelrelais, Endlagenüberwachung und Standard-Ventilimpuls können dokumentiert werden.

### Prüfablauf
Jeder Prüfschritt kann als bestanden, nicht bestanden, nicht anwendbar oder offen markiert werden. Zusätzlich steht ein Notizfeld zur Verfügung.

### Freigabestatus
Die Gesamtfreigabe bleibt eine bewusste Entscheidung. Messdaten ändern den Freigabestatus nicht automatisch.

## [COMMISSION-02] Live-Abnahme R001.16 – reale Modbus-Evidenz mitschneiden
Kategorie: Inbetriebnahme
Abhängigkeiten: COMMISSION-01, MODBUS-02
Folgewirkungen: ROLLOUT-01
Schlagwörter: Live-Abnahme, Messung, read-only, CSV, Evidence, Kommunikationsausfall
Screenshot: 61_live_abnahme.png
Screenshot-Hinweis: Live-Abnahme während laufender Messung mit mehreren Messzeilen aufnehmen.
---
### Read-only-Prinzip
Die Live-Abnahme beobachtet ausschließlich die bereits vorhandene Fleet-/Modbus-Diagnose. Sie erzeugt keinen zweiten Steuerweg und schaltet Q1 nicht direkt.

### Messgrößen
Aufgezeichnet werden unter anderem Verbindung, PC-/LOGO-Heartbeat, Command/Ack, StatusWord, ErrorCode, Gesamtzyklen, VE-Zähler und CompletionSequence.

### Ausfalltest
Bei einem kontrollierten WLAN-/PC-Ausfall kann die Messreihe Verbindungsabbruch und Wiederkehr dokumentieren. Die physische Bestätigung, dass die LOGO! lokal weiterzählt und einen fälligen Kistenwechsel ausführt, muss vor Ort erfolgen.

### Export
Abgeschlossene Messserien können als CSV exportiert und als Evidenz in Prüfnotizen übernommen werden. Bestanden/Nicht bestanden wird nicht automatisch gesetzt.

## [ROLLOUT-01] Rolloutstatus für 30 Maschinen
Kategorie: Inbetriebnahme
Abhängigkeiten: COMMISSION-01
Folgewirkungen: -
Schlagwörter: Rollout, 30 Maschinen, Freigabestatus, Übersicht
Screenshot: 62_rollout_30.png
Screenshot-Hinweis: Rolloutstatus mit mehreren Maschinen und unterschiedlichen Freigabeständen aufnehmen.
---
### Zweck
Die Rolloutübersicht zeigt auf einen Blick, welche Stationen noch nicht geprüft, in Prüfung, mit Auflagen freigegeben, freigegeben oder gesperrt sind.

### Vorgehen
Eine Referenzmaschine vollständig validieren und erst danach weitere Maschinen nach demselben technischen Standard ausrollen. Abweichende Hardware ist je Station zu dokumentieren.

## [ALS-01] ARBURG ALS – Übersicht und Datenfluss
Kategorie: ARBURG ALS
Abhängigkeiten: ARTICLE-01, MACHINE-01
Folgewirkungen: ALS-02, ALS-03, ALS-04
Schlagwörter: ARBURG, ALS, Leitrechner, Auftrag, Schnittstelle, Import
Screenshot: 70_als_auftraege.png
Screenshot-Hinweis: ARBURG-ALS-Reiter 'ALS-Aufträge' mit geladener Auftragsliste aufnehmen.
---
### Aufgabe der Schnittstelle
Partcounter kann freigegebene Auftragsdaten aus einem ALS-Dateiexport oder einem kundenspezifisch bereitgestellten REST/JSON-Endpunkt übernehmen.

### Kontrollierter Import
Standardmäßig werden importierte Daten nur in die normale Partcounter-Auftragsmaske übertragen. Der Bediener prüft Maschine, Artikel, Auftrag und Menge und startet danach bewusst.

### Keine erfundenen Endpunkte
REST-URL, Authentifizierung und JSON-Struktur müssen aus der konkreten ALS-Installation bzw. vom zuständigen Administrator stammen.

## [ALS-02] ALS-Verbindung – Datei/Hotfolder und REST/JSON
Kategorie: ARBURG ALS
Abhängigkeiten: ALS-01
Folgewirkungen: ALS-03, ALS-05
Schlagwörter: XLSX, CSV, TSV, Hotfolder, REST, JSON, Authentifizierung
Screenshot: 71_als_verbindung.png
Screenshot-Hinweis: ALS-Reiter 'Verbindung / Quelle' im Datei-Modus mit Pfad, Muster und Kopfzeile aufnehmen.
---
### Datei-Modus
Der Pfad kann auf eine konkrete XLSX/CSV/TSV-Datei oder einen Hotfolder zeigen. Bei einem Ordner wird die neueste passende Datei anhand des Dateimusters gewählt.

### REST-Modus
Konfigurierbar sind GET/POST, Authentifizierung, Timeout, JSON-Wurzelpfad, Zusatzheader, Request-Body und optional Client-Zertifikat.

### Zugangsdaten
Passwörter, Token, API-Key-Werte und Zertifikatpasswörter werden geschützt gespeichert.

[WARNUNG] Die Option für nicht vertrauenswürdige TLS-Zertifikate nur kurzfristig zum Test verwenden.

## [ALS-03] ALS-Feldmapping richtig konfigurieren
Kategorie: ARBURG ALS
Abhängigkeiten: ALS-02
Folgewirkungen: ALS-04, ALS-05
Schlagwörter: Feldmapping, Required, Pflicht, SourceField, JSON-Pfad, Spaltenname
Screenshot: 72_als_feldmapping.png
Screenshot-Hinweis: Feldmapping-Tabelle vollständig aufnehmen; Pflichtspalte, Zielfeld, Quellfeld, Bedeutung und Beispiel sichtbar.
---
### Grundprinzip
Partcounter kennt feste Zielfelder. Die tatsächlichen ALS-Spaltennamen oder JSON-Pfade sind installationsabhängig und werden deshalb frei zugeordnet.

### Schreibbare und schreibgeschützte Spalten
Nur **ALS Quellspalte / JSON-Pfad** ist editierbar. `Pflicht`, `Partcounter-Zielfeld`, `Bedeutung` und `Beispiel` sind reine Anzeigeinformationen.

### Pflichtfelder
OrderNumber, ArticleNumber und OrderQuantity müssen auf reale Quelldaten zeigen. Zusätzlich ist eine eindeutige Maschinenzuordnung erforderlich.

### R001.18 Hotfix
Die schreibgeschützten Spalten verwenden ausdrücklich OneWay-Bindungen. Dadurch wird die frühere WPF-Exception `TwoWay- oder OneWayToSource-Bindungen ... Required` verhindert.

## [ALS-04] ALS-Maschinenalias und eindeutige Zuordnung
Kategorie: ARBURG ALS
Abhängigkeiten: ALS-03, MACHINE-01
Folgewirkungen: ALS-05
Schlagwörter: Alias, MachineExternalId, ARB-0470-07, Maschine
Screenshot: 73_als_alias.png
Screenshot-Hinweis: Maschinen-Alias-Feld mit mindestens zwei Beispielzuordnungen aufnehmen.
---
### Warum Alias?
ALS kann Maschinen mit eigenen Namen oder externen IDs liefern. Partcounter arbeitet intern mit M01 bis M30.

### Beispiel
`ARB-0470-07=7` oder `ALLROUNDER_07=M07` ordnet externe Bezeichnungen der Partcounter-Maschine 07 zu.

### Reihenfolge
Partcounter versucht zuerst eine direkte Maschinennummer, danach Alias über externe ID/Name und anschließend exakten Namensvergleich.

## [ALS-05] ALS-Fehlersuche
Kategorie: ARBURG ALS
Abhängigkeiten: ALS-02, ALS-03, ALS-04
Folgewirkungen: TROUBLE-01
Schlagwörter: ALS Fehler, 401, 403, Mapping, Datei nicht gefunden, Required
Screenshot: 74_als_fehlerdiagnose.png
Screenshot-Hinweis: Optional einen kontrollierten ALS-Fehler oder Statushinweis aufnehmen; keine geheimen Zugangsdaten zeigen.
---
### Datei wird nicht gefunden
Pfad, Dateimuster, Blattname, Kopfzeile und Windows-Berechtigungen prüfen.

### Null Aufträge
Pflichtmapping, Zahlen-/Datumsformat und Maschinenzuordnung kontrollieren.

### REST 401/403
Servicekonto, Token/API-Key, Header und Berechtigungen prüfen.

### Zertifikatfehler
CA-Vertrauenskette und gegebenenfalls Client-Zertifikat prüfen. Unsichere TLS-Option nicht als Dauerlösung verwenden.

### WPF-Fehler 'Required'
Dieser Fehler wurde mit R001.18 behoben. Falls er erneut erscheint, sicherstellen, dass tatsächlich R001.18 oder neuer gestartet wurde.

## [PRINT-01] Drucker konfigurieren und Testetikett ausgeben
Kategorie: Einstellungen
Abhängigkeiten: ADMIN-01
Folgewirkungen: LABEL-04, REPRINT-01
Schlagwörter: Drucker, Windows, AutoPrint, Testetikett, Einstellungen
Screenshot: 80_druckeinstellungen.png
Screenshot-Hinweis: Einstellungen/Druck mit Druckername, Auto-Druck und Testetikett aufnehmen.
---
### Druckername
Partcounter verwendet den in Windows registrierten Namen der Druckerwarteschlange. Schreibweise muss zum installierten Queue-Namen passen.

### Auto-Druck
Ist die automatische Etikettenausgabe aktiviert, druckt Partcounter nach protokolliertem VE-Abschluss automatisch.

### Testetikett
Nach Druckertreiber-, Windows- oder Layoutänderungen immer ein Testetikett ausgeben.

## [SETTINGS-01] Einstellungen, Firmenlogo und geschützte Systemoptionen
Kategorie: Einstellungen
Abhängigkeiten: ADMIN-01
Folgewirkungen: UPDATE-01, BACKUP-01, PRINT-01
Schlagwörter: Einstellungen, Firmenlogo, Branding, Druck, geschützt
Screenshot: 81_einstellungen_gesamt.png
Screenshot-Hinweis: Einstellungen/Druck nach unten gescrollt, sodass Branding, Update und Produktionsbereitschaft erkennbar sind.
---
### Bereich
Die Einstellungsseite bündelt Drucker, Firmenbranding, Updatepfad, Datensicherung und Diagnosefunktionen.

### Firmenlogo
Das konfigurierte Firmenlogo wird unter anderem im Leitstand verwendet. Bilddateien sollten in geeigneter Auflösung vorliegen.

### Scrollen
Die Seite ist vertikal scrollbar, da mehrere administrative Module dynamisch ergänzt werden.

## [UPDATE-01] Partcounter-Update installieren
Kategorie: Einstellungen
Abhängigkeiten: SETTINGS-01, BACKUP-01
Folgewirkungen: TROUBLE-02
Schlagwörter: Update, Netzwerk, USB, ZIP, SHA-256, Version
Screenshot: 82_updatecenter.png
Screenshot-Hinweis: Software-Update-Panel mit Netzwerkpfad und Schaltflächen aufnehmen.
---
### Quellen
Updates können aus einem Netzwerkordner, von USB oder aus einer lokalen ZIP-Datei eingespielt werden.

### Prüfung
Partcounter prüft Manifest, Version und SHA-256-Dateiprüfsummen des Updatepakets, bevor eine Installation vorbereitet wird.

### Daten
Produktionsdaten unter `%LOCALAPPDATA%\Partcounter` bleiben bei einem regulären Programmupdate erhalten.

[PRAXIS] Vor größeren Updates eine aktuelle Datenbanksicherung kontrollieren.

## [BACKUP-01] Datensicherung, Datenbankprüfung und Diagnosepaket
Kategorie: Einstellungen
Abhängigkeiten: SETTINGS-01
Folgewirkungen: TROUBLE-01, UPDATE-01
Schlagwörter: Backup, SQLite, quick_check, Diagnosepaket, WAL
Screenshot: 83_backup_diagnose.png
Screenshot-Hinweis: Panel 'Datensicherung & Produktionsbereitschaft' vollständig aufnehmen.
---
### Automatische Sicherung
Partcounter erstellt automatisch eine tägliche konsistente SQLite-Sicherung und begrenzt die Zahl gespeicherter Generationen.

### Manuelle Sicherung
Vor Wartung, Update oder größeren Konfigurationsänderungen kann eine Sicherung bewusst ausgelöst werden.

### Datenbankprüfung
`quick_check` und Fremdschlüsselprüfung helfen, strukturelle Datenbankprobleme frühzeitig zu erkennen.

### Diagnosepaket
Das Diagnosepaket enthält technische System- und Ereignisinformationen für Supportzwecke, exportiert aber bewusst keine vollständige Settings-Tabelle und keine Datenbanksicherung.

## [TROUBLE-01] Allgemeine Fehlersuche – methodisches Vorgehen
Kategorie: Fehlersuche
Abhängigkeiten: BACKUP-01
Folgewirkungen: TROUBLE-02, TROUBLE-03, TROUBLE-04, ALS-05
Schlagwörter: Fehler, Diagnose, Troubleshooting, Log, Statusleiste
Screenshot: 90_fehlerdialog.png
Screenshot-Hinweis: Allgemeiner Partcounter-Fehlerdialog ohne sensible Daten aufnehmen.
---
### Reihenfolge
1. Sichtbare Fehlermeldung vollständig notieren.
2. Betroffenen Bereich und zuletzt ausgeführte Aktion bestimmen.
3. Statusleiste lesen.
4. Bei Kommunikationsfehlern Maschinenstatus und Inbetriebnahme-Diagnose prüfen.
5. Bei Startfehlern `Partcounter_startup.log` prüfen.
6. Bei unklaren Problemen Diagnosepaket erzeugen.

### Keine Datenblindflüge
Nicht gleichzeitig mehrere Einstellungen ändern. Änderungen einzeln durchführen und Wirkung dokumentieren.

## [TROUBLE-02] Programmstart und unerwartete Softwarefehler
Kategorie: Fehlersuche
Abhängigkeiten: TROUBLE-01
Folgewirkungen: -
Schlagwörter: Startfehler, Exception, startup.log, Absturz
Screenshot: 91_startfehler.png
Screenshot-Hinweis: Startfehlerdialog oder Hinweis auf startup.log aufnehmen; personenbezogene Windows-Pfade bei Handbuchbildern anonymisieren.
---
### Startprotokoll
Bei Startproblemen schreibt Partcounter nach `%LOCALAPPDATA%\Partcounter\Partcounter_startup.log`.

### Unerwarteter Fehler
Die globale Fehlerbehandlung protokolliert ungefangene Dispatcher-, AppDomain- und Task-Fehler. Der normale Programmstart soll durch reine Hilfe-/Update-/Branding-Zusatzfunktionen nicht blockiert werden.

### Vorgehen
Version prüfen, Fehlertext sichern, Diagnosepaket erzeugen und zuletzt geänderte Konfiguration nachvollziehen.

## [TROUBLE-03] Netzwerk- und Modbusfehler systematisch eingrenzen
Kategorie: Fehlersuche
Abhängigkeiten: MODBUS-02
Folgewirkungen: COMMISSION-02
Schlagwörter: Netzwerk, Ping, Port 502, Firewall, WLAN, AP Isolation
Screenshot: 92_netzwerk_diagnose.png
Screenshot-Hinweis: Maschinen-/Modbus-Konfiguration und passende Live-Diagnose nebeneinander bzw. als zwei spätere Screenshots aufnehmen.
---
### Prüfkette
1. Ist die LOGO! in RUN?
2. Stimmt die statische IP?
3. Ist Ping möglich?
4. Ist TCP 502 erreichbar?
5. Ist der Modbus-Server in LOGO!Soft Comfort eingerichtet?
6. Darf die Partcounter-PC-IP zugreifen?
7. Stimmen Unit-ID und Registermapping?
8. Ist AP-/Client-Isolation deaktiviert?

### WLAN-Ausfall
Kurze Netzwerkunterbrechungen dürfen keine Maschinenzyklen verlieren, weil die zeitkritische Zählung lokal in der LOGO! erfolgt.

## [TROUBLE-04] Etikettendruck und Nachdruck – typische Fehler
Kategorie: Fehlersuche
Abhängigkeiten: LABEL-04, REPRINT-02
Folgewirkungen: -
Schlagwörter: Etikett fehlt, Reprint, Snapshot beschädigt, Druckerfehler
Screenshot: 93_reprint_fehler.png
Screenshot-Hinweis: Falls möglich kontrollierten Reprint-Fehler mit nicht erreichbarem Testdrucker aufnehmen; keine produktive Queue stören.
---
### Originaldruck fehlt
Druckername, Queue, Auto-Druck-Einstellung und Windows-Druckstatus prüfen.

### Reprint gesperrt
Die VE muss bereits als gedruckt protokolliert sein. Für nie gedruckte Datensätze ist Reprint absichtlich deaktiviert.

### Snapshot beschädigt
Wenn SHA-256 nicht zur gespeicherten Snapshot-Definition passt, wird der historische Snapshot verworfen. Der Vorgang muss geprüft werden; ein stiller Druck mit manipuliertem Layout ist nicht vorgesehen.

### Ältere VE ohne Snapshot
Dies ist kein Datenfehler. Partcounter kennzeichnet den Fallback auf das aktuelle Layout ausdrücklich im Journal.
