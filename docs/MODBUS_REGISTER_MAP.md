# Partcounter V1 – Modbus TCP Register Map

**Protokollversion:** 1  
**Modbus TCP:** Port 502  
**PC:** Client/Master  
**Siemens LOGO!:** Server/Slave  
**Unit ID:** standardmäßig 1

> NModbus adressiert Holding Register nullbasiert. In der folgenden Tabelle ist `HR1` die fachliche erste Registerposition und entspricht in der PC-Software Adresse `0`. Die konkrete LOGO!-VM/VW-Zuordnung ist bei der Inbetriebnahme in LOGO! Soft Comfort gegen die verwendete Hardware-/Firmwareversion zu prüfen.

## PC → LOGO! Konfiguration HR1–HR12

| HR | Offset | Datentyp | Bedeutung |
|---:|---:|---|---|
| HR1 | 0 | UINT16 | ProtocolVersion = 1 |
| HR2 | 1 | UINT16 | CommandSequence |
| HR3 | 2 | UINT16 | CommandWord |
| HR4 | 3 | UINT16 | ActiveCavities 1–64 |
| HR5 | 4 | UINT16 | TargetPartsPerVE High Word |
| HR6 | 5 | UINT16 | TargetPartsPerVE Low Word |
| HR7 | 6 | UINT16 | ValvePulseMs |
| HR8 | 7 | UINT16 | JobId High Word |
| HR9 | 8 | UINT16 | JobId Low Word |
| HR10 | 9 | UINT16 | TargetCyclesPerVE High Word |
| HR11 | 10 | UINT16 | TargetCyclesPerVE Low Word |
| HR12 | 11 | UINT16 | PC Heartbeat |

### Zielzyklen

Der PC berechnet vor der Übertragung:

```text
TargetCyclesPerVE = ceil(TargetPartsPerVE / ActiveCavities)
EffectiveVE       = TargetCyclesPerVE × ActiveCavities
```

Damit muss die LOGO! keine Rundungsdivision ausführen. Beispiel 1.000 Teile / 64 Kavitäten → 16 Zielzyklen → 1.024 Teile.

## CommandWord HR3

| Bit | Maske | Bedeutung |
|---:|---:|---|
| 0 | 0x0001 | Automatic enabled |
| 1 | 0x0002 | Reset / neuer Auftrag |
| 2 | 0x0004 | Manueller VE-Wechsel |
| 3 | 0x0008 | Alarm quittieren |
| 4 | 0x0010 | Zählung pausieren |

`CommandSequence` wird bei jedem neuen Befehl erhöht. One-Shot-Bits wie Reset oder manueller Wechsel dürfen von der LOGO! nur einmal verarbeitet werden, wenn sich die Sequenz geändert hat. Danach schreibt die LOGO! die verarbeitete Sequenz als `AckSequence` zurück.

## LOGO! → PC Status HR20–HR36

| HR | Offset im Statusblock | Datentyp | Bedeutung |
|---:|---:|---|---|
| HR20 | 0 | UINT16 | ProtocolVersion = 1 |
| HR21 | 1 | UINT16 | StatusWord |
| HR22 | 2 | UINT16 | CurrentParts High Word |
| HR23 | 3 | UINT16 | CurrentParts Low Word |
| HR24 | 4 | UINT16 | TotalCycles High Word |
| HR25 | 5 | UINT16 | TotalCycles Low Word |
| HR26 | 6 | UINT16 | CurrentVENumber |
| HR27 | 7 | UINT16 | CompletedVEs |
| HR28 | 8 | UINT16 | LastCompletedVEQuantity High Word |
| HR29 | 9 | UINT16 | LastCompletedVEQuantity Low Word |
| HR30 | 10 | UINT16 | AckSequence |
| HR31 | 11 | UINT16 | ActiveCavitiesEcho |
| HR32 | 12 | UINT16 | LastCompletedVENumber |
| HR33 | 13 | UINT16 | CompletionSequence |
| HR34 | 14 | UINT16 | LOGO Heartbeat |
| HR35 | 15 | UINT16 | ErrorCode |
| HR36 | 16 | UINT16 | LastCompletionReason |

## StatusWord HR21

| Bit | Maske | Bedeutung |
|---:|---:|---|
| 0 | 0x0001 | LOGO bereit |
| 1 | 0x0002 | Automatik aktiv |
| 2 | 0x0004 | VE-Wechsel läuft |
| 3 | 0x0008 | Alarm |
| 4 | 0x0010 | Zykluseingang aktiv |

## LastCompletionReason HR36

| Wert | Bedeutung |
|---:|---|
| 0 | unbekannt / noch keine VE |
| 1 | automatisch voll |
| 2 | manueller VE-Wechsel |

## VE-Abschluss-Handshake

Vor dem Zurücksetzen des aktuellen VE-Zählers muss die LOGO! atomar bzw. in definierter Reihenfolge:

1. `LastCompletedVEQuantity = CurrentParts` setzen.
2. `LastCompletedVENumber = CurrentVENumber` setzen.
3. `LastCompletionReason` setzen.
4. `CompletedVEs` erhöhen.
5. `CompletionSequence` erhöhen.
6. pneumatischen Wechsel ausführen bzw. Wechselstatus setzen.
7. `CurrentVENumber` erhöhen.
8. aktuellen VE-Zähler auf 0 setzen.

Der PC erkennt eine neue fertige VE an einer Änderung von `CompletionSequence`. Dadurch muss er den kurzen Zustand „VE voll“ nicht exakt im Polling treffen und kann das Etikett anhand der gespeicherten Last-Completed-Werte sicher erzeugen.

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

Diese Word-Reihenfolge ist für Partcounter V1 verbindlich.
