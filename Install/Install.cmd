@echo off

regedit /s "C:\Users\misatran\Source\Repos\ImageSteganography\Install\SetHide.reg"

COPY hide.cmd C:\Users\misatran\hide.cmd
COPY unhide.cmd C:\Users\misatran\unhide.cmd