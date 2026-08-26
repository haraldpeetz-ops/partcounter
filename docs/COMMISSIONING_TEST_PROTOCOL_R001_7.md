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

Eine Station darf erst für den automatischen Verpackungswechsel freigegeben werden, wenn alle für die konkrete Maschine relevanten Muss-Prüfpunkte bestanden sind. Nicht vorhandene optionale Hardware wird mit `n. a.` gekennzeichnet. Abweichungen werden mit Fehlerbild, Maßnahme und Wiederholungsprüfung dokumentiert.

## Prüfschritte

| Nr. | Prüfschritt | Soll / Akzeptanzkriterium | Ergebnis | Bemerkung |
|---:|---|---|---|---|
| 1 | Sichtprüfung Verdrahtung | I1/Q1 und optionale I2/I3/Q2/Q3 entsprechen Schaltplan; keine Safety-Funktion läuft über Partcounter | | |
| 2 | Startzustand LOGO! | Q1 ist nach Einschalten AUS; keine ungewollte Bewegung | | |
| 3 | Netzwerk | LOGO! über konfigurierte IP erreichbar; TCP 502 verfügbar | | |
| 4 | Modbus-Protokoll | HR20 meldet ProtocolVersion 2 | | |
| 5 | VM-Zuordnung | HR1–HR12 und HR20–HR37 entsprechen Registerplan | | |
| 6 | DWord-Wordreihenfolge | bekannter Testwert auf DWord-Registern wird High Word / Low Word korrekt rekonstruiert | | |
| 7 | Zielwert-Mapping | VD18 / HR10-HR11 verändert den `On Threshold` des VE-Zählers korrekt | | |
| 8 | LOGO!-Heartbeat | HR34 ändert sich zyklisch und bleibt im Bereich 1…32767 | | |
| 9 | PC-Heartbeat | HR12 ändert sich im Echtbetrieb zyklisch und bleibt im Bereich 1…32767 | | |
| 10 | Ack-Handshake | neue CommandSequence wird genau einmal verarbeitet; HR30 übernimmt dieselbe Sequenz | | |
| 11 | PC-Neustart | erster Befehl nach Neustart verwendet die Sequenz nach aktuellem AckSequence und wird verarbeitet | | |
| 12 | CommandSequence-Wrap | 32767 → 1 wird als neuer Befehl erkannt; keine Mehrfachauslösung | | |
| 13 | Heartbeat-Wrap | 32767 → 1 erzeugt keine Kommunikationsstörung | | |
| 14 | I1-Flanke | eine reale Zyklusflanke erhöht CurrentVECycles exakt um 1 | | |
| 15 | Pause bei I1 HIGH | Fortsetzen bei noch anstehendem I1-Pegel erzeugt keinen künstlichen Zählimpuls | | |
| 16 | Störimpuls / Prellen | kein Doppelzählen bei einem Maschinenzyklus | | |
| 17 | 1 Kavität | PC-Teilezahl = CurrentVECycles × 1 | | |
| 18 | 2 Kavitäten | PC-Teilezahl = CurrentVECycles × 2 | | |
| 19 | 4 Kavitäten | PC-Teilezahl = CurrentVECycles × 4 | | |
| 20 | 8 Kavitäten | PC-Teilezahl = CurrentVECycles × 8 | | |
| 21 | 16 Kavitäten | PC-Teilezahl = CurrentVECycles × 16 | | |
| 22 | 32 Kavitäten | PC-Teilezahl = CurrentVECycles × 32 | | |
| 23 | 64 Kavitäten | PC-Teilezahl = CurrentVECycles × 64 | | |
| 24 | Rundungstest | 1000 Teile / 64 Kavitäten ergibt 16 Zielzyklen und 1024 effektive Teile | | |
| 25 | VE-Grenzwert 32767 | TargetCyclesPerVE = 32767 wird akzeptiert und korrekt gezählt | | |
| 26 | VE-Grenzwert 32768 | TargetCyclesPerVE = 32768 wird vom PC abgewiesen; LOGO!-Auftrag wird nicht verändert | | |
| 27 | TotalCycles DWORD | TotalCycles oberhalb 32767 bleibt im PC korrekt; kein 16-Bit-Überlauf | | |
| 28 | VE automatisch voll | bei Erreichen der Zielzyklen wird genau ein Abschluss erzeugt | | |
| 29 | CompletionSequence | HR33 erhöht sich je abgeschlossener VE exakt einmal | | |
| 30 | LastCompletedVECycles | HR28 = 0 und HR29 enthält den abgeschlossenen Zykluswert vor Reset | | |
| 31 | Snapshot-Stabilität | HR29 bleibt nach Reset von CurrentVECycles bis zum nächsten VE-Abschluss unverändert | | |
| 32 | LastCompletedCavities | HR37 speichert die zur abgeschlossenen VE gehörende Kavitätenzahl und bleibt stabil | | |
| 33 | CompletionReason automatisch | HR36 = 1 und bleibt bis zum nächsten Abschluss gespeichert | | |
| 34 | VE-Nummer | CurrentVENumber wird nach Abschluss genau um 1 erhöht | | |
| 35 | LastCompletedVENumber | HR32 enthält die Nummer der gerade abgeschlossenen VE | | |
| 36 | CompletedVEs | HR27 erhöht sich je Abschluss genau um 1 | | |
| 37 | Ventilregister 750 ms | Eingabe 750 ms führt zu HR7 = 75 | | |
| 38 | Ventilimpuls 750 ms | Q1 schaltet genau einmal und gemessene Impulszeit entspricht 750 ms innerhalb der festgelegten Toleranz | | |
| 39 | Ventil-Minimum | 50 ms → HR7 = 5; Ausgangsverhalten korrekt | | |
| 40 | Ventil-Maximum | 5000 ms → HR7 = 500; Ausgangsverhalten korrekt | | |
| 41 | ungültiges Zeitraster | z. B. 755 ms wird PC-seitig abgewiesen und nicht geschrieben | | |
| 42 | Wechselanzeige Q2 | falls genutzt: Anzeige entspricht VE-Wechselstatus | | |
| 43 | manueller VE-Wechsel | bei gefüllter Teil-VE genau ein Abschluss; HR36 = 2 und bleibt gespeichert | | |
| 44 | manueller Wechsel bei 0 | leere VE wird nicht abgeschlossen; Befehl wird dennoch quittiert | | |
| 45 | Pause | neue I1-Flanken verändern die Zähler nicht | | |
| 46 | Fortsetzen | Zählung setzt ohne Auftragsreset am vorhandenen Stand fort | | |
| 47 | letzte Teil-VE | kleinere Restmenge übernimmt neuen TargetCyclesPerVE nach vorherigem Abschluss ohne Reset von TotalCycles/CompletedVEs | | |
| 48 | verbotener Zielwertwechsel | VD18 wird während CurrentVECycles > 0 durch Partcounter nicht verändert | | |
| 49 | PC-Verbindung trennen | LOGO! zählt mit letzten gültigen Parametern lokal weiter | | |
| 50 | WLAN unterbrechen | ein während Ausfall fälliger automatischer VE-Wechsel wird lokal ausgeführt | | |
| 51 | Wiederverbindung | PC synchronisiert Zähler/VE ohne Doppelabschluss | | |
| 52 | PC-Heartbeat steht | Statusbit 5 wird gesetzt; Produktion läuft lokal weiter | | |
| 53 | falsche Protokollversion | Auftrag wird abgewiesen; ErrorCode 1; Q1 bleibt AUS | | |
| 54 | ungültige Kavitäten | Auftrag wird abgewiesen; ErrorCode 2 | | |
| 55 | TargetPartsPerVE = 0 | Auftrag wird abgewiesen; ErrorCode 3 | | |
| 56 | ungültige Zielzyklen | Auftrag wird abgewiesen; ErrorCode 4 | | |
| 57 | ungültige Ventilzeit | Auftrag wird abgewiesen; ErrorCode 5 | | |
| 58 | Endlagentimeout | falls I2 genutzt: ErrorCode 10, Alarm, weitere automatische Wechsel gesperrt | | |
| 59 | Alarm quittieren | definierter quittierbarer Fehler wird kontrolliert zurückgesetzt | | |
| 60 | LOGO!-Spannung AUS/EIN | Q1 bleibt beim Neustart AUS; keine ungewollte Bewegung | | |
| 61 | Zähler-Retentivität | Verhalten von CurrentVECycles/TotalCycles nach Power-Cycle entspricht freigegebenem Wiederanlaufkonzept | | |
| 62 | Etikett | pro neuer CompletionSequence genau ein Etikett / ein nachvollziehbarer Druckauftrag | | |
| 63 | VE-Historie | Soll, Ist, Mehrmenge, VE-ID, Maschine, Auftrag und Zeit sind korrekt gespeichert | | |
| 64 | Mehrfach-VE-Test | mindestens 20 aufeinanderfolgende VEs ohne Doppelzählung, Doppelabschluss oder Mehrfach-Ventilimpuls | | |
| 65 | Kommunikations-Langzeittest | mehrfache definierte WLAN-/LAN-Unterbrechungen ohne Verlust lokaler Zählungen | | |
| 66 | Produktionsgrenze | kein LOGO!-Auftrag überschreitet 999999 TotalCycles bzw. 32767 CompletedVEs | | |

## Messprotokoll Ventilimpuls

| Versuch | Eingabe PC [ms] | HR7 [10 ms] | Gemessen [ms] | Endlage erreicht | Ergebnis |
|---:|---:|---:|---:|---|---|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |
| 4 | | | | | |
| 5 | | | | | |

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
