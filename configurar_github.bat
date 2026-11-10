@echo off
rem Rodar UMA vez: instala Git + GitHub CLI, faz login pelo navegador, cria o repositorio
rem "ModsValheim" na sua conta e envia tudo. Depois disso o compilar.bat envia sozinho.
setlocal
cd /d "%~dp0"
set "LOG=%~dp0github.log"
echo ==== %date% %time% ==== > "%LOG%"
set "PATH=%PATH%;C:\Program Files\Git\cmd;C:\Program Files\GitHub CLI"

where git >nul 2>&1
if errorlevel 1 winget install --id Git.Git -e --accept-source-agreements --accept-package-agreements >> "%LOG%" 2>&1
where gh >nul 2>&1
if errorlevel 1 winget install --id GitHub.cli -e --accept-source-agreements --accept-package-agreements >> "%LOG%" 2>&1
where git >> "%LOG%" 2>&1 || goto falhou
where gh >> "%LOG%" 2>&1 || goto falhou

echo [1] repositorio local >> "%LOG%"
if not exist ".git" git init -b main >> "%LOG%" 2>&1
git config user.name >nul 2>&1 || git config user.name "Fabiano"
git config user.email >nul 2>&1 || git config user.email "fabiano@users.noreply.github.com"
git add -A >> "%LOG%" 2>&1
git commit -q -m "Ceifadora Obsidiana" >> "%LOG%" 2>&1

echo [2] login >> "%LOG%"
gh auth status >> "%LOG%" 2>&1
if not errorlevel 1 goto logado
echo Vai abrir o navegador para entrar no GitHub. Copie o codigo que aparecer aqui e cole no site.
gh auth login --hostname github.com --git-protocol https --web
gh auth status >> "%LOG%" 2>&1 || goto falhou
:logado
gh auth setup-git >> "%LOG%" 2>&1

echo [3] repositorio no GitHub >> "%LOG%"
set "USR="
for /f "delims=" %%u in ('gh api user --jq .login') do set "USR=%%u"
echo usuario: %USR% >> "%LOG%"
if not defined USR goto falhou
gh repo view "%USR%/ModsValheim" >> "%LOG%" 2>&1
if errorlevel 1 gh repo create "%USR%/ModsValheim" --public --description "Mods de Valheim" >> "%LOG%" 2>&1

echo [4] ligar e enviar >> "%LOG%"
git remote remove origin >> "%LOG%" 2>&1
git config --remove-section remote.origin >nul 2>&1
git remote add origin "https://github.com/%USR%/ModsValheim.git" >> "%LOG%" 2>&1
git remote -v >> "%LOG%" 2>&1
git push -u origin main >> "%LOG%" 2>&1 || goto falhou
echo RESULTADO: OK >> "%LOG%"
echo Pronto! Tudo guardado em https://github.com/%USR%/ModsValheim
pause
exit /b 0

:falhou
echo RESULTADO: FALHOU >> "%LOG%"
echo Algo deu errado. O detalhe esta em github.log (o Claude consegue ler).
pause
exit /b 1
