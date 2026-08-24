using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

namespace Bladehold.Tools
{
    /// <summary>
    /// Utility for converting AI-generated monochrome skill/UI sprites into transparent PNGs,
    /// Synty-standard variants (Clean, Stroke, Underlay, Embossed, Sunken), and SVG vector files.
    /// </summary>
    public static class SpriteVariantsProcessor
    {
        public static void ProcessFile(string inputPath, string outputDir, string baseName)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Source image not found: {inputPath}");

            Directory.CreateDirectory(outputDir);

            using var srcBmp = new Bitmap(inputPath);
            int width = srcBmp.Width;
            int height = srcBmp.Height;

            float[,] alpha = ExtractAlpha(srcBmp);

            // 1. Clean PNG (transparent pure white)
            string cleanPath = Path.Combine(outputDir, $"{baseName}_Clean.png");
            using (var bmpClean = RenderClean(alpha, width, height))
            {
                bmpClean.Save(cleanPath, ImageFormat.Png);
            }

            // 2. Stroke PNG (Synty style: dark outer contour ribbon with hollow interior)
            string strokePath = Path.Combine(outputDir, $"{baseName}_Stroke.png");
            using (var bmpStroke = RenderStroke(alpha, width, height, strokeRadius: 14))
            {
                bmpStroke.Save(strokePath, ImageFormat.Png);
            }

            // 3. Underlay PNG (Synty style: white silhouette with soft ambient drop shadow)
            string underlayPath = Path.Combine(outputDir, $"{baseName}_Underlay.png");
            using (var bmpUnderlay = RenderUnderlay(alpha, width, height, offsetX: 0, offsetY: 10, blurRadius: 14f, shadowOpacity: 0.6f))
            {
                bmpUnderlay.Save(underlayPath, ImageFormat.Png);
            }

            // 4. Embossed / Beveled PNG (3D raised look with top-left highlight and bottom-right shadow)
            string embossPath = Path.Combine(outputDir, $"{baseName}_Embossed.png");
            using (var bmpEmboss = RenderEmbossed(alpha, width, height))
            {
                bmpEmboss.Save(embossPath, ImageFormat.Png);
            }

            // 5. Sunken / Inset PNG (Engraved / recessed stone look)
            string sunkenPath = Path.Combine(outputDir, $"{baseName}_Sunken.png");
            using (var bmpSunken = RenderSunken(alpha, width, height))
            {
                bmpSunken.Save(sunkenPath, ImageFormat.Png);
            }

            // 6. SVG Vector Path (.svg)
            string svgPath = Path.Combine(outputDir, $"{baseName}.svg");
            string svgContent = VectorizeToSvg(alpha, width, height, simplifyEpsilon: 1.2f);
            File.WriteAllText(svgPath, svgContent);
        }

        public static float[,] ExtractAlpha(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            float[,] alpha = new float[w, h];

            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            int stride = data.Stride;

            unsafe
            {
                byte* ptr = (byte*)data.Scan0.ToPointer();
                for (int y = 0; y < h; y++)
                {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        byte b = row[x * 3];
                        byte g = row[x * 3 + 1];
                        byte r = row[x * 3 + 2];
                        float maxVal = Math.Max(r, Math.Max(g, b));

                        if (maxVal <= 25) alpha[x, y] = 0f;
                        else if (maxVal >= 220) alpha[x, y] = 1f;
                        else alpha[x, y] = (maxVal - 25f) / 195f;
                    }
                }
            }
            bmp.UnlockBits(data);
            return alpha;
        }

        public static Bitmap RenderClean(float[,] alpha, int w, int h)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;

            unsafe
            {
                byte* ptr = (byte*)data.Scan0.ToPointer();
                for (int y = 0; y < h; y++)
                {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        float a = alpha[x, y];
                        int idx = x * 4;
                        row[idx] = 255;
                        row[idx + 1] = 255;
                        row[idx + 2] = 255;
                        row[idx + 3] = (byte)(Math.Clamp(a, 0f, 1f) * 255f);
                    }
                }
            }
            bmp.UnlockBits(data);
            return bmp;
        }

        public static Bitmap RenderStroke(float[,] alpha, int w, int h, int strokeRadius)
        {
            float[,] dilated = new float[w, h];
            int rSq = strokeRadius * strokeRadius;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (alpha[x, y] >= 0.95f)
                    {
                        dilated[x, y] = 1f;
                        continue;
                    }

                    float maxVal = alpha[x, y];
                    for (int dy = -strokeRadius; dy <= strokeRadius; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;

                        for (int dx = -strokeRadius; dx <= strokeRadius; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w) continue;

                            float dSq = dx * dx + dy * dy;
                            if (dSq <= rSq)
                            {
                                float dist = MathF.Sqrt(dSq);
                                float falloff = Math.Clamp((strokeRadius - dist + 1f) / 2f, 0f, 1f);
                                float val = alpha[nx, ny] * falloff;
                                if (val > maxVal) maxVal = val;
                            }
                        }
                    }
                    dilated[x, y] = maxVal;
                }
            }

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;

            unsafe
            {
                byte* ptr = (byte*)data.Scan0.ToPointer();
                for (int y = 0; y < h; y++)
                {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        float d = dilated[x, y];
                        float fg = alpha[x, y];
                        float strokeA = Math.Clamp(d - fg, 0f, 1f);

                        int idx = x * 4;
                        if (strokeA <= 0.01f)
                        {
                            row[idx] = 0; row[idx + 1] = 0; row[idx + 2] = 0; row[idx + 3] = 0;
                        }
                        else
                        {
                            row[idx] = 59;     // B
                            row[idx + 1] = 59; // G
                            row[idx + 2] = 59; // R
                            row[idx + 3] = (byte)(strokeA * 255f);
                        }
                    }
                }
            }
            bmp.UnlockBits(data);
            return bmp;
        }

        public static Bitmap RenderUnderlay(float[,] alpha, int w, int h, int offsetX, int offsetY, float blurRadius, float shadowOpacity)
        {
            float[,] shadow = new float[w, h];
            for (int y = 0; y < h; y++)
            {
                int sy = y - offsetY;
                for (int x = 0; x < w; x++)
                {
                    int sx = x - offsetX;
                    if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                    {
                        shadow[x, y] = alpha[sx, sy];
                    }
                }
            }

            shadow = GaussianBlur(shadow, w, h, (int)Math.Ceiling(blurRadius));

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;

            unsafe
            {
                byte* ptr = (byte*)data.Scan0.ToPointer();
                for (int y = 0; y < h; y++)
                {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        float fgA = alpha[x, y];
                        float shA = shadow[x, y] * shadowOpacity;

                        int idx = x * 4;
                        if (fgA > 0.01f)
                        {
                            row[idx] = 255; row[idx + 1] = 255; row[idx + 2] = 255;
                            row[idx + 3] = (byte)(Math.Clamp(fgA, 0f, 1f) * 255f);
                        }
                        else if (shA > 0.01f)
                        {
                            row[idx] = 16; row[idx + 1] = 16; row[idx + 2] = 16;
                            row[idx + 3] = (byte)(Math.Clamp(shA, 0f, 1f) * 255f);
                        }
                        else
                        {
                            row[idx] = 0; row[idx + 1] = 0; row[idx + 2] = 0; row[idx + 3] = 0;
                        }
                    }
                }
            }
            bmp.UnlockBits(data);
            return bmp;
        }

        public static Bitmap RenderEmbossed(float[,] alpha, int w, int h)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;

            float[,] smoothAlpha = GaussianBlur(alpha, w, h, 4);

            unsafe
            {
                byte* ptr = (byte*)data.Scan0.ToPointer();
                for (int y = 0; y < h; y++)
                {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        float a = alpha[x, y];
                        int idx = x * 4;
                        if (a <= 0.01f)
                        {
                            row[idx] = 0; row[idx + 1] = 0; row[idx + 2] = 0; row[idx + 3] = 0;
                            continue;
                        }

                        int x0 = Math.Max(0, x - 3);
                        int x1 = Math.Min(w - 1, x + 3);
                        int y0 = Math.Max(0, y - 3);
                        int y1 = Math.Min(h - 1, y + 3);

                        float dx = (smoothAlpha[x1, y] - smoothAlpha[x0, y]) * 0.5f;
                        float dy = (smoothAlpha[x, y1] - smoothAlpha[x, y0]) * 0.5f;

                        float light = (-dx - dy) * 4.0f;
                        float baseGrey = 220f;
                        float shade = Math.Clamp(baseGrey + light * 90f, 50f, 255f);

                        row[idx] = (byte)shade;
                        row[idx + 1] = (byte)shade;
                        row[idx + 2] = (byte)shade;
                        row[idx + 3] = (byte)(Math.Clamp(a, 0f, 1f) * 255f);
                    }
                }
            }
            bmp.UnlockBits(data);
            return bmp;
        }

        public static Bitmap RenderSunken(float[,] alpha, int w, int h)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int stride = data.Stride;

            float[,] smoothAlpha = GaussianBlur(alpha, w, h, 4);

            unsafe
            {
                byte* ptr = (byte*)data.Scan0.ToPointer();
                for (int y = 0; y < h; y++)
                {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        float a = alpha[x, y];
                        int idx = x * 4;
                        if (a <= 0.01f)
                        {
                            row[idx] = 0; row[idx + 1] = 0; row[idx + 2] = 0; row[idx + 3] = 0;
                            continue;
                        }

                        int x0 = Math.Max(0, x - 3);
                        int x1 = Math.Min(w - 1, x + 3);
                        int y0 = Math.Max(0, y - 3);
                        int y1 = Math.Min(h - 1, y + 3);

                        float dx = (smoothAlpha[x1, y] - smoothAlpha[x0, y]) * 0.5f;
                        float dy = (smoothAlpha[x, y1] - smoothAlpha[x, y0]) * 0.5f;

                        float light = (dx + dy) * 4.2f;
                        float baseGrey = 180f;
                        float shade = Math.Clamp(baseGrey + light * 95f, 30f, 255f);

                        row[idx] = (byte)shade;
                        row[idx + 1] = (byte)shade;
                        row[idx + 2] = (byte)shade;
                        row[idx + 3] = (byte)(Math.Clamp(a, 0f, 1f) * 255f);
                    }
                }
            }
            bmp.UnlockBits(data);
            return bmp;
        }

        public static float[,] GaussianBlur(float[,] input, int w, int h, int radius)
        {
            float[,] output = new float[w, h];
            float[] kernel = new float[radius * 2 + 1];
            float sigma = radius / 2.5f;
            float sum = 0;
            for (int i = -radius; i <= radius; i++)
            {
                float val = MathF.Exp(-(i * i) / (2 * sigma * sigma));
                kernel[i + radius] = val;
                sum += val;
            }
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

            float[,] temp = new float[w, h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float val = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sx = Math.Clamp(x + k, 0, w - 1);
                        val += input[sx, y] * kernel[k + radius];
                    }
                    temp[x, y] = val;
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float val = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sy = Math.Clamp(y + k, 0, h - 1);
                        val += temp[x, sy] * kernel[k + radius];
                    }
                    output[x, y] = val;
                }
            }

            return output;
        }

        public static string VectorizeToSvg(float[,] alpha, int w, int h, float simplifyEpsilon)
        {
            var segments = ExtractMarchingSquaresSegments(alpha, w, h, 0.5f);
            var loops = AssembleLoops(segments);

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {w} {h}\" width=\"{w}\" height=\"{h}\">");
            
            foreach (var rawLoop in loops)
            {
                if (rawLoop.Count < 3) continue;
                var simplified = RamerDouglasPeucker(rawLoop, simplifyEpsilon);
                if (simplified.Count < 3) continue;

                sb.Append("  <path d=\"");
                for (int i = 0; i < simplified.Count; i++)
                {
                    var pt = simplified[i];
                    string xStr = pt.X.ToString("0.##", CultureInfo.InvariantCulture);
                    string yStr = pt.Y.ToString("0.##", CultureInfo.InvariantCulture);

                    if (i == 0) sb.Append($"M {xStr} {yStr}");
                    else sb.Append($" L {xStr} {yStr}");
                }
                sb.AppendLine(" Z\" fill=\"#FFFFFF\" />");
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        public struct PointF2D
        {
            public float X, Y;
            public PointF2D(float x, float y) { X = x; Y = y; }
        }

        public struct Segment
        {
            public PointF2D P1, P2;
            public Segment(PointF2D p1, PointF2D p2) { P1 = p1; P2 = p2; }
        }

        private static List<Segment> ExtractMarchingSquaresSegments(float[,] alpha, int w, int h, float threshold)
        {
            var segments = new List<Segment>();

            for (int y = 0; y < h - 1; y++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    float tl = alpha[x, y];
                    float tr = alpha[x + 1, y];
                    float br = alpha[x + 1, y + 1];
                    float bl = alpha[x, y + 1];

                    int cell = 0;
                    if (tl >= threshold) cell |= 8;
                    if (tr >= threshold) cell |= 4;
                    if (br >= threshold) cell |= 2;
                    if (bl >= threshold) cell |= 1;

                    if (cell == 0 || cell == 15) continue;

                    PointF2D top = new PointF2D(x + Interp(tl, tr, threshold), y);
                    PointF2D right = new PointF2D(x + 1, y + Interp(tr, br, threshold));
                    PointF2D bottom = new PointF2D(x + Interp(bl, br, threshold), y + 1);
                    PointF2D left = new PointF2D(x, y + Interp(tl, bl, threshold));

                    switch (cell)
                    {
                        case 1: segments.Add(new Segment(left, bottom)); break;
                        case 2: segments.Add(new Segment(bottom, right)); break;
                        case 3: segments.Add(new Segment(left, right)); break;
                        case 4: segments.Add(new Segment(top, right)); break;
                        case 5: segments.Add(new Segment(left, top)); segments.Add(new Segment(bottom, right)); break;
                        case 6: segments.Add(new Segment(top, bottom)); break;
                        case 7: segments.Add(new Segment(left, top)); break;
                        case 8: segments.Add(new Segment(top, left)); break;
                        case 9: segments.Add(new Segment(top, bottom)); break;
                        case 10: segments.Add(new Segment(top, right)); segments.Add(new Segment(left, bottom)); break;
                        case 11: segments.Add(new Segment(top, right)); break;
                        case 12: segments.Add(new Segment(left, right)); break;
                        case 13: segments.Add(new Segment(bottom, right)); break;
                        case 14: segments.Add(new Segment(left, bottom)); break;
                    }
                }
            }
            return segments;
        }

        private static float Interp(float val1, float val2, float th)
        {
            if (Math.Abs(val2 - val1) < 0.0001f) return 0.5f;
            return Math.Clamp((th - val1) / (val2 - val1), 0f, 1f);
        }

        private static List<List<PointF2D>> AssembleLoops(List<Segment> segments)
        {
            var loops = new List<List<PointF2D>>();
            var unused = new List<Segment>(segments);

            while (unused.Count > 0)
            {
                var currentLoop = new List<PointF2D>();
                var first = unused[0];
                unused.RemoveAt(0);

                currentLoop.Add(first.P1);
                currentLoop.Add(first.P2);
                var currentPt = first.P2;

                bool foundNext = true;
                while (foundNext && unused.Count > 0)
                {
                    foundNext = false;
                    int bestIdx = -1;
                    float bestDistSq = 4.0f;
                    bool flip = false;

                    for (int i = 0; i < unused.Count; i++)
                    {
                        var seg = unused[i];
                        float d1 = DistSq(currentPt, seg.P1);
                        if (d1 < bestDistSq)
                        {
                            bestDistSq = d1;
                            bestIdx = i;
                            flip = false;
                        }
                        float d2 = DistSq(currentPt, seg.P2);
                        if (d2 < bestDistSq)
                        {
                            bestDistSq = d2;
                            bestIdx = i;
                            flip = true;
                        }
                    }

                    if (bestIdx != -1)
                    {
                        var seg = unused[bestIdx];
                        unused.RemoveAt(bestIdx);
                        currentPt = flip ? seg.P1 : seg.P2;
                        currentLoop.Add(currentPt);
                        foundNext = true;

                        if (DistSq(currentPt, currentLoop[0]) < 2.0f) break;
                    }
                }

                if (currentLoop.Count >= 3)
                {
                    loops.Add(currentLoop);
                }
            }

            return loops;
        }

        private static float DistSq(PointF2D a, PointF2D b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private static List<PointF2D> RamerDouglasPeucker(List<PointF2D> points, float epsilon)
        {
            if (points.Count < 3) return new List<PointF2D>(points);

            int index = -1;
            float maxDist = 0;

            var p1 = points[0];
            var p2 = points[points.Count - 1];

            for (int i = 1; i < points.Count - 1; i++)
            {
                float dist = PerpendicularDistance(points[i], p1, p2);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    index = i;
                }
            }

            if (maxDist > epsilon)
            {
                var left = RamerDouglasPeucker(points.GetRange(0, index + 1), epsilon);
                var right = RamerDouglasPeucker(points.GetRange(index, points.Count - index), epsilon);

                var result = new List<PointF2D>(left);
                result.RemoveAt(result.Count - 1);
                result.AddRange(right);
                return result;
            }
            else
            {
                return new List<PointF2D> { points[0], points[points.Count - 1] };
            }
        }

        private static float PerpendicularDistance(PointF2D pt, PointF2D lineStart, PointF2D lineEnd)
        {
            float dx = lineEnd.X - lineStart.X;
            float dy = lineEnd.Y - lineStart.Y;
            float lineLen = MathF.Sqrt(dx * dx + dy * dy);
            if (lineLen < 0.0001f) return MathF.Sqrt(DistSq(pt, lineStart));

            float num = Math.Abs(dy * pt.X - dx * pt.Y + lineEnd.X * lineStart.Y - lineEnd.Y * lineStart.X);
            return num / lineLen;
        }
    }
}
