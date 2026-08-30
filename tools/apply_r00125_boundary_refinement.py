from pathlib import Path

path = Path("src/Partcounter.App/Help/PARTCOUNTER_HILFE_R001_25.md")
text = path.read_text(encoding="utf-8")

replacements = {
    "Die LOGO!-Station muss das freigegebene Modbus-Protokoll V2 verwenden.":
        "Die LOGO!-Station muss das freigegebene **Modbus-Protokoll V3** verwenden. Vor Echtbetrieb müssen Hold-Echo sowie die Statusbits CompletionHoldArmed/CompletionHoldActive gemäß M01-Abnahme geprüft sein.",

    "Beim Start schreibt Partcounter die freigegebenen Auftragsparameter über Modbus V2 an die ausgewählte LOGO!-Station. Erst wenn die Übertragung erfolgreich war, wird der Auftrag lokal als gestartet behandelt.":
        "Beim Start schreibt Partcounter die freigegebenen Auftragsparameter über **Modbus Protocol V3** an die ausgewählte LOGO!-Station. Der Auftrag gilt erst nach passender AckSequence, ErrorCode 0, Kavitäten-Echo, HoldAfterVeNumber-Echo und gesetztem CompletionHoldArmed als übernommen. Partcounter plant den ersten kritischen VE-Grenzpunkt bereits vor Produktionsbeginn.",

    "## [MODBUS-01] Modbus V2 – Register, Handshake und Heartbeats":
        "## [MODBUS-01] Modbus Protocol V3 – Register, Handshake, Grenzhalt und Heartbeats",

    "Partcounter und LOGO! müssen dieselbe Protokollversion verwenden. R001.18/R001.19 basieren auf Modbus-Protokoll V2.":
        "Partcounter und LOGO! müssen dieselbe Protokollversion verwenden. **R001.25 verlangt Protocol V3.** Eine ältere V2-LOGO!-Station wird im Echtbetrieb bewusst abgewiesen, weil sie den lokalen VE-Grenzhalt nicht verbindlich bestätigen kann.",

    "Ein erfolgreicher TCP-Schreibaufruf gilt nicht mehr als ausreichende Befehlsbestätigung. Partcounter wartet nach jedem Steuerbefehl auf die passende AckSequence, prüft ErrorCode=0 und bei Auftrags-/VE-Parametern zusätzlich das Kavitäten-Echo.":
        "Ein erfolgreicher TCP-Schreibaufruf gilt nicht als ausreichende Befehlsbestätigung. Partcounter wartet auf die passende AckSequence, prüft ErrorCode=0 und bei Auftrags-/VE-Parametern zusätzlich Kavitäten-Echo, HoldAfterVeNumber-Echo und bei Hold > 0 das Statusbit CompletionHoldArmed.",

    "Nach einer abgeschlossenen VE pausiert Partcounter im verfügbaren Onlinepfad zuerst die Zählung, überträgt und bestätigt das nächste VE-Ziel und gibt danach die Zählung wieder frei.":
        "R001.25 plant die erste kritische VE bereits **vor** Produktionsbeginn als HoldAfterVeNumber in der LOGO!. Gleichartige Standard-VE dürfen bis dorthin autonom laufen. An der geplanten Grenze muss die LOGO! CompletionHoldActive lokal latchen, bevor ein Folgezyklus gezählt werden kann. Partcounter prüft diesen Hold, überträgt im gehaltenen Zustand das nächste VE-Ziel samt neuem Grenzpunkt, wartet auf ACK/Echos/HoldArmed und sendet erst danach Resume. Bei der finalen VE erfolgt kein Resume.",
}

for old, new in replacements.items():
    if old not in text:
        raise SystemExit(f"Hilfe-Synchronisation fehlgeschlagen; Text nicht gefunden: {old[:90]}")
    text = text.replace(old, new, 1)

if "Modbus V2" in text or "Modbus-Protokoll V2" in text:
    raise SystemExit("Hilfe enthält nach der Synchronisation noch einen aktuellen V2-Verweis.")

path.write_text(text, encoding="utf-8", newline="\n")
print("R001.25 integrated help synchronized to Protocol V3.")
