$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repo 'obj\regression'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $outDir 'ScenarioRegression.exe'
$sources = @(
    'ScenarioCalloutBase.cs', 'ScenarioProgress.cs',
    'AbandonedVehicleCountryside.cs', 'SuspiciousGasStation.cs',
    'BarricadedSuspect.cs', 'OrganizedVehicleTheft.cs',
    'PoliceVehicleAccident.cs', 'BusIncidentFeetOnSeats.cs',
    'tests\ApiDoubles.cs', 'tests\ScenarioRegression.cs'
) | ForEach-Object { Join-Path $repo $_ }
& $compiler /nologo /target:exe "/out:$exe" /r:System.Drawing.dll /r:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Regression harness compilation failed.' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Scenario regression failed.' }

