using System.Security.AccessControl;
using System.Text;
using System.Text.Json;

namespace VKUI_Tokens_XAML_Generator
{
    internal class Program
    {
        const string VKUI_TOKENS_LIGHT_URL = "https://unpkg.com/@vkontakte/vkui-tokens@latest/themes/vkontakteCom/struct.json";
        const string VKUI_TOKENS_DARK_URL = "https://unpkg.com/@vkontakte/vkui-tokens@latest/themes/vkontakteComDark/struct.json";

        static string workingDirectory;

        static void Main(string[] args)
        {
            Console.WriteLine("VKUI Tokens XAML Generator by ELOR (Elchin Orujov)");

            var temp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            workingDirectory = Path.Combine(temp, "ELOR", "VKUITXG");
            DoAsync().Wait();
        }

        private static void ShowErrorAndQuit(Exception ex)
        {
            Console.WriteLine($"An error occured. HResult: 0x{ex.HResult.ToString("x8")}");
            Console.WriteLine(ex.Message);
            Environment.Exit(ex.HResult);
        }

        private static async Task DoAsync()
        {
            // Light
            var lightJson = await DownloadAndGetJsonAsync(VKUI_TOKENS_LIGHT_URL);

            Dictionary<string, string> colorsForLight = GetColorsFromTokens(lightJson);
            Dictionary<string, string> sizes = GetSizesFromTokens(lightJson);
            Dictionary<string, string> fonts = GetFontsFromTokens(lightJson);

            // Dark
            var darkJson = await DownloadAndGetJsonAsync(VKUI_TOKENS_DARK_URL);
            Dictionary<string, string> colorsForDark = GetColorsFromTokens(darkJson);

            lightJson.Dispose();
            darkJson.Dispose();

            await SaveColorsXAMLAsync(colorsForLight, colorsForDark);
            await SaveTokensXAMLAsync("x:Double", "Sizes", sizes);
            await SaveFontTokensXAMLAsync(fonts);
        }

        private static async Task<JsonDocument> DownloadAndGetJsonAsync(string jsonUrl)
        {
            Console.WriteLine($"Downloading <{jsonUrl}>...");
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetStringAsync(jsonUrl);

                    return JsonDocument.Parse(response);
                }
            }
            catch (Exception ex)
            {
                ShowErrorAndQuit(ex);
            }

            return null;
        }

        private static Dictionary<string, string> GetColorsFromTokens(JsonDocument jsonDoc)
        {
            Dictionary<string, string> colors = new Dictionary<string, string>();

            int i = 0;
            foreach (var element in jsonDoc.RootElement.GetProperty("color").EnumerateObject())
            {
                if (element.Value.ValueKind == JsonValueKind.Object &&
                    element.Value.TryGetProperty("normal", out var normalJP) &&
                    element.Value.TryGetProperty("hover", out var hoverJP) &&
                    element.Value.TryGetProperty("active", out var activeJP))
                {
                    string normal = ConvertToHexIfNeccessary($"{element.Name}.normal", normalJP.GetString());
                    string hover = ConvertToHexIfNeccessary($"{element.Name}.hover", hoverJP.GetString());
                    string active = ConvertToHexIfNeccessary($"{element.Name}.active", activeJP.GetString());

                    colors.Add(FixTokenName(element.Name), normal);
                    colors.Add($"{FixTokenName(element.Name)}Hover", hover);
                    colors.Add($"{FixTokenName(element.Name)}Active", active);
                }
                i++;
            }

            Console.WriteLine($"Parsed {i} color token(-s).");
            return colors.OrderBy(d => d.Key).ToDictionary();
        }

        private static Dictionary<string, string> GetSizesFromTokens(JsonDocument jsonDoc)
        {
            Dictionary<string, string> sizes = new Dictionary<string, string>();

            int i = 0;
            foreach (var element in jsonDoc.RootElement.GetProperty("size").EnumerateObject())
            {
                if (element.Value.ValueKind == JsonValueKind.Object &&
                    element.Value.TryGetProperty("regular", out var regularJP))
                {

                    double regular = regularJP.GetDouble();
                    double compact = regularJP.GetDouble(); // required

                    element.Value.TryGetProperty("compact", out var compactJP);
                    if (compactJP.ValueKind == JsonValueKind.Number) compactJP.TryGetDouble(out compact);

                    sizes.Add($"{FixTokenName(element.Name)}Regular", Convert.ToString(regular));
                    sizes.Add($"{FixTokenName(element.Name)}Compact", Convert.ToString(compact));
                }
                i++;
            }

            Console.WriteLine($"Parsed {i} size token(-s).");
            //return sizes.OrderBy(d => d.Key).ToDictionary();
            return sizes;
        }

        private static Dictionary<string, string> GetFontsFromTokens(JsonDocument jsonDoc)
        {
            Dictionary<string, string> fonts = new Dictionary<string, string>();

            int i = 0;
            foreach (var element in jsonDoc.RootElement.GetProperty("font").EnumerateObject())
            {
                if (element.Value.ValueKind == JsonValueKind.Object &&
                    element.Value.TryGetProperty("regular", out var regularJP))
                {

                    var metric = GetFontMetric(regularJP);
                    (double fontSizeRegular, double lineHeightRegular, string fontWeightRegular) = metric;
                    (double fontSizeCompact, double lineHeightCompact, string fontWeightCompact) = metric;

                    if (element.Value.TryGetProperty("compact", out var compactJP))
                    {
                        (fontSizeCompact, lineHeightCompact, fontWeightCompact) = GetFontMetric(compactJP);
                    }

                    fonts.Add($"{FixFontTokenName(element.Name)}RegularFontSize", Convert.ToString(fontSizeRegular));
                    fonts.Add($"{FixFontTokenName(element.Name)}RegularLineHeight", Convert.ToString(lineHeightRegular));
                    fonts.Add($"{FixFontTokenName(element.Name)}RegularFontWeight", fontWeightRegular);

                    fonts.Add($"{FixFontTokenName(element.Name)}CompactFontSize", Convert.ToString(fontSizeCompact));
                    fonts.Add($"{FixFontTokenName(element.Name)}CompactLineHeight", Convert.ToString(lineHeightCompact));
                    fonts.Add($"{FixFontTokenName(element.Name)}CompactFontWeight", fontWeightCompact);
                }
                i++;
            }

            Console.WriteLine($"Parsed {i} font token(-s).");
            return fonts;
        }

        private static (double fontSize, double lineHeight, string fontWeight) GetFontMetric(JsonElement json)
        {
            double fontSize = 0;
            double lineHeight = 0;
            ushort fontWeightNumber = 0;

            json.GetProperty("fontSize").TryGetDouble(out fontSize);
            json.GetProperty("lineHeight").TryGetDouble(out lineHeight);

            if (json.TryGetProperty("fontWeight", out var fw))
            {
                fw.TryGetUInt16(out fontWeightNumber);
            }

            string fontWeight = fontWeightNumber switch { 
                100 => "Thin",
                200 => "ExtraLight",
                300 => "Light",
                350 => "SemiLight",
                500 => "Medium",
                600 => "SemiBold",
                700 => "Bold",
                900 => "Black",
                950 => "ExtraBlack",
                _ => "Regular"
            };

            return (fontSize, lineHeight, fontWeight);
        }

        private static string ConvertToHexIfNeccessary(string name, string colorString)
        {
            if (colorString.StartsWith("#")) return colorString;

            if (colorString == "transparent") return "#00000000";

            if (colorString.StartsWith("rgba"))
            {
                var components = colorString.Substring(5, colorString.Length - 6);
                var rgbaArr = components.Split(", ");
                byte[] rgba = new byte[4];

                rgba[1] = Convert.ToByte(rgbaArr[0]);
                rgba[2] = Convert.ToByte(rgbaArr[1]);
                rgba[3] = Convert.ToByte(rgbaArr[2]);

                float af = Convert.ToSingle(rgbaArr[3]);
                rgba[0] = (byte)(256f * af);

                StringBuilder sb = new StringBuilder();
                sb.Append("#");
                sb.Append(Convert.ToHexString(rgba));
                return sb.ToString();
            }

            throw new ApplicationException($"Color \"{name}\" is invalid: \"{colorString}\"!");
        }

        private static string FixTokenName(string name)
        {
            return $"VK{name[0].ToString().ToUpper()}{name.Substring(1, name.Length - 1)}";
        }

        private static string FixFontTokenName(string name)
        {
            return $"VK{name[4].ToString().ToUpper()}{name.Substring(5, name.Length - 5)}";
        }

        private static string WrapToXamlColor(string name, Dictionary<string, string> brushResources)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"    <ResourceDictionary x:Key=\"{name}\">");

            foreach (var resource in brushResources)
            {
                sb.Append("       <SolidColorBrush x:Key=\"");
                sb.Append(resource.Key);
                sb.Append("\" Color=\"");
                sb.Append(resource.Value);
                sb.Append("\"/>\n");
            }

            sb.Append("    </ResourceDictionary>");
            return sb.ToString();
        }

        private static async Task SaveColorsXAMLAsync(Dictionary<string, string> lightColors, Dictionary<string, string> darkColors)
        {
            if (lightColors.Count != darkColors.Count) Console.WriteLine("WARNING! Colors count for light and dark themes are not equal!");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
            sb.AppendLine("  <ResourceDictionary.ThemeDictionaries>");
            sb.AppendLine(WrapToXamlColor("Default", darkColors));
            sb.AppendLine("");
            sb.AppendLine(WrapToXamlColor("Light", lightColors));
            sb.AppendLine("  </ResourceDictionary.ThemeDictionaries>");
            sb.AppendLine("</ResourceDictionary>");

            string xamlPath = Path.Combine(Environment.CurrentDirectory, "Colors.axaml");
            Console.WriteLine($"Saving color resources to file <{xamlPath}>...");

            File.WriteAllText(xamlPath, sb.ToString());
        }

        private static string WrapToXamlToken(string resourceType, Dictionary<string, string> brushResources)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var resource in brushResources)
            {
                sb.Append("  <");
                sb.Append(resourceType);
                sb.Append(" x:Key=\"");
                sb.Append(resource.Key);
                sb.Append("\">");
                sb.Append(resource.Value);
                sb.Append("</");
                sb.Append(resourceType);
                sb.Append(">\n");
            }

            return sb.ToString();
        }

        private static async Task SaveTokensXAMLAsync(string resourceType, string fileName, Dictionary<string, string> sizes)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
            sb.Append(WrapToXamlToken(resourceType, sizes));
            sb.AppendLine("</ResourceDictionary>");

            string xamlPath = Path.Combine(Environment.CurrentDirectory, $"{fileName}.axaml");
            Console.WriteLine($"Saving token resources to file <{xamlPath}>...");

            File.WriteAllText(xamlPath, sb.ToString());
        }

        private static string WrapToFontXamlToken(Dictionary<string, string> fonts)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var resource in fonts)
            {
                string resourceType = string.Empty;

                if (resource.Key.EndsWith("FontSize") || resource.Key.EndsWith("LineHeight"))
                {
                    resourceType = "x:Double";
                } else if (resource.Key.EndsWith("FontWeight"))
                {
                    resourceType = "FontWeight";
                }

                sb.Append("  <");
                sb.Append(resourceType);
                sb.Append(" x:Key=\"");
                sb.Append(resource.Key);
                sb.Append("\">");
                sb.Append(resource.Value);
                sb.Append("</");
                sb.Append(resourceType);
                sb.Append(">\n");
            }

            return sb.ToString();
        }

        private static async Task SaveFontTokensXAMLAsync(Dictionary<string, string> fonts)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
            sb.Append(WrapToFontXamlToken(fonts));
            sb.AppendLine("</ResourceDictionary>");

            string xamlPath = Path.Combine(Environment.CurrentDirectory, $"Fonts.axaml");
            Console.WriteLine($"Saving font resources to file <{xamlPath}>...");

            File.WriteAllText(xamlPath, sb.ToString());
        }
    }
}
