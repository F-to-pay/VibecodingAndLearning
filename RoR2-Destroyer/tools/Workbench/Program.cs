// Верстак Destroyer: то, что можно проверить без игры.
//
//   dotnet run --project tools/Workbench -c Release -- --test
//       самотест: формулы стаков, тексты, холст и фигуры. Код выхода 0 — всё хорошо.
//
//   dotnet run --project tools/Workbench -c Release -- --icons logs/icons [размер]
//       рисует все иконки из IconBook (и заглушку) в PNG, плюс _sheet.png — все иконки на фонах тиров,
//       как они будут выглядеть в игре, и _demo.png — витрина того, что умеет холст.
//
// build.cmd делает оба шага сам.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Destroyer.Art;
using Destroyer.Core;

namespace Workbench
{
    internal static class Program
    {
        /// <summary>Тот же размер, что у иконок в игре (Content/IconFactory.cs).</summary>
        private const int GameIconSize = 256;

        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--test") return SelfTest.Run();
            if (args.Length > 1 && args[0] == "--icons")
            {
                int size = args.Length > 2 ? int.Parse(args[2]) : GameIconSize;
                return RenderIcons(args[1], size);
            }

            Console.WriteLine("Usage: --test | --icons <dir> [size]");
            return 2;
        }

        private static int RenderIcons(string dir, int size)
        {
            Directory.CreateDirectory(dir);

            var icons = new List<(string name, Action<IconCanvas> paint)> { ("_PLACEHOLDER", IconBook.Placeholder) };
            icons.AddRange(IconBook.All.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)));

            var rendered = new List<(string name, IconCanvas canvas)>();
            foreach (var (name, paint) in icons)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var canvas = IconBook.Render(paint, size);
                watch.Stop();
                string path = Path.Combine(dir, name + ".png");
                Png.Write(path, size, size, canvas.ToRgba32(bottomUp: false));
                Console.WriteLine($"{name,-24} {watch.ElapsedMilliseconds,4} ms  coverage {canvas.Coverage():P0}  -> {path}");
                rendered.Add((name, canvas));
            }

            WriteSheet(Path.Combine(dir, "_sheet.png"), rendered);
            WriteBuffs(dir, size);
            WriteDemo(Path.Combine(dir, "_demo.png"));
            Console.WriteLine($"{rendered.Count} icon(s), sheet: {Path.Combine(dir, "_sheet.png")}");
            return 0;
        }

        /// <summary>Фоны ячеек в инвентаре игры — примерно цвета тиров (тёмные, как подложка в UI).</summary>
        private static readonly (string name, Rgba color)[] TierBackgrounds =
        {
            ("white", Rgba.Hex("#4a4d52")),
            ("green", Rgba.Hex("#2f5a2c")),
            ("red", Rgba.Hex("#6b2525")),
            ("yellow", Rgba.Hex("#6b5a1f")),
            ("lunar", Rgba.Hex("#24406b")),
            ("void", Rgba.Hex("#4a2466")),
        };

        /// <summary>Каждая иконка в ряд на всех фонах тиров, в размере 128 (как в инвентаре) и 64 (как в чате/HUD).</summary>
        private static void WriteSheet(string path, List<(string name, IconCanvas canvas)> icons)
        {
            const int big = 128, small = 64, pad = 8;
            int cell = big + pad;
            int width = pad + TierBackgrounds.Length * cell + small + pad;
            int height = pad + icons.Count * cell;
            var sheet = new IconCanvas(Math.Max(width, height));
            sheet.Background(Rgba.Hex("#16181b"));

            for (int row = 0; row < icons.Count; row++)
            {
                var bigIcon = Resample(icons[row].canvas, big);
                var smallIcon = Resample(icons[row].canvas, small);
                for (int col = 0; col < TierBackgrounds.Length; col++)
                {
                    var tile = new IconCanvas(big);
                    tile.Fill(Shapes.Box(0.5f, 0.5f, 0.5f, 0.5f, 0.06f), Paints.Vertical(TierBackgrounds[col].color.Tint(0.12f), TierBackgrounds[col].color.Shade(0.7f)));
                    tile.Paste(bigIcon, 0, 0);
                    sheet.Paste(tile, pad + col * cell, pad + row * cell);
                }
                sheet.Paste(smallIcon, pad + TierBackgrounds.Length * cell, pad + row * cell + (big - small) / 2);
            }

            // Холст квадратный, обрезаем до нужного прямоугольника.
            var full = sheet.ToRgba32(bottomUp: false);
            var cropped = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(full, y * sheet.Size * 4, cropped, y * width * 4, width * 4);
            Png.Write(path, width, height, cropped);
        }

        /// <summary>
        /// Иконки баффов: каждая отдельным PNG (белая, как в моде) и лист _buffs.png — как в игре: мелко,
        /// над полоской здоровья, перекрашенные в цвет баффа (тут — несколько типичных цветов).
        /// </summary>
        private static void WriteBuffs(string dir, int size)
        {
            var buffs = IconBook.Buffs.OrderBy(p => p.Key).ToList();
            if (buffs.Count == 0) return;
            var tints = new[] { Rgba.Hex("#ffffff"), Rgba.Hex("#ff9a3c"), Rgba.Hex("#e8dcc8"), Rgba.Hex("#7fd0ff") };
            const int cell = 64, pad = 8;
            int width = pad + tints.Length * (cell + pad), height = pad + buffs.Count * (cell + pad);
            var sheet = new IconCanvas(Math.Max(width, height));
            sheet.Background(Rgba.Hex("#1e2024"));

            for (int row = 0; row < buffs.Count; row++)
            {
                var canvas = IconBook.Render(buffs[row].Value, size);
                Png.Write(Path.Combine(dir, "buff_" + buffs[row].Key + ".png"), size, size, canvas.ToRgba32(bottomUp: false));
                var small = Resample(canvas, cell);
                for (int col = 0; col < tints.Length; col++)
                {
                    var tinted = new IconCanvas(cell);
                    for (int j = 0; j < cell; j++)
                        for (int i = 0; i < cell; i++)
                        {
                            var px = small.Get(i, j);
                            if (px.A > 0f) tinted.Set(i, j, new Rgba(px.R * tints[col].R, px.G * tints[col].G, px.B * tints[col].B, px.A));
                        }
                    sheet.Paste(tinted, pad + col * (cell + pad), pad + row * (cell + pad));
                }
                Console.WriteLine($"buff {buffs[row].Key}");
            }

            var full = sheet.ToRgba32(bottomUp: false);
            var cropped = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(full, y * sheet.Size * 4, cropped, y * width * 4, width * 4);
            Png.Write(Path.Combine(dir, "_buffs.png"), width, height, cropped);
        }

        /// <summary>Витрина примитивов холста: с чего начать, когда рисуешь свою иконку.</summary>
        private static void WriteDemo(string path)
        {
            const int tile = 160, cols = 4;
            var demos = Demos.All;
            int rows = (demos.Length + cols - 1) / cols;
            var sheet = new IconCanvas(Math.Max(cols, rows) * tile);
            sheet.Background(Rgba.Hex("#2b2e33"));
            for (int k = 0; k < demos.Length; k++)
            {
                var c = IconBook.Render(demos[k], tile);
                sheet.Paste(c, (k % cols) * tile, (k / cols) * tile);
            }
            var full = sheet.ToRgba32(bottomUp: false);
            int w = cols * tile, h = rows * tile;
            var cropped = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(full, y * sheet.Size * 4, cropped, y * w * 4, w * 4);
            Png.Write(path, w, h, cropped);
        }

        /// <summary>Уменьшение усреднением блоков (для листа предпросмотра).</summary>
        private static IconCanvas Resample(IconCanvas src, int size)
        {
            if (src.Size == size) return src;
            var bytes = src.ToRgba32(bottomUp: false);
            var dst = new IconCanvas(size);
            float scale = (float)src.Size / size;
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    int x0 = (int)(i * scale), x1 = Math.Max(x0 + 1, (int)((i + 1) * scale));
                    int y0 = (int)(j * scale), y1 = Math.Max(y0 + 1, (int)((j + 1) * scale));
                    float r = 0, g = 0, b = 0, a = 0; int n = 0;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            int o = (y * src.Size + x) * 4;
                            float al = bytes[o + 3] / 255f;
                            r += bytes[o] / 255f * al; g += bytes[o + 1] / 255f * al; b += bytes[o + 2] / 255f * al; a += al; n++;
                        }
                    if (a <= 0f) continue;
                    dst.Set(i, j, new Rgba(r / a, g / a, b / a, a / n));
                }
            return dst;
        }
    }
}
