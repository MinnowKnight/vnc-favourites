@echo off
rem Builds dist\VNCFavourites.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No Visual Studio needed.
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find the .NET Framework 4 C# compiler.
  exit /b 1
)
if not exist dist mkdir dist
> build.rsp echo /nologo /target:winexe /optimize+ /out:dist\VNCFavourites.exe
>> build.rsp echo /win32icon:src\app.ico /win32manifest:src\app.manifest
>> build.rsp echo /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Security.dll /r:System.Xml.dll
for %%f in (src\icons\*.png) do >> build.rsp echo /resource:"%%f",icon.%%~nf.png
>> build.rsp echo src\VNCFavourites.cs
"%CSC%" @build.rsp
set ERR=%ERRORLEVEL%
del build.rsp
if %ERR% neq 0 exit /b %ERR%
echo Built dist\VNCFavourites.exe
