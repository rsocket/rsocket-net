
rd /s /q RSocket
cd ../RSocket.Core
dotnet clean --configuration release
dotnet restore --source https://api.nuget.org/v3/index.json
dotnet publish -c release -o ../RSocket.Unity/RSocket
cd ../RSocket.Unity/RSocket
del System.Runtime.InteropServices.WindowsRuntime.dll
pause