using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminDesk : WebPage
    {
        private const string AreaKey = "desk";
        private static readonly string[] Methods = { "Card", "Cash", "Other" };
        private static readonly string[] OutFrom = { "Available", "Reserved", "Cleaning" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Desk | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminDeskJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminDeskJs.denied();");
                return null;
            }
            return c;
        }

        private static bool Can(Ctx c, char letter)
        {
            return Access.Can(c.U, AreaKey, letter);
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("dk-brand", Ui.Esc(site.Brand));
            response.SetElementContents("dk-menu", await Auth.Menu("dk", "AdminDesk", c));
            response.SetElementContents("dk-who", Auth.Who("dk", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminDeskJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string Term(string? v)
        {
            return Ui.Clip(v, 40);
        }

        private static int PageNo(string? v)
        {
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        private static string Pager(int page, int pages, int count, string one, string many)
        {
            return "<div class=\"dk-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"dk-btn dk-btn-ghost dk-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminDeskJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"dk-btn dk-btn-ghost dk-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminDeskJs.go(" + (page + 1) + ")\">Next</button></div></div>";
        }

        private static string Opt(string value, string label, string current)
        {
            return "<option value=\"" + Ui.Esc(value) + "\"" + (value == current ? " selected" : string.Empty) + ">" + Ui.Esc(label) + "</option>";
        }

        private static string Pill(string status)
        {
            string k = status switch
            {
                "Available" or "Arrived" or "Returned" or "Active" or "Done" or "Closed" or "Recovered" => "go",
                "Reserved" or "Planned" or "Picked up" or "Extended" or "On rent" or "In progress" or "In shop" => "new",
                "Cleaning" or "In transit" or "In transfer" or "Requested" or "Late pick-up" or "Must set password" or "Open" => "wait",
                "Maintenance" or "Repair" or "Hold" or "No-show" or "Overdue" or "Stolen" or "Needs a car" => "bad",
                _ => "off"
            };
            return "<span class=\"dk-pill dk-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"dk-err\" id=\"dk-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"dk-field\" for=\"dk-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"dk-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
        }

        private static string Num(decimal v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static decimal Dec(string? v)
        {
            return decimal.TryParse((v ?? string.Empty).Trim().TrimStart('$'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? Math.Round(d, 2) : -1;
        }

        private static int Int(string? v, int fallback)
        {
            return int.TryParse((v ?? string.Empty).Trim().Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;
        }

        private static string PickItem(MediaItem m)
        {
            string thumb = m.Thumb.Length > 0 ? m.Thumb : m.Path;
            return "<button type=\"button\" class=\"dk-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(thumb) + "\" onclick=\"AdminDeskJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\"><img src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\"><span>" + Ui.Esc(m.Name) + "</span></button>";
        }

        private static string PickGrid(List<MediaItem> media, string term, int page)
        {
            List<MediaItem> rows = media.Where(m => m.Kind == "photo" && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"dk-none\">Nothing matches. Upload a photo above, or add photos in the Media library.</p>";
            }
            int pages = (rows.Count + PickSize - 1) / PickSize;
            page = Math.Min(Math.Max(page, 1), pages);
            string pager = pages <= 1 ? string.Empty : "<div class=\"dk-pkpager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(rows.Count, "photo", "photos") + "</span><div><button type=\"button\" class=\"dk-btn dk-btn-ghost dk-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminDeskJs.pkList(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"dk-btn dk-btn-ghost dk-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminDeskJs.pkList(" + (page + 1) + ")\">Next</button></div></div>";
            return string.Concat(rows.Skip((page - 1) * PickSize).Take(PickSize).Select(PickItem)) + pager;
        }

        private const int PickSize = 12;

        private static int PickPage(string? v)
        {
            return int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("dk-pkgrid", PickGrid(media, Ui.Clip(GetDataValue("pterm"), 40), PickPage(GetDataValue("ppg"))));
            return response;
        }

        public async Task<ApiResponse> MediaUp()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E') && !Can(c, 'A'))
            {
                return await Refuse(response, c, "upload media", AreaKey, "You can view this area but not change it.");
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.ExecuteScript("AdminDeskJs.pkUploaded(false,'Choose a photo first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files.Take(1).ToList(), site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("dk-pkgrid", PickGrid(media, string.Empty, 1));
            response.ExecuteScript("AdminDeskJs.pkUploaded(" + (added > 0 ? "true" : "false") + ",'" + Ui.Js(((added > 0 ? "Photo added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
            return response;
        }

        private static DateTime DayOf(string? v)
        {
            return Rental.ParseDate(v, out DateTime d) ? d : DateTime.Today;
        }

        private static List<BookingRec> Rows(FleetData f, string branch, DateTime day, string tab)
        {
            DateTime now = DateTime.Now;
            bool today = day == DateTime.Today;
            IEnumerable<BookingRec> q = f.Bookings.Where(b => b.BranchId == branch);
            return tab switch
            {
                "in" => q.Where(b => Avail.Out(b) && (b.End.Date == day || (today && b.End < now))).OrderBy(b => b.End).ToList(),
                "rent" => q.Where(Avail.Out).OrderBy(b => b.End).ToList(),
                "ask" => q.Where(b => Avail.Live(b) && b.AskEnd != null).OrderBy(b => b.End).ToList(),
                _ => q.Where(b => b.Status == "Reserved" && (b.Start.Date == day || (today && b.Start < now.Date))).OrderBy(b => b.Start).ToList()
            };
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            FleetData f = await Avail.LoadSettled();
            await Shell(response, c, f.Site);
            List<BranchRec> mine = f.Branches.Where(b => Access.BranchOk(c.U, b.Id) && b.Open).ToList();
            if (mine.Count == 0)
            {
                response.SetElementContents("dk-main", "<div class=\"dk-ph\"><div><h1>Desk</h1></div></div><div class=\"dk-panel dk-empty\">You are not linked to an open branch. Ask a manager to add your branch on Staff.</div>");
                return response;
            }
            string branch = mine.Any(b => b.Id == GetDataValue("b")) ? GetDataValue("b")! : mine[0].Id;
            StringBuilder sb = new StringBuilder("<div class=\"dk-ph\"><div><h1>Desk</h1><p>Hand cars over and take them back. Check-out records the car, mileage, fuel and condition; check-in compares them and works out the charges.</p></div>" + (Access.Can(c.U, "bookings", 'A') ? "<a class=\"dk-btn\" href=\"AdminBookings?new=1\">New walk-in booking</a>" : string.Empty) + "</div>");
            sb.Append("<div class=\"dk-tools\"><select id=\"dk-b\" onchange=\"AdminDeskJs.list(true)\" aria-label=\"Branch\">" + string.Concat(mine.Select(b => Opt(b.Id, b.Name, branch))) + "</select><input type=\"date\" id=\"dk-d\" value=\"" + DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\" onchange=\"AdminDeskJs.list(true)\" aria-label=\"Day\"><button type=\"button\" class=\"dk-btn dk-btn-ghost dk-btn-sm\" onclick=\"AdminDeskJs.today()\">Today</button><input type=\"search\" id=\"dk-q\" placeholder=\"Code, name or phone\" maxlength=\"40\" oninput=\"AdminDeskJs.type()\" aria-label=\"Search\"></div>");
            sb.Append("<div id=\"dk-list\">" + ListHtml(c, f, branch, DateTime.Today, "out", string.Empty, 1) + "</div>");
            response.SetElementContents("dk-main", sb.ToString());
            return response;
        }

        private static string ListHtml(Ctx c, FleetData f, string branch, DateTime day, string tab, string term, int page)
        {
            DateTime now = DateTime.Now;
            string[] tabs = { "out", "in", "rent", "ask" };
            string[] names = { "Pick-ups", "Returns", "On rent", "Extension requests" };
            StringBuilder sb = new StringBuilder("<div class=\"dk-tabs\" role=\"tablist\">");
            for (int i = 0; i < tabs.Length; i++)
            {
                int n = Rows(f, branch, day, tabs[i]).Count;
                sb.Append("<button type=\"button\" class=\"dk-tab" + (tabs[i] == tab ? " dk-act" : string.Empty) + "\" onclick=\"AdminDeskJs.tab('" + tabs[i] + "')\">" + names[i] + "<i>" + n + "</i></button>");
            }
            sb.Append("</div>");
            string digits = Digits(term);
            List<BookingRec> rows = Rows(f, branch, day, tab).Where(b => term.Length == 0 || Ui.Has(b.Code, term) || Ui.Has(Rental.Name(b), term) || Ui.Has(b.Phone, term) || Ui.Has(b.Email, term) || (digits.Length >= 3 && Digits(b.Phone).Contains(digits, StringComparison.Ordinal))).ToList();
            if (rows.Count == 0)
            {
                string none = tab switch
                {
                    "in" => "No returns due " + (day == DateTime.Today ? "today" : "on " + Rental.Day(day)) + ".",
                    "rent" => "No cars out on rent from this branch.",
                    "ask" => "No extension requests waiting.",
                    _ => "No pick-ups " + (day == DateTime.Today ? "today" : "on " + Rental.Day(day)) + "."
                };
                return sb + "<div class=\"dk-panel dk-empty\">" + none + "</div>";
            }
            bool edit = Can(c, 'E');
            sb.Append("<div class=\"dk-wrapt\"><table class=\"dk-table\"><thead><tr><th>Booking</th><th>Class and car</th><th>" + (tab == "out" ? "Pick-up" : "Due back") + "</th><th>Status</th><th></th></tr></thead><tbody>");
            int pages = Math.Max(1, (rows.Count + DeskSize - 1) / DeskSize);
            page = Math.Min(Math.Max(page, 1), pages);
            foreach (BookingRec b in rows.Skip((page - 1) * DeskSize).Take(DeskSize))
            {
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == b.CarId);
                string id = Ui.Js(b.Id);
                bool late = Avail.Overdue(b, now);
                string status = late ? "Overdue" : b.Status == "Reserved" && b.Start < now ? "Late pick-up" : b.Status;
                StringBuilder acts = new StringBuilder();
                if (edit && b.Status == "Reserved")
                {
                    if (now >= b.Start.AddHours(-12) && now < b.End)
                    {
                        acts.Append("<button type=\"button\" class=\"dk-btn dk-btn-sm\" onclick=\"AdminDeskJs.outForm('" + id + "')\">Check out</button>");
                    }
                    if (now > b.Start)
                    {
                        acts.Append("<button type=\"button\" class=\"dk-btn dk-btn-ghost dk-btn-sm\" onclick=\"AdminDeskJs.noShow('" + id + "','" + Ui.Js(b.Code) + "')\">No-show</button>");
                    }
                }
                if (edit && Avail.Out(b))
                {
                    acts.Append("<button type=\"button\" class=\"dk-btn dk-btn-sm\" onclick=\"AdminDeskJs.inForm('" + id + "')\">Check in</button>");
                }
                if (edit && Avail.Live(b))
                {
                    acts.Append("<button type=\"button\" class=\"dk-btn dk-btn-ghost dk-btn-sm\" onclick=\"AdminDeskJs.extForm('" + id + "')\">" + (b.AskEnd != null ? "Review request" : "Extend") + "</button>");
                }
                sb.Append("<tr><td class=\"dk-first\"><b>" + Ui.Esc(b.Code) + "</b><small>" + Ui.Esc(Rental.Name(b) + " · " + b.Phone) + "</small></td><td data-label=\"Class and car\">" + Ui.Esc(Sheet.ClassName(f, b.ClassId)) + "<small>" + Ui.Esc(car == null ? "No car yet" : car.Plate + " · " + car.Make + " " + car.Model) + "</small></td><td data-label=\"" + (tab == "out" ? "Pick-up" : "Due back") + "\">" + Ui.Esc(Rental.When(tab == "out" ? b.Start : b.End)) + (b.AskEnd != null ? "<small>Asks for " + Ui.Esc(Rental.When(b.AskEnd.Value)) + "</small>" : string.Empty) + "</td><td data-label=\"Status\">" + Pill(status) + "</td><td data-label=\"\"><div class=\"dk-acts-row\">" + acts + "</div></td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "booking", "bookings"));
            return sb.ToString();
        }

        private const int DeskSize = 10;

        private static string Digits(string? v)
        {
            return new string((v ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        private async Task<string> ListNow(Ctx c)
        {
            FleetData f = await Avail.LoadSettled();
            string branch = Ui.Clip(GetDataValue("b"), 60);
            if (!Access.BranchOk(c.U, branch))
            {
                return "<div class=\"dk-panel dk-empty\">Choose one of your branches.</div>";
            }
            string tab = Ui.Clip(GetDataValue("t"), 6);
            return ListHtml(c, f, branch, DayOf(GetDataValue("d")), tab.Length == 0 ? "out" : tab, Term(GetDataValue("term")), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("dk-list", await ListNow(c));
            return response;
        }

        private async Task<(Ctx? C, FleetData F, BookingRec? B)> Open(ApiResponse response, string action)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return (null, new FleetData(), null);
            }
            if (!Can(c, 'E'))
            {
                await Refuse(response, c, action, string.Empty, "You can view the desk but not hand over or take back cars.");
                return (null, new FleetData(), null);
            }
            FleetData f = await Avail.LoadSettled();
            BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("id"), 60));
            if (b == null || !Access.BranchOk(c.U, b.BranchId))
            {
                await Refuse(response, c, action, string.Empty, "That booking was not found at your branches.");
                return (null, f, null);
            }
            return (c, f, b);
        }

        private static string PanelInputs(InspectionRec? before)
        {
            StringBuilder sb = new StringBuilder("<div class=\"dk-panels\">");
            foreach (string panel in Rental.Panels)
            {
                string v = before != null && before.Panels.TryGetValue(panel, out string? x) ? x : "OK";
                sb.Append("<label class=\"dk-field\"><span>" + Ui.Esc(panel) + "</span><select class=\"dk-pnl\" data-p=\"" + Ui.Esc(panel) + "\">" + string.Concat(Rental.Marks.Select(mk => Opt(mk, mk, v))) + "</select></label>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string FuelSel(int v)
        {
            return "<select id=\"dk-f-fuel\" data-k=\"fuel\">" + string.Concat(Enumerable.Range(0, 9).Reverse().Select(n => Opt(n.ToString(CultureInfo.InvariantCulture), Rental.FuelText(n), v.ToString(CultureInfo.InvariantCulture)))) + "</select>";
        }

        private static string PhotoBox()
        {
            return "<div class=\"dk-field\"><span>Photos (up to 8, from the media library or upload)</span><div class=\"dk-photos\" id=\"dk-photos\"></div><div><button type=\"button\" class=\"dk-btn dk-btn-ghost dk-btn-sm\" onclick=\"AdminDeskJs.addPhoto()\">Add a photo</button></div>" + Err("photos") + "</div>";
        }

        private static string PayBox(decimal due)
        {
            return "<div class=\"dk-grid2\">" + Field("paid", "Payment from the customer now (US dollars)", Text("paid", Num(Math.Max(0, due)), 10, "text", " inputmode=\"decimal\"")) + Field("method", "Paid by", "<select id=\"dk-f-method\" data-k=\"method\">" + string.Concat(Methods.Select(m => Opt(m, m, "Card"))) + "</select>") + "</div>";
        }

        public async Task<ApiResponse> OutForm()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData f, BookingRec? b) = await Open(response, "check out");
            if (c == null || b == null)
            {
                return response;
            }
            if (b.Status != "Reserved")
            {
                return await Refuse(response, c, "check out", b.Code, b.Code + " is " + b.Status.ToLowerInvariant() + ".");
            }
            DateTime now = DateTime.Now;
            List<CarRec> cars = f.Cars.Where(x => x.ClassId == b.ClassId && x.BranchId == b.BranchId && OutFrom.Contains(x.Status) && Avail.CarFree(f, x, b.BranchId, now < b.Start ? now : b.Start, b.End, b.Id)).OrderByDescending(x => x.Id == b.CarId).ThenBy(x => x.Status == "Cleaning").ThenBy(x => x.Plate).ToList();
            Quote q = Rental.Of(f.Site, b);
            StringBuilder sb = new StringBuilder("<form class=\"dk-stack\" id=\"dk-form\" onsubmit=\"return AdminDeskJs.outSave()\" novalidate><input type=\"hidden\" id=\"dk-f-id\" value=\"" + Ui.Esc(b.Id) + "\">");
            sb.Append("<div class=\"dk-two\"><div>" + Sheet.Summary("dk", f, b) + "</div><div>" + Sheet.Money("dk", f, b, null) + "</div></div>");
            if (cars.Count == 0)
            {
                sb.Append("<p class=\"dk-note\">No " + Ui.Esc(Sheet.ClassName(f, b.ClassId)) + " car is free at this branch until " + Ui.Esc(Rental.When(b.End)) + ". Check the fleet, or change the booking.</p>");
            }
            sb.Append("<p class=\"dk-sec\">Car</p>" + Field("car", "Car handed over", "<select id=\"dk-f-car\" data-k=\"car\" onchange=\"AdminDeskJs.carPick()\">" + string.Concat(cars.Select(x => "<option value=\"" + Ui.Esc(x.Id) + "\" data-m=\"" + x.Mileage + "\" data-f=\"" + x.Fuel + "\">" + Ui.Esc(x.Plate + " · " + x.Make + " " + x.Model + " · " + x.Color + (x.Id == b.CarId ? " (assigned)" : string.Empty) + (x.Status == "Cleaning" ? " (in cleaning)" : string.Empty)) + "</option>")) + "</select>"));
            CarRec? first = cars.FirstOrDefault();
            sb.Append("<p class=\"dk-sec\">Inspection before the rental</p><div class=\"dk-grid2\">" + Field("mileage", "Mileage", Text("mileage", (first?.Mileage ?? 0).ToString(CultureInfo.InvariantCulture), 7, "number", " min=\"0\"")) + Field("fuel", "Fuel", FuelSel(first?.Fuel ?? 8)) + "</div>");
            sb.Append(PanelInputs(null));
            sb.Append("<label class=\"dk-field\" for=\"dk-f-notes\"><span>Notes</span><textarea id=\"dk-f-notes\" data-k=\"notes\" rows=\"2\" maxlength=\"500\"></textarea>" + Err("notes") + "</label>" + PhotoBox());
            sb.Append("<p class=\"dk-sec\">Payment</p>" + PayBox(Bill.Of(f, b, null).DueCustomer));
            sb.Append("<label class=\"dk-check\"><input type=\"checkbox\" id=\"dk-f-lic\" data-k=\"lic\"> I checked the driver's licence and it matches " + Ui.Esc(b.License) + "</label>" + Err("lic"));
            sb.Append("<div class=\"dk-acts\"><button type=\"submit\" class=\"dk-btn\"" + (cars.Count == 0 ? " disabled" : string.Empty) + ">Hand over the car</button><button type=\"button\" class=\"dk-btn dk-btn-ghost\" onclick=\"AdminDeskJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("dk-mbody", sb.ToString());
            response.ExecuteScript("AdminDeskJs.opened('" + Ui.Js("Check out " + b.Code) + "');AdminDeskJs.photosReset();");
            return response;
        }

        private (Dictionary<string, string> Panels, List<string> Photos, string Error) ReadInspection(List<MediaItem> library)
        {
            Dictionary<string, string> panels = new Dictionary<string, string>();
            foreach (string part in (GetDataValue("panels") ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split('=');
                if (kv.Length == 2 && Rental.Panels.Contains(kv[0]) && Rental.Marks.Contains(kv[1]))
                {
                    panels[kv[0]] = kv[1];
                }
            }
            foreach (string p in Rental.Panels.Where(p => !panels.ContainsKey(p)))
            {
                panels[p] = "OK";
            }
            List<string> photos = (GetDataValue("photos") ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Distinct().ToList();
            string err = photos.Count > 8 ? "Up to 8 photos." : photos.Any(x => !library.Any(m => m.Path == x && m.Kind == "photo")) ? "A photo is no longer in the media library. Remove it and add it again." : string.Empty;
            return (panels, photos, err);
        }

        public async Task<ApiResponse> OutSave()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData _, BookingRec? b0) = await Open(response, "check out");
            if (c == null || b0 == null)
            {
                return response;
            }
            string carId = Ui.Clip(GetDataValue("car"), 60);
            int mileage = Int(GetDataValue("mileage"), -1);
            int fuel = Int(GetDataValue("fuel"), -1);
            string notes = Ui.Clip((GetDataValue("notes") ?? string.Empty).Replace("\r", string.Empty), 500);
            decimal paid = Dec(GetDataValue("paid"));
            string method = Methods.Contains(GetDataValue("method")) ? GetDataValue("method")! : "Card";
            bool lic = GetDataValue("lic") == "1";
            List<MediaItem> library = await Media.Load();
            (Dictionary<string, string> panels, List<string> photos, string ePhotos) = ReadInspection(library);
            string code;
            string plate;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == b0.Id);
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == carId);
                DateTime now = DateTime.Now;
                if (b == null || b.Status != "Reserved")
                {
                    return await Refuse(response, c, "check out", b0.Code, "This booking was changed meanwhile. Close and open it again.");
                }
                if (now < b.Start.AddHours(-12) || now >= b.End)
                {
                    return await Refuse(response, c, "check out", b.Code, "A car can be handed over from 12 hours before pick-up until the return time.");
                }
                Quote q = Rental.Of(f.Site, b);
                Dictionary<string, string> e = new Dictionary<string, string>
                {
                    ["car"] = car == null || car.ClassId != b.ClassId || car.BranchId != b.BranchId || !OutFrom.Contains(car.Status) || !Avail.CarFree(f, car, b.BranchId, now < b.Start ? now : b.Start, b.End, b.Id) ? "That car is no longer free. Choose another." : string.Empty,
                    ["mileage"] = car == null ? string.Empty : mileage < car.Mileage ? "Mileage cannot be lower than the car's last reading (" + car.Mileage.ToString("N0", Rental.Us) + ")." : mileage > car.Mileage + 2000 ? "That is far above the last reading. Please check it." : string.Empty,
                    ["fuel"] = fuel < 0 || fuel > 8 ? "Choose the fuel level." : string.Empty,
                    ["notes"] = string.Empty,
                    ["photos"] = ePhotos,
                    ["paid"] = paid < 0 || paid > q.Total + 10000 ? "Enter the amount taken now, or 0." : string.Empty,
                    ["lic"] = !lic ? "Check the licence before handing over the car." : string.Empty
                };
                foreach (KeyValuePair<string, string> kv in e)
                {
                    response.SetElementContents("dk-e-" + kv.Key, kv.Value);
                }
                if (e.Values.Any(v => v.Length > 0))
                {
                    return response;
                }
                CarRec t = car!;
                b.CarId = t.Id;
                b.Status = "Picked up";
                b.PickedUpAt = now;
                if (paid > 0)
                {
                    Bill.Add(f, b, "Customer", method, paid, now, string.Empty, "At check-out", c.U.Id);
                }
                t.Status = "On rent";
                t.StatusSince = now;
                t.StatusNote = string.Empty;
                t.Mileage = mileage;
                t.Fuel = fuel;
                List<InspectionRec> ins = await Store.Rows<InspectionRec>("inspections");
                ins.RemoveAll(x => x.BookingId == b.Id && x.Kind == "out");
                ins.Add(new InspectionRec { Id = Rental.NewId("i-"), BookingId = b.Id, CarId = t.Id, Kind = "out", When = now, By = c.U.Id, Mileage = mileage, Fuel = fuel, Panels = panels, Notes = notes, Photos = photos });
                Avail.Settle(f);
                await Ops.PersistAll(f);
                await Store.SaveRows("inspections", ins);
                code = b.Code;
                plate = t.Plate;
            }
            await Store.Log(c.U.Id, "check out", code + " · " + plate + (paid > 0 ? " · " + Rental.Money(paid) + " " + method : string.Empty), true);
            response.SetElementContents("dk-list", await ListNow(c));
            response.ExecuteScript("AdminDeskJs.clean();AdminDeskJs.shut();AdminDeskJs.toast('" + Ui.Js(plate + " handed over for " + code + ".") + "');");
            return response;
        }

        public async Task<ApiResponse> InForm()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData f, BookingRec? b) = await Open(response, "check in");
            if (c == null || b == null)
            {
                return response;
            }
            if (!Avail.Out(b))
            {
                return await Refuse(response, c, "check in", b.Code, b.Code + " is not out on rent.");
            }
            List<InspectionRec> ins = await Store.Rows<InspectionRec>("inspections");
            InspectionRec? o = ins.FirstOrDefault(x => x.BookingId == b.Id && x.Kind == "out");
            CarRec? car = f.Cars.FirstOrDefault(x => x.Id == b.CarId);
            List<MediaItem> media = await Media.Load();
            DateTime now = DateTime.Now;
            StringBuilder sb = new StringBuilder("<form class=\"dk-stack\" id=\"dk-form\" onsubmit=\"return AdminDeskJs.inSave()\" novalidate><input type=\"hidden\" id=\"dk-f-id\" value=\"" + Ui.Esc(b.Id) + "\">");
            sb.Append("<div class=\"dk-two\"><div>" + Sheet.Summary("dk", f, b) + "</div><div id=\"dk-sum\">" + Sheet.Money("dk", f, b, null) + "</div></div>");
            sb.Append("<p class=\"dk-sec\">At check-out</p>" + Sheet.Compare("dk", o, null, media));
            sb.Append("<p class=\"dk-sec\">Inspection at return</p><div class=\"dk-grid3\">" + Field("when", "Returned at", "<input type=\"datetime-local\" id=\"dk-f-when\" data-k=\"when\" value=\"" + Rental.Iso(new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0)) + "\">") + Field("mileage", "Mileage", Text("mileage", (car?.Mileage ?? o?.Mileage ?? 0).ToString(CultureInfo.InvariantCulture), 7, "number", " min=\"0\"")) + Field("fuel", "Fuel", FuelSel(o?.Fuel ?? 8)) + "</div>");
            sb.Append(PanelInputs(o));
            sb.Append("<label class=\"dk-field\" for=\"dk-f-notes\"><span>Notes</span><textarea id=\"dk-f-notes\" data-k=\"notes\" rows=\"2\" maxlength=\"500\"></textarea>" + Err("notes") + "</label>" + PhotoBox());
            sb.Append("<p class=\"dk-sec\">Charges</p><label class=\"dk-check\"><input type=\"checkbox\" id=\"dk-f-clean\" data-k=\"clean\"> Needs extra cleaning (" + Rental.Money(f.Site.CleaningFee) + ")</label>");
            sb.Append("<div class=\"dk-grid2\">" + Field("damage", "Damage charge (US dollars, 0 if none)", Text("damage", "0", 10, "text", " inputmode=\"decimal\"")) + Field("dnote", "What was damaged", Text("dnote", string.Empty, 120)) + "</div>");
            sb.Append("<label class=\"dk-check\"><input type=\"checkbox\" id=\"dk-f-repair\" data-k=\"repair\"> Send the car to repair instead of cleaning (always, when there is a damage charge). A repair record is opened.</label>");
            sb.Append("<div><button type=\"button\" class=\"dk-btn dk-btn-ghost\" onclick=\"AdminDeskJs.inPreview()\">Work out the charges</button></div>");
            sb.Append("<p class=\"dk-sec\">Payment</p>" + PayBox(Bill.Of(f, b, null).DueCustomer));
            sb.Append("<div class=\"dk-acts\"><button type=\"submit\" class=\"dk-btn\">Take the car back</button><button type=\"button\" class=\"dk-btn dk-btn-ghost\" onclick=\"AdminDeskJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("dk-mbody", sb.ToString());
            response.ExecuteScript("AdminDeskJs.opened('" + Ui.Js("Check in " + b.Code + (car != null ? " · " + car.Plate : string.Empty)) + "');AdminDeskJs.photosReset();AdminDeskJs.inPreview();");
            return response;
        }

        private (Dictionary<string, string> Errors, DateTime When, int Mileage, int Fuel, decimal Damage, string DNote, bool Clean, bool Repair) ReadReturn(InspectionRec? o, BookingRec b)
        {
            bool okWhen = Rental.ParseLocal(GetDataValue("when"), out DateTime when);
            int mileage = Int(GetDataValue("mileage"), -1);
            int fuel = Int(GetDataValue("fuel"), -1);
            decimal damage = Dec(GetDataValue("damage"));
            string dnote = Ui.Clip(GetDataValue("dnote"), 120);
            bool clean = GetDataValue("clean") == "1";
            bool repair = GetDataValue("repair") == "1";
            int outMiles = o?.Mileage ?? 0;
            DateTime from = b.PickedUpAt ?? b.Start;
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["when"] = !okWhen ? "Enter when the car came back." : when < from ? "The return cannot be before the pick-up (" + Rental.When(from) + ")." : when > DateTime.Now.AddMinutes(10) ? "The return cannot be in the future." : string.Empty,
                ["mileage"] = mileage < outMiles ? "Mileage cannot be lower than at check-out (" + outMiles.ToString("N0", Rental.Us) + ")." : mileage > outMiles + 30000 ? "That is far above the check-out reading. Please check it." : string.Empty,
                ["fuel"] = fuel < 0 || fuel > 8 ? "Choose the fuel level." : string.Empty,
                ["damage"] = damage < 0 || damage > 50000 ? "Enter the damage charge, or 0." : string.Empty,
                ["dnote"] = damage > 0 && dnote.Length < 3 ? "Say what was damaged." : string.Empty
            };
            return (e, when, mileage, fuel, Math.Max(0, damage), dnote, clean, repair);
        }

        public async Task<ApiResponse> InPreview()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData f, BookingRec? b) = await Open(response, "check in");
            if (c == null || b == null || !Avail.Out(b))
            {
                return response;
            }
            List<InspectionRec> ins = await Store.Rows<InspectionRec>("inspections");
            InspectionRec o = ins.FirstOrDefault(x => x.BookingId == b.Id && x.Kind == "out") ?? new InspectionRec { Mileage = 0, Fuel = 0 };
            var r = ReadReturn(o, b);
            foreach (KeyValuePair<string, string> kv in r.Errors)
            {
                response.SetElementContents("dk-e-" + kv.Key, kv.Value);
            }
            if (r.Errors.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            ClassRec cls = f.Classes.FirstOrDefault(x => x.Id == b.ClassId) ?? new ClassRec();
            List<ChargeRec> charges = Rental.ReturnCharges(f.Site, cls, b, o, r.Mileage, r.Fuel, r.When, r.Clean, r.Damage, r.DNote);
            decimal due = Bill.Of(f, b, charges).DueCustomer;
            response.SetElementContents("dk-sum", Sheet.Money("dk", f, b, charges));
            response.ExecuteScript("AdminDeskJs.due('" + Num(Math.Max(0, due)) + "');");
            return response;
        }

        public async Task<ApiResponse> InSave()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData _, BookingRec? b0) = await Open(response, "check in");
            if (c == null || b0 == null)
            {
                return response;
            }
            string notes = Ui.Clip((GetDataValue("notes") ?? string.Empty).Replace("\r", string.Empty), 500);
            decimal paid = Dec(GetDataValue("paid"));
            string method = Methods.Contains(GetDataValue("method")) ? GetDataValue("method")! : "Card";
            List<MediaItem> library = await Media.Load();
            (Dictionary<string, string> panels, List<string> photos, string ePhotos) = ReadInspection(library);
            string code;
            string plate;
            string next;
            string moved = string.Empty;
            decimal balance;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == b0.Id);
                if (b == null || !Avail.Out(b))
                {
                    return await Refuse(response, c, "check in", b0.Code, "This booking was changed meanwhile. Close and open it again.");
                }
                List<InspectionRec> ins = await Store.Rows<InspectionRec>("inspections");
                InspectionRec o = ins.FirstOrDefault(x => x.BookingId == b.Id && x.Kind == "out") ?? new InspectionRec { Mileage = 0, Fuel = 0 };
                var r = ReadReturn(o, b);
                r.Errors["photos"] = ePhotos;
                r.Errors["paid"] = paid < 0 ? "Enter the amount taken now, or 0." : string.Empty;
                r.Errors["notes"] = string.Empty;
                foreach (KeyValuePair<string, string> kv in r.Errors)
                {
                    response.SetElementContents("dk-e-" + kv.Key, kv.Value);
                }
                if (r.Errors.Values.Any(v => v.Length > 0))
                {
                    return response;
                }
                ClassRec cls = f.Classes.FirstOrDefault(x => x.Id == b.ClassId) ?? new ClassRec();
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == b.CarId);
                DateTime now = DateTime.Now;
                b.Charges = Rental.ReturnCharges(f.Site, cls, b, o, r.Mileage, r.Fuel, r.When, r.Clean, r.Damage, r.DNote);
                b.Status = "Returned";
                b.ReturnedAt = r.When;
                b.AskEnd = null;
                if (paid > 0)
                {
                    Bill.Add(f, b, "Customer", method, paid, now, string.Empty, "At check-in", c.U.Id);
                }
                balance = Bill.Of(f, b, b.Charges).DueCustomer;
                bool repair = r.Repair || r.Damage > 0 || f.Repairs.Any(x => x.CarId == b.CarId && (x.Status == "Open" || x.Status == "In shop"));
                bool newRepair = r.Repair || r.Damage > 0;
                if (car != null)
                {
                    car.Mileage = r.Mileage;
                    car.Fuel = r.Fuel;
                    car.BranchId = b.BranchId;
                    car.Status = repair ? "Repair" : "Cleaning";
                    car.StatusNote = repair ? (r.DNote.Length > 0 ? r.DNote : "Sent to repair at check-in") : string.Empty;
                    car.StatusSince = now;
                    car.BlockUntil = null;
                    if (newRepair)
                    {
                        f.Repairs.Add(new RepairRec { Id = Rental.NewId("r-"), CarId = car.Id, BookingId = b.Id, Opened = now, Problem = r.DNote.Length > 0 ? r.DNote : "Checked at return", Charged = r.Damage, Status = "Open", By = c.U.Id });
                    }
                    if (repair)
                    {
                        (string err, List<BookingRec> m) = Ops.Reflow(f, car, now, null, b.Id);
                        moved = (m.Count > 0 ? " " + Ui.Count(m.Count, "booking moved", "bookings moved") + " to other cars." : string.Empty) + (err.Length > 0 ? " Needs a car: " + err : string.Empty);
                    }
                }
                ins.RemoveAll(x => x.BookingId == b.Id && x.Kind == "in");
                ins.Add(new InspectionRec { Id = Rental.NewId("i-"), BookingId = b.Id, CarId = b.CarId, Kind = "in", When = now, By = c.U.Id, Mileage = r.Mileage, Fuel = r.Fuel, Panels = panels, Notes = notes, Photos = photos });
                Avail.Settle(f);
                await Ops.PersistAll(f);
                await Store.SaveRows("inspections", ins);
                code = b.Code;
                plate = car?.Plate ?? string.Empty;
                next = repair ? "repair" : "cleaning";
            }
            await Store.Log(c.U.Id, "check in", code + " · " + plate + " · to " + next + (paid > 0 ? " · " + Rental.Money(paid) + " " + method : string.Empty) + moved, true);
            response.SetElementContents("dk-list", await ListNow(c));
            response.ExecuteScript("AdminDeskJs.clean();AdminDeskJs.shut();AdminDeskJs.toast('" + Ui.Js(plate + " is back and goes to " + next + "." + (balance > 0.005m ? " Balance still open: " + Rental.Money(balance) + "." : string.Empty) + moved) + "');");
            return response;
        }

        public async Task<ApiResponse> NoShow()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData _, BookingRec? b0) = await Open(response, "no-show");
            if (c == null || b0 == null)
            {
                return response;
            }
            string code;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == b0.Id);
                if (b == null || b.Status != "Reserved" || DateTime.Now <= b.Start)
                {
                    return await Refuse(response, c, "no-show", b0.Code, "Only a reserved booking after its pick-up time can be marked as a no-show.");
                }
                b.Status = "No-show";
                b.AskEnd = null;
                code = b.Code;
                Avail.Settle(f);
                await Ops.Persist(f, true, true, false);
            }
            await Store.Log(c.U.Id, "no-show", code, true);
            response.SetElementContents("dk-list", await ListNow(c));
            response.ExecuteScript("AdminDeskJs.toast('" + Ui.Js(code + " marked as a no-show. The car is free again.") + "');");
            return response;
        }

        public async Task<ApiResponse> ExtForm()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData f, BookingRec? b) = await Open(response, "extend");
            if (c == null || b == null)
            {
                return response;
            }
            if (!Avail.Live(b))
            {
                return await Refuse(response, c, "extend", b.Code, b.Code + " is " + b.Status.ToLowerInvariant() + ".");
            }
            DateTime want = b.AskEnd ?? b.End.AddDays(1);
            StringBuilder sb = new StringBuilder("<form class=\"dk-stack\" id=\"dk-form\" onsubmit=\"return AdminDeskJs.extCheck()\" novalidate><input type=\"hidden\" id=\"dk-f-id\" value=\"" + Ui.Esc(b.Id) + "\">");
            sb.Append("<div class=\"dk-two\"><div>" + Sheet.Summary("dk", f, b) + "</div><div>" + Sheet.Money("dk", f, b, null) + "</div></div>");
            sb.Append("<div class=\"dk-grid2\">" + Field("end", "New return time", "<input type=\"datetime-local\" id=\"dk-f-end\" data-k=\"end\" value=\"" + Rental.Iso(want) + "\">") + Field("note", "Note for the record", Text("note", b.AskNote, 160)) + "</div>");
            sb.Append("<div id=\"dk-extres\"></div><div class=\"dk-acts\" id=\"dk-extacts\"><button type=\"submit\" class=\"dk-btn\">Check the new time</button><button type=\"button\" class=\"dk-btn dk-btn-ghost\" onclick=\"AdminDeskJs.closeModal()\">Cancel</button>" + (b.AskEnd != null ? "<button type=\"button\" class=\"dk-btn dk-btn-danger dk-push\" onclick=\"AdminDeskJs.decline('" + Ui.Js(b.Id) + "','" + Ui.Js(b.Code) + "')\">Decline the request</button>" : string.Empty) + "</div></form>");
            response.SetElementContents("dk-mbody", sb.ToString());
            response.ExecuteScript("AdminDeskJs.opened('" + Ui.Js("Extend " + b.Code) + "');");
            return response;
        }

        private static string PlanHtml(FleetData f, BookingRec b, ExtPlan p, DateTime end)
        {
            if (p.Error.Length > 0)
            {
                return "<p class=\"dk-note\">" + Ui.Esc(p.Error) + "</p>";
            }
            CarRec? car = f.Cars.FirstOrDefault(x => x.Id == p.CarId);
            StringBuilder sb = new StringBuilder("<div class=\"dk-ok\"><b>The extension to " + Ui.Esc(Rental.When(end)) + " is possible.</b></div><ul class=\"dk-plan\">");
            sb.Append(p.CarId == b.CarId ? "<li>Same car: " + Ui.Esc(Ops.ShortCar(car)) + ".</li>" : "<li>Car changes to " + Ui.Esc(Ops.ShortCar(car)) + ", because the booked car is not free for the longer time.</li>");
            foreach ((string id, string carId) in p.Moves)
            {
                BookingRec? o = f.Bookings.FirstOrDefault(x => x.Id == id);
                sb.Append("<li>Booking " + Ui.Esc(o?.Code ?? id) + " on this car moves to " + Ui.Esc(Ops.ShortCar(f.Cars.FirstOrDefault(x => x.Id == carId))) + ".</li>");
            }
            sb.Append("<li>" + Ui.Count(p.Days, "day", "days") + " in total. Rental and extras go from " + Rental.Money(b.RateTotal + b.ExtrasTotal) + " to " + Rental.Money(p.Rate + p.Extras) + " (" + (p.Diff >= 0 ? "+" : string.Empty) + Rental.Money(p.Diff) + " before tax).</li></ul>");
            sb.Append("<input type=\"hidden\" id=\"dk-f-sign\" value=\"" + Ui.Esc(p.Sign) + "\">");
            return sb.ToString();
        }

        public async Task<ApiResponse> ExtCheck()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData f, BookingRec? b) = await Open(response, "extend");
            if (c == null || b == null)
            {
                return response;
            }
            if (!Rental.ParseLocal(GetDataValue("end"), out DateTime end))
            {
                response.SetElementContents("dk-e-end", "Choose the new return time.");
                return response;
            }
            response.SetElementContents("dk-e-end", string.Empty);
            ExtPlan p = Ops.PlanExtend(f, b, end);
            response.SetElementContents("dk-extres", PlanHtml(f, b, p, end));
            response.SetElementContents("dk-extacts", "<button type=\"submit\" class=\"dk-btn dk-btn-ghost\">Check again</button>" + (p.Error.Length == 0 ? "<button type=\"button\" class=\"dk-btn\" onclick=\"AdminDeskJs.extSave()\">Confirm the extension</button>" : string.Empty) + "<button type=\"button\" class=\"dk-btn dk-btn-ghost\" onclick=\"AdminDeskJs.closeModal()\">Cancel</button>" + (b.AskEnd != null ? "<button type=\"button\" class=\"dk-btn dk-btn-danger dk-push\" onclick=\"AdminDeskJs.decline('" + Ui.Js(b.Id) + "','" + Ui.Js(b.Code) + "')\">Decline the request</button>" : string.Empty));
            return response;
        }

        public async Task<ApiResponse> ExtSave()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData _, BookingRec? b0) = await Open(response, "extend");
            if (c == null || b0 == null)
            {
                return response;
            }
            if (!Rental.ParseLocal(GetDataValue("end"), out DateTime end))
            {
                response.SetElementContents("dk-e-end", "Choose the new return time.");
                return response;
            }
            string sign = GetDataValue("sign") ?? string.Empty;
            string note = Ui.Clip(GetDataValue("note"), 160);
            string code;
            string email;
            string first;
            string text;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == b0.Id);
                if (b == null)
                {
                    return await Refuse(response, c, "extend", b0.Code, "That booking was not found.");
                }
                ExtPlan p = Ops.PlanExtend(f, b, end);
                if (p.Error.Length > 0 || p.Sign != sign)
                {
                    response.SetElementContents("dk-extres", (p.Error.Length == 0 ? "<p class=\"dk-note\">Something changed since you checked. Here is the new result.</p>" : string.Empty) + PlanHtml(f, b, p, end));
                    return response;
                }
                ExtensionRec x = Ops.ApplyExtend(f, b, p, end, c.U.Id, note);
                List<ExtensionRec> all = await Store.Rows<ExtensionRec>("extensions");
                all.Add(x);
                await Ops.Persist(f, true, true, false);
                await Store.SaveRows("extensions", all);
                code = b.Code;
                email = b.Email;
                first = b.First;
                text = "Hello " + b.First + "," + Environment.NewLine + Environment.NewLine + "Your rental " + b.Code + " is extended. The new return time is " + Rental.When(end) + " at " + Sheet.BranchName(f, b.BranchId) + "." + Environment.NewLine + "The new estimated total is " + Rental.Money(Rental.Of(f.Site, b).Total) + "." + Environment.NewLine + Environment.NewLine + f.Site.Legal + " · " + f.Site.Phone;
            }
            await Store.Log(c.U.Id, "extend booking", code + " · to " + Rental.When(end), true);
            await Sheet.Tell(email, "Your rental " + code + " is extended", text);
            response.SetElementContents("dk-list", await ListNow(c));
            response.ExecuteScript("AdminDeskJs.clean();AdminDeskJs.shut();AdminDeskJs.toast('" + Ui.Js(code + " now returns " + Rental.When(end) + ".") + "');");
            return response;
        }

        public async Task<ApiResponse> Decline()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, FleetData _, BookingRec? b0) = await Open(response, "decline extension");
            if (c == null || b0 == null)
            {
                return response;
            }
            string code;
            string email;
            string text;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == b0.Id);
                if (b == null || b.AskEnd == null)
                {
                    return await Refuse(response, c, "decline extension", b0.Code, "There is no open request on this booking.");
                }
                text = "Hello " + b.First + "," + Environment.NewLine + Environment.NewLine + "We are sorry, we cannot extend rental " + b.Code + " to " + Rental.When(b.AskEnd.Value) + ". Please return the car at " + Rental.When(b.End) + ", or call us to talk about other options." + Environment.NewLine + Environment.NewLine + f.Site.Legal + " · " + f.Site.Phone;
                b.AskEnd = null;
                b.AskNote = string.Empty;
                code = b.Code;
                email = b.Email;
                await Ops.Persist(f, true, false, false);
            }
            await Store.Log(c.U.Id, "decline extension", code, true);
            await Sheet.Tell(email, "About your rental " + code, text);
            response.SetElementContents("dk-list", await ListNow(c));
            response.ExecuteScript("AdminDeskJs.clean();AdminDeskJs.shut();AdminDeskJs.toast('" + Ui.Js("Request on " + code + " declined.") + "');");
            return response;
        }
    }
}
