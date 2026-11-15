using AstroValley.Presentation.Extensions;
using AstroValley.Presentation.Services.Interfaces;
using AstroValley.Presentation.ViewModels;
using AstroValley.Presentation.Views;
using AstroValley.Presentation.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;

namespace AstroValley.Presentation;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Debug / Trace output (the [REGRID] lines etc.) also goes to %LocalAppData%\AstroValleyAssistant\logs\app.log, fresh each run.
        // shortcut: Debug.WriteLine is compiled out of Release builds, so a Release run logs only Trace output.
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroValleyAssistant", "logs");
        Directory.CreateDirectory(logDir);
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(new StreamWriter(Path.Combine(logDir, "app.log"), append: false) { AutoFlush = true }));

        var builder = Host.CreateApplicationBuilder(e.Args);
        builder.Services.AddAppServices();

        _host = builder.Build();
        await _host.StartAsync();

        // Apply the brand color before the first window shows, so there is no color flash.
        _host.Services.GetRequiredService<IThemeService>().Initialize();

        // Old shell until ShellWindow takes over: set its first page after all services are ready.
        //_host.Services.GetRequiredService<MainViewModel>().CurrentViewModel = _host.Services.GetRequiredService<HomeViewModel>();
        //_host.Services.GetRequiredService<MainView>().Show();

        var shell = _host.Services.GetRequiredService<ShellWindow>();
        MainWindow = shell;
        shell.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services.GetRequiredService<ISettingsService>().Save();
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
