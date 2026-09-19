# Installs the Lapper dev/alpha build.
# Run from an elevated PowerShell in the folder containing Lapper.msix and
# Lapper-Dev.cer:   powershell -ExecutionPolicy Bypass -File .\Install-Lapper.ps1

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Please run this script as Administrator (needed once, to trust the signing certificate)."
}

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$cer = Join-Path $here "Lapper-Dev.cer"
$msix = Join-Path $here "Lapper.msix"

Write-Host "1/2 Trusting the Lapper dev signing certificate (local machine, Trusted People)..."
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

Write-Host "2/2 Installing Lapper..."
Add-AppxPackage -Path $msix

Write-Host ""
Write-Host "Done. Find 'Lapper' in the Start menu."
Write-Host "Remember: the backend must be running (see INSTALL.md) before pressing Ctrl+Alt+L."
