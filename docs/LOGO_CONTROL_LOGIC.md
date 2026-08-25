# Siemens LOGO! control logic – Partcounter R001

## Objective

The LOGO! is the autonomous machine-side counter and packaging-unit change controller. The PC is supervisory; WLAN availability is not allowed to be a prerequisite for correct counting.

## Physical I/O concept

Suggested starting point per machine:

- `I1`: production cycle / parts-ejected pulse from injection molding machine
- `I2`: optional VE changer home/position feedback
- `I3`: optional manual change pushbutton
- `Q1`: pneumatic valve / interposing relay for VE changer

The exact voltage/interface depends on the machine and LOGO! variant. Use a potential-free or appropriately isolated machine signal. Do not connect into machine safety circuits.

## Local cycle logic

On the positive edge of the valid cycle signal:

```text
TotalCycles := TotalCycles + 1
CurrentVeParts := CurrentVeParts + ActiveCavities

IF AutomaticMode AND CurrentVeParts >= TargetPartsPerVe THEN
    LastCompletedVeQuantity := CurrentVeParts
    CompletedVeCount := CompletedVeCount + 1
    CurrentVeNumber := CurrentVeNumber + 1
    Pulse Q1 for ValvePulseMs
    Set VE-completed event
    CurrentVeParts := 0
END_IF
```

## Why counting stays in the LOGO!

A PC poll is not deterministic enough to be the primary cycle counter. With local counting:

- cycle pulses are captured even if WLAN is temporarily unavailable;
- a slow or rebooting PC does not lose production quantities;
- the pneumatic VE change does not depend on network latency;
- the PC can reconnect and read the current authoritative state.

## Command handshake

The PC writes both a command word and a command sequence number. The LOGO! stores the last processed sequence.

```text
IF ReceivedSequence <> LastProcessedSequence THEN
    execute requested command once
    LastProcessedSequence := ReceivedSequence
    AckSequence := ReceivedSequence
END_IF
```

This is required because a Modbus write can be retried after a communication failure. A level bit alone could otherwise cause the same manual change or reset more than once.

## VE target and cavity count

Only complete molding cycles can normally be assigned to one packaging unit. Therefore exact target quantity requires:

`TargetPartsPerVe mod ActiveCavities = 0`

If not divisible, the actual VE quantity will be the next complete cycle quantity. Partcounter records this overfill explicitly.

Example:

- target: 1,000 parts
- active cavities: 8
- required cycles: 125
- actual quantity: 1,000 (exact)

Example with unavoidable cycle overfill:

- target: 1,000 parts
- active cavities: 64
- required cycles: 16
- actual quantity: 1,024
- overfill: 24 parts

## Recovery after network interruption

The LOGO! keeps the active job parameters and local counters. On PC reconnect:

1. PC reads protocol version and status block.
2. PC compares active job/sequence with its database state.
3. LOGO! state is treated as authoritative for the live part counter.
4. Conflicts are shown to the operator; the PC does not silently reset the LOGO!.

## Pneumatic changer timing

The actual mechanical VE changer must complete its switching movement before the next produced parts arrive. `ValvePulseMs` is therefore a process parameter, not just a software preference. A position feedback input is strongly recommended for production use.
