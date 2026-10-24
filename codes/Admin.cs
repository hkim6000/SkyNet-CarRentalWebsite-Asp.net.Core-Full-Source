using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class Admin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Dashboard | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
            }
            return c;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-brand", Ui.Esc(site.Brand));
            response.SetElementContents("db-menu", await Auth.Menu("db", "Admin", c));
            response.SetElementContents("db-who", Auth.Who("db", c));
        }

        private static string Tile(string href, int number, string label, string note, bool warn)
        {
            return "<a class=\"db-tile" + (warn && number > 0 ? " db-warn" : string.Empty) + "\" href=\"" + href + "\"><b>" + number.ToString(CultureInfo.InvariantCulture) + "</b><span>" + Ui.Esc(label) + "</span><small>" + Ui.Esc(note) + "</small></a>";
        }

        private static string Pill(string status)
        {
            string k = status switch
            {
                "Overdue" => "bad",
                "Reserved" or "Late pick-up" => "new",
                "Picked up" or "Extended" => "wait",
                _ => "go"
            };
            return "<span class=\"db-pill db-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
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
            DateTime now = DateTime.Now;
            DateTime today = DateTime.Today;
            List<BookingRec> mine = f.Bookings.Where(b => Access.BranchOk(c.U, b.BranchId)).ToList();
            List<CarRec> cars = f.Cars.Where(x => Access.BranchOk(c.U, x.BranchId) && !Avail.Gone.Contains(x.Status)).ToList();
            bool desk = Access.Can(c.U, "desk", 'V');
            bool book = Access.Can(c.U, "bookings", 'V');
            bool fleet = Access.Can(c.U, "fleet", 'V');
            bool msgs = Access.Can(c.U, "messages", 'V');
            List<BookingRec> outs = mine.Where(b => b.Status == "Reserved" && b.Start < today.AddDays(1)).OrderBy(b => b.Start).ToList();
            List<BookingRec> ins = mine.Where(b => Avail.Out(b) && b.End < today.AddDays(1)).OrderBy(b => b.End).ToList();
            int overdue = mine.Count(b => Avail.Overdue(b, now));
            int asks = mine.Count(b => Avail.Live(b) && b.AskEnd != null);
            int noCar = mine.Count(b => b.Status == "Reserved" && !f.Cars.Any(x => x.Id == b.CarId && Avail.CarFree(f, x, b.BranchId, b.Start, b.End, b.Id)));
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"db-ph\"><div><h1>Dashboard</h1><p>Welcome, " + Ui.Esc(c.U.Name) + ". " + Ui.Esc(Access.Describe(c.U, f.Branches)) + " &middot; " + Ui.Esc(Rental.Day(today)) + "</p></div><a class=\"db-btn db-btn-ghost\" href=\"Home\" target=\"_blank\" rel=\"noopener\">View the website</a></div>");
            StringBuilder tiles = new StringBuilder();
            if (desk)
            {
                tiles.Append(Tile("AdminDesk", outs.Count, outs.Count == 1 ? "pick-up today" : "pick-ups today", Ui.Count(outs.Count(b => b.Start < now), "is late", "are late"), false));
                tiles.Append(Tile("AdminDesk", ins.Count, ins.Count == 1 ? "return today" : "returns today", Ui.Count(mine.Count(Avail.Out), "car", "cars") + " on rent", false));
            }
            if (book)
            {
                tiles.Append(Tile("AdminBookings?st=overdue", overdue, "overdue", "Out past the return time", true));
                tiles.Append(Tile("AdminBookings?st=ask", asks, asks == 1 ? "extension request" : "extension requests", "Waiting for staff", true));
                tiles.Append(Tile("AdminBookings?st=nocar", noCar, noCar == 1 ? "booking needs a car" : "bookings need a car", "Its car is blocked or busy", true));
            }
            if (fleet)
            {
                tiles.Append(Tile("AdminFleet?st=Available", cars.Count(x => x.Status == "Available"), "cars available", Ui.Count(cars.Count, "car", "cars") + " in the fleet", false));
                tiles.Append(Tile("AdminFleet?st=Hold", cars.Count(x => x.Status == "Repair" || x.Status == "Maintenance" || x.Status == "Hold"), "off the road", "Repair, maintenance or hold", true));
                tiles.Append(Tile("AdminFleet?fl=Service+due", cars.Count(x => Ops.Flags(f, x).Contains("Service due")), "service due", Ui.Count(cars.Count(x => Ops.Flags(f, x).Contains("Registration due")), "registration due", "registrations due"), true));
            }
            if (Access.Can(c.U, "service", 'V'))
            {
                List<ServiceRec> sv = f.Services.Where(v => cars.Any(x => x.Id == v.CarId)).ToList();
                tiles.Append(Tile("AdminService", sv.Count(v => v.Status == "In progress") + sv.Count(v => v.Status == "Planned" && v.Start < today.AddDays(7)), "services this week", sv.Count(v => v.Status == "In progress") + " in progress", false));
            }
            if (Access.Can(c.U, "repairs", 'V'))
            {
                tiles.Append(Tile("AdminRepairs", f.Repairs.Count(r => (r.Status == "Open" || r.Status == "In shop") && cars.Any(x => x.Id == r.CarId)), "open repairs", "Cars off the road", true));
            }
            if (Access.Can(c.U, "incidents", 'V'))
            {
                List<IncidentRec> ic = f.Incidents.Where(x => x.Status == "Open" && f.Cars.Any(k => k.Id == x.CarId && Access.BranchOk(c.U, k.BranchId))).ToList();
                tiles.Append(Tile("AdminIncidents", ic.Count, ic.Count == 1 ? "open incident" : "open incidents", Ui.Count(ic.Count(x => x.Kind == "Stolen"), "car stolen", "cars stolen"), true));
            }
            if (Access.Can(c.U, "payments", 'V'))
            {
                List<Split> due = mine.Where(b => Bill.Charged(b) && b.Status != "Reserved").Select(b => Bill.Of(f, b, null)).ToList();
                decimal cust = due.Sum(x => Math.Max(0, x.DueCustomer));
                decimal paidToday = f.Payments.Where(x => x.When.Date == today && mine.Any(b => b.Id == x.BookingId)).Sum(x => x.Amount);
                tiles.Append("<a class=\"db-tile" + (cust > 0.005m ? " db-warn" : string.Empty) + "\" href=\"AdminBookings?st=due\"><b>" + Rental.Money(cust) + "</b><span>customers owe</span><small>" + Rental.Money(paidToday) + " received today</small></a>");
            }
            if (Access.Can(c.U, "insurers", 'V'))
            {
                decimal owed = f.Insurers.Sum(x => Bill.Owed(f, x.Id));
                tiles.Append("<a class=\"db-tile\" href=\"AdminInsurers\"><b>" + Rental.Money(owed) + "</b><span>insurers owe</span><small>" + Ui.Count(f.Insurers.Count(x => x.Active), "active insurer", "active insurers") + "</small></a>");
            }
            if (msgs)
            {
                List<InquiryRec> m = await Store.Rows<InquiryRec>("messages");
                int fresh = m.Count(x => x.Status == "New");
                tiles.Append(Tile("AdminMessages", fresh, fresh == 1 ? "new message" : "new messages", m.Count + " in total", false));
            }
            if (tiles.Length > 0)
            {
                sb.Append("<div class=\"db-tiles\">" + tiles + "</div>");
            }
            if (desk || book)
            {
                string link = desk ? "AdminDesk" : "AdminBookings";
                sb.Append("<div class=\"db-cols\"><section class=\"db-panel\"><div class=\"db-sh\"><h2>Pick-ups today</h2><a class=\"db-link\" href=\"" + link + "\">Open the desk</a></div>" + Feed(f, outs, true, now) + "</section><section class=\"db-panel\"><div class=\"db-sh\"><h2>Returns today</h2><a class=\"db-link\" href=\"" + link + "\">Open the desk</a></div>" + Feed(f, ins, false, now) + "</section></div>");
            }
            if (fleet)
            {
                sb.Append("<section class=\"db-panel\"><div class=\"db-sh\"><h2>Fleet by branch</h2><a class=\"db-link\" href=\"AdminFleet\">Fleet</a></div><div class=\"db-wrapt\"><table class=\"db-table\"><thead><tr><th>Branch</th>" + string.Concat(new[] { "Available", "Reserved", "On rent", "Cleaning", "Maintenance", "Repair", "Hold", "In transit" }.Select(x => "<th class=\"db-num\">" + (x == "In transit" ? "In transfer" : x) + "</th>")) + "</tr></thead><tbody>");
                foreach (BranchRec b in f.Branches.Where(x => Access.BranchOk(c.U, x.Id)))
                {
                    sb.Append("<tr><td class=\"db-first\"><b>" + Ui.Esc(b.Name) + "</b>" + (b.Open ? string.Empty : "<small>Closed</small>") + "</td>" + string.Concat(new[] { "Available", "Reserved", "On rent", "Cleaning", "Maintenance", "Repair", "Hold", "In transit" }.Select(x => "<td class=\"db-num\" data-label=\"" + (x == "In transit" ? "In transfer" : x) + "\">" + cars.Count(k => k.BranchId == b.Id && k.Status == x) + "</td>")) + "</tr>");
                }
                sb.Append("</tbody></table></div></section>");
            }
            if (Access.Can(c.U, "activity", 'V'))
            {
                List<ActivityRec> log = await Store.Rows<ActivityRec>("activity");
                StringBuilder feed = new StringBuilder("<ul class=\"db-feed\">");
                foreach (ActivityRec a in log.Take(6))
                {
                    feed.Append("<li><div><b>" + Ui.Esc(a.Action) + "</b><span class=\"db-pill " + (a.Ok ? "db-pill-go" : "db-pill-bad") + "\">" + (a.Ok ? "Done" : "Refused") + "</span><small>" + Ui.Esc(a.Target) + " &middot; " + Ui.Esc(await Store.Who(a.UserId)) + " &middot; " + Ui.Esc(Ui.When(a.When)) + "</small></div></li>");
                }
                sb.Append("<section class=\"db-panel\"><div class=\"db-sh\"><h2>Recent activity</h2><a class=\"db-link\" href=\"AdminActivity\">Activity log</a></div>" + (log.Count == 0 ? "<p class=\"db-hint\">Nothing has been logged yet.</p>" : feed + "</ul>") + "</section>");
            }
            StringBuilder quick = new StringBuilder();
            if (Access.Can(c.U, "bookings", 'A'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminBookings?new=1\">New booking</a>");
            }
            if (Access.Can(c.U, "transfers", 'A'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminTransfers\">Plan a transfer</a>");
            }
            if (Access.Can(c.U, "calendar", 'V'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminCalendar\">Calendar</a>");
            }
            if (Access.Can(c.U, "pages", 'E'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminPages\">Edit the website</a>");
            }
            if (Access.Can(c.U, "reports", 'V'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminReports\">Reports</a>");
            }
            if (Access.Can(c.U, "classes", 'E'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminClasses\">Change rates</a>");
            }
            if (quick.Length > 0)
            {
                sb.Append("<section class=\"db-panel\"><h2>Quick actions</h2><div class=\"db-quick\">" + quick + "</div></section>");
            }
            if (tiles.Length == 0 && quick.Length == 0)
            {
                sb.Append("<section class=\"db-panel\"><p class=\"db-hint\">Your account has no areas yet. Ask a manager to give you rights on Staff.</p></section>");
            }
            response.SetElementContents("db-main", sb.ToString());
            return response;
        }

        private static string Feed(FleetData f, List<BookingRec> list, bool pick, DateTime now)
        {
            if (list.Count == 0)
            {
                return "<p class=\"db-hint\">" + (pick ? "No pick-ups left today." : "No returns left today.") + "</p>";
            }
            return "<ul class=\"db-feed\">" + string.Concat(list.Take(8).Select(b =>
            {
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == b.CarId);
                string st = Avail.Overdue(b, now) ? "Overdue" : b.Status == "Reserved" && b.Start < now ? "Late pick-up" : b.Status;
                return "<li><a href=\"AdminBookings?code=" + Ui.Esc(b.Code) + "&amp;st=all\"><b>" + Ui.Esc(Rental.When(pick ? b.Start : b.End).Split(", ").Last() + " · " + b.Code) + "</b>" + Pill(st) + "<small>" + Ui.Esc(Rental.Name(b) + " · " + Sheet.ClassName(f, b.ClassId) + (car != null ? " · " + car.Plate : string.Empty) + " · " + Sheet.BranchName(f, b.BranchId)) + "</small></a></li>";
            })) + (list.Count > 8 ? "<li><small>And " + (list.Count - 8) + " more</small></li>" : string.Empty) + "</ul>";
        }
    }
}
