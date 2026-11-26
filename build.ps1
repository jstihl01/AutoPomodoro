[CmdletBinding()]
param(
    [string] $DotNet = 'dotnet',
    [string] $InnoSetup,
    [switch] $TestInstaller,
    [switch] $Release,
    [string] $SignTool,
    [string] $CertificateThumbprint
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$minimumInno = [version]'6.3.0'
$sourcePaths = @('src', 'tests', 'installer', 'assets', 'AutoPomodoro.sln', 'build.ps1', 'global.json', 'README.md')

function Assert-PlainDirectory([string] $Path) {
    for ($item = Get-Item -LiteralPath $Path -Force; $null -ne $item; $item = $item.Parent) {
        if ($item -isnot [IO.DirectoryInfo] -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Directorio de compilacion no seguro: $($item.FullName)"
        }
    }
}

function Remove-BuildDirectory([string] $Path, [string] $RunRoot) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = $RunRoot.TrimEnd('\') + '\'
    if ($full -ne $RunRoot -and -not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Ruta fuera de esta compilacion: $full"
    }
    Assert-PlainDirectory $full
    foreach ($item in Get-ChildItem -LiteralPath $full -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Punto de reanalisis inesperado: $($item.FullName)"
        }
        if ($item.PSIsContainer) { Remove-BuildDirectory $item.FullName $RunRoot }
        else { [IO.File]::Delete($item.FullName) }
    }
    [IO.Directory]::Delete($full)
}

function Test-BuildPathGuard([string] $RunRoot) {
    $target = Join-Path $RunRoot 'path-guard-target'
    $link = Join-Path $RunRoot 'path-guard-junction'
    New-Item -ItemType Directory -Path $target | Out-Null
    New-Item -ItemType Junction -Path $link -Target $target | Out-Null
    try {
        $rejected = $false
        try { Assert-PlainDirectory $link } catch { $rejected = $true }
        if (-not $rejected -or -not (Test-Path -LiteralPath $target -PathType Container)) {
            throw 'La proteccion de rutas no rechazo el enlace de prueba.'
        }
        Write-Host 'OK SEG-03: se rechaza un punto de reanalisis en la ruta de compilacion.'
    }
    finally { [IO.Directory]::Delete($link) }
}

function Get-Compiler {
    $candidates = @($InnoSetup)
    $onPath = Get-Command ISCC.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($onPath) { $candidates += $onPath.Source }
    foreach ($installBase in @([Environment]::GetFolderPath('ProgramFilesX86'), $env:ProgramFiles, $env:LOCALAPPDATA)) {
        if ($installBase) {
            $candidates += Join-Path $installBase 'Inno Setup 6\ISCC.exe'
            $candidates += Join-Path $installBase 'Programs\Inno Setup 6\ISCC.exe'
        }
    }

    $probe = @(
        '[Setup]'
        'AppName=AutoPomodoro compiler probe'
        'AppVersion=0'
        'DefaultDirName={localappdata}\AutoPomodoroProbe'
        'PrivilegesRequired=lowest'
    )
    $found = @()
    foreach ($candidate in ($candidates | Where-Object { $_ } | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        $path = (Get-Item -LiteralPath $candidate).FullName
        $output = $probe | & $path '/O-' '-'
        $match = [regex]::Match(($output -join ' '), 'Compiler engine version:\s*(?:Inno Setup )?(\d+\.\d+\.\d+(?:\.\d+)?)')
        if ($LASTEXITCODE -eq 0 -and $match.Success) {
            $detected = [version]$match.Groups[1].Value
            if ($detected -ge $minimumInno) { return @{ Path = $path; Version = $detected.ToString() } }
            $found += "$path ($detected)"
        }
        else {
            $found += "$path (version desconocida)"
        }
    }
    throw "Se requiere Inno Setup $minimumInno o posterior. Detectados: $($found -join ', ')."
}

function Read-PublishedRuntime([string] $Exe) {
    $start = [Diagnostics.ProcessStartInfo]::new($Exe, '--runtime-info')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill()
            $process.WaitForExit()
            throw 'La aplicacion autocontenida no respondio a la prueba de arranque.'
        }
        if ($process.ExitCode -ne 0) { throw "Fallo en la aplicacion publicada: $($errors.GetAwaiter().GetResult())" }
        return ($output.GetAwaiter().GetResult() | ConvertFrom-Json)
    }
    finally { $process.Dispose() }
}

function Confirm-Signature([string] $File) {
    $signature = Get-AuthenticodeSignature -LiteralPath $File
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint -or
        -not $signature.TimeStamperCertificate) {
        throw "Firma Authenticode, editor o sello de tiempo incorrectos: $File ($($signature.Status))."
    }
}

Push-Location -LiteralPath $root
try {
    Assert-PlainDirectory $root
    $inno = Get-Compiler
    $sdk = & $DotNet --version
    if ($LASTEXITCODE -ne 0) { throw 'No se encontro el SDK seleccionado en global.json.' }
    $commit = git rev-parse --verify HEAD
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo identificar el commit de origen.' }
    $changes = git --no-optional-locks status --porcelain --untracked-files=all -- @sourcePaths
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo comprobar el estado de los archivos fuente.' }

    $signing = @()
    if ($Release) {
        if ($changes) { throw 'Una publicacion requiere que los cambios de origen esten en Git.' }
        if ($CertificateThumbprint -notmatch '\A[a-fA-F0-9]{40}\z' -or -not $SignTool) {
            throw 'Una publicacion requiere -CertificateThumbprint y -SignTool.'
        }
        $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object {
            $_.Thumbprint -eq $CertificateThumbprint -and $_.HasPrivateKey
        } | Select-Object -First 1
        if (-not $cert) { throw 'No se encontro el certificado de firma con clave privada en CurrentUser\My.' }
        $signExe = (Get-Item -LiteralPath $SignTool).FullName
    }
    elseif ($CertificateThumbprint -or $SignTool) {
        throw 'Usa -Release para producir una entrega firmada.'
    }

    $project = Join-Path $root 'src\Pomodoro.App\Pomodoro.App.csproj'
    $testProject = Join-Path $root 'tests\Pomodoro.Tests\Pomodoro.Tests.csproj'
    $recipe = Join-Path $root 'installer\AutoPomodoro.iss'
    $projectXml = [xml](Get-Content -Raw -LiteralPath $project)
    $version = $projectXml.Project.PropertyGroup.Version
    if ($version -notmatch '\A(?<numeric>\d+\.\d+\.\d+)(-[a-zA-Z0-9]+([.-][a-zA-Z0-9]+)*)?\z') {
        throw "Version de aplicacion invalida: $version"
    }
    $numericVersion = $Matches.numeric
    $binaryVersion = "$numericVersion.0"
    $setupName = "AutoPomodoro-Setup-v$version.exe"

    $artifacts = Join-Path $root 'artifacts'
    if (-not (Test-Path -LiteralPath $artifacts)) { New-Item -ItemType Directory -Path $artifacts | Out-Null }
    Assert-PlainDirectory $artifacts
    $run = Join-Path $artifacts ("build-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $run | Out-Null
    Assert-PlainDirectory $run
    $publish = Join-Path $run 'publish'
    $staged = Join-Path $run 'setup'
    New-Item -ItemType Directory -Path $publish, $staged | Out-Null
    if ($Release) {
        $hostExe = (Get-Process -Id $PID).Path.Replace('$', '$$')
        $signScript = (Join-Path $root 'installer\Sign-Package.ps1').Replace('$', '$$')
        $signToolPath = $signExe.Replace('$', '$$')
        $signedOutput = $staged.Replace('$', '$$')
        $signCommand = '$q{0}$q -NoProfile -NonInteractive -File $q{1}$q -SignTool $q{2}$q -Thumbprint {3} -EvidenceDirectory $q{4}$q -InputFile $f'
        $signing = @('/DMySignInstaller', ("/SAutoPomodoroSign=" +
            ($signCommand -f $hostExe, $signScript, $signToolPath, $CertificateThumbprint, $signedOutput)))
    }
    Write-Host "Compilacion: $run"

    & $DotNet build (Join-Path $root 'AutoPomodoro.sln') -c Release --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'Fallo al compilar la solucion.' }
    & $DotNet run --project $testProject -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Las pruebas no se han superado.' }

    $publishArgs = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=None', '-p:DebugSymbols=false', '-o', $publish, '--disable-build-servers')
    & $DotNet publish $project @publishArgs
    if ($LASTEXITCODE -ne 0) { throw 'Fallo al publicar la aplicacion autocontenida.' }

    $appExe = Join-Path $publish 'AutoPomodoro.exe'
    $runtime = Read-PublishedRuntime $appExe
    if ($runtime.Version -ne $numericVersion -or $runtime.Architecture -ne 'X64' -or
        $runtime.RuntimeVersion -notlike '10.0.*' -or $runtime.WindowsDesktopVersion -notlike '10.0.*') {
        throw "Datos inesperados en la aplicacion publicada: $($runtime | ConvertTo-Json -Compress)"
    }

    $compilerArgs = @("/DMyAppVersion=$version", "/DMyBinaryVersion=$binaryVersion", "/DMyPublishDir=$publish")
    & $inno.Path @compilerArgs @signing "/O$staged" $recipe
    if ($LASTEXITCODE -ne 0) { throw 'Fallo al generar el instalador.' }

    $setup = Join-Path $staged $setupName
    $setupInfo = (Get-Item -LiteralPath $setup).VersionInfo
    $actualVersion = '{0}.{1}.{2}.{3}' -f $setupInfo.FileMajorPart, $setupInfo.FileMinorPart, $setupInfo.FileBuildPart, $setupInfo.FilePrivatePart
    if ($actualVersion -ne $binaryVersion -or $setupInfo.ProductVersion.Trim() -ne $version) {
        throw "La version del instalador no coincide: $actualVersion, $($setupInfo.ProductVersion)."
    }
    if ($Release) {
        Confirm-Signature $setup
        $uninstallers = @(Get-ChildItem -LiteralPath $staged -File -Filter 'uninstaller-signed-*.exe')
        if ($uninstallers.Count -eq 0) { throw 'No se obtuvo una copia verificable del desinstalador firmado.' }
        foreach ($uninstaller in $uninstallers) { Confirm-Signature $uninstaller.FullName }

        $current = git rev-parse --verify HEAD
        if ($LASTEXITCODE -ne 0 -or $current -ne $commit) { throw 'El commit de origen cambio durante la compilacion.' }
        $currentChanges = git --no-optional-locks status --porcelain --untracked-files=all -- @sourcePaths
        if ($LASTEXITCODE -ne 0 -or $currentChanges) { throw 'Los archivos de origen cambiaron durante la compilacion.' }
    }

    if ($TestInstaller) {
        Test-BuildPathGuard $run
        $fixtureArgs = @{
            Compiler = $inno.Path; Recipe = $recipe; CompilerArgs = $compilerArgs; BuildRoot = $run; ApplicationPath = $appExe
        }
        & (Join-Path $root 'tests\Test-Installer.ps1') @fixtureArgs
    }

    $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $setupName" | Set-Content -Encoding ascii -LiteralPath "$setup.sha256"
    [ordered]@{
        file = $setupName; sha256 = $hash; version = $version; commit = $commit; sourceModified = [bool]$changes
        sdk = $sdk; innoSetup = $inno.Version; runtimeIdentifier = 'win-x64'
        dotnetRuntime = $runtime.RuntimeVersion; windowsDesktopRuntime = $runtime.WindowsDesktopVersion
        applicationSha256 = (Get-FileHash -LiteralPath $appExe -Algorithm SHA256).Hash.ToLowerInvariant()
        signed = [bool]$Release; signerThumbprint = $CertificateThumbprint
        installerRegression = [bool]$TestInstaller
    } | ConvertTo-Json | Set-Content -Encoding ascii -LiteralPath "$setup.build.json"

    $releases = Join-Path $root 'releases'
    if (-not (Test-Path -LiteralPath $releases)) { New-Item -ItemType Directory -Path $releases | Out-Null }
    Assert-PlainDirectory $releases
    foreach ($file in @($setup, "$setup.sha256", "$setup.build.json")) {
        Move-Item -LiteralPath $file -Destination $releases -Force
    }
    Write-Host "Instalador: $(Join-Path $releases $setupName)"
    Write-Host "SHA-256: $hash"
    try { Remove-BuildDirectory $run $run }
    catch { Write-Warning "No se pudo limpiar $run. $($_.Exception.Message)" }
}
finally { Pop-Location }
