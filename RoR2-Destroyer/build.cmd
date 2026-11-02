@echo off
rem Builds the mod, runs the workbench self-test, renders icons to logs\icons
cd /d "%~dp0"
if not exist logs mkdir logs

echo [1/4] Building Destroyer.dll ...
dotnet build src\Destroyer\Destroyer.csproj -c Release > logs\build.log 2>&1
echo       exit code %ERRORLEVEL%  (0 = OK, details in logs\build.log)

echo [2/4] Listing resolved packages ...
dotnet list src\Destroyer\Destroyer.csproj package --include-transitive > logs\packages.log 2>&1

echo [3/4] Workbench self-test ...
dotnet run --project tools\Workbench\Workbench.csproj -c Release -- --test > logs\selftest.log 2>&1
echo       exit code %ERRORLEVEL%  (0 = OK, details in logs\selftest.log)

echo [4/4] Rendering item icons ...
dotnet run --project tools\Workbench\Workbench.csproj -c Release -- --icons logs\icons > logs\icons.log 2>&1
echo       exit code %ERRORLEVEL%  (PNG in logs\icons, sheet: logs\icons\_sheet.png)

echo.
echo Done. DLL: src\Destroyer\bin\Release\netstandard2.1\Destroyer.dll
pause
