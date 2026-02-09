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

            ConfigureImageSharp();

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

            // Add exception handling
            this.UnhandledException += App_UnhandledException;

#if DEBUG
            // Add binding failure debugging
            this.DebugSettings.BindingFailed += DebugSettings_BindingFailed;
#endif
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Settings & Theme
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IThemeService, ThemeService>();

            // MainWindowViewModel
            services.AddSingleton<MainWindowViewModel>();

            // SettingsWindowViewModel
            services.AddSingleton<SettingsWindowViewModel>();

            // App State
            services.AddSingleton<IAppStateService, AppStateService>();
            services.AddSingleton<IModelDownloadService, ModelDownloadService>();
            services.AddSingleton<IBundledModelService, BundledModelService>();

            // Image Processing
            services.AddTransient<IBorderDetectionService, BorderDetectionService>();
            services.AddTransient<ImageProcessingService>();

            // Page ViewModels
            services.AddSingleton<ConfigurationViewModel>();
            services.AddSingleton<ManagementViewModel>();

            // Settings Page ViewModels
            services.AddSingleton<GeneralSettingsViewModel>();
            services.AddSingleton<ImageProcessingSettingsViewModel>();
            services.AddSingleton<ModelSettingsViewModel>();

            // Feature Extraction
            services.AddSingleton<IFeatureExtractionService, FeatureExtractionService>();
            services.AddSingleton<ISimilarityAnalysisService, SimilarityAnalysisService>();

            services.AddSingleton<IAutoSelectionService, AutoSelectionService>();

            // Logging
            services.AddLogging();
        }

        /// <summary>
        /// Configure ImageSharp memory pool and concurrent operations.
        /// </summary>
        private static void ConfigureImageSharp(int maxPoolSizeMegabytes = 128)
        {
            try
            {
                Configuration.Default.MemoryAllocator = MemoryAllocator.Create(
                    new MemoryAllocatorOptions
                    {
                        MaximumPoolSizeMegabytes = maxPoolSizeMegabytes
                    });

                // TODO Use settings value for parallelism
                // Set maximum concurrent operations (Match ParallelProcessingCores constant)
                Configuration.Default.MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4);

                Debug.WriteLine($"ImageSharp configured: {maxPoolSizeMegabytes}MB pool, " + $"{Configuration.Default.MaxDegreeOfParallelism} max parallelism");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Warning: Failed to configure ImageSharp memory settings: {ex.Message}");
            }
        }

        public T GetService<T>() where T : class
        {
            return _serviceProvider.GetRequiredService<T>();
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

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            await _host.StartAsync();

            _logger = _serviceProvider.GetRequiredService<ILogger<App>>();

            // Initialize theme service
            IThemeService themeService = _serviceProvider.GetRequiredService<IThemeService>();
            themeService.Initialize();

            Window = new MainWindow();
            Window.Activate();
        }

        public void Shutdown()
        {
            // Close settings window
            _settingsWindow?.Close();
            _settingsWindow = null;

            // Stop host and dispose resources
            OnAppClosing();

            // Exit application
            Environment.Exit(0);
        }

        public async void OnAppClosing()
        {
            await _host.StopAsync();
            _host.Dispose();
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