# Partcounter Protocol V3 – deterministische VE-Grenze

## Anlass
Die R001.25-Härtungsprüfung hat einen Echtbetriebs-Race-Case identifiziert: Wird eine VE ausschließlich PC-seitig nach Erkennen der `CompletionSequence` pausiert, kann zwischen LOGO!-Abschluss und dem nächsten PC-Poll bereits ein weiterer Maschinenzyklus eintreffen.

## Entscheidung
Protocol V3 speichert deshalb den nächsten kritischen VE-Grenzpunkt **vorab lokal in der Siemens LOGO!** als `HoldAfterVeNumber`. Die LOGO! muss bei Abschluss genau dieser VE den Zähleingang lokal verriegeln, bevor der Abschluss an den PC publiziert wird.

Damit bleiben gleichartige Voll-VE bis zur geplanten Grenze auch bei PC-/WLAN-Ausfall autonom. Nur an Auftragsende oder vor einer VE mit geändertem Sollwert ist PC-Interaktion zwingend.

## Kompatibilität
Die vorhandenen V2-Adressen werden nicht verschoben. V3 ergänzt lediglich einen Konfigurationswert, ein Echo und zwei Statusbits. Eine V2-LOGO! wird absichtlich nicht als kompatibel akzeptiert; ein sicherheitskritischer Mechanismus darf nicht stillschweigend ignoriert werden.

## Freigabe
CI kann PC-Code, Planung, Protokollvertrag und Simulation prüfen. Die tatsächliche Scanreihenfolge und Verriegelungswirkung in der LOGO! muss anschließend an M01 mit realem CycleInput abgenommen werden.
