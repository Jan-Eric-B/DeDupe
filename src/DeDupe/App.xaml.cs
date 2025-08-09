using DeDupe.Services;
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
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // MainWindowViewModel
            services.AddSingleton<MainWindowViewModel>();

            // NavigationService
            services.AddSingleton<INavigationService, NavigationService>();

            // Page ViewModels
            services.AddSingleton<FileInputViewModel>();
            services.AddSingleton<ApproachViewModel>();
            services.AddSingleton<PreProcessingViewModel>();
            services.AddSingleton<AnalysisViewModel>();

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

            Window = new MainWindow();
            Window.Activate();
        }

        public async void OnAppClosing()
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}