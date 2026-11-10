@echo off
rem Rodar UMA vez: instala o Git (se faltar), cria o repositorio local e liga ao GitHub.
setlocal
cd /d "%~dp0"
set "PATH=%PATH%;C:\Program Files\Git\cmd"
where git >nul 2>&1
if not errorlevel 1 goto temgit
echo Instalando o Git pelo winget...
winget install --id Git.Git -e --accept-source-agreements --accept-package-agreements
:temgit
where git >nul 2>&1
if errorlevel 1 (
  echo Nao consegui instalar o Git. Instale em https://git-scm.com e rode de novo.
  pause
  exit /b 1
)
if not exist ".git" git init -b main
git config user.name >nul 2>&1
if errorlevel 1 git config user.name "Fabiano"
git config user.email >nul 2>&1
if errorlevel 1 git config user.email "fabiano@users.noreply.github.com"
git add -A
git commit -q -m "Ceifadora Obsidiana v2 (golpes do Valheim) + versao Wukong guardada em Arquivo"
git remote get-url origin >nul 2>&1
if not errorlevel 1 goto enviar
echo.
echo Crie um repositorio VAZIO em https://github.com/new  (sem README, sem .gitignore)
set /p URL=Cole aqui o endereco dele (https://github.com/.../....git) e aperte Enter: 
git remote add origin "%URL%"
:enviar
echo Enviando... (se abrir o navegador, faca login no GitHub)
git push -u origin main
if errorlevel 1 (echo FALHOU o envio.) else (echo Pronto! Tudo guardado no GitHub.)
pause
