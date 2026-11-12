@echo off
chcp 65001 >nul
rem Refaz a reducao das 4 foices no Blender, sem frestas. Pode levar alguns minutos.
set BASE=C:\Users\User\Documents\ModsValheim
set N=%BASE%\Ferramentas\novas
set BLENDER=
for /d %%D in ("C:\Program Files\Blender Foundation\Blender*") do if exist "%%D\blender.exe" set "BLENDER=%%D\blender.exe"
if not defined BLENDER for /f "delims=" %%B in ('where blender 2^>nul') do set "BLENDER=%%B"
if not defined BLENDER (echo [ERRO] Blender nao encontrado. & goto fim)
echo Refazendo as 4 foices no Blender (pode levar alguns minutos)...
"%BLENDER%" -b --factory-startup -P "%N%\reduzir_v2.py" > "%N%\v2_saida.log" 2>&1
findstr /c:"RESULTADO: OK" "%N%\v2\blender.log" >nul && (echo Pronto! Avise o Claude.) || (echo [ERRO] veja %N%\v2\blender.log)
:fim
pause
