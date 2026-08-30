# Partcounter Protocol V3 – Modbus TCP Register Map

**Partcounter Revision:** R001.25  
**Protokollversion:** 3  
**PC-Rolle:** Modbus-TCP Client/Master  
**Siemens LOGO!-Rolle:** Modbus-TCP Server  
**Standard-Port:** 502  
**Standard Unit ID:** 1

Dieses Dokument ist die **normative Registerbelegung** für Partcounter R001.25. PC-Software und LOGO!-Programm müssen exakt denselben V3-Stand verwenden. Eine LOGO! mit Protocol V1/V2 wird von R001.25 bewusst abgewiesen.

> NModbus adressiert nullbasiert. PC-Offset `0` entspricht fachlich `HR1` und LOGO-VM `VW0`.

## 1. Engineering-Grundsätze

- Die LOGO! zählt reale Maschinenzyklen lokal.
- Teilemengen werden am PC aus `Zyklen × Kavitäten` berechnet.
- Eine einzelne VE ist auf **32767 Zyklen** begrenzt.
- Ein LOGO!-Auftrag ist auf **999999 Gesamtzyklen** begrenzt.
- VE-/Command-/Heartbeat-Sequenzen arbeiten im positiven Bereich bis 32767.
- 32-Bit-Werte werden **High Word, danach Low Word** übertragen.
- Ein Befehl gilt PC-seitig erst als erfolgreich, wenn die LOGO! dieselbe `AckSequence`, `ErrorCode=0` und die erforderlichen Echo-Werte meldet.
- One-Shot-Retries verwenden **dieselbe CommandSequence**, damit ein verlorenes TCP-Antworttelegramm keinen zweiten Reset/VE-Wechsel erzeugt.
- `HoldAfterVeNumber` wird vorab in der LOGO! gespeichert. Dadurch kann sie an einer kritischen VE-Grenze lokal stoppen, bevor der PC den Abschluss pollt.
- `JobId` ist eine technische Produktionsinstanz-ID und nicht die sichtbare Auftragsnummer.

## 2. PC → LOGO! – HR1 bis HR13

| HR | PC-Offset | LOGO VM | Typ | Name | Freigegebener Inhalt |
|---:|---:|---|---|---|---|
| HR1 | 0 | VW0 | UINT16 | ProtocolVersion | `3` |
| HR2 | 1 | VW2 | UINT16 | CommandSequence | 1…32767 |
| HR3 | 2 | VW4 | UINT16 | CommandWord | Bitfeld |
| HR4 | 3 | VW6 | UINT16 | ActiveCavities | 1…64 |
| HR5 | 4 | VW8 | UINT16 | TargetPartsPerVE_HI | High Word |
| HR6 | 5 | VW10 | UINT16 | TargetPartsPerVE_LO | Low Word |
| HR7 | 6 | VW12 | UINT16 | ValvePulse10Ms | 5…500 |
| HR8 | 7 | VW14 | UINT16 | JobId_HI | 0…32767 |
| HR9 | 8 | VW16 | UINT16 | JobId_LO | 1…32767 |
| HR10 | 9 | VW18 | UINT16 | TargetCyclesPerVE_HI | High Word; im freigegebenen Bereich 0 |
| HR11 | 10 | VW20 | UINT16 | TargetCyclesPerVE_LO | 1…32767 |
| HR12 | 11 | VW22 | UINT16 | PcHeartbeat | 1…32767 |
| HR13 | 12 | VW24 | UINT16 | HoldAfterVeNumber | 1…32767 |

`VW26…VW36` bleiben für spätere Erweiterungen reserviert und dürfen von R001.25 nicht anderweitig verwendet werden.

### 2.1 TargetCyclesPerVE

```text
TargetCyclesPerVE = ceil(TargetPartsPerVE / ActiveCavities)
EffectiveVE       = TargetCyclesPerVE × ActiveCavities
```

Beispiel: 1000 Sollteile bei 64 Kavitäten → 16 Zyklen → 1024 effektive Teile.

Der `On Threshold` des LOGO!-VE-Zählers wird direkt auf `VD18` gemappt.

### 2.2 ValvePulse10Ms

```text
ValvePulse10Ms = ValvePulseMs / 10
```

Freigabe: 50…5000 ms in 10-ms-Schritten. Beispiel: 750 ms → HR7 = 75.

### 2.3 JobId

Jede neue reale Auftragsaktivierung erhält vor dem ersten Modbus-Schreiben eine eigene technische JobId. Partcounter erzeugt sie so, dass beide 16-Bit-Wörter im positiven LOGO!-Analogbereich 0…32767 bleiben; die Low-Word-Komponente ist nie 0. Dieselbe JobId bleibt während des kompletten Auftrags einschließlich Zieländerungen und Recovery bestehen.

Die sichtbare ERP-/Auftragsnummer wird dadurch nicht verändert und kann später erneut verwendet werden, ohne einen alten LOGO!-Auftrag mit einer neuen Produktionsinstanz zu verwechseln.

### 2.4 HoldAfterVeNumber

`HoldAfterVeNumber` ist die Nummer der nächsten VE, nach deren Abschluss die LOGO! lokal weitere Zählpulse blockieren muss.

Beispiel:

```text
Auftrag: 9500 Teile
Standard-VE: 1000
Kavitäten: 8

VE 1…9 können autonom laufen.
Nach VE 9: lokaler Completion-Hold.
PC setzt Ziel der letzten Teil-VE und neuen Hold auf VE 10.
Erst nach bestätigter Neuparametrierung und Resume läuft die Zählung weiter.
```

## 3. CommandWord HR3

Die niederwertigen Word-Bits liegen auf LOGO!-Seite in `V5.x`.

| Bit | Maske | LOGO-Bit | Bedeutung |
|---:|---:|---|---|
| 0 | 0x0001 | V5.0 | AutomaticEnabled, Level |
| 1 | 0x0002 | V5.1 | ResetJob / neuer Auftrag, One-Shot |
| 2 | 0x0004 | V5.2 | ManualVEChange, One-Shot |
| 3 | 0x0008 | V5.3 | AcknowledgeAlarm, One-Shot |
| 4 | 0x0010 | V5.4 | PauseCounting, Level |

One-Shot-Bits werden nur bei `CommandSequence != AckSequence` verarbeitet. Danach übernimmt die LOGO! die bearbeitete Sequenz in `AckSequence`.

## 4. LOGO! → PC – HR20 bis HR40

| HR | Status-Index | LOGO VM | Typ | Name |
|---:|---:|---|---|---|
| HR20 | 0 | VW38 | UINT16 | ProtocolVersion = 3 |
| HR21 | 1 | VW40 | UINT16 | StatusWord |
| HR22 | 2 | VW42 | UINT16 | CurrentVECycles_HI |
| HR23 | 3 | VW44 | UINT16 | CurrentVECycles_LO |
| HR24 | 4 | VW46 | UINT16 | TotalCycles_HI |
| HR25 | 5 | VW48 | UINT16 | TotalCycles_LO |
| HR26 | 6 | VW50 | UINT16 | CurrentVENumber |
| HR27 | 7 | VW52 | UINT16 | CompletedVEs |
| HR28 | 8 | VW54 | UINT16 | LastCompletedVECycles_HI |
| HR29 | 9 | VW56 | UINT16 | LastCompletedVECycles_LO |
| HR30 | 10 | VW58 | UINT16 | AckSequence |
| HR31 | 11 | VW60 | UINT16 | ActiveCavitiesEcho |
| HR32 | 12 | VW62 | UINT16 | LastCompletedVENumber |
| HR33 | 13 | VW64 | UINT16 | CompletionSequence |
| HR34 | 14 | VW66 | UINT16 | LogoHeartbeat |
| HR35 | 15 | VW68 | UINT16 | ErrorCode |
| HR36 | 16 | VW70 | UINT16 | LastCompletionReason |
| HR37 | 17 | VW72 | UINT16 | LastCompletedCavities |
| HR38 | 18 | VW74 | UINT16 | HoldAfterVeNumberEcho |
| HR39 | 19 | VW76 | UINT16 | JobIdEcho_HI |
| HR40 | 20 | VW78 | UINT16 | JobIdEcho_LO |

### 4.1 Parameter-/VM-Zuordnungen

- `CurrentVECycles.Counter` → `VD42` → HR22/HR23.
- `TotalCycles.Counter` → `VD46` → HR24/HR25.
- `CurrentVECycles.OnThreshold` → `VD18` ← HR10/HR11.
- `LastCompletedVECycles` → VW56; HR28 bleibt bei der freigegebenen 32767-VE-Grenze 0.
- `AckSequence` → VW58.
- `LastCompletionReason` → VW70.
- `LastCompletedCavities` → VW72.
- `HoldAfterVeNumberEcho` → VW74.
- `JobIdEcho` → VW76/VW78.

## 5. StatusWord HR21

Die niederwertigen Bits liegen in `VB41`.

| Bit | Maske | LOGO-Bit | Bedeutung |
|---:|---:|---|---|
| 0 | 0x0001 | V41.0 | Ready |
| 1 | 0x0002 | V41.1 | AutomaticEnabled |
| 2 | 0x0004 | V41.2 | VE-Wechslerimpuls aktiv |
| 3 | 0x0008 | V41.3 | Alarm |
| 4 | 0x0010 | V41.4 | I1 Zykluseingang aktiv |
| 5 | 0x0020 | V41.5 | PC-Heartbeat stale |
| 6 | 0x0040 | V41.6 | Completion-Hold armed |
| 7 | 0x0080 | V41.7 | Completion-Hold active |

`PcHeartbeatStale` ist Diagnose. Ein Kommunikationsausfall stoppt normale gleichartige Voll-VE nicht künstlich. Der lokale Completion-Hold stoppt dagegen gezielt an dem vorab geplanten Grenzpunkt.

## 6. ErrorCode HR35

Freigegebener Grundsatz:

| Wert | Bedeutung |
|---:|---|
| 0 | kein Fehler |
| 1 | falsche Protokollversion |
| 2 | ungültige Kavitätenzahl |
| 3 | ungültiges Teileziel |
| 4 | Zielzyklen außerhalb 1…32767 |
| 5 | Ventilimpuls außerhalb 50…5000 ms / ungültiges Raster |
| 10 | optionale Endlage VE-Wechsler nicht rechtzeitig erreicht |
| 30 | interner ungültiger Ablaufzustand |

Weitere Fehlercodes dürfen erst nach synchroner Erweiterung von PC-Code, LOGO!-Engineering und Testmatrix belegt werden.

## 7. LastCompletionReason HR36

| Wert | Bedeutung |
|---:|---|
| 0 | unbekannt / noch kein Abschluss |
| 1 | automatisch voll |
| 2 | manueller VE-Wechsel |

## 8. Command/Ack-Transaktion

PC-Sequenz:

1. Aktuellen LOGO!-Snapshot lesen.
2. Lokale CommandSequence mit `AckSequence` synchronisieren.
3. Nächste Sequenz 1…32767 bilden.
4. Parameter + CommandWord schreiben.
5. Status pollen.
6. Erst bei passender `AckSequence` weiterprüfen.
7. `ErrorCode == 0` verlangen.
8. Bei Parametertelegramm `ActiveCavitiesEcho`, `HoldAfterVeNumberEcho` und `JobIdEcho` mit Soll vergleichen.
9. Erst dann den Vorgang lokal als bestätigt behandeln.

Bei Verbindungs-/Antwortverlust wird derselbe Sequenzwert erneut verwendet. Meldet die LOGO! bereits diese AckSequence, wird nicht erneut ausgelöst, sondern nur die Antwort validiert.

## 9. VE-Abschluss-Handshake

Vor Reset der aktuellen VE müssen die Abschlussdaten stabil gespeichert werden:

1. LastCompletedVECycles übernehmen.
2. LastCompletedCavities übernehmen.
3. LastCompletedVENumber übernehmen.
4. LastCompletionReason setzen.
5. CompletedVEs erhöhen.
6. CompletionSequence erhöhen / bei 32767 auf 1 zurückführen.
7. VE-Wechslerimpuls ausführen.
8. CurrentVENumber fortschreiben.
9. CurrentVECycles zurücksetzen.
10. Wenn die abgeschlossene VE `HoldAfterVeNumber` erreicht hat, muss der lokale CountGate für nachfolgende Zyklusflanken blockiert bleiben.

Der PC erkennt eine neue VE an der Änderung von `CompletionSequence`.

## 10. Recovery nach PC-Neustart

Ein offener Echtauftrag wird auf PC-Seite persistiert. Nach Neustart gilt:

1. Auftrag lokal nur PAUSIERT laden.
2. LOGO!-Protocol V3 lesen.
3. `JobIdEcho` gegen den gespeicherten Produktionsinstanz-Token prüfen.
4. `ActiveCavitiesEcho` prüfen.
5. LOGO! kontrolliert pausieren und erneut lesen.
6. Zähler-/VE-/Hold-Zustand rekonstruieren.
7. Auftrag weiterhin PAUSIERT lassen.
8. Fortsetzen nur durch bewusste Bedieneraktion.

Ein unsicherer Start (`PendingActivation`) darf nur verworfen werden, wenn die LOGO! nachweislich keinen aktiven Produktionszustand besitzt.

## 11. Heartbeats

- PC: HR12, 1…32767, danach Wrap auf 1.
- LOGO!: HR34, 1…32767, danach Wrap auf 1.
- Entscheidend ist eine Wertänderung im Diagnosefenster.
- Heartbeat-Ausfall ist Diagnose und keine Safety-Funktion.

## 12. 32-Bit-Wortreihenfolge

```text
value = (high << 16) | low
```

High Word wird immer zuerst übertragen. Die tatsächliche Wortreihenfolge ist Bestandteil der M01-Abnahme mit bekannten Testmustern.

## 13. Betriebsgrenzen Protocol V3

- Kavitäten: 1…64.
- Zyklen je VE: 1…32767.
- Gesamtzyklen je LOGO-Auftrag: 0…999999.
- VE-Nummer / CompletedVEs: maximal 32767 je Auftrag.
- Ventilimpuls: 50…5000 ms in 10-ms-Schritten.
- CommandSequence: 1…32767 zyklisch.
- Heartbeats: 1…32767 zyklisch.
- JobId: nonzero; High-/Low-Word für den Partcounter-Generator jeweils im positiven LOGO-Bereich.

Aufträge oberhalb der freigegebenen LOGO-Zählergrenzen werden bereits vor dem Start vom PC abgewiesen und müssen segmentiert werden.

## 14. Safety-Grenze

Partcounter und die Standard-LOGO! sind **keine Sicherheitssteuerung**. Not-Halt, Schutztüren, sichere Bewegungsfreigaben und sonstige Safety-Funktionen verbleiben vollständig in den dafür vorgesehenen sicheren Maschinenstromkreisen bzw. Safety-Steuerungen.
