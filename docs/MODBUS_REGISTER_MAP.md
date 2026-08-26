# Partcounter V2 – Modbus TCP Register Map

**Protokollversion:** 2  
**Partcounter Revision:** R001.7  
**Modbus TCP:** Port 502  
**PC:** Client/Master  
**Siemens LOGO!:** Server/Slave  
**Unit ID:** standardmäßig 1

> NModbus adressiert Holding Register nullbasiert. In der folgenden Tabelle ist `HR1` die fachliche erste Registerposition und entspricht in der PC-Software Adresse `0`. Auf LOGO!-Seite beginnt die V2-VM-Zuordnung bei `VW0`.

## Warum Protokoll V2?

LOGO!-Auf/Ab-Zähler können bis 999999 zählen und ihr Zählerstand kann per Parameter-VM-Mapping als DWORD bereitgestellt werden. Die allgemeine LOGO!-Analogarithmetik ist dagegen auf 16-Bit-Integerwerte beschränkt. Deshalb überträgt V2 die **Zykluszähler** und berechnet die Teilezahl auf dem PC:

```text
CurrentParts             = CurrentVECycles × ActiveCavitiesEcho
LastCompletedVEQuantity  = LastCompletedVECycles × LastCompletedCavities
```

Damit muss die LOGO! keine potenziell überlaufende Multiplikation `Zyklen × Kavitäten` ausführen.

## PC → LOGO! Konfiguration HR1–HR12

| HR | Offset | LOGO VM | Datentyp | Bedeutung |
|---:|---:|---|---|---|
| HR1 | 0 | VW0 | UINT16 | ProtocolVersion = 2 |
| HR2 | 1 | VW2 | UINT16 | CommandSequence |
| HR3 | 2 | VW4 | UINT16 | CommandWord |
| HR4 | 3 | VW6 | UINT16 | ActiveCavities 1–64 |
| HR5 | 4 | VW8 | UINT16 | TargetPartsPerVE High Word |
| HR6 | 5 | VW10 | UINT16 | TargetPartsPerVE Low Word |
| HR7 | 6 | VW12 | UINT16 | ValvePulseMs |
| HR8 | 7 | VW14 | UINT16 | JobId High Word |
| HR9 | 8 | VW16 | UINT16 | JobId Low Word |
| HR10 | 9 | VW18 | UINT16 | TargetCyclesPerVE High Word |
| HR11 | 10 | VW20 | UINT16 | TargetCyclesPerVE Low Word |
| HR12 | 11 | VW22 | UINT16 | PC Heartbeat |

`VW24` bis `VW36` bleiben für spätere Protokollerweiterungen reserviert.

### Zielzyklen

Der PC berechnet vor der Übertragung:

```text
TargetCyclesPerVE = ceil(TargetPartsPerVE / ActiveCavities)
EffectiveVE       = TargetCyclesPerVE × ActiveCavities
```

Beispiel: **1.000 Teile / 64 Kavitäten = 16 Zyklen = 1.024 Teile tatsächlich**.

Für V2 gilt `TargetCyclesPerVE = 1…999999`.

## CommandWord HR3

| Bit | Maske | Bedeutung |
|---:|---:|---|
| 0 | 0x0001 | Automatic enabled |
| 1 | 0x0002 | Reset / neuer Auftrag |
| 2 | 0x0004 | Manueller VE-Wechsel |
| 3 | 0x0008 | Alarm quittieren |
| 4 | 0x0010 | Zählung pausieren |

`CommandSequence` wird bei jedem neuen Befehl erhöht. One-Shot-Bits dürfen von der LOGO! nur einmal verarbeitet werden, wenn sich die Sequenz geändert hat. Danach schreibt die LOGO! die verarbeitete Sequenz als `AckSequence` zurück.

Partcounter R001.7 liest bei einem PC-Neustart zuerst `AckSequence` und setzt seine lokale Sequenz auf diesen Stand. Dadurch kann der erste Befehl nach dem Neustart nicht versehentlich dieselbe Sequenznummer wie der letzte bereits bearbeitete Befehl erhalten.

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
| HR28 | VW54 | 8 | UINT16 | LastCompletedVECycles High Word |
| HR29 | VW56 | 9 | UINT16 | LastCompletedVECycles Low Word |
| HR30 | VW58 | 10 | UINT16 | AckSequence |
| HR31 | VW60 | 11 | UINT16 | ActiveCavitiesEcho |
| HR32 | VW62 | 12 | UINT16 | LastCompletedVENumber |
| HR33 | VW64 | 13 | UINT16 | CompletionSequence |
| HR34 | VW66 | 14 | UINT16 | LOGO Heartbeat |
| HR35 | VW68 | 15 | UINT16 | ErrorCode |
| HR36 | VW70 | 16 | UINT16 | LastCompletionReason |
| HR37 | VW72 | 17 | UINT16 | LastCompletedCavities |

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
| 4 | TargetCyclesPerVE außerhalb 1…999999 |
| 5 | ValvePulseMs außerhalb 50…5000 ms |
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

1. `LastCompletedVECycles = CurrentVECycles` setzen.
2. `LastCompletedCavities = ActiveCavitiesLatched` setzen.
3. `LastCompletedVENumber = CurrentVENumber` setzen.
4. `LastCompletionReason` setzen.
5. `CompletedVEs` erhöhen.
6. `CompletionSequence` erhöhen.
7. pneumatischen Wechsel ausführen bzw. Wechselstatus setzen.
8. `CurrentVENumber` erhöhen.
9. `CurrentVECycles` auf 0 setzen.

Der PC erkennt eine neue fertige VE an der Änderung von `CompletionSequence` und rekonstruiert die Istmenge sicher aus `LastCompletedVECycles × LastCompletedCavities`.

## Heartbeat-Verhalten

- PC erhöht `HR12` zyklisch.
- LOGO! erhöht `HR34` zyklisch.
- Ein eingefrorener Heartbeat erzeugt Kommunikationsstatus/Diagnose.
- **Ein PC-/WLAN-Ausfall darf die lokale Zählung und den automatischen VE-Wechsel nicht stoppen.**
- Sicherheitsfunktionen der Maschine dürfen niemals von diesem Protokoll abhängen.

## 32-Bit-Werte

32-Bit-Werte werden als High Word, danach Low Word übertragen:

```text
value = (high << 16) | low
```

Diese Word-Reihenfolge ist für Partcounter V2 verbindlich.

## Kompatibilität

Partcounter R001.7 erwartet `ProtocolVersion = 2`. Ein älteres LOGO!-Programm mit Protokoll V1 wird bewusst abgewiesen. Vor Produktivbetrieb müssen PC-Software und LOGO!-Programm dieselbe Protokollversion verwenden.
