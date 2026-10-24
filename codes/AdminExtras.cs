using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminExtras : WebPage
    {
        private const string AreaKey = "extras";
        private const int Size = 10;
        private static readonly (string Key, string Name, string Hint)[] Kinds =
        {
            ("item", "Item with stock", "Counted per branch, for example child seats or GPS units. A booking can only add one if one is free for its dates."),
            ("service", "Service", "No stock, for example an additional driver or roadside plus."),
            ("cover", "Coverage option", "Customers choose one coverage option. Basic coverage is always included at no charge.")
        };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Extras | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminExtrasJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminExtrasJs.denied();");
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
            response.SetElementContents("ex-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ex-menu", await Auth.Menu("ex", "AdminExtras", c));
            response.SetElementContents("ex-who", Auth.Who("ex", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminExtrasJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"ex-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"ex-btn ex-btn-ghost ex-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminExtrasJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"ex-btn ex-btn-ghost ex-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminExtrasJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"ex-pill ex-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"ex-err\" id=\"ex-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"ex-field\" for=\"ex-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"ex-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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
            FleetData f = await Avail.Load();
            await Shell(response, c, f.Site);
            response.SetElementContents("ex-main", "<div class=\"ex-ph\"><div><h1>Extras</h1><p>Child seats, GPS units, additional drivers and coverage options that customers add to a booking. Items with stock are counted per branch.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"ex-btn\" onclick=\"AdminExtrasJs.add()\">Add an extra</button>" : string.Empty) + "</div><div class=\"ex-tools\"><input type=\"search\" id=\"ex-q\" placeholder=\"Search extras\" maxlength=\"40\" oninput=\"AdminExtrasJs.type()\" aria-label=\"Search extras\"></div><div id=\"ex-list\">" + ListHtml(f, string.Empty, 1) + "</div>");
            return response;
        }

        private static string KindName(string k)
        {
            return Kinds.FirstOrDefault(x => x.Key == k).Name ?? k;
        }

        private static string ListHtml(FleetData f, string term, int page)
        {
            List<ExtraRec> rows = f.Extras.Where(x => term == string.Empty || Ui.Has(x.Name, term) || Ui.Has(x.Note, term)).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ex-panel ex-empty\">" + (f.Extras.Count == 0 ? "No extras yet. Add the first one." : "No extras match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            DateTime now = DateTime.Now;
            StringBuilder sb = new StringBuilder("<div class=\"ex-wrapt\"><table class=\"ex-table\"><thead><tr><th>Extra</th><th>Kind</th><th class=\"ex-num\">Per day</th><th>Stock by branch</th><th>Out now</th><th>Website</th></tr></thead><tbody>");
            foreach (ExtraRec x in rows.Skip((page - 1) * Size).Take(Size))
            {
                int outNow = f.Bookings.Where(b => Avail.Out(b)).Sum(b => b.Extras.Where(q => q.Id == x.Id).Sum(q => q.Qty));
                string stock = x.Kind != "item" ? "<small>Not counted</small>" : string.Join("<br>", f.Branches.Select(b => Ui.Esc(b.Name) + ": " + (x.Stock.TryGetValue(b.Id, out int n) ? n : 0)));
                sb.Append("<tr class=\"ex-row\" onclick=\"AdminExtrasJs.edit('" + Ui.Js(x.Id) + "')\"><td class=\"ex-first\"><b>" + Ui.Esc(x.Name) + "</b><small>" + Ui.Esc(x.Note) + "</small></td><td data-label=\"Kind\">" + Ui.Esc(KindName(x.Kind)) + "</td><td data-label=\"Per day\" class=\"ex-num\">" + Rental.Money(x.PerDay) + "</td><td data-label=\"Stock by branch\">" + stock + "</td><td data-label=\"Out now\">" + outNow + "</td><td data-label=\"Website\"><span class=\"ex-pill " + (x.Show ? "ex-pill-go\">Offered" : "ex-pill-off\">Hidden") + "</span></td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "extra", "extras"));
            return sb.ToString();
        }

        private async Task<string> ListNow()
        {
            FleetData f = await Avail.Load();
            return ListHtml(f, Term(GetDataValue("term")), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("ex-list", await ListNow());
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
            FleetData f = await Avail.Load();
            ExtraRec? x = f.Extras.FirstOrDefault(y => y.Id == id);
            if (id.Length > 0 && x == null)
            {
                return await Refuse(response, c, "open extra", id, "That extra was not found.");
            }
            if (x == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add extra", string.Empty, "You can view extras but not add them.");
            }
            response.SetElementContents("ex-mbody", FormHtml(c, f, x));
            response.ExecuteScript("AdminExtrasJs.opened('" + Ui.Js(x == null ? "New extra" : x.Name) + "');AdminExtrasJs.kind();");
            return response;
        }

        private static string FormHtml(Ctx c, FleetData f, ExtraRec? x)
        {
            bool isNew = x == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            ExtraRec r = x ?? new ExtraRec();
            string ro = edit ? string.Empty : " readonly";
            StringBuilder sb = new StringBuilder("<form class=\"ex-stack\" id=\"ex-form\" onsubmit=\"return AdminExtrasJs.save()\" novalidate><input type=\"hidden\" id=\"ex-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            sb.Append("<div class=\"ex-grid2\">" + Field("name", "Name", Text("name", r.Name, 40, "text", ro)) + Field("kind", "Kind", "<select id=\"ex-f-kind\" data-k=\"kind\" onchange=\"AdminExtrasJs.kind()\"" + (edit ? string.Empty : " disabled") + ">" + string.Concat(Kinds.Select(k => Opt(k.Key, k.Name, r.Kind))) + "</select>") + "</div>");
            sb.Append("<p class=\"ex-hint\" id=\"ex-khint\">" + string.Concat(Kinds.Select(k => "<span data-kind=\"" + k.Key + "\"" + (k.Key == r.Kind ? string.Empty : " hidden") + ">" + Ui.Esc(k.Hint) + "</span>")) + "</p>");
            sb.Append(Field("note", "Short description for customers", Text("note", r.Note, 120, "text", ro)));
            sb.Append("<div class=\"ex-grid2\">" + Field("perday", "Price per day (US dollars)", Text("perday", Num(r.PerDay), 8, "text", ro + " inputmode=\"decimal\"")) + Field("maxqty", "Most per booking", Text("maxqty", r.MaxQty.ToString(CultureInfo.InvariantCulture), 1, "number", ro + " min=\"1\" max=\"9\""), "Coverage is always one per booking.") + "</div>");
            sb.Append("<div class=\"ex-field\" id=\"ex-stockbox\"><span>Stock at each branch</span><div class=\"ex-grid3\">");
            foreach (BranchRec b in f.Branches)
            {
                sb.Append("<label class=\"ex-field\"><span>" + Ui.Esc(b.Name) + (b.Open ? string.Empty : " (closed)") + "</span><input type=\"number\" class=\"ex-stock\" data-b=\"" + Ui.Esc(b.Id) + "\" min=\"0\" max=\"999\" value=\"" + (r.Stock.TryGetValue(b.Id, out int n) ? n : 0) + "\"" + ro + "></label>");
            }
            sb.Append("</div>" + Err("stock") + "</div>");
            sb.Append("<label class=\"ex-check\"><input type=\"checkbox\" id=\"ex-f-show\" data-k=\"show\"" + (r.Show ? " checked" : string.Empty) + (edit ? string.Empty : " disabled") + "> Offer on the website</label>");
            sb.Append("<div class=\"ex-acts\">" + (edit ? "<button type=\"submit\" class=\"ex-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"ex-btn ex-btn-ghost\" onclick=\"AdminExtrasJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"ex-btn ex-btn-danger ex-push\" onclick=\"AdminExtrasJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "add extra" : "edit extra", id, "You do not have the right to " + (isNew ? "add" : "change") + " extras.");
            }
            string name = Ui.Clip(GetDataValue("name"), 40);
            string kind = Kinds.Any(k => k.Key == GetDataValue("kind")) ? GetDataValue("kind")! : "item";
            string note = Ui.Clip(GetDataValue("note"), 120);
            decimal perDay = Dec(GetDataValue("perday"));
            int maxQty = kind == "cover" ? 1 : Int(GetDataValue("maxqty"), -1);
            bool show = GetDataValue("show") == "1";
            Dictionary<string, int> stock = new Dictionary<string, int>();
            bool badStock = false;
            foreach (string part in (GetDataValue("stock") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split(':');
                int n = kv.Length == 2 ? Int(kv[1], -1) : -1;
                if (n < 0 || n > 999)
                {
                    badStock = true;
                }
                else if (kv[0].Length > 0)
                {
                    stock[kv[0]] = n;
                }
            }
            FleetData f = await Avail.Load();
            stock = stock.Where(kv => f.Branches.Any(b => b.Id == kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Please enter the name." : string.Empty,
                ["note"] = string.Empty,
                ["perday"] = perDay < 0 || perDay > 1000 ? "Enter the price per day, or 0 if free." : string.Empty,
                ["maxqty"] = maxQty < 1 || maxQty > 9 ? "From 1 to 9." : string.Empty,
                ["stock"] = kind == "item" && badStock ? "Stock must be a whole number from 0 to 999." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("ex-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<ExtraRec> all = await Store.Rows<ExtraRec>("extras");
                if (all.Any(x => x.Id != id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    response.SetElementContents("ex-e-name", "Another extra already has this name.");
                    return response;
                }
                ExtraRec? r = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    string key = "ex-" + Ui.Slug(name);
                    while (all.Any(x => x.Id == key))
                    {
                        key += "-2";
                    }
                    r = new ExtraRec { Id = key };
                    all.Add(r);
                }
                if (r == null)
                {
                    return await Refuse(response, c, "edit extra", id, "That extra was not found.");
                }
                r.Name = name;
                r.Kind = kind;
                r.Note = note;
                r.PerDay = perDay;
                r.MaxQty = maxQty;
                r.Show = show;
                r.Stock = kind == "item" ? stock : new Dictionary<string, int>();
                await Store.SaveRows("extras", all);
            }
            await Store.Log(c.U.Id, isNew ? "add extra" : "edit extra", name, true);
            response.SetElementContents("ex-list", await ListNow());
            response.ExecuteScript("AdminExtrasJs.saved();");
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
                return await Refuse(response, c, "delete extra", id, "You do not have the right to delete extras.");
            }
            string name;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                ExtraRec? x = f.Extras.FirstOrDefault(y => y.Id == id);
                if (x == null)
                {
                    return await Refuse(response, c, "delete extra", id, "That extra was not found.");
                }
                int live = f.Bookings.Count(b => Avail.Live(b) && b.Extras.Any(q => q.Id == id));
                if (live > 0)
                {
                    return await Refuse(response, c, "delete extra", x.Name, Ui.Count(live, "active booking uses", "active bookings use") + " " + x.Name + ". Hide it from the website instead.");
                }
                name = x.Name;
                f.Extras.Remove(x);
                await Store.SaveRows("extras", f.Extras);
            }
            await Store.Log(c.U.Id, "delete extra", name, true);
            response.SetElementContents("ex-list", await ListNow());
            response.ExecuteScript("AdminExtrasJs.deleted();");
            return response;
        }
    }
}
