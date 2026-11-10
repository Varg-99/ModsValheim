@echo off
rem Envia as mudancas para o GitHub. Chamado pelo compilar.bat; nunca derruba a compilacao.
setlocal
cd /d "%~dp0"
set "PATH=%PATH%;C:\Program Files\Git\cmd;C:\Program Files\GitHub CLI"
where git >nul 2>&1
if errorlevel 1 goto semrepo
if not exist ".git" goto semrepo
set "URL="
for /f "delims=" %%u in ('git remote get-url origin 2^>nul') do set "URL=%%u"
if not defined URL goto semrepo
git add -A
git diff --cached --quiet
if not errorlevel 1 goto nada
git commit -q -m "Atualizacao %date% %time%"
git push -q origin HEAD
if errorlevel 1 (echo [github] FALHOU o envio) else (echo [github] enviado)
exit /b 0
:semrepo
echo [github] ainda nao configurado - rode configurar_github.bat
exit /b 0
:nada
echo [github] nada novo para enviar
exit /b 0
