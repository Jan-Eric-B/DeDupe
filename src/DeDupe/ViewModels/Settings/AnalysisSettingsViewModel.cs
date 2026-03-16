using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Localization;
using DeDupe.Services;
using Microsoft.Extensions.Logging;
using System;

namespace DeDupe.ViewModels.Settings
{
    public partial class AnalysisSettingsViewModel : SettingsPageViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<AnalysisSettingsViewModel> _logger;

        public AnalysisSettingsViewModel(ISettingsService settingsService, ILocalizer localizer, ILogger<AnalysisSettingsViewModel> logger) : base(localizer)
        {
            _settingsService = settingsService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Title = L("AnalysisSettings_PageTitle");
        }

        private void LoadSettings()
        {
            SimilarityThreshold = _settingsService.SimilarityThreshold;
            AutoAnalyzeSimilarity = _settingsService.AutoAnalyzeSimilarity;

            LogSettingsLoaded(SimilarityThreshold, AutoAnalyzeSimilarity);
        }

        [ObservableProperty]
        public partial double SimilarityThreshold { get; set; }

        [ObservableProperty]
        public partial bool AutoAnalyzeSimilarity { get; set; }

        partial void OnSimilarityThresholdChanged(double value)
        {
            _settingsService.SimilarityThreshold = value;

            LogSimilarityThresholdChanged(value);
        }

        partial void OnAutoAnalyzeSimilarityChanged(bool value)
        {
            _settingsService.AutoAnalyzeSimilarity = value;

            LogAutoAnalyzeSimilarityChanged(value);
        }

        #region Navigation

        public override void OnNavigatedTo()
        {
            base.OnNavigatedTo();

            LoadSettings();
        }

        #endregion Navigation

        #region Logging

        [LoggerMessage(Level = LogLevel.Debug, Message = "General settings loaded: Similarity Threshold={SimilarityThreshold}; Auto Analyze Similarity={AutoAnalyzeSimilarity};")]
        private partial void LogSettingsLoaded(double similarityThreshold, bool autoAnalyzeSimilarity);

        [LoggerMessage(Level = LogLevel.Information, Message = "Similarity Threshold changed to {SimilarityThreshold}")]
        private partial void LogSimilarityThresholdChanged(double similarityThreshold);

        [LoggerMessage(Level = LogLevel.Information, Message = "Auto Analyze Similarity changed to {AutoAnalyzeSimilarity}")]
        private partial void LogAutoAnalyzeSimilarityChanged(bool autoAnalyzeSimilarity);

        #endregion Logging
    }
}