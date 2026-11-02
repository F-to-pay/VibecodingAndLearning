@echo off
rem Builds the mod. Log: logs\build.log
cd /d "%~dp0"
if not exist logs mkdir logs

echo [1/2] Building CharacterTest.dll ...
dotnet build src\CharacterTest\CharacterTest.csproj -c Release > logs\build.log 2>&1
echo       exit code %ERRORLEVEL%  (0 = OK, details in logs\build.log)

echo [2/2] Listing resolved packages ...
dotnet list src\CharacterTest\CharacterTest.csproj package --include-transitive > logs\packages.log 2>&1

echo.
echo Done. DLL: src\CharacterTest\bin\Release\netstandard2.1\CharacterTest.dll
pause
