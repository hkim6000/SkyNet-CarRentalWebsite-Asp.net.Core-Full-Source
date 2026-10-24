using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminClasses : WebPage
    {
        private const string AreaKey = "classes";
        private const int Size = 10;
        private static readonly string[] Gears = { "Automatic", "Manual" };
        private static readonly string[] Fuels = { "Gas", "Hybrid", "Electric", "Diesel" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Classes and rates | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClassesJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminClassesJs.denied();");
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
            response.SetElementContents("cs-brand", Ui.Esc(site.Brand));
            response.SetElementContents("cs-menu", await Auth.Menu("cs", "AdminClasses", c));
            response.SetElementContents("cs-who", Auth.Who("cs", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminClassesJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"cs-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"cs-btn cs-btn-ghost cs-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminClassesJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"cs-btn cs-btn-ghost cs-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminClassesJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"cs-pill cs-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"cs-err\" id=\"cs-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"cs-field\" for=\"cs-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"cs-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static string PickItem(MediaItem m)
        {
            string thumb = m.Thumb.Length > 0 ? m.Thumb : m.Path;
            return "<button type=\"button\" class=\"cs-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(thumb) + "\" onclick=\"AdminClassesJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\"><img src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\"><span>" + Ui.Esc(m.Name) + "</span></button>";
        }

        private static string PickGrid(List<MediaItem> media, string term, int page)
        {
            List<MediaItem> rows = media.Where(m => m.Kind == "photo" && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"cs-none\">Nothing matches. Upload a photo above, or add photos in the Media library.</p>";
            }
            int pages = (rows.Count + PickSize - 1) / PickSize;
            page = Math.Min(Math.Max(page, 1), pages);
            string pager = pages <= 1 ? string.Empty : "<div class=\"cs-pkpager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(rows.Count, "photo", "photos") + "</span><div><button type=\"button\" class=\"cs-btn cs-btn-ghost cs-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminClassesJs.pkList(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"cs-btn cs-btn-ghost cs-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminClassesJs.pkList(" + (page + 1) + ")\">Next</button></div></div>";
            return string.Concat(rows.Skip((page - 1) * PickSize).Take(PickSize).Select(PickItem)) + pager;
        }

        private const int PickSize = 12;

        private static int PickPage(string? v)
        {
            return int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("cs-pkgrid", PickGrid(media, Ui.Clip(GetDataValue("pterm"), 40), PickPage(GetDataValue("ppg"))));
            return response;
        }

        public async Task<ApiResponse> MediaUp()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E') && !Can(c, 'A'))
            {
                return await Refuse(response, c, "upload media", AreaKey, "You can view this area but not change it.");
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.ExecuteScript("AdminClassesJs.pkUploaded(false,'Choose a photo first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files.Take(1).ToList(), site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("cs-pkgrid", PickGrid(media, string.Empty, 1));
            response.ExecuteScript("AdminClassesJs.pkUploaded(" + (added > 0 ? "true" : "false") + ",'" + Ui.Js(((added > 0 ? "Photo added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
            return response;
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
            response.SetElementContents("cs-main", "<div class=\"cs-ph\"><div><h1>Classes and rates</h1><p>Customers book a class, not a single car. Each class has its daily, weekend and weekly rate. The order here is the order on the website.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"cs-btn\" onclick=\"AdminClassesJs.add()\">Add a class</button>" : string.Empty) + "</div><div class=\"cs-tools\"><input type=\"search\" id=\"cs-q\" placeholder=\"Search classes\" maxlength=\"40\" oninput=\"AdminClassesJs.type()\" aria-label=\"Search classes\"></div><div id=\"cs-list\">" + ListHtml(c, f, await Media.Load(), string.Empty, 1) + "</div>");
            return response;
        }

        private static string Arrow(bool up)
        {
            return "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"" + (up ? "M6 15l6-6 6 6" : "M6 9l6 6 6-6") + "\"/></svg>";
        }

        private static string ListHtml(Ctx c, FleetData f, List<MediaItem> media, string term, int page)
        {
            List<ClassRec> rows = f.Classes.Where(x => term == string.Empty || Ui.Has(x.Name, term) || Ui.Has(x.Example, term) || Ui.Has(x.Summary, term)).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"cs-panel cs-empty\">" + (f.Classes.Count == 0 ? "No classes yet. Add the first one." : "No classes match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"cs-wrapt\"><table class=\"cs-table\"><thead><tr><th>Class</th><th>Seats</th><th class=\"cs-num\">Daily</th><th class=\"cs-num\">Weekend day</th><th class=\"cs-num\">Week</th><th>Cars</th><th>Website</th><th>Order</th></tr></thead><tbody>");
            foreach (ClassRec r in rows.Skip((page - 1) * Size).Take(Size))
            {
                MediaItem? m = media.FirstOrDefault(x => x.Path == r.Media);
                string thumb = m == null ? string.Empty : m.Thumb.Length > 0 ? m.Thumb : m.Path;
                int at = f.Classes.IndexOf(r);
                string id = Ui.Js(r.Id);
                int cars = f.Cars.Count(x => x.ClassId == r.Id && !Avail.Gone.Contains(x.Status));
                sb.Append("<tr class=\"cs-row\" onclick=\"AdminClassesJs.edit('" + id + "')\"><td class=\"cs-first\"><div class=\"cs-cell-main\">" + (thumb.Length > 0 ? "<img class=\"cs-thumb\" src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\">" : "<span class=\"cs-thumb\"></span>") + "<div><b>" + Ui.Esc(r.Name) + "</b><small>" + Ui.Esc(r.Example) + "</small></div></div></td>");
                sb.Append("<td data-label=\"Seats\">" + r.Seats + " &middot; " + Ui.Esc(r.Transmission) + "</td><td data-label=\"Daily\" class=\"cs-num\">" + Rental.Money(r.Daily) + "</td><td data-label=\"Weekend day\" class=\"cs-num\">" + Rental.Money(r.Weekend) + "</td><td data-label=\"Week\" class=\"cs-num\">" + Rental.Money(r.Weekly) + "</td><td data-label=\"Cars\">" + cars + "</td><td data-label=\"Website\"><span class=\"cs-pill " + (r.Show ? "cs-pill-go\">Shown" : "cs-pill-off\">Hidden") + "</span></td>");
                sb.Append("<td data-label=\"Order\" onclick=\"event.stopPropagation()\">" + (!Can(c, 'E') ? string.Empty : term == string.Empty ? "<span class=\"cs-ctl\"><button type=\"button\" aria-label=\"Move up\"" + (at == 0 ? " disabled" : string.Empty) + " onclick=\"AdminClassesJs.move('" + id + "',-1)\">" + Arrow(true) + "</button><button type=\"button\" aria-label=\"Move down\"" + (at == f.Classes.Count - 1 ? " disabled" : string.Empty) + " onclick=\"AdminClassesJs.move('" + id + "',1)\">" + Arrow(false) + "</button></span>" : "<small>Clear the search to reorder</small>") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "class", "classes"));
            return sb.ToString();
        }

        private async Task<string> ListNow(Ctx c)
        {
            FleetData f = await Avail.Load();
            return ListHtml(c, f, await Media.Load(), Term(GetDataValue("term")), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("cs-list", await ListNow(c));
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
            List<ClassRec> all = await Store.Rows<ClassRec>("classes");
            ClassRec? r = all.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && r == null)
            {
                return await Refuse(response, c, "open class", id, "That class was not found.");
            }
            if (r == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add class", string.Empty, "You can view classes but not add them.");
            }
            response.SetElementContents("cs-mbody", FormHtml(c, r, await Media.Load()));
            response.ExecuteScript("AdminClassesJs.opened('" + Ui.Js(r == null ? "New class" : r.Name) + "');");
            return response;
        }

        private static string Sel(string key, string[] values, string current, bool edit)
        {
            return "<select id=\"cs-f-" + key + "\" data-k=\"" + key + "\"" + (edit ? string.Empty : " disabled") + ">" + string.Concat(values.Select(v => Opt(v, v, current))) + "</select>";
        }

        private static string FormHtml(Ctx c, ClassRec? x, List<MediaItem> media)
        {
            bool isNew = x == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            ClassRec r = x ?? new ClassRec();
            string ro = edit ? string.Empty : " readonly";
            MediaItem? m = media.FirstOrDefault(y => y.Path == r.Media);
            string thumb = m == null ? string.Empty : m.Thumb.Length > 0 ? m.Thumb : m.Path;
            StringBuilder sb = new StringBuilder("<form class=\"cs-stack\" id=\"cs-form\" onsubmit=\"return AdminClassesJs.save()\" novalidate><input type=\"hidden\" id=\"cs-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            sb.Append("<div class=\"cs-grid2\">" + Field("name", "Class name", Text("name", r.Name, 40, "text", ro)) + Field("example", "Example models (shown as \"or similar\")", Text("example", r.Example, 80, "text", ro)) + "</div>");
            sb.Append("<label class=\"cs-field\" for=\"cs-f-summary\"><span>Summary (one or two sentences)</span><textarea id=\"cs-f-summary\" data-k=\"summary\" rows=\"2\" maxlength=\"220\"" + ro + ">" + Ui.Esc(r.Summary) + "</textarea>" + Err("summary") + "</label>");
            sb.Append("<div class=\"cs-grid3\">" + Field("seats", "Seats", Text("seats", r.Seats.ToString(CultureInfo.InvariantCulture), 2, "number", ro + " min=\"1\" max=\"15\"")) + Field("doors", "Doors", Text("doors", r.Doors.ToString(CultureInfo.InvariantCulture), 1, "number", ro + " min=\"2\" max=\"5\"")) + Field("bags", "Large bags", Text("bags", r.Bags.ToString(CultureInfo.InvariantCulture), 2, "number", ro + " min=\"0\" max=\"10\"")) + "</div>");
            sb.Append("<div class=\"cs-grid2\">" + Field("gear", "Transmission", Sel("gear", Gears, r.Transmission, edit)) + Field("fuel", "Fuel", Sel("fuel", Fuels, r.Fuel, edit)) + "</div>");
            sb.Append("<p class=\"cs-sec\">Rates (US dollars)</p><div class=\"cs-grid2\">" + Field("daily", "Daily rate (Monday to Thursday)", Text("daily", Num(r.Daily), 8, "text", ro + " inputmode=\"decimal\"")) + Field("weekend", "Weekend day rate (Friday to Sunday)", Text("weekend", Num(r.Weekend), 8, "text", ro + " inputmode=\"decimal\"")) + Field("weekly", "Weekly rate (each full 7 days)", Text("weekly", Num(r.Weekly), 8, "text", ro + " inputmode=\"decimal\"")) + Field("deposit", "Deposit held at pick-up", Text("deposit", Num(r.Deposit), 8, "text", ro + " inputmode=\"decimal\"")) + "</div>");
            sb.Append("<label class=\"cs-field\" for=\"cs-f-features\"><span>Features (one per line, up to 8)</span><textarea id=\"cs-f-features\" data-k=\"features\" rows=\"4\" maxlength=\"600\"" + ro + ">" + Ui.Esc(string.Join("\n", r.Features)) + "</textarea>" + Err("features") + "</label>");
            sb.Append("<div class=\"cs-field\"><span>Picture</span><div class=\"cs-pic\" id=\"cs-f-media\" data-k=\"media\" data-src=\"" + Ui.Esc(r.Media) + "\" data-ph=\"Choose a picture\"><button type=\"button\" class=\"cs-picb\"" + (edit ? " onclick=\"AdminClassesJs.pic(this)\"" : " disabled") + " aria-label=\"Choose a picture\">" + (thumb.Length > 0 ? "<img src=\"" + Ui.Esc(thumb) + "\" alt=\"\">" : "<span>Choose a picture</span>") + "</button>" + (edit ? "<button type=\"button\" class=\"cs-picx\"" + (thumb.Length > 0 ? string.Empty : " hidden") + " onclick=\"AdminClassesJs.picClear(this)\">Remove</button>" : string.Empty) + "</div>" + Err("media") + "</div>");
            sb.Append("<label class=\"cs-check\"><input type=\"checkbox\" id=\"cs-f-show\" data-k=\"show\"" + (r.Show ? " checked" : string.Empty) + (edit ? string.Empty : " disabled") + "> Show on the website and take bookings</label>");
            sb.Append("<div class=\"cs-acts\">" + (edit ? "<button type=\"submit\" class=\"cs-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"cs-btn cs-btn-ghost\" onclick=\"AdminClassesJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"cs-btn cs-btn-danger cs-push\" onclick=\"AdminClassesJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "add class" : "edit class", id, "You do not have the right to " + (isNew ? "add" : "change") + " classes.");
            }
            string name = Ui.Clip(GetDataValue("name"), 40);
            string example = Ui.Clip(GetDataValue("example"), 80);
            string summary = Ui.Clip(GetDataValue("summary"), 220);
            int seats = Int(GetDataValue("seats"), -1);
            int doors = Int(GetDataValue("doors"), -1);
            int bags = Int(GetDataValue("bags"), -1);
            string gear = Gears.Contains(GetDataValue("gear")) ? GetDataValue("gear")! : "Automatic";
            string fuel = Fuels.Contains(GetDataValue("fuel")) ? GetDataValue("fuel")! : "Gas";
            decimal daily = Dec(GetDataValue("daily"));
            decimal weekend = Dec(GetDataValue("weekend"));
            decimal weekly = Dec(GetDataValue("weekly"));
            decimal deposit = Dec(GetDataValue("deposit"));
            List<string> features = (GetDataValue("features") ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
            string media = Ui.Clip(GetDataValue("media"), 200);
            bool show = GetDataValue("show") == "1";
            List<MediaItem> library = await Media.Load();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Please enter the class name." : string.Empty,
                ["example"] = example.Length < 3 ? "Please give an example model." : string.Empty,
                ["summary"] = summary.Length < 10 ? "Please write a short summary (at least 10 characters)." : string.Empty,
                ["seats"] = seats < 1 || seats > 15 ? "Seats from 1 to 15." : string.Empty,
                ["doors"] = doors < 2 || doors > 5 ? "Doors from 2 to 5." : string.Empty,
                ["bags"] = bags < 0 || bags > 10 ? "Bags from 0 to 10." : string.Empty,
                ["daily"] = daily <= 0 || daily > 5000 ? "Enter the daily rate, for example 49 or 49.50." : string.Empty,
                ["weekend"] = weekend <= 0 || weekend > 5000 ? "Enter the weekend day rate." : string.Empty,
                ["weekly"] = weekly <= 0 || weekly > 30000 ? "Enter the weekly rate." : daily > 0 && weekly > daily * 7 ? "The weekly rate should not be more than 7 daily rates." : string.Empty,
                ["deposit"] = deposit < 0 || deposit > 10000 ? "Enter the deposit, or 0." : string.Empty,
                ["features"] = features.Count > 8 ? "Up to 8 lines, please." : features.Any(v => v.Length > 60) ? "Keep each line under 60 characters." : string.Empty,
                ["media"] = media.Length > 0 && !library.Any(m => m.Path == media && m.Kind == "photo") ? "That picture is not in the media library. Choose it again." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("cs-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<ClassRec> all = await Store.Rows<ClassRec>("classes");
                if (all.Any(x => x.Id != id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    response.SetElementContents("cs-e-name", "Another class already has this name.");
                    return response;
                }
                ClassRec? r = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    string key = "cl-" + Ui.Slug(name);
                    while (all.Any(x => x.Id == key))
                    {
                        key += "-2";
                    }
                    r = new ClassRec { Id = key };
                    all.Add(r);
                }
                if (r == null)
                {
                    return await Refuse(response, c, "edit class", id, "That class was not found.");
                }
                r.Name = name;
                r.Example = example;
                r.Summary = summary;
                r.Seats = seats;
                r.Doors = doors;
                r.Bags = bags;
                r.Transmission = gear;
                r.Fuel = fuel;
                r.Daily = daily;
                r.Weekend = weekend;
                r.Weekly = weekly;
                r.Deposit = deposit;
                r.Features = features;
                r.Media = media;
                r.Show = show;
                await Store.SaveRows("classes", all);
            }
            await Store.Log(c.U.Id, isNew ? "add class" : "edit class", name, true);
            response.SetElementContents("cs-list", await ListNow(c));
            response.ExecuteScript("AdminClassesJs.saved();");
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
                return await Refuse(response, c, "delete class", id, "You do not have the right to delete classes.");
            }
            string name;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                ClassRec? r = f.Classes.FirstOrDefault(x => x.Id == id);
                if (r == null)
                {
                    return await Refuse(response, c, "delete class", id, "That class was not found.");
                }
                if (f.Cars.Any(x => x.ClassId == id) || f.Bookings.Any(x => x.ClassId == id))
                {
                    return await Refuse(response, c, "delete class", r.Name, r.Name + " has cars or bookings in its records, so it cannot be deleted. Hide it from the website instead.");
                }
                name = r.Name;
                f.Classes.Remove(r);
                await Store.SaveRows("classes", f.Classes);
            }
            await Store.Log(c.U.Id, "delete class", name, true);
            response.SetElementContents("cs-list", await ListNow(c));
            response.ExecuteScript("AdminClassesJs.deleted();");
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "reorder classes", id, "You do not have the right to change classes.");
            }
            int dir = GetDataValue("dir") == "-1" ? -1 : 1;
            string name = string.Empty;
            using (await Store.Hold())
            {
                List<ClassRec> all = await Store.Rows<ClassRec>("classes");
                int at = all.FindIndex(x => x.Id == id);
                int to = at + dir;
                if (at >= 0 && to >= 0 && to < all.Count)
                {
                    name = all[at].Name;
                    (all[at], all[to]) = (all[to], all[at]);
                    await Store.SaveRows("classes", all);
                }
            }
            if (name.Length > 0)
            {
                await Store.Log(c.U.Id, "reorder classes", name, true);
            }
            response.SetElementContents("cs-list", await ListNow(c));
            return response;
        }
    }
}
