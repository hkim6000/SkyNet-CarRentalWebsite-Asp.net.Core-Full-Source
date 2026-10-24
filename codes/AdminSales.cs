using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminSales : WebPage
    {
        private const string AreaKey = "sales";
        private const int Size = 12;
        private static readonly string[] NoSale = { "On rent", "In transit", "Stolen", "Sold" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Car sales | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSalesJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminSalesJs.denied();");
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
            response.SetElementContents("sa-brand", Ui.Esc(site.Brand));
            response.SetElementContents("sa-menu", await Auth.Menu("sa", "AdminSales", c));
            response.SetElementContents("sa-who", Auth.Who("sa", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminSalesJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"sa-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"sa-btn sa-btn-ghost sa-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminSalesJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"sa-btn sa-btn-ghost sa-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminSalesJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"sa-pill sa-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"sa-err\" id=\"sa-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"sa-field\" for=\"sa-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"sa-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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
            StringBuilder sb = new StringBuilder("<div class=\"sa-ph\"><div><h1>Car sales</h1><p>Sell a car out of the fleet. Bookings on it from the sale date move to other cars first; if one cannot move, the sale waits. A sold car keeps its history.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"sa-btn\" onclick=\"AdminSalesJs.add()\">Sell a car</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"sa-tiles\" id=\"sa-tiles\">" + Tiles(c, f) + "</div>");
            sb.Append("<div class=\"sa-tools\"><input type=\"search\" id=\"sa-q\" placeholder=\"Plate or buyer\" maxlength=\"40\" oninput=\"AdminSalesJs.type()\" aria-label=\"Search\"><select id=\"sa-fb\" onchange=\"AdminSalesJs.list(true)\" aria-label=\"Branch\">" + Opt(string.Empty, "All my branches", string.Empty) + string.Concat(f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select></div>");
            sb.Append("<div id=\"sa-list\">" + ListHtml(c, f, string.Empty, string.Empty, 1) + "</div>");
            response.SetElementContents("sa-main", sb.ToString());
            string carId = Ui.Clip(GetDataValue("car"), 60);
            if (carId.Length > 0 && Can(c, 'A') && Mine(c, f.Cars.FirstOrDefault(x => x.Id == carId)))
            {
                response.SetElementContents("sa-mbody", FormHtml(c, f, null, carId));
                response.ExecuteScript("AdminSalesJs.opened('Sell a car');");
            }
            return response;
        }

        private static List<SaleRec> MineSales(Ctx c, FleetData f)
        {
            return f.Sales.Where(s => Mine(c, f.Cars.FirstOrDefault(x => x.Id == s.CarId))).ToList();
        }

        private static string Tiles(Ctx c, FleetData f)
        {
            List<SaleRec> all = MineSales(c, f).Where(s => s.Date.Year == DateTime.Today.Year).ToList();
            decimal total = all.Sum(s => s.Price);
            decimal cost = all.Sum(s => f.Cars.FirstOrDefault(x => x.Id == s.CarId)?.Cost ?? 0);
            return "<div class=\"sa-tile\"><b>" + all.Count + "</b><span>cars sold this year</span></div><div class=\"sa-tile\"><b>" + Rental.Money(total) + "</b><span>sale total</span></div><div class=\"sa-tile\"><b>" + Rental.Money(total - cost) + "</b><span>against purchase cost</span></div>";
        }

        private static string ListHtml(Ctx c, FleetData f, string term, string branch, int page)
        {
            List<SaleRec> rows = MineSales(c, f).Where(s =>
            {
                CarRec car = f.Cars.First(x => x.Id == s.CarId);
                return (branch == string.Empty || car.BranchId == branch) && (term == string.Empty || Ui.Has(car.Plate, term) || Ui.Has(s.Buyer, term));
            }).OrderByDescending(s => s.Date).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"sa-panel sa-empty\">No car sales yet.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"sa-wrapt\"><table class=\"sa-table\"><thead><tr><th>Car</th><th>Sold</th><th>Buyer</th><th class=\"sa-num\">Mileage</th><th class=\"sa-num\">Price</th><th class=\"sa-num\">Against cost</th></tr></thead><tbody>");
            foreach (SaleRec s in rows.Skip((page - 1) * Size).Take(Size))
            {
                CarRec car = f.Cars.First(x => x.Id == s.CarId);
                decimal diff = s.Price - car.Cost;
                sb.Append("<tr class=\"sa-row\" onclick=\"AdminSalesJs.edit('" + Ui.Js(s.Id) + "')\"><td class=\"sa-first\"><b>" + Ui.Esc(car.Plate) + "</b><small>" + Ui.Esc(car.Year + " " + car.Make + " " + car.Model + " · " + Sheet.ClassName(f, car.ClassId)) + "</small></td><td data-label=\"Sold\">" + Ui.Esc(Rental.Day(s.Date)) + "</td><td data-label=\"Buyer\">" + Ui.Esc(s.Buyer) + "<small>" + Ui.Esc(s.Contact) + "</small></td><td data-label=\"Mileage\" class=\"sa-num\">" + s.Mileage.ToString("N0", Rental.Us) + "</td><td data-label=\"Price\" class=\"sa-num\">" + Rental.Money(s.Price) + "</td><td data-label=\"Against cost\" class=\"sa-num\"><span class=\"" + (diff < 0 ? "sa-neg" : "sa-pos") + "\">" + (diff >= 0 ? "+" : string.Empty) + Rental.Money(diff) + "</span></td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "sale", "sales"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            FleetData f = await Avail.LoadSettled();
            response.SetElementContents("sa-list", ListHtml(c, f, Term(GetDataValue("term")), Ui.Clip(GetDataValue("fb"), 60), PageNo(GetDataValue("pg"))));
            response.SetElementContents("sa-tiles", Tiles(c, f));
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
            SaleRec? s = f.Sales.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (s == null || !Mine(c, f.Cars.FirstOrDefault(x => x.Id == s.CarId))))
            {
                return await Refuse(response, c, "open sale", id, "That sale was not found at your branches.");
            }
            if (s == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "sell car", string.Empty, "You can view car sales but not sell cars.");
            }
            response.SetElementContents("sa-mbody", FormHtml(c, f, s, string.Empty));
            response.ExecuteScript("AdminSalesJs.opened('" + Ui.Js(s == null ? "Sell a car" : "Sale · " + (f.Cars.FirstOrDefault(x => x.Id == s.CarId)?.Plate ?? string.Empty)) + "');" + (s == null ? "AdminSalesJs.carPick();AdminSalesJs.clean();" : string.Empty));
            return response;
        }

        private static string FormHtml(Ctx c, FleetData f, SaleRec? x, string carId)
        {
            bool isNew = x == null;
            CarRec? pre = f.Cars.FirstOrDefault(k => k.Id == carId);
            SaleRec s = x ?? new SaleRec { CarId = carId, Date = DateTime.Today, Mileage = pre?.Mileage ?? 0 };
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            string ro = edit ? string.Empty : " readonly";
            CarRec? car = f.Cars.FirstOrDefault(k => k.Id == s.CarId);
            StringBuilder sb = new StringBuilder("<form class=\"sa-stack\" id=\"sa-form\" onsubmit=\"return AdminSalesJs.save()\" novalidate><input type=\"hidden\" id=\"sa-f-id\" value=\"" + Ui.Esc(s.Id) + "\">");
            if (isNew)
            {
                List<CarRec> cars = f.Cars.Where(k => Mine(c, k) && !NoSale.Contains(k.Status)).OrderBy(k => k.Plate).ToList();
                sb.Append(Field("car", "Car", "<select id=\"sa-f-car\" data-k=\"car\" onchange=\"AdminSalesJs.carPick()\">" + string.Concat(cars.Select(k => "<option value=\"" + Ui.Esc(k.Id) + "\" data-m=\"" + k.Mileage + "\"" + (k.Id == s.CarId ? " selected" : string.Empty) + ">" + Ui.Esc(k.Plate + " · " + k.Year + " " + k.Make + " " + k.Model + " · " + k.Mileage.ToString("N0", Rental.Us) + " mi · bought for " + Rental.Money(k.Cost)) + "</option>")) + "</select>", "Cars on rent or in transfer cannot be sold until they are back."));
            }
            else if (car != null)
            {
                sb.Append("<div class=\"sa-bar\"><div>" + Pill("Sold") + " <span class=\"sa-mini\">" + Ui.Esc(car.Plate + " · " + car.Year + " " + car.Make + " " + car.Model + " · bought " + Rental.Day(car.Purchased) + " for " + Rental.Money(car.Cost)) + "</span></div></div>");
            }
            sb.Append("<div class=\"sa-grid3\">" + Field("date", "Sale date", "<input type=\"date\" id=\"sa-f-date\" data-k=\"date\" value=\"" + s.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\"" + ro + ">") + Field("price", "Price (US dollars)", Text("price", s.Price > 0 ? Num(s.Price) : string.Empty, 10, "text", ro + " inputmode=\"decimal\"")) + Field("mileage", "Mileage at sale", Text("mileage", s.Mileage.ToString(CultureInfo.InvariantCulture), 7, "number", isNew && edit ? string.Empty : " readonly")) + "</div>");
            sb.Append("<div class=\"sa-grid2\">" + Field("buyer", "Buyer", Text("buyer", s.Buyer, 80, "text", ro)) + Field("contact", "Buyer phone or email", Text("contact", s.Contact, 80, "text", ro)) + "</div>");
            sb.Append("<label class=\"sa-field\" for=\"sa-f-notes\"><span>Notes</span><textarea id=\"sa-f-notes\" data-k=\"notes\" rows=\"2\" maxlength=\"400\"" + ro + ">" + Ui.Esc(s.Notes) + "</textarea></label>");
            sb.Append("<div class=\"sa-acts\">" + (edit ? "<button type=\"submit\" class=\"sa-btn\">" + (isNew ? "Record the sale" : "Save") + "</button>" : string.Empty) + "<button type=\"button\" class=\"sa-btn sa-btn-ghost\" onclick=\"AdminSalesJs.closeModal()\">Close</button>" + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"sa-btn sa-btn-danger sa-push\" onclick=\"AdminSalesJs.del('" + Ui.Js(s.Id) + "','" + Ui.Js(car?.Plate ?? string.Empty) + "')\">Undo the sale</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "sell car" : "edit sale", id, "You do not have the right to " + (isNew ? "sell cars" : "change sales") + ".");
            }
            bool okD = Rental.ParseDate(GetDataValue("date"), out DateTime date);
            decimal price = Dec(GetDataValue("price"));
            int mileage = Int(GetDataValue("mileage"), -1);
            string buyer = Ui.Clip(GetDataValue("buyer"), 80);
            string contact = Ui.Clip(GetDataValue("contact"), 80);
            string notes = Ui.Clip((GetDataValue("notes") ?? string.Empty).Replace("\r", string.Empty), 400);
            string moved = string.Empty;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                SaleRec? s = f.Sales.FirstOrDefault(x => x.Id == id);
                if (!isNew && s == null)
                {
                    return await Refuse(response, c, "edit sale", id, "That sale was not found.");
                }
                CarRec? car = f.Cars.FirstOrDefault(x => x.Id == (isNew ? Ui.Clip(GetDataValue("car"), 60) : s!.CarId));
                Dictionary<string, string> e = new Dictionary<string, string>
                {
                    ["car"] = car == null || !Mine(c, car) ? "Choose one of your cars." : isNew && NoSale.Contains(car.Status) ? "This car is " + car.Status.ToLowerInvariant() + " and cannot be sold now." : string.Empty,
                    ["date"] = !okD || date > DateTime.Today ? "Enter the sale date, today or earlier." : car != null && date < car.Purchased.Date ? "The sale cannot be before the purchase date." : string.Empty,
                    ["price"] = price <= 0 || price > 1000000 ? "Enter the sale price." : string.Empty,
                    ["mileage"] = isNew && car != null && (mileage < car.Mileage || mileage > car.Mileage + 5000) ? "Mileage from " + car.Mileage.ToString("N0", Rental.Us) + " up." : string.Empty,
                    ["buyer"] = buyer.Length < 2 ? "Enter the buyer." : string.Empty
                };
                foreach (KeyValuePair<string, string> kv in e)
                {
                    response.SetElementContents("sa-e-" + kv.Key, kv.Value);
                }
                if (e.Values.Any(v => v.Length > 0))
                {
                    return response;
                }
                CarRec t = car!;
                if (isNew)
                {
                    DateTime now = DateTime.Now;
                    (string err, List<BookingRec> m) = Ops.Reflow(f, t, date < now ? now : date, null, string.Empty);
                    if (err.Length > 0)
                    {
                        return await Refuse(response, c, "sell car", t.Plate, "Cannot sell " + t.Plate + " yet. " + err);
                    }
                    moved = Ops.Moved(m);
                    s = new SaleRec { Id = Rental.NewId("v-"), CarId = t.Id, PrevStatus = t.Status, Mileage = mileage, Created = now, By = c.U.Id };
                    f.Sales.Add(s);
                    foreach (ServiceRec v in f.Services.Where(v => v.CarId == t.Id && (v.Status == "Planned" || v.Status == "In progress")))
                    {
                        v.Status = "Cancelled";
                    }
                    foreach (TransferRec tr in f.Transfers.Where(tr => tr.CarId == t.Id && tr.Status == "Planned"))
                    {
                        tr.Status = "Cancelled";
                    }
                    t.Mileage = mileage;
                    t.Status = "Sold";
                    t.StatusNote = "Sold to " + buyer;
                    t.BlockUntil = null;
                    t.StatusSince = date;
                }
                SaleRec w = s!;
                w.Date = date;
                w.Price = price;
                w.Buyer = buyer;
                w.Contact = contact;
                w.Notes = notes;
                Avail.Settle(f);
                await Ops.PersistAll(f);
                target = t.Plate + " · " + buyer + " · " + Rental.Money(price);
            }
            await Store.Log(c.U.Id, isNew ? "sell car" : "edit sale", target + moved, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminSalesJs.saved();" + (moved.Length > 0 ? "AdminSalesJs.toast('" + Ui.Js("Sold." + moved) + "');" : string.Empty));
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
                return await Refuse(response, c, "undo sale", id, "You do not have the right to undo sales.");
            }
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                SaleRec? s = f.Sales.FirstOrDefault(x => x.Id == id);
                CarRec? car = s == null ? null : f.Cars.FirstOrDefault(x => x.Id == s.CarId);
                if (s == null || car == null || !Mine(c, car))
                {
                    return await Refuse(response, c, "undo sale", id, "That sale was not found.");
                }
                f.Sales.Remove(s);
                target = car.Plate + " · " + s.Buyer;
                if (car.Status == "Sold")
                {
                    car.Status = "Hold";
                    car.StatusNote = "Sale undone, check the car";
                    car.BlockUntil = null;
                    car.StatusSince = DateTime.Now;
                }
                Avail.Settle(f);
                await Ops.PersistAll(f);
            }
            await Store.Log(c.U.Id, "undo sale", target, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminSalesJs.deleted();AdminSalesJs.toast('Sale undone. The car is back in the fleet, on hold until someone marks it ready.');");
            return response;
        }
    }
}
