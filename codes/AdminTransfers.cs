using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminTransfers : WebPage
    {
        private const string AreaKey = "transfers";
        private const int Size = 12;
        private static readonly string[] Movable = { "Available", "Reserved", "Cleaning" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Transfers | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminTransfersJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminTransfersJs.denied();");
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
            response.SetElementContents("tf-brand", Ui.Esc(site.Brand));
            response.SetElementContents("tf-menu", await Auth.Menu("tf", "AdminTransfers", c));
            response.SetElementContents("tf-who", Auth.Who("tf", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminTransfersJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"tf-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"tf-btn tf-btn-ghost tf-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminTransfersJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"tf-btn tf-btn-ghost tf-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminTransfersJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"tf-pill tf-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"tf-err\" id=\"tf-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"tf-field\" for=\"tf-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"tf-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static string Bn(FleetData f, string id)
        {
            return f.Branches.FirstOrDefault(b => b.Id == id)?.Name ?? id;
        }

        private static bool Mine(Ctx c, TransferRec t)
        {
            return Access.BranchOk(c.U, t.From) || Access.BranchOk(c.U, t.To);
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
            StringBuilder sb = new StringBuilder("<div class=\"tf-ph\"><div><h1>Transfers</h1><p>Move cars between branches. A planned car stays bookable at its branch until it leaves and counts at the new branch from its arrival time.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"tf-btn\" onclick=\"AdminTransfersJs.add()\">Plan a transfer</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"tf-tools\"><input type=\"search\" id=\"tf-q\" placeholder=\"Plate or note\" maxlength=\"40\" oninput=\"AdminTransfersJs.type()\" aria-label=\"Search transfers\"><select id=\"tf-fs\" onchange=\"AdminTransfersJs.list(true)\" aria-label=\"Status\">" + Opt("open", "Planned and in transit", "open") + Opt("Planned", "Planned", "open") + Opt("In transit", "In transit", "open") + Opt("Arrived", "Arrived", "open") + Opt("Cancelled", "Cancelled", "open") + Opt(string.Empty, "All", "open") + "</select><select id=\"tf-fb\" onchange=\"AdminTransfersJs.list(true)\" aria-label=\"Branch\">" + Opt(string.Empty, "All my branches", string.Empty) + string.Concat(f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select></div>");
            sb.Append("<div id=\"tf-list\">" + ListHtml(c, f, string.Empty, "open", string.Empty, 1) + "</div>");
            response.SetElementContents("tf-main", sb.ToString());
            return response;
        }

        private static string ListHtml(Ctx c, FleetData f, string term, string status, string branch, int page)
        {
            List<TransferRec> rows = f.Transfers.Where(t => Mine(c, t)
                && (status == string.Empty || (status == "open" ? t.Status == "Planned" || t.Status == "In transit" : t.Status == status))
                && (branch == string.Empty || t.From == branch || t.To == branch)
                && (term == string.Empty || Ui.Has(f.Cars.FirstOrDefault(x => x.Id == t.CarId)?.Plate, term) || Ui.Has(t.Note, term))).OrderByDescending(t => t.Status == "In transit").ThenByDescending(t => t.Status == "Planned").ThenByDescending(t => t.Depart).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"tf-panel tf-empty\">No transfers match.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"tf-wrapt\"><table class=\"tf-table\"><thead><tr><th>Car</th><th>Route</th><th>Leaves</th><th>Arrives</th><th>Status</th><th></th></tr></thead><tbody>");
            foreach (TransferRec t in rows.Skip((page - 1) * Size).Take(Size))
            {
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == t.CarId);
                string id = Ui.Js(t.Id);
                string acts = string.Empty;
                if (Can(c, 'E'))
                {
                    if (t.Status == "Planned")
                    {
                        acts = "<button type=\"button\" class=\"tf-btn tf-btn-ghost tf-btn-sm\" onclick=\"AdminTransfersJs.act('" + id + "','depart')\">Left now</button><button type=\"button\" class=\"tf-btn tf-btn-danger tf-btn-sm\" onclick=\"AdminTransfersJs.act('" + id + "','cancel')\">Cancel</button>";
                    }
                    else if (t.Status == "In transit")
                    {
                        acts = "<button type=\"button\" class=\"tf-btn tf-btn-sm\" onclick=\"AdminTransfersJs.act('" + id + "','arrive')\">Arrived now</button>";
                    }
                }
                sb.Append("<tr><td class=\"tf-first\"><b>" + Ui.Esc(car?.Plate ?? t.CarId) + "</b><small>" + Ui.Esc(car == null ? string.Empty : car.Make + " " + car.Model) + (t.Note.Length > 0 ? " · " + Ui.Esc(t.Note) : string.Empty) + "</small></td><td data-label=\"Route\">" + Ui.Esc(Bn(f, t.From)) + " &rarr; " + Ui.Esc(Bn(f, t.To)) + "</td><td data-label=\"Leaves\">" + Ui.Esc(Rental.When(t.Depart)) + "</td><td data-label=\"Arrives\">" + Ui.Esc(Rental.When(t.Arrive)) + "</td><td data-label=\"Status\">" + Pill(t.Status) + "</td><td data-label=\"\"><div class=\"tf-acts-row\">" + acts + "</div></td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "transfer", "transfers"));
            return sb.ToString();
        }

        private async Task<string> ListNow(Ctx c)
        {
            FleetData f = await Avail.LoadSettled();
            return ListHtml(c, f, Term(GetDataValue("term")), Ui.Clip(GetDataValue("fs"), 20), Ui.Clip(GetDataValue("fb"), 60), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("tf-list", await ListNow(c));
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
            if (!Can(c, 'A'))
            {
                return await Refuse(response, c, "plan transfer", string.Empty, "You can view transfers but not plan them.");
            }
            FleetData f = await Avail.LoadSettled();
            List<CarRec> cars = f.Cars.Where(x => Movable.Contains(x.Status) && Access.BranchOk(c.U, x.BranchId) && !f.Transfers.Any(t => t.CarId == x.Id && (t.Status == "Planned" || t.Status == "In transit"))).OrderBy(x => x.BranchId).ThenBy(x => x.Plate).ToList();
            DateTime d0 = DateTime.Now.AddHours(1);
            d0 = new DateTime(d0.Year, d0.Month, d0.Day, d0.Hour, 0, 0);
            StringBuilder sb = new StringBuilder("<form class=\"tf-stack\" id=\"tf-form\" onsubmit=\"return AdminTransfersJs.save()\" novalidate>");
            if (cars.Count == 0)
            {
                sb.Append("<p class=\"tf-note\">No car at your branches can move now. A car must be available, reserved or in cleaning, with no other transfer planned.</p>");
            }
            sb.Append(Field("car", "Car", "<select id=\"tf-f-car\" data-k=\"car\">" + string.Concat(cars.Select(x => Opt(x.Id, x.Plate + " · " + x.Make + " " + x.Model + " · " + (f.Classes.FirstOrDefault(k => k.Id == x.ClassId)?.Name ?? string.Empty) + " · at " + Bn(f, x.BranchId), string.Empty))) + "</select>"));
            sb.Append(Field("to", "To branch", "<select id=\"tf-f-to\" data-k=\"to\">" + string.Concat(f.Branches.Where(b => b.Open).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select>"));
            sb.Append("<div class=\"tf-grid2\">" + Field("depart", "Leaves at", "<input type=\"datetime-local\" id=\"tf-f-depart\" data-k=\"depart\" value=\"" + Rental.Iso(d0) + "\">") + Field("arrive", "Arrives at", "<input type=\"datetime-local\" id=\"tf-f-arrive\" data-k=\"arrive\" value=\"" + Rental.Iso(d0.AddHours(2)) + "\">") + "</div>");
            sb.Append(Field("note", "Note (driver, reason)", Text("note", string.Empty, 120)));
            sb.Append("<p class=\"tf-hint\">Bookings on this car from the time it leaves are moved to other free cars of the same class. If one cannot move, the transfer is refused.</p>");
            sb.Append("<div class=\"tf-acts\"><button type=\"submit\" class=\"tf-btn\"" + (cars.Count == 0 ? " disabled" : string.Empty) + ">Plan transfer</button><button type=\"button\" class=\"tf-btn tf-btn-ghost\" onclick=\"AdminTransfersJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("tf-mbody", sb.ToString());
            response.ExecuteScript("AdminTransfersJs.opened('Plan a transfer');");
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
            if (!Can(c, 'A'))
            {
                return await Refuse(response, c, "plan transfer", string.Empty, "You do not have the right to plan transfers.");
            }
            string carId = Ui.Clip(GetDataValue("car"), 60);
            string to = Ui.Clip(GetDataValue("to"), 60);
            bool dOk = Rental.ParseLocal(GetDataValue("depart"), out DateTime depart);
            bool aOk = Rental.ParseLocal(GetDataValue("arrive"), out DateTime arrive);
            string note = Ui.Clip(GetDataValue("note"), 120);
            string moved;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == carId);
                Dictionary<string, string> e = new Dictionary<string, string>
                {
                    ["car"] = car == null || !Access.BranchOk(c.U, car.BranchId) ? "Choose a car." : !Movable.Contains(car.Status) ? "This car is " + car.Status.ToLowerInvariant() + " and cannot move now." : f.Transfers.Any(t => t.CarId == car.Id && (t.Status == "Planned" || t.Status == "In transit")) ? "This car already has a transfer planned." : string.Empty,
                    ["to"] = !f.Branches.Any(b => b.Id == to && b.Open) ? "Choose an open branch." : car != null && car.BranchId == to ? "The car is already at this branch." : string.Empty,
                    ["depart"] = !dOk ? "Choose when the car leaves." : depart < DateTime.Now.AddMinutes(-30) ? "The car cannot leave in the past." : depart > DateTime.Now.AddDays(90) ? "Plan transfers up to 90 days ahead." : string.Empty,
                    ["arrive"] = !aOk ? "Choose when the car arrives." : dOk && arrive <= depart ? "Arrival must be after it leaves." : dOk && (arrive - depart).TotalHours > 72 ? "A transfer can take up to 72 hours." : string.Empty,
                    ["note"] = string.Empty
                };
                foreach (KeyValuePair<string, string> kv in e)
                {
                    response.SetElementContents("tf-e-" + kv.Key, kv.Value);
                }
                if (e.Values.Any(v => v.Length > 0))
                {
                    return response;
                }
                CarRec t = car!;
                (string err, List<BookingRec> m) = Ops.Reflow(f, t, depart, null, string.Empty);
                if (err.Length > 0)
                {
                    return await Refuse(response, c, "plan transfer", t.Plate, "Cannot move " + t.Plate + ". " + err);
                }
                f.Transfers.Add(new TransferRec { Id = Rental.NewId("t-"), CarId = t.Id, From = t.BranchId, To = to, Depart = depart, Arrive = arrive, Status = "Planned", Note = note, Created = DateTime.Now, By = c.U.Id });
                Avail.Settle(f);
                moved = m.Count > 0 ? " " + Ui.Count(m.Count, "booking moved", "bookings moved") + " to other cars: " + string.Join(", ", m.Select(b => b.Code)) + "." : string.Empty;
                target = t.Plate + " · " + Bn(f, t.BranchId) + " to " + Bn(f, to);
                await Ops.Persist(f, true, true, true);
            }
            await Store.Log(c.U.Id, "plan transfer", target + moved, true);
            response.SetElementContents("tf-list", await ListNow(c));
            response.ExecuteScript("AdminTransfersJs.saved();" + (moved.Length > 0 ? "AdminTransfersJs.toast('" + Ui.Js("Transfer planned." + moved) + "');" : string.Empty));
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
                return await Refuse(response, c, "update transfer", id, "You do not have the right to change transfers.");
            }
            string msg;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                TransferRec? t = f.Transfers.FirstOrDefault(x => x.Id == id);
                CarRec? car = t == null ? null : f.Cars.FirstOrDefault(x => x.Id == t.CarId);
                if (t == null || car == null || !Mine(c, t))
                {
                    return await Refuse(response, c, "update transfer", id, "That transfer was not found.");
                }
                DateTime now = DateTime.Now;
                target = car.Plate + " · " + Bn(f, t.From) + " to " + Bn(f, t.To);
                if (what == "depart" && t.Status == "Planned")
                {
                    if (!Movable.Contains(car.Status))
                    {
                        return await Refuse(response, c, "transfer left", target, "The car is " + car.Status.ToLowerInvariant() + " and cannot leave now.");
                    }
                    (string err, List<BookingRec> _) = Ops.Reflow(f, car, now, null, string.Empty);
                    if (err.Length > 0)
                    {
                        return await Refuse(response, c, "transfer left", target, err);
                    }
                    if (t.Arrive <= now)
                    {
                        t.Arrive = now.AddHours(1);
                    }
                    t.Depart = now;
                    t.Status = "In transit";
                    car.Status = "In transit";
                    car.StatusSince = now;
                    msg = car.Plate + " is on its way.";
                }
                else if (what == "arrive" && t.Status == "In transit")
                {
                    t.Arrive = now;
                    t.Status = "Arrived";
                    car.BranchId = t.To;
                    car.Status = "Available";
                    car.StatusSince = now;
                    msg = car.Plate + " arrived at " + Bn(f, t.To) + ".";
                }
                else if (what == "cancel" && t.Status == "Planned")
                {
                    t.Status = "Cancelled";
                    msg = "Transfer cancelled.";
                }
                else
                {
                    return await Refuse(response, c, "update transfer", target, "That transfer has already moved on. The list is refreshed.");
                }
                Avail.Settle(f);
                await Ops.Persist(f, true, true, true);
            }
            await Store.Log(c.U.Id, what == "depart" ? "transfer left" : what == "arrive" ? "transfer arrived" : "cancel transfer", target, true);
            response.SetElementContents("tf-list", await ListNow(c));
            response.ExecuteScript("AdminTransfersJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }
    }
}
