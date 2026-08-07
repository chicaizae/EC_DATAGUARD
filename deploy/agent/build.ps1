param(
    [string]$OutDir = "dist"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$dist = Join-Path $PSScriptRoot $OutDir

function Get-Sha256($path) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    return "sha256:$hash"
}

Write-Host "==> Publicando agente Legacy (Windows 7, x86, self-contained) <=="
dotnet publish (Join-Path $root "src/Agent/EcDataguard.Agent.Legacy/EcDataguard.Agent.Legacy.csproj") `
    -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true `
    -o (Join-Path $dist "win7")

Write-Host "==> Publicando agente moderno (Windows 10/11, x64) <=="
dotnet publish (Join-Path $root "src/Agent/EcDataguard.Agent/EcDataguard.Agent.csproj") `
    -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true `
    -o (Join-Path $dist "win10")

Write-Host "==> Publicando agente Linux (x64) <=="
dotnet publish (Join-Path $root "src/Agent/EcDataguard.Agent/EcDataguard.Agent.csproj") `
    -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true `
    -o (Join-Path $dist "linux")
if (Test-Path (Join-Path $dist "linux/EcDataguardAgent")) {
    Move-Item (Join-Path $dist "linux/EcDataguardAgent") (Join-Path $dist "linux/ecdataguard-agent") -Force
}

$manifest = [ordered]@{
    product = "EcDataguard Agent"
    version = "1.0.0"
    builds  = @(
        [ordered]@{ os = "windows"; tag = "win7";  arch = "x86"; rid = "win-x86"; framework = "net6.0-windows7.0"; file = "EcDataguardAgent7.exe"; path = "win7/EcDataguardAgent7.exe"; sha256 = Get-Sha256 (Join-Path $dist "win7/EcDataguardAgent7.exe"); size = (Get-Item (Join-Path $dist "win7/EcDataguardAgent7.exe")).Length },
        [ordered]@{ os = "windows"; tag = "win10"; arch = "x64"; rid = "win-x64"; framework = "net8.0";              file = "EcDataguardAgent.exe";    path = "win10/EcDataguardAgent.exe";   sha256 = Get-Sha256 (Join-Path $dist "win10/EcDataguardAgent.exe"); size = (Get-Item (Join-Path $dist "win10/EcDataguardAgent.exe")).Length },
        [ordered]@{ os = "linux";   tag = "linux";  arch = "x64"; rid = "linux-x64"; framework = "net8.0";           file = "ecdataguard-agent";        path = "linux/ecdataguard-agent";      sha256 = Get-Sha256 (Join-Path $dist "linux/ecdataguard-agent");        size = (Get-Item (Join-Path $dist "linux/ecdataguard-agent")).Length }
    )
}

$manifestPath = Join-Path $dist "manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "==> Manifiesto generado en $manifestPath"
Write-Host "Listo. Copie el contenido de '$dist' a la descarga pÃºblica (volumen 'agents')."
