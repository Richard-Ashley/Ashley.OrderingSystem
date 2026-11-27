# Starts RabbitMQ and Seq (Docker), builds the solution, and runs the three services in the
# background. Logs go to .\logs, process ids to .\logs\pids. Stop everything with stop-all.ps1.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

New-Item -ItemType Directory -Force logs | Out-Null
if ((Test-Path logs\pids) -and (Get-Content logs\pids -ErrorAction SilentlyContinue)) {
    Write-Error 'Services already started (logs\pids exists). Run scripts\stop-all.ps1 first.'
}

Write-Host 'Starting RabbitMQ and Seq...'
docker compose up -d rabbitmq seq
if ($LASTEXITCODE -ne 0) { throw 'docker compose failed' }
while ((docker inspect -f '{{.State.Health.Status}}' ordering-rabbitmq 2>$null) -ne 'healthy') {
    Start-Sleep -Seconds 2
}

# Build first: running a stale binary after a migration change crashes at startup.
Write-Host 'Building...'
dotnet build --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }

$env:ASPNETCORE_ENVIRONMENT = 'Development'

function Start-Service-Process([string]$name, [int]$port) {
    $env:ASPNETCORE_URLS = "http://localhost:$port"
    $process = Start-Process -FilePath dotnet `
        -ArgumentList "bin\Debug\net10.0\$name.dll" `
        -WorkingDirectory (Join-Path $root "services\$name") `
        -RedirectStandardOutput (Join-Path $root "logs\$name.log") `
        -RedirectStandardError (Join-Path $root "logs\$name.err.log") `
        -WindowStyle Hidden -PassThru
    Add-Content (Join-Path $root 'logs\pids') $process.Id

    for ($i = 0; $i -lt 60; $i++) {
        try {
            Invoke-WebRequest "http://localhost:$port/swagger/v1/swagger.json" -UseBasicParsing | Out-Null
            Write-Host "  $name ready on :$port"
            return
        } catch { Start-Sleep -Seconds 1 }
    }
    throw "$name did not start; see logs\$name.log"
}

# Inventory and Notification first so their queues exist before Orders publishes.
Start-Service-Process 'InventoryService' 5102
Start-Service-Process 'NotificationService' 5103
Start-Service-Process 'OrdersService' 5101

@'

Everything is running.
  OrdersService        http://localhost:5101/swagger
  InventoryService     http://localhost:5102/swagger
  NotificationService  http://localhost:5103/swagger
  RabbitMQ management  http://localhost:15672   (ordering / ordering_dev_pass)
  Seq (logs)           http://localhost:8081

Try it:
  curl.exe -X POST http://localhost:5101/api/orders -H "Content-Type: application/json" -d "{\"productId\": 1, \"quantity\": 2, \"customerEmail\": \"you@example.com\"}"
Stop with scripts\stop-all.ps1
'@ | Write-Host
