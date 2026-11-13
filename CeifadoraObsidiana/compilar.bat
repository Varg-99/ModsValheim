@echo off
rem Compila a Ceifadora Obsidiana e copia para BepInEx\plugins\CeifadoraObsidiana.
rem Toda a saida vai para build.log, nesta mesma pasta.
cd /d "%~dp0"
echo ==== %date% %time% ==== > build.log

rem --- Versao 2 (simples): guarda a versao do Wukong fora do projeto (so na 1a vez) ---
set "ARQ=%~dp0..\Arquivo\Ceifadora_Wukong_v1.12"
set "PLUG=F:\SteamLibrary\steamapps\common\Valheim\BepInEx\plugins\CeifadoraObsidiana"
set "VELHO=%~dp0..\Animacoes\plugin_wukong"
if not exist "%ARQ%\src" mkdir "%ARQ%\src"
for %%f in (AnimacoesCustom Avanco Bastao Carga EldenheimCompat Empunhadura Fotos TrilhaArma) do (
  if exist "src\%%f.cs" move /y "src\%%f.cs" "%ARQ%\src\" >> build.log
)
if not exist "%VELHO%" mkdir "%VELHO%"
for %%f in (ceifadora_anims golpes.txt) do (
  if exist "%PLUG%\%%f" move /y "%PLUG%\%%f" "%VELHO%\" >> build.log
)
for %%d in (trilhas fotos) do (
  if exist "%PLUG%\%%d" move /y "%PLUG%\%%d" "%VELHO%\%%d" >> build.log
)
set "CFG=F:\SteamLibrary\steamapps\common\Valheim\BepInEx\config\fabiano.ceifadoraobsidiana.cfg"
if exist "%CFG%" if not exist "%VELHO%\config_wukong.cfg" move /y "%CFG%" "%VELHO%\config_wukong.cfg" >> build.log

where dotnet >> build.log 2>&1
if errorlevel 1 (
  echo .NET SDK nao encontrado. Instalando pelo winget... >> build.log
  winget install --id Microsoft.DotNet.SDK.8 -e --accept-source-agreements --accept-package-agreements >> build.log 2>&1
  set "PATH=%PATH%;C:\Program Files\dotnet"
)
dotnet --list-sdks >> build.log 2>&1

dotnet build CeifadoraObsidiana.csproj -c Release >> build.log 2>&1
if errorlevel 1 (
  echo RESULTADO: FALHOU >> build.log
) else (
  echo RESULTADO: OK >> build.log
)
call "%~dp0..\publicar_github.bat" >> build.log 2>&1
echo Pronto. Veja build.log
timeout /t 5 >nul
