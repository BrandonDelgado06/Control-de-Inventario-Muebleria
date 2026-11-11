@echo off
rem Doble clic para abrir el sistema en el navegador (API + frontend).
rem Para detenerlo, cierra las ventanas "Inventario - API" e "Inventario - Web".
title Iniciando Inventario Muebleria
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\iniciar.ps1"
if errorlevel 1 (
  echo.
  echo No se pudo iniciar. Revisa los mensajes de arriba.
  pause
  exit /b 1
)
timeout /t 8 >nul
