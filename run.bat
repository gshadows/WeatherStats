@echo off

if exist results del /F /Q results\*.*

WeatherStats.exe --bg EmptyMap.jpg --mask Mask.jpg --mult 7 MeteoDiary results --log log.log
