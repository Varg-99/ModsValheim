@echo off
chcp 65001 >nul
rem Reduz as foices novas no Blender (sem janela). Pode levar alguns minutos.
set BASE=C:\Users\User\Documents\ModsValheim
set N=%BASE%\Ferramentas\novas
set BLENDER=
for /d %%D in ("C:\Program Files\Blender Foundation\Blender*") do if exist "%%D\blender.exe" set "BLENDER=%%D\blender.exe"
if not defined BLENDER for /f "delims=" %%B in ('where blender 2^>nul') do set "BLENDER=%%B"
if not defined BLENDER (echo [ERRO] Blender nao encontrado. & goto fim)
echo Reduzindo as foices novas no Blender (pode levar alguns minutos)...
"%BLENDER%" -b --factory-startup -P "%N%\reduzir_novas.py" > "%N%\blender_saida.log" 2>&1
findstr /c:"RESULTADO: OK" "%N%\blender.log" >nul && (echo Pronto! Avise o Claude.) || (echo [ERRO] veja %N%\blender.log)
:fim
pause
