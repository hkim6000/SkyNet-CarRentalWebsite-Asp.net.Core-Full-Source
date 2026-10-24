using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminCalendar : WebPage
    {
        private const string AreaKey = "calendar";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Calendar | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCalendarJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminCalendarJs.denied();");
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
            response.SetElementContents("ca-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ca-menu", await Auth.Menu("ca", "AdminCalendar", c));
            response.SetElementContents("ca-who", Auth.Who("ca", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminCalendarJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"ca-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"ca-btn ca-btn-ghost ca-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminCalendarJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"ca-btn ca-btn-ghost ca-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminCalendarJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"ca-pill ca-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"ca-err\" id=\"ca-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"ca-field\" for=\"ca-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"ca-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private sealed class Mark
        {
            public string Kind { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string Tip { get; set; } = string.Empty;
            public bool Start { get; set; }
        }

        private static DateTime Month(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime m) ? m : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
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
            List<BranchRec> mine = f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).ToList();
            string carId = Ui.Clip(GetDataValue("car"), 60);
            CarRec? car = f.Cars.FirstOrDefault(x => x.Id == carId && Access.BranchOk(c.U, x.BranchId));
            string view = car != null ? "car" : "class";
            string branch = car?.BranchId ?? mine.FirstOrDefault()?.Id ?? string.Empty;
            StringBuilder sb = new StringBuilder("<div class=\"ca-ph\"><div><h1>Calendar</h1><p>See a month at a time: how many cars of each class are free per day at a branch, every car of a class, or one car's month.</p></div></div>");
            sb.Append("<div class=\"ca-tabs\"><button type=\"button\" class=\"ca-tab" + (view == "class" ? " ca-act" : string.Empty) + "\" id=\"ca-t-class\" onclick=\"AdminCalendarJs.view('class')\">By branch and class</button><button type=\"button\" class=\"ca-tab" + (view == "car" ? " ca-act" : string.Empty) + "\" id=\"ca-t-car\" onclick=\"AdminCalendarJs.view('car')\">One car</button></div>");
            sb.Append("<div class=\"ca-tools\"><span class=\"ca-ctl\"><button type=\"button\" aria-label=\"Previous month\" onclick=\"AdminCalendarJs.step(-1)\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><button type=\"button\" aria-label=\"Next month\" onclick=\"AdminCalendarJs.step(1)\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></span><input type=\"month\" id=\"ca-m\" value=\"" + DateTime.Today.ToString("yyyy-MM", CultureInfo.InvariantCulture) + "\" onchange=\"AdminCalendarJs.show()\" aria-label=\"Month\">");
            sb.Append("<select id=\"ca-b\" onchange=\"AdminCalendarJs.show()\" aria-label=\"Branch\"" + (view == "car" ? " hidden" : string.Empty) + ">" + string.Concat(mine.Select(b => Opt(b.Id, b.Name, branch))) + "</select>");
            sb.Append("<select id=\"ca-k\" onchange=\"AdminCalendarJs.show()\" aria-label=\"Class\"" + (view == "car" ? " hidden" : string.Empty) + ">" + Opt(string.Empty, "All classes, free cars per day", string.Empty) + string.Concat(f.Classes.Select(k => Opt(k.Id, k.Name + ", every car", string.Empty))) + "</select>");
            sb.Append("<select id=\"ca-c\" onchange=\"AdminCalendarJs.show()\" aria-label=\"Car\"" + (view == "class" ? " hidden" : string.Empty) + ">" + string.Concat(f.Cars.Where(x => Access.BranchOk(c.U, x.BranchId) && !Avail.Gone.Contains(x.Status)).OrderBy(x => x.Plate).Select(x => Opt(x.Id, x.Plate + " · " + x.Make + " " + x.Model, carId))) + "</select></div>");
            sb.Append("<div class=\"ca-legend\"><span><i class=\"ca-k-res\"></i>Reserved</span><span><i class=\"ca-k-out\"></i>On rent</span><span><i class=\"ca-k-late\"></i>Overdue</span><span><i class=\"ca-k-done\"></i>Returned</span><span><i class=\"ca-k-blk\"></i>Hold, repair, maintenance</span><span><i class=\"ca-k-trn\"></i>Transfer</span></div>");
            DateTime m = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            sb.Append("<div id=\"ca-list\">" + (view == "car" && car != null ? CarMonth(f, car, m) : ClassView(c, f, branch, string.Empty, m)) + "</div>");
            response.SetElementContents("ca-main", sb.ToString());
            if (view == "car")
            {
                response.ExecuteScript("AdminCalendarJs.mode('car');");
            }
            response.ExecuteScript("AdminCalendarJs.toToday();");
            return response;
        }

        public async Task<ApiResponse> Show()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            FleetData f = await Avail.LoadSettled();
            DateTime m = Month(GetDataValue("m"));
            if (GetDataValue("view") == "car")
            {
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == GetDataValue("c") && Access.BranchOk(c.U, x.BranchId));
                response.SetElementContents("ca-list", car == null ? "<div class=\"ca-panel ca-empty\">Choose a car.</div>" : CarMonth(f, car, m));
                return response;
            }
            string branch = Ui.Clip(GetDataValue("b"), 60);
            if (!Access.BranchOk(c.U, branch) || !f.Branches.Any(b => b.Id == branch))
            {
                response.SetElementContents("ca-list", "<div class=\"ca-panel ca-empty\">Choose one of your branches.</div>");
                return response;
            }
            response.SetElementContents("ca-list", ClassView(c, f, branch, Ui.Clip(GetDataValue("k"), 60), m));
            response.ExecuteScript("AdminCalendarJs.toToday();");
            return response;
        }

        private static List<Mark> Marks(FleetData f, CarRec car, DateTime day)
        {
            DateTime d0 = day.Date;
            DateTime d1 = d0.AddDays(1);
            DateTime now = DateTime.Now;
            List<Mark> list = new List<Mark>();
            foreach (BookingRec b in f.Bookings.Where(x => x.CarId == car.Id && (Avail.Live(x) || x.Status == "Returned")))
            {
                DateTime s = b.Status == "Returned" && b.PickedUpAt != null ? b.PickedUpAt.Value : b.Start;
                DateTime e = b.Status == "Returned" && b.ReturnedAt != null ? b.ReturnedAt.Value : Avail.EndOf(b, now);
                if (Avail.Overlap(s, e, d0, d1))
                {
                    string kind = b.Status == "Returned" ? "done" : Avail.Overdue(b, now) ? "late" : Avail.Out(b) ? "out" : "res";
                    list.Add(new Mark { Kind = kind, Label = b.Code, Start = s >= d0 && s < d1, Tip = b.Code + " · " + Rental.Name(b) + " · " + Rental.When(b.Start) + " to " + Rental.When(b.End) + " · " + b.Status });
                }
            }
            foreach (TransferRec t in f.Transfers.Where(x => x.CarId == car.Id && x.Status != "Cancelled"))
            {
                if (Avail.Overlap(t.Depart, t.Arrive, d0, d1))
                {
                    string to = f.Branches.FirstOrDefault(b => b.Id == t.To)?.Name ?? t.To;
                    list.Add(new Mark { Kind = "trn", Label = "To " + to, Start = t.Depart >= d0 && t.Depart < d1, Tip = "Transfer to " + to + " · " + Rental.When(t.Depart) + " to " + Rental.When(t.Arrive) + " · " + t.Status });
                }
            }
            foreach (ServiceRec v in f.Services.Where(x => x.CarId == car.Id && (x.Status == "Planned" || x.Status == "In progress" || x.Status == "Done")))
            {
                DateTime ve = v.Status == "Done" && v.DoneAt != null ? v.DoneAt.Value : v.End;
                if (Avail.Overlap(v.Start, ve, d0, d1))
                {
                    list.Add(new Mark { Kind = "blk", Label = "Service", Start = v.Start >= d0 && v.Start < d1, Tip = v.Kind + " · " + Rental.When(v.Start) + " to " + Rental.When(ve) + " · " + v.Status });
                }
            }
            if ((Avail.Blocking.Contains(car.Status) && !(car.Status == "Maintenance" && f.Services.Any(x => x.CarId == car.Id && x.Status == "In progress"))) || Avail.Gone.Contains(car.Status))
            {
                DateTime s = car.StatusSince == default ? now : car.StatusSince;
                DateTime e = Avail.Gone.Contains(car.Status) || car.BlockUntil == null ? DateTime.MaxValue.AddDays(-2) : car.BlockUntil.Value;
                if (Avail.Overlap(s, e, d0, d1))
                {
                    list.Add(new Mark { Kind = "blk", Label = car.Status, Start = s >= d0 && s < d1, Tip = car.Status + (car.StatusNote.Length > 0 ? " · " + car.StatusNote : string.Empty) });
                }
            }
            return list;
        }

        private static readonly string[] Rank = { "late", "out", "res", "trn", "blk", "done" };

        private static string ClassView(Ctx c, FleetData f, string branch, string cls, DateTime m)
        {
            int days = DateTime.DaysInMonth(m.Year, m.Month);
            string bname = f.Branches.FirstOrDefault(b => b.Id == branch)?.Name ?? string.Empty;
            StringBuilder head = new StringBuilder("<tr><th class=\"ca-cname\">" + Ui.Esc(m.ToString("MMMM yyyy", Rental.Us)) + "</th>");
            for (int d = 1; d <= days; d++)
            {
                DateTime day = new DateTime(m.Year, m.Month, d);
                head.Append("<th class=\"ca-dh" + (day == DateTime.Today ? " ca-today" : string.Empty) + (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday ? " ca-we" : string.Empty) + "\"><small>" + day.ToString("ddd", Rental.Us).Substring(0, 2) + "</small>" + d + "</th>");
            }
            head.Append("</tr>");
            StringBuilder sb = new StringBuilder();
            if (cls.Length == 0)
            {
                sb.Append("<p class=\"ca-hint\">Free cars per day at " + Ui.Esc(bname) + ": cars of the class that are free for the whole day, after the cleaning buffer. Choose a class to see every car.</p><div class=\"ca-gwrap\"><table class=\"ca-grid\"><thead>" + head + "</thead><tbody>");
                foreach (ClassRec k in f.Classes)
                {
                    int total = f.Cars.Count(x => x.ClassId == k.Id && x.BranchId == branch && !Avail.Gone.Contains(x.Status));
                    sb.Append("<tr><th class=\"ca-cname\"><button type=\"button\" class=\"ca-link\" onclick=\"AdminCalendarJs.pick('" + Ui.Js(k.Id) + "')\">" + Ui.Esc(k.Name) + "</button><small>" + Ui.Count(total, "car", "cars") + "</small></th>");
                    for (int d = 1; d <= days; d++)
                    {
                        DateTime day = new DateTime(m.Year, m.Month, d);
                        int free = Avail.FreeCount(f, branch, k.Id, day, day.AddDays(1));
                        string heat = free == 0 ? "ca-h0" : free == 1 ? "ca-h1" : "ca-h2";
                        sb.Append("<td class=\"ca-num0 " + heat + (day < DateTime.Today ? " ca-past" : string.Empty) + "\" title=\"" + Ui.Esc(k.Name + ", " + Rental.Day(day) + ": " + free + " free") + "\">" + free + "</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div>");
                return sb.ToString();
            }
            ClassRec? kc = f.Classes.FirstOrDefault(x => x.Id == cls);
            DateTime m0 = m;
            DateTime m1 = m.AddMonths(1);
            List<CarRec> cars = f.Cars.Where(x => x.ClassId == cls && !Avail.Gone.Contains(x.Status) && (x.BranchId == branch || f.Transfers.Any(t => t.CarId == x.Id && t.Status != "Cancelled" && (t.To == branch || t.From == branch) && Avail.Overlap(t.Depart, t.Arrive.AddDays(31), m0, m1)))).OrderBy(x => x.Plate).ToList();
            sb.Append("<p class=\"ca-hint\">" + Ui.Esc((kc?.Name ?? string.Empty) + " at " + bname) + ". Point at a mark for details.</p><div class=\"ca-gwrap\"><table class=\"ca-grid\"><thead>" + head + "</thead><tbody>");
            if (cars.Count == 0)
            {
                sb.Append("<tr><td colspan=\"" + (days + 1) + "\" class=\"ca-empty\">No cars of this class at this branch.</td></tr>");
            }
            foreach (CarRec car in cars)
            {
                sb.Append("<tr><th class=\"ca-cname\"><a href=\"AdminCalendar?car=" + Ui.Esc(car.Id) + "\" class=\"ca-link\">" + Ui.Esc(car.Plate) + "</a><small>" + Ui.Esc(car.Make + " " + car.Model) + "</small></th>");
                for (int d = 1; d <= days; d++)
                {
                    DateTime day = new DateTime(m.Year, m.Month, d);
                    List<Mark> mk = Marks(f, car, day);
                    Mark? top = mk.OrderBy(x => Array.IndexOf(Rank, x.Kind)).FirstOrDefault();
                    sb.Append(top == null ? "<td class=\"ca-free" + (day < DateTime.Today ? " ca-past" : string.Empty) + "\"></td>" : "<td class=\"ca-m ca-k-" + top.Kind + "\" title=\"" + Ui.Esc(string.Join(" | ", mk.Select(x => x.Tip))) + "\">" + (top.Start ? "<span>" + Ui.Esc(top.Label) + "</span>" : string.Empty) + "</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("<tr class=\"ca-freerow\"><th class=\"ca-cname\">Free cars</th>");
            for (int d = 1; d <= days; d++)
            {
                DateTime day = new DateTime(m.Year, m.Month, d);
                int free = Avail.FreeCount(f, branch, cls, day, day.AddDays(1));
                sb.Append("<td class=\"ca-num0 " + (free == 0 ? "ca-h0" : free == 1 ? "ca-h1" : "ca-h2") + "\">" + free + "</td>");
            }
            sb.Append("</tr></tbody></table></div>");
            return sb.ToString();
        }

        private static string CarMonth(FleetData f, CarRec car, DateTime m)
        {
            int days = DateTime.DaysInMonth(m.Year, m.Month);
            int lead = ((int)m.DayOfWeek + 6) % 7;
            StringBuilder sb = new StringBuilder("<div class=\"ca-bar\"><div><b>" + Ui.Esc(car.Plate) + "</b> <span class=\"ca-mini\">" + Ui.Esc(car.Year + " " + car.Make + " " + car.Model + " · " + (f.Classes.FirstOrDefault(k => k.Id == car.ClassId)?.Name ?? string.Empty) + " · " + Avail.Location(f, car)) + "</span></div>" + Pill(car.Status == "In transit" ? "In transfer" : car.Status) + "</div>");
            sb.Append("<div class=\"ca-cal\"><div class=\"ca-calh\">" + m.ToString("MMMM yyyy", Rental.Us) + "</div><div class=\"ca-calg\">" + string.Concat(new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }.Select(w => "<div class=\"ca-wd\">" + w + "</div>")));
            for (int i = 0; i < lead; i++)
            {
                sb.Append("<div class=\"ca-day ca-out\"></div>");
            }
            for (int d = 1; d <= days; d++)
            {
                DateTime day = new DateTime(m.Year, m.Month, d);
                List<Mark> mk = Marks(f, car, day).OrderBy(x => Array.IndexOf(Rank, x.Kind)).ToList();
                sb.Append("<div class=\"ca-day" + (day == DateTime.Today ? " ca-today" : string.Empty) + "\"><b>" + d + "</b>" + string.Concat(mk.Select(x => "<span class=\"ca-chipm ca-k-" + x.Kind + "\" title=\"" + Ui.Esc(x.Tip) + "\">" + Ui.Esc(x.Label) + "</span>")) + "</div>");
            }
            sb.Append("</div></div>");
            List<BookingRec> list = f.Bookings.Where(b => b.CarId == car.Id && (Avail.Live(b) || b.Status == "Returned") && Avail.Overlap(b.Start, b.ReturnedAt ?? b.End, m, m.AddMonths(1))).OrderBy(b => b.Start).ToList();
            if (list.Count > 0)
            {
                sb.Append("<div class=\"ca-wrapt\" style=\"margin-top:16px\"><table class=\"ca-table\"><thead><tr><th>Booking</th><th>Pick-up</th><th>Return</th><th>Status</th></tr></thead><tbody>" + string.Concat(list.Select(b => "<tr><td class=\"ca-first\"><b>" + Ui.Esc(b.Code) + "</b><small>" + Ui.Esc(Rental.Name(b)) + "</small></td><td data-label=\"Pick-up\">" + Ui.Esc(Rental.When(b.Start)) + "</td><td data-label=\"Return\">" + Ui.Esc(Rental.When(b.End)) + "</td><td data-label=\"Status\">" + Pill(Avail.Overdue(b, DateTime.Now) ? "Overdue" : b.Status) + "</td></tr>")) + "</tbody></table></div>");
            }
            return sb.ToString();
        }
    }
}
