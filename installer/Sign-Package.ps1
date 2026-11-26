[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SignTool,
    [Parameter(Mandatory)] [ValidatePattern('\A[a-fA-F0-9]{40}\z')] [string] $Thumbprint,
    [Parameter(Mandatory)] [string] $EvidenceDirectory,
    [Parameter(Mandatory)] [string] $InputFile
)

$ErrorActionPreference = 'Stop'
& $SignTool sign /sha1 $Thumbprint /fd SHA256 /tr https://timestamp.digicert.com /td SHA256 $InputFile
if ($LASTEXITCODE -ne 0) { throw "SignTool no pudo firmar $InputFile." }

# ISCC embeds the signed uninstaller and deletes the temporary file.
if ([IO.Path]::GetFileName($InputFile) -like 'uninst*') {
    $snapshot = Join-Path $EvidenceDirectory ("uninstaller-signed-" + [guid]::NewGuid().ToString('N') + '.exe')
    Copy-Item -LiteralPath $InputFile -Destination $snapshot
}
