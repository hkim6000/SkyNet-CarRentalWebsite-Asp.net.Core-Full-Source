using System.Globalization;
using System.Net;
using System.Text;

namespace CarRental.Core
{
    public static class Ui
    {
        public static string Esc(string? v)
        {
            return WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in v ?? string.Empty)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\'': sb.Append("\\'"); break;
                    case '"': sb.Append("\\u0022"); break;
                    case '<': sb.Append("\\u003c"); break;
                    case '>': sb.Append("\\u003e"); break;
                    case '&': sb.Append("\\u0026"); break;
                    case '\r': break;
                    case '\n': sb.Append("\\n"); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        public static string Clip(string? v, int max)
        {
            string q = (v ?? string.Empty).Trim();
            return q.Length > max ? q.Substring(0, max) : q;
        }

        public static string Count(int n, string one, string many)
        {
            return n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);
        }

        public static string When(DateTime utc)
        {
            if (utc == default)
            {
                return string.Empty;
            }
            DateTime t = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
            return t.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
        }

        public static string Day(DateTime d)
        {
            return d.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        }

        public static bool Has(string? text, string q)
        {
            return q.Length > 0 && (text ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEmail(string v)
        {
            if (v.Length < 5 || v.Length > 80 || v.Contains(' '))
            {
                return false;
            }
            int at = v.IndexOf('@');
            int dot = v.LastIndexOf('.');
            return at > 0 && at == v.LastIndexOf('@') && dot > at + 1 && dot < v.Length - 1;
        }

        public static bool IsPhone(string v)
        {
            int digits = v.Count(char.IsDigit);
            return digits >= 7 && digits <= 15 && v.All(ch => char.IsDigit(ch) || " +()-.".Contains(ch));
        }

        public static string Slug(string v)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in v.ToLowerInvariant().Replace("&", " and "))
            {
                if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    sb.Append(ch);
                }
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                {
                    sb.Append('-');
                }
            }
            string s = sb.ToString().Trim('-');
            return s.Length > 60 ? s.Substring(0, 60).Trim('-') : s;
        }

        public static string Paras(string? text)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string block in (text ?? string.Empty).Replace("\r", string.Empty).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                string t = block.Trim();
                if (t.Length == 0)
                {
                    continue;
                }
                if (t.StartsWith("## ", StringComparison.Ordinal))
                {
                    sb.Append("<h3>" + Esc(t.Substring(3).Trim()) + "</h3>");
                }
                else
                {
                    sb.Append("<p>" + Esc(t).Replace("\n", "<br>") + "</p>");
                }
            }
            return sb.ToString();
        }
    }
}
