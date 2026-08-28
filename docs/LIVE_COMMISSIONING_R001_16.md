# Partcounter R001.16 – Reale Live-Abnahme Referenzmaschine 01

Stand: 28.08.2026

## Ziel

R001.16 ergänzt den bereits vorhandenen geschützten Bereich **Inbetriebnahme / Diagnose** um eine read-only Live-Abnahmemessung für die reale Referenzmaschine 01. Die Funktion dient dazu, die Modbus-V2-Kommunikation und die Zähl-/VE-Ereignisse während der tatsächlichen Inbetriebnahme objektiv mitzuschneiden.

## Sicherheits- und Architekturprinzip

Die neue Live-Abnahme erzeugt **keinen zweiten Steuerpfad** zur Siemens LOGO! und schreibt selbst keine Modbus-Register. Sie beobachtet ausschließlich die vorhandene `MachineFleetService`-Diagnose der regulären Partcounter-Kommunikation.

Damit gilt weiterhin:

- Auftragsparameter werden nur über die bestehende, bereits implementierte Betriebslogik geschrieben.
- Q1 wird nicht direkt aus dem Inbetriebnahme-Messmodul angesteuert.
- Ein Betriebsmoduswechsel wird nicht automatisch durchgeführt.
- Safety-Funktionen bleiben vollständig außerhalb von Partcounter/Modbus.
- Elektrische Signalpegel, reale Ventilbewegung, Koppelrelais, Absicherung und mechanischer Kistenwechsel müssen vor Ort geprüft werden.

## Bedienweg

1. Administration entsperren.
2. `Inbetriebnahme / Diagnose` öffnen.
3. Unterreiter `Live-Abnahme R001.16` wählen.
4. Referenzmaschine auswählen, bevorzugt M01.
5. Echtbetrieb über die vorhandene geschützte Betriebsmodus-Umschaltung aktivieren.
6. Preflight prüfen: ONLINE, aktuelle LOGO!-Antwort und Command/Ack-Synchronität.
7. `Messung starten` wählen.
8. Reale Testfolge an der Maschine durchführen.
9. `Messung stoppen` wählen.
10. Zusammenfassung prüfen und CSV exportieren.
11. Optional `Messdaten in Prüfnotizen` wählen. Dabei werden nur Notizen ergänzt; Prüfergebnisse werden nie automatisch auf BESTANDEN gesetzt.

## Erfasste Werte

Je Messpunkt werden – soweit die Fleet-Diagnose verfügbar ist – gespeichert:

- Zeitstempel
- Verbindungszustand
- PC-Heartbeat
- LOGO!-Heartbeat
- lokale CommandSequence
- AckSequence und Synchronitätsstatus
- StatusWord
- ErrorCode
- CompletionSequence
- aktive Kavitäten (Echo)
- aktuelle Teile
- Gesamtzyklen
- aktuelle VE-Nummer
- abgeschlossene VEs
- Zeitpunkt des zugrunde liegenden LOGO!-Snapshots
- Kommunikationsmeldung

Das UI hält maximal 500 Messpunkte sichtbar. Die Messserie selbst ist auf 20.000 Messpunkte begrenzt, um unbeabsichtigtes Dauerlogging zu vermeiden.

## Automatische Auswertung

Nach und während einer Messung werden unter anderem berechnet:

- Online-/Offline-/Fehlermesspunkte
- erkannte Verbindungsabbrüche und Wiederkehr
- Änderung von PC- und LOGO!-Heartbeat
- Anzahl unsynchroner Command/Ack-Messpunkte
- Delta des Gesamtzykluszählers
- Delta der CompletionSequence
- Delta der abgeschlossenen VEs
- Anzahl Alarm-Messpunkte
- Anzahl `PC heartbeat stale`-Messpunkte
- Anzahl Stichproben mit aktivem I1-Statusbit

## Grenzen der automatischen Bewertung

Das Sampling erfolgt im 750-ms-Raster. Deshalb darf aus dem I1-Statusbit allein **nicht** abgeleitet werden, dass jede kurze Maschinenflanke gesehen wurde. Für `I1-02` bleibt die Vor-Ort-Beobachtung notwendig: ein realer Maschinenzyklus muss genau einen Zählschritt erzeugen.

Ebenso beweist eine Änderung der `CompletionSequence` nicht die reale Impulsdauer von Q1 und nicht den mechanischen Kistenwechsel. Diese Punkte bleiben Teil der elektrischen/mechanischen Abnahme.

## Prüfnotizen

Die Funktion kann Messdaten als Evidenz in folgende vorhandene Prüfpunkte übernehmen:

- `MOD-01`
- `HB-01`
- `CMD-01`
- `I1-02`
- `VE-01`
- `COM-01`

Vorhandene Notizen werden nicht überschrieben. Die R001.16-Evidenz wird mit Zeitstempel angehängt. Ergebnis und Prüfzeitpunkt bleiben unverändert.

## CSV-Export

Ablage:

`%USERPROFILE%\Documents\Partcounter\Inbetriebnahme`

Dateiname:

`Partcounter_R00116_LiveAbnahme_Mxx_YYYYMMDD_HHMMSS.csv`

Der Export enthält eine Zusammenfassung sowie sämtliche Messpunkte der Session.

## Empfohlene reale Testfolge für M01

1. LOGO! und PC einschalten; Q1 muss beim Start AUS bleiben.
2. Modbus-Session auf ONLINE bringen.
3. Heartbeats mindestens 30 Sekunden beobachten.
4. Einen Testauftrag mit bekannter Kavitätenzahl starten.
5. Mindestens 10 reale Maschinenzyklen erzeugen und Gesamtzyklus-Delta vergleichen.
6. Eine kurze VE einstellen und automatischen VE-Abschluss provozieren.
7. CompletionSequence, Historie, Etikett und realen Q1-/Ventilimpuls prüfen.
8. PC/WLAN bewusst unterbrechen, während die LOGO! weiter reale Zyklusimpulse erhält.
9. Prüfen, dass die LOGO! lokal weiterzählt und ein fälliger VE-Wechsel lokal erfolgen kann.
10. Verbindung wiederherstellen und Synchronisation/Doppelbuchungen prüfen.
11. Abschließend Power-Cycle der LOGO! gemäß Freigabekonzept durchführen.

## Versionsbezug

- Software: Partcounter R001.16 / 0.1.16
- Basis: R001.15 Production Readiness
- SPS-Protokoll: Modbus Protocol V2 unverändert
- LOGO!-Programm: Partcounter_LOGO_V001 unverändert
