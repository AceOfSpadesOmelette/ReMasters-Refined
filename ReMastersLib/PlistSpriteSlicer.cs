using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace ReMastersLib
{
    public static class PlistSpriteSlicer
    {
        private static readonly Regex TextureRectRegex = new Regex(
            @"\{\{(-?\d+),(-?\d+)\},\{(-?\d+),(-?\d+)\}\}",
            RegexOptions.Compiled);

        public static void Slice(string plistPath, string texturePngPath)
        {
            var outDir = Path.Combine(
                Path.GetDirectoryName(plistPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(plistPath));
            Directory.CreateDirectory(outDir);

            var doc = XDocument.Load(plistPath);
            var dict = doc.Root?.Element("dict");
            if (dict == null)
                return;

            var framesDict = FindDictByKey(dict, "frames");
            if (framesDict == null)
                return;

            using (var texture = Image.Load(texturePngPath))
            {
                var items = framesDict.Elements().ToList();
                for (int i = 0; i < items.Count - 1; i++)
                {
                    var key = items[i];
                    var value = items[i + 1];
                    if (key.Name != "key" || value.Name != "dict")
                        continue;

                    var frameName = key.Value?.Trim();
                    if (string.IsNullOrEmpty(frameName) || !frameName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var rectString = FindStringByKey(value, "textureRect");
                    if (string.IsNullOrWhiteSpace(rectString))
                        continue;

                    if (!TryParseTextureRect(rectString, out var x, out var y, out var width, out var height))
                        continue;

                    var rotated = FindBoolByKey(value, "textureRotated");
                    var cropWidth = rotated ? height : width;
                    var cropHeight = rotated ? width : height;

                    if (cropWidth <= 0 || cropHeight <= 0)
                        continue;
                    if (x < 0 || y < 0 || x + cropWidth > texture.Width || y + cropHeight > texture.Height)
                        continue;

                    using (var sprite = texture.Clone(ctx => ctx.Crop(new Rectangle(x, y, cropWidth, cropHeight))))
                    {
                        if (rotated)
                            sprite.Mutate(ctx => ctx.Rotate(-90));

                        var outPath = Path.Combine(outDir, Path.GetFileName(frameName));
                        sprite.Save(outPath, new PngEncoder());
                        Console.WriteLine($"Extracting: {outPath}");
                    }
                }
            }
        }

        private static XElement FindDictByKey(XElement dict, string key)
        {
            var items = dict.Elements().ToList();
            for (int i = 0; i < items.Count - 1; i++)
            {
                if (items[i].Name == "key" && string.Equals(items[i].Value, key, StringComparison.Ordinal))
                {
                    if (items[i + 1].Name == "dict")
                        return items[i + 1];
                }
            }
            return null;
        }

        private static string FindStringByKey(XElement dict, string key)
        {
            var items = dict.Elements().ToList();
            for (int i = 0; i < items.Count - 1; i++)
            {
                if (items[i].Name == "key" && string.Equals(items[i].Value, key, StringComparison.Ordinal))
                {
                    if (items[i + 1].Name == "string")
                        return items[i + 1].Value;
                }
            }
            return null;
        }

        private static bool FindBoolByKey(XElement dict, string key)
        {
            var items = dict.Elements().ToList();
            for (int i = 0; i < items.Count - 1; i++)
            {
                if (items[i].Name == "key" && string.Equals(items[i].Value, key, StringComparison.Ordinal))
                {
                    return items[i + 1].Name == "true";
                }
            }
            return false;
        }

        private static bool TryParseTextureRect(string value, out int x, out int y, out int w, out int h)
        {
            x = y = w = h = 0;
            var m = TextureRectRegex.Match(value.Replace(" ", string.Empty));
            if (!m.Success)
                return false;

            return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                   && int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
                   && int.TryParse(m.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out w)
                   && int.TryParse(m.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out h);
        }
    }
}
