# Partcounter R001.9 – Inbetriebnahme- und Diagnosezentrum

## Zweck

Das Inbetriebnahmezentrum unterstützt die strukturierte Freigabe der bis zu 30 Partcounter-Stationen. Produktionszählung und VE-Wechsel bleiben weiterhin Aufgabe der Siemens LOGO!; das Diagnosezentrum liest Kommunikations- und Zustandsdaten, verwaltet Hardwareprofile und dokumentiert die Abnahme.

## Referenzmaschine 01

- Siemens LOGO! `6ED1052-2MD08-0BA2`
- LOGO! 12/24RCEo
- Versorgung 24 V DC
- I1 = 24-V-Zyklusimpuls
- Q1 = 24-V-Koppel-/Interface-Relais für kleines Festo-Ventil
- I2 = Endlagenrückmeldung optional vorbereitet, Station 01 deaktiviert
- Ventilimpuls 50…5000 ms, Raster 10 ms, Standard 750 ms
- Modbus TCP Port 502, Unit-ID standardmäßig 1
- ProtocolVersion 2

## Tab „Inbetriebnahme / Diagnose“

### Live-Diagnose

Angezeigt werden pro Maschine:

- Verbindung ONLINE/OFFLINE
- konfigurierte IP-Adresse, Port und Unit-ID
- Zeitpunkt der letzten gültigen LOGO!-Antwort
- PC-Heartbeat
- LOGO!-Heartbeat
- lokale CommandSequence
- AckSequence der LOGO!
- Synchronisationsstatus des Befehls-Handshakes
- StatusWord hexadezimal und dezimal
- dekodierte Statusbits
- ErrorCode mit Klartext
- aktuelle VE-Teilezahl
- TotalCycles
- aktuelle VE-Nummer
- CompletedVEs
- ActiveCavitiesEcho

Die Live-Werte stammen aus derselben `MachineFleetService`-Session, die auch den Produktionsleitstand versorgt. Es wird kein zweiter Hintergrund-Pollingworker gestartet.

### Direkte Modbus-Leseprobe

Die Schaltfläche „Direkte Modbus-Leseprobe“ öffnet temporär eine TCP-Verbindung zur ausgewählten LOGO! und liest ausschließlich den Statusblock.

Die Leseprobe:

- schreibt keine Auftragsparameter,
- verändert keine CommandSequence,
- schaltet Q1 nicht,
- löst keinen manuellen VE-Wechsel aus,
- besitzt einen Timeout von vier Sekunden.

Sie dient zur ersten Netzwerk-/Protokollprüfung, bevor der Echtbetrieb aktiviert wird.

## Hardware- und Freigabeprofil

Pro Maschine werden separat in SQLite gespeichert:

- LOGO!-Bestellnummer
- LOGO!-Typ
- Versorgungsspannung
- Zykluseingang und Signalart
- Ventilausgang und Ventilspannung
- Verwendung eines Koppel-/Interface-Relais
- Endlagenüberwachung Ein/Aus
- Endlageneingang
- Standard-Ventilimpuls
- Freigabestatus
- Notizen

Freigabestatus:

1. `NotTested` – nicht geprüft
2. `InTest` – in Prüfung
3. `ReleasedWithConditions` – mit Auflagen freigegeben
4. `Released` – freigegeben
5. `Blocked` – gesperrt

## Geführter Prüfablauf

R001.9 enthält 16 Kernprüfschritte:

1. LOGO!-Typ und Versorgung
2. IP, Port und Unit-ID
3. ProtocolVersion 2
4. PC-/LOGO!-Heartbeat
5. CommandSequence/AckSequence
6. 24-V-Signal an I1
7. ein Zyklus = ein Zählschritt
8. Pause/Fortsetzen bei I1 HIGH
9. 64-fach-Rundungstest
10. Q1-Koppelrelais und Absicherung
11. Ventilimpulse 50 / 750 / 5000 ms
12. automatischer VE-Abschluss
13. manueller VE-Abschluss
14. PC-/WLAN-Ausfall
15. LOGO!-Power-Cycle
16. Etikett und VE-Historie

Jeder Prüfschritt kann als offen, bestanden, nicht bestanden oder nicht anwendbar dokumentiert werden. Notiz und Zeitstempel werden gespeichert.

## CSV-Protokoll

Das maschinenbezogene Prüfprotokoll wird unter

```text
Dokumente\Partcounter\Inbetriebnahme
```

abgelegt. Der Export enthält Hardwareprofil, aktuellen Diagnosestand und die vollständige Prüfliste.

## Tab „Rolloutstatus 30 Maschinen“

Die Rolloutübersicht zeigt alle Stationen in einer Tabelle:

- Maschinenname
- Endpunkt
- Live-Verbindung
- letzte Antwort
- Freigabestatus
- Fortschritt der Prüfliste
- Hardwareprofil
- ErrorCode

Die Übersicht kann ebenfalls als CSV exportiert werden.

## Empfohlener Ablauf für Station 01

1. LOGO!-Programm `Partcounter_LOGO_V001` in LOGO! Soft Comfort aufbauen und simulieren.
2. 24-V-Versorgung und I1 prüfen.
3. Q1 zunächst nur mit Koppelrelais testen, Ventil noch nicht anschließen.
4. Direkte Modbus-Leseprobe durchführen.
5. Echtbetrieb in Partcounter aktivieren und Heartbeats beobachten.
6. Sequenz-/Ack-Handshake prüfen.
7. I1-Zählung mit realem Maschinensignal prüfen.
8. Q1-Impulszeiten 50, 750 und 5000 ms am Koppelrelais messen.
9. Festo-Ventil anschließen und mechanischen Wechsel prüfen.
10. automatischen und manuellen VE-Abschluss testen.
11. PC-/WLAN-Ausfalltest durchführen.
12. Power-Cycle-Test durchführen.
13. Etikettierung und VE-Historie prüfen.
14. alle Prüfschritte dokumentieren.
15. erst danach Freigabestatus `Released` setzen.

## Sicherheitsgrenze

Partcounter und die Siemens LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztüren, Maschinenfreigaben und alle sonstigen Safety-Funktionen verbleiben vollständig in den dafür vorgesehenen sicheren Steuerungskreisen.

Ein Diagnose- oder Softwarestatus darf niemals als Ersatz für eine reale elektrische bzw. mechanische Abnahme verwendet werden.
