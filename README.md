DesktopWidget - WPF widget (source)

Contents:
- MainWindow.xaml / MainWindow.xaml.cs : UI and code
- App.xaml / App.xaml.cs
- DesktopWidget.csproj : project file (net7.0-windows, uses WPF)
- widget_settings.json : settings template (place API key here)

Build instructions:
1. Install .NET 7 SDK (or .NET 6 with WPF support).
2. Open the folder 'DesktopWidget' in Visual Studio or run `dotnet restore` then `dotnet build`.
3. To publish a self-contained EXE for Windows x64:
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
4. The built executable will be in bin\Release\net7.0-windows\win-x64\publish

Notes:
- Put your OpenWeatherMap API key into widget_settings.json before running to enable weather.
- The project uses NAudio and Newtonsoft.Json via NuGet.
- For code signing and distribution, obtain a code signing certificate and sign the executable.
