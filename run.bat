@echo off

if exist results del /F /Q results\*.*

WeatherStats.exe --bg EmptyMap.jpg --mask Mask2.jpg MeteoDiary results --log log.log -p

rem WeatherStats.exe --bg EmptyMap.jpg --mask Mask2.jpg MeteoDiary results --log log.log --mult 6
