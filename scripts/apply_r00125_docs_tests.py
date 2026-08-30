from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]

def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8')

def write(rel, text):
    p = ROOT / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding='utf-8', newline='\n')

# Tests
write('tests/Partcounter.Tests/Partcounter.Tests.csproj', '''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\\..\\src\\Partcounter.App\\Partcounter.App.csproj" />
  </ItemGroup>
</Project>
''')

write('tests/Partcounter.Tests/CoreRegressionTests.cs', '''using Microsoft.Data.Sqlite;
using Partcounter.Models;
using Partcounter.Services;

namespace Partcounter.Tests;

public sealed class CoreRegressionTests
{
    [Fact]
    public void SixtyFourCavityRounding_IsDeterministic()
    {
        var article = new ArticleDefinition(1, "A", "Test", "WZ", 64, 1000, true);
        Assert.Equal((uint)16, article.RequiredCycles);
        Assert.Equal((uint)1024, article.EffectivePackagingQuantity);
        Assert.Equal((uint)24, article.ExpectedOverfill);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(65535u)]
    [InlineData(65536u)]
    [InlineData(uint.MaxValue)]
    public void ModbusDword_RoundTrips(uint value)
    {
        Assert.Equal(value, ModbusRegisterMap.ToUInt32(ModbusRegisterMap.HighWord(value), ModbusRegisterMap.LowWord(value)));
    }

    [Theory]
    [InlineData((ushort)50, (ushort)5)]
    [InlineData((ushort)750, (ushort)75)]
    [InlineData((ushort)5000, (ushort)500)]
    public void ValvePulse_UsesTenMillisecondUnits(ushort milliseconds, ushort expected)
    {
        Assert.Equal(expected, ModbusRegisterMap.ToValvePulse10Ms(milliseconds));
    }

    [Fact]
    public async Task SqliteWriteCoordinator_SerializesConcurrentWriters()
    {
        var root = Path.Combine(Path.GetTempPath(), "PartcounterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var db = Path.Combine(root, "test.db");

        await SqliteWriteCoordinator.ExecuteAsync(db, async connection =>
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE T(Id INTEGER PRIMARY KEY AUTOINCREMENT, V INTEGER NOT NULL);";
            await cmd.ExecuteNonQueryAsync();
        });

        var tasks = Enumerable.Range(0, 100).Select(i => SqliteWriteCoordinator.ExecuteAsync(db, async connection =>
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO T(V) VALUES($v);";
            cmd.Parameters.AddWithValue("$v", i);
            await cmd.ExecuteNonQueryAsync();
        }));
        await Task.WhenAll(tasks);

        await using var verify = new SqliteConnection(SqliteWriteCoordinator.BuildConnectionString(db));
        await verify.OpenAsync();
        var count = verify.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM T;";
        Assert.Equal(100L, Convert.ToInt64(await count.ExecuteScalarAsync()));
    }

    [Fact]
    public void ProtocolV2_ContractRemainsStable()
    {
        Assert.Equal((ushort)2, ModbusRegisterMap.ProtocolVersion);
        Assert.Equal((ushort)0, ModbusRegisterMap.ConfigStart);
        Assert.Equal((ushort)12, ModbusRegisterMap.ConfigLength);
        Assert.Equal((ushort)19, ModbusRegisterMap.StatusStart);
        Assert.Equal((ushort)18, ModbusRegisterMap.StatusLength);
        Assert.Equal((ushort)32767, ModbusRegisterMap.MaxSequenceValue);
    }
}
''')

# Add tests project to solution.
sln = read('Partcounter.sln')
if 'Partcounter.Tests' not in sln:
    project = 'Project("{9A19103F-16F7-4C22-9C2E-1D87E85E7D86}") = "Partcounter.Tests", "tests\\Partcounter.Tests\\Partcounter.Tests.csproj", "{4D8D7611-70ED-47C7-A632-8CF704B33725}"\nEndProject\n'
    sln = sln.replace('EndProject\nGlobal', 'EndProject\n' + project + 'Global', 1)
    configs = '\t\t{4D8D7611-70ED-47C7-A632-8CF704B33725}.Debug|Any CPU.ActiveCfg = Debug|Any CPU\n\t\t{4D8D7611-70ED-47C7-A632-8CF704B33725}.Debug|Any CPU.Build.0 = Debug|Any CPU\n\t\t{4D8D7611-70ED-47C7-A632-8CF704B33725}.Release|Any CPU.ActiveCfg = Release|Any CPU\n\t\t{4D8D7611-70ED-47C7-A632-8CF704B33725}.Release|Any CPU.Build.0 = Release|Any CPU\n'
    sln = sln.replace('\tEndGlobalSection\nEndGlobal', configs + '\tEndGlobalSection\nEndGlobal', 1)
write('Partcounter.sln', sln)

# Updated integrated help.
old_help = read('src/Partcounter.App/Help/PARTCOUNTER_HILFE_R001_19.md')
old_help = re.sub(r'^# PARTCOUNTER R001\.19', '# PARTCOUNTER R001.25', old_help, count=1)
addendum = '''

## [SOURCE-01] Auftragsquellen – ARBURG ALS oder proALPHA
Kategorie: Schnittstellen
Abhängigkeiten: ADMIN-01, ALS-01
Folgewirkungen: ORDER-01
Schlagwörter: Auftragsquelle, ALS, proALPHA, ERP, führend, aktiv
Screenshot: 75_auftragsquellen.png
Screenshot-Hinweis: Administration → Auftragsquellen mit sichtbarer Auswahl der führenden Quelle aufnehmen.
---
### Grundsatz
Partcounter verwendet produktiv genau eine führende Auftragsquelle: ARBURG ALS oder proALPHA. Die inaktive Quelle darf weiterhin konfiguriert und per Verbindungstest geprüft werden, wird aber weder automatisch gepollt noch darf ein Auftrag daraus produktiv übernommen werden.

### Erstinbetriebnahme
1. Quelle wählen und speichern.
2. Vollständigkeits-/Preflight-Prüfung ohne Fehler abschließen.
3. Verbindung testen.
4. Auftrag laden und Feldmapping kontrollieren.
5. AutoStartOnApply zunächst AUS lassen.
6. Einen bekannten Auftrag nur in die Partcounter-Auftragsmaske übernehmen.
7. Maschine, Artikel, Kavitäten, VE-Menge und Sollmenge gegen das Quellsystem vergleichen.
8. Erst danach produktiv starten.

## [MODBUS-ACK-01] Bestätigte LOGO!-Befehle und Wiederholungslogik
Kategorie: Modbus / LOGO
Abhängigkeiten: MACHINE-01, ORDER-01
Folgewirkungen: COMMISSION-01
Schlagwörter: AckSequence, CommandSequence, Retry, Timeout, Reconnect
Screenshot: 63_command_ack.png
Screenshot-Hinweis: Inbetriebnahmeansicht mit CommandSequence und AckSequence aufnehmen.
---
### R001.25-Verhalten
Ein erfolgreicher TCP-Schreibaufruf gilt nicht mehr als ausreichende Befehlsbestätigung. Partcounter wartet nach jedem Steuerbefehl auf die passende AckSequence, prüft ErrorCode=0 und bei Auftrags-/VE-Parametern zusätzlich das Kavitäten-Echo.

Bleibt das ACK aus, wird derselbe Sequenzwert bis zu dreimal verwendet. Das ist absichtlich idempotent: Ein One-Shot darf bei einem verlorenen TCP-Antworttelegramm nicht doppelt ausgelöst werden.

### VE-Grenze
Nach einer abgeschlossenen VE pausiert Partcounter im verfügbaren Onlinepfad zuerst die Zählung, überträgt und bestätigt das nächste VE-Ziel und gibt danach die Zählung wieder frei.

## [BACKUP-24H-01] Datensicherung im 24/7-Betrieb
Kategorie: Einstellungen / Support
Abhängigkeiten: SETTINGS-01
Folgewirkungen: -
Schlagwörter: Backup, täglich, 24/7, Dauerbetrieb
Screenshot: 84_backup_24h.png
Screenshot-Hinweis: Supportbereich mit letzter Sicherung aufnehmen.
---
R001.25 prüft die Tagessicherung zusätzlich alle 30 Minuten. Ein wochenlang laufender Leitstand erzeugt daher auch ohne Neustart pro Kalendertag höchstens eine neue, per SQLite quick_check geprüfte Sicherung.
'''
write('src/Partcounter.App/Help/PARTCOUNTER_HILFE_R001_25.md', old_help.rstrip() + addendum + '\n')

write('README.md', '''# Partcounter

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
''')

write('docs/PROJECT_STATUS_R001_25_CHECKPOINT.md', '''# Partcounter R001.25 – Final Hardening

Branch: `r001.25-final-hardening`  
Version: `0.1.25`  
Runtime: `.NET 10 LTS`

## Freigabeziele
R001.25 schließt die bei der Gesamtprüfung von R001.24 gefundenen Software-Restpunkte: bestätigter Command/Ack-Handshake, Retry/Reconnect, sichere VE-Grenztransaktion, global koordinierte SQLite-Schreiber, echte 24/7-Tagessicherung, produktiv exklusive Auftragsquelle ALS/proALPHA, .NET-10-Lifecycle, Warnungen als Buildfehler, Unit-/Regressionstests und aktuelle Hilfe/README.

## Bewusste externe Freigabebedingungen
Eine reale Maschinenabnahme kann Softwareautomation nicht ersetzen. R001.25 ist erst nach bestandenem M01-Prüfprotokoll als Produktionsbaseline zu markieren.

## Release-Regel
Der Branch darf erst auf `main` übernommen werden, wenn Build, Unit Tests, WPF-Stresstest und Multi-Resolution-Layouttest vollständig PASS melden.
''')

write('docs/logo_v001/LOGO_V001_R001_25_COMMAND_ACK_AND_VE_BOUNDARY.md', '''# LOGO V001 – R001.25 Command/Ack- und VE-Grenzregel

Die Registerbelegung des Modbus Protocol V2 bleibt unverändert.

## Command/Ack
Die LOGO! verarbeitet One-Shot-Bits nur, wenn `CommandSequence != AckSequence`. Nach abgeschlossener Verarbeitung kopiert sie die neue CommandSequence nach AckSequence. Ein wiederholtes Telegramm mit derselben Sequenz darf den One-Shot nicht erneut auslösen.

## VE-Grenze
PC-seitig gilt im Onlinepfad: VE-Abschluss erkennen → PauseCounting senden und ACK abwarten → nächste VE-Parameter mit pauseCounting=true senden und ACK abwarten → bei laufendem Auftrag ResumeCounting senden und ACK abwarten.

## Reale Abnahme
Gezielt testen: verlorene TCP-Antwort nach Write, kein Doppel-One-Shot bei Retry, Verbindung während Zielwechsel, AckSequence-Wrap 32767 → 1.
''')

write('docs/logo_v001/LOGO_V001_R001_25_COMMAND_ACK_FLOW.svg', '''<svg xmlns="http://www.w3.org/2000/svg" width="1400" height="620" viewBox="0 0 1400 620"><style>text{font-family:Segoe UI,Arial,sans-serif;fill:#17202a}.h{font-size:28px;font-weight:700}.t{font-size:20px}.s{font-size:16px}.box{fill:#f7f9fb;stroke:#315b7d;stroke-width:2}.ok{fill:#eef8ee;stroke:#397a42;stroke-width:2}.warn{fill:#fff6df;stroke:#b77a00;stroke-width:2}.line{stroke:#315b7d;stroke-width:3;fill:none;marker-end:url(#a)}</style><defs><marker id="a" markerWidth="10" markerHeight="10" refX="9" refY="3" orient="auto"><path d="M0,0 L0,6 L9,3 z" fill="#315b7d"/></marker></defs><text x="40" y="45" class="h">Partcounter R001.25 – bestätigter LOGO!-Command/Ack- und VE-Grenzablauf</text><rect x="40" y="90" width="240" height="90" rx="8" class="box"/><text x="70" y="125" class="t">PC: CommandSequence n</text><text x="70" y="155" class="s">HR2 + CommandWord/Parameter</text><rect x="360" y="90" width="260" height="90" rx="8" class="box"/><text x="395" y="125" class="t">LOGO!: n != Ack?</text><text x="395" y="155" class="s">One-Shot höchstens einmal</text><rect x="700" y="90" width="260" height="90" rx="8" class="ok"/><text x="735" y="125" class="t">LOGO!: ausführen</text><text x="735" y="155" class="s">danach AckSequence = n</text><rect x="1040" y="90" width="300" height="90" rx="8" class="ok"/><text x="1070" y="125" class="t">PC: ACK n + ErrorCode 0</text><text x="1070" y="155" class="s">erst jetzt Befehl erfolgreich</text><path d="M280 135 H360" class="line"/><path d="M620 135 H700" class="line"/><path d="M960 135 H1040" class="line"/><rect x="40" y="245" width="300" height="90" rx="8" class="warn"/><text x="70" y="280" class="t">VE abgeschlossen</text><text x="70" y="310" class="s">CompletionSequence neu</text><rect x="410" y="245" width="250" height="90" rx="8" class="box"/><text x="455" y="280" class="t">PauseCounting</text><text x="455" y="310" class="s">ACK abwarten</text><rect x="730" y="245" width="300" height="90" rx="8" class="box"/><text x="770" y="280" class="t">nächstes VE-Ziel</text><text x="770" y="310" class="s">pause=true + ACK</text><rect x="1100" y="245" width="240" height="90" rx="8" class="ok"/><text x="1140" y="280" class="t">ResumeCounting</text><text x="1140" y="310" class="s">nur bei Running</text><path d="M340 290 H410" class="line"/><path d="M660 290 H730" class="line"/><path d="M1030 290 H1100" class="line"/><rect x="280" y="410" width="840" height="125" rx="8" class="warn"/><text x="330" y="450" class="t">Timeout / TCP-Abbruch</text><text x="330" y="485" class="s">Bis zu 3 Versuche; derselbe Sequenzwert wird erneut gesendet.</text><text x="330" y="515" class="s">Ein bereits ausgeführter One-Shot darf dadurch nicht doppelt wirken.</text><text x="40" y="590" class="s">Safety: Partcounter/LOGO! bleiben nicht-sicherheitsgerichtet.</text></svg>
''')

# Permanent R001.25 CI.
write('.github/workflows/build-r00125.yml', '''name: Build Partcounter R001.25

on:
  push:
    branches: [ r001.25-final-hardening, main ]
  workflow_dispatch:

jobs:
  build-validate-windows:
    runs-on: windows-latest
    timeout-minutes: 35
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - name: Restore
        run: dotnet restore Partcounter.sln
      - name: Build Release - warnings are errors
        run: dotnet build Partcounter.sln -c Release --no-restore
      - name: Unit and regression tests
        run: dotnet test tests/Partcounter.Tests/Partcounter.Tests.csproj -c Release --no-build --logger "trx;LogFileName=R00125.trx"
      - name: Publish portable folder win-x64
        run: dotnet publish src/Partcounter.App/Partcounter.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/Partcounter_R001_25_Portable_Folder_win-x64
      - name: Publish single-file win-x64
        run: dotnet publish src/Partcounter.App/Partcounter.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/Partcounter_R001_25_SingleFile_win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
      - name: Real WPF simulation stress test
        shell: pwsh
        run: |
          New-Item -ItemType Directory -Force -Path artifacts/Validation_R001_25 | Out-Null
          $exe=(Resolve-Path artifacts/Partcounter_R001_25_Portable_Folder_win-x64/Partcounter.exe).Path
          $report=(Join-Path (Resolve-Path artifacts/Validation_R001_25).Path STRESS_REPORT_R001_25.txt)
          $p=Start-Process $exe -ArgumentList '--stress-smoke',"--stress-report=$report" -PassThru
          if(-not $p.WaitForExit(360000)){try{$p.Kill($true)}catch{};throw 'Stress timeout'}
          Get-Content $report
          if($p.ExitCode -ne 0 -or -not (Select-String $report -Pattern 'Ergebnis: PASS' -Quiet)){throw 'Stress failed'}
      - name: Multi-resolution WPF layout smoke test
        shell: pwsh
        run: |
          $exe=(Resolve-Path artifacts/Partcounter_R001_25_Portable_Folder_win-x64/Partcounter.exe).Path
          $report=(Join-Path (Resolve-Path artifacts/Validation_R001_25).Path LAYOUT_REPORT_R001_25.txt)
          $p=Start-Process $exe -ArgumentList '--layout-smoke',"--layout-report=$report" -PassThru
          if(-not $p.WaitForExit(300000)){try{$p.Kill($true)}catch{};throw 'Layout timeout'}
          Get-Content $report
          if($p.ExitCode -ne 0 -or -not (Select-String $report -Pattern 'Ergebnis: PASS' -Quiet)){throw 'Layout failed'}
      - name: Static final-hardening audit
        shell: pwsh
        run: |
          $audit='artifacts/Validation_R001_25/STATIC_AUDIT_R001_25.txt'
          "PARTCOUNTER R001.25 STATIC AUDIT" | Set-Content $audit
          $hits=Get-ChildItem src -Recurse -File -Include *.cs,*.xaml | Select-String -Pattern 'R001\.5'
          if($hits){$hits | Out-String | Add-Content $audit; throw 'stale R001.5 UI reference found'}
          $todo=Get-ChildItem src -Recurse -File -Include *.cs,*.xaml | Select-String -Pattern 'TODO|FIXME|NotImplementedException'
          if($todo){$todo | Out-String | Add-Content $audit; throw 'unfinished marker found'}
          "PASS" | Add-Content $audit
      - name: Add engineering and documentation
        shell: pwsh
        run: |
          $targets=@('artifacts/Partcounter_R001_25_Portable_Folder_win-x64','artifacts/Partcounter_R001_25_SingleFile_win-x64')
          foreach($target in $targets){
            New-Item -ItemType Directory -Force "$target/Engineering" | Out-Null
            Copy-Item docs/*.md "$target/Engineering/" -Force
            Copy-Item docs/logo_v001 "$target/Engineering/LOGO_V001" -Recurse -Force
            Copy-Item src/Partcounter.App/Help/PARTCOUNTER_HILFE_R001_25.md "$target/Engineering/" -Force
            Copy-Item artifacts/Validation_R001_25/*.txt "$target/Engineering/" -Force
          }
          New-Item -ItemType Directory -Force artifacts/Engineering_R001_25 | Out-Null
          Copy-Item docs/*.md artifacts/Engineering_R001_25/ -Force
          Copy-Item docs/logo_v001 artifacts/Engineering_R001_25/LOGO_V001 -Recurse -Force
          Copy-Item src/Partcounter.App/Help/PARTCOUNTER_HILFE_R001_25.md artifacts/Engineering_R001_25/ -Force
          Copy-Item artifacts/Validation_R001_25/*.txt artifacts/Engineering_R001_25/ -Force
      - name: Create standardized update package
        shell: pwsh
        run: |
          $package='artifacts/UpdatePackage_R001_25';$payload="$package/payload"
          New-Item -ItemType Directory -Force $payload | Out-Null
          Copy-Item 'artifacts/Partcounter_R001_25_SingleFile_win-x64/*' $payload -Recurse -Force
          $root=(Resolve-Path $payload).Path
          Get-ChildItem $payload -Recurse -File | Sort-Object FullName | ForEach-Object {
            $r=[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\\','/')
            $h=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$h  $r"
          } | Set-Content "$package/payload-sha256.txt" -Encoding ASCII
          [ordered]@{schemaVersion=1;product='Partcounter';version='0.1.25';revision='R001.25';architecture='win-x64';payloadRoot='payload/';requireAuthenticode=$false;publisherCertificateThumbprint='';createdAtUtc=(Get-Date).ToUniversalTime().ToString('o');releaseNotes='R001.25 Final Hardening'} | ConvertTo-Json | Set-Content "$package/partcounter-update.json" -Encoding UTF8
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: Partcounter_R001_25_ValidationReports
          path: |
            artifacts/Validation_R001_25
            tests/Partcounter.Tests/TestResults
          retention-days: 90
      - uses: actions/upload-artifact@v4
        with:
          name: Partcounter_R001_25_Portable_Folder_win-x64
          path: artifacts/Partcounter_R001_25_Portable_Folder_win-x64
          retention-days: 90
      - uses: actions/upload-artifact@v4
        with:
          name: Partcounter_R001_25_SingleFile_win-x64
          path: artifacts/Partcounter_R001_25_SingleFile_win-x64
          retention-days: 90
      - uses: actions/upload-artifact@v4
        with:
          name: Partcounter_R001_25_Engineering
          path: artifacts/Engineering_R001_25
          retention-days: 90
      - uses: actions/upload-artifact@v4
        with:
          name: Partcounter_R001_25_UpdatePackage
          path: artifacts/UpdatePackage_R001_25
          retention-days: 90
''')

print('R001.25 docs/tests/workflow applied')
