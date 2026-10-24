using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminActivity : WebPage
    {
        private const int Size = 25;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Activity log | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private const string AreaKey = "activity";

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminActivityJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminActivityJs.denied();");
                return null;
            }
            return c;
        }

        private static bool Can(Ctx c, char letter)
        {
            return Access.Can(c.U, AreaKey, letter);
        }

        private static async Task<ApiResponse> Deny(ApiResponse response, Ctx c, string action, string message)
        {
            await Store.Log(c.U.Id, action, "no right", false);
            response.ExecuteScript("AdminActivityJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("lo-brand", Ui.Esc(site.Brand));
            response.SetElementContents("lo-menu", await Auth.Menu("lo", "AdminActivity", c));
            response.SetElementContents("lo-who", Auth.Who("lo", c));
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Shell(response, c, site);
            List<ActivityRec> rows = await Store.Rows<ActivityRec>("activity");
            response.SetElementContents("lo-main", "<div class=\"lo-ph\"><div><h1>Activity log</h1><p>Every change made in the staff area, and every request that was refused. The newest 5,000 entries are kept.</p></div></div><div class=\"lo-tools\"><input type=\"search\" id=\"lo-q\" placeholder=\"Search action or name\" maxlength=\"40\" oninput=\"AdminActivityJs.type()\" aria-label=\"Search the log\"><select id=\"lo-fr\" aria-label=\"Result\" onchange=\"AdminActivityJs.list(true)\"><option value=\"\">Done and refused</option><option value=\"ok\">Done</option><option value=\"no\">Refused</option></select></div><div id=\"lo-list\">" + await ListHtml(rows, string.Empty, string.Empty, 1) + "</div>");
            return response;
        }

        private static int PageNo(string? v)
        {
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        private static async Task<string> ListHtml(List<ActivityRec> all, string term, string result, int page)
        {
            Dictionary<string, string> names = new Dictionary<string, string>();
            foreach (string id in all.Select(a => a.UserId).Distinct())
            {
                names[id] = await Store.Who(id);
            }
            List<ActivityRec> rows = all.Where(a => (result == string.Empty || (result == "ok" ? a.Ok : !a.Ok)) && (term == string.Empty || Ui.Has(a.Action, term) || Ui.Has(a.Target, term) || Ui.Has(names[a.UserId], term))).ToList();
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            if (rows.Count == 0)
            {
                return "<div class=\"lo-panel lo-empty\">" + (all.Count == 0 ? "Nothing has been logged yet." : "No entries match.") + "</div>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"lo-wrapt\"><table class=\"lo-table\"><thead><tr><th>When</th><th>Who</th><th>Action</th><th>Detail</th><th>Result</th></tr></thead><tbody>");
            foreach (ActivityRec a in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr><td class=\"lo-first\">" + Ui.Esc(Ui.When(a.When)) + "</td><td data-label=\"Who\">" + Ui.Esc(names[a.UserId]) + "</td><td data-label=\"Action\">" + Ui.Esc(a.Action) + "</td><td data-label=\"Detail\">" + Ui.Esc(a.Target) + "</td><td data-label=\"Result\"><span class=\"lo-pill " + (a.Ok ? "lo-pill-go" : "lo-pill-bad") + "\">" + (a.Ok ? "Done" : "Refused") + "</span></td></tr>");
            }
            sb.Append("</tbody></table></div><div class=\"lo-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(rows.Count, "entry", "entries") + "</span><div>");
            sb.Append("<button type=\"button\" class=\"lo-btn lo-btn-ghost lo-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminActivityJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"lo-btn lo-btn-ghost lo-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminActivityJs.go(" + (page + 1) + ")\">Next</button></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            List<ActivityRec> rows = await Store.Rows<ActivityRec>("activity");
            string result = (GetDataValue("res") ?? string.Empty).Trim();
            response.SetElementContents("lo-list", await ListHtml(rows, Ui.Clip(GetDataValue("term"), 40), result is "ok" or "no" ? result : string.Empty, PageNo(GetDataValue("pg"))));
            return response;
        }
    }
}
