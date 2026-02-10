namespace DeDupe.Models.Configuration
{
    public sealed record NormalizationSettings(double MeanR, double MeanG, double MeanB, double StdR, double StdG, double StdB)
    {
        /// <summary>
        /// ImageNet normalization (ResNet & DINOv2).
        /// </summary>
        public static NormalizationSettings ImageNet { get; } = new(
            MeanR: 0.485,
            MeanG: 0.456,
            MeanB: 0.406,
            StdR: 0.229,
            StdG: 0.224,
            StdB: 0.225);

        /// <summary>
        /// CLIP normalization.
        /// </summary>
        public static NormalizationSettings Clip { get; } = new(
            MeanR: 0.48145466,
            MeanG: 0.4578275,
            MeanB: 0.40821073,
            StdR: 0.26862954,
            StdG: 0.26130258,
            StdB: 0.27577711);

        /// <summary>
        /// Default normalization (ImageNet).
        /// </summary>
        public static NormalizationSettings Default => ImageNet;

        /// <summary>
        /// Convert to float for tensor operations.
        /// </summary>
        public NormalizationSettingsFloat ToFloat() => new((float)MeanR, (float)MeanG, (float)MeanB, (float)StdR, (float)StdG, (float)StdB);
    }

    /// <summary>
    /// Normalization settings Float.
    /// </summary>
    public readonly record struct NormalizationSettingsFloat(float MeanR, float MeanG, float MeanB, float StdR, float StdG, float StdB)
    {
        /// <summary>
        /// Inverse standard deviations.
        /// </summary>
        public float InvStdR => 1.0f / StdR;
        public float InvStdG => 1.0f / StdG;
        public float InvStdB => 1.0f / StdB;
    }
}