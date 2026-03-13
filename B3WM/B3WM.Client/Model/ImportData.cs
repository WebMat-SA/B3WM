using Microsoft.AspNetCore.Components.Forms;
using System.Globalization;
using System.Text.RegularExpressions;

namespace B3WM.Client.Model
{
    public class ImportData
    {
        public IBrowserFile File { get; set; } = default!;
        public DateTime? Date { get; set; } = DateTime.Today;
        public string? Symbol { get; set; }

        public bool Processing { get; set; } = false;
        public bool Processed { get; set; } = false;

        public bool CanDelete => !Processed || !Processing;
        public long MbSize => File == null ? 0 : File.Size / (long)1024;

        public static DateTime TryExtractDateFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return DateTime.Today;

            // Procura padrões yyyy-MM-dd OU dd-MM-yyyy
            var match = Regex.Match(fileName, @"\d{4}-\d{2}-\d{2}|\d{2}-\d{2}-\d{4}");

            if (!match.Success)
                return DateTime.Today;

            var dateStr = match.Value;

            // Tenta converter nos dois formatos possíveis
            if (DateTime.TryParseExact(
                    dateStr,
                    new[] { "yyyy-MM-dd", "dd-MM-yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                return date;
            }

            return DateTime.Today;
        }
        public static string TryExtractSymbolFromFileName(string fileName, string failMatch)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return failMatch;

            // Padrão de contratos futuros da B3
            var match = Regex.Match(
                fileName.ToUpper(),
                @"[A-Z]{3}[FGHJKMNQUVXZ]\d{2}"
            );

            return match.Success ? match.Value : failMatch;
        }
    }
}
