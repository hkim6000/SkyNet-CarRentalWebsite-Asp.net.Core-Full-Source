using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class Search : WebPage
    {
        public override async Task OnInitialized()
        {
            PublicData d = await Store.LoadPublic();
            FleetData f = await Avail.LoadSettled();
            string branch = Ui.Clip(QueryValue("b"), 60);
            string cls = Ui.Clip(QueryValue("c"), 60);
            bool okS = Rental.ParseLocal(QueryValue("s"), out DateTime s);
            bool okE = Rental.ParseLocal(QueryValue("e"), out DateTime e);
            (DateTime s0, DateTime e0) = DefaultTrip();
            if (!d.Branches.Any(x => x.Id == branch))
            {
                branch = d.Branches.FirstOrDefault()?.Id ?? string.Empty;
            }
            HtmlDoc.SetTitle("Available cars | " + d.Site.Legal);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, follow");
            StringBuilder sb = new StringBuilder("<div class=\"sr-wrap sr-ph\"><div class=\"sr-crumbs\"><a href=\"Home\">Home</a> / Available cars</div><h1>Available cars</h1></div><div class=\"sr-wrap sr-top\">" + SearchBox(d, branch, okS ? s : s0, okE ? e : e0, string.Empty, string.Empty) + "</div>");
            string err = !okS || !okE ? "Choose the pick-up and return date and time, then search." : Ops.TripError(f, branch, string.Empty, s, e, false);
            if (err.Length > 0)
            {
                sb.Append("<section class=\"sr-wrap sr-sec\"><p class=\"sr-note\">" + Ui.Esc(err) + "</p></section>");
            }
            else
            {
                sb.Append(Results(d, f, branch, cls, s, e));
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_head}", HeadHtml(d, string.Empty))
                .Replace("{plhd_main}", sb.ToString())
                .Replace("{plhd_foot}", FootHtml(d));
        }

        private static string Results(PublicData d, FleetData f, string branch, string cls, DateTime s, DateTime e)
        {
            BranchRec b = d.Branches.First(x => x.Id == branch);
            int days = Rental.Days(s, e, d.Site.GraceMinutes);
            List<ClassRec> order = d.Classes.OrderByDescending(k => k.Id == cls).ToList();
            List<(ClassRec K, bool Free, Quote Q)> rows = new List<(ClassRec, bool, Quote)>();
            foreach (ClassRec k in order)
            {
                bool free = Avail.Assign(f, branch, k.Id, s, e, string.Empty).CarId.Length > 0;
                rows.Add((k, free, Rental.Price(d.Site, k, d.Extras, s, e, new List<BookedExtra>(), 0)));
            }
            int n = rows.Count(r => r.Free);
            StringBuilder sb = new StringBuilder("<section class=\"sr-wrap sr-sec sr-res\"><div class=\"sr-trip\"><div><b>" + Ui.Esc(b.Name) + "</b><span>" + Ui.Esc(Rental.When(s)) + " to " + Ui.Esc(Rental.When(e)) + "</span></div><span class=\"sr-days\">" + Ui.Count(days, "rental day", "rental days") + "</span></div>");
            sb.Append("<p class=\"sr-hint sr-count\">" + (n == 0 ? "Sorry, every class is booked for these dates at this branch. Try other dates or the other branch." : Ui.Count(n, "class is", "classes are") + " free. Prices are for the whole rental before tax, extras and coverage.") + "</p><div class=\"sr-cgrid\">");
            foreach ((ClassRec k, bool free, Quote q) in rows)
            {
                string link = "Book?b=" + Uri.EscapeDataString(branch) + "&c=" + Uri.EscapeDataString(k.Id) + "&s=" + Uri.EscapeDataString(Rental.Iso(s)) + "&e=" + Uri.EscapeDataString(Rental.Iso(e));
                string foot = free
                    ? "<div class=\"sr-ccard-f\"><span class=\"sr-price\"><b>" + Rental.Money(q.Rate) + "</b><small>" + Ui.Count(q.Days, "day", "days") + " &middot; about " + Rental.Money(Math.Round(q.Rate / Math.Max(1, q.Days), 2)) + " a day</small></span><a class=\"sr-btn sr-btn-sm\" href=\"" + Ui.Esc(link) + "\">Book</a></div>"
                    : "<div class=\"sr-ccard-f\"><span class=\"sr-sold\">Not available for these dates</span></div>";
                string card = ClassCard(d, k, link, foot);
                if (!free)
                {
                    card = card.Replace("<article class=\"sr-ccard\">", "<article class=\"sr-ccard sr-off\">");
                }
                if (k.Id == cls)
                {
                    card = card.Replace("<article class=\"sr-ccard", "<article data-pick=\"1\" class=\"sr-ccard sr-pick");
                }
                sb.Append(card);
            }
            sb.Append("</div></section>");
            return sb.ToString();
        }

        private string DataPart(PublicData d, Block b, bool first)
        {
            return string.Empty;
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
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"sr-btn" + (main ? string.Empty : " sr-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"sr-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"sr-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"sr-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"sr-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "sr-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"sr-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"sr-wrap sr-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            List<BlockItem> more = b.Items.Where(i => i.Media != string.Empty && !Blocks.IsVideo(i.Media) && media.Any(m => m.Path == i.Media)).ToList();
            string text = "<div class=\"sr-wrap sr-bhero-in\"><div class=\"sr-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"sr-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "sr-blead") + Btns(site, b) + "</div></div>";
            if (mv != string.Empty && more.Count > 0)
            {
                MediaItem? first = media.FirstOrDefault(m => m.Path == b.Media);
                List<(string Html, string Cap)> all = new List<(string, string)> { (mv, first?.Caption ?? string.Empty) };
                foreach (BlockItem it in more)
                {
                    MediaItem? m = media.FirstOrDefault(x => x.Path == it.Media);
                    all.Add(("<img src=\"" + Ui.Esc(it.Media) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : it.Title) + "\" decoding=\"async\">", it.Title));
                }
                StringBuilder sl = new StringBuilder("<div class=\"sr-bhero-p sr-slides\" id=\"sr-slides\" aria-roledescription=\"carousel\" aria-label=\"Pictures\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<figure class=\"sr-slide" + (k == 0 ? " sr-on" : string.Empty) + "\" aria-roledescription=\"slide\" aria-label=\"" + (k + 1) + " of " + all.Count + "\"" + (k == 0 ? string.Empty : " aria-hidden=\"true\"") + ">" + all[k].Html + (all[k].Cap == string.Empty ? string.Empty : "<figcaption>" + Ui.Esc(all[k].Cap) + "</figcaption>") + "</figure>");
                }
                sl.Append("<div class=\"sr-sctl\"><button type=\"button\" class=\"sr-sarr\" aria-label=\"Previous picture\" onclick=\"SearchJs.slide(-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><div class=\"sr-sdots\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<button type=\"button\" class=\"sr-sdot" + (k == 0 ? " sr-on" : string.Empty) + "\" aria-label=\"Show picture " + (k + 1) + "\" aria-pressed=\"" + (k == 0 ? "true" : "false") + "\" onclick=\"SearchJs.slideTo(" + k + ")\"></button>");
                }
                sl.Append("</div><button type=\"button\" class=\"sr-sarr\" aria-label=\"Next picture\" onclick=\"SearchJs.slide(1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div></div>");
                return "<section class=\"sr-bhero sr-bslides\" id=\"" + Ui.Esc(b.Id) + "\">" + sl + text + "</section>";
            }
            return "<section class=\"sr-bhero" + (mv == string.Empty ? " sr-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"sr-bhero-p\">" + mv + "</div>" : string.Empty) + text + "</section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"sr-bsplit" + (b.Flip ? " sr-bflip" : string.Empty) + (b.Media == string.Empty ? " sr-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"sr-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"sr-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"sr-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"sr-bcards sr-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"sr-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                sb.Append(it.Link != string.Empty ? "<a class=\"sr-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"sr-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>" : "<div class=\"sr-bcard\">" + inner + "</div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"sr-bsteps sr-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"sr-bstats sr-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"sr-bpeople sr-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                string[] parts = it.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
                sb.Append("<div class=\"sr-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"sr-bnoface\">" + Ui.Esc(ini) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"sr-bfaq\">" + Head(site, b, false) + "<div class=\"sr-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"sr-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"sr-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"sr-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "sr-blead") + "</div>" + Btns(site, b) + "</div>");
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
                    "text" => Sec(b, "<div class=\"sr-btext\">" + Kick(d.Site, b) + H2(d.Site, b, first ? "h1" : "h2") + Rich(d.Site, b.Text, string.Empty) + "</div>"),
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
            StringBuilder sb = new StringBuilder("<a class=\"sr-skip\" href=\"#sr-main\">Skip to content</a><header class=\"sr-head\" id=\"sr-head\"><div class=\"sr-wrap sr-head-row\"><a class=\"sr-logo\" href=\"Home\" aria-label=\"" + Ui.Esc(d.Site.Legal) + " home\">" + Logo(40) + "<span><b>" + Ui.Esc(d.Site.Brand) + "</b><small>Car Rental</small></span></a>");
            sb.Append("<nav class=\"sr-nav\" id=\"sr-nav\" aria-label=\"Main\"><div class=\"sr-nav-top\"><b>Menu</b><button type=\"button\" class=\"sr-ibtn\" aria-label=\"Close menu\" onclick=\"SearchJs.closeNav()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><ul class=\"sr-menu\">");
            foreach ((string page, string name) in NavItems)
            {
                sb.Append("<li><a class=\"sr-ml" + (page == active ? " sr-on" : string.Empty) + "\" href=\"" + page + "\"" + (page == active ? " aria-current=\"page\"" : string.Empty) + ">" + name + "</a></li>");
            }
            sb.Append("<li class=\"sr-mmanage\"><a class=\"sr-ml" + (active == "Manage" ? " sr-on" : string.Empty) + "\" href=\"Manage\">Manage booking</a></li></ul></nav><a class=\"sr-btn sr-btn-ghost sr-btn-sm sr-head-mg\" href=\"Manage\">Manage booking</a><a class=\"sr-btn sr-btn-sm sr-head-cta\" href=\"Home#find\">Book a car</a><button type=\"button\" class=\"sr-burger\" aria-label=\"Open menu\" onclick=\"SearchJs.openNav()\"><svg viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M4 12h16M4 17h16\"/></svg></button></div><div class=\"sr-scrim\" id=\"sr-scrim\" onclick=\"SearchJs.closeNav()\"></div></header>");
            return sb.ToString();
        }

        private static string FootHtml(PublicData d)
        {
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<footer class=\"sr-foot\"><div class=\"sr-wrap sr-foot-grid\"><div class=\"sr-fbrand\"><a class=\"sr-logo\" href=\"Home\">" + Logo(40) + "<span><b>" + Ui.Esc(s.Brand) + "</b><small>Car Rental</small></span></a><p>" + Ui.Esc(s.Tagline) + "</p><p class=\"sr-fcontact\"><a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a><br><a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></p></div>");
            sb.Append("<div><h3>Rent</h3><ul><li><a href=\"Home#find\">Find a car</a></li><li><a href=\"Fleet\">Fleet</a></li><li><a href=\"Manage\">Manage booking</a></li><li><a href=\"Policies\">Rental policies</a></li><li><a href=\"Contact\">Contact</a></li></ul></div>");
            sb.Append("<div><h3>Classes</h3><ul>" + string.Concat(d.Classes.Take(7).Select(k => "<li><a href=\"Vehicle?c=" + Ui.Esc(k.Id) + "\">" + Ui.Esc(k.Name) + "</a></li>")) + "</ul></div>");
            sb.Append("<div><h3>Branches</h3><ul class=\"sr-fbr\">" + string.Concat(d.Branches.Select(b => "<li><a href=\"Branches#b-" + Ui.Esc(b.Id) + "\"><b>" + Ui.Esc(b.Name) + "</b></a><span>" + Ui.Esc(b.Street + ", " + b.City) + "</span><span><a href=\"tel:" + Ui.Esc(b.Phone) + "\">" + Ui.Esc(b.Phone) + "</a></span></li>")) + "</ul></div></div>");
            sb.Append("<div class=\"sr-foot-b\"><div class=\"sr-wrap\"><span>&copy; " + DateTime.Now.Year + " " + Ui.Esc(s.Legal) + ". Prices in US dollars. Taxes added at checkout.</span><a class=\"sr-staff\" href=\"AdminLogin\">Staff sign in</a></div></div></footer>");
            return sb.ToString();
        }

        private static string Specs(ClassRec k)
        {
            return "<ul class=\"sr-specs\"><li title=\"Seats\">" + Icon("people") + k.Seats + "</li><li title=\"Large bags\">" + Icon("bag") + k.Bags + "</li><li title=\"Doors\">" + Icon("door") + k.Doors + "</li><li title=\"Transmission\">" + Icon("gear") + Ui.Esc(k.Transmission) + "</li>" + (k.Fuel != "Gas" ? "<li title=\"Fuel\">" + Icon("fuel") + Ui.Esc(k.Fuel) + "</li>" : string.Empty) + "</ul>";
        }

        private static string ClassCard(PublicData d, ClassRec k, string href, string foot)
        {
            return "<article class=\"sr-ccard\"><a class=\"sr-ccard-img\" href=\"Vehicle?c=" + Ui.Esc(k.Id) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, k.Media, k.Name, false, true) + "</a><div class=\"sr-ccard-b\"><div class=\"sr-ccard-h\"><h3><a href=\"Vehicle?c=" + Ui.Esc(k.Id) + "\">" + Ui.Esc(k.Name) + "</a></h3><small>" + Ui.Esc(k.Example) + " or similar</small></div>" + Specs(k) + (foot.Length > 0 ? foot : "<div class=\"sr-ccard-f\"><span class=\"sr-from\"><small>From</small><b>" + Rental.Money(Math.Min(k.Daily, k.Weekend)) + "</b><small>a day</small></span><a class=\"sr-btn sr-btn-sm\" href=\"" + Ui.Esc(href) + "\">See dates</a></div>") + "</div></article>";
        }

        private static (DateTime S, DateTime E) DefaultTrip()
        {
            DateTime s = DateTime.Today.AddDays(1).AddHours(10);
            return (s, s.AddDays(3));
        }

        private static string SearchBox(PublicData d, string branch, DateTime s, DateTime e, string cls, string title)
        {
            string min = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
            StringBuilder sb = new StringBuilder("<form class=\"sr-search\" id=\"find\" action=\"Search\" method=\"get\" onsubmit=\"return SearchJs.find(this)\">" + (title.Length > 0 ? "<h2 class=\"sr-search-t\">" + Ui.Esc(title) + "</h2>" : string.Empty) + "<div class=\"sr-search-row\">");
            sb.Append("<label class=\"sr-sf sr-sf-b\"><span>" + Icon("pin") + "Pick-up and return at</span><select name=\"b\" aria-label=\"Branch\">" + string.Concat(d.Branches.Select(b => "<option value=\"" + Ui.Esc(b.Id) + "\"" + (b.Id == branch ? " selected" : string.Empty) + ">" + Ui.Esc(b.Name) + "</option>")) + "</select></label>");
            sb.Append("<label class=\"sr-sf\"><span>" + Icon("calendar") + "Pick-up</span><input type=\"datetime-local\" name=\"s\" step=\"1800\" min=\"" + min + "\" value=\"" + Rental.Iso(s) + "\" required></label>");
            sb.Append("<label class=\"sr-sf\"><span>" + Icon("calendar") + "Return</span><input type=\"datetime-local\" name=\"e\" step=\"1800\" min=\"" + min + "\" value=\"" + Rental.Iso(e) + "\" required></label>");
            if (cls.Length > 0)
            {
                sb.Append("<input type=\"hidden\" name=\"c\" value=\"" + Ui.Esc(cls) + "\">");
            }
            sb.Append("<button type=\"submit\" class=\"sr-btn sr-sbtn\">Find a car</button></div><p class=\"sr-serr\" role=\"alert\"></p></form>");
            return sb.ToString();
        }
    }
}
