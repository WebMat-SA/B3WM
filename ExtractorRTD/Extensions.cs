using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractorRTD
{
    public static class Extensions
    {
        public static bool TryParsePrice(
            string value,
            out double result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            // remove espaços
            value = value.Trim();

            // tenta pt-BR primeiro
            if (double.TryParse(
                value,
                NumberStyles.Any,
                new CultureInfo("pt-BR"),
                out result))
            {
                return true;
            }

            // tenta invariant
            if (double.TryParse(
                value,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out result))
            {
                return true;
            }

            // fallback manual
            value = value.Replace(".", ",");

            if (double.TryParse(
                value,
                NumberStyles.Any,
                new CultureInfo("pt-BR"),
                out result))
            {
                return true;
            }

            value = value.Replace(",", ".");

            if (double.TryParse(
                value,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out result))
            {
                return true;
            }

            return false;
        }
    }
}
