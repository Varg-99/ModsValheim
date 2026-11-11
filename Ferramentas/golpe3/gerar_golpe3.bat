@echo off
chcp 65001 >nul
rem Gera o 3o golpe da Ceifadora feito do zero:
rem   1) Blender (sem janela): BVH -> FBX no projeto Unity
rem   2) Unity (abre e fecha sozinho): FBX -> pacote ceifadora_golpes na pasta do plugin
rem   3) compila o mod
rem Feche o Valheim e o Unity antes.
set BASE=C:\Users\User\Documents\ModsValheim
set G=%BASE%\Ferramentas\golpe3
set UNITY=F:\SteamLibrary\UnityEditors\6000.0.75f1\Editor\Unity.exe
set LOG=%G%\gerar.log
echo ==== %date% %time% ==== > "%LOG%"

set BLENDER=
for /d %%D in ("C:\Program Files\Blender Foundation\Blender*") do if exist "%%D\blender.exe" set "BLENDER=%%D\blender.exe"
if not defined BLENDER for /f "delims=" %%B in ('where blender 2^>nul') do set "BLENDER=%%B"
if not defined BLENDER for /d %%D in ("F:\SteamLibrary\steamapps\common\Blender*" "C:\Program Files (x86)\Steam\steamapps\common\Blender*") do if exist "%%D\blender.exe" set "BLENDER=%%D\blender.exe"
if not defined BLENDER (
  echo [ERRO] Blender nao encontrado. >> "%LOG%"
  echo [ERRO] Blender nao encontrado. Me diga onde ele esta instalado.
  goto fim
)
echo Blender: %BLENDER% >> "%LOG%"
echo [1/3] Blender: criando o FBX...
"%BLENDER%" -b --factory-startup -P "%G%\bvh_para_fbx.py" >> "%LOG%" 2>&1
findstr /c:"RESULTADO: OK" "%G%\blender.log" >nul || (echo [ERRO] Blender falhou, veja blender.log & echo [ERRO] Blender >> "%LOG%" & goto fim)

if not exist "%UNITY%" (echo [ERRO] Unity nao encontrado em %UNITY% & echo [ERRO] Unity nao encontrado >> "%LOG%" & goto fim)
echo [2/3] Unity: a janela do Unity vai abrir e fechar sozinha (pode levar alguns minutos)...
if exist "%BASE%\UnityCeifadora\Logs\golpes_build.log" del "%BASE%\UnityCeifadora\Logs\golpes_build.log"
start "" /wait "%UNITY%" -projectPath "%BASE%\UnityCeifadora" -executeMethod CeifadoraGolpes.Build -quit -logFile "%G%\unity.log"
type "%BASE%\UnityCeifadora\Logs\golpes_build.log" >> "%LOG%"
findstr /c:"RESULTADO: OK" "%BASE%\UnityCeifadora\Logs\golpes_build.log" >nul || (echo [ERRO] Unity falhou, veja unity.log & echo [ERRO] Unity >> "%LOG%" & goto fim)

echo [3/3] Compilando o mod...
call "%BASE%\CeifadoraObsidiana\compilar.bat"
echo RESULTADO GERAL: OK >> "%LOG%"
echo.
echo Pronto! Golpe 3 gerado e mod compilado.
:fim
echo Log: %LOG%
pause
