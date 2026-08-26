# Partcounter R001.7 – Inbetriebnahme- und Abnahmeprotokoll

**Software:** Partcounter R001.7  
**LOGO!-Programm:** Partcounter_LOGO_V001  
**Modbus-Protokoll:** V2  
**Zweck:** Erstinbetriebnahme und reproduzierbare Freigabe jeder realen Maschinenstation

## Stammdaten der Prüfung

| Feld | Eintrag |
|---|---|
| Datum | |
| Prüfer | |
| Maschine / Maschinen-Nr. | |
| Spritzgussmaschine Typ | |
| LOGO! Typ / Firmware | |
| LOGO! IP-Adresse | |
| Modbus Port | 502 |
| Unit ID | 1 |
| WLAN-Bridge / Client | |
| Access Point | |
| Ventil / Spulenspannung | |
| Koppelrelais vorhanden | |
| Endlagenrückmeldung I2 | ja / nein |
| Freigegebene Ventilimpulszeit | ms |

## Freigaberegel

Eine Station darf erst für den automatischen Verpackungswechsel freigegeben werden, wenn alle für die konkrete Maschine relevanten Muss-Prüfpunkte bestanden sind. Nicht vorhandene optionale Hardware ist mit `n. a.` zu kennzeichnen. Abweichungen werden mit Fehlerbild, Maßnahme und Wiederholungsprüfung dokumentiert.

## Prüfschritte

| Nr. | Prüfschritt | Soll / Akzeptanzkriterium | Ergebnis | Bemerkung |
|---:|---|---|---|---|
| 1 | Sichtprüfung Verdrahtung | I1/Q1 und optionale I2/I3/Q2/Q3 entsprechen Schaltplan; keine Safety-Funktion läuft über Partcounter | | |
| 2 | Startzustand LOGO! | Q1 ist nach Einschalten AUS; keine ungewollte Bewegung | | |
| 3 | Netzwerk | LOGO! über konfigurierte IP erreichbar; TCP 502 verfügbar | | |
| 4 | Modbus-Protokoll | HR20 meldet ProtocolVersion 2 | | |
| 5 | VM-Zuordnung | HR1–HR12 und HR20–HR37 entsprechen Registerplan | | |
| 6 | LOGO!-Heartbeat | HR34 ändert sich zyklisch | | |
| 7 | PC-Heartbeat | HR12 ändert sich im Echtbetrieb zyklisch | | |
| 8 | Ack-Handshake | neue CommandSequence wird genau einmal verarbeitet; HR30 übernimmt dieselbe Sequenz | | |
| 9 | PC-Neustart | erster Befehl nach Neustart verwendet Sequenz nach aktuellem AckSequence und wird verarbeitet | | |
| 10 | I1-Flanke | eine reale Zyklusflanke erhöht CurrentVECycles exakt um 1 | | |
| 11 | Störimpuls / Prellen | kein Doppelzählen bei einem Maschinenzyklus | | |
| 12 | 1 Kavität | PC-Teilezahl = CurrentVECycles × 1 | | |
| 13 | 2 Kavitäten | PC-Teilezahl = CurrentVECycles × 2 | | |
| 14 | 4 Kavitäten | PC-Teilezahl = CurrentVECycles × 4 | | |
| 15 | 8 Kavitäten | PC-Teilezahl = CurrentVECycles × 8 | | |
| 16 | 16 Kavitäten | PC-Teilezahl = CurrentVECycles × 16 | | |
| 17 | 32 Kavitäten | PC-Teilezahl = CurrentVECycles × 32 | | |
| 18 | 64 Kavitäten | PC-Teilezahl = CurrentVECycles × 64 | | |
| 19 | Rundungstest | 1000 Teile / 64 Kavitäten ergibt 16 Zielzyklen und 1024 effektive Teile | | |
| 20 | DWORD-Grenztest | Zählerdarstellung oberhalb 32767 bleibt korrekt; kein 16-Bit-Überlauf in der PC-Anzeige | | |
| 21 | VE automatisch voll | bei Erreichen der Zielzyklen wird genau ein Abschluss erzeugt | | |
| 22 | CompletionSequence | HR33 erhöht sich je abgeschlossener VE exakt einmal | | |
| 23 | LastCompletedVECycles | HR28/HR29 speichern den abgeschlossenen Zykluswert vor Reset | | |
| 24 | LastCompletedCavities | HR37 speichert die zu dieser VE gehörende Kavitätenzahl | | |
| 25 | VE-Nummer | CurrentVENumber wird nach Abschluss genau um 1 erhöht | | |
| 26 | CompletedVEs | Zähler erhöht sich je Abschluss genau um 1 | | |
| 27 | Ventilimpuls | Q1 schaltet genau einmal und für die parametrierte Zeit | | |
| 28 | Wechselanzeige Q2 | falls genutzt: Anzeige entspricht VE-Wechselstatus | | |
| 29 | manueller VE-Wechsel | bei gefüllter Teil-VE genau ein Abschluss, Reason = 2 | | |
| 30 | manueller Wechsel bei 0 | leerer Wechsel wird ignoriert; Befehl wird dennoch quittiert | | |
| 31 | Pause | neue I1-Flanken verändern die Zähler nicht | | |
| 32 | Fortsetzen | Zählung setzt ohne Auftragsreset am vorhandenen Stand fort | | |
| 33 | letzte Teil-VE | kleinere Restmenge übernimmt neue TargetCyclesPerVE ohne Reset von TotalCycles/CompletedVEs | | |
| 34 | PC-Verbindung trennen | LOGO! zählt mit letzten gültigen Parametern lokal weiter | | |
| 35 | WLAN unterbrechen | ein während Ausfall fälliger automatischer VE-Wechsel wird lokal ausgeführt | | |
| 36 | Wiederverbindung | PC synchronisiert Zähler/VE ohne Doppelabschluss | | |
| 37 | PC-Heartbeat steht | Statusbit 5 wird gesetzt; Produktion läuft lokal weiter | | |
| 38 | falsche Protokollversion | Auftrag wird abgewiesen; ErrorCode 1; Q1 bleibt AUS | | |
| 39 | ungültige Kavitäten | Auftrag wird abgewiesen; ErrorCode 2 | | |
| 40 | TargetPartsPerVE = 0 | Auftrag wird abgewiesen; ErrorCode 3 | | |
| 41 | ungültige Zielzyklen | Auftrag wird abgewiesen; ErrorCode 4 | | |
| 42 | ungültige Ventilzeit | Auftrag wird abgewiesen; ErrorCode 5 | | |
| 43 | Endlagentimeout | falls I2 genutzt: ErrorCode 10, Alarm, weitere automatische Wechsel gesperrt | | |
| 44 | Alarm quittieren | definierter quittierbarer Fehler wird kontrolliert zurückgesetzt | | |
| 45 | LOGO!-Spannung AUS/EIN | Q1 bleibt beim Neustart AUS; Wiederanlaufverhalten entspricht Freigabekonzept | | |
| 46 | Etikett | pro neuer CompletionSequence genau ein Etikett / ein nachvollziehbarer Druckauftrag | | |
| 47 | VE-Historie | Soll, Ist, Mehrmenge, VE-ID, Maschine, Auftrag und Zeit sind korrekt gespeichert | | |
| 48 | Langzeittest | mindestens mehrere aufeinanderfolgende VEs ohne Doppelzählung oder Mehrfachwechsel | | |

## Messprotokoll Ventilimpuls

| Versuch | Soll [ms] | Gemessen [ms] | Endlage erreicht | Ergebnis |
|---:|---:|---:|---|---|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |

## Kommunikationstest

| Test | Unterbrechungsdauer | Zyklen während Unterbrechung | erwartete VE-Wechsel | tatsächlich | Ergebnis |
|---|---:|---:|---:|---:|---|
| PC-Anwendung beendet | | | | | |
| LAN getrennt | | | | | |
| WLAN getrennt | | | | | |

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
