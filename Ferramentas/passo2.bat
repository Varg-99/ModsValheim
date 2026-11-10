@echo off
rem Passo 2: recompila a Ceifadora (com mais diagnostico no log) e instala o
rem Unity Editor 6000.0.75f1, a mesma versao do Valheim 1.0 (~3 GB).
rem Saida em passo2.log nesta pasta.
cd /d "%~dp0"
echo ==== %date% %time% ==== > passo2.log

echo [1/2] Compilando a Ceifadora... >> passo2.log
dotnet build "..\CeifadoraObsidiana\CeifadoraObsidiana.csproj" -c Release -v q >> passo2.log 2>&1
if errorlevel 1 (echo COMPILACAO: FALHOU >> passo2.log) else (echo COMPILACAO: OK >> passo2.log)

echo [2/2] Instalando Unity 6000.0.75f1 pelo Unity Hub... >> passo2.log
set "HUB=C:\Program Files\Unity Hub\Unity Hub.exe"
if not exist "%HUB%" set "HUB=%LOCALAPPDATA%\Programs\Unity Hub\Unity Hub.exe"
"%HUB%" -- --headless install --version 6000.0.75f1 --changeset 26349cd2a5c8 >> passo2.log 2>&1
echo UNITY: comando terminou (codigo %errorlevel%) >> passo2.log
"%HUB%" -- --headless editors -i >> passo2.log 2>&1

echo RESULTADO: FIM >> passo2.log
