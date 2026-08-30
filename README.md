# Partcounter

**Aktueller Engineering-Stand:** R001.25 – Final Hardening  
**Version:** 0.1.25  
**Plattform:** Windows 10/11 · C# · .NET 10 LTS · WPF  
**Anlage:** bis zu 30 Spritzgussmaschinen · Siemens LOGO! · Modbus TCP · WLAN/LAN

Partcounter ist ein industrieller Leitstand für Verpackungseinheiten im Spritzguss. Die Siemens LOGO! zählt Maschinenzyklen lokal; Partcounter verwaltet Aufträge, VE-Ziele, Historie, Etikettierung, ARBURG ALS/proALPHA und die Inbetriebnahme.

## R001.25 – Final Hardening
- .NET 10 LTS
- Compilerwarnungen als Fehler
- bestätigter CommandSequence/AckSequence-Handshake
- gleicher Sequenzwert bei Retry, damit One-Shots idempotent bleiben
- ErrorCode- und Kavitäten-Echo-Prüfung
- sichere Online-VE-Grenztransaktion: Pause → nächstes Ziel → ACK → Resume
- global serialisierte SQLite-Schreibzugriffe
- 24/7-Tagessicherung auch ohne Programmneustart
- produktiv exklusive Auftragsquelle: ARBURG ALS oder proALPHA
- DPAPI-geschützte Schnittstellen-Secrets
- optionale Authenticode-Prüfung für signierte Updatepakete
- automatisierte Unit-/Regressionstests
- WPF-Stresstest und Multi-Resolution-Layouttest bleiben Release-Gates

## Noch vor endgültiger Maschinenfreigabe
Die Softwaretests ersetzen nicht die reale Abnahme an Referenzmaschine M01. Vor Serienrollout müssen I1, Q1/Koppelrelais/Ventil, Command/Ack, WLAN-Abbruch/Wiederkehr, PC-Neustart, letzte Teil-VE, Drucker sowie reale ALS-/proALPHA-Zugänge mit dem Prüfprotokoll validiert werden.

## Modbus Protocol V2
PC → LOGO!: HR1…HR12 / VW0…VW22  
LOGO! → PC: HR20…HR37 / VW38…VW72  
LOGO! = Modbus-TCP-Server, Partcounter = Client/Master, Standard TCP 502, Unit ID 1.

Die Registerbelegung bleibt in R001.25 kompatibel zu R001.24. Die PC-Seite wertet den vorhandenen Command/Ack-Mechanismus nun verbindlich aus.

## Safety
Partcounter und die Standard-Siemens-LOGO! sind keine Sicherheitssteuerung. Not-Halt, Schutztüren und alle sicherheitsgerichteten Funktionen verbleiben vollständig in den vorgesehenen Maschinenkreisen.
