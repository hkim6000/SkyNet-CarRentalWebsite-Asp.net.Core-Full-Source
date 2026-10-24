using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminMessages : WebPage
    {
        private const int Size = 10;
        private static readonly string[] Statuses = { "New", "Read", "Replied", "Archived" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Messages | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private const string AreaKey = "messages";

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminMessagesJs.denied();");
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
            response.ExecuteScript("AdminMessagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ms-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.SetElementContents("ms-who", Auth.Who("ms", c));
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
            List<InquiryRec> rows = await Store.Rows<InquiryRec>("messages");
            List<BranchRec> areas = await Store.Rows<BranchRec>("branches");
            response.SetElementContents("ms-main", "<div class=\"ms-ph\"><div><h1>Messages</h1><p>Questions sent from the Contact page. Mark each one read, replied or archived so everyone at the desk knows where it stands.</p></div></div><div class=\"ms-tools\"><input type=\"search\" id=\"ms-q\" placeholder=\"Search name, email or message\" maxlength=\"40\" oninput=\"AdminMessagesJs.type()\" aria-label=\"Search messages\"></div><div id=\"ms-list\">" + ListHtml(rows, areas, string.Empty, string.Empty, 1) + "</div>");
            return response;
        }

        private static int PageNo(string? v)
        {
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        private static string Pill(string status)
        {
            string cls = status switch { "New" => "ms-pill-new", "Read" => "ms-pill", "Replied" => "ms-pill-go", _ => "ms-pill-off" };
            return "<span class=\"ms-pill " + cls + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string AreaName(List<BranchRec> areas, string key)
        {
            return key.Length == 0 ? "Any branch" : areas.FirstOrDefault(a => a.Id == key)?.Name ?? key;
        }

        private static string ListHtml(List<InquiryRec> all, List<BranchRec> areas, string term, string status, int page)
        {
            StringBuilder sb = new StringBuilder("<div class=\"ms-chips\" id=\"ms-chips\"><button type=\"button\" class=\"ms-chip" + (status == string.Empty ? " ms-act" : string.Empty) + "\" onclick=\"AdminMessagesJs.status('')\">All<small>" + all.Count + "</small></button>");
            foreach (string s in Statuses)
            {
                sb.Append("<button type=\"button\" class=\"ms-chip" + (status == s ? " ms-act" : string.Empty) + "\" onclick=\"AdminMessagesJs.status('" + s + "')\">" + s + "<small>" + all.Count(m => m.Status == s) + "</small></button>");
            }
            sb.Append("</div><div class=\"ms-gap\"></div>");
            List<InquiryRec> rows = all.Where(m => (status == string.Empty || m.Status == status) && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Email, term) || Ui.Has(m.Text, term))).OrderByDescending(m => m.When).ToList();
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            if (rows.Count == 0)
            {
                sb.Append("<div class=\"ms-panel ms-empty\">" + (all.Count == 0 ? "No messages yet. They appear here when someone uses the form on the Contact page." : "No messages match.") + "</div>");
                return sb.ToString();
            }
            sb.Append("<div class=\"ms-wrapt\"><table class=\"ms-table\"><thead><tr><th>From</th><th>Branch</th><th>Message</th><th>Received</th><th>Status</th></tr></thead><tbody>");
            foreach (InquiryRec m in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr class=\"ms-row\" onclick=\"AdminMessagesJs.open('" + Ui.Js(m.Id) + "')\"><td class=\"ms-first\"><b>" + Ui.Esc(m.Name) + "</b><small>" + Ui.Esc(m.Email) + "</small></td><td data-label=\"Branch\">" + Ui.Esc(AreaName(areas, m.Area)) + "</td><td data-label=\"Message\">" + Ui.Esc(Ui.Clip(m.Text, 90)) + (m.Text.Length > 90 ? "&hellip;" : string.Empty) + "</td><td data-label=\"Received\">" + Ui.Esc(Ui.When(m.When)) + "</td><td data-label=\"Status\">" + Pill(m.Status) + "</td></tr>");
            }
            sb.Append("</tbody></table></div><div class=\"ms-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(rows.Count, "message", "messages") + "</span><div>");
            sb.Append("<button type=\"button\" class=\"ms-btn ms-btn-ghost ms-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminMessagesJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"ms-btn ms-btn-ghost ms-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminMessagesJs.go(" + (page + 1) + ")\">Next</button></div></div>");
            return sb.ToString();
        }

        private async Task<string> ListNow()
        {
            List<InquiryRec> rows = await Store.Rows<InquiryRec>("messages");
            List<BranchRec> areas = await Store.Rows<BranchRec>("branches");
            string status = (GetDataValue("st") ?? string.Empty).Trim();
            return ListHtml(rows, areas, Ui.Clip(GetDataValue("term"), 40), Statuses.Contains(status) ? status : string.Empty, PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("ms-list", await ListNow());
            return response;
        }

        private static string DetailHtml(SiteData site, InquiryRec m, List<BranchRec> areas)
        {
            string subject = "Re: your message to " + site.Legal;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ms-stack\"><dl class=\"ms-kv\"><dt>From</dt><dd>" + Ui.Esc(m.Name) + "</dd><dt>Email</dt><dd><a class=\"ms-link\" href=\"mailto:" + Ui.Esc(m.Email) + "\">" + Ui.Esc(m.Email) + "</a></dd><dt>Phone</dt><dd>" + (m.Phone.Length > 0 ? Ui.Esc(m.Phone) : "Not given") + "</dd><dt>Branch</dt><dd>" + Ui.Esc(AreaName(areas, m.Area)) + "</dd><dt>Received</dt><dd>" + Ui.Esc(Ui.When(m.When)) + "</dd><dt>Booking code</dt><dd>" + (m.Code.Length > 0 ? "<a class=\"ms-link\" href=\"AdminBookings?code=" + Ui.Esc(m.Code) + "\">" + Ui.Esc(m.Code) + "</a>" : "None") + "</dd></dl>");
            sb.Append("<div class=\"ms-quote\">" + Ui.Esc(m.Text) + "</div>");
            sb.Append("<div class=\"ms-field\"><span>Status</span><div class=\"ms-chips\">" + string.Concat(Statuses.Select(s => "<button type=\"button\" class=\"ms-chip" + (s == m.Status ? " ms-act" : string.Empty) + "\" onclick=\"AdminMessagesJs.setStatus('" + Ui.Js(m.Id) + "','" + s + "')\">" + s + "</button>")) + "</div><small>Mark a request Replied once you have answered it, and Archived when it needs no more attention.</small></div>");
            sb.Append("<div class=\"ms-acts\"><a class=\"ms-btn\" href=\"mailto:" + Ui.Esc(m.Email) + "?subject=" + Uri.EscapeDataString(subject) + "\">Reply by email</a><button type=\"button\" class=\"ms-btn ms-btn-ghost\" onclick=\"AdminMessagesJs.closeModal()\">Close</button><button type=\"button\" class=\"ms-btn ms-btn-danger ms-push\" onclick=\"AdminMessagesJs.del('" + Ui.Js(m.Id) + "','" + Ui.Js(m.Name) + "')\">Delete</button></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            SiteData site = await Store.LoadSite();
            InquiryRec? m;
            using (await Store.Hold())
            {
                List<InquiryRec> rows = await Store.Rows<InquiryRec>("messages");
                m = rows.FirstOrDefault(x => x.Id == id);
                if (m != null && m.Status == "New")
                {
                    m.Status = "Read";
                    await Store.SaveRows("messages", rows);
                }
            }
            if (m == null)
            {
                response.ExecuteScript("AdminMessagesJs.toast('That message was not found.');");
                return response;
            }
            List<BranchRec> areas = await Store.Rows<BranchRec>("branches");
            response.SetElementContents("ms-mbody", DetailHtml(site, m, areas));
            response.SetElementContents("ms-list", await ListNow());
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.opened('" + Ui.Js("Request from " + m.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> SetStatus()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Deny(response, c, "message status", "You can read messages but not change their status.");
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            string status = (GetDataValue("to") ?? string.Empty).Trim();
            if (!Statuses.Contains(status))
            {
                return await Refuse(response, c, "message status", id, "That status is not allowed.");
            }
            SiteData site = await Store.LoadSite();
            InquiryRec? m;
            using (await Store.Hold())
            {
                List<InquiryRec> rows = await Store.Rows<InquiryRec>("messages");
                m = rows.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    m.Status = status;
                    await Store.SaveRows("messages", rows);
                }
            }
            if (m == null)
            {
                response.ExecuteScript("AdminMessagesJs.toast('That message was not found.');");
                return response;
            }
            await Store.Log(c.U.Id, "message " + status.ToLowerInvariant(), m.Name, true);
            List<BranchRec> areas = await Store.Rows<BranchRec>("branches");
            response.SetElementContents("ms-mbody", DetailHtml(site, m, areas));
            response.SetElementContents("ms-list", await ListNow());
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.toast('Marked " + status + "');");
            return response;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminMessagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'D'))
            {
                return await Deny(response, c, "delete message", "You can read messages but not delete them.");
            }
            string id = Ui.Clip(GetDataValue("id"), 40);
            string name = string.Empty;
            using (await Store.Hold())
            {
                List<InquiryRec> rows = await Store.Rows<InquiryRec>("messages");
                InquiryRec? m = rows.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    name = m.Name;
                    rows.Remove(m);
                    await Store.SaveRows("messages", rows);
                }
            }
            if (name.Length == 0)
            {
                response.ExecuteScript("AdminMessagesJs.toast('That message was not found.');");
                return response;
            }
            await Store.Log(c.U.Id, "delete message", name, true);
            response.SetElementContents("ms-list", await ListNow());
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.deleted();");
            return response;
        }
    }
}
