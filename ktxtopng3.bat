setx
for /R %%G in (*.ktx) do (PVRTexToolCLI.exe -i "%%G" -f r8g8b8a8 -d "%%G.png")
goto: eof

:sub
@echo %1
PVRTexToolCLI.exe -i "%1" -f r8g8b8a8 -d "%1.png"
goto :eof