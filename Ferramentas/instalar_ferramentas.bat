@echo off
rem Instala as ferramentas para converter as animacoes do Wukong para o Valheim.
rem Tudo vem de fontes oficiais (winget = instalador oficial do Windows).
rem A saida vai para instalar.log nesta pasta.
cd /d "%~dp0"
echo ==== %date% %time% ==== > instalar.log

echo [1/4] Extraindo FModel... >> instalar.log
powershell -NoProfile -Command "Expand-Archive -Force 'FModel.zip' 'FModel'" >> instalar.log 2>&1
mkdir FModelOutput 2>nul

echo [2/4] .NET Desktop Runtime 8 (necessario para o FModel)... >> instalar.log
winget install --id Microsoft.DotNet.DesktopRuntime.8 -e --accept-source-agreements --accept-package-agreements >> instalar.log 2>&1

echo [3/4] Blender... >> instalar.log
winget install --id BlenderFoundation.Blender -e --accept-source-agreements --accept-package-agreements >> instalar.log 2>&1

echo [4/4] Unity Hub... >> instalar.log
winget install --id Unity.UnityHub -e --accept-source-agreements --accept-package-agreements >> instalar.log 2>&1

echo RESULTADO: FIM >> instalar.log
echo Pronto. Veja instalar.log
timeout /t 5 >nul
