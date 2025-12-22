dotnet pack 'src\DbLive\DbLive.csproj' --configuration Release
dotnet pack 'C:\Data\Code\Personal\DbLive\src\DbLive.MSSQL\DbLive.MSSQL.csproj' --configuration Release

$folderPath = "C:\Users\Dev\.nuget\packages\DbLive"

if (Test-Path $folderPath -PathType Container) {
    Remove-Item -Path $folderPath -Recurse -Force
    Write-Host "Folder removed successfully."
} else {
    Write-Host "The folder does not exist at the specified path."
}

$folderPath = "C:\Users\Dev\.nuget\packages\DbLive.mssql"

if (Test-Path $folderPath -PathType Container) {
    Remove-Item -Path $folderPath -Recurse -Force
    Write-Host "Folder removed successfully."
} else {
    Write-Host "The folder does not exist at the specified path."
}

copy src\DbLive\bin\Release\DbLive.0.0.0.nupkg C:\Data\Code\Personal\nugetstest
copy src\DbLive.MSSQL\bin\Release\DbLive.MSSQL.0.0.0.nupkg C:\Data\Code\Personal\nugetstest 