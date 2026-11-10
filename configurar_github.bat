@echo off
rem Rodar UMA vez: instala Git + GitHub CLI, faz login pelo navegador, cria o repositorio
rem "ModsValheim" na sua conta e envia tudo. Depois disso o compilar.bat envia sozinho.
setlocal
cd /d "%~dp0"
set "LOG=%~dp0github.log"
echo ==== %date% %time% ==== > "%LOG%"
set "PATH=%PATH%;C:\Program Files\Git\cmd;C:\Program Files\GitHub CLI"

where git >nul 2>&1
if not errorlevel 1 goto temgit
echo Instalando o Git...
winget install --id Git.Git -e --accept-source-agreements --accept-package-agreements >> "%LOG%" 2>&1
:temgit
where gh >nul 2>&1
if not errorlevel 1 goto temgh
echo Instalando o GitHub CLI...
winget install --id GitHub.cli -e --accept-source-agreements --accept-package-agreements >> "%LOG%" 2>&1
:temgh
where git >> "%LOG%" 2>&1
if errorlevel 1 goto falhou
where gh >> "%LOG%" 2>&1
if errorlevel 1 goto falhou

if not exist ".git" git init -b main >> "%LOG%" 2>&1
git config user.name >nul 2>&1
if errorlevel 1 git config user.name "Fabiano"
git config user.email >nul 2>&1
if errorlevel 1 git config user.email "fabiano@users.noreply.github.com"
git add -A >> "%LOG%" 2>&1
git commit -q -m "Ceifadora Obsidiana" >> "%LOG%" 2>&1

gh auth status >> "%LOG%" 2>&1
if not errorlevel 1 goto logado
echo.
echo Vai abrir o navegador para entrar no GitHub.
echo Quando aparecer um codigo aqui, copie e cole no site.
gh auth login --hostname github.com --git-protocol https --web
gh auth status >> "%LOG%" 2>&1
if errorlevel 1 goto falhou
:logado
gh auth setup-git >> "%LOG%" 2>&1

rem Endereco vazio (tentativa anterior) e removido.
set "URL="
for /f "delims=" %%u in ('git remote get-url origin 2^>nul') do set "URL=%%u"
if defined URL goto enviar
git remote remove origin >nul 2>&1
echo Criando o repositorio ModsValheim na sua conta...
gh repo create ModsValheim --public --source . --remote origin >> "%LOG%" 2>&1
if not errorlevel 1 goto enviar
rem Ja existe: so liga a ele.
for /f "delims=" %%u in ('gh api user -q .login') do set "USR=%%u"
git remote add origin "https://github.com/%USR%/ModsValheim.git" >> "%LOG%" 2>&1

:enviar
git push -u origin main >> "%LOG%" 2>&1
if errorlevel 1 goto falhou
for /f "delims=" %%u in ('git remote get-url origin') do echo Pronto! Tudo guardado em %%u
echo RESULTADO: OK >> "%LOG%"
pause
exit /b 0

:falhou
echo RESULTADO: FALHOU >> "%LOG%"
echo Algo deu errado. O detalhe esta em github.log (o Claude consegue ler).
pause
exit /b 1
