# Partcounter R001.25 – eindeutige Auftragsinstanz-ID

Für den Echtbetrieb wird `JobId` nicht aus der sichtbaren Auftragsnummer abgeleitet. Jede neue LOGO!-Auftragsaktivierung erhält einen eigenen kryptografisch erzeugten 32-Bit-Korrelationstoken. Beide 16-Bit-Wörter bleiben bewusst im Bereich 0…32767, damit die Siemens-LOGO!-Analog-/Netzwerkbausteine den Wert ohne Vorzeichenmehrdeutigkeit als High-/Low-Word spiegeln können.

Der Token wird **vor** dem ersten Modbus-Auftragsschreiben als `PendingActivation` in SQLite persistiert. Erst danach darf Partcounter den Reset-/Startbefehl an die LOGO! senden. Nach erfolgreichem Ack wird derselbe Token für alle Zieländerungen, Pausen/Wiederanläufe und Recovery-Prüfungen dieses Produktionsauftrags beibehalten.

Bei einem Neustart akzeptiert Partcounter einen gespeicherten Echtauftrag nur, wenn `JobIdEcho`, Kavitäten und Grenzhalt zum Checkpoint passen. Eine wiederverwendete sichtbare Auftragsnummer erzeugt deshalb eine neue technische Instanz und kann nicht irrtümlich mit einem alten LOGO!-Auftrag verwechselt werden.
