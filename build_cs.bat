@echo off

set CSx86=
set CSx64=64

set CSARCH=%CSx64%

rem set CSVER=v2.0.50727
rem set CSVER=v3.5
set CSVER=v4.0.30319

del *.exe

set CSC=%SystemRoot%\Microsoft.NET\Framework%CSARCH%\%CSVER%\csc.exe

echo %CSC%
rem for %%c in (*.cs) do %CSC% /nologo %%c
%CSC% /nologo /recurse:*.cs
