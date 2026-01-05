@echo off
setlocal enabledelayedexpansion

echo =========================================
echo Building NuGet packages (local override)
echo =========================================

set NUGET_LOCAL=C:\nuget-local
set NUGET_CACHE=%USERPROFILE%\.nuget\packages

REM --- build packages ---
dotnet pack DbLive\DbLive.csproj -c Release || goto :error
dotnet pack DbLive.MSSQL\DbLive.MSSQL.csproj -c Release || goto :error

REM --- copy nupkg to local feed ---
if not exist "%NUGET_LOCAL%" (
    mkdir "%NUGET_LOCAL%"
)

copy /Y DbLive\bin\Release\*.nupkg "%NUGET_LOCAL%"
copy /Y DbLive.MSSQL\bin\Release\*.nupkg "%NUGET_LOCAL%"

REM --- remove cached packages (id-based, version-agnostic) ---
echo Cleaning NuGet cache (targeted)...

if exist "%NUGET_CACHE%\dblive" (
    rmdir /S /Q "%NUGET_CACHE%\dblive"
)

if exist "%NUGET_CACHE%\dblive.mssql" (
    rmdir /S /Q "%NUGET_CACHE%\dblive.mssql"
)

REM --- optional: clean http cache metadata ---
dotnet nuget locals http-cache --clear

echo =========================================
echo Done. Local NuGet packages refreshed.
echo =========================================
exit /b 0

:error
echo.
echo ERROR: build failed
exit /b 1
