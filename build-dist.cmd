@echo off
setlocal

cd "%~dp0"

xcopy dist\ bld\dist\ /E

For %%a in (
"XwaOpter\XwaOpter\bin\Release\net48\*.exe"
"XwaOpter\XwaOpter\bin\Release\net48\*.exe.config"
) do (
xcopy /s /d "%%~a" bld\dist\
)
