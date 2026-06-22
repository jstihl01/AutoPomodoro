[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$publish = Join-Path $root 'artifacts\publish'
$dist = Join-Path $root 'dist'

dotnet restore (Join-Path $root 'AutoPomodoro.sln')
if ($LASTEXITCODE -ne 0) { throw 'Fallo al restaurar la solucion.' }

dotnet build (Join-Path $root 'src\Pomodoro.App\Pomodoro.App.csproj') -c Release --no-restore --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Fallo al compilar la aplicacion.' }

dotnet build (Join-Path $root 'tests\Pomodoro.Tests\Pomodoro.Tests.csproj') -c Release --no-restore --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Fallo al compilar las pruebas.' }

dotnet run --project (Join-Path $root 'tests\Pomodoro.Tests\Pomodoro.Tests.csproj') -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Las pruebas no se han superado.' }

$publishFullPath = [System.IO.Path]::GetFullPath($publish)
$workspacePrefix = [System.IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
if (-not $publishFullPath.StartsWith($workspacePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'La carpeta de publicacion no pertenece al workspace.'
}
if (Test-Path -LiteralPath $publishFullPath) {
    Remove-Item -LiteralPath $publishFullPath -Recurse -Force
}

dotnet publish (Join-Path $root 'src\Pomodoro.App\Pomodoro.App.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -o $publish --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Fallo al publicar la aplicacion autocontenida.' }

$compilerCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'No se encontro ISCC.exe. Instala Inno Setup 6.'
}

New-Item -ItemType Directory -Path $dist -Force | Out-Null
& $compiler (Join-Path $root 'installer\AutoPomodoro.iss')
if ($LASTEXITCODE -ne 0) { throw 'Fallo al generar el instalador.' }

$setup = Join-Path $dist 'AutoPomodoro-Setup.exe'
$hash = (Get-FileHash -Algorithm SHA256 $setup).Hash.ToLowerInvariant()
"$hash  AutoPomodoro-Setup.exe" | Set-Content -Encoding ascii (Join-Path $dist 'AutoPomodoro-Setup.exe.sha256')
Write-Host "Instalador: $setup"
Write-Host "SHA-256: $hash"
