using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminRepairs : WebPage
    {
        private const string AreaKey = "repairs";
        private const int Size = 12;
        private static readonly string[] NoRepair = { "On rent", "In transit", "Stolen", "Sold" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Repairs | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRepairsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminRepairsJs.denied();");
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
            response.SetElementContents("rp-brand", Ui.Esc(site.Brand));
            response.SetElementContents("rp-menu", await Auth.Menu("rp", "AdminRepairs", c));
            response.SetElementContents("rp-who", Auth.Who("rp", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminRepairsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"rp-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"rp-btn rp-btn-ghost rp-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminRepairsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"rp-btn rp-btn-ghost rp-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminRepairsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"rp-pill rp-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"rp-err\" id=\"rp-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"rp-field\" for=\"rp-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"rp-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static bool Open(RepairRec r)
        {
            return r.Status == "Open" || r.Status == "In shop";
        }

        private static DateTime? Until(FleetData f, string carId)
        {
            List<RepairRec> open = f.Repairs.Where(r => r.CarId == carId && Open(r)).ToList();
            return open.Count == 0 || open.Any(r => r.ReadyBy == null) ? null : open.Max(r => r.ReadyBy);
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
            StringBuilder sb = new StringBuilder("<div class=\"rp-ph\"><div><h1>Repairs</h1><p>A car with an open repair is off the road. With a ready-by time, it can be booked again from then on. Repairs opened at check-in show the booking and the damage charged.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"rp-btn\" onclick=\"AdminRepairsJs.add()\">Open a repair</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"rp-tools\"><input type=\"search\" id=\"rp-q\" placeholder=\"Plate, problem or shop\" maxlength=\"40\" oninput=\"AdminRepairsJs.type()\" aria-label=\"Search\"><select id=\"rp-fs\" onchange=\"AdminRepairsJs.list(true)\" aria-label=\"Status\">" + Opt("open", "Open and in shop", "open") + Opt("Done", "Done", "open") + Opt("Cancelled", "Cancelled", "open") + Opt("all", "All repairs", "open") + "</select><select id=\"rp-fb\" onchange=\"AdminRepairsJs.list(true)\" aria-label=\"Branch\">" + Opt(string.Empty, "All my branches", string.Empty) + string.Concat(f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select></div>");
            sb.Append("<div id=\"rp-list\">" + ListHtml(c, f, string.Empty, "open", string.Empty, 1) + "</div>");
            response.SetElementContents("rp-main", sb.ToString());
            string carId = Ui.Clip(GetDataValue("car"), 60);
            if (carId.Length > 0 && Can(c, 'A') && Mine(c, f.Cars.FirstOrDefault(x => x.Id == carId)))
            {
                response.SetElementContents("rp-mbody", FormHtml(c, f, null, carId));
                response.ExecuteScript("AdminRepairsJs.opened('Open a repair');");
            }
            return response;
        }

        private static string Source(FleetData f, RepairRec r)
        {
            BookingRec? b = f.Bookings.FirstOrDefault(x => x.Id == r.BookingId);
            IncidentRec? i = f.Incidents.FirstOrDefault(x => x.Id == r.IncidentId);
            return b != null ? "From check-in of " + b.Code + (r.Charged > 0 ? ", " + Rental.Money(r.Charged) + " charged" : string.Empty) : i != null ? "From incident: " + i.Kind : string.Empty;
        }

        private static string ListHtml(Ctx c, FleetData f, string term, string st, string branch, int page)
        {
            List<RepairRec> rows = f.Repairs.Where(r =>
            {
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == r.CarId);
                return Mine(c, car) && (branch == string.Empty || car!.BranchId == branch)
                    && (st == "all" || (st == "open" ? Open(r) : r.Status == st))
                    && (term == string.Empty || Ui.Has(car!.Plate, term) || Ui.Has(r.Problem, term) || Ui.Has(r.Shop, term));
            }).OrderByDescending(r => Open(r)).ThenByDescending(r => r.Opened).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"rp-panel rp-empty\">No repairs match.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            DateTime now = DateTime.Now;
            StringBuilder sb = new StringBuilder("<div class=\"rp-wrapt\"><table class=\"rp-table\"><thead><tr><th>Car</th><th>Problem</th><th>Opened</th><th>Ready by</th><th>Status</th><th class=\"rp-num\">Cost</th></tr></thead><tbody>");
            foreach (RepairRec r in rows.Skip((page - 1) * Size).Take(Size))
            {
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == r.CarId);
                string src = Source(f, r);
                sb.Append("<tr class=\"rp-row\" onclick=\"AdminRepairsJs.edit('" + Ui.Js(r.Id) + "')\"><td class=\"rp-first\"><b>" + Ui.Esc(car?.Plate ?? r.CarId) + "</b><small>" + Ui.Esc(car == null ? string.Empty : car.Make + " " + car.Model + " · " + Avail.Location(f, car)) + "</small></td><td data-label=\"Problem\">" + Ui.Esc(r.Problem) + (src.Length > 0 ? "<small>" + Ui.Esc(src) + "</small>" : string.Empty) + "</td><td data-label=\"Opened\">" + Ui.Esc(Rental.Day(r.Opened)) + "</td><td data-label=\"Ready by\">" + (r.ReadyBy == null ? "<small>Not set</small>" : Ui.Esc(Rental.When(r.ReadyBy.Value)) + (Open(r) && r.ReadyBy < now ? "<small>Late</small>" : string.Empty)) + "</td><td data-label=\"Status\">" + Pill(r.Status) + (r.Shop.Length > 0 ? "<small>" + Ui.Esc(r.Shop) + "</small>" : string.Empty) + "</td><td data-label=\"Cost\" class=\"rp-num\">" + (r.Status == "Done" ? Rental.Money(r.Cost) : r.Estimate > 0 ? "<small>Estimate " + Rental.Money(r.Estimate) + "</small>" : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "repair", "repairs"));
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
            response.SetElementContents("rp-list", await ListNow(c));
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
            RepairRec? r = f.Repairs.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (r == null || !Mine(c, f.Cars.FirstOrDefault(x => x.Id == r.CarId))))
            {
                return await Refuse(response, c, "open repair", id, "That repair was not found at your branches.");
            }
            if (r == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "open repair", string.Empty, "You can view repairs but not open them.");
            }
            response.SetElementContents("rp-mbody", FormHtml(c, f, r, string.Empty));
            response.ExecuteScript("AdminRepairsJs.opened('" + Ui.Js(r == null ? "Open a repair" : "Repair · " + (f.Cars.FirstOrDefault(x => x.Id == r.CarId)?.Plate ?? string.Empty)) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, FleetData f, RepairRec? x, string carId)
        {
            bool isNew = x == null;
            RepairRec r = x ?? new RepairRec { CarId = carId, Opened = DateTime.Now };
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E') && Open(r);
            string ro = edit ? string.Empty : " readonly";
            CarRec? car = f.Cars.FirstOrDefault(k => k.Id == r.CarId);
            StringBuilder sb = new StringBuilder("<form class=\"rp-stack\" id=\"rp-form\" onsubmit=\"return AdminRepairsJs.save()\" novalidate><input type=\"hidden\" id=\"rp-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            if (isNew)
            {
                List<CarRec> cars = f.Cars.Where(k => Mine(c, k) && !NoRepair.Contains(k.Status)).OrderBy(k => k.Plate).ToList();
                sb.Append(Field("car", "Car", "<select id=\"rp-f-car\" data-k=\"car\">" + string.Concat(cars.Select(k => Opt(k.Id, k.Plate + " · " + k.Make + " " + k.Model + " · " + Avail.Location(f, k) + " · " + k.Status, r.CarId))) + "</select>", "Cars on rent or in transfer cannot go to repair until they are back."));
            }
            else
            {
                string src = Source(f, r);
                sb.Append("<div class=\"rp-bar\"><div>" + Pill(r.Status) + " <span class=\"rp-mini\">" + Ui.Esc(car == null ? string.Empty : car.Plate + " · " + car.Make + " " + car.Model + " · opened " + Rental.When(r.Opened)) + "</span></div>" + (src.Length > 0 ? "<span class=\"rp-mini\">" + Ui.Esc(src) + "</span>" : string.Empty) + "</div>");
            }
            sb.Append(Field("problem", "What needs fixing", Text("problem", r.Problem, 160, "text", ro)));
            sb.Append("<div class=\"rp-grid3\">" + Field("shop", "Shop", Text("shop", r.Shop, 60, "text", ro)) + Field("estimate", "Estimate (US dollars)", Text("estimate", Num(r.Estimate), 10, "text", ro + " inputmode=\"decimal\"")) + Field("ready", "Ready by (optional)", "<input type=\"datetime-local\" id=\"rp-f-ready\" data-k=\"ready\" value=\"" + (r.ReadyBy == null ? string.Empty : Rental.Iso(r.ReadyBy.Value)) + "\"" + ro + ">", "Empty: the car stays off the road until the repair is done.") + "</div>");
            if (!isNew && Open(r))
            {
                sb.Append(Field("status", "Stage", "<select id=\"rp-f-status\" data-k=\"status\"" + (edit ? string.Empty : " disabled") + ">" + Opt("Open", "Open, waiting for the shop", r.Status) + Opt("In shop", "In the shop", r.Status) + "</select>"));
            }
            sb.Append("<label class=\"rp-field\" for=\"rp-f-notes\"><span>Notes</span><textarea id=\"rp-f-notes\" data-k=\"notes\" rows=\"2\" maxlength=\"400\"" + ro + ">" + Ui.Esc(r.Notes) + "</textarea></label>");
            if (!isNew)
            {
                bool fin = Open(r) && Can(c, 'E');
                sb.Append("<p class=\"rp-sec\">" + (r.Status == "Done" ? "Finished " + Ui.Esc(r.ClosedAt == null ? string.Empty : Rental.When(r.ClosedAt.Value)) : "Finish") + "</p><div class=\"rp-grid2\">" + Field("cost", "Final cost (US dollars)", Text("cost", Num(r.Cost), 10, "text", (fin ? string.Empty : " readonly") + " inputmode=\"decimal\"")) + "</div>");
            }
            sb.Append("<div class=\"rp-acts\">" + (edit ? "<button type=\"submit\" class=\"rp-btn\">" + (isNew ? "Open repair" : "Save") + "</button>" : string.Empty) + (!isNew && Open(r) && Can(c, 'E') ? "<button type=\"button\" class=\"rp-btn rp-btn-ghost\" onclick=\"AdminRepairsJs.act('done')\">Repair done</button>" : string.Empty) + "<button type=\"button\" class=\"rp-btn rp-btn-ghost\" onclick=\"AdminRepairsJs.closeModal()\">Close</button>" + (!isNew && Open(r) && Can(c, 'E') ? "<button type=\"button\" class=\"rp-btn rp-btn-danger rp-push\" onclick=\"AdminRepairsJs.cancelIt()\">Cancel repair</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "open repair" : "edit repair", id, "You do not have the right to " + (isNew ? "open" : "change") + " repairs.");
            }
            string problem = Ui.Clip(GetDataValue("problem"), 160);
            string shop = Ui.Clip(GetDataValue("shop"), 60);
            decimal estimate = Dec(GetDataValue("estimate"));
            string readyRaw = Ui.Clip(GetDataValue("ready"), 20);
            DateTime? ready = null;
            string notes = Ui.Clip((GetDataValue("notes") ?? string.Empty).Replace("\r", string.Empty), 400);
            string status = GetDataValue("status") == "In shop" ? "In shop" : "Open";
            string eReady = string.Empty;
            if (readyRaw.Length > 0)
            {
                if (!Rental.ParseLocal(readyRaw, out DateTime rd) || rd <= DateTime.Now)
                {
                    eReady = "Choose a time in the future, or leave it empty.";
                }
                else
                {
                    ready = rd;
                }
            }
            string moved;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                RepairRec? r = f.Repairs.FirstOrDefault(x => x.Id == id);
                if (!isNew && (r == null || !Open(r)))
                {
                    return await Refuse(response, c, "edit repair", id, "Only an open repair can be changed.");
                }
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == (isNew ? Ui.Clip(GetDataValue("car"), 60) : r!.CarId));
                string eCar = car == null || !Mine(c, car) ? "Choose one of your cars." : isNew && NoRepair.Contains(car.Status) ? "This car is " + car.Status.ToLowerInvariant() + " and cannot go to repair now." : string.Empty;
                string eProb = problem.Length < 3 ? "Say what needs fixing." : string.Empty;
                string eEst = estimate < 0 || estimate > 200000 ? "Enter the estimate, or 0." : string.Empty;
                response.SetElementContents("rp-e-car", eCar);
                response.SetElementContents("rp-e-problem", eProb);
                response.SetElementContents("rp-e-estimate", eEst);
                response.SetElementContents("rp-e-ready", eReady);
                if (eCar + eProb + eEst + eReady != string.Empty)
                {
                    return response;
                }
                CarRec t = car!;
                (string err, List<BookingRec> m) = Ops.Reflow(f, t, DateTime.Now, ready, string.Empty);
                if (err.Length > 0)
                {
                    return await Refuse(response, c, isNew ? "open repair" : "edit repair", t.Plate, "Cannot take " + t.Plate + " off the road for this time. " + err);
                }
                if (isNew)
                {
                    r = new RepairRec { Id = Rental.NewId("r-"), CarId = t.Id, Opened = DateTime.Now, By = c.U.Id, Status = "Open" };
                    f.Repairs.Add(r);
                }
                RepairRec w = r!;
                w.Problem = problem;
                w.Shop = shop;
                w.Estimate = estimate;
                w.ReadyBy = ready;
                w.Notes = notes;
                if (!isNew)
                {
                    w.Status = status;
                }
                if (t.Status != "Repair")
                {
                    t.StatusSince = DateTime.Now;
                }
                t.Status = "Repair";
                t.StatusNote = problem;
                t.BlockUntil = Until(f, t.Id);
                Avail.Settle(f);
                await Ops.PersistAll(f);
                moved = Ops.Moved(m);
                target = t.Plate + " · " + problem;
            }
            await Store.Log(c.U.Id, isNew ? "open repair" : "edit repair", target + moved, true);
            response.SetElementContents("rp-list", await ListNow(c));
            response.ExecuteScript("AdminRepairsJs.saved();" + (moved.Length > 0 ? "AdminRepairsJs.toast('" + Ui.Js("Saved." + moved) + "');" : string.Empty));
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
                return await Refuse(response, c, "update repair", id, "You do not have the right to change repairs.");
            }
            decimal cost = Dec(GetDataValue("cost"));
            string notes = Ui.Clip((GetDataValue("notes") ?? string.Empty).Replace("\r", string.Empty), 400);
            string msg;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                RepairRec? r = f.Repairs.FirstOrDefault(x => x.Id == id);
                CarRec? car = r == null ? null : f.Cars.FirstOrDefault(x => x.Id == r.CarId);
                if (r == null || car == null || !Mine(c, car) || !Open(r))
                {
                    return await Refuse(response, c, "update repair", id, "That repair is not open.");
                }
                target = car.Plate + " · " + r.Problem;
                if (what == "done")
                {
                    if (cost < 0 || cost > 200000)
                    {
                        response.SetElementContents("rp-e-cost", "Enter the final cost, or 0.");
                        return response;
                    }
                    r.Status = "Done";
                    r.Cost = cost;
                    msg = "Repair done.";
                }
                else
                {
                    r.Status = "Cancelled";
                    msg = "Repair cancelled.";
                }
                r.Notes = notes;
                r.ClosedAt = DateTime.Now;
                if (car.Status == "Repair")
                {
                    Ops.Release(f, car, "Repair");
                    msg += car.Status == "Repair" ? " " + car.Plate + " still has another open repair." : " " + car.Plate + " is " + car.Status.ToLowerInvariant() + ".";
                }
                await Ops.PersistAll(f);
            }
            await Store.Log(c.U.Id, what == "done" ? "finish repair" : "cancel repair", target + (what == "done" ? " · " + Rental.Money(cost) : string.Empty), true);
            response.SetElementContents("rp-list", await ListNow(c));
            response.ExecuteScript("AdminRepairsJs.clean();AdminRepairsJs.shut();AdminRepairsJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }
    }
}
