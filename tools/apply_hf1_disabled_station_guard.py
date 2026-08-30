from pathlib import Path

path = Path('src/Partcounter.App/ViewModels/MainViewModel.cs')
text = path.read_text(encoding='utf-8')
needle = '''        if (SelectedMachine is null || SelectedArticle is null)\n        {\n            StatusMessage = "Bitte Maschine und Artikel auswählen.";\n            return;\n        }\n\n        if (HasUnresolvedPendingActivation(SelectedMachine))\n'''
replacement = '''        if (SelectedMachine is null || SelectedArticle is null)\n        {\n            StatusMessage = "Bitte Maschine und Artikel auswählen.";\n            return;\n        }\n\n        if (!IsSimulationMode && !SelectedMachine.Configuration.Enabled)\n        {\n            StatusMessage = $"{SelectedMachine.DisplayName}: Auftrag nicht gestartet. Die Station ist in der Maschinen-/Modbus-Konfiguration administrativ deaktiviert und besitzt im Echtbetrieb bewusst keine Kommunikationssession.";\n            return;\n        }\n\n        if (HasUnresolvedPendingActivation(SelectedMachine))\n'''
if needle not in text:
    raise SystemExit('HF1 disabled-station guard anchor not found')
text = text.replace(needle, replacement, 1)
path.write_text(text, encoding='utf-8')
print('HF1 disabled-station live-order guard applied')
