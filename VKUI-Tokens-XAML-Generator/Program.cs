using System.Text;
using System.Text.Json;

namespace VKUI_Tokens_XAML_Generator
{
    internal class Program
    {
        const string VKUI_TOKENS_LIGHT_URL = "https://unpkg.com/@vkontakte/vkui-tokens@latest/themes/vkontakteCom/index.json";
        const string VKUI_TOKENS_DARK_URL = "https://unpkg.com/@vkontakte/vkui-tokens@latest/themes/vkontakteComDark/index.json";

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
            var lightJson = await DownloadAndGetJsonAsync(VKUI_TOKENS_LIGHT_URL);
            Dictionary<string, string> colorsForLight = GetColorsFromTokens(lightJson);
            Console.WriteLine($"Parsed {colorsForLight.Count} color(-s).");

            var darkJson = await DownloadAndGetJsonAsync(VKUI_TOKENS_DARK_URL);
            Dictionary<string, string> colorsForDark = GetColorsFromTokens(darkJson);
            Console.WriteLine($"Parsed {colorsForDark.Count} color(-s).");

            if (colorsForLight.Count != colorsForDark.Count) Console.WriteLine("WARNING! Colors count for light and dark themes are not equal!");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
            sb.AppendLine("  <ResourceDictionary.ThemeDictionaries>");
            sb.AppendLine(WrapToXaml("Default", colorsForDark));
            sb.AppendLine("  </ResourceDictionary.ThemeDictionaries>");
            sb.AppendLine("");
            sb.AppendLine("  <ResourceDictionary.ThemeDictionaries>");
            sb.AppendLine(WrapToXaml("Light", colorsForLight));
            sb.AppendLine("  </ResourceDictionary.ThemeDictionaries>");
            sb.AppendLine("</ResourceDictionary>");

            string xamlPath = Path.Combine(Environment.CurrentDirectory, "VKColors.axaml");
            Console.WriteLine($"Saving to file <{xamlPath}>...");

            File.WriteAllText(xamlPath, sb.ToString());
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

            foreach (var element in jsonDoc.RootElement.EnumerateObject())
            {
                if (element.Value.ValueKind == JsonValueKind.Object &&
                    element.Value.TryGetProperty("normal", out var normalJP) &&
                    element.Value.TryGetProperty("hover", out var hoverJP) &&
                    element.Value.TryGetProperty("active", out var activeJP))
                {
                    string normal = ConvertToHexIfNeccessary($"{element.Name}.normal", normalJP.GetString());
                    string hover = ConvertToHexIfNeccessary($"{element.Name}.hover", hoverJP.GetString());
                    string active = ConvertToHexIfNeccessary($"{element.Name}.active", activeJP.GetString());

                    colors.Add(FixColorName(element.Name), normal);
                    colors.Add($"{FixColorName(element.Name)}Hover", hover);
                    colors.Add($"{FixColorName(element.Name)}Active", active);
                }
            }

            jsonDoc.Dispose();
            return colors.OrderBy(d => d.Key).ToDictionary();
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

        private static string FixColorName(string name)
        {
            if (name.StartsWith("vkontakte"))
            {
                return $"VK{name.Substring(2, name.Length - 2)}";
            }

            return $"VK{name[0].ToString().ToUpper()}{name.Substring(1, name.Length - 1)}";
        }

        private static string WrapToXaml(string name, Dictionary<string, string> brushResources)
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
    }
}
