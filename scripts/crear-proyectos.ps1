# Genera la solución .NET y el proyecto React (Sprint 0).
# Requisitos: SDK de .NET 10 y Node.js 22+.
# Uso (desde la raíz del repositorio):  powershell -ExecutionPolicy Bypass -File scripts\crear-proyectos.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# --- Verificaciones -----------------------------------------------------------
$sdk = (dotnet --version)
if (-not $sdk.StartsWith('10.')) { throw "Se requiere el SDK de .NET 10 (versión actual: $sdk)." }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Se requiere Node.js 22 o superior.' }
if (Test-Path 'src/backend/Muebleria.Inventario.slnx') { throw 'La solución ya existe; no se vuelve a generar.' }

# --- Backend ------------------------------------------------------------------
$b = 'src/backend'
dotnet new sln     -n Muebleria.Inventario -o $b
dotnet new classlib -n Muebleria.Domain          -o "$b/Muebleria.Domain"          --force
dotnet new classlib -n Muebleria.Application     -o "$b/Muebleria.Application"     --force
dotnet new classlib -n Muebleria.Infrastructure  -o "$b/Muebleria.Infrastructure"  --force
dotnet new webapi   -n Muebleria.Api             -o "$b/Muebleria.Api" --use-controllers --force
dotnet new xunit    -n Muebleria.UnitTests        -o 'tests/Muebleria.UnitTests'        --force
dotnet new xunit    -n Muebleria.IntegrationTests -o 'tests/Muebleria.IntegrationTests' --force
Get-ChildItem $b, 'tests' -Recurse -Filter Class1.cs | Remove-Item

$sln = "$b/Muebleria.Inventario.slnx"
dotnet sln $sln add (Get-ChildItem $b, 'tests' -Recurse -Filter *.csproj).FullName

# Referencias entre capas
dotnet add "$b/Muebleria.Application"    reference "$b/Muebleria.Domain"
dotnet add "$b/Muebleria.Infrastructure" reference "$b/Muebleria.Application"
dotnet add "$b/Muebleria.Api"            reference "$b/Muebleria.Application" "$b/Muebleria.Infrastructure"
dotnet add 'tests/Muebleria.UnitTests'        reference "$b/Muebleria.Application"
dotnet add 'tests/Muebleria.IntegrationTests' reference "$b/Muebleria.Api"

# Paquetes
dotnet add "$b/Muebleria.Application" package FluentValidation.DependencyInjectionExtensions
dotnet add "$b/Muebleria.Infrastructure" package Microsoft.EntityFrameworkCore.SqlServer
dotnet add "$b/Muebleria.Infrastructure" package Microsoft.EntityFrameworkCore.Design
dotnet add "$b/Muebleria.Infrastructure" package QuestPDF
dotnet add "$b/Muebleria.Infrastructure" package ClosedXML
dotnet add "$b/Muebleria.Api" package Microsoft.EntityFrameworkCore.Design
dotnet add "$b/Muebleria.Api" package Scalar.AspNetCore
dotnet add "$b/Muebleria.Api" package Microsoft.Extensions.Hosting.WindowsServices
dotnet add 'tests/Muebleria.UnitTests'        package FluentAssertions
dotnet add 'tests/Muebleria.IntegrationTests' package FluentAssertions
dotnet add 'tests/Muebleria.IntegrationTests' package Microsoft.AspNetCore.Mvc.Testing

dotnet tool install --global dotnet-ef 2>$null

# --- Frontend -----------------------------------------------------------------
# Vite no acepta carpetas con archivos, así que se genera aparte y se copia.
# create-vite no acepta rutas absolutas de Windows: se ejecuta dentro de TEMP con un nombre relativo.
$tmp = Join-Path $env:TEMP 'muebleria-vite'
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
Push-Location $env:TEMP
npm create vite@latest muebleria-vite -- --template react-ts --no-interactive
Pop-Location
robocopy $tmp 'src/frontend' /E /NFL /NDL /NJH /NJS | Out-Null
Remove-Item $tmp -Recurse -Force
(Get-Content 'src/frontend/package.json' -Raw) -replace '"name": "[^"]*"', '"name": "muebleria-frontend"' |
  Set-Content 'src/frontend/package.json' -Encoding utf8 -NoNewline

Push-Location 'src/frontend'
npm install
npm install react-router @tanstack/react-query axios react-hook-form zod @hookform/resolvers `
  @mui/material @mui/icons-material @emotion/react @emotion/styled @mui/x-data-grid @mui/x-charts `
  @fontsource/roboto dayjs
npm install -D vitest @testing-library/react @testing-library/jest-dom jsdom prettier
Pop-Location

# Quitar los .gitkeep de carpetas que ya tienen archivos
Get-ChildItem -Recurse -Force -Filter .gitkeep | Where-Object {
  (Get-ChildItem $_.DirectoryName -Force | Measure-Object).Count -gt 1
} | Remove-Item

Write-Host "`nListo. Siguiente paso: dotnet build $sln  y  cd src/frontend; npm run dev" -ForegroundColor Green
