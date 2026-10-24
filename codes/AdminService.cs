using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminService : WebPage
    {
        private const string AreaKey = "service";
        private const int Size = 12;
        private static readonly string[] Kinds = { "Scheduled service", "Oil and filter", "Tires", "Brakes", "Registration and inspection", "Detailing", "Other" };
        private static readonly string[] CanStart = { "Available", "Reserved", "Cleaning" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Maintenance | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminServiceJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminServiceJs.denied();");
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
            response.SetElementContents("sv-brand", Ui.Esc(site.Brand));
            response.SetElementContents("sv-menu", await Auth.Menu("sv", "AdminService", c));
            response.SetElementContents("sv-who", Auth.Who("sv", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminServiceJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"sv-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"sv-btn sv-btn-ghost sv-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminServiceJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"sv-btn sv-btn-ghost sv-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminServiceJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"sv-pill sv-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"sv-err\" id=\"sv-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"sv-field\" for=\"sv-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"sv-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static bool Mine(Ctx c, CarRec? car)
        {
            return car != null && Access.BranchOk(c.U, car.BranchId);
        }

        private static string CarText(CarRec? car)
        {
            return car == null ? "Unknown car" : car.Plate + " · " + car.Make + " " + car.Model;
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
            StringBuilder sb = new StringBuilder("<div class=\"sv-ph\"><div><h1>Maintenance</h1><p>Plan services ahead. A planned service takes the car out of the booking calendar for its time window, and bookings on the car move to another car first. The car goes to Maintenance when the service starts.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"sv-btn\" onclick=\"AdminServiceJs.add()\">Plan a service</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"sv-tools\"><input type=\"search\" id=\"sv-q\" placeholder=\"Plate, kind or shop\" maxlength=\"40\" oninput=\"AdminServiceJs.type()\" aria-label=\"Search\"><select id=\"sv-fs\" onchange=\"AdminServiceJs.list(true)\" aria-label=\"Show\">" + Opt("open", "Planned and in progress", "open") + Opt("due", "Cars due soon", "open") + Opt("Done", "Done", "open") + Opt("Cancelled", "Cancelled", "open") + Opt("all", "All services", "open") + "</select><select id=\"sv-fb\" onchange=\"AdminServiceJs.list(true)\" aria-label=\"Branch\">" + Opt(string.Empty, "All my branches", string.Empty) + string.Concat(f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select></div>");
            sb.Append("<div id=\"sv-list\">" + ListHtml(c, f, string.Empty, "open", string.Empty, 1) + "</div>");
            response.SetElementContents("sv-main", sb.ToString());
            string carId = Ui.Clip(GetDataValue("car"), 60);
            if (carId.Length > 0 && Can(c, 'A') && Mine(c, f.Cars.FirstOrDefault(x => x.Id == carId)))
            {
                response.SetElementContents("sv-mbody", FormHtml(c, f, null, carId));
                response.ExecuteScript("AdminServiceJs.opened('Plan a service');");
            }
            return response;
        }

        private static string ListHtml(Ctx c, FleetData f, string term, string st, string branch, int page)
        {
            if (st == "due")
            {
                List<CarRec> cars = f.Cars.Where(x => Mine(c, x) && !Avail.Gone.Contains(x.Status) && (branch == string.Empty || x.BranchId == branch)
                    && (Ops.Flags(f, x).Any(g => g == "Service due" || g == "Registration due") || (x.ServiceMiles > 0 && x.Mileage >= x.ServiceMiles - 1500))
                    && (term == string.Empty || Ui.Has(x.Plate, term) || Ui.Has(x.Make + " " + x.Model, term))).OrderBy(x => x.ServiceMiles - x.Mileage).ToList();
                if (cars.Count == 0)
                {
                    return "<div class=\"sv-panel sv-empty\">No car is due for service or registration soon.</div>";
                }
                StringBuilder d = new StringBuilder("<div class=\"sv-wrapt\"><table class=\"sv-table\"><thead><tr><th>Car</th><th class=\"sv-num\">Mileage</th><th class=\"sv-num\">Next service at</th><th>Registration due</th><th>Planned</th><th></th></tr></thead><tbody>");
                foreach (CarRec x in cars)
                {
                    ServiceRec? next = f.Services.Where(v => v.CarId == x.Id && (v.Status == "Planned" || v.Status == "In progress")).OrderBy(v => v.Start).FirstOrDefault();
                    d.Append("<tr><td class=\"sv-first\"><b>" + Ui.Esc(x.Plate) + "</b><small>" + Ui.Esc(x.Make + " " + x.Model + " · " + Avail.Location(f, x)) + "</small><div class=\"sv-flags\">" + string.Concat(Ops.Flags(f, x).Select(g => "<span class=\"sv-flag\">" + Ui.Esc(g) + "</span>")) + "</div></td><td data-label=\"Mileage\" class=\"sv-num\">" + x.Mileage.ToString("N0", Rental.Us) + "</td><td data-label=\"Next service at\" class=\"sv-num\">" + (x.ServiceMiles > 0 ? x.ServiceMiles.ToString("N0", Rental.Us) : "Not set") + "</td><td data-label=\"Registration due\">" + Ui.Esc(x.RegistrationDue == default ? "Not set" : Rental.Day(x.RegistrationDue)) + "</td><td data-label=\"Planned\">" + (next == null ? "<small>Nothing planned</small>" : Ui.Esc(next.Kind) + "<small>" + Ui.Esc(Rental.When(next.Start)) + "</small>") + "</td><td data-label=\"\">" + (Can(c, 'A') ? "<button type=\"button\" class=\"sv-btn sv-btn-ghost sv-btn-sm\" onclick=\"AdminServiceJs.plan('" + Ui.Js(x.Id) + "')\">Plan</button>" : string.Empty) + "</td></tr>");
                }
                d.Append("</tbody></table></div>");
                return d.ToString();
            }
            List<ServiceRec> rows = f.Services.Where(v =>
            {
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == v.CarId);
                return Mine(c, car) && (branch == string.Empty || car!.BranchId == branch)
                    && (st == "all" || (st == "open" ? v.Status == "Planned" || v.Status == "In progress" : v.Status == st))
                    && (term == string.Empty || Ui.Has(car!.Plate, term) || Ui.Has(v.Kind, term) || Ui.Has(v.Shop, term));
            }).OrderBy(v => v.Status == "In progress" ? 0 : v.Status == "Planned" ? 1 : 2).ThenBy(v => st == "open" ? v.Start.Ticks : -v.Start.Ticks).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"sv-panel sv-empty\">No services match.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"sv-wrapt\"><table class=\"sv-table\"><thead><tr><th>Car</th><th>Service</th><th>Window</th><th>Shop</th><th>Status</th><th class=\"sv-num\">Cost</th></tr></thead><tbody>");
            foreach (ServiceRec v in rows.Skip((page - 1) * Size).Take(Size))
            {
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == v.CarId);
                bool late = v.Status == "In progress" && v.End < DateTime.Now;
                sb.Append("<tr class=\"sv-row\" onclick=\"AdminServiceJs.edit('" + Ui.Js(v.Id) + "')\"><td class=\"sv-first\"><b>" + Ui.Esc(car?.Plate ?? v.CarId) + "</b><small>" + Ui.Esc(car == null ? string.Empty : car.Make + " " + car.Model + " · " + Avail.Location(f, car)) + "</small></td><td data-label=\"Service\">" + Ui.Esc(v.Kind) + "</td><td data-label=\"Window\">" + Ui.Esc(Rental.When(v.Start)) + "<small>to " + Ui.Esc(Rental.When(v.End)) + "</small></td><td data-label=\"Shop\">" + Ui.Esc(v.Shop) + "</td><td data-label=\"Status\">" + Pill(v.Status) + (late ? "<small>Past its end time</small>" : string.Empty) + "</td><td data-label=\"Cost\" class=\"sv-num\">" + (v.Status == "Done" ? Rental.Money(v.Cost) : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "service", "services"));
            return sb.ToString();
        }

        private async Task<string> ListNow(Ctx c)
        {
            FleetData f = await Avail.LoadSettled();
            string st = Ui.Clip(GetDataValue("fs"), 20);
            return ListHtml(c, f, Term(GetDataValue("term")), st.Length == 0 ? "open" : st, Ui.Clip(GetDataValue("fb"), 60), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("sv-list", await ListNow(c));
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
            ServiceRec? v = f.Services.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (v == null || !Mine(c, f.Cars.FirstOrDefault(x => x.Id == v.CarId))))
            {
                return await Refuse(response, c, "open service", id, "That service was not found at your branches.");
            }
            if (v == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "plan service", string.Empty, "You can view maintenance but not plan services.");
            }
            response.SetElementContents("sv-mbody", FormHtml(c, f, v, Ui.Clip(GetDataValue("car"), 60)));
            response.ExecuteScript("AdminServiceJs.opened('" + Ui.Js(v == null ? "Plan a service" : v.Kind + " · " + CarText(f.Cars.FirstOrDefault(x => x.Id == v.CarId))) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, FleetData f, ServiceRec? x, string carId)
        {
            bool isNew = x == null;
            DateTime d0 = DateTime.Today.AddDays(1).AddHours(8);
            ServiceRec v = x ?? new ServiceRec { CarId = carId, Kind = Kinds[0], Start = d0, End = d0.AddHours(6) };
            bool open = v.Status == "Planned" || v.Status == "In progress";
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E') && v.Status == "Planned";
            CarRec? car = f.Cars.FirstOrDefault(k => k.Id == v.CarId);
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<form class=\"sv-stack\" id=\"sv-form\" onsubmit=\"return AdminServiceJs.save()\" novalidate><input type=\"hidden\" id=\"sv-f-id\" value=\"" + Ui.Esc(v.Id) + "\">");
            if (!isNew)
            {
                sb.Append("<div class=\"sv-bar\"><div>" + Pill(v.Status) + " <span class=\"sv-mini\">" + Ui.Esc(CarText(car)) + (car != null ? " · " + Ui.Esc(car.Mileage.ToString("N0", Rental.Us)) + " miles" : string.Empty) + "</span></div>" + (v.DoneAt != null ? "<span class=\"sv-mini\">Done " + Ui.Esc(Rental.When(v.DoneAt.Value)) + "</span>" : string.Empty) + "</div>");
            }
            if (isNew)
            {
                List<CarRec> cars = f.Cars.Where(k => Mine(c, k) && !Avail.Gone.Contains(k.Status)).OrderBy(k => k.Plate).ToList();
                sb.Append(Field("car", "Car", "<select id=\"sv-f-car\" data-k=\"car\">" + string.Concat(cars.Select(k => Opt(k.Id, k.Plate + " · " + k.Make + " " + k.Model + " · " + Avail.Location(f, k) + " · " + k.Status, v.CarId))) + "</select>"));
            }
            sb.Append("<div class=\"sv-grid2\">" + Field("kind", "Service", "<select id=\"sv-f-kind\" data-k=\"kind\"" + dis + ">" + string.Concat(Kinds.Select(k => Opt(k, k, v.Kind))) + "</select>") + Field("shop", "Shop or technician", Text("shop", v.Shop, 60, "text", ro)) + "</div>");
            sb.Append("<div class=\"sv-grid2\">" + Field("start", "Starts", "<input type=\"datetime-local\" id=\"sv-f-start\" data-k=\"start\" value=\"" + Rental.Iso(v.Start) + "\"" + ro + ">") + Field("end", "Car back by", "<input type=\"datetime-local\" id=\"sv-f-end\" data-k=\"end\" value=\"" + Rental.Iso(v.End) + "\"" + ro + ">") + "</div>" + Err("window"));
            sb.Append("<label class=\"sv-field\" for=\"sv-f-notes\"><span>Notes</span><textarea id=\"sv-f-notes\" data-k=\"notes\" rows=\"2\" maxlength=\"400\"" + (open && Can(c, isNew ? 'A' : 'E') ? string.Empty : " readonly") + ">" + Ui.Esc(v.Notes) + "</textarea></label>");
            if (v.Status == "In progress" || v.Status == "Done")
            {
                bool fin = v.Status == "In progress" && Can(c, 'E');
                sb.Append("<p class=\"sv-sec\">Finish</p><div class=\"sv-grid2\">" + Field("mileage", "Mileage at the end", Text("mileage", (v.Status == "Done" ? v.Mileage : car?.Mileage ?? 0).ToString(CultureInfo.InvariantCulture), 7, "number", fin ? string.Empty : " readonly")) + Field("cost", "Cost (US dollars)", Text("cost", Num(v.Cost), 10, "text", (fin ? string.Empty : " readonly") + " inputmode=\"decimal\"")) + "</div>");
                if (fin)
                {
                    sb.Append("<p class=\"sv-hint\">A scheduled service or oil change sets the next service to " + f.Site.ServiceEvery.ToString("N0", Rental.Us) + " miles later. A registration and inspection moves the registration due date one year ahead.</p>");
                }
            }
            sb.Append("<div class=\"sv-acts\">");
            if (edit)
            {
                sb.Append("<button type=\"submit\" class=\"sv-btn\">" + (isNew ? "Plan service" : "Save") + "</button>");
            }
            if (!isNew && Can(c, 'E'))
            {
                if (v.Status == "Planned")
                {
                    sb.Append("<button type=\"button\" class=\"sv-btn sv-btn-ghost\" onclick=\"AdminServiceJs.act('start')\">Start now</button>");
                }
                if (v.Status == "In progress")
                {
                    sb.Append("<button type=\"button\" class=\"sv-btn\" onclick=\"AdminServiceJs.act('done')\">Mark done</button>");
                }
            }
            sb.Append("<button type=\"button\" class=\"sv-btn sv-btn-ghost\" onclick=\"AdminServiceJs.closeModal()\">Close</button>");
            if (!isNew && open && Can(c, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"sv-btn sv-btn-danger sv-push\" onclick=\"AdminServiceJs.cancelIt()\">Cancel service</button>");
            }
            sb.Append("</div></form>");
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
                return await Refuse(response, c, isNew ? "plan service" : "edit service", id, "You do not have the right to " + (isNew ? "plan" : "change") + " services.");
            }
            string kind = Kinds.Contains(GetDataValue("kind")) ? GetDataValue("kind")! : Kinds[0];
            string shop = Ui.Clip(GetDataValue("shop"), 60);
            string notes = Ui.Clip((GetDataValue("notes") ?? string.Empty).Replace("\r", string.Empty), 400);
            bool okS = Rental.ParseLocal(GetDataValue("start"), out DateTime s);
            bool okE = Rental.ParseLocal(GetDataValue("end"), out DateTime e);
            string moved;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                ServiceRec? v = f.Services.FirstOrDefault(x => x.Id == id);
                if (!isNew && (v == null || v.Status != "Planned"))
                {
                    return await Refuse(response, c, "edit service", id, "Only a planned service can be changed.");
                }
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == (isNew ? Ui.Clip(GetDataValue("car"), 60) : v!.CarId));
                string ew = !okS || !okE ? "Choose when the service starts and when the car is back." : e <= s ? "The car must be back after the service starts." : (e - s).TotalDays > 30 ? "A service can last up to 30 days." : isNew && s < DateTime.Now.AddMinutes(-30) ? "A service cannot start in the past." : s > DateTime.Now.AddDays(365) ? "Plan services up to a year ahead." : string.Empty;
                string ec = car == null || !Mine(c, car) ? "Choose one of your cars." : Avail.Gone.Contains(car.Status) ? "This car is " + car.Status.ToLowerInvariant() + "." : string.Empty;
                if (ew.Length == 0 && ec.Length == 0 && f.Services.Any(x => x.CarId == car!.Id && x.Id != id && (x.Status == "Planned" || x.Status == "In progress") && Avail.Overlap(x.Start, x.End, s, e)))
                {
                    ew = "Another service on this car overlaps this time.";
                }
                if (ew.Length == 0 && ec.Length == 0 && s <= DateTime.Now && !CanStart.Contains(car!.Status))
                {
                    ew = "The car is " + car.Status.ToLowerInvariant() + " now, so the service cannot start yet. Choose a later start.";
                }
                response.SetElementContents("sv-e-window", ew);
                response.SetElementContents("sv-e-car", ec);
                if (ew.Length > 0 || ec.Length > 0)
                {
                    return response;
                }
                CarRec t = car!;
                (string err, List<BookingRec> m) = Ops.Reflow(f, t, s, e, string.Empty);
                if (err.Length > 0)
                {
                    return await Refuse(response, c, "plan service", t.Plate, "Cannot plan the service on " + t.Plate + ". " + err);
                }
                if (isNew)
                {
                    v = new ServiceRec { Id = Rental.NewId("s-"), CarId = t.Id, Status = "Planned", Created = DateTime.Now, By = c.U.Id };
                    f.Services.Add(v);
                }
                ServiceRec w = v!;
                w.Kind = kind;
                w.Shop = shop;
                w.Notes = notes;
                w.Start = s;
                w.End = e;
                Avail.Settle(f);
                await Ops.PersistAll(f);
                moved = Ops.Moved(m);
                target = t.Plate + " · " + kind + " · " + Rental.When(s);
            }
            await Store.Log(c.U.Id, isNew ? "plan service" : "edit service", target + moved, true);
            response.SetElementContents("sv-list", await ListNow(c));
            response.ExecuteScript("AdminServiceJs.saved();" + (moved.Length > 0 ? "AdminServiceJs.toast('" + Ui.Js("Saved." + moved) + "');" : string.Empty));
            return response;
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            string what = Ui.Clip(GetDataValue("what"), 10);
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "update service", id, "You do not have the right to change services.");
            }
            int mileage = Int(GetDataValue("mileage"), -1);
            decimal cost = Dec(GetDataValue("cost"));
            string notes = Ui.Clip((GetDataValue("notes") ?? string.Empty).Replace("\r", string.Empty), 400);
            string msg;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                ServiceRec? v = f.Services.FirstOrDefault(x => x.Id == id);
                CarRec? car = v == null ? null : f.Cars.FirstOrDefault(x => x.Id == v.CarId);
                if (v == null || car == null || !Mine(c, car))
                {
                    return await Refuse(response, c, "update service", id, "That service was not found at your branches.");
                }
                DateTime now = DateTime.Now;
                target = car.Plate + " · " + v.Kind;
                if (what == "start" && v.Status == "Planned")
                {
                    if (!CanStart.Contains(car.Status))
                    {
                        return await Refuse(response, c, "start service", target, "The car is " + car.Status.ToLowerInvariant() + " now, so the service cannot start.");
                    }
                    DateTime end = v.End > now ? v.End : now.AddHours(4);
                    (string err, List<BookingRec> _) = Ops.Reflow(f, car, now, end, string.Empty);
                    if (err.Length > 0)
                    {
                        return await Refuse(response, c, "start service", target, err);
                    }
                    v.Start = now;
                    v.End = end;
                    v.Status = "In progress";
                    car.Status = "Maintenance";
                    car.StatusNote = v.Kind;
                    car.BlockUntil = end;
                    car.StatusSince = now;
                    msg = car.Plate + " is in maintenance.";
                }
                else if (what == "done" && v.Status == "In progress")
                {
                    string em = mileage < car.Mileage ? "Mileage cannot be lower than " + car.Mileage.ToString("N0", Rental.Us) + "." : mileage > car.Mileage + 5000 ? "That is far above the last reading. Please check it." : string.Empty;
                    string eco = cost < 0 || cost > 100000 ? "Enter the cost, or 0." : string.Empty;
                    response.SetElementContents("sv-e-mileage", em);
                    response.SetElementContents("sv-e-cost", eco);
                    if (em.Length > 0 || eco.Length > 0)
                    {
                        return response;
                    }
                    v.Status = "Done";
                    v.DoneAt = now;
                    v.Mileage = mileage;
                    v.Cost = cost;
                    v.Notes = notes;
                    car.Mileage = mileage;
                    if (v.Kind == "Scheduled service" || v.Kind == "Oil and filter")
                    {
                        car.ServiceMiles = mileage + Math.Max(1000, f.Site.ServiceEvery);
                    }
                    if (v.Kind == "Registration and inspection")
                    {
                        car.RegistrationDue = DateTime.Today.AddYears(1);
                    }
                    Ops.Release(f, car, "Maintenance");
                    msg = car.Plate + " is back from service.";
                }
                else if (what == "cancel" && (v.Status == "Planned" || v.Status == "In progress"))
                {
                    bool running = v.Status == "In progress";
                    v.Status = "Cancelled";
                    if (running)
                    {
                        Ops.Release(f, car, "Maintenance");
                    }
                    msg = "Service cancelled.";
                }
                else
                {
                    return await Refuse(response, c, "update service", target, "That service has already moved on. The list is refreshed.");
                }
                Avail.Settle(f);
                await Ops.PersistAll(f);
            }
            await Store.Log(c.U.Id, what == "start" ? "start service" : what == "done" ? "finish service" : "cancel service", target + (what == "done" ? " · " + Rental.Money(cost) : string.Empty), true);
            response.SetElementContents("sv-list", await ListNow(c));
            response.ExecuteScript("AdminServiceJs.clean();AdminServiceJs.shut();AdminServiceJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }
    }
}
