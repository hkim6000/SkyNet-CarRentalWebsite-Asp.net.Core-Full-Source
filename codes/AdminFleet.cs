using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminFleet : WebPage
    {
        private const string AreaKey = "fleet";
        private const int Size = 12;
        private static readonly string[] HoldFrom = { "Available", "Reserved", "Cleaning", "Hold" };
        private static readonly string[] ReadyFrom = { "Hold", "Cleaning" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Fleet | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminFleetJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminFleetJs.denied();");
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
            response.SetElementContents("ft-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ft-menu", await Auth.Menu("ft", "AdminFleet", c));
            response.SetElementContents("ft-who", Auth.Who("ft", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminFleetJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"ft-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"ft-btn ft-btn-ghost ft-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminFleetJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"ft-btn ft-btn-ghost ft-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminFleetJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"ft-pill ft-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"ft-err\" id=\"ft-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"ft-field\" for=\"ft-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"ft-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static List<BranchRec> MyBranches(Ctx c, FleetData f)
        {
            return f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).ToList();
        }

        private static bool Mine(Ctx c, FleetData f, CarRec car)
        {
            return Access.BranchOk(c.U, car.BranchId) || f.Transfers.Any(t => t.CarId == car.Id && (t.Status == "Planned" || t.Status == "In transit") && Access.BranchOk(c.U, t.To));
        }

        private static string Status(CarRec c)
        {
            return c.Status == "In transit" ? "In transfer" : c.Status;
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
            string st = Ui.Clip(GetQ("st"), 30);
            string fl = Ui.Clip(GetQ("fl"), 30);
            StringBuilder sb = new StringBuilder("<div class=\"ft-ph\"><div><h1>Fleet</h1><p>Every car with its status and flags. Status changes come from actions: bookings, check-out, check-in and transfers. By hand you can only put a car on hold or mark it ready, with a reason.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"ft-btn\" onclick=\"AdminFleetJs.add()\">Add a car</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"ft-tiles\" id=\"ft-tiles\">" + Tiles(c, f) + "</div>");
            sb.Append("<div class=\"ft-tools\"><input type=\"search\" id=\"ft-q\" placeholder=\"Plate, VIN, make or model\" maxlength=\"40\" oninput=\"AdminFleetJs.type()\" aria-label=\"Search cars\">");
            sb.Append("<select id=\"ft-fb\" onchange=\"AdminFleetJs.list(true)\" aria-label=\"Branch\">" + Opt(string.Empty, "All my branches", string.Empty) + string.Concat(MyBranches(c, f).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select>");
            sb.Append("<select id=\"ft-fc\" onchange=\"AdminFleetJs.list(true)\" aria-label=\"Class\">" + Opt(string.Empty, "All classes", string.Empty) + string.Concat(f.Classes.Select(x => Opt(x.Id, x.Name, string.Empty))) + "</select>");
            sb.Append("<select id=\"ft-fs\" onchange=\"AdminFleetJs.list(true)\" aria-label=\"Status\">" + Opt(string.Empty, "Any status", st) + string.Concat(Avail.Statuses.Select(s => Opt(s, s == "In transit" ? "In transfer" : s, st))) + "</select>");
            sb.Append("<select id=\"ft-ff\" onchange=\"AdminFleetJs.list(true)\" aria-label=\"Flag\">" + Opt(string.Empty, "Any flag", fl) + string.Concat(Ops.FlagNames.Select(s => Opt(s, s, fl))) + "</select></div>");
            sb.Append("<div id=\"ft-list\">" + ListHtml(c, f, string.Empty, string.Empty, string.Empty, st, fl, 1) + "</div>");
            response.SetElementContents("ft-main", sb.ToString());
            return response;
        }

        private string GetQ(string k)
        {
            return GetDataValue(k) ?? string.Empty;
        }

        private static string Tiles(Ctx c, FleetData f)
        {
            List<CarRec> mine = f.Cars.Where(x => Mine(c, f, x) && !Avail.Gone.Contains(x.Status)).ToList();
            StringBuilder sb = new StringBuilder();
            foreach (string s in new[] { "Available", "Reserved", "On rent", "Cleaning", "Maintenance", "Repair", "Hold", "In transit" })
            {
                int n = mine.Count(x => x.Status == s);
                sb.Append("<button type=\"button\" class=\"ft-tile" + ((s == "Repair" || s == "Maintenance") && n > 0 ? " ft-warn" : string.Empty) + "\" onclick=\"AdminFleetJs.byStatus('" + s + "')\"><b>" + n + "</b><span>" + (s == "In transit" ? "In transfer" : s) + "</span></button>");
            }
            return sb.ToString();
        }

        private static string NextUse(FleetData f, CarRec car)
        {
            DateTime now = DateTime.Now;
            BookingRec? b = f.Bookings.Where(x => x.CarId == car.Id && Avail.Live(x) && Avail.EndOf(x, now) > now).OrderBy(x => x.Start).FirstOrDefault();
            if (b == null)
            {
                return "<small>None</small>";
            }
            return Avail.Out(b) ? Ui.Esc(b.Code) + "<small>Due back " + Ui.Esc(Rental.When(b.End)) + "</small>" : Ui.Esc(b.Code) + "<small>" + Ui.Esc(Rental.When(b.Start)) + "</small>";
        }

        private static string ListHtml(Ctx c, FleetData f, string term, string branch, string cls, string status, string flag, int page)
        {
            List<CarRec> rows = f.Cars.Where(x => Mine(c, f, x)
                && (branch == string.Empty || x.BranchId == branch || f.Transfers.Any(t => t.CarId == x.Id && t.To == branch && (t.Status == "Planned" || t.Status == "In transit")))
                && (cls == string.Empty || x.ClassId == cls)
                && (status == string.Empty ? !Avail.Gone.Contains(x.Status) : x.Status == status)
                && (flag == string.Empty || Ops.Flags(f, x).Contains(flag))
                && (term == string.Empty || Ui.Has(x.Plate, term) || Ui.Has(x.Vin, term) || Ui.Has(x.Make + " " + x.Model, term) || Ui.Has(x.Color, term))).OrderBy(x => f.Classes.FindIndex(k => k.Id == x.ClassId)).ThenBy(x => x.Plate).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ft-panel ft-empty\">No cars match.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"ft-wrapt\"><table class=\"ft-table\"><thead><tr><th>Car</th><th>Class</th><th>Where</th><th>Status</th><th class=\"ft-num\">Mileage</th><th>Next booking</th></tr></thead><tbody>");
            foreach (CarRec x in rows.Skip((page - 1) * Size).Take(Size))
            {
                List<string> flags = Ops.Flags(f, x);
                sb.Append("<tr class=\"ft-row\" onclick=\"AdminFleetJs.edit('" + Ui.Js(x.Id) + "')\"><td class=\"ft-first\"><b>" + Ui.Esc(x.Plate) + "</b><small>" + Ui.Esc(x.Year + " " + x.Make + " " + x.Model + " · " + x.Color) + "</small></td>");
                sb.Append("<td data-label=\"Class\">" + Ui.Esc(f.Classes.FirstOrDefault(k => k.Id == x.ClassId)?.Name ?? x.ClassId) + "</td><td data-label=\"Where\">" + Ui.Esc(Avail.Location(f, x)) + "</td>");
                sb.Append("<td data-label=\"Status\"><div>" + Pill(Status(x)) + (flags.Count > 0 ? "<div class=\"ft-flags\">" + string.Concat(flags.Select(g => "<span class=\"ft-flag\">" + Ui.Esc(g) + "</span>")) + "</div>" : string.Empty) + "</div></td>");
                sb.Append("<td data-label=\"Mileage\" class=\"ft-num\">" + x.Mileage.ToString("N0", Rental.Us) + "</td><td data-label=\"Next booking\">" + NextUse(f, x) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "car", "cars"));
            return sb.ToString();
        }

        private async Task<(string List, string Tiles)> Now(Ctx c)
        {
            FleetData f = await Avail.LoadSettled();
            return (ListHtml(c, f, Term(GetDataValue("term")), Ui.Clip(GetDataValue("fb"), 60), Ui.Clip(GetDataValue("fc"), 60), Ui.Clip(GetDataValue("fs"), 30), Ui.Clip(GetDataValue("ff"), 30), PageNo(GetDataValue("pg"))), Tiles(c, f));
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            (string list, string tiles) = await Now(c);
            response.SetElementContents("ft-list", list);
            response.SetElementContents("ft-tiles", tiles);
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            await Refresh(response, c);
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
            CarRec? car = f.Cars.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (car == null || !Mine(c, f, car)))
            {
                return await Refuse(response, c, "open car", id, "That car was not found at your branches.");
            }
            if (car == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add car", string.Empty, "You can view the fleet but not add cars.");
            }
            response.SetElementContents("ft-mbody", FormHtml(c, f, car));
            response.ExecuteScript("AdminFleetJs.opened('" + Ui.Js(car == null ? "New car" : car.Plate + " · " + car.Make + " " + car.Model) + "');");
            return response;
        }

        private static string D(DateTime d)
        {
            return d == default ? string.Empty : d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string FormHtml(Ctx c, FleetData f, CarRec? x)
        {
            bool isNew = x == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E') && Access.BranchOk(c.U, x!.BranchId);
            CarRec r = x ?? new CarRec { Year = DateTime.Today.Year, Fuel = 8, Purchased = DateTime.Today, RegistrationDue = DateTime.Today.AddYears(1) };
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder();
            if (!isNew)
            {
                List<string> flags = Ops.Flags(f, r);
                sb.Append("<div class=\"ft-bar\"><div><div>" + Pill(Status(r)) + " <span class=\"ft-mini\">since " + Ui.Esc(Rental.When(r.StatusSince)) + (r.BlockUntil != null ? " &middot; until " + Ui.Esc(Rental.When(r.BlockUntil.Value)) : string.Empty) + "</span></div>" + (r.StatusNote.Length > 0 ? "<div class=\"ft-mini\">" + Ui.Esc(r.StatusNote) + "</div>" : string.Empty) + (flags.Count > 0 ? "<div class=\"ft-flags\">" + string.Concat(flags.Select(g => "<span class=\"ft-flag\">" + Ui.Esc(g) + "</span>")) + "</div>" : string.Empty) + "</div><span class=\"ft-mini\">" + Ui.Esc(Avail.Location(f, r)) + " &middot; fuel " + Ui.Esc(Rental.FuelText(r.Fuel)) + "</span></div>");
                if (edit && (HoldFrom.Contains(r.Status) || ReadyFrom.Contains(r.Status)))
                {
                    sb.Append("<div class=\"ft-panel ft-stat\" id=\"ft-stat\"><p class=\"ft-sec\" style=\"border:0;padding:0;margin:0 0 10px\">Change status by hand</p><div class=\"ft-grid2\">" + Field("reason", "Reason (required)", Text("reason", string.Empty, 120)) + (HoldFrom.Contains(r.Status) ? Field("until", "Hold until (leave empty for no end date)", "<input type=\"datetime-local\" id=\"ft-f-until\" data-k=\"until\">") : string.Empty) + "</div><div class=\"ft-acts\">");
                    if (HoldFrom.Contains(r.Status))
                    {
                        sb.Append("<button type=\"button\" class=\"ft-btn ft-btn-ghost\" onclick=\"AdminFleetJs.hold('" + Ui.Js(r.Id) + "')\">" + (r.Status == "Hold" ? "Update hold" : "Put on hold") + "</button>");
                    }
                    if (ReadyFrom.Contains(r.Status))
                    {
                        sb.Append("<button type=\"button\" class=\"ft-btn\" onclick=\"AdminFleetJs.ready('" + Ui.Js(r.Id) + "')\">Mark ready</button>");
                    }
                    sb.Append("</div></div>");
                }
            }
            sb.Append("<form class=\"ft-stack\" id=\"ft-form\" onsubmit=\"return AdminFleetJs.save()\" novalidate><input type=\"hidden\" id=\"ft-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            sb.Append("<div class=\"ft-grid3\">" + Field("plate", "Plate", Text("plate", r.Plate, 10, "text", ro + " autocapitalize=\"characters\"")) + Field("vin", "VIN (17 characters)", Text("vin", r.Vin, 17, "text", ro + " autocapitalize=\"characters\"")) + Field("color", "Colour", Text("color", r.Color, 20, "text", ro)) + "</div>");
            sb.Append("<div class=\"ft-grid3\">" + Field("make", "Make", Text("make", r.Make, 30, "text", ro)) + Field("model", "Model", Text("model", r.Model, 30, "text", ro)) + Field("year", "Year", Text("year", r.Year.ToString(CultureInfo.InvariantCulture), 4, "number", ro + " min=\"1990\" max=\"2100\"")) + "</div>");
            sb.Append("<div class=\"ft-grid2\">" + Field("cls", "Class", "<select id=\"ft-f-cls\" data-k=\"cls\"" + dis + ">" + string.Concat(f.Classes.Select(k => Opt(k.Id, k.Name, r.ClassId))) + "</select>"));
            if (isNew)
            {
                sb.Append(Field("branch", "Home branch", "<select id=\"ft-f-branch\" data-k=\"branch\"" + dis + ">" + string.Concat(MyBranches(c, f).Where(b => b.Open).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select>"));
            }
            else
            {
                sb.Append("<div class=\"ft-field\"><span>Branch</span><input type=\"text\" value=\"" + Ui.Esc(Avail.Location(f, r)) + "\" readonly><small>To move the car, plan a transfer.</small></div>");
            }
            sb.Append("</div><div class=\"ft-grid3\">" + Field("mileage", "Mileage", Text("mileage", r.Mileage.ToString(CultureInfo.InvariantCulture), 7, "number", ro + " min=\"0\"")) + Field("fuel", "Fuel now", "<select id=\"ft-f-fuel\" data-k=\"fuel\"" + dis + ">" + string.Concat(Enumerable.Range(0, 9).Reverse().Select(n => Opt(n.ToString(CultureInfo.InvariantCulture), Rental.FuelText(n), r.Fuel.ToString(CultureInfo.InvariantCulture)))) + "</select>") + Field("service", "Next service at (miles)", Text("service", r.ServiceMiles > 0 ? r.ServiceMiles.ToString(CultureInfo.InvariantCulture) : string.Empty, 7, "number", ro + " min=\"0\"")) + "</div>");
            sb.Append("<div class=\"ft-grid3\">" + Field("regdue", "Registration due", "<input type=\"date\" id=\"ft-f-regdue\" data-k=\"regdue\" value=\"" + D(r.RegistrationDue) + "\"" + ro + ">") + Field("bought", "Purchase date", "<input type=\"date\" id=\"ft-f-bought\" data-k=\"bought\" value=\"" + D(r.Purchased) + "\"" + ro + ">") + Field("cost", "Purchase cost (US dollars)", Text("cost", Num(r.Cost), 10, "text", ro + " inputmode=\"decimal\"")) + "</div>");
            sb.Append("<label class=\"ft-field\" for=\"ft-f-note\"><span>Staff note</span><textarea id=\"ft-f-note\" data-k=\"note\" rows=\"2\" maxlength=\"300\"" + ro + ">" + Ui.Esc(r.Note) + "</textarea>" + Err("note") + "</label>");
            if (!isNew)
            {
                DateTime now = DateTime.Now;
                List<BookingRec> next = f.Bookings.Where(b => b.CarId == r.Id && Avail.Live(b)).OrderBy(b => b.Start).Take(6).ToList();
                sb.Append("<p class=\"ft-sec\">Bookings on this car</p>" + (next.Count == 0 ? "<p class=\"ft-hint\">No active bookings.</p>" : "<div class=\"ft-wrapt\"><table class=\"ft-table\"><thead><tr><th>Booking</th><th>Pick-up</th><th>Return</th><th>Status</th></tr></thead><tbody>" + string.Concat(next.Select(b => "<tr><td class=\"ft-first\"><b>" + Ui.Esc(b.Code) + "</b><small>" + Ui.Esc(Rental.Name(b)) + "</small></td><td data-label=\"Pick-up\">" + Ui.Esc(Rental.When(b.Start)) + "</td><td data-label=\"Return\">" + Ui.Esc(Rental.When(b.End)) + "</td><td data-label=\"Status\">" + Pill(Avail.Overdue(b, now) ? "Overdue" : b.Status) + "</td></tr>")) + "</tbody></table></div>") + "<p class=\"ft-hint\"><a href=\"AdminCalendar?car=" + Ui.Esc(r.Id) + "\" style=\"text-decoration:underline\">Open this car's calendar</a></p>" + History(c, f, r));
            }
            sb.Append("<div class=\"ft-acts\">" + (edit ? "<button type=\"submit\" class=\"ft-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"ft-btn ft-btn-ghost\" onclick=\"AdminFleetJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') && Access.BranchOk(c.U, r.BranchId) ? "<button type=\"button\" class=\"ft-btn ft-btn-danger ft-push\" onclick=\"AdminFleetJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Plate) + "')\">Delete</button>" : string.Empty) + "</div></form>");
            return sb.ToString();
        }

        private static string History(Ctx c, FleetData f, CarRec r)
        {
            List<(DateTime When, string What, string Status, string Link)> rows = new List<(DateTime, string, string, string)>();
            rows.AddRange(f.Services.Where(v => v.CarId == r.Id).Select(v => (v.Start, "Service: " + v.Kind + (v.Shop.Length > 0 ? " · " + v.Shop : string.Empty), v.Status, "AdminService")));
            rows.AddRange(f.Repairs.Where(x => x.CarId == r.Id).Select(x => (x.Opened, "Repair: " + x.Problem, x.Status, "AdminRepairs")));
            rows.AddRange(f.Incidents.Where(x => x.CarId == r.Id).Select(x => (x.When, x.Kind + (x.PoliceReport.Length > 0 ? " · police report " + x.PoliceReport : string.Empty), x.Status, "AdminIncidents")));
            rows.AddRange(f.Sales.Where(x => x.CarId == r.Id).Select(x => (x.Date, "Sold to " + x.Buyer + " for " + Rental.Money(x.Price), "Sold", "AdminSales")));
            StringBuilder sb = new StringBuilder("<p class=\"ft-sec\">Service, repairs and incidents</p>");
            List<string> acts = new List<string>();
            bool gone = Avail.Gone.Contains(r.Status);
            if (!gone && Access.Can(c.U, "service", 'A'))
            {
                acts.Add("<a class=\"ft-btn ft-btn-ghost ft-btn-sm\" href=\"AdminService?car=" + Ui.Esc(r.Id) + "\">Plan a service</a>");
            }
            if (!gone && Access.Can(c.U, "repairs", 'A'))
            {
                acts.Add("<a class=\"ft-btn ft-btn-ghost ft-btn-sm\" href=\"AdminRepairs?car=" + Ui.Esc(r.Id) + "\">Open a repair</a>");
            }
            if (Access.Can(c.U, "incidents", 'A'))
            {
                acts.Add("<a class=\"ft-btn ft-btn-ghost ft-btn-sm\" href=\"AdminIncidents?car=" + Ui.Esc(r.Id) + "\">Report an incident</a>");
            }
            if (!gone && Access.Can(c.U, "sales", 'A'))
            {
                acts.Add("<a class=\"ft-btn ft-btn-ghost ft-btn-sm\" href=\"AdminSales?car=" + Ui.Esc(r.Id) + "\">Sell this car</a>");
            }
            if (rows.Count == 0)
            {
                sb.Append("<p class=\"ft-hint\">Nothing recorded yet.</p>");
            }
            else
            {
                sb.Append("<div class=\"ft-wrapt\"><table class=\"ft-table\"><thead><tr><th>Date</th><th>What</th><th>Status</th></tr></thead><tbody>" + string.Concat(rows.OrderByDescending(x => x.When).Take(10).Select(x => "<tr><td class=\"ft-first\"><b>" + Ui.Esc(Rental.Day(x.When)) + "</b></td><td data-label=\"What\">" + Ui.Esc(x.What) + "</td><td data-label=\"Status\">" + Pill(x.Status) + "</td></tr>")) + "</tbody></table></div>");
            }
            if (acts.Count > 0)
            {
                sb.Append("<div class=\"ft-acts-row\" style=\"justify-content:flex-start;margin-top:10px\">" + string.Concat(acts) + "</div>");
            }
            return sb.ToString();
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
                return await Refuse(response, c, isNew ? "add car" : "edit car", id, "You do not have the right to " + (isNew ? "add" : "change") + " cars.");
            }
            string plate = Ui.Clip(GetDataValue("plate"), 10).ToUpperInvariant();
            string vin = Ui.Clip(GetDataValue("vin"), 17).ToUpperInvariant();
            string color = Ui.Clip(GetDataValue("color"), 20);
            string make = Ui.Clip(GetDataValue("make"), 30);
            string model = Ui.Clip(GetDataValue("model"), 30);
            int year = Int(GetDataValue("year"), 0);
            string cls = Ui.Clip(GetDataValue("cls"), 60);
            string branch = Ui.Clip(GetDataValue("branch"), 60);
            int mileage = Int(GetDataValue("mileage"), -1);
            int fuel = Int(GetDataValue("fuel"), -1);
            string serviceRaw = Ui.Clip(GetDataValue("service"), 9);
            int service = serviceRaw.Length == 0 ? 0 : Int(serviceRaw, -1);
            bool regOk = Rental.ParseDate(GetDataValue("regdue"), out DateTime regdue);
            bool boughtOk = Rental.ParseDate(GetDataValue("bought"), out DateTime bought);
            decimal cost = Dec(GetDataValue("cost"));
            string note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 300);
            string moved = string.Empty;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == id);
                if (!isNew && (car == null || !Access.BranchOk(c.U, car.BranchId)))
                {
                    return await Refuse(response, c, "edit car", id, "That car was not found at your branches.");
                }
                int lastSeen = car == null ? 0 : await LastMileage(car.Id);
                Dictionary<string, string> e = new Dictionary<string, string>
                {
                    ["plate"] = plate.Length < 2 || !plate.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == ' ') ? "Please enter the plate (letters, numbers, dashes)." : f.Cars.Any(x => x.Id != id && x.Plate == plate) ? "Another car already has this plate." : string.Empty,
                    ["vin"] = vin.Length != 17 || !vin.All(char.IsLetterOrDigit) || vin.Any(ch => ch == 'I' || ch == 'O' || ch == 'Q') ? "A VIN has 17 letters and numbers, without I, O or Q." : f.Cars.Any(x => x.Id != id && x.Vin == vin) ? "Another car already has this VIN." : string.Empty,
                    ["color"] = color.Length < 2 ? "Please enter the colour." : string.Empty,
                    ["make"] = make.Length < 2 ? "Please enter the make." : string.Empty,
                    ["model"] = model.Length < 1 ? "Please enter the model." : string.Empty,
                    ["year"] = year < 1990 || year > DateTime.Today.Year + 1 ? "Please enter the model year." : string.Empty,
                    ["cls"] = !f.Classes.Any(k => k.Id == cls) ? "Choose a class." : string.Empty,
                    ["branch"] = isNew && (!f.Branches.Any(b => b.Id == branch && b.Open) || !Access.BranchOk(c.U, branch)) ? "Choose one of your open branches." : string.Empty,
                    ["mileage"] = mileage < 0 || mileage > 999999 ? "Enter the mileage." : mileage < lastSeen ? "Mileage cannot be lower than the last inspection (" + lastSeen.ToString("N0", Rental.Us) + ")." : string.Empty,
                    ["fuel"] = fuel < 0 || fuel > 8 ? "Choose the fuel level." : string.Empty,
                    ["service"] = service < 0 || service > 999999 ? "Enter miles, or leave empty." : string.Empty,
                    ["regdue"] = !regOk ? "Enter the registration due date." : string.Empty,
                    ["bought"] = !boughtOk || bought > DateTime.Today ? "Enter the purchase date." : string.Empty,
                    ["cost"] = cost < 0 ? "Enter the purchase cost, or 0." : string.Empty,
                    ["note"] = string.Empty
                };
                foreach (KeyValuePair<string, string> kv in e)
                {
                    response.SetElementContents("ft-e-" + kv.Key, kv.Value);
                }
                if (e.Values.Any(v => v.Length > 0))
                {
                    return response;
                }
                if (isNew)
                {
                    car = new CarRec { Id = Rental.NewId("c-"), BranchId = branch, Status = "Available", StatusSince = DateTime.Now };
                    f.Cars.Add(car);
                }
                CarRec t = car!;
                if (!isNew && t.ClassId != cls)
                {
                    string old = t.ClassId;
                    t.ClassId = cls;
                    (string err, List<BookingRec> m) = Ops.Reflow(f, t, DateTime.Now, null, string.Empty);
                    if (err.Length > 0)
                    {
                        t.ClassId = old;
                        return await Refuse(response, c, "edit car", t.Plate, "The class cannot change while bookings need this car. " + err);
                    }
                    moved = m.Count > 0 ? " " + Ui.Count(m.Count, "booking moved", "bookings moved") + " to other cars: " + string.Join(", ", m.Select(b => b.Code)) + "." : string.Empty;
                }
                t.Plate = plate;
                t.Vin = vin;
                t.Color = color;
                t.Make = make;
                t.Model = model;
                t.Year = year;
                t.ClassId = cls;
                t.Mileage = mileage;
                t.Fuel = fuel;
                t.ServiceMiles = service;
                t.RegistrationDue = regdue;
                t.Purchased = bought;
                t.Cost = cost;
                t.Note = note;
                target = t.Plate;
                Avail.Settle(f);
                await Ops.Persist(f, true, true, false);
            }
            await Store.Log(c.U.Id, isNew ? "add car" : "edit car", target + moved, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminFleetJs.saved();" + (moved.Length > 0 ? "AdminFleetJs.toast('" + Ui.Js("Saved." + moved) + "');" : string.Empty));
            return response;
        }

        private static async Task<int> LastMileage(string carId)
        {
            List<InspectionRec> ins = await Store.Rows<InspectionRec>("inspections");
            return ins.Where(i => i.CarId == carId).Select(i => i.Mileage).DefaultIfEmpty(0).Max();
        }

        public async Task<ApiResponse> HoldCar()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            string reason = Ui.Clip(GetDataValue("reason"), 120);
            string untilRaw = Ui.Clip(GetDataValue("until"), 20);
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "hold car", id, "You do not have the right to change cars.");
            }
            DateTime? until = null;
            if (untilRaw.Length > 0)
            {
                if (!Rental.ParseLocal(untilRaw, out DateTime u) || u <= DateTime.Now)
                {
                    response.SetElementContents("ft-e-until", "Choose a time in the future, or leave it empty.");
                    return response;
                }
                until = u;
            }
            response.SetElementContents("ft-e-until", string.Empty);
            if (reason.Length < 3)
            {
                response.SetElementContents("ft-e-reason", "Please give a reason.");
                return response;
            }
            response.SetElementContents("ft-e-reason", string.Empty);
            string plate;
            string moved;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == id);
                if (car == null || !Access.BranchOk(c.U, car.BranchId))
                {
                    return await Refuse(response, c, "hold car", id, "That car was not found at your branches.");
                }
                if (!HoldFrom.Contains(car.Status))
                {
                    return await Refuse(response, c, "hold car", car.Plate, "A car that is " + Status(car).ToLowerInvariant() + " cannot be put on hold.");
                }
                (string err, List<BookingRec> m) = Ops.Reflow(f, car, DateTime.Now, until, string.Empty);
                if (err.Length > 0)
                {
                    return await Refuse(response, c, "hold car", car.Plate, "Cannot hold " + car.Plate + ". " + err);
                }
                car.Status = "Hold";
                car.StatusNote = reason;
                car.BlockUntil = until;
                car.StatusSince = DateTime.Now;
                plate = car.Plate;
                moved = m.Count > 0 ? " " + Ui.Count(m.Count, "booking moved", "bookings moved") + " to other cars: " + string.Join(", ", m.Select(b => b.Code)) + "." : string.Empty;
                await Ops.Persist(f, true, true, false);
            }
            await Store.Log(c.U.Id, "hold car", plate + " · " + reason + (until != null ? " · until " + Rental.When(until.Value) : string.Empty) + moved, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminFleetJs.shut();AdminFleetJs.toast('" + Ui.Js(plate + " is on hold." + moved) + "');");
            return response;
        }

        public async Task<ApiResponse> Ready()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            string reason = Ui.Clip(GetDataValue("reason"), 120);
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "car ready", id, "You do not have the right to change cars.");
            }
            if (reason.Length < 3)
            {
                response.SetElementContents("ft-e-reason", "Please give a reason.");
                return response;
            }
            response.SetElementContents("ft-e-reason", string.Empty);
            string plate;
            string from;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == id);
                if (car == null || !Access.BranchOk(c.U, car.BranchId))
                {
                    return await Refuse(response, c, "car ready", id, "That car was not found at your branches.");
                }
                if (!ReadyFrom.Contains(car.Status))
                {
                    return await Refuse(response, c, "car ready", car.Plate, "Only a car on hold or in cleaning can be marked ready. Finish repairs on Repairs and services on Maintenance.");
                }
                from = car.Status;
                car.Status = "Available";
                car.StatusNote = string.Empty;
                car.BlockUntil = null;
                car.StatusSince = DateTime.Now;
                plate = car.Plate;
                Avail.Settle(f);
                await Ops.Persist(f, false, true, false);
            }
            await Store.Log(c.U.Id, "car ready", plate + " · from " + from + " · " + reason, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminFleetJs.shut();AdminFleetJs.toast('" + Ui.Js(plate + " is ready to rent.") + "');");
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
                return await Refuse(response, c, "delete car", id, "You do not have the right to delete cars.");
            }
            string plate;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == id);
                if (car == null || !Access.BranchOk(c.U, car.BranchId))
                {
                    return await Refuse(response, c, "delete car", id, "That car was not found at your branches.");
                }
                if (f.Bookings.Any(b => b.CarId == id) || f.Transfers.Any(t => t.CarId == id))
                {
                    return await Refuse(response, c, "delete car", car.Plate, car.Plate + " has bookings or transfers in its records, so it cannot be deleted. Put it on hold instead.");
                }
                plate = car.Plate;
                f.Cars.Remove(car);
                await Ops.Persist(f, false, true, false);
            }
            await Store.Log(c.U.Id, "delete car", plate, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminFleetJs.deleted();");
            return response;
        }
    }
}
