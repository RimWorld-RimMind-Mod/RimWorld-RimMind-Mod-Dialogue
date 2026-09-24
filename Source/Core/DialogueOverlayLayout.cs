using System;
using System.Collections.Generic;

namespace RimMind.Dialogue.Core
{
    public readonly struct OverlayBounds : IEquatable<OverlayBounds>
    {
        public OverlayBounds(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public bool Equals(OverlayBounds other) =>
            X.Equals(other.X) &&
            Y.Equals(other.Y) &&
            Width.Equals(other.Width) &&
            Height.Equals(other.Height);

        public override bool Equals(object? obj) => obj is OverlayBounds other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    }

    public static class DialogueOverlayLayout
    {
        public static OverlayBounds Normalize(
            OverlayBounds value,
            float screenWidth,
            float screenHeight,
            float minWidth,
            float minHeight)
        {
            float safeWidth = Math.Max(1f, screenWidth);
            float safeHeight = Math.Max(1f, screenHeight);
            float width = Clamp(value.Width, Math.Min(minWidth, safeWidth), safeWidth);
            float height = Clamp(value.Height, Math.Min(minHeight, safeHeight), safeHeight);

            return new OverlayBounds(
                Clamp(value.X, 0f, safeWidth - width),
                Clamp(value.Y, 0f, safeHeight - height),
                width,
                height);
        }

        public static int FindFirstVisibleIndex(
            IReadOnlyList<float> lineHeights,
            float availableHeight)
        {
            if (lineHeights == null || lineHeights.Count == 0)
                return 0;

            float remaining = Math.Max(0f, availableHeight);
            int first = lineHeights.Count - 1;
            for (int i = lineHeights.Count - 1; i >= 0; i--)
            {
                float height = Math.Max(0f, lineHeights[i]);
                if (i < lineHeights.Count - 1 && height > remaining)
                    break;

                first = i;
                remaining = Math.Max(0f, remaining - Math.Min(height, remaining));
            }

            return first;
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}
