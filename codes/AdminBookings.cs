using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminBookings : WebPage
    {
        private const string AreaKey = "bookings";
        private const int Size = 12;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Bookings | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminBookingsJs.denied();");
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
            response.SetElementContents("bo-brand", Ui.Esc(site.Brand));
            response.SetElementContents("bo-menu", await Auth.Menu("bo", "AdminBookings", c));
            response.SetElementContents("bo-who", Auth.Who("bo", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminBookingsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"bo-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"bo-btn bo-btn-ghost bo-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminBookingsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"bo-btn bo-btn-ghost bo-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminBookingsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"bo-pill bo-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"bo-err\" id=\"bo-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"bo-field\" for=\"bo-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"bo-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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
            string st = Ui.Clip(GetDataValue("st"), 20);
            st = st.Length == 0 ? "active" : st;
            StringBuilder sb = new StringBuilder("<div class=\"bo-ph\"><div><h1>Bookings</h1><p>Every booking at your branches, from the website and from the desk. Open one for the driver, car, charges and the inspections side by side.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"bo-btn\" onclick=\"AdminBookingsJs.add()\">New booking</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"bo-tools\"><input type=\"search\" id=\"bo-q\" placeholder=\"Code, name, email, phone or plate\" maxlength=\"40\" oninput=\"AdminBookingsJs.type()\" aria-label=\"Search bookings\">");
            sb.Append("<select id=\"bo-fs\" onchange=\"AdminBookingsJs.list(true)\" aria-label=\"Status\">" + Opt("active", "Reserved and out", st) + Opt("overdue", "Overdue", st) + Opt("ask", "Extension requests", st) + Opt("nocar", "Needs a car", st) + Opt("insurer", "Insurer pays", st) + Opt("due", "Balance open", st) + string.Concat(Avail.BookingStatuses.Select(s => Opt(s, s, st))) + Opt("all", "All", st) + "</select>");
            sb.Append("<select id=\"bo-fb\" onchange=\"AdminBookingsJs.list(true)\" aria-label=\"Branch\">" + Opt(string.Empty, "All my branches", string.Empty) + string.Concat(f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select>");
            sb.Append("<label class=\"bo-dlab\">Pick-up from <input type=\"date\" id=\"bo-f0\" onchange=\"AdminBookingsJs.list(true)\"></label><label class=\"bo-dlab\">to <input type=\"date\" id=\"bo-f1\" onchange=\"AdminBookingsJs.list(true)\"></label></div>");
            sb.Append("<div id=\"bo-list\">" + ListHtml(c, f, string.Empty, st, string.Empty, null, null, 1) + "</div>");
            response.SetElementContents("bo-main", sb.ToString());
            if (GetDataValue("new") == "1" && Can(c, 'A'))
            {
                response.ExecuteScript("AdminBookingsJs.add();");
            }
            string code = Ui.Clip(GetDataValue("code"), 20);
            BookingRec? open = f.Bookings.FirstOrDefault(b => b.Code == code && Access.BranchOk(c.U, b.BranchId));
            if (open != null)
            {
                response.ExecuteScript("AdminBookingsJs.view('" + Ui.Js(open.Id) + "');");
            }
            return response;
        }

        private static bool NeedsCar(FleetData f, BookingRec b)
        {
            if (b.Status != "Reserved")
            {
                return false;
            }
            CarRec? car = f.Cars.FirstOrDefault(x => x.Id == b.CarId);
            return car == null || !Avail.CarFree(f, car, b.BranchId, b.Start, b.End, b.Id);
        }

        private static string StatusOf(FleetData f, BookingRec b, DateTime now)
        {
            return Avail.Overdue(b, now) ? "Overdue" : NeedsCar(f, b) ? "Needs a car" : b.Status;
        }

        private static string ListHtml(Ctx c, FleetData f, string term, string st, string branch, DateTime? d0, DateTime? d1, int page)
        {
            DateTime now = DateTime.Now;
            List<BookingRec> rows = f.Bookings.Where(b => Access.BranchOk(c.U, b.BranchId)
                && (branch == string.Empty || b.BranchId == branch)
                && (st switch
                {
                    "active" => Avail.Live(b),
                    "overdue" => Avail.Overdue(b, now),
                    "ask" => Avail.Live(b) && b.AskEnd != null,
                    "nocar" => NeedsCar(f, b),
                    "insurer" => b.PayerKind == "insurer" && Bill.Charged(b),
                    "due" => Bill.Charged(b) && b.Status != "Reserved" && Bill.Of(f, b, null).Due > 0.005m,
                    "all" => true,
                    _ => b.Status == st
                })
                && (d0 == null || b.Start.Date >= d0.Value)
                && (d1 == null || b.Start.Date <= d1.Value)
                && (term == string.Empty || Ui.Has(b.Code, term) || Ui.Has(Rental.Name(b), term) || Ui.Has(b.Email, term) || Ui.Has(b.Phone, term) || Ui.Has(f.Cars.FirstOrDefault(x => x.Id == b.CarId)?.Plate, term))).ToList();
            rows = st == "active" || st == "Reserved" || st == "ask" || st == "nocar" ? rows.OrderBy(b => Avail.Out(b) ? b.End : b.Start).ToList() : rows.OrderByDescending(b => b.Start).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"bo-panel bo-empty\">No bookings match.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"bo-wrapt\"><table class=\"bo-table\"><thead><tr><th>Booking</th><th>Class and car</th><th>Pick-up</th><th>Return</th><th>Status</th><th class=\"bo-num\">Total</th></tr></thead><tbody>");
            foreach (BookingRec b in rows.Skip((page - 1) * Size).Take(Size))
            {
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == b.CarId);
                sb.Append("<tr class=\"bo-row\" onclick=\"AdminBookingsJs.view('" + Ui.Js(b.Id) + "')\"><td class=\"bo-first\"><b>" + Ui.Esc(b.Code) + "</b><small>" + Ui.Esc(Rental.Name(b)) + "</small></td><td data-label=\"Class and car\">" + Ui.Esc(Sheet.ClassName(f, b.ClassId)) + "<small>" + Ui.Esc(car == null ? "No car" : car.Plate) + " &middot; " + Ui.Esc(Sheet.BranchName(f, b.BranchId)) + "</small></td><td data-label=\"Pick-up\">" + Ui.Esc(Rental.When(b.Start)) + "</td><td data-label=\"Return\">" + Ui.Esc(Rental.When(b.End)) + "</td><td data-label=\"Status\">" + Pill(StatusOf(f, b, now)) + (b.AskEnd != null && Avail.Live(b) ? "<small>Asks to extend</small>" : string.Empty) + "</td><td data-label=\"Total\" class=\"bo-num\">" + Rental.Money(Rental.Of(f.Site, b).Total) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "booking", "bookings"));
            return sb.ToString();
        }

        private async Task<string> ListNow(Ctx c)
        {
            FleetData f = await Avail.LoadSettled();
            DateTime? d0 = Rental.ParseDate(GetDataValue("f0"), out DateTime a) ? a : null;
            DateTime? d1 = Rental.ParseDate(GetDataValue("f1"), out DateTime z) ? z : null;
            string st = Ui.Clip(GetDataValue("fs"), 20);
            return ListHtml(c, f, Term(GetDataValue("term")), st.Length == 0 ? "active" : st, Ui.Clip(GetDataValue("fb"), 60), d0, d1, PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("bo-list", await ListNow(c));
            return response;
        }

        public async Task<ApiResponse> View()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            FleetData f = await Avail.LoadSettled();
            BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("id"), 60));
            if (b == null || !Access.BranchOk(c.U, b.BranchId))
            {
                return await Refuse(response, c, "open booking", string.Empty, "That booking was not found at your branches.");
            }
            List<InspectionRec> ins = (await Store.Rows<InspectionRec>("inspections")).Where(x => x.BookingId == b.Id).ToList();
            List<ExtensionRec> ext = (await Store.Rows<ExtensionRec>("extensions")).Where(x => x.BookingId == b.Id).OrderBy(x => x.When).ToList();
            List<MediaItem> media = await Media.Load();
            DateTime now = DateTime.Now;
            StringBuilder sb = new StringBuilder("<div class=\"bo-bar\"><div>" + Pill(StatusOf(f, b, now)) + " <span class=\"bo-mini\">" + Ui.Esc(b.Code) + "</span></div><div class=\"bo-acts-row\">");
            if (Access.Can(c.U, "desk", 'E') && Avail.Live(b))
            {
                sb.Append("<a class=\"bo-btn bo-btn-ghost bo-btn-sm\" href=\"AdminDesk?b=" + Ui.Esc(b.BranchId) + "\">Open the desk</a>");
            }
            if (Can(c, 'E') && b.Status == "Reserved")
            {
                sb.Append("<button type=\"button\" class=\"bo-btn bo-btn-ghost bo-btn-sm\" onclick=\"AdminBookingsJs.edit('" + Ui.Js(b.Id) + "')\">Change</button><button type=\"button\" class=\"bo-btn bo-btn-danger bo-btn-sm\" onclick=\"AdminBookingsJs.cancel('" + Ui.Js(b.Id) + "','" + Ui.Js(b.Code) + "')\">Cancel booking</button>");
            }
            if (Can(c, 'D') && (b.Status == "Cancelled" || b.Status == "No-show"))
            {
                sb.Append("<button type=\"button\" class=\"bo-btn bo-btn-danger bo-btn-sm\" onclick=\"AdminBookingsJs.del('" + Ui.Js(b.Id) + "','" + Ui.Js(b.Code) + "')\">Delete</button>");
            }
            sb.Append("</div></div>");
            if (NeedsCar(f, b))
            {
                sb.Append("<p class=\"bo-note\">The car on this booking is not free for these dates. Use Change and save to find another car, or contact the customer.</p>");
            }
            sb.Append("<div class=\"bo-two\"><div>" + Sheet.Summary("bo", f, b) + "</div><div>" + Sheet.Money("bo", f, b, null) + "</div></div>");
            sb.Append("<p class=\"bo-sec\">Inspections</p>" + Sheet.Compare("bo", ins.FirstOrDefault(x => x.Kind == "out"), ins.FirstOrDefault(x => x.Kind == "in"), media));
            List<PaymentRec> pays = f.Payments.Where(x => x.BookingId == b.Id).OrderBy(x => x.When).ToList();
            sb.Append("<p class=\"bo-sec\">Payments</p>" + (pays.Count == 0 ? "<p class=\"bo-hint\">No payments yet.</p>" : "<div class=\"bo-wrapt\"><table class=\"bo-table\"><thead><tr><th>When</th><th>Payer</th><th>Method</th><th class=\"bo-num\">Amount</th></tr></thead><tbody>" + string.Concat(pays.Select(x => "<tr><td class=\"bo-first\"><b>" + Ui.Esc(Rental.When(x.When)) + "</b><small>" + Ui.Esc(x.Note) + "</small></td><td data-label=\"Payer\">" + Ui.Esc(x.Payer == "Insurer" ? f.Insurers.FirstOrDefault(i => i.Id == x.InsurerId)?.Name ?? "Insurer" : "Customer") + "</td><td data-label=\"Method\">" + Ui.Esc(x.Method) + (x.Reference.Length > 0 ? "<small>" + Ui.Esc(x.Reference) + "</small>" : string.Empty) + "</td><td data-label=\"Amount\" class=\"bo-num\">" + Rental.Money(x.Amount) + "</td></tr>")) + "</tbody></table></div>") + (Access.Can(c.U, "payments", 'A') && Bill.Charged(b) ? "<div class=\"bo-acts-row\" style=\"justify-content:flex-start;margin-top:10px\"><a class=\"bo-btn bo-btn-ghost bo-btn-sm\" href=\"AdminPayments?code=" + Ui.Esc(b.Code) + "\">Record a payment or refund</a></div>" : string.Empty));
            if (ext.Count > 0)
            {
                List<AdminUser> staff = await Store.LoadStaff();
                sb.Append("<p class=\"bo-sec\">Extensions</p><div class=\"bo-wrapt\"><table class=\"bo-table\"><thead><tr><th>When</th><th>Return moved</th><th>Car</th><th class=\"bo-num\">Change</th></tr></thead><tbody>" + string.Concat(ext.Select(x => "<tr><td class=\"bo-first\"><b>" + Ui.Esc(Rental.When(x.When)) + "</b><small>" + Ui.Esc(staff.FirstOrDefault(u => u.Id == x.By)?.Name ?? x.By) + (x.Note.Length > 0 ? " · " + Ui.Esc(x.Note) : string.Empty) + "</small></td><td data-label=\"Return moved\">" + Ui.Esc(Rental.When(x.OldEnd)) + " &rarr; " + Ui.Esc(Rental.When(x.NewEnd)) + "</td><td data-label=\"Car\">" + (x.OldCarId == x.NewCarId ? "Same car" : Ui.Esc(f.Cars.FirstOrDefault(k => k.Id == x.OldCarId)?.Plate ?? "?") + " &rarr; " + Ui.Esc(f.Cars.FirstOrDefault(k => k.Id == x.NewCarId)?.Plate ?? "?")) + "</td><td data-label=\"Change\" class=\"bo-num\">" + Rental.Money(x.Amount) + "</td></tr>")) + "</tbody></table></div>");
            }
            response.SetElementContents("bo-mbody", sb.ToString());
            response.ExecuteScript("AdminBookingsJs.opened('" + Ui.Js(b.Code + " · " + Rental.Name(b)) + "');");
            return response;
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            FleetData f = await Avail.LoadSettled();
            BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (b == null || !Access.BranchOk(c.U, b.BranchId) || b.Status != "Reserved"))
            {
                return await Refuse(response, c, "change booking", id, "Only a reserved booking at your branches can be changed.");
            }
            if (!Can(c, b == null ? 'A' : 'E'))
            {
                return await Refuse(response, c, b == null ? "add booking" : "change booking", id, "You do not have the right to " + (b == null ? "add" : "change") + " bookings.");
            }
            response.SetElementContents("bo-mbody", FormHtml(c, f, b));
            response.ExecuteScript("AdminBookingsJs.opened('" + Ui.Js(b == null ? "New booking" : "Change " + b.Code) + "');AdminBookingsJs.payer();");
            return response;
        }

        private static string FormHtml(Ctx c, FleetData f, BookingRec? x)
        {
            bool isNew = x == null;
            DateTime d0 = DateTime.Now.AddHours(1);
            d0 = new DateTime(d0.Year, d0.Month, d0.Day, d0.Hour, 0, 0);
            BookingRec r = x ?? new BookingRec { Start = d0, End = d0.AddDays(3) };
            List<BranchRec> mine = f.Branches.Where(b => b.Open && Access.BranchOk(c.U, b.Id)).ToList();
            StringBuilder sb = new StringBuilder("<form class=\"bo-stack\" id=\"bo-form\" onsubmit=\"return AdminBookingsJs.save()\" oninput=\"AdminBookingsJs.later()\" onchange=\"AdminBookingsJs.later()\" novalidate><input type=\"hidden\" id=\"bo-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            sb.Append("<div class=\"bo-two\"><div class=\"bo-stack\"><p class=\"bo-sec\" style=\"border:0;padding:0;margin:0\">Trip</p><div class=\"bo-grid2\">" + Field("branch", "Branch", "<select id=\"bo-f-branch\" data-k=\"branch\">" + string.Concat(mine.Select(b => Opt(b.Id, b.Name, r.BranchId))) + "</select>") + Field("cls", "Class", "<select id=\"bo-f-cls\" data-k=\"cls\">" + string.Concat(f.Classes.Select(k => Opt(k.Id, k.Name + " · " + Rental.Money(k.Daily) + " a day", r.ClassId))) + "</select>") + "</div>");
            sb.Append("<div class=\"bo-grid2\">" + Field("start", "Pick-up", "<input type=\"datetime-local\" id=\"bo-f-start\" data-k=\"start\" value=\"" + Rental.Iso(r.Start) + "\">") + Field("end", "Return", "<input type=\"datetime-local\" id=\"bo-f-end\" data-k=\"end\" value=\"" + Rental.Iso(r.End) + "\">") + "</div>" + Err("trip"));
            sb.Append("<p class=\"bo-sec\">Driver</p><div class=\"bo-grid2\">" + Field("first", "First name", Text("first", r.First, 40)) + Field("last", "Last name", Text("last", r.Last, 40)) + Field("email", "Email", Text("email", r.Email, 80, "email")) + Field("phone", "Phone", Text("phone", r.Phone, 30, "tel")) + Field("license", "Driver's licence number", Text("license", r.License, 20)) + Field("lstate", "Issued by (state or country)", Text("lstate", r.LicenseState, 30)) + Field("birth", "Date of birth", "<input type=\"date\" id=\"bo-f-birth\" data-k=\"birth\" value=\"" + (r.Birth == default ? string.Empty : r.Birth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) + "\">") + "</div>");
            sb.Append("<p class=\"bo-sec\">Extras and coverage</p><div class=\"bo-grid2\">");
            foreach (ExtraRec e in f.Extras.Where(e => e.Kind != "cover"))
            {
                int q = r.Extras.FirstOrDefault(y => y.Id == e.Id)?.Qty ?? 0;
                sb.Append("<label class=\"bo-field\"><span>" + Ui.Esc(e.Name + " · " + Rental.Money(e.PerDay) + " a day") + (e.Show ? string.Empty : " (hidden on website)") + "</span><select class=\"bo-xq\" data-x=\"" + Ui.Esc(e.Id) + "\">" + string.Concat(Enumerable.Range(0, e.MaxQty + 1).Select(n => Opt(n.ToString(CultureInfo.InvariantCulture), n == 0 ? "None" : n.ToString(CultureInfo.InvariantCulture), q.ToString(CultureInfo.InvariantCulture)))) + "</select></label>");
            }
            string cover = r.Extras.FirstOrDefault(y => f.Extras.Any(e => e.Id == y.Id && e.Kind == "cover"))?.Id ?? string.Empty;
            sb.Append("</div><div class=\"bo-stack\"><label class=\"bo-check\"><input type=\"radio\" name=\"bo-cover\" value=\"\"" + (cover.Length == 0 ? " checked" : string.Empty) + "> Basic coverage, included</label>");
            foreach (ExtraRec e in f.Extras.Where(e => e.Kind == "cover"))
            {
                sb.Append("<label class=\"bo-check\"><input type=\"radio\" name=\"bo-cover\" value=\"" + Ui.Esc(e.Id) + "\"" + (cover == e.Id ? " checked" : string.Empty) + "> " + Ui.Esc(e.Name + " · " + Rental.Money(e.PerDay) + " a day") + "</label>");
            }
            List<InsurerRec> ins = f.Insurers.Where(i => i.Active || i.Id == r.InsurerId).ToList();
            bool byIns = r.PayerKind == "insurer" && ins.Any(i => i.Id == r.InsurerId);
            sb.Append("</div>" + Err("extras") + "<p class=\"bo-sec\">Who pays</p><div class=\"bo-stack\"><label class=\"bo-check\"><input type=\"radio\" name=\"bo-payer\" value=\"self\" onchange=\"AdminBookingsJs.payer()\"" + (byIns ? string.Empty : " checked") + "> The customer pays everything</label><label class=\"bo-check\"><input type=\"radio\" name=\"bo-payer\" value=\"insurer\" onchange=\"AdminBookingsJs.payer()\"" + (byIns ? " checked" : string.Empty) + (ins.Count == 0 ? " disabled" : string.Empty) + "> An insurer pays the rental, the customer pays the rest" + (ins.Count == 0 ? " (add an insurer first)" : string.Empty) + "</label></div><div class=\"bo-grid2\" id=\"bo-insbox\">" + Field("ins", "Insurer", "<select id=\"bo-f-ins\" data-k=\"ins\">" + string.Concat(ins.Select(i => "<option value=\"" + Ui.Esc(i.Id) + "\" data-x=\"" + (i.CoverExtras ? "1" : "0") + "\"" + (i.Id == r.InsurerId ? " selected" : string.Empty) + ">" + Ui.Esc(i.Name + (i.DailyCap > 0 ? " · up to " + Rental.Money(i.DailyCap) + " a day" : string.Empty)) + "</option>")) + "</select>") + Field("claim", "Claim number", Text("claim", r.ClaimNo, 40)) + "<label class=\"bo-check\"><input type=\"checkbox\" id=\"bo-f-cover\" data-k=\"cover\"" + (r.CoverExtras ? " checked" : string.Empty) + "> The insurer also pays the extras</label></div>" + Err("payer") + "<label class=\"bo-field\" for=\"bo-f-note\"><span>Staff note</span><textarea id=\"bo-f-note\" data-k=\"note\" rows=\"2\" maxlength=\"300\">" + Ui.Esc(r.Note) + "</textarea></label></div>");
            sb.Append("<div class=\"bo-stack bo-stick\"><p class=\"bo-sec\" style=\"border:0;padding:0;margin:0\">Price</p><div id=\"bo-quote\"><p class=\"bo-hint\">Fill in the trip to see the price.</p></div>" + (isNew ? "<label class=\"bo-check\"><input type=\"checkbox\" id=\"bo-f-mail\" data-k=\"mail\" checked> Email the confirmation to the customer</label>" : string.Empty) + "</div></div>");
            sb.Append("<div class=\"bo-acts\"><button type=\"submit\" class=\"bo-btn\">" + (isNew ? "Book" : "Save changes") + "</button><button type=\"button\" class=\"bo-btn bo-btn-ghost\" onclick=\"AdminBookingsJs.closeModal()\">Cancel</button></div></form>");
            return sb.ToString();
        }

        private (string Branch, string Cls, DateTime Start, DateTime End, List<BookedExtra> Extras) ReadTrip()
        {
            Rental.ParseLocal(GetDataValue("start"), out DateTime s);
            Rental.ParseLocal(GetDataValue("end"), out DateTime e);
            return (Ui.Clip(GetDataValue("branch"), 60), Ui.Clip(GetDataValue("cls"), 60), s, e, Ops.ParseExtras(GetDataValue("extras")));
        }

        public async Task<ApiResponse> Quote()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            FleetData f = await Avail.LoadSettled();
            string id = Ui.Clip(GetDataValue("id"), 60);
            var t = ReadTrip();
            string err = !Access.BranchOk(c.U, t.Branch) ? "Choose one of your branches." : Ops.TripError(f, t.Branch, t.Cls, t.Start, t.End, true);
            if (err.Length == 0)
            {
                err = Ops.ExtrasError(f, t.Branch, t.Start, t.End, id, t.Extras, true);
            }
            if (err.Length > 0)
            {
                response.SetElementContents("bo-quote", "<p class=\"bo-note\">" + Ui.Esc(err) + "</p>");
                return response;
            }
            ClassRec k = f.Classes.First(x => x.Id == t.Cls);
            int free = Avail.FreeCars(f, t.Branch, t.Cls, t.Start, t.End, id).Count;
            BookingRec? cur = f.Bookings.FirstOrDefault(x => x.Id == id);
            bool keeps = cur != null && f.Cars.Any(x => x.Id == cur.CarId && x.ClassId == t.Cls && Avail.CarFree(f, x, t.Branch, t.Start, t.End, id));
            Quote q = Rental.Price(f.Site, k, f.Extras, t.Start, t.End, t.Extras, 0);
            StringBuilder sb = new StringBuilder("<div class=\"bo-sum\"><div><span>" + Ui.Esc(k.Name) + ", " + Ui.Count(q.Days, "day", "days") + "</span><span>" + Rental.Money(q.Rate) + "</span></div>" + (q.Extras > 0 ? "<div><span>Extras</span><span>" + Rental.Money(q.Extras) + "</span></div>" : string.Empty) + "<div><span>Tax</span><span>" + Rental.Money(q.Tax) + "</span></div><div class=\"bo-tot\"><span>Total</span><span>" + Rental.Money(q.Total) + "</span></div>" + (k.Deposit > 0 ? "<div class=\"bo-mini\"><span>Deposit at pick-up</span><span>" + Rental.Money(k.Deposit) + "</span></div>" : string.Empty) + "</div>");
            BookingRec tmp = new BookingRec { Start = t.Start, End = t.End, RateTotal = q.Rate, ExtrasTotal = q.Extras, PayerKind = GetDataValue("payer") == "insurer" ? "insurer" : "self", InsurerId = Ui.Clip(GetDataValue("ins"), 60), CoverExtras = GetDataValue("cover") == "1" };
            Split sp = Bill.Of(f, tmp, new List<ChargeRec>());
            if (sp.Insurer > 0)
            {
                sb.Append("<div class=\"bo-sum\"><div><span>" + Ui.Esc(sp.InsurerName) + " pays</span><span>" + Rental.Money(sp.Insurer) + "</span></div><div class=\"bo-tot\"><span>Customer pays</span><span>" + Rental.Money(sp.Customer) + "</span></div></div>");
            }
            sb.Append(free > 0 || keeps ? "<p class=\"bo-ok\">" + (keeps ? "The booked car stays free for these dates." : Ui.Count(free, "car is", "cars are") + " free for these dates.") + "</p>" : "<p class=\"bo-note\">No car of this class is free for these dates" + (Avail.Assign(f, t.Branch, t.Cls, t.Start, t.End, id).CarId.Length > 0 ? ", but one can be freed by moving another booking. Saving does that." : ".") + "</p>");
            response.SetElementContents("bo-quote", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            bool isNew = id.Length == 0;
            if (!Can(c, isNew ? 'A' : 'E'))
            {
                return await Refuse(response, c, isNew ? "add booking" : "change booking", id, "You do not have the right to " + (isNew ? "add" : "change") + " bookings.");
            }
            var t = ReadTrip();
            Driver d = new Driver { First = Ui.Clip(GetDataValue("first"), 40), Last = Ui.Clip(GetDataValue("last"), 40), Email = Ui.Clip(GetDataValue("email"), 80), Phone = Ui.Clip(GetDataValue("phone"), 30), License = Ui.Clip(GetDataValue("license"), 20).ToUpperInvariant(), State = Ui.Clip(GetDataValue("lstate"), 30), BirthText = Ui.Clip(GetDataValue("birth"), 10) };
            string note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 300);
            bool mail = GetDataValue("mail") == "1";
            string code;
            string text = string.Empty;
            string subject = string.Empty;
            string email = d.Email;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                string trip = !Access.BranchOk(c.U, t.Branch) ? "Choose one of your branches." : Ops.TripError(f, t.Branch, t.Cls, t.Start, t.End, true);
                Dictionary<string, string> e = Ops.DriverErrors(f.Site, d, t.Start == default ? DateTime.Today : t.Start, out DateTime birth);
                e["trip"] = trip;
                e["extras"] = trip.Length == 0 ? Ops.ExtrasError(f, t.Branch, t.Start, t.End, id, t.Extras, true) : string.Empty;
                foreach (KeyValuePair<string, string> kv in e)
                {
                    response.SetElementContents("bo-e-" + kv.Key, kv.Value);
                }
                if (e.Values.Any(v => v.Length > 0))
                {
                    response.ExecuteScript("AdminBookingsJs.toast('Please check the marked fields.');");
                    return response;
                }
                BookingRec? b = isNew ? new BookingRec { Source = "desk", CreatedBy = c.U.Id } : f.Bookings.FirstOrDefault(x => x.Id == id);
                if (b == null || (!isNew && (b.Status != "Reserved" || !Access.BranchOk(c.U, b.BranchId))))
                {
                    return await Refuse(response, c, "change booking", id, "Only a reserved booking at your branches can be changed.");
                }
                if (!isNew && b.ClassId != t.Cls)
                {
                    b.CarId = string.Empty;
                }
                b.BranchId = t.Branch;
                b.ClassId = t.Cls;
                b.Start = t.Start;
                b.End = t.End;
                b.Extras = t.Extras;
                b.First = d.First;
                b.Last = d.Last;
                b.Email = d.Email;
                b.Phone = d.Phone;
                b.License = d.License;
                b.LicenseState = d.State;
                b.Birth = birth;
                b.Note = note;
                InsurerRec? insurer = f.Insurers.FirstOrDefault(i => i.Id == Ui.Clip(GetDataValue("ins"), 60) && (i.Active || i.Id == b.InsurerId));
                if (GetDataValue("payer") == "insurer")
                {
                    if (insurer == null)
                    {
                        response.SetElementContents("bo-e-payer", "Choose the insurer.");
                        return response;
                    }
                    b.PayerKind = "insurer";
                    b.InsurerId = insurer.Id;
                    b.ClaimNo = Ui.Clip(GetDataValue("claim"), 40);
                    b.CoverExtras = GetDataValue("cover") == "1";
                }
                else
                {
                    b.PayerKind = "self";
                    b.InsurerId = string.Empty;
                    b.ClaimNo = string.Empty;
                    b.CoverExtras = false;
                }
                (BookingRec? placed, string err) = Ops.Place(f, b, true);
                if (placed == null)
                {
                    response.SetElementContents("bo-e-trip", err);
                    return response;
                }
                await Ops.Persist(f, true, true, false);
                code = placed.Code;
                if (isNew && mail)
                {
                    text = Sheet.ConfirmText(f, placed);
                    subject = "Your " + f.Site.Brand + " booking " + code;
                }
            }
            await Store.Log(c.U.Id, isNew ? "add booking" : "change booking", code, true);
            if (text.Length > 0)
            {
                await Sheet.Tell(email, subject, text);
            }
            response.SetElementContents("bo-list", await ListNow(c));
            response.ExecuteScript("AdminBookingsJs.clean();AdminBookingsJs.shut();AdminBookingsJs.toast('" + Ui.Js(isNew ? "Booked. The code is " + code + "." : code + " saved.") + "');");
            return response;
        }

        public async Task<ApiResponse> Cancel()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            string reason = Ui.Clip(GetDataValue("reason"), 160);
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "cancel booking", id, "You do not have the right to change bookings.");
            }
            string code;
            string email;
            string text;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == id);
                if (b == null || !Access.BranchOk(c.U, b.BranchId) || b.Status != "Reserved")
                {
                    return await Refuse(response, c, "cancel booking", id, "Only a reserved booking can be cancelled.");
                }
                b.Status = "Cancelled";
                b.CancelledAt = DateTime.Now;
                b.AskEnd = null;
                b.Note = (b.Note + (reason.Length > 0 ? (b.Note.Length > 0 ? " · " : string.Empty) + "Cancelled: " + reason : string.Empty)).Trim();
                code = b.Code;
                email = b.Email;
                text = "Hello " + b.First + "," + Environment.NewLine + Environment.NewLine + "Your booking " + b.Code + " for " + Rental.When(b.Start) + " is cancelled. Nothing is charged." + Environment.NewLine + Environment.NewLine + f.Site.Legal + " · " + f.Site.Phone;
                Avail.Settle(f);
                await Ops.Persist(f, true, true, false);
            }
            await Store.Log(c.U.Id, "cancel booking", code + (reason.Length > 0 ? " · " + reason : string.Empty), true);
            await Sheet.Tell(email, "Booking " + code + " cancelled", text);
            response.SetElementContents("bo-list", await ListNow(c));
            response.ExecuteScript("AdminBookingsJs.clean();AdminBookingsJs.shut();AdminBookingsJs.toast('" + Ui.Js(code + " cancelled.") + "');");
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
            string id = Ui.Clip(GetDataValue("id"), 60);
            if (!Can(c, 'D'))
            {
                return await Refuse(response, c, "delete booking", id, "You do not have the right to delete bookings.");
            }
            string code;
            using (await Store.Hold())
            {
                List<BookingRec> all = await Store.Rows<BookingRec>("bookings");
                BookingRec? b = all.FirstOrDefault(x => x.Id == id);
                if (b == null || !Access.BranchOk(c.U, b.BranchId) || (b.Status != "Cancelled" && b.Status != "No-show"))
                {
                    return await Refuse(response, c, "delete booking", id, "Only a cancelled or no-show booking can be deleted.");
                }
                code = b.Code;
                all.Remove(b);
                await Store.SaveRows("bookings", all);
                List<ExtensionRec> ext = await Store.Rows<ExtensionRec>("extensions");
                if (ext.RemoveAll(x => x.BookingId == id) > 0)
                {
                    await Store.SaveRows("extensions", ext);
                }
            }
            await Store.Log(c.U.Id, "delete booking", code, true);
            response.SetElementContents("bo-list", await ListNow(c));
            response.ExecuteScript("AdminBookingsJs.deleted();");
            return response;
        }
    }
}
