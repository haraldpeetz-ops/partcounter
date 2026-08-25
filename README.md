# Partcounter

**Industrial Packaging Unit Counter for Injection Molding**

Revision: **R001 – System Foundation**  
Status: Initial architecture / MVP foundation

## Purpose

Partcounter supervises up to 30 injection molding machines. Each machine is equipped with a Siemens LOGO! controller. The LOGO! counts production cycles locally, converts cycles into produced parts using the active cavity count, controls a pneumatic packaging-unit changer and exposes all relevant production data to a central Windows application over Modbus TCP.

The PC application displays the fill level of the active packaging unit (VE), indicates completed packaging units and provides job/configuration data to each LOGO!.

## Core design principle

**Counting and automatic VE change are executed locally in the Siemens LOGO!.**

The Windows application is the supervisory and configuration layer. This ensures that a temporary PC, Ethernet or WLAN failure does not cause a missed count or missed packaging-unit change.

## Target architecture

```text
Injection molding machine cycle signal
             |
             v
      Siemens LOGO! 8.x ----> Pneumatic valve / VE changer
             |
        Ethernet
             |
     WLAN client bridge
             ))
             ))  Industrial WLAN
             ((
       Access Point(s)
             |
        Ethernet LAN
             |
      Partcounter PC
      .NET 8 / WPF
      Modbus TCP Client
```

## Planned machine data

For each of up to 30 machines:

- Machine number / name
- LOGO! IP address
- Modbus TCP port (default 502)
- Connection status
- Article number
- Tool number
- Active cavity count (1…64)
- Target parts per packaging unit
- Current parts in active packaging unit
- Current fill level in %
- Current packaging-unit number
- Number of completed packaging units
- Current cycle count
- Last cycle timestamp
- Automatic/manual mode
- Packaging unit full status
- VE changer status
- Alarm / communication status

## Production logic

1. PC sends job parameters to LOGO!.
2. LOGO! receives one cycle pulse from the injection molding machine.
3. LOGO! increments local cycle counter.
4. LOGO! adds the active cavity count to the current VE part counter.
5. When the configured VE target is reached or exceeded, LOGO! actuates the pneumatic changer.
6. LOGO! marks the packaging unit as completed and starts the next VE.
7. PC reads the resulting state and updates the dashboard.

### Important: cavity count vs. VE target

A packaging-unit target should ideally be divisible by the number of active cavities. Example: 8 cavities and VE target 1,000 parts cannot be filled exactly by complete cycles. The system therefore records both the configured target and the actual filled quantity and reports any cycle-related overfill.

## Communications

- Protocol: Modbus TCP
- Transport: Ethernet/IP over WLAN bridge
- Recommended topology: one persistent PC client connection per LOGO!, with independent communication workers
- Polling is staggered/parallel so one unreachable machine cannot block the remaining machines
- Write operations use a command sequence/acknowledgement handshake instead of short network pulses

See `docs/MODBUS_REGISTER_MAP.md` for the initial logical register map.

## Windows application

Initial technical baseline:

- C#
- .NET 8
- WPF
- MVVM-oriented architecture
- SQLite planned for configuration, production history and audit data
- NModbus planned for Modbus TCP communication
- Simulation mode for development without physical LOGO! hardware

## R001 scope

R001 establishes:

- solution/project structure
- 30-machine data model
- dashboard foundation
- machine configuration model
- communication abstraction
- simulation mode
- Modbus register-map specification
- local-LOGO-first control philosophy

## Planned revisions

- **R001** – System foundation and simulator
- **R002** – Real Modbus TCP communication with one LOGO!
- **R003** – 30-machine parallel communication manager
- **R004** – Job/article/tool management and SQLite persistence
- **R005** – VE history, alarms, statistics and audit trail
- **R006** – Production-ready packaging change handshake and recovery logic
- **R007** – Deployment, portable build, diagnostics and field commissioning tools

## Safety / commissioning note

Partcounter is a production monitoring/control system, not a safety PLC. Safety functions of the injection molding machine must remain in the machine safety circuit. Interfaces to machine signals and pneumatic actuators must be electrically suitable, isolated where required, and commissioned by qualified personnel.
