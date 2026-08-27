# Partcounter R001.14 – integrierte Hilfe

Diese Datei ist die fachliche Quelle der in Partcounter eingebauten Hilfefunktion. Jeder Abschnitt beschreibt eine konkrete Programmfunktion, ihre Voraussetzungen, Abhängigkeiten und Folgewirkungen.

## [system-overview] Systemüberblick und Datenfluss
Kategorie: Grundlagen
Abhängigkeiten: -
Folgewirkungen: leitstand, article-master, modbus-v2, ve-history, label-printing, update-center
Schlagwörter: Architektur, Datenfluss, PC, LOGO, Modbus, VE
---
Partcounter besteht aus einer PC-Anwendung und je Maschine einer Siemens LOGO!. Der PC übernimmt Bedienung, Auftragsparameter, Visualisierung, Historie, Etiketten, Stammdaten, Diagnose und Administration. Die LOGO! zählt den realen Maschinenzyklus lokal und löst den Verpackungswechsel lokal aus.

Der zentrale Datenfluss lautet:

Artikelstamm → Auftrag im Leitstand → Jobparameter → Modbus HR1–HR12 → LOGO! → I1-Zyklusimpulse → lokale Zykluszählung → VE-Abschluss → Modbus HR20–HR37 → Leitstand → VE-Historie → Etikettenvorlage → Druck.

Die lokale Zählung in der LOGO! ist bewusst von der PC-Verfügbarkeit entkoppelt. Ein kurzzeitiger PC-, LAN- oder WLAN-Ausfall darf keinen realen Maschinenzyklus verlieren und darf einen fälligen VE-Wechsel nicht verhindern. Nach Wiederverbindung synchronisiert Partcounter die Anzeige aus den LOGO!-Statusregistern.

## [startup] Programmstart und Initialisierung
Kategorie: Grundlagen
Abhängigkeiten: system-overview, data-storage
Folgewirkungen: leitstand, admin-access, company-branding
Schlagwörter: Start, Initialisierung, Datenbank, Fehlerlog
---
Beim Start initialisiert Partcounter zuerst die lokale SQLite-Datenbank, lädt Maschinen, Artikel, Einstellungen und die letzten VE-Datensätze. Danach werden die Kommunikations-, Inbetriebnahme-, ALS-, Etiketten- und Branding-Komponenten vorbereitet.

Der normale Produktionsbetrieb benötigt keine Anmeldung. Nach jedem Programmstart ist lediglich die Administration gesperrt. Leitstand, Artikelstamm, VE-Historie, Hilfe und Über bleiben frei zugänglich.

Kann das Hauptfenster nicht korrekt gestartet werden, schreibt Partcounter eine Diagnose nach %LOCALAPPDATA%\Partcounter\Partcounter_startup.log. Ein Fehler in einer Zusatzfunktion soll den regulären Produktionsbetrieb möglichst nicht blockieren.

## [leitstand] Leitstand – zentrale Produktionsansicht
Kategorie: Leitstand
Abhängigkeiten: article-master, machine-selection, system-overview
Folgewirkungen: order-start, order-pause, order-resume, order-end, fill-level, ve-history
Schlagwörter: Leitstand, Produktion, Maschine, Auftrag
---
Der Leitstand ist die zentrale Bedienoberfläche für den regulären Betrieb. Hier werden Maschine, Artikel, Auftragsnummer, Auftragsmenge und der aktuelle VE-Zustand zusammengeführt.

Die Maschinenkacheln zeigen Verbindungsstatus, Auftrag, Artikel, Werkzeug, aktive Kavitäten, aktuelle VE, Füllgrad, verbleibende Zyklen, zyklusbedingte Mehrmenge, Auftragsfortschritt, aktuelle VE-Nummer und Anzahl abgeschlossener VEs.

Der Leitstand hängt fachlich vom Artikelstamm und technisch im Echtbetrieb von der Maschinen-/Modbus-Konfiguration ab. Änderungen an Modbus oder LOGO! erfolgen jedoch ausschließlich im Admin-Bereich.

## [machine-selection] Maschine auswählen
Kategorie: Leitstand
Abhängigkeiten: leitstand, machine-modbus
Folgewirkungen: order-start, order-pause, order-resume, order-end, temporary-disable
Schlagwörter: Maschine, Auswahl
---
Die Maschinenauswahl legt fest, auf welche Station die nachfolgenden Auftrags- und Bedienbefehle wirken. Im Echtbetrieb verweist jede Maschine auf eine feste IP-Adresse, Port 502 und eine Modbus Unit-ID.

Vor dem Start eines Auftrags muss die richtige Maschine ausgewählt sein. Ein falscher Maschinenbezug führt im Echtbetrieb dazu, dass die Jobparameter an die falsche LOGO!-Station übertragen würden. Deshalb müssen Maschinenname, Maschinennummer und IP-Adresse bei der Inbetriebnahme eindeutig validiert werden.

## [article-selection] Artikel im Leitstand auswählen
Kategorie: Leitstand
Abhängigkeiten: article-master
Folgewirkungen: order-start, fill-calculation, label-printing
Schlagwörter: Artikel, Werkzeug, Kavitäten, VE
---
Die Artikelauswahl übernimmt Werkzeugnummer, aktive Kavitäten und Standard-VE-Menge aus dem Artikelstamm. Diese Daten bilden die Grundlage für die Berechnung der Zielzyklen je VE.

Die Kavitätenzahl ist besonders kritisch: Partcounter überträgt sie an die LOGO! und verwendet sie später, um aus den von der LOGO! gelieferten VE-Zyklen die tatsächliche Stückzahl zu berechnen. Eine falsche Kavitätenzahl verfälscht deshalb Füllgrad, Istmenge, Mehrmenge, Historie und Etikett.

## [order-number] Auftragsnummer
Kategorie: Leitstand
Abhängigkeiten: leitstand
Folgewirkungen: order-start, ve-history, label-printing
Schlagwörter: Auftrag, Auftragsnummer
---
Die Auftragsnummer dient der Rückverfolgbarkeit. Sie wird beim Start des Auftrags in den Maschinenzustand übernommen und bei jeder abgeschlossenen VE in der Historie und auf dem Etikett gespeichert.

Ist keine Auftragsnummer eingegeben, erzeugt Partcounter beim Start eine zeitbezogene Ersatznummer. Für den realen Produktionsbetrieb sollte nach Möglichkeit die betriebliche Auftragsnummer verwendet werden.

## [order-quantity] Auftragsmenge
Kategorie: Leitstand
Abhängigkeiten: article-selection
Folgewirkungen: order-start, last-partial-ve, order-progress
Schlagwörter: Auftragsmenge, Restmenge, Teil-VE
---
Die Auftragsmenge beschreibt die gesamte Sollstückzahl des laufenden Produktionsauftrags. Sie ist unabhängig von der Standard-VE-Menge des Artikels.

Partcounter berechnet daraus die Anzahl benötigter Verpackungseinheiten und verkleinert die letzte VE automatisch auf die verbleibende Restmenge. Auch diese letzte VE wird auf vollständige Werkzeugzyklen aufgerundet.

## [valve-pulse] Ventilimpuls im Auftrag
Kategorie: Leitstand
Abhängigkeiten: order-start, logo-control
Folgewirkungen: ve-completion
Schlagwörter: Ventil, Pneumatik, Impuls, Q1
---
Der Ventilimpuls legt fest, wie lange Q1 der LOGO! beim VE-Wechsel angesteuert wird. Partcounter erlaubt 50 bis 5000 ms in 10-ms-Schritten. Intern wird der Wert für Protocol V2 in 10-ms-Einheiten an HR7 übertragen.

Beispiel: 750 ms werden als Wert 75 übertragen. Die LOGO! verwendet diesen Wert für den lokalen Impulsbaustein. Mechanik, Ventil und Koppelrelais müssen für diese Bewegung separat validiert sein; Partcounter ist keine Sicherheitssteuerung.

## [order-start] Auftrag starten
Kategorie: Leitstand
Abhängigkeiten: machine-selection, article-selection, order-number, order-quantity, valve-pulse
Folgewirkungen: modbus-job-write, fill-level, order-progress
Schlagwörter: Auftrag starten, Job, Modbus
---
Beim Start prüft Partcounter Maschine, Artikel, Kavitäten, VE-Menge, Auftragsmenge und Ventilimpuls. Im Echtbetrieb wird anschließend ein Jobtelegramm an die ausgewählte LOGO! übertragen.

Erst wenn die Übertragung erfolgreich war, wird der Auftrag auf PC-Seite als laufend gesetzt. Dadurch soll verhindert werden, dass der Leitstand einen gestarteten Auftrag anzeigt, obwohl die LOGO! die Parameter nicht erhalten hat.

Bei einem neuen Auftrag wird CommandResetJob gesetzt. Die LOGO! setzt dadurch VE-Zähler, Gesamtzykluszähler und Auftragszähler auf den definierten Startzustand und bestätigt die CommandSequence über AckSequence.

## [order-pause] Auftrag pausieren
Kategorie: Leitstand
Abhängigkeiten: order-start, command-sequence
Folgewirkungen: order-resume
Schlagwörter: Pause, Zählung
---
Pausieren stoppt die Freigabe neuer Zählimpulse in der LOGO!, ohne Kommunikation und Statusübertragung abzuschalten. Der laufende VE-Zähler wird nicht gelöscht.

Das ist wichtig für Unterbrechungen, bei denen der Auftrag später fortgesetzt werden soll. Beim Pausieren wird eine neue CommandSequence mit gesetztem Pause-Bit übertragen. Die LOGO! bestätigt diesen Befehl über AckSequence.

## [order-resume] Auftrag fortsetzen
Kategorie: Leitstand
Abhängigkeiten: order-pause, command-sequence
Folgewirkungen: fill-level
Schlagwörter: Fortsetzen, Resume
---
Fortsetzen hebt das Pause-Latch der LOGO! auf. Die vorhandenen VE- und Auftragszähler bleiben erhalten.

Die Zyklusflanke wird im LOGO!-Programm vor der Freigabelogik erkannt. Dadurch darf beim Fortsetzen kein künstlicher Zyklus entstehen, wenn I1 während der Pause noch HIGH ist. Gezählt wird erst wieder eine echte neue LOW-HIGH-Flanke.

## [order-end] Auftrag beenden
Kategorie: Leitstand
Abhängigkeiten: order-start
Folgewirkungen: ve-history
Schlagwörter: Auftrag beenden
---
Auftrag beenden beendet den Produktionsauftrag auf PC-Seite kontrolliert. Im Echtbetrieb wird die Zählung zuvor pausiert, damit keine weiteren Produktionszyklen versehentlich diesem Auftrag zugerechnet werden.

Eine bereits abgeschlossene VE bleibt in der Historie erhalten. Ein neuer Auftrag verwendet anschließend wieder CommandResetJob und startet mit einem definierten Zählerzustand.

## [temporary-disable] Maschine temporär deaktivieren
Kategorie: Leitstand
Abhängigkeiten: machine-selection, order-pause
Folgewirkungen: visible-machines, mini-monitor
Schlagwörter: deaktivieren, Polling, Maschine
---
Eine temporär deaktivierte Maschine wird aus den aktiven Leitstands- und Mini-Monitor-Ansichten entfernt. Im Echtbetrieb wird das Polling zu dieser Station beendet.

Läuft beim Deaktivieren ein Auftrag, wird die Zählung zuerst pausiert. Nach Reaktivierung wird die Kommunikation wieder aufgenommen, ein pausierter Auftrag aber nicht automatisch fortgesetzt. Das erfordert eine bewusste Bedienaktion.

## [visible-machines] Maschinen ohne Auftrag anzeigen
Kategorie: Leitstand
Abhängigkeiten: leitstand
Folgewirkungen: mini-monitor
Schlagwörter: Filter, Maschinen, sichtbar
---
Standardmäßig zeigt Partcounter im Leitstand nur Maschinen mit relevantem Produktionszustand. Über „Maschinen ohne Auftrag anzeigen“ können auch Leerlaufstationen eingeblendet werden.

Die Funktion ändert keine Maschinenkonfiguration und keine Kommunikation; sie beeinflusst nur die Darstellung.

## [fill-level] Füllgrad und Ampelstufen
Kategorie: Leitstand
Abhängigkeiten: current-ve-cycles, article-selection
Folgewirkungen: ve-attention
Schlagwörter: Füllgrad, Ampel, 80, 95, voll
---
Der Füllgrad wird aus aktueller Istmenge und aktuellem VE-Soll berechnet. Die Istmenge entsteht aus CurrentVECycles × ActiveCavitiesEcho.

Die Darstellung verwendet Stufen: normal bis 79 %, Vorwarnung ab 80 %, kritisch ab 95 % und VE voll beim abgeschlossenen Wechsel. Die Ampel ist eine Visualisierung; der eigentliche automatische VE-Abschluss wird lokal von der LOGO! anhand der Zielzyklen ausgelöst.

## [ve-attention] VE-voll-Aufmerksamkeit und Fokus
Kategorie: Leitstand
Abhängigkeiten: ve-completion, fill-level
Folgewirkungen: mini-monitor
Schlagwörter: VE voll, Fokus, Aufmerksamkeit
---
Bei einer neu abgeschlossenen VE hebt Partcounter die betroffene Maschine optisch hervor. Ist das Hauptfenster geöffnet, kann die Kachel in den sichtbaren Bereich geführt werden. Ist Partcounter minimiert, übernimmt der Mini-Monitor die Aufmerksamkeit.

Der Trigger basiert nicht nur auf einem Füllgradwert, sondern auf der CompletionSequence der LOGO!. Dadurch kann jede neue abgeschlossene VE eindeutig erkannt werden.

## [manual-ve-change] Manueller VE-Wechsel
Kategorie: Leitstand
Abhängigkeiten: command-sequence, ve-completion
Folgewirkungen: ve-history, label-printing
Schlagwörter: manueller Wechsel, VE
---
Der manuelle VE-Wechsel löst denselben Abschlussablauf wie ein automatischer Wechsel aus, trägt aber den Abschlussgrund „Manuell“.

Die LOGO! ignoriert einen manuellen Wechsel bei leerer aktueller VE. Der Befehl wird als One-Shot über eine neue CommandSequence übertragen, damit ein dauerhaft gesetztes Bit nicht mehrfach auslöst.

## [reset-machine] Reset Maschine
Kategorie: Leitstand
Abhängigkeiten: command-sequence
Folgewirkungen: current-ve-cycles
Schlagwörter: Reset, Zähler
---
Reset setzt den definierten Auftrags-/Zählerzustand der ausgewählten Maschine zurück. Im Echtbetrieb ist dies ein aktiver Steuerbefehl an die LOGO! und darf nicht mit dem bloßen Schließen eines Auftrags verwechselt werden.

Vor einem Reset muss geklärt sein, ob bestehende Zählerstände noch für Rückverfolgbarkeit oder Historie benötigt werden.

## [simulation] Simulation und Simulationszyklus
Kategorie: Betrieb
Abhängigkeiten: admin-access
Folgewirkungen: leitstand
Schlagwörter: Simulation, Test, + Zyklus
---
Im Simulationsmodus werden keine Modbus-Schreibbefehle an reale LOGO!-Stationen gesendet. Die Maschinenzustände werden in der PC-Anwendung simuliert.

„+ Zyklus (Sim)“ erzeugt ausschließlich in diesem Modus einen manuellen Simulationszyklus. Der Wechsel Simulation ↔ Echtbetrieb ist admin-geschützt, weil dadurch die reale Maschinenkommunikation aktiviert oder deaktiviert wird.

## [mini-monitor] Always-on-Top Mini-Monitor
Kategorie: Leitstand
Abhängigkeiten: visible-machines, ve-attention
Folgewirkungen: leitstand
Schlagwörter: Mini Monitor, minimiert, Always on Top
---
Wird das Hauptfenster minimiert, erscheint ein kompakter Always-on-Top-Monitor für die aktiven Maschinen. Er übernimmt insbesondere die Sichtbarkeit von VE-Füllständen und Aufmerksamkeitssignalen.

Beim Wiederherstellen des Hauptfensters wird der Mini-Monitor ausgeblendet. Ein Klick auf eine betroffene Maschine kann den Leitstand wiederherstellen und die Maschine fokussieren.

## [article-master] Artikelstamm
Kategorie: Stammdaten
Abhängigkeiten: data-storage
Folgewirkungen: article-selection, fill-calculation, label-printing
Schlagwörter: Artikelstamm, Werkzeug, Kavitäten, VE-Menge
---
Der Artikelstamm enthält Artikelnummer, Bezeichnung, Werkzeugnummer, aktive Kavitäten und Standardstückzahl je Verpackungseinheit.

Diese Daten sind produktionswirksam. Insbesondere Kavitätenzahl und VE-Menge bestimmen die Zielzyklen und damit den automatischen Verpackungswechsel. Änderungen werden in SQLite gespeichert und stehen danach im Leitstand zur Auswahl.

## [article-save] Artikel speichern/aktualisieren
Kategorie: Stammdaten
Abhängigkeiten: article-master
Folgewirkungen: article-selection
Schlagwörter: Artikel speichern
---
„Artikel speichern“ legt einen neuen Artikel an oder aktualisiert einen vorhandenen Artikel mit gleicher Artikelnummer. Partcounter validiert Kavitäten 1–64 und VE-Menge > 0.

Ein bereits laufender Auftrag übernimmt spätere Stammdatenänderungen nicht automatisch. Für neue Produktionsaufträge wird der aktualisierte Artikelstamm verwendet.

## [fill-calculation] Zyklen, effektive VE und Mehrmenge
Kategorie: Stammdaten
Abhängigkeiten: article-master
Folgewirkungen: order-start, fill-level, last-partial-ve
Schlagwörter: Berechnung, Aufrundung, Kavitäten
---
Werkzeuge werfen pro Maschinenzyklus eine feste Anzahl aktiver Kavitäten aus. Daher kann nicht jede gewünschte VE-Stückzahl exakt erreicht werden.

Zielzyklen = ceil(VE-Soll / aktive Kavitäten).
Effektive VE-Menge = Zielzyklen × aktive Kavitäten.
Mehrmenge = effektive VE-Menge – VE-Soll.

Beispiel: 1000 Sollteile bei 64 Kavitäten ergeben 16 Zyklen und damit 1024 tatsächliche Teile.

## [last-partial-ve] Automatische letzte Teil-VE
Kategorie: Auftragslogik
Abhängigkeiten: order-quantity, fill-calculation, ve-completion
Folgewirkungen: modbus-job-write
Schlagwörter: Restmenge, letzte VE, Teil-VE
---
Wenn die verbleibende Auftragsmenge kleiner als die Standard-VE ist, berechnet Partcounter eine neue Zielmenge für die letzte VE. Diese Restmenge wird erneut auf ganze Werkzeugzyklen aufgerundet.

Die neuen VE-Parameter werden mit neuer CommandSequence, aber ohne CommandResetJob übertragen. Dadurch bleiben TotalCycles, CompletedVEs und CurrentVENumber erhalten. Das Parameterupdate darf erst erfolgen, wenn die vorherige VE abgeschlossen und der aktuelle VE-Zähler wieder 0 ist.

## [ve-history] VE-Historie
Kategorie: Rückverfolgbarkeit
Abhängigkeiten: ve-completion, data-storage
Folgewirkungen: label-printing
Schlagwörter: Historie, VE-ID, Rückverfolgbarkeit
---
Jede abgeschlossene Verpackungseinheit wird mit eindeutiger VE-ID in SQLite gespeichert. Enthalten sind Maschine, VE-Nummer, Auftrag, Artikel, Werkzeug, Kavitäten, Sollmenge, Istmenge, Mehrmenge, Abschlussgrund, Zeitpunkt und Etikettenstatus.

Die Historie wird erst bei einem tatsächlich erkannten VE-Abschluss erzeugt. Der robuste Trigger ist die Änderung der CompletionSequence der LOGO!.

## [label-printing] Automatischer Etikettendruck
Kategorie: Etiketten
Abhängigkeiten: ve-history, label-template-resolution, printer-settings
Folgewirkungen: label-status
Schlagwörter: Etikett, Druck, automatisch
---
Nach einem VE-Abschluss wird zuerst der VE-Datensatz gespeichert und anschließend – falls aktiviert – der Etikettendruck ausgelöst. Dieses Write-first-Prinzip erhält die Rückverfolgbarkeit auch bei Druckerfehlern.

Der Druck verwendet dieselbe Rendering-Engine wie die Vorschau des Etiketteneditors. Damit stimmen Positionen, Texte, Barcodes, QR-Codes und Bilder zwischen Editor und Produktionsdruck überein.

## [label-template-resolution] Auswahl der Etikettenvorlage
Kategorie: Etiketten
Abhängigkeiten: label-designer, article-master
Folgewirkungen: label-printing
Schlagwörter: Vorlage, Standard, Artikelzuordnung
---
Partcounter sucht zuerst nach einer Vorlage, die explizit dem aktuellen Artikel zugeordnet ist. Existiert keine solche Vorlage, wird die als Standard markierte Vorlage verwendet. Fehlt auch diese, wird die erste gültige Vorlage als Fallback genutzt.

Damit können einzelne Artikel eigene Layouts besitzen, ohne dass für jeden Artikel zwingend eine separate Vorlage angelegt werden muss.

## [label-designer] Etiketteneditor
Kategorie: Etiketten
Abhängigkeiten: admin-access, label-template-resolution
Folgewirkungen: label-printing
Schlagwörter: WYSIWYG, Editor, Vorlage
---
Der Etiketteneditor ist admin-geschützt. Vorlagen können neu angelegt, kopiert, gespeichert, gelöscht und einem Artikel zugeordnet werden.

Elemente werden mit X/Y-Position sowie Breite und Höhe in Millimetern definiert und können zusätzlich in der Vorschau mit der Maus verschoben werden. Unterstützt werden statischer Text, dynamische Datenfelder, QR-Code, Code128, Rahmen, Linien und Bild/Logo.

Ein Testdruck verwendet den aktuellen Editorstand. Produktionsdrucke verwenden die gespeicherte Vorlage.

## [label-tokens] Datenplatzhalter im Etikett
Kategorie: Etiketten
Abhängigkeiten: label-designer, ve-history
Folgewirkungen: label-printing
Schlagwörter: Platzhalter, Token, Datenfeld
---
Dynamische Etikettentexte verwenden Platzhalter wie {{ArticleNumber}}, {{OrderNumber}}, {{ActualQuantity}}, {{VE_ID}} oder {{CompletedAt}}. Beim Rendering ersetzt Partcounter diese Tokens durch die Daten der konkreten Verpackungseinheit.

Unbekannte Platzhalter bleiben sichtbar und sollten im Editor korrigiert werden.

## [label-images] Bilder und Logos im Etikett
Kategorie: Etiketten
Abhängigkeiten: label-designer
Folgewirkungen: label-printing
Schlagwörter: Bild, Logo, PNG, JPG
---
Bildobjekte können PNG, JPG/JPEG, BMP, GIF oder TIFF enthalten. Das Bild wird direkt in der Vorlagendefinition eingebettet. Dadurch bleibt die Vorlage funktionsfähig, auch wenn die ursprünglich ausgewählte Datei später verschoben oder gelöscht wird.

Optional kann das Seitenverhältnis erhalten bleiben. Für Firmenlogos ist PNG mit transparentem Hintergrund besonders geeignet.

## [printer-settings] Druckereinstellungen
Kategorie: Einstellungen
Abhängigkeiten: admin-access
Folgewirkungen: label-printing
Schlagwörter: Drucker, AutoPrint, Windows
---
Im geschützten Einstellungsbereich wird der Windows-Druckername festgelegt und der automatische Etikettendruck ein- oder ausgeschaltet. Ein Testetikett dient zur Prüfung von Windows-Druckwarteschlange, Treiber, Papierformat und Layout.

Die Druckereinstellung beeinflusst keinen VE-Abschluss. Bei Druckproblemen bleibt die Produktionshistorie erhalten.

## [company-branding] Firmenlogo im Leitstand
Kategorie: Einstellungen
Abhängigkeiten: admin-access
Folgewirkungen: leitstand
Schlagwörter: Firmenlogo, Branding, Kopfzeile
---
Im geschützten Bereich Einstellungen/Druck kann ein Firmenlogo ausgewählt, ersetzt oder entfernt werden. Das Logo erscheint links neben dem PARTCOUNTER-Schriftzug.

Die Datei wird nach %LOCALAPPDATA%\Partcounter\Branding kopiert. Damit ist die Kopfzeile nicht vom ursprünglichen Dateipfad abhängig. Ist kein gültiges Logo vorhanden, fällt die Oberfläche auf die normale Kopfzeile zurück.

## [admin-access] Bediener-/Admin-Trennung
Kategorie: Administration
Abhängigkeiten: startup
Folgewirkungen: machine-modbus, label-designer, commissioning, als, printer-settings, company-branding, update-center, simulation
Schlagwörter: Admin, Passwort, Sperre
---
Der reguläre Produktionsbetrieb ist ohne Anmeldung möglich. Geschützt werden Funktionen, die Konfiguration oder Systemverhalten verändern.

Beim ersten Zugriff auf einen geschützten Bereich wird lokal ein Admin-Passwort eingerichtet. Danach kann die Administration für die laufende Sitzung entsperrt werden. Nach Programmneustart ist sie wieder gesperrt.

Das Passwort wird nicht im Klartext gespeichert. Partcounter speichert einen PBKDF2-SHA256-Hash mit zufälligem Salt. Die Admin-Sperre ist eine Bedien- und Konfigurationsbarriere; sie ersetzt keine Betriebssystem- oder Netzwerksicherheit.

## [machine-modbus] Maschinen-/Modbus-Konfiguration
Kategorie: Kommunikation
Abhängigkeiten: admin-access, modbus-v2
Folgewirkungen: machine-selection, commissioning
Schlagwörter: IP, Port, Unit-ID, Modbus
---
Für jede Maschine werden Maschinenname, feste IP-Adresse, TCP-Port, Unit-ID und Aktivstatus gepflegt. Standardport ist 502, Standard Unit-ID 1.

Die 30 LOGO!-Stationen sollen eindeutige feste Adressen besitzen. Änderungen an IP oder Unit-ID müssen mit der realen LOGO!-Konfiguration übereinstimmen, sonst kann Partcounter die Station nicht korrekt erreichen.

## [modbus-v2] Modbus Protocol V2
Kategorie: Kommunikation
Abhängigkeiten: machine-modbus
Folgewirkungen: modbus-job-write, current-ve-cycles, command-sequence, heartbeat
Schlagwörter: HR1, HR37, ProtocolVersion 2
---
Partcounter R001.14 verwendet unverändert Modbus Protocol Version 2. PC→LOGO! liegt auf HR1–HR12, LOGO!→PC auf HR20–HR37.

Wesentlich ist, dass die LOGO! aktuelle VE-Zyklen überträgt und nicht die bereits multiplizierte Stückzahl. Partcounter berechnet CurrentParts = CurrentVECycles × ActiveCavitiesEcho. Für die zuletzt abgeschlossene VE gilt LastCompletedVEQuantity = LastCompletedVECycles × LastCompletedCavities.

HR37 trägt deshalb die Kavitätenzahl, die zum Zeitpunkt des letzten VE-Abschlusses gültig war.

## [modbus-job-write] Jobparameter PC → LOGO!
Kategorie: Kommunikation
Abhängigkeiten: modbus-v2, order-start, command-sequence
Folgewirkungen: logo-control
Schlagwörter: HR1-HR12, Jobparameter
---
Beim Jobstart schreibt Partcounter ProtocolVersion, CommandSequence, CommandWord, Kavitäten, VE-Soll, Ventilimpuls, Job-ID, Zielzyklen und PC-Heartbeat in den Konfigurationsbereich.

32-Bit-Werte werden als High Word vor Low Word übertragen. TargetCyclesPerVE ist im V2-Betrieb auf maximal 32767 begrenzt.

## [command-sequence] CommandSequence und AckSequence
Kategorie: Kommunikation
Abhängigkeiten: modbus-v2
Folgewirkungen: order-pause, order-resume, manual-ve-change, reset-machine
Schlagwörter: Sequence, Ack, One-Shot, 32767
---
Jeder neue Befehl erhält eine CommandSequence zwischen 1 und 32767. Nach 32767 folgt wieder 1. Die LOGO! bearbeitet einen Befehl nur, wenn CommandSequence und zuletzt bestätigte Sequenz unterschiedlich sind.

Nach Verarbeitung schreibt die LOGO! die Sequenz als AckSequence zurück. Nach Wiederverbindung liest Partcounter zuerst die aktuelle AckSequence und setzt die nächste CommandSequence darauf auf. Dadurch werden Doppelbefehle nach PC-Neustart oder Verbindungsabbruch vermieden.

## [heartbeat] PC- und LOGO-Heartbeat
Kategorie: Kommunikation
Abhängigkeiten: modbus-v2
Folgewirkungen: commissioning, offline-recovery
Schlagwörter: Heartbeat, Diagnose, WLAN
---
PC und LOGO! führen jeweils einen Heartbeat im Bereich 1–32767. Der PC-Heartbeat dient der LOGO! als Kommunikationsdiagnose. Ein stehengebliebener PC-Heartbeat ist ausdrücklich kein Produktions-Stopp-Befehl.

Die LOGO! zählt mit den zuletzt gültigen Parametern weiter. Der LOGO-Heartbeat ermöglicht dem PC zu erkennen, ob die Steuerung selbst noch zyklisch arbeitet.

## [current-ve-cycles] VE-Zykluszähler und Stückzahl
Kategorie: LOGO
Abhängigkeiten: logo-control, modbus-v2
Folgewirkungen: fill-level, ve-completion
Schlagwörter: CurrentVECycles, Stückzahl
---
Die LOGO! zählt pro gültiger I1-Flanke exakt einen Werkzeugzyklus. CurrentVECycles wird als DWord über HR22/HR23 gemeldet. Partcounter multipliziert den Zyklusstand mit ActiveCavitiesEcho.

Diese Trennung vermeidet 16-Bit-Probleme innerhalb der LOGO! und hält die Kavitätenrechnung nachvollziehbar auf der PC-Seite.

## [logo-control] Partcounter_LOGO_V001
Kategorie: LOGO
Abhängigkeiten: modbus-job-write
Folgewirkungen: current-ve-cycles, ve-completion, offline-recovery
Schlagwörter: LOGO, I1, Q1, Zähler
---
Das standardisierte LOGO!-Programm erkennt zuerst die echte positive Maschinenflanke an I1. Ein CountGate lässt diese Flanke nur durch, wenn Automatik aktiv, Zählung nicht pausiert und kein VE-Wechsel aktiv ist.

CurrentVECycles zählt die aktuelle VE, TotalCycles den gesamten Auftrag. Erreicht CurrentVECycles den TargetCyclesPerVE-Wert, erzeugt die LOGO! lokal einen CompletionPulse und startet den VE-Wechsel.

Q1 steuert das vorgesehene Koppel-/Interface-Relais beziehungsweise die validierte Ventilansteuerung. Safety-Funktionen bleiben vollständig außerhalb von Partcounter.

## [ve-completion] Automatischer VE-Abschluss
Kategorie: LOGO
Abhängigkeiten: logo-control, current-ve-cycles
Folgewirkungen: ve-history, label-printing, last-partial-ve, ve-attention
Schlagwörter: CompletionSequence, VE fertig, Q1
---
Beim VE-Abschluss speichert die LOGO! vor dem Reset den Zyklusstand, Abschlussgrund und die aktive Kavitätenzahl. Danach werden CompletedVEs und CompletionSequence erhöht, Q1 für die konfigurierte Zeit angesteuert und der aktuelle VE-Zähler zurückgesetzt.

LastCompletedVECycles, LastCompletionReason und LastCompletedCavities bleiben bis zum nächsten Abschluss stabil. Partcounter erkennt eine neue VE an einer geänderten CompletionSequence und kann dadurch Historie und Etikett genau einmal erzeugen.

## [offline-recovery] PC-/WLAN-Ausfall und Wiederverbindung
Kategorie: Kommunikation
Abhängigkeiten: heartbeat, logo-control, command-sequence
Folgewirkungen: leitstand
Schlagwörter: Offline, WLAN, Wiederverbindung, Synchronisierung
---
Fällt der PC oder die WLAN-Strecke kurzzeitig aus, arbeitet die LOGO! lokal weiter. Sie zählt reale Zyklen und führt einen fälligen VE-Wechsel aus.

Nach Wiederverbindung liest Partcounter den vollständigen Statusblock. Die CommandSequence wird aus AckSequence synchronisiert. Die Anzeige übernimmt CurrentVECycles, TotalCycles, VE-Nummern, CompletionSequence und Fehlerstatus aus der LOGO!.

Ein Wiederverbindungstest gehört zwingend zur Inbetriebnahme jeder Maschine.

## [commissioning] Inbetriebnahme / Diagnose
Kategorie: Inbetriebnahme
Abhängigkeiten: admin-access, machine-modbus, modbus-v2, heartbeat
Folgewirkungen: rollout
Schlagwörter: Diagnose, Test, Inbetriebnahme
---
Der Inbetriebnahmebereich unterstützt die strukturierte Prüfung einer realen Station: IP/Port, ProtocolVersion, Heartbeats, Statusword, ErrorCode, Zähler, AckSequence, CompletionSequence und relevante Produktionswerte.

Die technische Freigabe einer Maschine darf erst nach erfolgreichem Test von Zyklusflanke, Kavitäten, Aufrundung, Pause/Resume, manuellem Wechsel, Ventilimpuls, Kommunikationsausfall und Wiederverbindung erfolgen.

## [rollout] Rolloutstatus 30 Maschinen
Kategorie: Inbetriebnahme
Abhängigkeiten: commissioning
Folgewirkungen: machine-modbus
Schlagwörter: Rollout, 30 Maschinen, Freigabe
---
Der Rolloutstatus fasst den Inbetriebnahmefortschritt über den Maschinenpark zusammen. Er dient der organisatorischen Übersicht und ersetzt nicht das technische Prüfprotokoll der einzelnen Station.

Eine Station sollte erst als produktionsbereit markiert werden, wenn Hardwareverdrahtung, LOGO!-Programm, Netzwerkparameter und Funktionsprüfungen dokumentiert sind.

## [als] ARBURG ALS Integration
Kategorie: Schnittstellen
Abhängigkeiten: admin-access, article-master, machine-selection
Folgewirkungen: order-start
Schlagwörter: ARBURG, ALS, Excel, REST, Hotfolder
---
Die ALS-Integration kann Auftragsdaten über Datei/Hotfolder oder REST/JSON einlesen. Unterstützt werden konfigurierbare Feldmappings und Maschinen-Aliase.

Ein importierter ALS-Auftrag wird standardmäßig zunächst in die Partcounter-Auftragsmaske übernommen. Ein automatischer Start sollte erst nach vollständig validierter Zuordnung von Maschine, Artikel und Mengen aktiviert werden.

Datei-/Hotfolder-Modus unterstützt XLSX/XLSM sowie CSV/TXT/TSV. REST kann Basic, Bearer, API-Key und optional Clientzertifikat verwenden. Zugangsdaten werden geschützt gespeichert.

## [data-storage] SQLite-Datenhaltung
Kategorie: Daten
Abhängigkeiten: -
Folgewirkungen: article-master, ve-history, printer-settings, company-branding
Schlagwörter: SQLite, Datenbank, WAL
---
Die zentrale lokale Datenbank liegt standardmäßig unter %LOCALAPPDATA%\Partcounter\partcounter.db. Gespeichert werden unter anderem Maschinen, Artikel, Verpackungseinheiten, Einstellungen und Ereignisse.

SQLite arbeitet im WAL-Modus. Die Datenbank ist Produktionsdatenhaltung und sollte in ein betriebliches Backupkonzept einbezogen werden. Ein Softwareupdate darf diese Datenbank nicht ersetzen.

## [event-log] Ereignisse und Fehlerprotokoll
Kategorie: Daten
Abhängigkeiten: data-storage
Folgewirkungen: commissioning
Schlagwörter: Events, Fehler, Log
---
Partcounter protokolliert relevante Betriebsereignisse und Kommunikationsfehler in der Datenbank. Zusätzlich existiert für Start- und unbehandelte Programmfehler Partcounter_startup.log im lokalen Partcounter-Verzeichnis.

Bei sporadischen Kommunikationsproblemen sollten Zeitpunkt, Maschine, Windows-Netzwerkstatus und LOGO!-Status gemeinsam betrachtet werden.

## [update-center] Software-Update-Center
Kategorie: Administration
Abhängigkeiten: admin-access, update-package
Folgewirkungen: startup
Schlagwörter: Update, Netzwerk, USB, lokal
---
Ab R001.14 besitzt Partcounter ein standardisiertes Update-Center im geschützten Einstellungsbereich. Updates können aus einem konfigurierten Netzwerkordner, von USB oder aus einer lokalen ZIP-Datei eingelesen werden.

Jedes Updatepaket enthält ein Manifest, Versionsinformationen, einen Payload und SHA-256-Prüfsummen. Partcounter prüft Produktname, Paketstruktur, Zielversion und Dateiintegrität, bevor ein Update angeboten wird.

Für die Installation wird der Payload zunächst in %LOCALAPPDATA%\Partcounter\Updates\Staging entpackt. Nach Bestätigung erzeugt Partcounter einen lokalen Installationsprozess, beendet die laufende Anwendung, sichert zu überschreibende Dateien, kopiert den neuen Stand in den Installationsordner und startet Partcounter erneut.

Die SQLite-Datenbank, Admin-Zugangsdaten, Brandingdateien und sonstige Benutzerdaten unter %LOCALAPPDATA% werden nicht durch den Update-Payload ersetzt.

## [update-package] Partcounter-Updatepaket
Kategorie: Administration
Abhängigkeiten: -
Folgewirkungen: update-center
Schlagwörter: ZIP, Manifest, SHA256, Payload
---
Ein gültiges Updatepaket ist eine ZIP-Datei mit partcounter-update.json im Wurzelverzeichnis, payload-sha256.txt und dem Unterordner payload/.

Das Manifest enthält mindestens SchemaVersion, Product=Partcounter, Version, Revision, Architecture und PayloadRoot. Die Prüfsummendatei enthält SHA-256-Werte aller Payload-Dateien.

Partcounter führt keine vom Paket gelieferten Skripte aus. Der eigentliche Installationsablauf wird von der Anwendung selbst erzeugt. Dadurch bleibt die Update-Logik kontrolliert und reproduzierbar.

## [about] Über Partcounter
Kategorie: Grundlagen
Abhängigkeiten: -
Folgewirkungen: -
Schlagwörter: Über, Version, Programmierer, Lizenz
---
Die frei zugängliche Über-Funktion zeigt Produktname, aktuelle Programmversion, Revision, Programmierer, Programmiersprache/Technologie, Modbus-Protokollversion sowie Systeminformationen zu Windows, .NET Runtime und Prozessarchitektur.

Programmierer: Harald Peetz.
Technologie: C#, .NET 8, WPF, SQLite, NModbus.

Der angezeigte Lizenzhinweis ist eine kurze Produktkennzeichnung und keine vollständige juristische Lizenzvereinbarung.

## [help] Integrierte Hilfe
Kategorie: Grundlagen
Abhängigkeiten: -
Folgewirkungen: -
Schlagwörter: Hilfe, F1, Suche, Abhängigkeiten
---
Die Hilfe ist ohne Admin-Anmeldung verfügbar. Sie kann über den Hilfe-Button oder F1 geöffnet werden.

Links können Themen nach Kategorie und Suchbegriff gefiltert werden. Rechts zeigt Partcounter die vollständige Beschreibung, direkte Abhängigkeiten und Folgewirkungen. Abhängigkeiten sind anklickbar, sodass sich zusammenhängende Funktionsketten nachvollziehen lassen.

Die Hilfe beschreibt nicht nur einzelne Bedienelemente, sondern auch das technische Zusammenspiel von Leitstand, Datenbank, Modbus, LOGO!, VE-Historie und Etikettendruck.

## [end-to-end] Gesamtprozess: vom Artikel bis zum fertigen Etikett
Kategorie: Zusammenspiel
Abhängigkeiten: article-master, order-start, modbus-job-write, logo-control, ve-completion, ve-history, label-printing
Folgewirkungen: -
Schlagwörter: Gesamtprozess, Abhängigkeiten, Zusammenspiel
---
1. Im Artikelstamm werden Werkzeug, Kavitäten und Standard-VE definiert.
2. Im Leitstand wählt der Bediener Maschine und Artikel und gibt Auftrag sowie Gesamtmenge ein.
3. Partcounter berechnet erste VE-Zielmenge und Zielzyklen.
4. Im Echtbetrieb werden die Jobparameter per Modbus an die LOGO! übertragen.
5. Die LOGO! zählt jede echte I1-Flanke lokal.
6. Der PC liest CurrentVECycles und multipliziert mit ActiveCavitiesEcho für die Anzeige.
7. Bei Erreichen der Zielzyklen schließt die LOGO! die VE ab und pulst Q1.
8. Die LOGO! erhöht CompletionSequence und hält die Abschlussdaten stabil.
9. Partcounter erkennt die neue CompletionSequence und erzeugt den VE-Historieneintrag.
10. Die passende Etikettenvorlage wird aufgelöst und – falls AutoPrint aktiv – gedruckt.
11. Ist die Auftragsrestmenge kleiner als die Standard-VE, überträgt Partcounter die Parameter der letzten Teil-VE ohne Auftragsreset.
12. Nach Erreichen der Auftragsmenge kann der Auftrag kontrolliert beendet werden.

Diese Kette ist die wichtigste Funktionsabhängigkeit des gesamten Systems.

## [safety] Abgrenzung Safety / Prozesssteuerung
Kategorie: Grundlagen
Abhängigkeiten: logo-control
Folgewirkungen: commissioning
Schlagwörter: Safety, Not-Halt, Schutztür
---
Partcounter und die verwendete Standard-LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztür, sichere Maschinenfreigaben und andere Safety-Funktionen verbleiben in den dafür vorgesehenen sicheren Schaltungen beziehungsweise Steuerungen.

Der Verpackungswechsler muss so ausgelegt und bewertet sein, dass Neustart, Kommunikationsverlust, Spannungsausfall und fehlerhafte Telegramme keinen gefährlichen Zustand erzeugen. Vor Produktionsfreigabe sind Risikobeurteilung, I/O-Prüfung und dokumentierte Inbetriebnahme erforderlich.
