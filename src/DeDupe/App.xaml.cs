using DeDupe.Services;
using DeDupe.Services.Analysis;
using DeDupe.Services.PreProcessing;
using DeDupe.ViewModels;
using DeDupe.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using System;

namespace DeDupe
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public static Window? Window { get; private set; }

        private readonly IServiceProvider _serviceProvider;
        private readonly IHost _host;
        private ILogger<App>? _logger;

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

            // Add exception handling
            this.UnhandledException += App_UnhandledException;

#if DEBUG
            // Add binding failure debugging
            this.DebugSettings.BindingFailed += DebugSettings_BindingFailed;
#endif
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // MainWindowViewModel
            services.AddSingleton<MainWindowViewModel>();

            // App State
            services.AddSingleton<IAppStateService, AppStateService>();

            // Navigation
            services.AddSingleton<INavigationService, NavigationService>();

            // Image Processing
            services.AddTransient<IBorderDetectionService, BorderDetectionService>();
            services.AddTransient<IImageFormatService, ImageFormatService>();
            services.AddTransient<IImageResizeService, ImageResizeService>();
            services.AddTransient<ImageProcessingService>();

            // Page ViewModels
            services.AddSingleton<FileInputViewModel>();
            services.AddSingleton<ModelConfigurationViewModel>();
            services.AddSingleton<PreProcessingViewModel>();
            services.AddSingleton<ManagementViewModel>();

            services.AddSingleton<FeatureExtractionService>();

            // Logging
            services.AddLogging();
        }

        public T GetService<T>() where T : class
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            await _host.StartAsync();

            _logger = _serviceProvider.GetRequiredService<ILogger<App>>();

            Window = new MainWindow();
            Window.Activate();
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
            System.Diagnostics.Debug.WriteLine(message);
            System.Diagnostics.Debug.WriteLine(stackTrace);

            // Prevent debugger break
            e.Handled = true;
        }

#if DEBUG

        private void DebugSettings_BindingFailed(object sender, BindingFailedEventArgs e)
        {
            string? message = $"Binding Failed: {e.Message}";
            System.Diagnostics.Debug.WriteLine(message);

            _logger?.LogWarning("Data binding failed: {BindingMessage}", e.Message);
        }

#endif

        #endregion Exception Handling
    }
}