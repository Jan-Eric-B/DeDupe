using System.Threading.Tasks;

namespace DeDupe.Services
{
    /// <summary>
    /// Service for accessing bundled model files included with the application.
    /// </summary>
    public interface IBundledModelService
    {
        /// <summary>
        /// Gets the file path to the bundled model.
        /// </summary>
        string BundledModelPath { get; }

        /// <summary>
        /// Gets the display name of the bundled model.
        /// </summary>
        string BundledModelName { get; }

        /// <summary>
        /// Gets whether the bundled model exists and is accessible.
        /// </summary>
        bool IsBundledModelAvailable { get; }

        /// <summary>
        /// Validates that the bundled model exists and is accessible.
        /// </summary>
        Task<bool> ValidateBundledModelAsync();
    }
}