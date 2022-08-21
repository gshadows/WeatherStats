@echo off

if exist results del /F /Q results\*.*
if not exist results mkdir results

wstat EmptyMap.jpg Mask.jpg MeteoDiary results log.log
