using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminBranches : WebPage
    {
        private const string AreaKey = "branches";
        private const int Size = 10;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Branches | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBranchesJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminBranchesJs.denied();");
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
            response.SetElementContents("bn-brand", Ui.Esc(site.Brand));
            response.SetElementContents("bn-menu", await Auth.Menu("bn", "AdminBranches", c));
            response.SetElementContents("bn-who", Auth.Who("bn", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminBranchesJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"bn-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"bn-btn bn-btn-ghost bn-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminBranchesJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"bn-btn bn-btn-ghost bn-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminBranchesJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"bn-pill bn-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"bn-err\" id=\"bn-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"bn-field\" for=\"bn-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"bn-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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
            return "<button type=\"button\" class=\"bn-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(thumb) + "\" onclick=\"AdminBranchesJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\"><img src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\"><span>" + Ui.Esc(m.Name) + "</span></button>";
        }

        private static string PickGrid(List<MediaItem> media, string term, int page)
        {
            List<MediaItem> rows = media.Where(m => m.Kind == "photo" && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"bn-none\">Nothing matches. Upload a photo above, or add photos in the Media library.</p>";
            }
            int pages = (rows.Count + PickSize - 1) / PickSize;
            page = Math.Min(Math.Max(page, 1), pages);
            string pager = pages <= 1 ? string.Empty : "<div class=\"bn-pkpager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(rows.Count, "photo", "photos") + "</span><div><button type=\"button\" class=\"bn-btn bn-btn-ghost bn-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminBranchesJs.pkList(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"bn-btn bn-btn-ghost bn-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminBranchesJs.pkList(" + (page + 1) + ")\">Next</button></div></div>";
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
            response.SetElementContents("bn-pkgrid", PickGrid(media, Ui.Clip(GetDataValue("pterm"), 40), PickPage(GetDataValue("ppg"))));
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
                response.ExecuteScript("AdminBranchesJs.pkUploaded(false,'Choose a photo first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files.Take(1).ToList(), site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("bn-pkgrid", PickGrid(media, string.Empty, 1));
            response.ExecuteScript("AdminBranchesJs.pkUploaded(" + (added > 0 ? "true" : "false") + ",'" + Ui.Js(((added > 0 ? "Photo added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
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
            FleetData f = await Avail.LoadSettled();
            await Shell(response, c, f.Site);
            response.SetElementContents("bn-main", "<div class=\"bn-ph\"><div><h1>Branches</h1><p>Where customers pick up and return cars. Closed branches stay in the records but are hidden from the website and from new bookings.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"bn-btn\" onclick=\"AdminBranchesJs.add()\">Add a branch</button>" : string.Empty) + "</div><div class=\"bn-tools\"><input type=\"search\" id=\"bn-q\" placeholder=\"Search branches\" maxlength=\"40\" oninput=\"AdminBranchesJs.type()\" aria-label=\"Search branches\"></div><div id=\"bn-list\">" + ListHtml(f, await Media.Load(), string.Empty, 1) + "</div>");
            return response;
        }

        private static string ListHtml(FleetData f, List<MediaItem> media, string term, int page)
        {
            List<BranchRec> rows = f.Branches.Where(b => term == string.Empty || Ui.Has(b.Name, term) || Ui.Has(b.City, term) || Ui.Has(b.Street, term)).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"bn-panel bn-empty\">" + (f.Branches.Count == 0 ? "No branches yet. Add the first one." : "No branches match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            DateTime now = DateTime.Now;
            StringBuilder sb = new StringBuilder("<div class=\"bn-wrapt\"><table class=\"bn-table\"><thead><tr><th>Branch</th><th>Phone</th><th>Cars here</th><th>Active bookings</th><th>Website</th></tr></thead><tbody>");
            foreach (BranchRec b in rows.Skip((page - 1) * Size).Take(Size))
            {
                MediaItem? m = media.FirstOrDefault(x => x.Path == b.Media);
                string thumb = m == null ? string.Empty : m.Thumb.Length > 0 ? m.Thumb : m.Path;
                int cars = f.Cars.Count(x => x.BranchId == b.Id && !Avail.Gone.Contains(x.Status));
                int live = f.Bookings.Count(x => x.BranchId == b.Id && Avail.Live(x));
                sb.Append("<tr class=\"bn-row\" onclick=\"AdminBranchesJs.edit('" + Ui.Js(b.Id) + "')\"><td class=\"bn-first\"><div class=\"bn-cell-main\">" + (thumb.Length > 0 ? "<img class=\"bn-thumb\" src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\">" : "<span class=\"bn-thumb\"></span>") + "<div><b>" + Ui.Esc(b.Name) + "</b><small>" + Ui.Esc(b.Street + ", " + b.City) + "</small></div></div></td>");
                sb.Append("<td data-label=\"Phone\">" + Ui.Esc(b.Phone) + "</td><td data-label=\"Cars here\">" + cars + "</td><td data-label=\"Active bookings\">" + live + "</td><td data-label=\"Website\"><span class=\"bn-pill " + (b.Open ? "bn-pill-go\">Open" : "bn-pill-off\">Closed") + "</span></td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "branch", "branches"));
            return sb.ToString();
        }

        private async Task<string> ListNow()
        {
            FleetData f = await Avail.Load();
            return ListHtml(f, await Media.Load(), Term(GetDataValue("term")), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("bn-list", await ListNow());
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
            BranchRec? b = f.Branches.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && b == null)
            {
                return await Refuse(response, c, "open branch", id, "That branch was not found.");
            }
            if (b == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add branch", string.Empty, "You can view branches but not add them.");
            }
            response.SetElementContents("bn-mbody", FormHtml(c, f, b, await Media.Load()));
            response.ExecuteScript("AdminBranchesJs.opened('" + Ui.Js(b == null ? "New branch" : b.Name) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, FleetData f, BranchRec? b, List<MediaItem> media)
        {
            bool isNew = b == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            BranchRec r = b ?? new BranchRec();
            string ro = edit ? string.Empty : " readonly";
            MediaItem? m = media.FirstOrDefault(x => x.Path == r.Media);
            string thumb = m == null ? string.Empty : m.Thumb.Length > 0 ? m.Thumb : m.Path;
            StringBuilder sb = new StringBuilder("<form class=\"bn-stack\" id=\"bn-form\" onsubmit=\"return AdminBranchesJs.save()\" novalidate><input type=\"hidden\" id=\"bn-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            if (!isNew)
            {
                int cars = f.Cars.Count(x => x.BranchId == r.Id && !Avail.Gone.Contains(x.Status));
                int live = f.Bookings.Count(x => x.BranchId == r.Id && Avail.Live(x));
                sb.Append("<div class=\"bn-bar\"><span>" + "<span class=\"bn-pill " + (r.Open ? "bn-pill-go\">Open" : "bn-pill-off\">Closed") + "</span>" + " &middot; " + Ui.Count(cars, "car", "cars") + " here &middot; " + Ui.Count(live, "active booking", "active bookings") + "</span>" + (Can(c, 'E') ? "<button type=\"button\" class=\"bn-btn bn-btn-ghost bn-btn-sm\" onclick=\"AdminBranchesJs.toggle('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Name) + "'," + (r.Open ? "true" : "false") + ")\">" + (r.Open ? "Close this branch" : "Reopen this branch") + "</button>" : string.Empty) + "</div>");
            }
            sb.Append("<div class=\"bn-grid2\">" + Field("name", "Branch name", Text("name", r.Name, 60, "text", ro)) + Field("phone", "Phone", Text("phone", r.Phone, 30, "text", ro)) + "</div>");
            sb.Append(Field("street", "Street address", Text("street", r.Street, 80, "text", ro)));
            sb.Append("<div class=\"bn-grid3\">" + Field("city", "City", Text("city", r.City, 40, "text", ro)) + Field("region", "State", Text("region", r.Region, 30, "text", ro)) + Field("zip", "ZIP code", Text("zip", r.Zip, 12, "text", ro)) + "</div>");
            sb.Append(Field("email", "Email", Text("email", r.Email, 80, "email", ro)));
            sb.Append("<label class=\"bn-field\" for=\"bn-f-hours\"><span>Opening hours (one line per row, for example Mon to Fri: 7 am to 9 pm)</span><textarea id=\"bn-f-hours\" data-k=\"hours\" rows=\"3\" maxlength=\"300\"" + ro + ">" + Ui.Esc(r.Hours) + "</textarea>" + Err("hours") + "</label>");
            sb.Append("<label class=\"bn-field\" for=\"bn-f-note\"><span>Directions or note for customers</span><textarea id=\"bn-f-note\" data-k=\"note\" rows=\"2\" maxlength=\"300\"" + ro + ">" + Ui.Esc(r.Note) + "</textarea>" + Err("note") + "</label>");
            sb.Append("<div class=\"bn-field\"><span>Picture</span><div class=\"bn-pic\" id=\"bn-f-media\" data-k=\"media\" data-src=\"" + Ui.Esc(r.Media) + "\" data-ph=\"Choose a picture\"><button type=\"button\" class=\"bn-picb\"" + (edit ? " onclick=\"AdminBranchesJs.pic(this)\"" : " disabled") + " aria-label=\"Choose a picture\">" + (thumb.Length > 0 ? "<img src=\"" + Ui.Esc(thumb) + "\" alt=\"\">" : "<span>Choose a picture</span>") + "</button>" + (edit ? "<button type=\"button\" class=\"bn-picx\"" + (thumb.Length > 0 ? string.Empty : " hidden") + " onclick=\"AdminBranchesJs.picClear(this)\">Remove</button>" : string.Empty) + "</div>" + Err("media") + "</div>");
            sb.Append("<div class=\"bn-acts\">" + (edit ? "<button type=\"submit\" class=\"bn-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"bn-btn bn-btn-ghost\" onclick=\"AdminBranchesJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"bn-btn bn-btn-danger bn-push\" onclick=\"AdminBranchesJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "add branch" : "edit branch", id, "You do not have the right to " + (isNew ? "add" : "change") + " branches.");
            }
            string name = Ui.Clip(GetDataValue("name"), 60);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string street = Ui.Clip(GetDataValue("street"), 80);
            string city = Ui.Clip(GetDataValue("city"), 40);
            string region = Ui.Clip(GetDataValue("region"), 30);
            string zip = Ui.Clip(GetDataValue("zip"), 12);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string hours = Ui.Clip((GetDataValue("hours") ?? string.Empty).Replace("\r", string.Empty), 300);
            string note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 300);
            string media = Ui.Clip(GetDataValue("media"), 200);
            List<MediaItem> library = await Media.Load();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Please enter the branch name." : string.Empty,
                ["phone"] = !Ui.IsPhone(phone) ? "Please enter a phone number." : string.Empty,
                ["street"] = street.Length < 3 ? "Please enter the street address." : string.Empty,
                ["city"] = city.Length < 2 ? "Please enter the city." : string.Empty,
                ["region"] = region.Length < 2 ? "Please enter the state." : string.Empty,
                ["zip"] = zip.Length < 3 ? "Please enter the ZIP code." : string.Empty,
                ["email"] = email.Length > 0 && !Ui.IsEmail(email) ? "Please enter a valid email address, or leave it empty." : string.Empty,
                ["hours"] = hours.Length < 3 ? "Please enter the opening hours." : string.Empty,
                ["note"] = string.Empty,
                ["media"] = media.Length > 0 && !library.Any(m => m.Path == media && m.Kind == "photo") ? "That picture is not in the media library. Choose it again." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("bn-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<BranchRec> all = await Store.Rows<BranchRec>("branches");
                if (all.Any(x => x.Id != id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    response.SetElementContents("bn-e-name", "Another branch already has this name.");
                    return response;
                }
                BranchRec? b = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    string key = "br-" + Ui.Slug(name);
                    while (all.Any(x => x.Id == key))
                    {
                        key += "-2";
                    }
                    b = new BranchRec { Id = key, Open = true };
                    all.Add(b);
                }
                if (b == null)
                {
                    return await Refuse(response, c, "edit branch", id, "That branch was not found.");
                }
                b.Name = name;
                b.Phone = phone;
                b.Street = street;
                b.City = city;
                b.Region = region;
                b.Zip = zip;
                b.Email = email;
                b.Hours = hours;
                b.Note = note;
                b.Media = media;
                await Store.SaveRows("branches", all);
            }
            await Store.Log(c.U.Id, isNew ? "add branch" : "edit branch", name, true);
            response.SetElementContents("bn-list", await ListNow());
            response.ExecuteScript("AdminBranchesJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Toggle()
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
                return await Refuse(response, c, "close branch", id, "You do not have the right to change branches.");
            }
            string name;
            bool open;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                BranchRec? b = f.Branches.FirstOrDefault(x => x.Id == id);
                if (b == null)
                {
                    return await Refuse(response, c, "close branch", id, "That branch was not found.");
                }
                if (b.Open)
                {
                    int live = f.Bookings.Count(x => x.BranchId == b.Id && Avail.Live(x));
                    if (live > 0)
                    {
                        return await Refuse(response, c, "close branch", b.Name, b.Name + " has " + Ui.Count(live, "active booking", "active bookings") + ". Finish or cancel them before closing the branch.");
                    }
                    if (f.Branches.Count(x => x.Open) <= 1)
                    {
                        return await Refuse(response, c, "close branch", b.Name, "At least one branch must stay open.");
                    }
                }
                b.Open = !b.Open;
                name = b.Name;
                open = b.Open;
                await Store.SaveRows("branches", f.Branches);
            }
            await Store.Log(c.U.Id, open ? "reopen branch" : "close branch", name, true);
            response.SetElementContents("bn-list", await ListNow());
            response.ExecuteScript("AdminBranchesJs.saved();AdminBranchesJs.toast('" + Ui.Js(name + (open ? " is open again." : " is closed.")) + "');");
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
                return await Refuse(response, c, "delete branch", id, "You do not have the right to delete branches.");
            }
            string name;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                BranchRec? b = f.Branches.FirstOrDefault(x => x.Id == id);
                if (b == null)
                {
                    return await Refuse(response, c, "delete branch", id, "That branch was not found.");
                }
                if (f.Cars.Any(x => x.BranchId == id) || f.Bookings.Any(x => x.BranchId == id) || f.Transfers.Any(x => x.From == id || x.To == id))
                {
                    return await Refuse(response, c, "delete branch", b.Name, b.Name + " has cars, bookings or transfers in its records, so it cannot be deleted. Close it instead.");
                }
                name = b.Name;
                f.Branches.Remove(b);
                await Store.SaveRows("branches", f.Branches);
                List<ExtraRec> extras = await Store.Rows<ExtraRec>("extras");
                foreach (ExtraRec x in extras)
                {
                    x.Stock.Remove(id);
                }
                await Store.SaveRows("extras", extras);
                List<AdminUser> staff = await Store.LoadStaff();
                foreach (AdminUser u in staff)
                {
                    u.Branches.Remove(id);
                }
                await Store.SaveStaff(staff);
            }
            await Store.Log(c.U.Id, "delete branch", name, true);
            response.SetElementContents("bn-list", await ListNow());
            response.ExecuteScript("AdminBranchesJs.deleted();");
            return response;
        }
    }
}
