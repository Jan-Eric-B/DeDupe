using DeDupe.Localization;
using DeDupe.Models;
using DeDupe.Models.Results;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Processing
{
    /// <summary>
    /// Service for processing images before feature extraction.
    /// </summary>
    public interface IImageProcessingService
    {
        bool ClearTempFolder();

        bool InitializeTempFolder();

        Task ProcessItemsAsync(IEnumerable<AnalysisItem> items, ILocalizer localizer, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default);
    }
}