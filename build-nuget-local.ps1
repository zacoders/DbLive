dotnet pack 'src\EasyFlow\EasyFlow.csproj' --configuration Release
dotnet pack 'C:\Data\Code\Personal\EasySqlFlow\src\EasyFlow.MSSQL\EasyFlow.MSSQL.csproj' --configuration Release

$folderPath = "C:\Users\Dev\.nuget\packages\easyflow"

if (Test-Path $folderPath -PathType Container) {
    Remove-Item -Path $folderPath -Recurse -Force
    Write-Host "Folder removed successfully."
} else {
    Write-Host "The folder does not exist at the specified path."
}

$folderPath = "C:\Users\Dev\.nuget\packages\easyflow.mssql"

if (Test-Path $folderPath -PathType Container) {
    Remove-Item -Path $folderPath -Recurse -Force
    Write-Host "Folder removed successfully."
} else {
    Write-Host "The folder does not exist at the specified path."
}

copy src\EasyFlow\bin\Release\EasyFlow.0.0.0.nupkg C:\Data\Code\Personal\nugetstest
copy src\EasyFlow.MSSQL\bin\Release\EasyFlow.MSSQL.0.0.0.nupkg C:\Data\Code\Personal\nugetstest 