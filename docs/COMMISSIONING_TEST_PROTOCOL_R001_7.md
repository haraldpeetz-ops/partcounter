# Partcounter R001.7 – Inbetriebnahme- und Abnahmeprotokoll

**Software:** Partcounter R001.7  
**LOGO!-Programm:** Partcounter_LOGO_V001  
**Modbus-Protokoll:** V2  
**Referenzhardware:** Siemens LOGO! 12/24RCEo, `6ED1052-2MD08-0BA2`  
**Zweck:** Erstinbetriebnahme und reproduzierbare Freigabe jeder realen Maschinenstation

## Stammdaten der Prüfung

| Feld | Eintrag / Referenzmaschine 01 |
|---|---|
| Datum | |
| Prüfer | |
| Maschine / Maschinen-Nr. | Referenzmaschine 01 |
| Spritzgussmaschine Typ | |
| LOGO! Typ / Firmware | 6ED1052-2MD08-0BA2 / Firmware prüfen |
| LOGO! Versorgung | 24 V DC |
| LOGO! IP-Adresse | |
| Modbus Port | 502 |
| Unit ID | 1 |
| WLAN-Bridge / Client | |
| Access Point | |
| Zyklusimpuls I1 | 24 V DC |
| Ventil | 24-V-DC-Spule |
| Q1 | LOGO!-Relaisausgang |
| Ventil-Spulenstrom / Leistung | noch einzutragen |
| externe Q1-Absicherung | erforderlich |
| Koppelrelais vorhanden | bevorzugt / festlegen |
| Freilauf-/Entstörbeschaltung | prüfen |
| Endlagenrückmeldung I2 | nein; optional vorbereitet |
| Endlagenüberwachung | OFF |
| Ventilimpuls | 50…5000 ms, 10-ms-Raster |
| Standard-Ventilimpuls | 750 ms |

## Freigaberegel

Eine Station darf erst für den automatischen Verpackungswechsel freigegeben werden, wenn alle für die konkrete Maschine relevanten Muss-Prüfpunkte bestanden sind. Nicht vorhandene optionale Hardware wird mit `n. a.` gekennzeichnet. Abweichungen werden mit Fehlerbild, Maßnahme und Wiederholungsprüfung dokumentiert.

Für die verwendete LOGO! sind die Relaisausgänge nicht kurzschlussgeschützt; eine externe Absicherung ist daher erforderlich. Die maximale spezifizierte Kontaktlast beträgt 10 A ohmsch bzw. 3 A induktiv. Die tatsächliche Ventilspule muss trotzdem anhand ihrer realen Strom-/Leistungsdaten bewertet werden.

## Prüfschritte

| Nr. | Prüfschritt | Soll / Akzeptanzkriterium | Ergebnis | Bemerkung |
|---:|---|---|---|---|
| 1 | LOGO!-Identifikation | Bestellnummer 6ED1052-2MD08-0BA2 bestätigt | | |
| 2 | Versorgung LOGO! | 24 V DC vorhanden und polaritätsrichtig | | |
| 3 | Sichtprüfung Verdrahtung | I1/Q1 und optionale I2/I3/Q2/Q3 entsprechen Schaltplan; keine Safety-Funktion läuft über Partcounter | | |
| 4 | Q1-Absicherung | externe Sicherung vorhanden und passend dimensioniert | | |
| 5 | Ventilspule | 24 V DC; Nennstrom/Nennleistung dokumentiert | | |
| 6 | Entstörung Ventil | Freilauf-/Entstörbeschaltung vorhanden oder im Ventilstecker integriert | | |
| 7 | Koppelrelais | Entscheidung direkt/Koppelrelais dokumentiert; bevorzugt Koppelrelais | | |
| 8 | Startzustand LOGO! | Q1 ist nach Einschalten AUS; keine ungewollte Bewegung | | |
| 9 | Netzwerk | LOGO! über konfigurierte IP erreichbar; TCP 502 verfügbar | | |
| 10 | Modbus-Protokoll | HR20 meldet ProtocolVersion 2 | | |
| 11 | VM-Zuordnung | HR1–HR12 und HR20–HR37 entsprechen Registerplan | | |
| 12 | DWord-Wordreihenfolge | bekannter Testwert auf DWord-Registern wird High Word / Low Word korrekt rekonstruiert | | |
| 13 | Zielwert-Mapping | VD18 / HR10-HR11 verändert den `On Threshold` des VE-Zählers korrekt | | |
| 14 | LOGO!-Heartbeat | HR34 ändert sich zyklisch und bleibt im Bereich 1…32767 | | |
| 15 | PC-Heartbeat | HR12 ändert sich im Echtbetrieb zyklisch und bleibt im Bereich 1…32767 | | |
| 16 | Ack-Handshake | neue CommandSequence wird genau einmal verarbeitet; HR30 übernimmt dieselbe Sequenz | | |
| 17 | PC-Neustart | erster Befehl nach Neustart verwendet die Sequenz nach aktuellem AckSequence und wird verarbeitet | | |
| 18 | CommandSequence-Wrap | 32767 → 1 wird als neuer Befehl erkannt; keine Mehrfachauslösung | | |
| 19 | Heartbeat-Wrap | 32767 → 1 erzeugt keine Kommunikationsstörung | | |
| 20 | I1-Spannung | Zyklusimpuls liegt als sauberes 24-V-DC-Signal an | | |
| 21 | I1-Flanke | eine reale Zyklusflanke erhöht CurrentVECycles exakt um 1 | | |
| 22 | Pause bei I1 HIGH | Fortsetzen bei noch anstehendem I1-Pegel erzeugt keinen künstlichen Zählimpuls | | |
| 23 | Störimpuls / Prellen | kein Doppelzählen bei einem Maschinenzyklus | | |
| 24 | 1 Kavität | PC-Teilezahl = CurrentVECycles × 1 | | |
| 25 | 2 Kavitäten | PC-Teilezahl = CurrentVECycles × 2 | | |
| 26 | 4 Kavitäten | PC-Teilezahl = CurrentVECycles × 4 | | |
| 27 | 8 Kavitäten | PC-Teilezahl = CurrentVECycles × 8 | | |
| 28 | 16 Kavitäten | PC-Teilezahl = CurrentVECycles × 16 | | |
| 29 | 32 Kavitäten | PC-Teilezahl = CurrentVECycles × 32 | | |
| 30 | 64 Kavitäten | PC-Teilezahl = CurrentVECycles × 64 | | |
| 31 | Rundungstest | 1000 Teile / 64 Kavitäten ergibt 16 Zielzyklen und 1024 effektive Teile | | |
| 32 | VE-Grenzwert 32767 | TargetCyclesPerVE = 32767 wird akzeptiert und korrekt gezählt | | |
| 33 | VE-Grenzwert 32768 | TargetCyclesPerVE = 32768 wird vom PC abgewiesen; LOGO!-Auftrag wird nicht verändert | | |
| 34 | TotalCycles DWORD | TotalCycles oberhalb 32767 bleibt im PC korrekt; kein 16-Bit-Überlauf | | |
| 35 | VE automatisch voll | bei Erreichen der Zielzyklen wird genau ein Abschluss erzeugt | | |
| 36 | CompletionSequence | HR33 erhöht sich je abgeschlossener VE exakt einmal | | |
| 37 | LastCompletedVECycles | HR28 = 0 und HR29 enthält den abgeschlossenen Zykluswert vor Reset | | |
| 38 | Snapshot-Stabilität | HR29 bleibt nach Reset von CurrentVECycles bis zum nächsten VE-Abschluss unverändert | | |
| 39 | LastCompletedCavities | HR37 speichert die zur abgeschlossenen VE gehörende Kavitätenzahl und bleibt stabil | | |
| 40 | CompletionReason automatisch | HR36 = 1 und bleibt bis zum nächsten Abschluss gespeichert | | |
| 41 | VE-Nummer | CurrentVENumber wird nach Abschluss genau um 1 erhöht | | |
| 42 | LastCompletedVENumber | HR32 enthält die Nummer der gerade abgeschlossenen VE | | |
| 43 | CompletedVEs | HR27 erhöht sich je Abschluss genau um 1 | | |
| 44 | Ventilregister 750 ms | Eingabe 750 ms führt zu HR7 = 75 | | |
| 45 | Ventilimpuls 750 ms | Q1 schaltet genau einmal und gemessene Impulszeit entspricht 750 ms innerhalb der festgelegten Toleranz | | |
| 46 | Ventil-Minimum | 50 ms → HR7 = 5; Ausgangsverhalten korrekt | | |
| 47 | Ventil-Maximum | 5000 ms → HR7 = 500; Ausgangsverhalten korrekt | | |
| 48 | ungültiges Zeitraster | z. B. 755 ms wird PC-seitig abgewiesen und nicht geschrieben | | |
| 49 | Endlagenüberwachung Station 01 | deaktiviert; I2 darf keinen Alarm oder Wechselabbruch auslösen | | |
| 50 | optionale I2-Funktion | Programmstruktur enthält aktivierbare Endlagenüberwachung für spätere Maschinen | | |
| 51 | Wechselanzeige Q2 | falls genutzt: Anzeige entspricht VE-Wechselstatus | | |
| 52 | manueller VE-Wechsel | bei gefüllter Teil-VE genau ein Abschluss; HR36 = 2 und bleibt gespeichert | | |
| 53 | manueller Wechsel bei 0 | leere VE wird nicht abgeschlossen; Befehl wird dennoch quittiert | | |
| 54 | Pause | neue I1-Flanken verändern die Zähler nicht | | |
| 55 | Fortsetzen | Zählung setzt ohne Auftragsreset am vorhandenen Stand fort | | |
| 56 | letzte Teil-VE | kleinere Restmenge übernimmt neuen TargetCyclesPerVE nach vorherigem Abschluss ohne Reset von TotalCycles/CompletedVEs | | |
| 57 | verbotener Zielwertwechsel | VD18 wird während CurrentVECycles > 0 durch Partcounter nicht verändert | | |
| 58 | PC-Verbindung trennen | LOGO! zählt mit letzten gültigen Parametern lokal weiter | | |
| 59 | WLAN unterbrechen | ein während Ausfall fälliger automatischer VE-Wechsel wird lokal ausgeführt | | |
| 60 | Wiederverbindung | PC synchronisiert Zähler/VE ohne Doppelabschluss | | |
| 61 | PC-Heartbeat steht | Statusbit 5 wird gesetzt; Produktion läuft lokal weiter | | |
| 62 | falsche Protokollversion | Auftrag wird abgewiesen; ErrorCode 1; Q1 bleibt AUS | | |
| 63 | ungültige Kavitäten | Auftrag wird abgewiesen; ErrorCode 2 | | |
| 64 | TargetPartsPerVE = 0 | Auftrag wird abgewiesen; ErrorCode 3 | | |
| 65 | ungültige Zielzyklen | Auftrag wird abgewiesen; ErrorCode 4 | | |
| 66 | ungültige Ventilzeit | Auftrag wird abgewiesen; ErrorCode 5 | | |
| 67 | optionale Endlage – Simulation | bei aktivierter Testkonfiguration kann fehlende I2-Bestätigung ErrorCode 10 erzeugen | | |
| 68 | Alarm quittieren | definierter quittierbarer Fehler wird kontrolliert zurückgesetzt | | |
| 69 | LOGO!-Spannung AUS/EIN | Q1 bleibt beim Neustart AUS; keine ungewollte Bewegung | | |
| 70 | Zähler-Retentivität | Verhalten von CurrentVECycles/TotalCycles nach Power-Cycle entspricht freigegebenem Wiederanlaufkonzept | | |
| 71 | Etikett | pro neuer CompletionSequence genau ein Etikett / ein nachvollziehbarer Druckauftrag | | |
| 72 | VE-Historie | Soll, Ist, Mehrmenge, VE-ID, Maschine, Auftrag und Zeit sind korrekt gespeichert | | |
| 73 | Mehrfach-VE-Test | mindestens 20 aufeinanderfolgende VEs ohne Doppelzählung, Doppelabschluss oder Mehrfach-Ventilimpuls | | |
| 74 | Kommunikations-Langzeittest | mehrfache definierte WLAN-/LAN-Unterbrechungen ohne Verlust lokaler Zählungen | | |
| 75 | Produktionsgrenze | kein LOGO!-Auftrag überschreitet 999999 TotalCycles bzw. 32767 CompletedVEs | | |

## Messprotokoll Ventilimpuls

| Versuch | Eingabe PC [ms] | HR7 [10 ms] | Gemessen [ms] | Endlage vorhanden | Ergebnis |
|---:|---:|---:|---:|---|---|
| 1 | 50 | 5 | | nein | |
| 2 | 250 | 25 | | nein | |
| 3 | 750 | 75 | | nein | |
| 4 | 2500 | 250 | | nein | |
| 5 | 5000 | 500 | | nein | |

## Kommunikationstest

| Test | Unterbrechungsdauer | Zyklen während Unterbrechung | erwartete VE-Wechsel | tatsächlich | Ergebnis |
|---|---:|---:|---:|---:|---|
| PC-Anwendung beendet | | | | | |
| LAN getrennt | | | | | |
| WLAN getrennt | | | | | |
| PC-Neustart | | | | | |

## Sequenztest

| Test | Ausgangswert | Folgewert | Ack / Status | Ergebnis |
|---|---:|---:|---|---|
| CommandSequence normal | | | | |
| CommandSequence Wrap | 32767 | 1 | | |
| PC Heartbeat Wrap | 32767 | 1 | | |
| LOGO Heartbeat Wrap | 32767 | 1 | | |

## Abweichungen und Maßnahmen

| Nr. | Abweichung | Ursache | Maßnahme | Wiederholungsprüfung | erledigt von / Datum |
|---:|---|---|---|---|---|
| | | | | | |
| | | | | | |
| | | | | | |

## Freigabe

| Rolle | Name | Datum | Unterschrift / Freigabevermerk |
|---|---|---|---|
| Inbetriebnahme | | | |
| Produktion | | | |
| Instandhaltung / Automatisierung | | | |
| QS / technische Freigabe, falls erforderlich | | | |

**Freigabestatus:** nicht geprüft / mit Auflagen freigegeben / freigegeben / gesperrt

Hinweis: Partcounter und die Siemens LOGO! ersetzen keine sicherheitsgerichtete Steuerung. Not-Halt, Schutztüren und sonstige Safety-Funktionen verbleiben vollständig in den dafür vorgesehenen sicheren Maschinenkreisen.
