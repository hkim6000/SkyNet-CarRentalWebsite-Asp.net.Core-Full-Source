using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminReports : WebPage
    {
        private const string AreaKey = "reports";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Reports | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminReportsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminReportsJs.denied();");
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
            response.SetElementContents("rt-brand", Ui.Esc(site.Brand));
            response.SetElementContents("rt-menu", await Auth.Menu("rt", "AdminReports", c));
            response.SetElementContents("rt-who", Auth.Who("rt", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminReportsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"rt-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"rt-btn rt-btn-ghost rt-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminReportsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"rt-btn rt-btn-ghost rt-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminReportsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"rt-pill rt-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"rt-err\" id=\"rt-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"rt-field\" for=\"rt-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"rt-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private sealed class Row
        {
            public int Rentals { get; set; }
            public int Days { get; set; }
            public decimal Rate { get; set; }
            public decimal Extras { get; set; }
            public decimal Charges { get; set; }
            public decimal Tax { get; set; }
            public decimal Sub => Rate + Extras + Charges;
            public decimal Total => Sub + Tax;
            public double Hours { get; set; }
            public double Open { get; set; }
            public decimal Costs { get; set; }
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
            DateTime d0 = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
            DateTime d1 = DateTime.Today;
            StringBuilder sb = new StringBuilder("<div class=\"rt-ph\"><div><h1>Reports</h1><p>Revenue counts rentals returned in the period, before tax unless a column says otherwise. Use Download on any table to open it in a spreadsheet.</p></div></div>");
            sb.Append("<div class=\"rt-tools\"><label class=\"rt-dlab\">From <input type=\"date\" id=\"rt-f0\" value=\"" + d0.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\" onchange=\"AdminReportsJs.run()\"></label><label class=\"rt-dlab\">to <input type=\"date\" id=\"rt-f1\" value=\"" + d1.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\" onchange=\"AdminReportsJs.run()\"></label><select id=\"rt-fb\" onchange=\"AdminReportsJs.run()\" aria-label=\"Branch\">" + Opt(string.Empty, "All my branches", string.Empty) + string.Concat(f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select><span class=\"rt-chips\"><button type=\"button\" class=\"rt-chip\" onclick=\"AdminReportsJs.span('month')\">This month</button><button type=\"button\" class=\"rt-chip\" onclick=\"AdminReportsJs.span('last')\">Last month</button><button type=\"button\" class=\"rt-chip\" onclick=\"AdminReportsJs.span('year')\">This year</button></span></div>");
            sb.Append("<div id=\"rt-list\">" + Report(c, f, d0, d1, string.Empty) + "</div>");
            response.SetElementContents("rt-main", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Run()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            FleetData f = await Avail.LoadSettled();
            if (!Rental.ParseDate(GetDataValue("f0"), out DateTime d0) || !Rental.ParseDate(GetDataValue("f1"), out DateTime d1) || d1 < d0 || (d1 - d0).TotalDays > 1100)
            {
                response.SetElementContents("rt-list", "<div class=\"rt-panel rt-empty\">Choose a period: a start date on or before the end date, up to three years.</div>");
                return response;
            }
            string branch = Ui.Clip(GetDataValue("fb"), 60);
            response.SetElementContents("rt-list", Report(c, f, d0, d1, Access.BranchOk(c.U, branch) ? branch : string.Empty));
            return response;
        }

        private static double Hours(DateTime a0, DateTime a1, DateTime b0, DateTime b1)
        {
            DateTime s = a0 > b0 ? a0 : b0;
            DateTime e = a1 < b1 ? a1 : b1;
            return e > s ? (e - s).TotalHours : 0;
        }

        private static void Add(Row r, Quote q)
        {
            r.Rentals++;
            r.Days += q.Days;
            r.Rate += q.Rate;
            r.Extras += q.Extras;
            r.Charges += q.Charges;
            r.Tax += q.Tax;
        }

        private static string Pct(double used, double open)
        {
            return open <= 0 ? "&mdash;" : (used / open * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static string Table(string id, string title, string head, string body, string foot)
        {
            return "<section class=\"rt-rsec\"><div class=\"rt-rhead\"><h2>" + Ui.Esc(title) + "</h2><button type=\"button\" class=\"rt-btn rt-btn-ghost rt-btn-sm\" onclick=\"AdminReportsJs.csv('" + id + "','" + Ui.Js(title) + "')\">Download</button></div><div class=\"rt-wrapt\"><table class=\"rt-table rt-rtab\" id=\"" + id + "\"><thead><tr>" + head + "</tr></thead><tbody>" + body + "</tbody>" + (foot.Length > 0 ? "<tfoot><tr>" + foot + "</tr></tfoot>" : string.Empty) + "</table></div></section>";
        }

        private static string Th(params string[] cols)
        {
            return string.Concat(cols.Select((x, i) => "<th" + (i > 0 && x.StartsWith("#", StringComparison.Ordinal) ? " class=\"rt-num\"" : string.Empty) + ">" + Ui.Esc(x.TrimStart('#')) + "</th>"));
        }

        private static string Td(string label, string value, bool num, bool first = false)
        {
            return first ? "<td class=\"rt-first\">" + value + "</td>" : "<td data-label=\"" + Ui.Esc(label) + "\"" + (num ? " class=\"rt-num\"" : string.Empty) + ">" + value + "</td>";
        }

        private static string Report(Ctx c, FleetData f, DateTime d0, DateTime d1, string branch)
        {
            DateTime from = d0.Date;
            DateTime to = d1.Date.AddDays(1);
            DateTime now = DateTime.Now;
            DateTime clip = to < now ? to : now;
            Func<string, bool> inScope = id => Access.BranchOk(c.U, id) && (branch == string.Empty || id == branch);
            List<BookingRec> done = f.Bookings.Where(b => b.Status == "Returned" && b.ReturnedAt != null && b.ReturnedAt.Value >= from && b.ReturnedAt.Value < to && inScope(b.BranchId)).ToList();
            Dictionary<string, Quote> qs = done.ToDictionary(b => b.Id, b => Rental.Of(f.Site, b));
            List<CarRec> cars = f.Cars.Where(x => inScope(x.BranchId) || done.Any(b => b.CarId == x.Id)).ToList();
            Dictionary<string, Row> byCar = new Dictionary<string, Row>();
            foreach (CarRec car in cars)
            {
                Row r = new Row();
                DateTime life0 = car.Purchased > from ? car.Purchased : from;
                SaleRec? sale = f.Sales.FirstOrDefault(x => x.CarId == car.Id);
                DateTime life1 = sale != null && sale.Date < clip ? sale.Date : clip;
                r.Open = Hours(life0, life1, from, clip);
                foreach (BookingRec b in f.Bookings.Where(b => b.CarId == car.Id && (b.Status == "Returned" || Avail.Out(b)) && b.PickedUpAt != null))
                {
                    r.Hours += Hours(b.PickedUpAt!.Value, b.ReturnedAt ?? now, from, clip);
                }
                r.Costs = f.Services.Where(v => v.CarId == car.Id && v.Status == "Done" && v.DoneAt >= from && v.DoneAt < to).Sum(v => v.Cost) + f.Repairs.Where(x => x.CarId == car.Id && x.Status == "Done" && x.ClosedAt >= from && x.ClosedAt < to).Sum(x => x.Cost);
                byCar[car.Id] = r;
            }
            foreach (BookingRec b in done)
            {
                if (byCar.TryGetValue(b.CarId, out Row? r))
                {
                    Add(r, qs[b.Id]);
                }
            }
            Row all = new Row();
            foreach (BookingRec b in done)
            {
                Add(all, qs[b.Id]);
            }
            all.Hours = byCar.Values.Sum(x => x.Hours);
            all.Open = byCar.Values.Sum(x => x.Open);
            all.Costs = byCar.Values.Sum(x => x.Costs);
            List<PaymentRec> pays = f.Payments.Where(p => p.When >= from && p.When < to && f.Bookings.Any(b => b.Id == p.BookingId && inScope(b.BranchId))).ToList();
            List<(BookingRec B, Split S)> owing = f.Bookings.Where(b => inScope(b.BranchId) && Bill.Charged(b) && b.Status != "Reserved").Select(b => (B: b, S: Bill.Of(f, b, null))).Where(x => x.S.DueCustomer > 0.005m || x.S.DueInsurer > 0.005m).OrderBy(x => x.B.ReturnedAt ?? x.B.End).ToList();
            int lost = f.Bookings.Count(b => (b.Status == "Cancelled" || b.Status == "No-show") && b.Start >= from && b.Start < to && inScope(b.BranchId));
            StringBuilder sb = new StringBuilder("<div class=\"rt-tiles\">");
            sb.Append("<div class=\"rt-tile\"><b>" + all.Rentals + "</b><span>rentals returned</span></div>");
            sb.Append("<div class=\"rt-tile\"><b>" + all.Days + "</b><span>rental days</span></div>");
            sb.Append("<div class=\"rt-tile\"><b>" + Rental.Money(all.Sub) + "</b><span>revenue before tax</span></div>");
            sb.Append("<div class=\"rt-tile\"><b>" + Pct(all.Hours, all.Open) + "</b><span>fleet time on rent</span></div>");
            sb.Append("<div class=\"rt-tile\"><b>" + Rental.Money(pays.Sum(p => p.Amount)) + "</b><span>payments received, net</span></div>");
            sb.Append("<div class=\"rt-tile" + (owing.Count > 0 ? " rt-warn" : string.Empty) + "\"><b>" + Rental.Money(owing.Sum(x => Math.Max(0, x.S.DueCustomer)) + owing.Sum(x => Math.Max(0, x.S.DueInsurer))) + "</b><span>open balances today</span></div>");
            sb.Append("<div class=\"rt-tile\"><b>" + Rental.Money(all.Costs) + "</b><span>service and repair costs</span></div>");
            sb.Append("<div class=\"rt-tile\"><b>" + lost + "</b><span>cancelled or no-show</span></div></div>");

            StringBuilder body = new StringBuilder();
            foreach (BranchRec br in f.Branches.Where(x => inScope(x.Id)))
            {
                Row r = new Row();
                foreach (BookingRec b in done.Where(b => b.BranchId == br.Id))
                {
                    Add(r, qs[b.Id]);
                }
                List<Row> bc = cars.Where(x => x.BranchId == br.Id).Select(x => byCar[x.Id]).ToList();
                body.Append("<tr>" + Td(string.Empty, "<b>" + Ui.Esc(br.Name) + "</b>", false, true) + Td("Rentals", r.Rentals.ToString(CultureInfo.InvariantCulture), true) + Td("Days", r.Days.ToString(CultureInfo.InvariantCulture), true) + Td("Rental", Rental.Money(r.Rate), true) + Td("Extras", Rental.Money(r.Extras), true) + Td("Charges", Rental.Money(r.Charges), true) + Td("Before tax", Rental.Money(r.Sub), true) + Td("Tax", Rental.Money(r.Tax), true) + Td("Total", Rental.Money(r.Total), true) + Td("On rent", Pct(bc.Sum(x => x.Hours), bc.Sum(x => x.Open)), true) + "</tr>");
            }
            string totals = Td(string.Empty, "<b>Total</b>", false, true) + Td("Rentals", all.Rentals.ToString(CultureInfo.InvariantCulture), true) + Td("Days", all.Days.ToString(CultureInfo.InvariantCulture), true) + Td("Rental", Rental.Money(all.Rate), true) + Td("Extras", Rental.Money(all.Extras), true) + Td("Charges", Rental.Money(all.Charges), true) + Td("Before tax", Rental.Money(all.Sub), true) + Td("Tax", Rental.Money(all.Tax), true) + Td("Total", Rental.Money(all.Total), true) + Td("On rent", Pct(all.Hours, all.Open), true);
            sb.Append(Table("rt-r-branch", "Revenue by branch", Th("Branch", "#Rentals", "#Days", "#Rental", "#Extras", "#Charges", "#Before tax", "#Tax", "#Total", "#On rent"), body.ToString(), totals));

            body.Clear();
            foreach (ClassRec k in f.Classes)
            {
                Row r = new Row();
                foreach (BookingRec b in done.Where(b => b.ClassId == k.Id))
                {
                    Add(r, qs[b.Id]);
                }
                List<Row> kc = cars.Where(x => x.ClassId == k.Id).Select(x => byCar[x.Id]).ToList();
                if (r.Rentals == 0 && kc.Count == 0)
                {
                    continue;
                }
                body.Append("<tr>" + Td(string.Empty, "<b>" + Ui.Esc(k.Name) + "</b><small>" + Ui.Count(kc.Count, "car", "cars") + "</small>", false, true) + Td("Rentals", r.Rentals.ToString(CultureInfo.InvariantCulture), true) + Td("Days", r.Days.ToString(CultureInfo.InvariantCulture), true) + Td("Before tax", Rental.Money(r.Sub), true) + Td("Per rental day", r.Days > 0 ? Rental.Money(Math.Round(r.Sub / r.Days, 2)) : "&mdash;", true) + Td("On rent", Pct(kc.Sum(x => x.Hours), kc.Sum(x => x.Open)), true) + "</tr>");
            }
            sb.Append(Table("rt-r-class", "Revenue by class", Th("Class", "#Rentals", "#Days", "#Before tax", "#Per rental day", "#On rent"), body.ToString(), string.Empty));

            body.Clear();
            foreach (CarRec car in cars.OrderByDescending(x => byCar[x.Id].Sub).ThenBy(x => x.Plate))
            {
                Row r = byCar[car.Id];
                decimal net = r.Sub - r.Costs;
                body.Append("<tr>" + Td(string.Empty, "<b>" + Ui.Esc(car.Plate) + "</b><small>" + Ui.Esc(car.Make + " " + car.Model + " · " + Sheet.ClassName(f, car.ClassId) + " · " + Sheet.BranchName(f, car.BranchId) + (Avail.Gone.Contains(car.Status) ? " · " + car.Status : string.Empty)) + "</small>", false, true) + Td("Rentals", r.Rentals.ToString(CultureInfo.InvariantCulture), true) + Td("Days", r.Days.ToString(CultureInfo.InvariantCulture), true) + Td("On rent", Pct(r.Hours, r.Open), true) + Td("Before tax", Rental.Money(r.Sub), true) + Td("Service and repair", Rental.Money(r.Costs), true) + Td("Net", "<span class=\"" + (net < 0 ? "rt-neg" : string.Empty) + "\">" + Rental.Money(net) + "</span>", true) + "</tr>");
            }
            sb.Append(Table("rt-r-car", "Revenue by car", Th("Car", "#Rentals", "#Days", "#On rent", "#Before tax", "#Service and repair", "#Net"), body.ToString(), string.Empty));

            body.Clear();
            foreach (IGrouping<string, ChargeRec> g in done.SelectMany(b => b.Charges).GroupBy(x => x.Kind).OrderByDescending(g => g.Sum(x => x.Amount)))
            {
                string name = g.Key switch { "fuel" => "Fuel refills", "miles" => "Extra miles", "late" => "Late returns", "cleaning" => "Cleaning", "damage" => "Damage", _ => g.Key };
                body.Append("<tr>" + Td(string.Empty, "<b>" + Ui.Esc(name) + "</b>", false, true) + Td("Times", g.Count().ToString(CultureInfo.InvariantCulture), true) + Td("Amount", Rental.Money(g.Sum(x => x.Amount)), true) + "</tr>");
            }
            sb.Append("<div class=\"rt-rtwo\">" + Table("rt-r-charges", "Return charges", Th("Charge", "#Times", "#Amount"), body.Length > 0 ? body.ToString() : "<tr><td class=\"rt-first\" colspan=\"3\">No return charges in this period.</td></tr>", string.Empty));

            body.Clear();
            foreach (IGrouping<(string Payer, string Method), PaymentRec> g in pays.GroupBy(p => (p.Payer, p.Method)).OrderBy(g => g.Key.Payer).ThenByDescending(g => g.Sum(p => p.Amount)))
            {
                body.Append("<tr>" + Td(string.Empty, "<b>" + Ui.Esc(g.Key.Payer == "Insurer" ? "Insurers" : "Customers") + "</b><small>" + Ui.Esc(g.Key.Method) + "</small>", false, true) + Td("Payments", g.Count().ToString(CultureInfo.InvariantCulture), true) + Td("Amount", Rental.Money(g.Sum(p => p.Amount)), true) + "</tr>");
            }
            sb.Append(Table("rt-r-pay", "Payments received", Th("Payer and method", "#Payments", "#Amount"), body.Length > 0 ? body.ToString() : "<tr><td class=\"rt-first\" colspan=\"3\">No payments in this period.</td></tr>", pays.Count > 0 ? Td(string.Empty, "<b>Total</b>", false, true) + Td("Payments", pays.Count.ToString(CultureInfo.InvariantCulture), true) + Td("Amount", Rental.Money(pays.Sum(p => p.Amount)), true) : string.Empty) + "</div>");

            body.Clear();
            foreach ((BookingRec b, Split s) in owing)
            {
                body.Append("<tr>" + Td(string.Empty, "<b><a class=\"rt-lnk\" href=\"AdminBookings?code=" + Ui.Esc(b.Code) + "&amp;st=all\">" + Ui.Esc(b.Code) + "</a></b><small>" + Ui.Esc(Rental.Name(b) + " · " + Sheet.BranchName(f, b.BranchId)) + "</small>", false, true) + Td("Status", Ui.Esc(Avail.Overdue(b, now) ? "Overdue" : b.Status), false) + Td("Returned", Ui.Esc(b.ReturnedAt == null ? "Not yet" : Rental.Day(b.ReturnedAt.Value)), false) + Td("Customer owes", Rental.Money(Math.Max(0, s.DueCustomer)), true) + Td("Insurer owes", s.Insurer > 0 ? Rental.Money(Math.Max(0, s.DueInsurer)) + "<small>" + Ui.Esc(s.InsurerName) + "</small>" : "&mdash;", true) + "</tr>");
            }
            sb.Append(Table("rt-r-owe", "Open balances today", Th("Booking", "Status", "Returned", "#Customer owes", "#Insurer owes"), body.Length > 0 ? body.ToString() : "<tr><td class=\"rt-first\" colspan=\"5\">Nothing is owed.</td></tr>", string.Empty));

            body.Clear();
            foreach (BookingRec b in done.OrderBy(b => b.ReturnedAt).Take(500))
            {
                Quote q = qs[b.Id];
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == b.CarId);
                body.Append("<tr>" + Td(string.Empty, "<b>" + Ui.Esc(b.Code) + "</b><small>" + Ui.Esc(Rental.Name(b)) + "</small>", false, true) + Td("Returned", Ui.Esc(Rental.Day(b.ReturnedAt!.Value)), false) + Td("Car", Ui.Esc((car?.Plate ?? string.Empty) + " · " + Sheet.ClassName(f, b.ClassId)), false) + Td("Days", q.Days.ToString(CultureInfo.InvariantCulture), true) + Td("Before tax", Rental.Money(q.Subtotal), true) + Td("Total", Rental.Money(q.Total), true) + Td("Paid by", Ui.Esc(b.PayerKind == "insurer" ? f.Insurers.FirstOrDefault(i => i.Id == b.InsurerId)?.Name ?? "Insurer" : "Customer"), false) + "</tr>");
            }
            sb.Append(Table("rt-r-hist", "Rentals returned", Th("Booking", "Returned", "Car", "#Days", "#Before tax", "#Total", "Paid by"), body.Length > 0 ? body.ToString() : "<tr><td class=\"rt-first\" colspan=\"7\">No rentals were returned in this period.</td></tr>", string.Empty));
            return sb.ToString();
        }
    }
}
