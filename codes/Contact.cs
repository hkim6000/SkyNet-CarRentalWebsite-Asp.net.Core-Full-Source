using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class Contact : WebPage
    {
        private static readonly Dictionary<string, List<DateTime>> Recent = new Dictionary<string, List<DateTime>>();
        private string pre = string.Empty;

        public override async Task OnInitialized()
        {
            PublicData d = await Store.LoadPublic();
            string b = Ui.Clip(QueryValue("b"), 60);
            pre = d.Branches.Any(x => x.Id == b) ? b : string.Empty;
            HtmlDoc.SetTitle("Contact | " + d.Site.Legal);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", "Call, email or send a message to " + d.Site.Legal + ".");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_head}", HeadHtml(d, "Contact"))
                .Replace("{plhd_main}", PageHtml(d, "contact"))
                .Replace("{plhd_foot}", FootHtml(d));
        }

        private string DataPart(PublicData d, Block b, bool first)
        {
            if (b.Source != "contactform")
            {
                return string.Empty;
            }
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<section class=\"ct-wrap ct-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + Head(s, b, true, first ? "h1" : "h2") + "<div class=\"ct-ctwo\"><div class=\"ct-details\"><h2>Call or write</h2><ul class=\"ct-contact\"><li>" + Icon("phone") + "<a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a></li><li>" + Icon("mail") + "<a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></li></ul>");
            foreach (BranchRec x in d.Branches)
            {
                sb.Append("<div class=\"ct-cbr\"><b>" + Ui.Esc(x.Name) + "</b><span>" + Ui.Esc(x.Street + ", " + x.City + ", " + x.Region + " " + x.Zip) + "</span><a href=\"tel:" + Ui.Esc(x.Phone) + "\">" + Ui.Esc(x.Phone) + "</a><small>" + Lines(x.Hours) + "</small></div>");
            }
            sb.Append("<p class=\"ct-hint\">To change or cancel a booking, use <a href=\"Manage\">Manage booking</a>. It is faster.</p></div>");
            sb.Append("<form class=\"ct-form ct-panel\" id=\"ct-form\" onsubmit=\"return ContactJs.send()\" novalidate><h2>Send us a message</h2>");
            sb.Append("<label class=\"ct-field\" for=\"ct-name\"><span>Your name</span><input type=\"text\" id=\"ct-name\" maxlength=\"60\" autocomplete=\"name\"><em class=\"ct-err\" id=\"ct-e-name\"></em></label>");
            sb.Append("<div class=\"ct-grid2\"><label class=\"ct-field\" for=\"ct-email\"><span>Email</span><input type=\"email\" id=\"ct-email\" maxlength=\"80\" autocomplete=\"email\"><em class=\"ct-err\" id=\"ct-e-email\"></em></label><label class=\"ct-field\" for=\"ct-phone\"><span>Phone (optional)</span><input type=\"tel\" id=\"ct-phone\" maxlength=\"30\" autocomplete=\"tel\"><em class=\"ct-err\" id=\"ct-e-phone\"></em></label></div>");
            sb.Append("<div class=\"ct-grid2\"><label class=\"ct-field\" for=\"ct-branch\"><span>Branch</span><select id=\"ct-branch\"><option value=\"\">Any branch</option>" + string.Concat(d.Branches.Select(x => "<option value=\"" + Ui.Esc(x.Id) + "\"" + (x.Id == pre ? " selected" : string.Empty) + ">" + Ui.Esc(x.Name) + "</option>")) + "</select></label><label class=\"ct-field\" for=\"ct-code\"><span>Booking code (if you have one)</span><input type=\"text\" id=\"ct-code\" maxlength=\"12\" placeholder=\"BR-XXXXXX\" autocapitalize=\"characters\"><em class=\"ct-err\" id=\"ct-e-code\"></em></label></div>");
            sb.Append("<label class=\"ct-field\" for=\"ct-msg\"><span>How can we help?</span><textarea id=\"ct-msg\" rows=\"6\" maxlength=\"2000\"></textarea><em class=\"ct-err\" id=\"ct-e-msg\"></em></label>");
            sb.Append("<div class=\"ct-hp\" aria-hidden=\"true\"><label for=\"ct-web\">Leave this field empty</label><input type=\"text\" id=\"ct-web\" tabindex=\"-1\" autocomplete=\"off\"></div>");
            sb.Append("<button type=\"submit\" class=\"ct-btn ct-wide\" id=\"ct-send\">Send message</button><div class=\"ct-sent\" id=\"ct-sent\" role=\"status\" aria-live=\"polite\"></div></form></div></section>");
            return sb.ToString();
        }

        private static bool Allowed(string visitor)
        {
            lock (Recent)
            {
                if (!Recent.TryGetValue(visitor, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Recent[visitor] = list;
                }
                list.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-10));
                return list.Count < 3;
            }
        }

        private static void Noted(string visitor)
        {
            lock (Recent)
            {
                if (!Recent.TryGetValue(visitor, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Recent[visitor] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        public async Task<ApiResponse> Submit()
        {
            ApiResponse response = new ApiResponse();
            PublicData d = await Store.LoadPublic();
            string name = Ui.Clip(GetDataValue("name"), 60);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string branch = Ui.Clip(GetDataValue("branch"), 60);
            branch = d.Branches.Any(x => x.Id == branch) ? branch : string.Empty;
            string code = Ui.Clip(GetDataValue("code"), 12).ToUpperInvariant();
            string msg = (GetDataValue("msg") ?? string.Empty).Replace("\r", string.Empty).Trim();
            string trap = (GetDataValue("web") ?? string.Empty).Trim();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Please enter your name." : string.Empty,
                ["email"] = !Ui.IsEmail(email) ? "Please enter a valid email address." : string.Empty,
                ["phone"] = phone.Length > 0 && !Ui.IsPhone(phone) ? "Please enter a valid phone number, or leave it empty." : string.Empty,
                ["code"] = code.Length > 0 && (code.Length < 6 || !code.All(ch => char.IsLetterOrDigit(ch) || ch == '-')) ? "That does not look like a booking code. Leave it empty if you are not sure." : string.Empty,
                ["msg"] = msg.Length < 10 ? "Please write a little more so we understand your question." : msg.Length > 2000 ? "Please keep it under 2,000 characters." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("ct-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                response.SetElementContents("ct-sent", string.Empty);
                response.ExecuteScript("ContactJs.failed();");
                return response;
            }
            string who = Local.Visitor();
            if (trap.Length > 0)
            {
                response.SetElementContents("ct-sent", "<b>Thank you.</b> Your message was received.");
                response.ExecuteScript("ContactJs.sent();");
                return response;
            }
            if (!Allowed(who))
            {
                response.SetElementContents("ct-sent", "<span class=\"ct-bad\">You have sent several messages in a short time. Please wait a few minutes, or call us on " + Ui.Esc(d.Site.Phone) + ".</span>");
                response.ExecuteScript("ContactJs.failed();");
                return response;
            }
            Noted(who);
            InquiryRec rec = new InquiryRec { Id = Guid.NewGuid().ToString("N").Substring(0, 12), Name = name, Email = email, Phone = phone, Area = branch, Code = code, Text = msg, When = DateTime.UtcNow, Status = "New" };
            using (await Store.Hold())
            {
                List<InquiryRec> rows = await Store.Rows<InquiryRec>("messages");
                rows.Add(rec);
                await Store.SaveRows("messages", rows);
            }
            await Store.Log("visitor", "contact message", name, true);
            if (Mailer.Ready && Ui.IsEmail(d.Site.NotifyEmail))
            {
                string bname = d.Branches.FirstOrDefault(x => x.Id == branch)?.Name ?? "Any branch";
                string result = await Mailer.Send(d.Site.NotifyEmail, "New message from " + name, "From: " + name + " <" + email + ">\nPhone: " + (phone.Length > 0 ? phone : "not given") + "\nBranch: " + bname + "\nBooking code: " + (code.Length > 0 ? code : "none") + "\n\n" + msg + "\n");
                if (result.Length > 0)
                {
                    await Store.Log("system", "notification email", result, false);
                }
            }
            response.SetElementContents("ct-sent", "<b>Thank you, " + Ui.Esc(name) + ".</b> We have your message and will answer by email" + (phone.Length > 0 ? " or phone" : string.Empty) + ", usually within one working day.");
            response.ExecuteScript("ContactJs.sent();");
            return response;
        }

        private static readonly (string Page, string Name)[] NavItems =
        {
            ("Home", "Home"),
            ("Fleet", "Fleet"),
            ("Branches", "Branches"),
            ("Policies", "Rental policies"),
            ("Contact", "Contact")
        };

        private static string Tok(SiteData site, string v)
        {
            return v.Replace("{name}", site.Brand);
        }

        private static string TokH(SiteData site, string v)
        {
            return v.Replace("{name}", Ui.Esc(site.Brand));
        }

        private static string Lines(string v)
        {
            return Ui.Esc(v).Replace("\n", "<br>");
        }

        private static string Icon(string k)
        {
            string a = "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">";
            return k switch
            {
                "car" => a + "<path d=\"M5 16h14M3.5 16v-3.5l2-5A2 2 0 0 1 7.4 6h9.2a2 2 0 0 1 1.9 1.5l2 5V16\"/><path d=\"M3.5 12.5h17\"/><circle cx=\"7.5\" cy=\"16.5\" r=\"1.8\"/><circle cx=\"16.5\" cy=\"16.5\" r=\"1.8\"/></svg>",
                "key" => a + "<circle cx=\"8\" cy=\"15\" r=\"4\"/><path d=\"M11 12l8-8M16 7l2 2M14 9l2 2\"/></svg>",
                "calendar" => a + "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/></svg>",
                "pin" => a + "<path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/></svg>",
                "shield" => a + "<path d=\"M12 3l7 3v5c0 5-3 8.5-7 10-4-1.5-7-5-7-10V6z\"/><path d=\"M9 12l2 2 4-4\"/></svg>",
                "fuel" => a + "<path d=\"M5 20V5a1.5 1.5 0 0 1 1.5-1.5h6A1.5 1.5 0 0 1 14 5v15M3.5 20h12M5 10h9\"/><path d=\"M14 8l3 2.5V17a1.5 1.5 0 0 0 3 0V9l-2.5-2.5\"/></svg>",
                "clock" => a + "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg>",
                "phone" => a + "<path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg>",
                "mail" => a + "<rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg>",
                "check" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>",
                "seat" => a + "<path d=\"M8 4h5a2 2 0 0 1 2 2v7H9a2 2 0 0 1-2-2V5a1 1 0 0 1 1-1z\"/><path d=\"M6 13h11a2 2 0 0 1 2 2v2H7a2 2 0 0 1-2-2v-1\"/><path d=\"M8 17v3M17 17v3\"/></svg>",
                "road" => a + "<path d=\"M8 21L10.5 3M16 21L13.5 3\"/><path d=\"M12 6v2M12 11v2M12 16v2\"/></svg>",
                "card" => a + "<rect x=\"3\" y=\"6\" width=\"18\" height=\"12\" rx=\"2\"/><path d=\"M3 10h18M7 15h4\"/></svg>",
                "people" => a + "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg>",
                "bag" => a + "<rect x=\"4\" y=\"8\" width=\"16\" height=\"12\" rx=\"2\"/><path d=\"M9 8V5.5A1.5 1.5 0 0 1 10.5 4h3A1.5 1.5 0 0 1 15 5.5V8M9 12v4M15 12v4\"/></svg>",
                "gear" => a + "<circle cx=\"6\" cy=\"6\" r=\"1.8\"/><circle cx=\"12\" cy=\"6\" r=\"1.8\"/><circle cx=\"18\" cy=\"6\" r=\"1.8\"/><circle cx=\"6\" cy=\"18\" r=\"1.8\"/><circle cx=\"12\" cy=\"18\" r=\"1.8\"/><path d=\"M6 8v8M12 8v8M18 8v4H6\"/></svg>",
                "door" => a + "<path d=\"M5 20V8l6-4h8v16z\"/><path d=\"M5 12h14M15 15h2\"/></svg>",
                _ => string.Empty
            };
        }

        private static string Logo(int size)
        {
            return "<svg viewBox=\"0 0 40 40\" width=\"" + size + "\" height=\"" + size + "\" aria-hidden=\"true\"><rect width=\"40\" height=\"40\" rx=\"9\" fill=\"#13202B\"/><path d=\"M12 30L18 10M28 30L22 10\" fill=\"none\" stroke=\"#FFFFFF\" stroke-width=\"2.6\" stroke-linecap=\"round\"/><path d=\"M20 13v3M20 20v3M20 27v2\" fill=\"none\" stroke=\"#E2622B\" stroke-width=\"2.6\" stroke-linecap=\"round\"/></svg>";
        }

        private static string Pic(List<MediaItem> media, string src, string alt, bool eager, bool thumb)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            string use = thumb && m != null && m.Thumb.Length > 0 ? m.Thumb : src;
            return "<img src=\"" + Ui.Esc(use) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + " decoding=\"async\">";
        }

        private static string Mv(List<MediaItem> media, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            if (Blocks.IsVideo(src))
            {
                return "<video src=\"" + Ui.Esc(src) + "\" playsinline " + (bg ? "autoplay muted loop aria-hidden=\"true\"" : "controls preload=\"metadata\" aria-label=\"" + Ui.Esc(alt) + "\"") + "></video>";
            }
            return Pic(media, src, alt, eager, false);
        }

        private static string Btn(SiteData site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"ct-btn" + (main ? string.Empty : " ct-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"ct-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"ct-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"ct-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"ct-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "ct-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"ct-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"ct-wrap ct-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            List<BlockItem> more = b.Items.Where(i => i.Media != string.Empty && !Blocks.IsVideo(i.Media) && media.Any(m => m.Path == i.Media)).ToList();
            string text = "<div class=\"ct-wrap ct-bhero-in\"><div class=\"ct-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"ct-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "ct-blead") + Btns(site, b) + "</div></div>";
            if (mv != string.Empty && more.Count > 0)
            {
                MediaItem? first = media.FirstOrDefault(m => m.Path == b.Media);
                List<(string Html, string Cap)> all = new List<(string, string)> { (mv, first?.Caption ?? string.Empty) };
                foreach (BlockItem it in more)
                {
                    MediaItem? m = media.FirstOrDefault(x => x.Path == it.Media);
                    all.Add(("<img src=\"" + Ui.Esc(it.Media) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : it.Title) + "\" decoding=\"async\">", it.Title));
                }
                StringBuilder sl = new StringBuilder("<div class=\"ct-bhero-p ct-slides\" id=\"ct-slides\" aria-roledescription=\"carousel\" aria-label=\"Pictures\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<figure class=\"ct-slide" + (k == 0 ? " ct-on" : string.Empty) + "\" aria-roledescription=\"slide\" aria-label=\"" + (k + 1) + " of " + all.Count + "\"" + (k == 0 ? string.Empty : " aria-hidden=\"true\"") + ">" + all[k].Html + (all[k].Cap == string.Empty ? string.Empty : "<figcaption>" + Ui.Esc(all[k].Cap) + "</figcaption>") + "</figure>");
                }
                sl.Append("<div class=\"ct-sctl\"><button type=\"button\" class=\"ct-sarr\" aria-label=\"Previous picture\" onclick=\"ContactJs.slide(-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><div class=\"ct-sdots\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<button type=\"button\" class=\"ct-sdot" + (k == 0 ? " ct-on" : string.Empty) + "\" aria-label=\"Show picture " + (k + 1) + "\" aria-pressed=\"" + (k == 0 ? "true" : "false") + "\" onclick=\"ContactJs.slideTo(" + k + ")\"></button>");
                }
                sl.Append("</div><button type=\"button\" class=\"ct-sarr\" aria-label=\"Next picture\" onclick=\"ContactJs.slide(1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div></div>");
                return "<section class=\"ct-bhero ct-bslides\" id=\"" + Ui.Esc(b.Id) + "\">" + sl + text + "</section>";
            }
            return "<section class=\"ct-bhero" + (mv == string.Empty ? " ct-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"ct-bhero-p\">" + mv + "</div>" : string.Empty) + text + "</section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"ct-bsplit" + (b.Flip ? " ct-bflip" : string.Empty) + (b.Media == string.Empty ? " ct-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"ct-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"ct-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"ct-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"ct-bcards ct-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"ct-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                sb.Append(it.Link != string.Empty ? "<a class=\"ct-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"ct-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>" : "<div class=\"ct-bcard\">" + inner + "</div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"ct-bsteps ct-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><i>" + i++ + "</i><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><p>" + Lines(Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(b, sb.ToString());
        }

        private static string StatRow(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"ct-bstats ct-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"ct-bpeople ct-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                string[] parts = it.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
                sb.Append("<div class=\"ct-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"ct-bnoface\">" + Ui.Esc(ini) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"ct-bfaq\">" + Head(site, b, false) + "<div class=\"ct-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"ct-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"ct-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"ct-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "ct-blead") + "</div>" + Btns(site, b) + "</div>");
        }

        private string PageHtml(PublicData d, string key)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            if (!d.Site.Pages.TryGetValue(key, out PageDoc? doc))
            {
                return string.Empty;
            }
            foreach (Block b in doc.Blocks.Where(x => !x.Hidden))
            {
                string html = b.Type switch
                {
                    "hero" => Hero(d.Site, d.Media, b, first),
                    "text" => Sec(b, "<div class=\"ct-btext\">" + Kick(d.Site, b) + H2(d.Site, b, first ? "h1" : "h2") + Rich(d.Site, b.Text, string.Empty) + "</div>"),
                    "split" => Split(d.Site, d.Media, b),
                    "cards" => Cards(d.Site, b),
                    "steps" => Steps(d.Site, b),
                    "stats" => StatRow(d.Site, b),
                    "people" => People(d.Site, d.Media, b),
                    "faq" => Faq(d.Site, b),
                    "media" => Wide(d.Site, d.Media, b),
                    "cta" => Cta(d.Site, b),
                    "data" => DataPart(d, b, first),
                    _ => string.Empty
                };
                if (html.Length > 0 && (b.Type == "hero" || b.Type == "data" || b.Type == "text"))
                {
                    first = false;
                }
                sb.Append(html);
            }
            return sb.ToString();
        }

        private static string HeadHtml(PublicData d, string active)
        {
            StringBuilder sb = new StringBuilder("<a class=\"ct-skip\" href=\"#ct-main\">Skip to content</a><header class=\"ct-head\" id=\"ct-head\"><div class=\"ct-wrap ct-head-row\"><a class=\"ct-logo\" href=\"Home\" aria-label=\"" + Ui.Esc(d.Site.Legal) + " home\">" + Logo(40) + "<span><b>" + Ui.Esc(d.Site.Brand) + "</b><small>Car Rental</small></span></a>");
            sb.Append("<nav class=\"ct-nav\" id=\"ct-nav\" aria-label=\"Main\"><div class=\"ct-nav-top\"><b>Menu</b><button type=\"button\" class=\"ct-ibtn\" aria-label=\"Close menu\" onclick=\"ContactJs.closeNav()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><ul class=\"ct-menu\">");
            foreach ((string page, string name) in NavItems)
            {
                sb.Append("<li><a class=\"ct-ml" + (page == active ? " ct-on" : string.Empty) + "\" href=\"" + page + "\"" + (page == active ? " aria-current=\"page\"" : string.Empty) + ">" + name + "</a></li>");
            }
            sb.Append("<li class=\"ct-mmanage\"><a class=\"ct-ml" + (active == "Manage" ? " ct-on" : string.Empty) + "\" href=\"Manage\">Manage booking</a></li></ul></nav><a class=\"ct-btn ct-btn-ghost ct-btn-sm ct-head-mg\" href=\"Manage\">Manage booking</a><a class=\"ct-btn ct-btn-sm ct-head-cta\" href=\"Home#find\">Book a car</a><button type=\"button\" class=\"ct-burger\" aria-label=\"Open menu\" onclick=\"ContactJs.openNav()\"><svg viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M4 12h16M4 17h16\"/></svg></button></div><div class=\"ct-scrim\" id=\"ct-scrim\" onclick=\"ContactJs.closeNav()\"></div></header>");
            return sb.ToString();
        }

        private static string FootHtml(PublicData d)
        {
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<footer class=\"ct-foot\"><div class=\"ct-wrap ct-foot-grid\"><div class=\"ct-fbrand\"><a class=\"ct-logo\" href=\"Home\">" + Logo(40) + "<span><b>" + Ui.Esc(s.Brand) + "</b><small>Car Rental</small></span></a><p>" + Ui.Esc(s.Tagline) + "</p><p class=\"ct-fcontact\"><a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a><br><a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></p></div>");
            sb.Append("<div><h3>Rent</h3><ul><li><a href=\"Home#find\">Find a car</a></li><li><a href=\"Fleet\">Fleet</a></li><li><a href=\"Manage\">Manage booking</a></li><li><a href=\"Policies\">Rental policies</a></li><li><a href=\"Contact\">Contact</a></li></ul></div>");
            sb.Append("<div><h3>Classes</h3><ul>" + string.Concat(d.Classes.Take(7).Select(k => "<li><a href=\"Vehicle?c=" + Ui.Esc(k.Id) + "\">" + Ui.Esc(k.Name) + "</a></li>")) + "</ul></div>");
            sb.Append("<div><h3>Branches</h3><ul class=\"ct-fbr\">" + string.Concat(d.Branches.Select(b => "<li><a href=\"Branches#b-" + Ui.Esc(b.Id) + "\"><b>" + Ui.Esc(b.Name) + "</b></a><span>" + Ui.Esc(b.Street + ", " + b.City) + "</span><span><a href=\"tel:" + Ui.Esc(b.Phone) + "\">" + Ui.Esc(b.Phone) + "</a></span></li>")) + "</ul></div></div>");
            sb.Append("<div class=\"ct-foot-b\"><div class=\"ct-wrap\"><span>&copy; " + DateTime.Now.Year + " " + Ui.Esc(s.Legal) + ". Prices in US dollars. Taxes added at checkout.</span><a class=\"ct-staff\" href=\"AdminLogin\">Staff sign in</a></div></div></footer>");
            return sb.ToString();
        }

        private static string Specs(ClassRec k)
        {
            return "<ul class=\"ct-specs\"><li title=\"Seats\">" + Icon("people") + k.Seats + "</li><li title=\"Large bags\">" + Icon("bag") + k.Bags + "</li><li title=\"Doors\">" + Icon("door") + k.Doors + "</li><li title=\"Transmission\">" + Icon("gear") + Ui.Esc(k.Transmission) + "</li>" + (k.Fuel != "Gas" ? "<li title=\"Fuel\">" + Icon("fuel") + Ui.Esc(k.Fuel) + "</li>" : string.Empty) + "</ul>";
        }

        private static string ClassCard(PublicData d, ClassRec k, string href, string foot)
        {
            return "<article class=\"ct-ccard\"><a class=\"ct-ccard-img\" href=\"Vehicle?c=" + Ui.Esc(k.Id) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, k.Media, k.Name, false, true) + "</a><div class=\"ct-ccard-b\"><div class=\"ct-ccard-h\"><h3><a href=\"Vehicle?c=" + Ui.Esc(k.Id) + "\">" + Ui.Esc(k.Name) + "</a></h3><small>" + Ui.Esc(k.Example) + " or similar</small></div>" + Specs(k) + (foot.Length > 0 ? foot : "<div class=\"ct-ccard-f\"><span class=\"ct-from\"><small>From</small><b>" + Rental.Money(Math.Min(k.Daily, k.Weekend)) + "</b><small>a day</small></span><a class=\"ct-btn ct-btn-sm\" href=\"" + Ui.Esc(href) + "\">See dates</a></div>") + "</div></article>";
        }

        private static (DateTime S, DateTime E) DefaultTrip()
        {
            DateTime s = DateTime.Today.AddDays(1).AddHours(10);
            return (s, s.AddDays(3));
        }

        private static string SearchBox(PublicData d, string branch, DateTime s, DateTime e, string cls, string title)
        {
            string min = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
            StringBuilder sb = new StringBuilder("<form class=\"ct-search\" id=\"find\" action=\"Search\" method=\"get\" onsubmit=\"return ContactJs.find(this)\">" + (title.Length > 0 ? "<h2 class=\"ct-search-t\">" + Ui.Esc(title) + "</h2>" : string.Empty) + "<div class=\"ct-search-row\">");
            sb.Append("<label class=\"ct-sf ct-sf-b\"><span>" + Icon("pin") + "Pick-up and return at</span><select name=\"b\" aria-label=\"Branch\">" + string.Concat(d.Branches.Select(b => "<option value=\"" + Ui.Esc(b.Id) + "\"" + (b.Id == branch ? " selected" : string.Empty) + ">" + Ui.Esc(b.Name) + "</option>")) + "</select></label>");
            sb.Append("<label class=\"ct-sf\"><span>" + Icon("calendar") + "Pick-up</span><input type=\"datetime-local\" name=\"s\" step=\"1800\" min=\"" + min + "\" value=\"" + Rental.Iso(s) + "\" required></label>");
            sb.Append("<label class=\"ct-sf\"><span>" + Icon("calendar") + "Return</span><input type=\"datetime-local\" name=\"e\" step=\"1800\" min=\"" + min + "\" value=\"" + Rental.Iso(e) + "\" required></label>");
            if (cls.Length > 0)
            {
                sb.Append("<input type=\"hidden\" name=\"c\" value=\"" + Ui.Esc(cls) + "\">");
            }
            sb.Append("<button type=\"submit\" class=\"ct-btn ct-sbtn\">Find a car</button></div><p class=\"ct-serr\" role=\"alert\"></p></form>");
            return sb.ToString();
        }
    }
}
