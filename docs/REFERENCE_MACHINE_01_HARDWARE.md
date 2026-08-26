# Partcounter – Referenzmaschine 01 / Hardware-Festlegung

**Revision:** R001.7  
**LOGO!-Programm:** `Partcounter_LOGO_V001`  
**Status:** verbindliche Basis für die erste reale Teststation

## 1. Siemens LOGO!

| Merkmal | Festlegung |
|---|---|
| Hersteller | Siemens |
| Bestellnummer | `6ED1052-2MD08-0BA2` |
| Bezeichnung | LOGO! 12/24RCEo |
| Generation | LOGO! 8.4 |
| Display | ohne Display (`o`) |
| Versorgung | 12/24 V DC |
| zulässiger Versorgungsbereich | 10,8…28,8 V DC |
| Digitaleingänge | 8 DI, davon 4 als AI 0…10 V nutzbar |
| Digitalausgänge | 4 Relaisausgänge |
| Kurzschlussschutz Ausgänge | nein; externe Absicherung erforderlich |
| max. Relaislast ohmsch | 10 A |
| max. Relaislast induktiv | 3 A |
| Netzwerk | Ethernet |
| LOGO! Soft Comfort | V8.4 oder höher |

Herstellerabgleich: Siemens führt die Bestellnummer `6ED1052-2MD08-0BA2` als LOGO! 12/24RCEo mit 12/24-V-DC-Versorgung, Relaisausgängen und Ethernet. Das technische Datenblatt nennt für die Relaiskontakte maximal 10 A ohmsche bzw. 3 A induktive Last und fordert eine externe Absicherung.

## 2. Reale I/O-Belegung Teststation

| Signal | LOGO!-Klemme | Elektrische Festlegung | Verwendung |
|---|---|---|---|
| Maschinenzyklus | I1 | 24 V DC | positive Flanke = genau ein gültiger Produktionszyklus |
| Wechsler-Endlage | I2 | aktuell nicht belegt | optional vorbereitet; später parametrierbar aktivierbar |
| Handquittierung | I3 | optional | derzeit nicht zwingend erforderlich |
| VE-Wechsler | Q1 | Relaisausgang, schaltet 24 V DC | pneumatisches Ventil |
| Wechselanzeige | Q2 | optional | optische Anzeige VE-Wechsel |
| Sammelstörung | Q3 | optional | technische Störung / Warnsignal |

## 3. Zyklusimpuls I1

An I1 liegt ein **24-V-DC-Signal** der Spritzgussmaschine an.

Verbindliche Auswertung:

- es zählt ausschließlich die positive Flanke,
- eine Flanke entspricht genau einem Maschinenzyklus,
- die Flankenerkennung erfolgt vor der Auto-/Pause-/Wechselfreigabe,
- eine Pause darf beim Wiederanlauf und noch anstehendem HIGH-Pegel keinen künstlichen Zusatzzyklus erzeugen,
- bei der Inbetriebnahme werden Pulsbreite und Signalqualität real gemessen,
- falls notwendig wird eine LOGO!-seitige Entprell-/Mindestpulslogik ergänzt.

## 4. Pneumatikventil Q1

Die Ventilspule arbeitet mit **24 V DC**.

### Standardbeschaltung

Empfohlene Grundschaltung:

```text
+24 V DC
   |
   +-- externe Sicherung / abgesicherter Steuerstromkreis
   |
   +-- Q1 Relaiskontakt LOGO!
   |
   +-- Ventilspule 24 V DC
   |
  0 V
```

Bei einer DC-Magnetspule ist eine geeignete **Freilauf-/Entstörbeschaltung direkt an der Spule** vorzusehen, sofern diese nicht bereits im Ventilstecker oder Ventil integriert ist.

### Direkt oder über Koppelrelais

Die LOGO! besitzt Relaisausgänge. Laut technischem Datenblatt liegt die maximale Kontaktbelastung bei 3 A für induktive Last. Das bedeutet jedoch nicht automatisch, dass jede 24-V-Ventilspule ohne weitere Prüfung direkt geschaltet werden soll.

Für die Freigabe werden noch erfasst:

- Nennstrom oder Nennleistung der Ventilspule bei 24 V DC,
- Einschalt-/Anzugsstrom, sofern angegeben,
- integrierte Schutzbeschaltung ja/nein,
- erwartete Schalthäufigkeit und gewünschte Lebensdauer.

**Bevorzugter Industriestandard für Partcounter:** Q1 steuert ein 24-V-DC-Koppel-/Interface-Relais an, das wiederum die Ventilspule schaltet. Das reduziert die elektrische und mechanische Belastung des fest eingebauten LOGO!-Relaiskontakts und vereinfacht Wartung und Austausch. Eine direkte Ventilansteuerung bleibt zulässig, wenn die realen Spulendaten, Schutzbeschaltung und Schalthäufigkeit dies eindeutig erlauben.

## 5. Ventilimpuls

Der Ventilimpuls ist vom Bediener in Partcounter einstellbar.

Verbindlicher Bereich:

```text
Minimum:  50 ms
Maximum: 5000 ms
Raster:    10 ms
Standard: 750 ms
```

Die PC-Anwendung arbeitet in Millisekunden. Modbus HR7 überträgt den Wert in 10-ms-Einheiten:

```text
ValvePulse10Ms = ValvePulseMs / 10
```

Beispiele:

| Eingabe PC | HR7 |
|---:|---:|
| 50 ms | 5 |
| 100 ms | 10 |
| 250 ms | 25 |
| 500 ms | 50 |
| 750 ms | 75 |
| 1000 ms | 100 |
| 2500 ms | 250 |
| 5000 ms | 500 |

Werte kleiner 50 ms, größer 5000 ms oder nicht im 10-ms-Raster werden nicht an die LOGO! übertragen.

## 6. Endlagenrückmeldung

Die erste Testmaschine besitzt **keine Endlagenrückmeldung** des Verpackungswechslers.

Daher gilt für Station 01:

```text
EndPositionMonitoring = OFF
I2 = unbelegt / ignoriert
```

Die Funktion bleibt jedoch im Standardprogramm `Partcounter_LOGO_V001` vorbereitet. Für spätere Maschinen kann sie maschinenbezogen aktiviert werden:

```text
EndPositionMonitoring = ON
I2 = bestätigte Wechsler-Endlage
Timeout -> ErrorCode 10
```

Damit bleibt dasselbe LOGO!-Grundprogramm für Maschinen mit und ohne Endlagensensor verwendbar.

## 7. Verhalten ohne Endlagensensor

Bei Station 01 gilt ein VE-Wechsel logisch als ausgelöst, wenn:

1. der VE-Abschluss vollständig gespeichert wurde,
2. `CompletionSequence` erhöht wurde,
3. Q1 den parametrierten Ventilimpuls ausgeführt hat.

Es gibt ohne I2 **keine physische Bestätigung**, dass die Mechanik tatsächlich ihre Endposition erreicht hat. Deshalb ist die mechanische Zuverlässigkeit des Wechslers Bestandteil der Erstabnahme und des Langzeittests.

## 8. Parametrierbarer Maschinenstandard

Folgende Parameter sollen künftig pro Maschine im Partcounter-Maschinenstamm gespeichert werden:

| Parameter | Station 01 |
|---|---|
| LOGO!-Typ | 6ED1052-2MD08-0BA2 |
| LOGO!-Variante | 12/24RCEo, Relaisausgänge |
| Versorgung | 24 V DC |
| Zykluseingang | I1 / 24 V DC |
| Ventilausgang | Q1 / Relais |
| Ventilspule | 24 V DC |
| Ventilimpuls | 50…5000 ms, 10-ms-Raster |
| Standardimpuls | 750 ms |
| Endlagenüberwachung | Nein |
| Endlageneingang | I2 optional |
| Endlagentimeout | nur bei aktivierter Überwachung |

## 9. Noch offener Hardwarewert

Für die endgültige Freigabe der Q1-Beschaltung fehlt nur noch:

**Stromaufnahme bzw. Leistung der 24-V-Ventilspule.**

Beispiel: Bei einer Ventilspule mit 4,8 W bei 24 V DC beträgt der Nennstrom 0,2 A. Dieser Wert ist aber für das tatsächlich verwendete Ventil anhand Typenschild oder Datenblatt festzustellen und darf nicht angenommen werden.

## 10. Freigabestatus

Mit den jetzt bekannten Daten ist die elektrische Grundarchitektur von Referenzmaschine 01 festgelegt. Offen bleiben nur die reale Spulenstromaufnahme und anschließend die praktische Verdrahtungs-/Funktionsprüfung.

Partcounter und die Siemens LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztüren, Maschinenfreigaben und andere Safety-Funktionen verbleiben vollständig in den vorhandenen sicheren Maschinenkreisen.
