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

            // Dark
            var darkJson = await DownloadAndGetJsonAsync(VKUI_TOKENS_DARK_URL);
            Dictionary<string, string> colorsForDark = GetColorsFromTokens(darkJson);

            lightJson.Dispose();
            darkJson.Dispose();

            await SaveColorsXAMLAsync(colorsForLight, colorsForDark);
            await SaveTokensXAMLAsync("x:Double", "Sizes", sizes);
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
            }

            Console.WriteLine($"Parsed {colors.Count} color token(-s).");
            return colors.OrderBy(d => d.Key).ToDictionary();
        }

        private static Dictionary<string, string> GetSizesFromTokens(JsonDocument jsonDoc)
        {
            Dictionary<string, string> sizes = new Dictionary<string, string>();

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
            }

            Console.WriteLine($"Parsed {sizes.Count} size token(-s).");
            return sizes.OrderBy(d => d.Key).ToDictionary();
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
                sb.Append("\" Color=\"");
                sb.Append(resource.Value);
                sb.Append("\"/>\n");
            }

            return sb.ToString();
        }

        private static async Task SaveTokensXAMLAsync(string resourceType, string fileName, Dictionary<string, string> sizes)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
            sb.AppendLine(WrapToXamlToken(resourceType, sizes));
            sb.AppendLine("</ResourceDictionary>");

            string xamlPath = Path.Combine(Environment.CurrentDirectory, $"{fileName}.axaml");
            Console.WriteLine($"Saving token resources to file <{xamlPath}>...");

            File.WriteAllText(xamlPath, sb.ToString());
        }
    }
}
