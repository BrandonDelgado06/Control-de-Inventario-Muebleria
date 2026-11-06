# Abre el sistema para probarlo en el navegador.
# Levanta la API (http://localhost:5000) y el frontend (http://localhost:5173), cada uno en su
# propia ventana, y abre el navegador cuando los dos responden.
# Para detenerlos, cierra esas dos ventanas.
#
# Uso (desde la raíz del repositorio):  powershell -ExecutionPolicy Bypass -File scripts\iniciar.ps1

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')

function EnUso([int]$puerto) {
  [bool](Get-NetTCPConnection -LocalPort $puerto -State Listen -ErrorAction SilentlyContinue)
}

function Esperar([string]$url, [int]$segundos) {
  $limite = (Get-Date).AddSeconds($segundos)
  while ((Get-Date) -lt $limite) {
    try { Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 3 | Out-Null; return $true }
    catch { if ($_.Exception.Response) { return $true } } # 401 también significa que ya responde
    Start-Sleep -Seconds 2
  }
  return $false
}

if (-not (Test-Path "$raiz\src\frontend\node_modules")) {
  Write-Host 'Instalando dependencias del frontend (solo la primera vez)...'
  npm --prefix "$raiz\src\frontend" install
}

if (EnUso 5000) { Write-Host 'La API ya está corriendo en el puerto 5000.' }
else {
  Start-Process powershell -ArgumentList '-NoExit', '-Command',
    "`$host.UI.RawUI.WindowTitle = 'Inventario - API (no cerrar)'; Set-Location '$raiz'; dotnet run --project src\backend\Muebleria.Api --launch-profile http"
}

if (EnUso 5173) { Write-Host 'El frontend ya está corriendo en el puerto 5173.' }
else {
  Start-Process powershell -ArgumentList '-NoExit', '-Command',
    "`$host.UI.RawUI.WindowTitle = 'Inventario - Web (no cerrar)'; Set-Location '$raiz\src\frontend'; npm run dev"
}

Write-Host 'Esperando a que la API y el frontend respondan...'
$api = Esperar 'http://localhost:5000/api/auth/sesion' 180
$web = Esperar 'http://localhost:5173' 90
if (-not ($api -and $web)) {
  Write-Warning 'Alguno no respondió a tiempo. Revisa los mensajes en las ventanas "Inventario - API" e "Inventario - Web".'
  exit 1
}

Start-Process 'http://localhost:5173'
Write-Host ''
Write-Host 'Listo: http://localhost:5173' -ForegroundColor Green
Write-Host 'Usuarios de prueba: admin, encargado y trabajador.'
Write-Host 'Sus contraseñas están en src\backend\Muebleria.Api\appsettings.Development.json (sección Seed).'
