using System;

namespace DeDupe.Services.PreProcessing
{
    public class BorderDetectionService : IBorderDetectionService
    {
        public (byte[] pixels, uint newWidth, uint newHeight) RemoveBorders(byte[] pixelData, uint width, uint height, int tolerance = 30)
        {
            // Too small to process
            if (width < 20 || height < 20)
            {
                return (pixelData, width, height);
            }

            uint stride = width * 4; // RGBA
            uint minBorderSize = Math.Max(2, Math.Min(width, height) / 100);

            // Border detection
            (uint top, uint bottom, uint left, uint right) = DetectBorders(pixelData, width, height, stride, tolerance, minBorderSize);

            if (top == 0 && bottom == 0 && left == 0 && right == 0)
            {
                return (pixelData, width, height);
            }

            // Calculate new dimensions
            uint newWidth = width - left - right;
            uint newHeight = height - top - bottom;

            // Ensure minimum size
            if (newWidth < 10 || newHeight < 10)
            {
                return (pixelData, width, height);
            }

            // Extract cropped region
            return ExtractImageRegion(pixelData, width, left, top, newWidth, newHeight);
        }

        private static (uint top, uint bottom, uint left, uint right) DetectBorders(
            byte[] pixelData,
            uint width,
            uint height,
            uint stride,
            int tolerance,
            uint minBorderSize)
        {
            // Sample edge colors from borders
            (BorderColor top, BorderColor bottom, BorderColor left, BorderColor right) = SampleEdgeColors(pixelData, width, height, stride);

            uint topBorder = ScanFromTop(pixelData, width, height, stride, top, tolerance);
            if (topBorder < minBorderSize)
            {
                topBorder = 0;
            }

            uint bottomBorder = ScanFromBottom(pixelData, width, height, stride, bottom, tolerance);
            if (bottomBorder < minBorderSize)
            {
                bottomBorder = 0;
            }

            uint leftBorder = ScanFromLeft(pixelData, width, height, stride, left, tolerance);
            if (leftBorder < minBorderSize)
            {
                leftBorder = 0;
            }

            uint rightBorder = ScanFromRight(pixelData, width, height, stride, right, tolerance);
            if (rightBorder < minBorderSize)
            {
                rightBorder = 0;
            }

            return (topBorder, bottomBorder, leftBorder, rightBorder);
        }

        private static (BorderColor top, BorderColor bottom, BorderColor left, BorderColor right) SampleEdgeColors(byte[] pixelData, uint width, uint height, uint stride)
        {
            BorderColor topColor = GetAverageEdgeColor(pixelData, width, height, stride, EdgeSide.Top);
            BorderColor bottomColor = GetAverageEdgeColor(pixelData, width, height, stride, EdgeSide.Bottom);
            BorderColor leftColor = GetAverageEdgeColor(pixelData, width, height, stride, EdgeSide.Left);
            BorderColor rightColor = GetAverageEdgeColor(pixelData, width, height, stride, EdgeSide.Right);

            return (topColor, bottomColor, leftColor, rightColor);
        }

        private static BorderColor GetAverageEdgeColor(byte[] pixelData, uint width, uint height, uint stride, EdgeSide side)
        {
            int r = 0, g = 0, b = 0, count = 0;
            uint sampleStep = Math.Max(1, (side == EdgeSide.Top || side == EdgeSide.Bottom ? width : height) / 20);

            switch (side)
            {
                case EdgeSide.Top:
                    for (uint x = 0; x < width; x += sampleStep)
                    {
                        uint index = x * 4;
                        r += pixelData[index];
                        g += pixelData[index + 1];
                        b += pixelData[index + 2];
                        count++;
                    }
                    break;

                case EdgeSide.Bottom:
                    for (uint x = 0; x < width; x += sampleStep)
                    {
                        uint index = (height - 1) * stride + x * 4;
                        r += pixelData[index];
                        g += pixelData[index + 1];
                        b += pixelData[index + 2];
                        count++;
                    }
                    break;

                case EdgeSide.Left:
                    for (uint y = 0; y < height; y += sampleStep)
                    {
                        uint index = y * stride;
                        r += pixelData[index];
                        g += pixelData[index + 1];
                        b += pixelData[index + 2];
                        count++;
                    }
                    break;

                case EdgeSide.Right:
                    for (uint y = 0; y < height; y += sampleStep)
                    {
                        uint index = y * stride + (width - 1) * 4;
                        r += pixelData[index];
                        g += pixelData[index + 1];
                        b += pixelData[index + 2];
                        count++;
                    }
                    break;
            }

            return new BorderColor((byte)(r / count), (byte)(g / count), (byte)(b / count));
        }

        private static uint ScanFromTop(byte[] pixelData, uint width, uint height, uint stride, BorderColor borderColor, int tolerance)
        {
            for (uint row = 0; row < height / 2; row++)
            {
                if (!IsRowUniform(pixelData, width, stride, row, borderColor, tolerance))
                {
                    return row;
                }
            }
            return 0;
        }

        private static uint ScanFromBottom(byte[] pixelData, uint width, uint height, uint stride, BorderColor borderColor, int tolerance)
        {
            for (uint row = 0; row < height / 2; row++)
            {
                uint actualRow = height - 1 - row;
                if (!IsRowUniform(pixelData, width, stride, actualRow, borderColor, tolerance))
                {
                    return row;
                }
            }
            return 0;
        }

        private static uint ScanFromLeft(byte[] pixelData, uint width, uint height, uint stride, BorderColor borderColor, int tolerance)
        {
            for (uint col = 0; col < width / 2; col++)
            {
                if (!IsColumnUniform(pixelData, height, stride, col, borderColor, tolerance))
                {
                    return col;
                }
            }
            return 0;
        }

        private static uint ScanFromRight(byte[] pixelData, uint width, uint height, uint stride, BorderColor borderColor, int tolerance)
        {
            for (uint col = 0; col < width / 2; col++)
            {
                uint actualCol = width - 1 - col;
                if (!IsColumnUniform(pixelData, height, stride, actualCol, borderColor, tolerance))
                {
                    return col;
                }
            }
            return 0;
        }

        private static bool IsRowUniform(byte[] pixelData, uint width, uint stride, uint row, BorderColor borderColor, int tolerance)
        {
            uint rowStart = row * stride;
            uint sampleStep = Math.Max(1, width / 20); // More samples for accuracy
            int matchCount = 0;
            int totalSamples = 0;

            for (uint x = 0; x < width; x += sampleStep)
            {
                uint index = rowStart + x * 4;
                if (IsColorMatch(pixelData, index, borderColor, tolerance))
                {
                    matchCount++;
                }
                totalSamples++;
            }

            // Require 85% of samples to match (allows for noise)
            double matchPercentage = (double)matchCount / totalSamples;
            return matchPercentage >= 0.85;
        }

        private static bool IsColumnUniform(byte[] pixelData, uint height, uint stride, uint col, BorderColor borderColor, int tolerance)
        {
            // Get Samples for accuracy
            uint sampleStep = Math.Max(1, height / 20);
            int matchCount = 0;
            int totalSamples = 0;

            for (uint y = 0; y < height; y += sampleStep)
            {
                uint index = y * stride + col * 4;
                if (IsColorMatch(pixelData, index, borderColor, tolerance))
                {
                    matchCount++;
                }

                totalSamples++;
            }

            // Require 85% of samples to match (allows for noise)
            double matchPercentage = (double)matchCount / totalSamples;
            return matchPercentage >= 0.85;
        }

        private static bool IsColorMatch(byte[] pixelData, uint index, BorderColor targetColor, int tolerance)
        {
            return Math.Abs(pixelData[index] - targetColor.R) <= tolerance &&
                   Math.Abs(pixelData[index + 1] - targetColor.G) <= tolerance &&
                   Math.Abs(pixelData[index + 2] - targetColor.B) <= tolerance;
        }

        private static (byte[] pixels, uint newWidth, uint newHeight) ExtractImageRegion(
            byte[] sourcePixels,
            uint sourceWidth,
            uint startX,
            uint startY,
            uint newWidth,
            uint newHeight)
        {
            byte[] newPixels = new byte[newWidth * newHeight * 4];
            uint sourceStride = sourceWidth * 4;
            uint newStride = newWidth * 4;

            for (uint y = 0; y < newHeight; y++)
            {
                uint sourceRowStart = (y + startY) * sourceStride + startX * 4;
                uint newRowStart = y * newStride;

                Array.Copy(sourcePixels, sourceRowStart, newPixels, newRowStart, newStride);
            }

            return (newPixels, newWidth, newHeight);
        }

        private enum EdgeSide
        { Top, Bottom, Left, Right }

        private readonly struct BorderColor(byte r, byte g, byte b)
        {
            public readonly byte R = r, G = g, B = b;
        }
    }
}