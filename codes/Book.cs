using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class Book : WebPage
    {
        public override async Task OnInitialized()
        {
            PublicData d = await Store.LoadPublic();
            FleetData f = await Avail.LoadSettled();
            string branch = Ui.Clip(QueryValue("b"), 60);
            string cls = Ui.Clip(QueryValue("c"), 60);
            bool okS = Rental.ParseLocal(QueryValue("s"), out DateTime s);
            bool okE = Rental.ParseLocal(QueryValue("e"), out DateTime e);
            HtmlDoc.SetTitle("Book your car | " + d.Site.Legal);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, follow");
            string err = !okS || !okE ? "Choose the dates first." : Ops.TripError(f, branch, cls, s, e, false);
            if (err.Length == 0 && Avail.Assign(f, branch, cls, s, e, string.Empty).CarId.Length == 0)
            {
                err = "Sorry, this class was just booked for these dates. Please choose another.";
            }
            string main;
            if (err.Length > 0)
            {
                (DateTime s0, DateTime e0) = DefaultTrip();
                main = "<div class=\"bk-wrap bk-ph\"><div class=\"bk-crumbs\"><a href=\"Home\">Home</a> / Book</div><h1>Book a car</h1></div><section class=\"bk-wrap bk-sec\"><p class=\"bk-note\">" + Ui.Esc(err) + "</p><div style=\"margin-top:20px\">" + SearchBox(d, branch, okS ? s : s0, okE ? e : e0, string.Empty, "Search again") + "</div></section>";
            }
            else
            {
                main = FormHtml(d, branch, cls, s, e);
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_head}", HeadHtml(d, string.Empty))
                .Replace("{plhd_main}", "<div id=\"bk-flow\">" + main + "</div>")
                .Replace("{plhd_foot}", FootHtml(d));
        }

        private static string Hid(string k, string v)
        {
            return "<input type=\"hidden\" id=\"bk-f-" + k + "\" data-k=\"" + k + "\" value=\"" + Ui.Esc(v) + "\">";
        }

        private static string In(string k, string label, string type, int max, string auto, string hint = "")
        {
            return "<label class=\"bk-field\" for=\"bk-f-" + k + "\"><span>" + Ui.Esc(label) + "</span><input type=\"" + type + "\" id=\"bk-f-" + k + "\" data-k=\"" + k + "\" maxlength=\"" + max + "\" autocomplete=\"" + auto + "\">" + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + "<em class=\"bk-err\" id=\"bk-e-" + k + "\"></em></label>";
        }

        private static string FormHtml(PublicData d, string branch, string cls, DateTime s, DateTime e)
        {
            BranchRec b = d.Branches.First(x => x.Id == branch);
            ClassRec k = d.Classes.First(x => x.Id == cls);
            string back = "Search?b=" + Uri.EscapeDataString(branch) + "&c=" + Uri.EscapeDataString(cls) + "&s=" + Uri.EscapeDataString(Rental.Iso(s)) + "&e=" + Uri.EscapeDataString(Rental.Iso(e));
            StringBuilder sb = new StringBuilder("<div class=\"bk-wrap bk-ph\"><div class=\"bk-crumbs\"><a href=\"Home\">Home</a> / <a href=\"" + Ui.Esc(back) + "\">Available cars</a> / Book</div><h1>Book your " + Ui.Esc(k.Name) + " rental</h1></div>");
            sb.Append("<form class=\"bk-wrap bk-sec bk-book\" id=\"bk-form\" onsubmit=\"return BookJs.place()\" onchange=\"BookJs.quote()\" novalidate>" + Hid("b", branch) + Hid("c", cls) + Hid("s", Rental.Iso(s)) + Hid("e", Rental.Iso(e)));
            sb.Append("<div class=\"bk-bmain\">");
            sb.Append("<section class=\"bk-panel bk-stack\"><h2>Extras</h2><div class=\"bk-xlist\">");
            foreach (ExtraRec x in d.Extras.Where(x => x.Kind != "cover"))
            {
                sb.Append("<label class=\"bk-xrow\"><span class=\"bk-xname\"><b>" + Ui.Esc(x.Name) + "</b><small>" + Ui.Esc(x.Note) + "</small></span><span class=\"bk-xprice\">" + Rental.Money(x.PerDay) + "<small>a day</small></span><select class=\"bk-xq\" data-x=\"" + Ui.Esc(x.Id) + "\" aria-label=\"" + Ui.Esc(x.Name) + "\">" + string.Concat(Enumerable.Range(0, x.MaxQty + 1).Select(n => "<option value=\"" + n + "\">" + (n == 0 ? "None" : n.ToString(CultureInfo.InvariantCulture)) + "</option>")) + "</select></label>");
            }
            sb.Append("</div><h2 class=\"bk-h2b\">Coverage</h2><div class=\"bk-covers\"><label class=\"bk-cover\"><input type=\"radio\" name=\"bk-cover\" value=\"\" checked><span><b>Basic coverage</b><small>Included. Third-party liability as required by law, with your deposit as the excess for damage to the car.</small></span><em>Included</em></label>");
            foreach (ExtraRec x in d.Extras.Where(x => x.Kind == "cover"))
            {
                sb.Append("<label class=\"bk-cover\"><input type=\"radio\" name=\"bk-cover\" value=\"" + Ui.Esc(x.Id) + "\"><span><b>" + Ui.Esc(x.Name) + "</b><small>" + Ui.Esc(x.Note) + "</small></span><em>" + Rental.Money(x.PerDay) + " a day</em></label>");
            }
            sb.Append("</div><em class=\"bk-err\" id=\"bk-e-extras\"></em></section>");
            sb.Append("<section class=\"bk-panel bk-stack\"><h2>Driver</h2><p class=\"bk-hint\">The main driver must be at least " + d.Site.MinAge + " years old and bring this licence and a payment card in their own name.</p><div class=\"bk-grid2\">" + In("first", "First name", "text", 40, "given-name") + In("last", "Last name", "text", 40, "family-name") + In("email", "Email", "email", 80, "email", "We send the booking code here.") + In("phone", "Mobile phone", "tel", 30, "tel") + In("license", "Driver's licence number", "text", 20, "off") + In("lstate", "Issued by (state or country)", "text", 30, "off") + "<label class=\"bk-field\" for=\"bk-f-birth\"><span>Date of birth</span><input type=\"date\" id=\"bk-f-birth\" data-k=\"birth\" autocomplete=\"bday\"><em class=\"bk-err\" id=\"bk-e-birth\"></em></label></div>");
            sb.Append("<label class=\"bk-check\"><input type=\"checkbox\" id=\"bk-f-agree\" data-k=\"agree\"> <span>I have read the <a href=\"Policies\" target=\"_blank\" rel=\"noopener\">rental policies</a>.</span></label><em class=\"bk-err\" id=\"bk-e-agree\"></em></section>");
            sb.Append("</div><aside class=\"bk-side\"><div class=\"bk-panel bk-stack\"><div class=\"bk-sidecar\">" + Pic(d.Media, k.Media, k.Name, true, true) + "</div><div><h3>" + Ui.Esc(k.Name) + "</h3><small class=\"bk-hint\">" + Ui.Esc(k.Example) + " or similar</small></div>" + Specs(k));
            sb.Append("<dl class=\"bk-tripdl\"><dt>Pick-up</dt><dd>" + Ui.Esc(Rental.When(s)) + "</dd><dt>Return</dt><dd>" + Ui.Esc(Rental.When(e)) + "</dd><dt>Branch</dt><dd>" + Ui.Esc(b.Name) + "<br><small>" + Ui.Esc(b.Street + ", " + b.City) + "</small></dd></dl><a class=\"bk-change\" href=\"" + Ui.Esc(back) + "\">Change the car or dates</a>");
            sb.Append("<div id=\"bk-quote\">" + QuoteHtml(d.Site, k, d.Extras, s, e, new List<BookedExtra>()) + "</div><p class=\"bk-hint\">Nothing is charged now. You pay at pick-up. Free cancellation until " + d.Site.CancelHours + " hours before pick-up.</p><button type=\"submit\" class=\"bk-btn bk-wide\" id=\"bk-go\">Confirm the booking</button><p class=\"bk-serr2\" id=\"bk-e-trip\" role=\"alert\"></p></div></aside></form>");
            return sb.ToString();
        }

        private static string QuoteHtml(SiteData site, ClassRec k, List<ExtraRec> extras, DateTime s, DateTime e, List<BookedExtra> picked)
        {
            Quote q = Rental.Price(site, k, extras, s, e, picked, 0);
            StringBuilder sb = new StringBuilder("<div class=\"bk-sum\"><div><span>" + Ui.Esc(k.Name) + ", " + Ui.Count(q.Days, "day", "days") + "</span><span>" + Rental.Money(q.Rate) + "</span></div>");
            foreach (BookedExtra p in picked)
            {
                ExtraRec? x = extras.FirstOrDefault(y => y.Id == p.Id);
                if (x != null)
                {
                    sb.Append("<div><span>" + Ui.Esc(x.Name + (p.Qty > 1 ? " × " + p.Qty : string.Empty)) + "</span><span>" + Rental.Money(x.PerDay * p.Qty * q.Days) + "</span></div>");
                }
            }
            sb.Append("<div><span>Tax " + site.TaxRate.ToString("0.##", Rental.Us) + "%</span><span>" + Rental.Money(q.Tax) + "</span></div><div class=\"bk-tot\"><span>Total at pick-up</span><span>" + Rental.Money(q.Total) + "</span></div>" + (k.Deposit > 0 ? "<div class=\"bk-dep\"><span>Refundable deposit held on your card</span><span>" + Rental.Money(k.Deposit) + "</span></div>" : string.Empty) + "</div>");
            return sb.ToString();
        }

        private (string Branch, string Cls, DateTime S, DateTime E, List<BookedExtra> Extras) Trip()
        {
            Rental.ParseLocal(GetDataValue("s"), out DateTime s);
            Rental.ParseLocal(GetDataValue("e"), out DateTime e);
            return (Ui.Clip(GetDataValue("b"), 60), Ui.Clip(GetDataValue("c"), 60), s, e, Ops.ParseExtras(GetDataValue("extras")));
        }

        public async Task<ApiResponse> Quote()
        {
            ApiResponse response = new ApiResponse();
            PublicData d = await Store.LoadPublic();
            FleetData f = await Avail.LoadSettled();
            var t = Trip();
            ClassRec? k = d.Classes.FirstOrDefault(x => x.Id == t.Cls);
            string err = k == null ? "That class is not offered." : Ops.TripError(f, t.Branch, t.Cls, t.S, t.E, false);
            if (err.Length == 0)
            {
                err = Ops.ExtrasError(f, t.Branch, t.S, t.E, string.Empty, t.Extras, false);
            }
            response.SetElementContents("bk-e-extras", Ui.Esc(err));
            if (k != null)
            {
                response.SetElementContents("bk-quote", QuoteHtml(d.Site, k, d.Extras, t.S, t.E, err.Length == 0 ? t.Extras : new List<BookedExtra>()));
            }
            return response;
        }

        public async Task<ApiResponse> Place()
        {
            ApiResponse response = new ApiResponse();
            string who = "bk:" + Local.Visitor();
            if (Auth.Paused(who))
            {
                response.SetElementContents("bk-e-trip", "Too many bookings from this connection. Please wait a few minutes or call us.");
                response.ExecuteScript("BookJs.done();");
                return response;
            }
            var t = Trip();
            Driver dr = new Driver { First = Ui.Clip(GetDataValue("first"), 40), Last = Ui.Clip(GetDataValue("last"), 40), Email = Ui.Clip(GetDataValue("email"), 80), Phone = Ui.Clip(GetDataValue("phone"), 30), License = Ui.Clip(GetDataValue("license"), 20).ToUpperInvariant(), State = Ui.Clip(GetDataValue("lstate"), 30), BirthText = Ui.Clip(GetDataValue("birth"), 10) };
            bool agree = GetDataValue("agree") == "1";
            string text;
            string code;
            string copy;
            BookingRec done;
            FleetData snap;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                Dictionary<string, string> e = Ops.DriverErrors(f.Site, dr, t.S == default ? DateTime.Today : t.S, out DateTime birth);
                e["agree"] = agree ? string.Empty : "Please confirm you have read the rental policies.";
                string trip = Ops.TripError(f, t.Branch, t.Cls, t.S, t.E, false);
                e["extras"] = trip.Length == 0 ? Ops.ExtrasError(f, t.Branch, t.S, t.E, string.Empty, t.Extras, false) : string.Empty;
                e["trip"] = trip;
                foreach (KeyValuePair<string, string> kv in e)
                {
                    response.SetElementContents("bk-e-" + kv.Key, Ui.Esc(kv.Value));
                }
                if (e.Values.Any(v => v.Length > 0))
                {
                    response.ExecuteScript("BookJs.done();BookJs.toFirstError();");
                    return response;
                }
                BookingRec draft = new BookingRec { BranchId = t.Branch, ClassId = t.Cls, Start = t.S, End = t.E, Extras = t.Extras, First = dr.First, Last = dr.Last, Email = dr.Email, Phone = dr.Phone, License = dr.License, LicenseState = dr.State, Birth = birth, Source = "web" };
                (BookingRec? placed, string err) = Ops.Place(f, draft, false);
                if (placed == null)
                {
                    response.SetElementContents("bk-e-trip", Ui.Esc(err));
                    response.ExecuteScript("BookJs.done();");
                    return response;
                }
                await Ops.Persist(f, true, true, false);
                done = placed;
                code = placed.Code;
                text = Sheet.ConfirmText(f, placed);
                copy = f.Site.NotifyEmail;
                snap = f;
            }
            Auth.Failed(who);
            await Store.Log("visitor", "web booking", code, true);
            await Sheet.Tell(done.Email, "Your " + snap.Site.Brand + " booking " + code, text);
            if (copy.Length > 0)
            {
                await Sheet.Tell(copy, "New web booking " + code, text);
            }
            response.SetElementContents("bk-flow", Confirmed(snap, done));
            response.ExecuteScript("BookJs.top();");
            return response;
        }

        private static string Confirmed(FleetData f, BookingRec b)
        {
            BranchRec? br = f.Branches.FirstOrDefault(x => x.Id == b.BranchId);
            Quote q = Rental.Of(f.Site, b);
            return "<div class=\"bk-wrap bk-ph\"><h1>You are booked</h1></div><section class=\"bk-wrap bk-sec\"><div class=\"bk-panel bk-stack bk-conf\"><div class=\"bk-code\"><small>Booking code</small><b>" + Ui.Esc(b.Code) + "</b></div><p>Thank you, " + Ui.Esc(b.First) + ". " + (Mailer.Ready ? "We sent the details to " + Ui.Esc(b.Email) + "." : "Keep this code: with your email address it opens your booking on Manage booking.") + "</p><dl class=\"bk-tripdl\"><dt>Car class</dt><dd>" + Ui.Esc(Sheet.ClassName(f, b.ClassId)) + "</dd><dt>Pick-up</dt><dd>" + Ui.Esc(Rental.When(b.Start)) + "</dd><dt>Return</dt><dd>" + Ui.Esc(Rental.When(b.End)) + "</dd><dt>Branch</dt><dd>" + Ui.Esc(br == null ? string.Empty : br.Name + ", " + br.Street + ", " + br.City) + "</dd><dt>Extras</dt><dd>" + Ui.Esc(Sheet.ExtrasText(f, b)) + "</dd><dt>Total at pick-up</dt><dd><b>" + Rental.Money(q.Total) + "</b></dd></dl><p class=\"bk-hint\">Bring the driver's licence and a payment card in the driver's name. Free cancellation until " + f.Site.CancelHours + " hours before pick-up.</p><div class=\"bk-bbtns\"><a class=\"bk-btn\" href=\"Manage?code=" + Ui.Esc(b.Code) + "\">Manage this booking</a><a class=\"bk-btn bk-btn-ghost\" href=\"Home\">Back to home</a></div></div></section>";
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
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"bk-btn" + (main ? string.Empty : " bk-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"bk-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"bk-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"bk-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"bk-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "bk-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"bk-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"bk-wrap bk-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            List<BlockItem> more = b.Items.Where(i => i.Media != string.Empty && !Blocks.IsVideo(i.Media) && media.Any(m => m.Path == i.Media)).ToList();
            string text = "<div class=\"bk-wrap bk-bhero-in\"><div class=\"bk-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"bk-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "bk-blead") + Btns(site, b) + "</div></div>";
            if (mv != string.Empty && more.Count > 0)
            {
                MediaItem? first = media.FirstOrDefault(m => m.Path == b.Media);
                List<(string Html, string Cap)> all = new List<(string, string)> { (mv, first?.Caption ?? string.Empty) };
                foreach (BlockItem it in more)
                {
                    MediaItem? m = media.FirstOrDefault(x => x.Path == it.Media);
                    all.Add(("<img src=\"" + Ui.Esc(it.Media) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : it.Title) + "\" decoding=\"async\">", it.Title));
                }
                StringBuilder sl = new StringBuilder("<div class=\"bk-bhero-p bk-slides\" id=\"bk-slides\" aria-roledescription=\"carousel\" aria-label=\"Pictures\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<figure class=\"bk-slide" + (k == 0 ? " bk-on" : string.Empty) + "\" aria-roledescription=\"slide\" aria-label=\"" + (k + 1) + " of " + all.Count + "\"" + (k == 0 ? string.Empty : " aria-hidden=\"true\"") + ">" + all[k].Html + (all[k].Cap == string.Empty ? string.Empty : "<figcaption>" + Ui.Esc(all[k].Cap) + "</figcaption>") + "</figure>");
                }
                sl.Append("<div class=\"bk-sctl\"><button type=\"button\" class=\"bk-sarr\" aria-label=\"Previous picture\" onclick=\"BookJs.slide(-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><div class=\"bk-sdots\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<button type=\"button\" class=\"bk-sdot" + (k == 0 ? " bk-on" : string.Empty) + "\" aria-label=\"Show picture " + (k + 1) + "\" aria-pressed=\"" + (k == 0 ? "true" : "false") + "\" onclick=\"BookJs.slideTo(" + k + ")\"></button>");
                }
                sl.Append("</div><button type=\"button\" class=\"bk-sarr\" aria-label=\"Next picture\" onclick=\"BookJs.slide(1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div></div>");
                return "<section class=\"bk-bhero bk-bslides\" id=\"" + Ui.Esc(b.Id) + "\">" + sl + text + "</section>";
            }
            return "<section class=\"bk-bhero" + (mv == string.Empty ? " bk-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"bk-bhero-p\">" + mv + "</div>" : string.Empty) + text + "</section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"bk-bsplit" + (b.Flip ? " bk-bflip" : string.Empty) + (b.Media == string.Empty ? " bk-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"bk-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"bk-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"bk-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"bk-bcards bk-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"bk-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                sb.Append(it.Link != string.Empty ? "<a class=\"bk-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"bk-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>" : "<div class=\"bk-bcard\">" + inner + "</div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"bk-bsteps bk-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"bk-bstats bk-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"bk-bpeople bk-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                string[] parts = it.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
                sb.Append("<div class=\"bk-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"bk-bnoface\">" + Ui.Esc(ini) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"bk-bfaq\">" + Head(site, b, false) + "<div class=\"bk-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"bk-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"bk-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"bk-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "bk-blead") + "</div>" + Btns(site, b) + "</div>");
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
                    "text" => Sec(b, "<div class=\"bk-btext\">" + Kick(d.Site, b) + H2(d.Site, b, first ? "h1" : "h2") + Rich(d.Site, b.Text, string.Empty) + "</div>"),
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
            StringBuilder sb = new StringBuilder("<a class=\"bk-skip\" href=\"#bk-main\">Skip to content</a><header class=\"bk-head\" id=\"bk-head\"><div class=\"bk-wrap bk-head-row\"><a class=\"bk-logo\" href=\"Home\" aria-label=\"" + Ui.Esc(d.Site.Legal) + " home\">" + Logo(40) + "<span><b>" + Ui.Esc(d.Site.Brand) + "</b><small>Car Rental</small></span></a>");
            sb.Append("<nav class=\"bk-nav\" id=\"bk-nav\" aria-label=\"Main\"><div class=\"bk-nav-top\"><b>Menu</b><button type=\"button\" class=\"bk-ibtn\" aria-label=\"Close menu\" onclick=\"BookJs.closeNav()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><ul class=\"bk-menu\">");
            foreach ((string page, string name) in NavItems)
            {
                sb.Append("<li><a class=\"bk-ml" + (page == active ? " bk-on" : string.Empty) + "\" href=\"" + page + "\"" + (page == active ? " aria-current=\"page\"" : string.Empty) + ">" + name + "</a></li>");
            }
            sb.Append("<li class=\"bk-mmanage\"><a class=\"bk-ml" + (active == "Manage" ? " bk-on" : string.Empty) + "\" href=\"Manage\">Manage booking</a></li></ul></nav><a class=\"bk-btn bk-btn-ghost bk-btn-sm bk-head-mg\" href=\"Manage\">Manage booking</a><a class=\"bk-btn bk-btn-sm bk-head-cta\" href=\"Home#find\">Book a car</a><button type=\"button\" class=\"bk-burger\" aria-label=\"Open menu\" onclick=\"BookJs.openNav()\"><svg viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M4 12h16M4 17h16\"/></svg></button></div><div class=\"bk-scrim\" id=\"bk-scrim\" onclick=\"BookJs.closeNav()\"></div></header>");
            return sb.ToString();
        }

        private static string FootHtml(PublicData d)
        {
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<footer class=\"bk-foot\"><div class=\"bk-wrap bk-foot-grid\"><div class=\"bk-fbrand\"><a class=\"bk-logo\" href=\"Home\">" + Logo(40) + "<span><b>" + Ui.Esc(s.Brand) + "</b><small>Car Rental</small></span></a><p>" + Ui.Esc(s.Tagline) + "</p><p class=\"bk-fcontact\"><a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a><br><a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></p></div>");
            sb.Append("<div><h3>Rent</h3><ul><li><a href=\"Home#find\">Find a car</a></li><li><a href=\"Fleet\">Fleet</a></li><li><a href=\"Manage\">Manage booking</a></li><li><a href=\"Policies\">Rental policies</a></li><li><a href=\"Contact\">Contact</a></li></ul></div>");
            sb.Append("<div><h3>Classes</h3><ul>" + string.Concat(d.Classes.Take(7).Select(k => "<li><a href=\"Vehicle?c=" + Ui.Esc(k.Id) + "\">" + Ui.Esc(k.Name) + "</a></li>")) + "</ul></div>");
            sb.Append("<div><h3>Branches</h3><ul class=\"bk-fbr\">" + string.Concat(d.Branches.Select(b => "<li><a href=\"Branches#b-" + Ui.Esc(b.Id) + "\"><b>" + Ui.Esc(b.Name) + "</b></a><span>" + Ui.Esc(b.Street + ", " + b.City) + "</span><span><a href=\"tel:" + Ui.Esc(b.Phone) + "\">" + Ui.Esc(b.Phone) + "</a></span></li>")) + "</ul></div></div>");
            sb.Append("<div class=\"bk-foot-b\"><div class=\"bk-wrap\"><span>&copy; " + DateTime.Now.Year + " " + Ui.Esc(s.Legal) + ". Prices in US dollars. Taxes added at checkout.</span><a class=\"bk-staff\" href=\"AdminLogin\">Staff sign in</a></div></div></footer>");
            return sb.ToString();
        }

        private static string Specs(ClassRec k)
        {
            return "<ul class=\"bk-specs\"><li title=\"Seats\">" + Icon("people") + k.Seats + "</li><li title=\"Large bags\">" + Icon("bag") + k.Bags + "</li><li title=\"Doors\">" + Icon("door") + k.Doors + "</li><li title=\"Transmission\">" + Icon("gear") + Ui.Esc(k.Transmission) + "</li>" + (k.Fuel != "Gas" ? "<li title=\"Fuel\">" + Icon("fuel") + Ui.Esc(k.Fuel) + "</li>" : string.Empty) + "</ul>";
        }

        private static string ClassCard(PublicData d, ClassRec k, string href, string foot)
        {
            return "<article class=\"bk-ccard\"><a class=\"bk-ccard-img\" href=\"Vehicle?c=" + Ui.Esc(k.Id) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, k.Media, k.Name, false, true) + "</a><div class=\"bk-ccard-b\"><div class=\"bk-ccard-h\"><h3><a href=\"Vehicle?c=" + Ui.Esc(k.Id) + "\">" + Ui.Esc(k.Name) + "</a></h3><small>" + Ui.Esc(k.Example) + " or similar</small></div>" + Specs(k) + (foot.Length > 0 ? foot : "<div class=\"bk-ccard-f\"><span class=\"bk-from\"><small>From</small><b>" + Rental.Money(Math.Min(k.Daily, k.Weekend)) + "</b><small>a day</small></span><a class=\"bk-btn bk-btn-sm\" href=\"" + Ui.Esc(href) + "\">See dates</a></div>") + "</div></article>";
        }

        private static (DateTime S, DateTime E) DefaultTrip()
        {
            DateTime s = DateTime.Today.AddDays(1).AddHours(10);
            return (s, s.AddDays(3));
        }

        private static string SearchBox(PublicData d, string branch, DateTime s, DateTime e, string cls, string title)
        {
            string min = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
            StringBuilder sb = new StringBuilder("<form class=\"bk-search\" id=\"find\" action=\"Search\" method=\"get\" onsubmit=\"return BookJs.find(this)\">" + (title.Length > 0 ? "<h2 class=\"bk-search-t\">" + Ui.Esc(title) + "</h2>" : string.Empty) + "<div class=\"bk-search-row\">");
            sb.Append("<label class=\"bk-sf bk-sf-b\"><span>" + Icon("pin") + "Pick-up and return at</span><select name=\"b\" aria-label=\"Branch\">" + string.Concat(d.Branches.Select(b => "<option value=\"" + Ui.Esc(b.Id) + "\"" + (b.Id == branch ? " selected" : string.Empty) + ">" + Ui.Esc(b.Name) + "</option>")) + "</select></label>");
            sb.Append("<label class=\"bk-sf\"><span>" + Icon("calendar") + "Pick-up</span><input type=\"datetime-local\" name=\"s\" step=\"1800\" min=\"" + min + "\" value=\"" + Rental.Iso(s) + "\" required></label>");
            sb.Append("<label class=\"bk-sf\"><span>" + Icon("calendar") + "Return</span><input type=\"datetime-local\" name=\"e\" step=\"1800\" min=\"" + min + "\" value=\"" + Rental.Iso(e) + "\" required></label>");
            if (cls.Length > 0)
            {
                sb.Append("<input type=\"hidden\" name=\"c\" value=\"" + Ui.Esc(cls) + "\">");
            }
            sb.Append("<button type=\"submit\" class=\"bk-btn bk-sbtn\">Find a car</button></div><p class=\"bk-serr\" role=\"alert\"></p></form>");
            return sb.ToString();
        }
    }
}
