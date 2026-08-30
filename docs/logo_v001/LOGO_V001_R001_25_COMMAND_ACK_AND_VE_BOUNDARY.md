# LOGO V001 – R001.25 Command/Ack- und VE-Grenzregel

Die Registerbelegung des Modbus Protocol V2 bleibt unverändert.

## Command/Ack
Die LOGO! verarbeitet One-Shot-Bits nur, wenn `CommandSequence != AckSequence`. Nach abgeschlossener Verarbeitung kopiert sie die neue CommandSequence nach AckSequence. Ein wiederholtes Telegramm mit derselben Sequenz darf den One-Shot nicht erneut auslösen.

## VE-Grenze
PC-seitig gilt im Onlinepfad: VE-Abschluss erkennen → PauseCounting senden und ACK abwarten → nächste VE-Parameter mit pauseCounting=true senden und ACK abwarten → bei laufendem Auftrag ResumeCounting senden und ACK abwarten.

## Reale Abnahme
Gezielt testen: verlorene TCP-Antwort nach Write, kein Doppel-One-Shot bei Retry, Verbindung während Zielwechsel, AckSequence-Wrap 32767 → 1.
