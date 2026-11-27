# Stops the three services started by start-all.ps1. Pass -Infra to also stop RabbitMQ and Seq.
param([switch]$Infra)

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (Test-Path logs\pids) {
    foreach ($id in Get-Content logs\pids) {
        if ($id) { Stop-Process -Id ([int]$id) -Force -ErrorAction SilentlyContinue }
    }
    Remove-Item logs\pids
    Write-Host 'Services stopped.'
} else {
    Write-Host 'No logs\pids file; nothing to stop.'
}

if ($Infra) { docker compose stop rabbitmq seq }
