@echo off

REM Set the path to your solution folder
set "solutionPath=.\"

REM Recursively delete bin and obj folders within the solution folder
REM Excluding paths containing 'node_modules'
for /d /r "%solutionPath%" %%d in (bin,obj) do (
    if exist "%%d" (
        echo %%~pnxd | find /i "node_modules" > nul
        if errorlevel 1 (
            echo Deleting "%%d"
            rd /s /q "%%d"
        )
    )
)

echo Cleanup complete.
