namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Extracted features from processed image
    /// </summary>
    public class ExtractedFeatures(ProcessedMedia processedMedia, float[] featureVector, int[] featureDimensions)
    {
        /// <summary>
        /// Get original processed media item
        /// </summary>
        public ProcessedMedia ProcessedMedia { get; } = processedMedia;

        /// <summary>
        /// Get feature vector extracted from model
        /// </summary>
        public float[] FeatureVector { get; } = featureVector;

        /// <summary>
        /// Get dimensions of feature vector
        /// </summary>
        public int[] FeatureDimensions { get; } = featureDimensions;

        /// <summary>
        /// Get path to original image file
        /// </summary>
        public string OriginalFilePath => ProcessedMedia.OriginalItem.FilePath;

        /// <summary>
        /// Get display name for original media
        /// </summary>
        public string DisplayName => ProcessedMedia.OriginalItem.GetDisplayName();

        /// <summary>
        /// Get size of feature vector
        /// </summary>
        public int FeatureCount => FeatureVector.Length;

        /// <summary>
        /// Return string of extracted feature info
        /// </summary>
        public override string ToString()
        {
            return $"{DisplayName} - Features: {FeatureCount}";
        }
    }
}