namespace DeDupe.Services.Model
{
    /// <summary>
    /// Service for accessing bundled ONNX model.
    /// </summary>
    public interface IBundledModelService
    {
        /// <summary>
        /// Gets path to the bundled model file.
        /// </summary>
        string GetModelPath();

        /// <summary>
        /// Returns true if bundled model file exists.
        /// </summary>
        bool IsModelAvailable();
    }
}