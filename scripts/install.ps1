$ErrorActionPreference = "Stop"

Write-Host "Installing Agelos..."

$arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq "Arm64") { "arm64" } else { "x64" }
$binary = "Agelos-win-$arch.exe"
$url = "https://github.com/yourname/agelos/releases/latest/download/$binary"

$installDir = "$env:USERPROFILE\.local\bin"
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

$dest = "$installDir\agelos.exe"

Write-Host "Downloading $binary..."
Invoke-WebRequest -Uri $url -OutFile $dest

# Add to user PATH if not already present
$currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($currentPath -notlike "*$installDir*") {
    [Environment]::SetEnvironmentVariable("PATH", "$currentPath;$installDir", "User")
    Write-Host "Added $installDir to your PATH."
    Write-Host "Restart your terminal for the change to take effect."
}

Write-Host ""
Write-Host "Agelos installed successfully to $dest"
Write-Host ""
Write-Host "Get started:"
Write-Host "  mkdir my-project; cd my-project"
Write-Host "  agelos run opencode"
Write-Host ""

