#!/usr/bin/env pwsh
# Build script for AmbilightSmoothed plugin
# Run from the project root directory

param(
    [string]$Configuration = "Release"
)

# Get the directory where this script is located (project root)
$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot "Artemis.Plugins.LayerBrushes.AmbilightSmoothed.csproj"
$outputDir = Join-Path $projectRoot "bin\$Configuration\net10.0-windows"
$zipOutputDir = Join-Path $projectRoot "bin\$Configuration"
$zipFile = Join-Path $zipOutputDir "AmbilightSmoothed.zip"
$tempDir = Join-Path $zipOutputDir "temp_zip"

Write-Host "=== Building AmbilightSmoothed Plugin ===" -ForegroundColor Cyan
Write-Host "Project Root: $projectRoot" -ForegroundColor Gray
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host ""

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build $projectFile -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n=== Build Failed ===" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Build Succeeded ===" -ForegroundColor Green

# Create plugin package
Write-Host "`nCreating plugin package..." -ForegroundColor Yellow

# Clean up temp directory if it exists
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}

# Create temp directory and copy files
New-Item -ItemType Directory -Path $tempDir | Out-Null
Copy-Item "$outputDir\*" -Destination $tempDir -Recurse -Exclude "*.zip"

# Remove existing zip if present
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

# Create the zip file
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $zipFile, [System.IO.Compression.CompressionLevel]::Optimal, $false)

# Clean up temp directory
Remove-Item $tempDir -Recurse -Force

# Display package info
Write-Host "`n=== Package Created ===" -ForegroundColor Green
$packageInfo = Get-Item $zipFile | Select-Object Name, @{Name="Size (KB)";Expression={[math]::Round($_.Length/1KB, 2)}}, LastWriteTime
$packageInfo | Format-List
Write-Host "Location: $zipFile" -ForegroundColor Cyan
