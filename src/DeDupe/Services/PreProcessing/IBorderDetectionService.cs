namespace DeDupe.Services.PreProcessing
{
    public interface IBorderDetectionService
    {
        (byte[] pixels, uint newWidth, uint newHeight) RemoveBorders(
            byte[] pixelData,
            uint width,
            uint height,
            int tolerance = 30);
    }
}