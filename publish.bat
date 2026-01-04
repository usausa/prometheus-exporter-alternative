rmdir /s /q Publish
dotnet publish PrometheusExporter\PrometheusExporter.csproj -o Publish\Windows -c Release -f net10.0-windows10.0.26100.0    -r win-x64     /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained
dotnet publish PrometheusExporter\PrometheusExporter.csproj -o Publish\Linux   -c Release -f net10.0 /p:BuildPlatform=linux -r linux-x64   /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained
dotnet publish PrometheusExporter\PrometheusExporter.csproj -o Publish\RasPi   -c Release -f net10.0 /p:BuildPlatform=linux -r linux-arm64 /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained
dotnet publish PrometheusExporter\PrometheusExporter.csproj -o Publish\Mac     -c Release -f net10.0 /p:BuildPlatform=mac   -r osx-arm64   /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained

del Publish\Windows\*.json
del Publish\Linux\*.json
del Publish\RasPi\*.json
del Publish\Mac\*.json
copy PrometheusExporter\appsettings.json Publish\Windows\appsettings.json
copy PrometheusExporter\appsettings.Platform.Linux.json Publish\Linux\appsettings.json
copy PrometheusExporter\appsettings.Platform.Linux.json Publish\RasPi\appsettings.json
copy PrometheusExporter\appsettings.Platform.Mac.json Publish\Mac\appsettings.json
