using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace DeDupe.Services.Processing
{
    public class BorderDetectionService : IBorderDetectionService
    {
        public Rectangle DetectBorders(Image<Rgba32> image, int tolerance = 30)
        {
            int width = image.Width;
            int height = image.Height;

            // Too small to process
            if (width < 20 || height < 20)
            {
                return new Rectangle(0, 0, width, height);
            }

            uint minBorderSize = (uint)Math.Max(2, Math.Min(width, height) / 100);

            // Sample edge colors
            BorderColor topColor = GetAverageEdgeColor(image, EdgeSide.Top);
            BorderColor bottomColor = GetAverageEdgeColor(image, EdgeSide.Bottom);
            BorderColor leftColor = GetAverageEdgeColor(image, EdgeSide.Left);
            BorderColor rightColor = GetAverageEdgeColor(image, EdgeSide.Right);

            // Detect borders
            int topBorder = ScanFromTop(image, topColor, tolerance);
            if (topBorder < minBorderSize)
            {
                topBorder = 0;
            }

            int bottomBorder = ScanFromBottom(image, bottomColor, tolerance);
            if (bottomBorder < minBorderSize)
            {
                bottomBorder = 0;
            }

            int leftBorder = ScanFromLeft(image, leftColor, tolerance);
            if (leftBorder < minBorderSize)
            {
                leftBorder = 0;
            }

            int rightBorder = ScanFromRight(image, rightColor, tolerance);
            if (rightBorder < minBorderSize)
            {
                rightBorder = 0;
            }

            // Calculate new dimensions
            int newWidth = width - leftBorder - rightBorder;
            int newHeight = height - topBorder - bottomBorder;

            // Ensure minimum size
            if (newWidth < 10 || newHeight < 10)
            {
                return new Rectangle(0, 0, width, height);
            }

            return new Rectangle(leftBorder, topBorder, newWidth, newHeight);
        }

        private static BorderColor GetAverageEdgeColor(Image<Rgba32> image, EdgeSide side)
        {
            int r = 0, g = 0, b = 0, count = 0;
            int sampleCount = 20;

            switch (side)
            {
                case EdgeSide.Top:
                    {
                        int step = Math.Max(1, image.Width / sampleCount);
                        for (int x = 0; x < image.Width; x += step)
                        {
                            Rgba32 pixel = image[x, 0];
                            r += pixel.R; g += pixel.G; b += pixel.B;
                            count++;
                        }
                        break;
                    }
                case EdgeSide.Bottom:
                    {
                        int step = Math.Max(1, image.Width / sampleCount);
                        int y = image.Height - 1;
                        for (int x = 0; x < image.Width; x += step)
                        {
                            Rgba32 pixel = image[x, y];
                            r += pixel.R; g += pixel.G; b += pixel.B;
                            count++;
                        }
                        break;
                    }
                case EdgeSide.Left:
                    {
                        int step = Math.Max(1, image.Height / sampleCount);
                        for (int y = 0; y < image.Height; y += step)
                        {
                            Rgba32 pixel = image[0, y];
                            r += pixel.R; g += pixel.G; b += pixel.B;
                            count++;
                        }
                        break;
                    }
                case EdgeSide.Right:
                    {
                        int step = Math.Max(1, image.Height / sampleCount);
                        int x = image.Width - 1;
                        for (int y = 0; y < image.Height; y += step)
                        {
                            Rgba32 pixel = image[x, y];
                            r += pixel.R; g += pixel.G; b += pixel.B;
                            count++;
                        }
                        break;
                    }
            }

            return count > 0
                ? new BorderColor((byte)(r / count), (byte)(g / count), (byte)(b / count))
                : new BorderColor(0, 0, 0);
        }

        private static int ScanFromTop(Image<Rgba32> image, BorderColor borderColor, int tolerance)
        {
            int maxScan = image.Height / 2;
            for (int row = 0; row < maxScan; row++)
            {
                if (!IsRowUniform(image, row, borderColor, tolerance))
                {
                    return row;
                }
            }
            return 0;
        }

        private static int ScanFromBottom(Image<Rgba32> image, BorderColor borderColor, int tolerance)
        {
            int maxScan = image.Height / 2;
            for (int offset = 0; offset < maxScan; offset++)
            {
                int row = image.Height - 1 - offset;
                if (!IsRowUniform(image, row, borderColor, tolerance))
                {
                    return offset;
                }
            }
            return 0;
        }

        private static int ScanFromLeft(Image<Rgba32> image, BorderColor borderColor, int tolerance)
        {
            int maxScan = image.Width / 2;
            for (int col = 0; col < maxScan; col++)
            {
                if (!IsColumnUniform(image, col, borderColor, tolerance))
                {
                    return col;
                }
            }
            return 0;
        }

        private static int ScanFromRight(Image<Rgba32> image, BorderColor borderColor, int tolerance)
        {
            int maxScan = image.Width / 2;
            for (int offset = 0; offset < maxScan; offset++)
            {
                int col = image.Width - 1 - offset;
                if (!IsColumnUniform(image, col, borderColor, tolerance))
                {
                    return offset;
                }
            }
            return 0;
        }

        private static bool IsRowUniform(Image<Rgba32> image, int row, BorderColor borderColor, int tolerance)
        {
            int sampleStep = Math.Max(1, image.Width / 20);
            int matchCount = 0;
            int totalSamples = 0;

            for (int x = 0; x < image.Width; x += sampleStep)
            {
                Rgba32 pixel = image[x, row];
                if (IsColorMatch(pixel, borderColor, tolerance))
                {
                    matchCount++;
                }
                totalSamples++;
            }

            return totalSamples > 0 && (double)matchCount / totalSamples >= 0.85;
        }

        private static bool IsColumnUniform(Image<Rgba32> image, int col, BorderColor borderColor, int tolerance)
        {
            int sampleStep = Math.Max(1, image.Height / 20);
            int matchCount = 0;
            int totalSamples = 0;

            for (int y = 0; y < image.Height; y += sampleStep)
            {
                Rgba32 pixel = image[col, y];
                if (IsColorMatch(pixel, borderColor, tolerance))
                {
                    matchCount++;
                }
                totalSamples++;
            }

            return totalSamples > 0 && (double)matchCount / totalSamples >= 0.85;
        }

        private static bool IsColorMatch(Rgba32 pixel, BorderColor target, int tolerance)
        {
            return Math.Abs(pixel.R - target.R) <= tolerance &&
                   Math.Abs(pixel.G - target.G) <= tolerance &&
                   Math.Abs(pixel.B - target.B) <= tolerance;
        }

        #region Types

        private enum EdgeSide
        { Top, Bottom, Left, Right }

        private readonly struct BorderColor(byte r, byte g, byte b)
        {
            public readonly byte R = r, G = g, B = b;
        }

        #endregion Types
    }
}