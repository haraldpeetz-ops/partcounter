# Partcounter V2 – Modbus TCP Register Map

**Protokollversion:** 2  
**Partcounter Revision:** R001.7  
**Modbus TCP:** Port 502  
**PC:** Client/Master  
**Siemens LOGO!:** Server/Slave  
**Unit ID:** standardmäßig 1

> NModbus adressiert Holding Register nullbasiert. In der folgenden Tabelle ist `HR1` die fachliche erste Registerposition und entspricht in der PC-Software Adresse `0`. Auf LOGO!-Seite beginnt die V2-VM-Zuordnung bei `VW0`.

## Engineering-Grundsätze V2

Die LOGO! verwendet für Zählungen native Vor-/Rückwärtszähler. Der Gesamtzykluszähler wird als DWord übertragen; die PC-Anwendung berechnet Teilemengen mit der Kavitätenzahl. Damit wird die 16-Bit-Grenze der allgemeinen LOGO!-Analogarithmetik nicht für Teilemultiplikationen verwendet.

Der Siemens-Vor-/Rückwärtszähler besitzt zwar einen Bereich bis 999999 und kann Zählerstand sowie Ein-/Ausschaltschwellen per Parameter-VM-Mapping als `VD` bereitstellen. Partcounter begrenzt eine einzelne VE bewusst auf **32767 Zyklen**. Grund: der abgeschlossene VE-Zähler muss innerhalb der LOGO! vor dem Reset sicher gepuffert werden; Werte im positiven 16-Bit-Bereich lassen sich ohne 32-Bit-Hilfslogik zuverlässig verarbeiten.

Weitere LOGO!-interne Sequenz-/Heartbeat-Werte werden ebenfalls auf 1…32767 begrenzt und danach auf 1 zurückgeführt.

## PC → LOGO! Konfiguration HR1–HR12

| HR | Offset | LOGO VM | Datentyp | Bedeutung |
|---:|---:|---|---|---|
| HR1 | 0 | VW0 | UINT16 | ProtocolVersion = 2 |
| HR2 | 1 | VW2 | UINT16 | CommandSequence 1…32767 |
| HR3 | 2 | VW4 | UINT16 | CommandWord |
| HR4 | 3 | VW6 | UINT16 | ActiveCavities 1–64 |
| HR5 | 4 | VW8 | UINT16 | TargetPartsPerVE High Word |
| HR6 | 5 | VW10 | UINT16 | TargetPartsPerVE Low Word |
| HR7 | 6 | VW12 | UINT16 | ValvePulse10Ms, 5…500 = 50…5000 ms |
| HR8 | 7 | VW14 | UINT16 | JobId High Word |
| HR9 | 8 | VW16 | UINT16 | JobId Low Word |
| HR10 | 9 | VW18 | UINT16 | TargetCyclesPerVE High Word |
| HR11 | 10 | VW20 | UINT16 | TargetCyclesPerVE Low Word |
| HR12 | 11 | VW22 | UINT16 | PC Heartbeat 1…32767 |

`VW24` bis `VW36` bleiben für spätere Protokollerweiterungen reserviert.

### Direkte Parameter-VM-Zuordnung des VE-Zählers

Der Ein-Schwellwert (`On Threshold`) des LOGO!-Vor-/Rückwärtszählers wird als DWord direkt auf **VD18** gemappt. Damit bilden HR10/HR11 gemeinsam den dynamischen Zielzykluswert des Zählerblocks.

Für Partcounter V2 gilt:

```text
1 <= TargetCyclesPerVE <= 32767
TargetCyclesPerVE = ceil(TargetPartsPerVE / ActiveCavities)
EffectiveVE       = TargetCyclesPerVE × ActiveCavities
```

Beispiel: **1000 Teile / 64 Kavitäten = 16 Zielzyklen = 1024 effektive Teile**.

### Ventilzeit HR7

Die PC-Oberfläche und das interne Datenmodell arbeiten weiter in Millisekunden. Vor dem Modbus-Schreiben rechnet Partcounter auf die fest konfigurierte LOGO!-Zeitbasis von 10 ms um:

```text
ValvePulse10Ms = ValvePulseMs / 10
```

Beispiel: `750 ms -> HR7 = 75`.

Zulässig sind 50…5000 ms in 10-ms-Schritten. Der verwendete LOGO!-Zeitbaustein wird auf die feste Zeitbasis **10 Millisekunden** eingestellt und sein Zeitparameter per Parameter-VM-Mapping an VW12 gebunden.

## CommandWord HR3

| Bit | Maske | Bedeutung |
|---:|---:|---|
| 0 | 0x0001 | Automatic enabled |
| 1 | 0x0002 | Reset / neuer Auftrag |
| 2 | 0x0004 | Manueller VE-Wechsel |
| 3 | 0x0008 | Alarm quittieren |
| 4 | 0x0010 | Zählung pausieren |

One-Shot-Befehle werden nur bearbeitet, wenn `CommandSequence != AckSequence`. Nach Bearbeitung übernimmt die LOGO! die Sequenz in `AckSequence`.

Partcounter verwendet Sequenznummern 1…32767. Nach 32767 folgt wieder 1. Beim PC-Neustart liest Partcounter zuerst den aktuellen `AckSequence`-Wert und beginnt mit dem darauffolgenden Wert. Damit wird weder die LOGO!-16-Bit-Grenze verletzt noch der erste Befehl nach Neustart als altes Duplikat verworfen.

## LOGO! → PC Status HR20–HR37

| HR | LOGO VM | Offset | Datentyp | Bedeutung |
|---:|---|---:|---|---|
| HR20 | VW38 | 0 | UINT16 | ProtocolVersion = 2 |
| HR21 | VW40 | 1 | UINT16 | StatusWord |
| HR22 | VW42 | 2 | UINT16 | CurrentVECycles High Word |
| HR23 | VW44 | 3 | UINT16 | CurrentVECycles Low Word |
| HR24 | VW46 | 4 | UINT16 | TotalCycles High Word |
| HR25 | VW48 | 5 | UINT16 | TotalCycles Low Word |
| HR26 | VW50 | 6 | UINT16 | CurrentVENumber |
| HR27 | VW52 | 7 | UINT16 | CompletedVEs |
| HR28 | VW54 | 8 | UINT16 | LastCompletedVECycles High Word, V2 = 0 |
| HR29 | VW56 | 9 | UINT16 | LastCompletedVECycles Low Word |
| HR30 | VW58 | 10 | UINT16 | AckSequence 0…32767 |
| HR31 | VW60 | 11 | UINT16 | ActiveCavitiesEcho |
| HR32 | VW62 | 12 | UINT16 | LastCompletedVENumber |
| HR33 | VW64 | 13 | UINT16 | CompletionSequence 0…32767 |
| HR34 | VW66 | 14 | UINT16 | LOGO Heartbeat 1…32767 |
| HR35 | VW68 | 15 | UINT16 | ErrorCode |
| HR36 | VW70 | 16 | UINT16 | LastCompletionReason |
| HR37 | VW72 | 17 | UINT16 | LastCompletedCavities |

### Parameter-VM-Zuordnung der Zähler

- `CurrentVECycles.Counter` → **VD42** (HR22/HR23)
- `TotalCycles.Counter` → **VD46** (HR24/HR25)
- `TargetCyclesPerVE / CurrentVECycles.On Threshold` → **VD18** (HR10/HR11)

Da `CurrentVECycles <= 32767`, bleibt dessen High Word immer 0. Der Gesamtzykluszähler darf den 16-Bit-Bereich überschreiten und wird deshalb vollständig als DWord ausgewertet.

`LastCompletedVECycles` wird beim VE-Abschluss vor dem Reset in einem 16-Bit-Arbeitswert gespeichert. HR28 bleibt 0; HR29 enthält den gepufferten Zykluswert. Die DWord-Darstellung bleibt bewusst erhalten, damit die PC-Struktur einheitlich bleibt und eine spätere Protokollerweiterung möglich ist.

## StatusWord HR21

| Bit | Maske | Bedeutung |
|---:|---:|---|
| 0 | 0x0001 | LOGO bereit |
| 1 | 0x0002 | Automatik aktiv |
| 2 | 0x0004 | VE-Wechsel läuft |
| 3 | 0x0008 | Alarm |
| 4 | 0x0010 | Zykluseingang aktiv |
| 5 | 0x0020 | PC-Heartbeat steht |

Ein stehender PC-Heartbeat ist Diagnose, kein Produktions-Stopp-Befehl.

## ErrorCode HR35

| Wert | Bedeutung |
|---:|---|
| 0 | kein Fehler |
| 1 | falsche Protokollversion |
| 2 | ungültige Kavitätenzahl |
| 3 | TargetPartsPerVE = 0 |
| 4 | TargetCyclesPerVE außerhalb 1…32767 |
| 5 | Ventilimpuls außerhalb 50…5000 ms bzw. kein 10-ms-Raster |
| 10 | optionale Wechsler-Endlage nicht rechtzeitig erreicht |
| 30 | interner ungültiger Ablaufzustand |

## LastCompletionReason HR36

| Wert | Bedeutung |
|---:|---|
| 0 | unbekannt / noch keine VE |
| 1 | automatisch voll |
| 2 | manueller VE-Wechsel |

## VE-Abschluss-Handshake

Vor dem Zurücksetzen des aktuellen VE-Zählers muss die LOGO! in definierter Reihenfolge:

1. `LastCompletedVECycles = CurrentVECycles` speichern.
2. `LastCompletedCavities = ActiveCavitiesLatched` speichern.
3. `LastCompletedVENumber = CurrentVENumber` speichern.
4. `LastCompletionReason` setzen.
5. `CompletedVEs` erhöhen.
6. `CompletionSequence` erhöhen; nach 32767 auf 1 zurückführen.
7. VE-Wechselstatus setzen und den pneumatischen Impuls ausführen.
8. `CurrentVENumber` erhöhen.
9. `CurrentVECycles` zurücksetzen.

Der PC erkennt eine neue fertige VE an jeder Änderung von `CompletionSequence` und berechnet:

```text
LastCompletedVEQuantity = LastCompletedVECycles × LastCompletedCavities
```

## Heartbeat-Verhalten

- PC schreibt zyklisch Werte 1…32767 in HR12 und springt danach auf 1.
- LOGO! schreibt zyklisch Werte 1…32767 in HR34 und springt danach auf 1.
- Ein nicht mehr wechselnder Wert erzeugt Kommunikationsdiagnose.
- **Ein PC-/WLAN-Ausfall darf die lokale Zählung und den automatischen VE-Wechsel nicht stoppen.**
- Sicherheitsfunktionen der Maschine dürfen niemals von diesem Protokoll abhängen.

## 32-Bit-Werte

Partcounter V2 überträgt DWord-Werte als High Word, danach Low Word:

```text
value = (high << 16) | low
```

Die VM-DWord-Belegung folgt der Siemens-Darstellung mit höchstwertigem Byte zuerst. Die tatsächliche Zuordnung wird bei der ersten Testmaschine zusätzlich mit bekannten Testwerten verifiziert.

## Betriebsgrenzen V2

- Kavitäten: 1…64
- Zyklen je VE: 1…32767
- Ventilimpuls: 50…5000 ms in 10-ms-Schritten
- CommandSequence: 1…32767, zyklisch
- Heartbeat: 1…32767, zyklisch
- Gesamtzykluszähler LOGO!: bis 999999 je Auftrag; größere Produktionslose müssen in mehrere LOGO!-Aufträge segmentiert werden, solange kein erweitertes Zählkonzept freigegeben ist.

## Kompatibilität

Partcounter R001.7 erwartet `ProtocolVersion = 2`. Ein älteres LOGO!-Programm mit Protokoll V1 wird bewusst abgewiesen. Vor Produktivbetrieb müssen PC-Software und LOGO!-Programm dieselbe Protokollversion verwenden.
