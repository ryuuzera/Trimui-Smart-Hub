using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using ControlzEx.Theming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Serilog;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Application.ViewModels;
using TrimuiSmartHub.Core.Bios;
using TrimuiSmartHub.Core.Devices;
using TrimuiSmartHub.Core.Downloads;
using TrimuiSmartHub.Core.Games;
using TrimuiSmartHub.Core.Jobs;
using TrimuiSmartHub.Core.Platforms;
using TrimuiSmartHub.Core.Scraping;
using TrimuiSmartHub.Core.Settings;
using TrimuiSmartHub.Core.Sync;
using TrimuiSmartHub.Core.Updates;

namespace TrimuiSmartHub.Application;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new AppPaths();
        Directory.CreateDirectory(paths.Logs);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Polly", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(Path.Combine(paths.Logs, "hub-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        var settings = new SettingsStore(paths.SettingsFile);
        Loc.Initialize(settings.Current.Language);

        // WPF formats bindings (dates, numbers) as en-US unless told otherwise; follow Windows instead.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag)));
        // Dark theme with the app's own blue as accent (checkboxes, progress bars, focus).
        var accent = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D8FBF");
        var theme = RuntimeThemeGenerator.Current.GenerateRuntimeTheme("Dark", accent);
        if (theme != null) ThemeManager.Current.ChangeTheme(this, ThemeManager.Current.AddTheme(theme));
        else ThemeManager.Current.ChangeTheme(this, "Dark.Steel");

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services => ConfigureServices(services, paths, settings))
            .Build();
        await _host.StartAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        await _host.Services.GetRequiredService<MainViewModel>().StartAsync();
    }

    private static void ConfigureServices(IServiceCollection services, AppPaths paths, SettingsStore settings)
    {
        services.AddSingleton(paths);
        services.AddSingleton(settings);
        services.AddSingleton(PlatformRegistry.LoadDefault());
        services.AddSingleton(sp => new GameDatabase(paths.GameDatabaseSource, paths.LocalData, sp.GetRequiredService<ILogger<GameDatabase>>()));
        services.AddSingleton(sp => DeviceDetector.CreateDefault());
        services.AddSingleton<JobQueue>();
        services.AddSingleton<SyncPlanner>();
        services.AddSingleton<SyncExecutor>();
        services.AddSingleton<BiosChecker>();

        services.AddHttpClient("web", c =>
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("TrimuiSmartHub/1.0");
                c.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddStandardResilienceHandler(NoRetryForPost);

        // The download source needs cookies across its GET → POST → GET flow.
        services.AddHttpClient("downloads", c =>
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                c.Timeout = TimeSpan.FromSeconds(60);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer() })
            .AddStandardResilienceHandler(NoRetryForPost);

        services.AddSingleton(sp => new LibretroThumbnails(sp.GetRequiredService<IHttpClientFactory>().CreateClient("web"), paths.Cache));
        services.AddSingleton(sp => new UpdateChecker(sp.GetRequiredService<IHttpClientFactory>().CreateClient("web")));
        services.AddSingleton(sp => new RetrosticClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("downloads")));

        services.AddSingleton<LibraryContext>();
        services.AddSingleton<DeviceMonitor>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<Navigator>();

        services.AddSingleton<JobsViewModel>();
        services.AddSingleton<SyncViewModel>();
        services.AddSingleton<ScrapeViewModel>();
        services.AddSingleton<DeviceViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<DownloadsViewModel>();
        services.AddSingleton<SavesViewModel>();
        services.AddSingleton<BiosViewModel>();
        services.AddSingleton<StorageViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    /// <summary>Retrying a POST could submit a form twice; only idempotent requests are retried.</summary>
    private static void NoRetryForPost(HttpStandardResilienceOptions options)
    {
        var shouldHandle = options.Retry.ShouldHandle;
        options.Retry.ShouldHandle = args => args.Context.GetRequestMessage()?.Method == HttpMethod.Post
            ? ValueTask.FromResult(false)
            : shouldHandle(args);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        MessageBox.Show(Loc.Format("UnexpectedError", e.Exception.Message), "Trimui Smart Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            _host.Services.GetService<DeviceMonitor>()?.Dispose();
            var queue = _host.Services.GetService<JobQueue>();
            if (queue != null) await queue.DisposeAsync();
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
