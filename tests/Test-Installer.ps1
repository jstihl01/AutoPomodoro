[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Compiler,
    [Parameter(Mandatory)] [string] $Recipe,
    [Parameter(Mandatory)] [string[]] $CompilerArgs,
    [Parameter(Mandatory)] [string] $BuildRoot,
    [Parameter(Mandatory)] [string] $ApplicationPath
)

$ErrorActionPreference = 'Stop'
$id = [guid]::NewGuid().ToString('N')
$fixture = Join-Path $BuildRoot "installer-test-$id"
$installed = Join-Path $fixture 'installed'
$modern = Join-Path $fixture 'profile\AutoPomodoro'
$legacy = Join-Path $fixture 'profile\Pomodoro'
New-Item -ItemType Directory -Path $modern, $legacy | Out-Null

function Invoke-FixtureSetup([string] $Executable, [string] $LogName) {
    $log = Join-Path $fixture "$LogName.log"
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOCANCEL', ('/LOG="{0}"' -f $log))
    $process = Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Fallo en $LogName ($($process.ExitCode)). Consulta $log." }
}

$fixtureInstaller = Join-Path $fixture 'AutoPomodoro-Installer-Test.exe'
& $Compiler @CompilerArgs "/DMyInstallerTestId=$id" "/DMyInstallerTestRoot=$fixture" "/O$fixture" '/FAutoPomodoro-Installer-Test' $Recipe
if ($LASTEXITCODE -ne 0) { throw 'Fallo al compilar el instalador de prueba.' }

$owned = @(
    'settings.json'
    'settings.json.bak'
    'settings.json.tmp'
    'settings.json.0123456789abcdef0123456789abcdef.tmp'
    'settings.json.0123456789abcdef0123456789abcdef.recovered'
)
foreach ($file in $owned) { '{"VolumePercent":31}' | Set-Content -LiteralPath (Join-Path $modern $file) -Encoding ascii }
$otherFiles = @(
    (Join-Path $modern 'mis-datos.txt')
    (Join-Path $modern 'settings.json.manual.tmp')
    (Join-Path $legacy 'settings.json')
    (Join-Path $legacy 'otro-proyecto.txt')
)
foreach ($file in $otherFiles) { 'conservar' | Set-Content -LiteralPath $file -Encoding ascii }

Invoke-FixtureSetup $fixtureInstaller 'install'
$installedApp = Join-Path $installed 'AutoPomodoro.exe'
$registry = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AutoPomodoroTest-$($id)_is1"
if (-not (Test-Path -LiteralPath $registry)) { throw 'Falta el registro de la instalacion de prueba.' }
$expectedHash = (Get-FileHash -LiteralPath $ApplicationPath -Algorithm SHA256).Hash
if ((Get-FileHash -LiteralPath $installedApp -Algorithm SHA256).Hash -ne $expectedHash) { throw 'La aplicacion instalada no coincide.' }

'binario sustituido para la prueba' | Set-Content -LiteralPath $installedApp -Encoding ascii
$oldExe = Join-Path $installed 'Pomodoro.exe'
'archivo anterior para la prueba' | Set-Content -LiteralPath $oldExe -Encoding ascii
Invoke-FixtureSetup $fixtureInstaller 'reinstall'
if ((Get-FileHash -LiteralPath $installedApp -Algorithm SHA256).Hash -ne $expectedHash -or (Test-Path -LiteralPath $oldExe)) {
    throw 'La reinstalacion no sustituyo los ejecutables.'
}
foreach ($file in $owned) {
    if ((Get-Content -LiteralPath (Join-Path $modern $file) -Raw).Trim() -ne '{"VolumePercent":31}') {
        throw "La reinstalacion altero las preferencias: $file."
    }
}

Invoke-FixtureSetup (Join-Path $installed 'unins000.exe') 'uninstall'
if ((Test-Path -LiteralPath $installedApp) -or (Test-Path -LiteralPath $registry)) {
    throw 'La desinstalacion no retiro la aplicacion de prueba.'
}
foreach ($file in $owned) {
    if (Test-Path -LiteralPath (Join-Path $modern $file)) { throw "No se retiro un archivo propio: $file." }
}
foreach ($file in $otherFiles) {
    if ((Get-Content -LiteralPath $file -Raw).Trim() -ne 'conservar') { throw "La desinstalacion altero un archivo ajeno: $file." }
}
Write-Host 'OK SEG-02: instalacion, reinstalacion y desinstalacion en un perfil de prueba aislado.'
