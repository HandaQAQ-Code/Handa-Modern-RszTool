[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string[]] $Path,

    [string] $OutputDirectory,

    [switch] $InPlace,

    [switch] $NoBackup,

    [switch] $Force,

    [switch] $WhatIf,

    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
    Batch-repair RE4 .scn.20 files to the original-compatible RSZ layout.

.DESCRIPTION
    Repairs the two confirmed changes from the ADA ANIMATED POSE SCN files:
      1. Removes duplicate NULL RSZ instances by rebuilding the SCN tables.
      2. Restores the known GuiType values 22 -> 21 and 99 -> 98.

    By default repaired files are written below .\scn-repaired.
    Use -InPlace to replace the source files. In-place mode creates a .bak
    copy before each successful repair unless -NoBackup is specified.

.EXAMPLE
    .\repair-scn-batch.ps1 -Path 'D:\Game\modmanager\Games\RE4R\CASE\ADA ANIMATED POSE'

.EXAMPLE
    .\repair-scn-batch.ps1 -Path 'D:\Mods\A','D:\Mods\B' -OutputDirectory 'D:\Mods\repaired' -Force

.EXAMPLE
    .\repair-scn-batch.ps1 -Path 'D:\Mods\A' -InPlace
#>

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..'))
$testProject = Join-Path $repoRoot 'RszTool.Test\RszTool.Test.csproj'
$testDll = Join-Path $repoRoot 'RszTool.Test\bin\Release\net8.0\RszTool.Test.dll'
$runtimeJson = Join-Path $repoRoot 'RszTool.App\bin\Release\net8.0-windows\rszre4.json'

function Get-ScnFiles {
    param([string[]] $Inputs)

    $results = [Collections.Generic.List[string]]::new()
    foreach ($item in $Inputs) {
        $resolved = (Resolve-Path -LiteralPath $item).Path
        $entry = Get-Item -LiteralPath $resolved
        if ($entry.PSIsContainer) {
            Get-ChildItem -LiteralPath $entry.FullName -Recurse -File -Filter '*.scn.20' |
                ForEach-Object { $results.Add($_.FullName) }
        }
        elseif ($entry.Name -like '*.scn.20') {
            $results.Add($entry.FullName)
        }
        else {
            throw "Not an .scn.20 file or directory: $item"
        }
    }

    return @($results | Sort-Object -Unique)
}

function Get-OutputPath {
    param(
        [string] $InputFile,
        [string[]] $OriginalInputs,
        [string] $OutputRoot
    )

    $directoryRoots = @(
        $OriginalInputs |
            ForEach-Object { (Resolve-Path -LiteralPath $_).Path } |
            ForEach-Object {
                $entry = Get-Item -LiteralPath $_
                if ($entry.PSIsContainer) { $entry }
            }
    )

    foreach ($root in $directoryRoots) {
        $relative = [IO.Path]::GetRelativePath($root.FullName, $InputFile)
        if (-not $relative.StartsWith('..') -and -not [IO.Path]::IsPathRooted($relative)) {
            return Join-Path (Join-Path $OutputRoot $root.Name) $relative
        }
    }

    return Join-Path $OutputRoot ([IO.Path]::GetFileName($InputFile))
}

if (-not $InPlace -and [string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Get-Location) 'scn-repaired'
}

$files = @(Get-ScnFiles -Inputs $Path)
if ($files.Count -eq 0) {
    throw 'No .scn.20 files found.'
}

if (-not $NoBuild) {
    Write-Host "Building repair tool..."
    & dotnet build $testProject --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "RszTool.Test build failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $runtimeJson)) {
        $appProject = Join-Path $repoRoot 'RszTool.App\RszTool.App.csproj'
        & dotnet build $appProject --configuration Release --framework net8.0-windows --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "RszTool.App build failed with exit code $LASTEXITCODE."
        }
    }
}

if (-not (Test-Path -LiteralPath $testDll)) {
    throw "Repair tool not found: $testDll"
}
if (-not (Test-Path -LiteralPath $runtimeJson)) {
    throw "RE4 schema not found: $runtimeJson"
}

$counts = @{
    Repaired = 0
    Unchanged = 0
    Skipped = 0
    Failed = 0
}

foreach ($file in $files) {
    if ($InPlace) {
        $destination = $file
        $backup = "$file.bak"
        if ((Test-Path -LiteralPath $backup) -and $Force) {
            $backup = "$file.bak.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        }
    }
    else {
        $destination = Get-OutputPath -InputFile $file -OriginalInputs $Path -OutputRoot $OutputDirectory
        if ((Test-Path -LiteralPath $destination) -and -not $Force) {
            Write-Warning "Output exists; use -Force to overwrite: $destination"
            $counts.Skipped++
            continue
        }
    }

    if ($WhatIf) {
        Write-Host "[WhatIf] $file -> $destination"
        continue
    }

    if ($InPlace -and -not $NoBackup) {
        Copy-Item -LiteralPath $file -Destination $backup -Force
        Write-Host "Backup: $backup"
    }

    $destinationDirectory = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null

    $output = & dotnet $testDll repair-scn $file $destination 2>&1
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }

    if ($exitCode -eq 0) {
        $counts.Repaired++
    }
    elseif ($exitCode -eq 2) {
        if (-not $InPlace) {
            Copy-Item -LiteralPath $file -Destination $destination -Force
        }
        $counts.Unchanged++
    }
    else {
        $counts.Failed++
        Write-Warning "Repair failed: $file"
    }
}

Write-Host ''
Write-Host ("Summary: files={0}, repaired={1}, unchanged={2}, skipped={3}, failed={4}" -f `
    $files.Count, $counts.Repaired, $counts.Unchanged, $counts.Skipped, $counts.Failed)

if ($counts.Failed -gt 0) {
    exit 1
}

exit 0
