using DeDupe.Localization;
using DeDupe.Services;
using DeDupe.Services.Analysis;
using DeDupe.Services.Model;
using DeDupe.Services.Processing;
using DeDupe.ViewModels;
using DeDupe.ViewModels.Pages;
using DeDupe.ViewModels.Settings;
using DeDupe.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Serilog;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using static DeDupe.Localization.LocalizationActions;

namespace DeDupe
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;

        public static Window? Window { get; private set; }

        private readonly IServiceProvider _serviceProvider;
        private readonly IHost _host;
        private ILogger<App>? _logger;

        private SettingsWindow? _settingsWindow;

        public App()
        {
            InitializeComponent();

            string logPath = ApplicationData.Current.LocalSettings.Values["LogFolderPath"] as string ?? Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Logs");
            logPath = Path.Combine(logPath, "dedupe-.log");

            LoggerConfiguration? logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .WriteTo.File(logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] {SourceContext}{NewLine} {Message:lj}{NewLine}{Exception}",
                    shared: true);

#if DEBUG
            logConfig = logConfig.WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}");
#endif
            Log.Logger = logConfig.CreateLogger();

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(ConfigureServices)
                .Build();

            _serviceProvider = _host.Services;

            // Configure ImageSharp memory pool and concurrent operations.
            ISettingsService settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            ConfigureImageSharp(settingsService.ParallelProcessingCores);

            // Update ImageSharp if user changes setting at runtime
            settingsService.ParallelProcessingCoresChanged += (_, cores) =>
            {
                Configuration.Default.MaxDegreeOfParallelism = cores;
                Log.Debug("ImageSharp parallelism updated to {Cores}", cores);
            };

            UnhandledException += App_UnhandledException;

#if DEBUG
            DebugSettings.BindingFailed += DebugSettings_BindingFailed;
#endif
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // ViewModels

            // Windows
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<SettingsWindowViewModel>();

            // Pages
            services.AddSingleton<ConfigurationViewModel>();
            services.AddSingleton<ManagementViewModel>();
            services.AddSingleton<GeneralSettingsViewModel>();
            services.AddSingleton<ImageProcessingSettingsViewModel>();
            services.AddSingleton<ModelSettingsViewModel>();
            services.AddSingleton<AnalysisSettingsViewModel>();
            services.AddSingleton<AboutSettingsViewModel>();

            // Services

            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IAppStateService, AppStateService>();

            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IFileOperationService, FileOperationService>();

            // Image Processing
            services.AddTransient<IBorderDetectionService, BorderDetectionService>();
            services.AddTransient<IImageProcessingService, ImageProcessingService>();

            // Model Management
            services.AddSingleton<IBundledModelService, BundledModelService>();

            // Feature Extraction
            services.AddSingleton<IFeatureExtractionService, FeatureExtractionService>();

            // Similarity Analysis
            services.AddSingleton<ISimilarityAnalysisService, SimilarityAnalysisService>();

            // Auto Selection
            services.AddSingleton<IAutoSelectionService, AutoSelectionService>();

            // Localization
            services.AddSingleton<ILocalizer>(sp =>
            {
                ISettingsService settingsService = sp.GetRequiredService<ISettingsService>();
                string stringsFolderPath = Path.Combine(AppContext.BaseDirectory, "Strings");

                return new LocalizerBuilder()
                    .AddStringResourcesFolderForLanguageDictionaries(stringsFolderPath)
                    .SetLogger(sp.GetRequiredService<ILogger<Localizer>>())
                    .SetDefaultLanguage(settingsService.Language)
                    .SetDisableDefaultLocalizationActions(false)
                    .AddLocalizationAction(
                        new LocalizationAction(typeof(Hyperlink), args =>
                        {
                            if (args.DependencyObject is Hyperlink { Inlines.Count: 0 } target)
                            {
                                target.Inlines.Clear();
                                target.Inlines.Add(new Run() { Text = args.Value });
                            }
                        }))
                    .Build();
            });

            // Logging
            services.AddLogging();
        }

        public T GetService<T>() where T : class
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        private static void ConfigureImageSharp(int maxParallelism, int maxPoolSizeMegabytes = 128)
        {
            try
            {
                Configuration.Default.MemoryAllocator = MemoryAllocator.Create(
                    new MemoryAllocatorOptions
                    {
                        MaximumPoolSizeMegabytes = maxPoolSizeMegabytes
                    });

                Configuration.Default.MaxDegreeOfParallelism = maxParallelism;

                Log.Debug("ImageSharp configured with {PoolSizeMB}MB pool and {MaxParallelism} max parallelism", maxPoolSizeMegabytes, Configuration.Default.MaxDegreeOfParallelism);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ImageSharp memory configuration failed, using defaults");
            }
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            await _host.StartAsync();

            _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            LogApplicationStarted();

            // Resolve the localizer to ensure it's built before any UI loads.
            // The DI factory handles all initialization (loading dictionaries, setting language, etc.).
            _ = _serviceProvider.GetRequiredService<ILocalizer>();

            IThemeService themeService = _serviceProvider.GetRequiredService<IThemeService>();
            themeService.Initialize();

            Window = new MainWindow();
            Window.Activate();
        }

        public void OpenSettingsWindow()
        {
            if (_settingsWindow is null)
            {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                LogSettingsWindowOpened();
            }

            _settingsWindow.Activate();
        }

        public async Task Shutdown()
        {
            LogApplicationShuttingDown();

            _settingsWindow?.Close();
            _settingsWindow = null;

            await _host.StopAsync();

            _host.Dispose();

            await Log.CloseAndFlushAsync();

            Environment.Exit(0);
        }

        #region Exception Handling

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogUnhandledException(e.Exception);

            Debug.WriteLine($"Unhandled Exception: {e.Exception.Message}");
            Debug.WriteLine($"Stack Trace: {e.Exception.StackTrace}");

            // Prevent debugger break
            e.Handled = true;
        }

#if DEBUG

        private void DebugSettings_BindingFailed(object sender, BindingFailedEventArgs e)
        {
            LogBindingFailed(e.Message);
            Debug.WriteLine($"Binding Failed: {e.Message}");
        }

#endif

        #endregion Exception Handling

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "Application started")]
        private partial void LogApplicationStarted();

        [LoggerMessage(Level = LogLevel.Information, Message = "Application shutting down")]
        private partial void LogApplicationShuttingDown();

        [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled application exception")]
        private partial void LogUnhandledException(Exception ex);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Settings window opened")]
        private partial void LogSettingsWindowOpened();

#if DEBUG

        [LoggerMessage(Level = LogLevel.Warning, Message = "Data binding failed: {BindingMessage}")]
        private partial void LogBindingFailed(string bindingMessage);

#endif

        #endregion Logging
    }
}