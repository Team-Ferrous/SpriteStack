using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ferrous.SpriteStack.Editor
{
    internal static class SpriteStackBuilder
    {
        internal const int MaxAtlasSize = 2048;
        internal const int Gutter = 1;

        internal sealed class View
        {
            internal readonly int Width, Height;
            internal readonly Color32[] Pixels;
            internal View(int width, int height, Color32[] pixels)
            {
                Width = width;
                Height = height;
                Pixels = pixels;
            }

            internal Color32 At(int x, int y) => Pixels[y * Width + x];

            internal View Crop(int x, int y, int w, int h)
            {
                var pixels = new Color32[w * h];
                for (int row = 0; row < h; row++)
                    Array.Copy(Pixels, (y + row) * Width + x, pixels, row * w, w);
                return new View(w, h, pixels);
            }

            internal View Trim(string label, int alpha)
            {
                int left = Width, right = -1, bottom = Height, top = -1;
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (At(x, y).a < alpha) continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y);
                    top = Math.Max(top, y);
                }
                if (right < left) throw new InvalidOperationException(label + " has no pixels above the alpha cutoff.");
                return Crop(left, bottom, right - left + 1, top - bottom + 1);
            }

            internal View Fit(int width, int height)
            {
                if (width == Width && height == Height) return this;
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = At((2 * x + 1) * Width / (2 * width),
                        (2 * y + 1) * Height / (2 * height));
                return new View(width, height, pixels);
            }
        }

        internal sealed class Slice
        {
            internal int HeightIndex;
            internal Color32[] Pixels;
        }

        internal sealed class Stack
        {
            internal int Width, Depth, Height;
            internal readonly List<Slice> Slices = new List<Slice>();
        }

        internal static View ReadPng(Texture2D input)
        {
            if (input == null) throw new InvalidOperationException("Assign every input view.");
            string path = AssetDatabase.GetAssetPath(input);
            if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidOperationException("Use a PNG texture from the Project window: " + input.name + ".");

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 || bytes[12] != 73 || bytes[13] != 72)
                throw new InvalidOperationException("Invalid PNG header: " + input.name + ".");
            if (ReadBigEndian(bytes, 16) > 4096 || ReadBigEndian(bytes, 20) > 4096)
                throw new InvalidOperationException("Input PNGs must be at most 4096 pixels per axis.");

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(decoded, bytes))
                    throw new InvalidOperationException("Could not decode " + input.name + ".");
                return new View(decoded.width, decoded.height, decoded.GetPixels32());
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        private static uint ReadBigEndian(byte[] bytes, int offset)
            => (uint)bytes[offset] << 24 | (uint)bytes[offset + 1] << 16 |
                (uint)bytes[offset + 2] << 8 | bytes[offset + 3];

        internal static Stack Build(View top, View side, View front, View back, int cutoff, bool fit,
            bool topNoseUp, bool sideNoseRight, bool mirrorBack)
        {
            top = top.Trim("Top", cutoff);
            side = side.Trim("Side", cutoff);
            front = front.Trim("Front", cutoff);
            back = back?.Trim("Back", cutoff);

            int width = top.Width, depth = top.Height;
            int height = fit ? Mathf.Max(1, Mathf.RoundToInt(side.Height * (float)depth / side.Width)) : side.Height;
            if (width > 256 || depth > 256 || height > 128)
                throw new InvalidOperationException("The model must fit within 256 × 256 × 128 pixel units.");
            if (!fit && (side.Width != depth || front.Width != width || front.Height != height ||
                (back != null && (back.Width != width || back.Height != height))))
                throw new InvalidOperationException(
                    $"Alignment mismatch after trimming. Expected Side {depth}×{height}, Front/Back {width}×{height}. " +
                    "Align the views or enable Fit mismatched views.");

            side = side.Fit(depth, height);
            front = front.Fit(width, height);
            back = back?.Fit(width, height);
            var stack = new Stack { Width = width, Depth = depth, Height = height };
            var solid = new bool[width * depth * height];
            for (int z = 0; z < height; z++)
            for (int y = 0; y < depth; y++)
            {
                int sideX = sideNoseRight ? depth - 1 - y : y;
                if (side.At(sideX, z).a < cutoff) continue;
                for (int x = 0; x < width; x++)
                {
                    int backX = mirrorBack ? width - 1 - x : x;
                    int topX = topNoseUp ? width - 1 - x : x;
                    int topY = topNoseUp ? depth - 1 - y : y;
                    solid[(z * depth + y) * width + x] = top.At(topX, topY).a >= cutoff &&
                        front.At(x, z).a >= cutoff && (back == null || back.At(backX, z).a >= cutoff);
                }
            }

            bool IsSolid(int x, int y, int z)
                => x >= 0 && x < width && y >= 0 && y < depth && z >= 0 && z < height &&
                    solid[(z * depth + y) * width + x];

            for (int z = 0; z < height; z++)
            {
                var pixels = new Color32[width * depth];
                bool any = false;
                for (int y = 0; y < depth; y++)
                for (int x = 0; x < width; x++)
                {
                    if (!IsSolid(x, y, z)) continue;
                    any = true;
                    var color = top.At(topNoseUp ? width - 1 - x : x, topNoseUp ? depth - 1 - y : y);
                    if (IsSolid(x, y, z + 1))
                    {
                        if (!IsSolid(x, y - 1, z))
                            color = front.At(x, z);
                        else if (back != null && !IsSolid(x, y + 1, z))
                            color = back.At(mirrorBack ? width - 1 - x : x, z);
                        else if (!IsSolid(x - 1, y, z) || !IsSolid(x + 1, y, z))
                            color = side.At(sideNoseRight ? depth - 1 - y : y, z);
                        else
                        {
                            color.r = (byte)(color.r * 4 / 5);
                            color.g = (byte)(color.g * 4 / 5);
                            color.b = (byte)(color.b * 4 / 5);
                        }
                    }
                    color.a = 255;
                    pixels[y * width + x] = color;
                }
                if (any) stack.Slices.Add(new Slice { HeightIndex = z, Pixels = pixels });
            }
            if (stack.Slices.Count == 0)
                throw new InvalidOperationException("No overlapping solid pixels. Check the silhouettes and nose directions.");
            return stack;
        }

        internal static Texture2D Preview(Stack stack, float yaw, float squash, float spacing)
        {
            float radius = Mathf.Sqrt(stack.Width * stack.Width + stack.Depth * stack.Depth) * .5f;
            int width = Mathf.CeilToInt(radius * 2f) + 4;
            int baseY = Mathf.CeilToInt(radius * squash) + 2;
            int height = 2 * baseY + Mathf.CeilToInt((stack.Height - 1) * spacing);
            var pixels = new Color32[width * height];
            float c = Mathf.Cos(yaw * Mathf.Deg2Rad), s = Mathf.Sin(yaw * Mathf.Deg2Rad);
            foreach (var slice in stack.Slices)
            {
                float centerY = baseY + slice.HeightIndex * spacing;
                int first = Mathf.Max(0, Mathf.FloorToInt(centerY - radius * squash));
                int last = Mathf.Min(height - 1, Mathf.CeilToInt(centerY + radius * squash));
                for (int y = first; y <= last; y++)
                for (int x = 0; x < width; x++)
                {
                    float py = (y + .5f - centerY) / squash;
                    float px = x + .5f - width * .5f;
                    int sx = Mathf.FloorToInt(c * px + s * py + stack.Width * .5f);
                    int sy = Mathf.FloorToInt(-s * px + c * py + stack.Depth * .5f);
                    if (sx < 0 || sy < 0 || sx >= stack.Width || sy >= stack.Depth) continue;
                    var color = slice.Pixels[sy * stack.Width + sx];
                    if (color.a != 0) pixels[y * width + x] = color;
                }
            }
            var preview = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            preview.SetPixels32(pixels);
            preview.Apply(false, true);
            return preview;
        }

        internal static int AtlasColumns(Stack stack)
        {
            int tileW = stack.Width + 2 * Gutter, tileH = stack.Depth + 2 * Gutter;
            int maxColumns = MaxAtlasSize / tileW, maxRows = MaxAtlasSize / tileH;
            int minimum = (stack.Slices.Count + maxRows - 1) / maxRows;
            if (minimum > maxColumns) throw new InvalidOperationException("The slices exceed one 2048×2048 atlas. Use smaller inputs or fewer layers.");
            int square = Mathf.CeilToInt(Mathf.Sqrt(stack.Slices.Count * (float)tileH / tileW));
            return Mathf.Clamp(square, minimum, Mathf.Min(maxColumns, stack.Slices.Count));
        }
    }
}
