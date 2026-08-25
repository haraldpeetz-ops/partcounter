# Partcounter – Modbus TCP register map R001

This is the initial logical contract between the Windows application and each Siemens LOGO!.

## Addressing rule

Siemens LOGO! maps variable words to Modbus holding registers. The intended mapping is:

- HR1 = VW0
- HR2 = VW2
- HR3 = VW4
- ...

In the PC source code NModbus uses **zero-based protocol addresses**, therefore NModbus address `0` corresponds to the first holding register (HR1).

## PC → LOGO! configuration / command block

| HR | LOGO VM | Meaning | Format |
|---:|---:|---|---|
| 1 | VW0 | Protocol version | UInt16 |
| 2 | VW2 | Command sequence | UInt16 |
| 3 | VW4 | Command word | bit field |
| 4 | VW6 | Active cavities | UInt16 (1…64) |
| 5 | VW8 | VE target, high word | UInt32 high |
| 6 | VW10 | VE target, low word | UInt32 low |
| 7 | VW12 | Pneumatic valve pulse time | ms UInt16 |
| 8 | VW14 | Job ID high word | UInt32 high |
| 9 | VW16 | Job ID low word | UInt32 low |
| 10 | VW18 | Reserved | UInt16 |

### Command word

- bit 0: automatic VE change enabled
- bit 1: reset active job/counters
- bit 2: manual VE change request
- bit 3: acknowledge alarm

The LOGO! executes a command only when `Command sequence` changes. After execution, the LOGO! copies the processed sequence number into `Status acknowledgement sequence`. This prevents lost or repeatedly executed commands after WLAN interruptions.

## LOGO! → PC status block

| HR | LOGO VM | Meaning | Format |
|---:|---:|---|---|
| 20 | VW38 | Protocol version | UInt16 |
| 21 | VW40 | Status word | bit field |
| 22 | VW42 | Current VE parts high | UInt32 high |
| 23 | VW44 | Current VE parts low | UInt32 low |
| 24 | VW46 | Total cycles high | UInt32 high |
| 25 | VW48 | Total cycles low | UInt32 low |
| 26 | VW50 | Current VE number | UInt16 |
| 27 | VW52 | Completed VE count | UInt16 |
| 28 | VW54 | Last completed VE quantity high | UInt32 high |
| 29 | VW56 | Last completed VE quantity low | UInt32 low |
| 30 | VW58 | Acknowledged command sequence | UInt16 |
| 31 | VW60 | Active cavities echo | UInt16 |

## Proposed status word

- bit 0: job active
- bit 1: automatic mode active
- bit 2: VE change output active
- bit 3: VE completed event
- bit 4: configuration valid
- bit 5: cycle input seen
- bit 6: local alarm
- bit 7: manual mode

## Polling strategy

Each LOGO! gets its own communication state machine. A lost connection to machine 07 must not block machines 01–06 or 08–30.

Initial recommendation:

- normal status polling: 500–1000 ms per machine
- parallel/staggered polling
- persistent TCP connection where possible
- short connection/read timeouts
- exponential reconnect delay after failures
- command read-back using the sequence/acknowledgement pair

## Revision control

Any incompatible register-map change increments `Protocol version`. The PC must reject an unexpected version instead of interpreting incorrect registers.
