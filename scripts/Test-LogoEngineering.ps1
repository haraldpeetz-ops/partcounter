param(
    [string]$ConnectionMatrix = "docs/logo_v001/LOGO_V001_BLOCK_CONNECTIONS_R001_25.csv",
    [string]$VmMap = "docs/logo_v001/LOGO_V001_VM_MAP_R001_25.csv",
    [string]$TestCases = "docs/logo_v001/LOGO_V001_TEST_CASES_R001_25.csv"
)

$ErrorActionPreference = "Stop"

$connections = Import-Csv -Delimiter ";" -LiteralPath $ConnectionMatrix
$registers = Import-Csv -Delimiter ";" -LiteralPath $VmMap
$tests = Import-Csv -Delimiter ";" -LiteralPath $TestCases
$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

foreach ($column in @("Network", "Block", "BlockType", "SignalName", "InputOrParameter", "SourceOrValue", "OutputDestination")) {
    if ($column -notin $connections[0].PSObject.Properties.Name) {
        $failures.Add("Connection matrix is missing column '$column'.")
    }
}

$blockPattern = "\bB\d{3}[A-Z]?\b"
$definedBlocks = @($connections.Block | Where-Object { $_ -match "^B\d{3}[A-Z]?$" } | Sort-Object -Unique)
$referencedBlocks = @(
    foreach ($row in $connections) {
        foreach ($field in @($row.SourceOrValue, $row.OutputDestination, $row.PurposeOrImportantSetting)) {
            [regex]::Matches([string]$field, $blockPattern) | ForEach-Object Value
        }
    }
) | Sort-Object -Unique

foreach ($reference in $referencedBlocks) {
    if ($reference -notin $definedBlocks) {
        $failures.Add("Referenced block '$reference' has no definition row.")
    }
}

foreach ($block in $definedBlocks) {
    $rows = @($connections | Where-Object Block -eq $block)
    if (-not ($rows.OutputDestination | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $failures.Add("Block '$block' has no documented output destination.")
    }
}

$expectedPcRegisters = 1..13 | ForEach-Object { "HR$_" }
$expectedLogoRegisters = 20..40 | ForEach-Object { "HR$_" }
$protocolRows = @($registers | Where-Object { -not [string]::IsNullOrWhiteSpace($_.HoldingRegister) })
$actualRegisters = @($protocolRows.HoldingRegister)
foreach ($register in @($expectedPcRegisters + $expectedLogoRegisters)) {
    if ($register -notin $actualRegisters) {
        $failures.Add("Protocol V3 register '$register' is missing from the VM map.")
    }
}
if (($actualRegisters | Sort-Object -Unique).Count -ne $actualRegisters.Count) {
    $failures.Add("The VM map contains duplicate holding-register definitions.")
}
if (($protocolRows.PCOffset | Sort-Object -Unique).Count -ne $protocolRows.Count) {
    $failures.Add("The VM map contains duplicate PC offsets.")
}
if (($registers.LOGOVM | Sort-Object -Unique).Count -ne $registers.Count) {
    $failures.Add("The VM map contains duplicate LOGO VM addresses.")
}

$testNumbers = @($tests.TestId | ForEach-Object { [int]($_ -replace "^T", "") } | Sort-Object)
if (($tests.TestId | Sort-Object -Unique).Count -ne $tests.Count) {
    $failures.Add("The LOGO test matrix contains duplicate TestId values.")
}
if (($testNumbers -join ",") -ne ((1..$testNumbers.Count) -join ",")) {
    $failures.Add("The LOGO test IDs are not contiguous from T01 through T$($testNumbers.Count.ToString('00')).")
}

$allConnectionText = ($connections | ForEach-Object {
    $_.PSObject.Properties.Value -join " "
}) -join " "
if ($allConnectionText -notmatch "VW68") {
    $warnings.Add("HR35/VW68 ErrorCode is listed in the VM map but has no concrete producer in the connection matrix.")
}
if ($allConnectionText -match "AlarmLatch" -and $connections.Block -notcontains "AlarmLatch") {
    $warnings.Add("AlarmLatch is referenced symbolically but is not expanded into concrete block rows.")
}
$configRows = @($connections | Where-Object Block -eq "ConfigValid")
if ($configRows.Count -gt 0 -and ($configRows.BlockType | Sort-Object -Unique) -contains "Composite validation") {
    $warnings.Add("ConfigValid is documented as a composite condition, not as a native LOGO! block-by-block network.")
}

Write-Output "LOGO engineering audit"
Write-Output "  Connection rows : $($connections.Count)"
Write-Output "  Defined B blocks : $($definedBlocks.Count)"
Write-Output "  Protocol mappings: $($protocolRows.Count) (+$($registers.Count - $protocolRows.Count) internal)"
Write-Output "  Test cases       : $($tests.Count)"
Write-Output "  Structural errors: $($failures.Count)"
Write-Output "  Review warnings  : $($warnings.Count)"

foreach ($warning in $warnings) {
    Write-Warning $warning
}
foreach ($failure in $failures) {
    Write-Output "ERROR: $failure"
}

if ($failures.Count -gt 0) {
    exit 1
}
