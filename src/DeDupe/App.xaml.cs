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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

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

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
#if DEBUG
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Debug);
#else
            logging.SetMinimumLevel(LogLevel.Warning);
#endif
                })
                .Build();

            _serviceProvider = _host.Services;

            // Configure ImageSharp memory pool and concurrent operations.
            ISettingsService settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            ConfigureImageSharp(settingsService.ParallelProcessingCores);

            // Update ImageSharp if user changes setting at runtime
            settingsService.ParallelProcessingCoresChanged += (_, cores) =>
            {
                Configuration.Default.MaxDegreeOfParallelism = cores;
                Debug.WriteLine($"ImageSharp parallelism updated to {cores}");
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

            // Services

            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IAppStateService, AppStateService>();

            // Image Processing
            services.AddTransient<IBorderDetectionService, BorderDetectionService>();
            services.AddTransient<ImageProcessingService>();

            // Model Management
            services.AddSingleton<IModelDownloadService, ModelDownloadService>();
            services.AddSingleton<IBundledModelService, BundledModelService>();

            // Feature Extraction
            services.AddSingleton<IFeatureExtractionService, FeatureExtractionService>();

            // Similarity Analysis
            services.AddSingleton<ISimilarityAnalysisService, SimilarityAnalysisService>();

            // Auto Selection
            services.AddSingleton<IAutoSelectionService, AutoSelectionService>();

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

                Debug.WriteLine($"ImageSharp configured: {maxPoolSizeMegabytes}MB pool, {Configuration.Default.MaxDegreeOfParallelism} max parallelism");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Warning: Failed to configure ImageSharp memory settings: {ex.Message}");
            }
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            await _host.StartAsync();

            _logger = _serviceProvider.GetRequiredService<ILogger<App>>();

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
            }

            _settingsWindow.Activate();
        }

        public async void Shutdown()
        {
            // Close settings window
            _settingsWindow?.Close();
            _settingsWindow = null;

            // Stop host
            await _host.StopAsync();

            // Dispose
            _host.Dispose();

            // Exit application
            Environment.Exit(0);
        }

        #region Exception Handling

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Exception? exception = e.Exception;
            string? message = $"Unhandled Exception: {exception.Message}";
            string? stackTrace = $"Stack Trace: {exception.StackTrace}";

            _logger?.LogError(exception, "Unhandled application exception occurred");

            // Debug output
            Debug.WriteLine(message);
            Debug.WriteLine(stackTrace);

            // Prevent debugger break
            e.Handled = true;
        }

#if DEBUG

        private void DebugSettings_BindingFailed(object sender, BindingFailedEventArgs e)
        {
            string? message = $"Binding Failed: {e.Message}";
            Debug.WriteLine(message);

            _logger?.LogWarning("Data binding failed: {BindingMessage}", e.Message);
        }

#endif

        #endregion Exception Handling
    }
}