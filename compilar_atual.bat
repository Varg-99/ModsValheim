@echo off
rem Roda o compilar.bat do mod indicado em compilar_alvo.txt (o Claude troca esse arquivo).
set /p ALVO=<"%~dp0compilar_alvo.txt"
echo Compilando: %ALVO%
call "%ALVO%"
