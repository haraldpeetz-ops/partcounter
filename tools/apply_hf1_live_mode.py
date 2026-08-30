from pathlib import Path

root = Path(__file__).resolve().parents[1]
vm_path = root / "src/Partcounter.App/ViewModels/MainViewModel.cs"
text = vm_path.read_text(encoding="utf-8")
start = text.index("    private async Task ToggleOperatingModeAsync()")
end = text.index("    private async Task ApplySelectedArticleAsync()", start)
replacement = r'''    private async Task ToggleOperatingModeAsync()
    {
        if (IsSimulationMode)
        {
            var activationPlan = OperatingModeActivationPolicy.Build(Machines, _startupRecoveryMachines);

            if (activationPlan.DisabledRecoveryMachineNumbers.Count > 0)
            {
                var machines = string.Join(", ", activationPlan.DisabledRecoveryMachineNumbers.Select(n => $"M{n:00}"));
                StatusMessage = $"Echtbetrieb NICHT aktiviert: Für {machines} liegt ein offener Echtbetrieb-Recovery-Auftrag vor, die Station ist aber in der Maschinen-/Modbus-Konfiguration administrativ deaktiviert. Station zuerst aktivieren und Partcounter neu starten.";
                return;
            }

            if (activationPlan.LiveMachines.Count == 0)
            {
                StatusMessage = "Echtbetrieb NICHT aktiviert: In der Maschinen-/Modbus-Konfiguration ist keine LOGO!-Station administrativ aktiviert. Mindestens eine Station aktivieren und Partcounter neu starten.";
                return;
            }

            // Pure simulation orders have no real-world authority. They must never prevent the operator
            // from entering live mode and must never leak their counters/order state into a real LOGO session.
            var discardedSimulationOrders = activationPlan.SimulationOrdersToDiscard.Count;
            foreach (var machine in activationPlan.SimulationOrdersToDiscard)
            {
                var discardedOrder = machine.OrderNumber;
                machine.ClearRecoveredOrder();
                _scheduledCompletionHolds.Remove(machine.Configuration.MachineNumber);
                _manualVeReconfigurationPending.Remove(machine.Configuration.MachineNumber);
                try
                {
                    await _database.AddEventAsync(machine.Configuration.MachineNumber, "SIMULATION_ORDER_DISCARDED_FOR_LIVE_MODE",
                        $"Simulationsauftrag {discardedOrder} wurde beim bewussten Wechsel in den Echtbetrieb verworfen.");
                }
                catch
                {
                    // A diagnostic event must never make the live-mode transition fail.
                }
            }

            foreach (var machine in Machines)
                machine.ConnectionState = ConnectionState.Offline;

            try
            {
                // StartAsync creates sessions only for administratively enabled configurations.
                // Every subsequent fleet call in this transition therefore uses LiveMachines only.
                await _fleet.StartAsync(activationPlan.LiveMachines.Select(m => m.Configuration), publishSnapshots: false);

                var recoveryErrors = await ReconcilePendingLiveOrdersAsync();
                if (recoveryErrors.Count > 0)
                {
                    await RollbackLiveModeActivationAsync();
                    StatusMessage = $"Echtbetrieb NICHT aktiviert. Recovery-Fehler: {string.Join(" | ", recoveryErrors)} Bereits eindeutig erkannte Aufträge bleiben sicher pausiert.";
                    return;
                }

                // Runtime-temporarily-disabled machines still have a communication session because their
                // administrative configuration is enabled. Keep them silent before publishing live snapshots.
                foreach (var machine in activationPlan.LiveMachines.Where(m => m.IsTemporarilyDisabled))
                {
                    await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);
                    await _fleet.SetMachinePollingEnabledAsync(machine.Configuration.MachineNumber, enabled: false);
                }

                var recoveredCount = _startupRecoveryMachines.Count;
                _startupRecoveryMachines.Clear();

                // Commit the mode only after fleet creation, recovery and temporary-disable setup succeeded.
                IsSimulationMode = false;

                foreach (var machine in activationPlan.LiveMachines.Where(m => !m.IsTemporarilyDisabled))
                    await _fleet.SetSnapshotPublishingEnabledAsync(machine.Configuration.MachineNumber, enabled: true);

                var notes = new List<string>();
                if (activationPlan.AdministrativelyDisabledCount > 0)
                    notes.Add($"{activationPlan.AdministrativelyDisabledCount} administrativ deaktivierte Station(en) werden ignoriert");
                if (discardedSimulationOrders > 0)
                    notes.Add($"{discardedSimulationOrders} Simulationsauftrag/Simulationsaufträge kontrolliert verworfen");
                var suffix = notes.Count == 0 ? string.Empty : $" · {string.Join(" · ", notes)}";

                StatusMessage = recoveredCount > 0
                    ? $"Echtbetrieb aktiv: {recoveredCount} wiederhergestellte(r) Auftrag/Aufträge mit JobId/Kavitäten/Hold gegen die LOGO! verifiziert und absichtlich PAUSIERT. Fortsetzen muss je Maschine bewusst erfolgen.{suffix}"
                    : $"Echtbetrieb aktiv: {activationPlan.LiveMachines.Count} freigegebene LOGO!-Station(en) initialisiert. Nicht erreichbare Stationen werden einzeln als Offline gemeldet; Protocol V3 wird bei Kommunikation zwingend geprüft.{suffix}";
            }
            catch (Exception ex)
            {
                await RollbackLiveModeActivationAsync();
                StatusMessage = $"Echtbetrieb konnte nicht aktiviert werden: {ex.Message} Partcounter wurde vollständig in die Simulation zurückgesetzt.";
                try
                {
                    await _database.AddEventAsync(null, "LIVE_MODE_ACTIVATION_FAILED", StatusMessage);
                }
                catch
                {
                    // Preserve the actual activation error even if diagnostics cannot be persisted.
                }
            }
        }
        else
        {
            if (Machines.Any(m => m.IsActiveOrder))
            {
                StatusMessage = "Betriebsartwechsel gesperrt: Laufende oder pausierte Echtaufträge zuerst kontrolliert beenden.";
                return;
            }

            try
            {
                await _fleet.StopAsync();
                IsSimulationMode = true;
                foreach (var machine in Machines)
                    machine.ConnectionState = ConnectionState.Simulation;
                StatusMessage = "Simulation aktiv. Es werden keine Modbus-Schreibbefehle an LOGO! gesendet.";
            }
            catch (Exception ex)
            {
                // Even if teardown reports a diagnostic problem, the safe user-facing state is simulation.
                IsSimulationMode = true;
                foreach (var machine in Machines)
                    machine.ConnectionState = ConnectionState.Simulation;
                StatusMessage = $"Simulation aktiviert; beim Beenden der Modbus-Sessions trat ein Diagnosefehler auf: {ex.Message}";
            }
        }
    }

    private async Task RollbackLiveModeActivationAsync()
    {
        try
        {
            await _fleet.StopAsync();
        }
        catch
        {
            // Rollback is best-effort; local mode and UI state are still forced back to simulation below.
        }

        IsSimulationMode = true;
        foreach (var machine in Machines)
            machine.ConnectionState = ConnectionState.Simulation;
    }

'''
vm_path.write_text(text[:start] + replacement + text[end:], encoding="utf-8")

csproj = root / "src/Partcounter.App/Partcounter.App.csproj"
project = csproj.read_text(encoding="utf-8")
project = project.replace("<FileVersion>0.1.25.0</FileVersion>", "<FileVersion>0.1.25.1</FileVersion>")
project = project.replace("<InformationalVersion>0.1.25-r001.25-final-hardening</InformationalVersion>", "<InformationalVersion>0.1.25-r001.25-hf1-live-mode</InformationalVersion>")
csproj.write_text(project, encoding="utf-8")

workflow = root / ".github/workflows/build-r00125.yml"
wf = workflow.read_text(encoding="utf-8")
wf = wf.replace("branches: [ r001.25-final-hardening, main ]", "branches: [ r001.25-final-hardening, main, 'hotfix/**' ]")
workflow.write_text(wf, encoding="utf-8")

print("R001.25 HF1 live-mode patch applied.")
