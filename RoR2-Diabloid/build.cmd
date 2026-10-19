@echo off
rem Builds the mod, runs the loot generator self-test and writes everything to logs\
cd /d "%~dp0"
if not exist logs mkdir logs

echo [1/4] Building Diabloid.dll ...
dotnet build src\Diabloid\Diabloid.csproj -c Release > logs\build.log 2>&1
echo       exit code %ERRORLEVEL%  (0 = OK, details in logs\build.log)

echo [2/4] Listing resolved packages ...
dotnet list src\Diabloid\Diabloid.csproj package --include-transitive > logs\packages.log 2>&1

echo [3/4] Loot generator self-test ...
dotnet run --project tools\LootSim\LootSim.csproj -c Release -- --test > logs\selftest.log 2>&1
echo       exit code %ERRORLEVEL%  (0 = OK, details in logs\selftest.log)

echo [4/4] Sample loot (RU + EN) ...
dotnet run --project tools\LootSim\LootSim.csproj -c Release -- 12 10 42 > logs\sample.log 2>&1
dotnet run --project tools\LootSim\LootSim.csproj -c Release -- 8 10 42 --en > logs\sample_en.log 2>&1

echo.
echo Done. DLL: src\Diabloid\bin\Release\netstandard2.1\Diabloid.dll
pause
