@echo off
chcp 65001 >nul
rem Usa uma animacao do Mixamo como 3o golpe da Ceifadora.
rem 1) Coloque o .fbx baixado do Mixamo nesta pasta (Ferramentas\mixamo).
rem 2) Feche o Valheim e o Unity e rode este arquivo.
set BASE=C:\Users\User\Documents\ModsValheim
set M=%BASE%\Ferramentas\mixamo
set DEST=%BASE%\UnityCeifadora\Assets\Ceifada
set UNITY=F:\SteamLibrary\UnityEditors\6000.0.75f1\Editor\Unity.exe
set LOG=%M%\mixamo.log
echo ==== %date% %time% ==== > "%LOG%"
if not exist "%M%\*.fbx" (echo [ERRO] Nenhum .fbx nesta pasta. & echo [ERRO] sem fbx >> "%LOG%" & goto fim)
if not exist "%DEST%" mkdir "%DEST%"
for %%F in ("%M%\*.fbx") do (
  echo Copiando %%~nxF
  copy /y "%%F" "%DEST%\%%~nxF" >nul
  echo fbx: %%~nxF >> "%LOG%"
)
for %%F in ("%M%\*.cortes") do copy /y "%%F" "%DEST%\%%~nxF" >nul
echo [1/2] Unity: a janela vai abrir e fechar sozinha...
if exist "%BASE%\UnityCeifadora\Logs\golpes_build.log" del "%BASE%\UnityCeifadora\Logs\golpes_build.log"
start "" /wait "%UNITY%" -projectPath "%BASE%\UnityCeifadora" -executeMethod CeifadoraGolpes.Build -quit -logFile "%M%\unity.log"
type "%BASE%\UnityCeifadora\Logs\golpes_build.log" >> "%LOG%"
findstr /c:"RESULTADO: OK" "%BASE%\UnityCeifadora\Logs\golpes_build.log" >nul || (echo [ERRO] Unity falhou, veja unity.log & echo [ERRO] Unity >> "%LOG%" & goto fim)
echo [2/2] Compilando o mod...
call "%BASE%\CeifadoraObsidiana\compilar.bat"
echo RESULTADO GERAL: OK >> "%LOG%"
echo.
echo Pronto! Abra o jogo e teste o 3o golpe.
:fim
echo Log: %LOG%
pause
