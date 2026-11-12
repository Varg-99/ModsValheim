@echo off
rem Junta o mod pronto (DLL + modelos + animacoes) num .zip na Area de Trabalho, para mandar para outras pessoas.
rem Rode DEPOIS do compilar.bat ter dado "RESULTADO: OK".
cd /d "%~dp0"
findstr /c:"RESULTADO: OK" build.log >nul
if errorlevel 1 (
  echo A ultima compilacao nao deu OK. Rode o compilar.bat primeiro.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0empacotar.ps1"
pause
