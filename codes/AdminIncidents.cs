using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminIncidents : WebPage
    {
        private const string AreaKey = "incidents";
        private const int Size = 12;
        private static readonly string[] Kinds = { "Stolen", "Accident", "Vandalism", "Breakdown", "Traffic ticket", "Lost key", "Other" };
        private static readonly string[] Damaging = { "Accident", "Vandalism", "Breakdown" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Incidents | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminIncidentsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminIncidentsJs.denied();");
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
            response.SetElementContents("ic-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ic-menu", await Auth.Menu("ic", "AdminIncidents", c));
            response.SetElementContents("ic-who", Auth.Who("ic", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminIncidentsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"ic-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"ic-btn ic-btn-ghost ic-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminIncidentsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"ic-btn ic-btn-ghost ic-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminIncidentsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"ic-pill ic-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"ic-err\" id=\"ic-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"ic-field\" for=\"ic-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"ic-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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
            return "<button type=\"button\" class=\"ic-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(thumb) + "\" onclick=\"AdminIncidentsJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\"><img src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\"><span>" + Ui.Esc(m.Name) + "</span></button>";
        }

        private static string PickGrid(List<MediaItem> media, string term, int page)
        {
            List<MediaItem> rows = media.Where(m => m.Kind == "photo" && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"ic-none\">Nothing matches. Upload a photo above, or add photos in the Media library.</p>";
            }
            int pages = (rows.Count + PickSize - 1) / PickSize;
            page = Math.Min(Math.Max(page, 1), pages);
            string pager = pages <= 1 ? string.Empty : "<div class=\"ic-pkpager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(rows.Count, "photo", "photos") + "</span><div><button type=\"button\" class=\"ic-btn ic-btn-ghost ic-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminIncidentsJs.pkList(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"ic-btn ic-btn-ghost ic-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminIncidentsJs.pkList(" + (page + 1) + ")\">Next</button></div></div>";
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
            response.SetElementContents("ic-pkgrid", PickGrid(media, Ui.Clip(GetDataValue("pterm"), 40), PickPage(GetDataValue("ppg"))));
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
                response.ExecuteScript("AdminIncidentsJs.pkUploaded(false,'Choose a photo first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files.Take(1).ToList(), site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("ic-pkgrid", PickGrid(media, string.Empty, 1));
            response.ExecuteScript("AdminIncidentsJs.pkUploaded(" + (added > 0 ? "true" : "false") + ",'" + Ui.Js(((added > 0 ? "Photo added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
            return response;
        }

        private static bool Mine(Ctx c, CarRec? car)
        {
            return car != null && Access.BranchOk(c.U, car.BranchId);
        }

        private static BookingRec? During(FleetData f, string carId, DateTime when)
        {
            DateTime now = DateTime.Now;
            return f.Bookings.Where(b => b.CarId == carId && (Avail.Out(b) || b.Status == "Returned") && (b.PickedUpAt ?? b.Start) <= when && when <= (b.ReturnedAt ?? Avail.EndOf(b, now))).OrderByDescending(b => b.Start).FirstOrDefault();
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
            StringBuilder sb = new StringBuilder("<div class=\"ic-ph\"><div><h1>Incidents</h1><p>Thefts, accidents, vandalism, breakdowns and tickets, with the police report. A stolen car leaves the fleet until it is recovered; a damaged car goes to Repairs.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"ic-btn\" onclick=\"AdminIncidentsJs.add()\">Report an incident</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"ic-tools\"><input type=\"search\" id=\"ic-q\" placeholder=\"Plate, report number or text\" maxlength=\"40\" oninput=\"AdminIncidentsJs.type()\" aria-label=\"Search\"><select id=\"ic-fs\" onchange=\"AdminIncidentsJs.list(true)\" aria-label=\"Status\">" + Opt("Open", "Open", "Open") + Opt("Closed", "Closed", "Open") + Opt("all", "All incidents", "Open") + "</select><select id=\"ic-fk\" onchange=\"AdminIncidentsJs.list(true)\" aria-label=\"Kind\">" + Opt(string.Empty, "Any kind", string.Empty) + string.Concat(Kinds.Select(k => Opt(k, k, string.Empty))) + "</select><select id=\"ic-fb\" onchange=\"AdminIncidentsJs.list(true)\" aria-label=\"Branch\">" + Opt(string.Empty, "All my branches", string.Empty) + string.Concat(f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select></div>");
            sb.Append("<div id=\"ic-list\">" + ListHtml(c, f, string.Empty, "Open", string.Empty, string.Empty, 1) + "</div>");
            response.SetElementContents("ic-main", sb.ToString());
            string carId = Ui.Clip(GetDataValue("car"), 60);
            if (carId.Length > 0 && Can(c, 'A') && Mine(c, f.Cars.FirstOrDefault(x => x.Id == carId)))
            {
                response.SetElementContents("ic-mbody", FormHtml(c, f, null, carId, await Media.Load()));
                response.ExecuteScript("AdminIncidentsJs.opened('Report an incident');AdminIncidentsJs.photosSet([]);AdminIncidentsJs.kindNow();");
            }
            return response;
        }

        private static string ListHtml(Ctx c, FleetData f, string term, string st, string kind, string branch, int page)
        {
            List<IncidentRec> rows = f.Incidents.Where(x =>
            {
                CarRec? car = f.Cars.FirstOrDefault(k => k.Id == x.CarId);
                return Mine(c, car) && (branch == string.Empty || car!.BranchId == branch) && (st == "all" || x.Status == st) && (kind == string.Empty || x.Kind == kind)
                    && (term == string.Empty || Ui.Has(car!.Plate, term) || Ui.Has(x.PoliceReport, term) || Ui.Has(x.Description, term) || Ui.Has(x.Place, term));
            }).OrderByDescending(x => x.When).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ic-panel ic-empty\">No incidents match.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"ic-wrapt\"><table class=\"ic-table\"><thead><tr><th>Car</th><th>Incident</th><th>When</th><th>Police report</th><th>Status</th></tr></thead><tbody>");
            foreach (IncidentRec x in rows.Skip((page - 1) * Size).Take(Size))
            {
                CarRec? car = f.Cars.FirstOrDefault(k => k.Id == x.CarId);
                BookingRec? b = f.Bookings.FirstOrDefault(k => k.Id == x.BookingId);
                sb.Append("<tr class=\"ic-row\" onclick=\"AdminIncidentsJs.edit('" + Ui.Js(x.Id) + "')\"><td class=\"ic-first\"><b>" + Ui.Esc(car?.Plate ?? x.CarId) + "</b><small>" + Ui.Esc(car == null ? string.Empty : car.Make + " " + car.Model + " · " + car.Status) + "</small></td><td data-label=\"Incident\"><span class=\"ic-kind ic-k-" + Ui.Slug(x.Kind) + "\">" + Ui.Esc(x.Kind) + "</span><small>" + Ui.Esc(Ui.Clip(x.Description, 80)) + (x.Description.Length > 80 ? "&hellip;" : string.Empty) + (b != null ? " · " + Ui.Esc(b.Code) : string.Empty) + "</small></td><td data-label=\"When\">" + Ui.Esc(Rental.When(x.When)) + "<small>" + Ui.Esc(x.Place) + "</small></td><td data-label=\"Police report\">" + (x.PoliceReport.Length > 0 ? Ui.Esc(x.PoliceReport) + "<small>" + Ui.Esc(x.PoliceAgency) + "</small>" : "<small>None</small>") + "</td><td data-label=\"Status\">" + Pill(x.Status) + (x.RecoveredAt != null ? "<small>Recovered " + Ui.Esc(Rental.Day(x.RecoveredAt.Value)) + "</small>" : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "incident", "incidents"));
            return sb.ToString();
        }

        private async Task<string> ListNow(Ctx c)
        {
            FleetData f = await Avail.LoadSettled();
            string st = Ui.Clip(GetDataValue("fs"), 20);
            return ListHtml(c, f, Term(GetDataValue("term")), st.Length == 0 ? "Open" : st, Ui.Clip(GetDataValue("fk"), 20), Ui.Clip(GetDataValue("fb"), 60), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("ic-list", await ListNow(c));
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
            IncidentRec? x = f.Incidents.FirstOrDefault(k => k.Id == id);
            if (id.Length > 0 && (x == null || !Mine(c, f.Cars.FirstOrDefault(k => k.Id == x.CarId))))
            {
                return await Refuse(response, c, "open incident", id, "That incident was not found at your branches.");
            }
            if (x == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "report incident", string.Empty, "You can view incidents but not report them.");
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("ic-mbody", FormHtml(c, f, x, string.Empty, media));
            string shots = "[" + string.Join(",", (x?.Photos ?? new List<string>()).Select(p =>
            {
                MediaItem? m = media.FirstOrDefault(k => k.Path == p);
                return "{src:'" + Ui.Js(p) + "',thumb:'" + Ui.Js(m != null && m.Thumb.Length > 0 ? m.Thumb : p) + "'}";
            })) + "]";
            response.ExecuteScript("AdminIncidentsJs.opened('" + Ui.Js(x == null ? "Report an incident" : x.Kind + " · " + (f.Cars.FirstOrDefault(k => k.Id == x.CarId)?.Plate ?? string.Empty)) + "');AdminIncidentsJs.photosSet(" + shots + ");AdminIncidentsJs.kindNow();");
            return response;
        }

        private static string FormHtml(Ctx c, FleetData f, IncidentRec? y, string carId, List<MediaItem> media)
        {
            bool isNew = y == null;
            DateTime n0 = DateTime.Now;
            IncidentRec x = y ?? new IncidentRec { CarId = carId, Kind = "Accident", When = new DateTime(n0.Year, n0.Month, n0.Day, n0.Hour, n0.Minute, 0) };
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E') && x.Status == "Open";
            string ro = edit ? string.Empty : " readonly";
            CarRec? car = f.Cars.FirstOrDefault(k => k.Id == x.CarId);
            StringBuilder sb = new StringBuilder("<form class=\"ic-stack\" id=\"ic-form\" onsubmit=\"return AdminIncidentsJs.save()\" novalidate><input type=\"hidden\" id=\"ic-f-id\" value=\"" + Ui.Esc(x.Id) + "\">");
            if (isNew)
            {
                List<CarRec> cars = f.Cars.Where(k => Mine(c, k) && k.Status != "Sold").OrderBy(k => k.Plate).ToList();
                sb.Append("<div class=\"ic-grid2\">" + Field("car", "Car", "<select id=\"ic-f-car\" data-k=\"car\">" + string.Concat(cars.Select(k => Opt(k.Id, k.Plate + " · " + k.Make + " " + k.Model + " · " + k.Status, x.CarId))) + "</select>") + Field("kind", "What happened", "<select id=\"ic-f-kind\" data-k=\"kind\" onchange=\"AdminIncidentsJs.kindNow()\">" + string.Concat(Kinds.Select(k => Opt(k, k, x.Kind))) + "</select>") + "</div>");
            }
            else
            {
                BookingRec? b = f.Bookings.FirstOrDefault(k => k.Id == x.BookingId);
                List<RepairRec> reps = f.Repairs.Where(r => r.IncidentId == x.Id).ToList();
                sb.Append("<div class=\"ic-bar\"><div>" + Pill(x.Status) + " <b>" + Ui.Esc(x.Kind) + "</b> <span class=\"ic-mini\">" + Ui.Esc(car == null ? string.Empty : car.Plate + " · " + car.Make + " " + car.Model + " · now " + car.Status.ToLowerInvariant()) + "</span></div><span class=\"ic-mini\">" + (b != null ? "During booking " + Ui.Esc(b.Code) + " (" + Ui.Esc(Rental.Name(b)) + ")" : "No booking at that time") + (reps.Count > 0 ? " · " + Ui.Count(reps.Count, "repair", "repairs") + ": " + Ui.Esc(string.Join(", ", reps.Select(r => r.Status))) : string.Empty) + "</span></div><input type=\"hidden\" id=\"ic-f-kind\" value=\"" + Ui.Esc(x.Kind) + "\">");
            }
            sb.Append("<div class=\"ic-grid2\">" + Field("when", "When", "<input type=\"datetime-local\" id=\"ic-f-when\" data-k=\"when\" value=\"" + Rental.Iso(x.When) + "\"" + (isNew ? string.Empty : " readonly") + ">") + Field("place", "Where", Text("place", x.Place, 120, "text", ro)) + "</div>");
            sb.Append("<label class=\"ic-field\" for=\"ic-f-desc\"><span>What happened, in short</span><textarea id=\"ic-f-desc\" data-k=\"desc\" rows=\"3\" maxlength=\"800\"" + ro + ">" + Ui.Esc(x.Description) + "</textarea>" + Err("desc") + "</label>");
            sb.Append("<div class=\"ic-grid2\">" + Field("report", "Police report number", Text("report", x.PoliceReport, 40, "text", ro), "Required for a stolen car.") + Field("agency", "Police department", Text("agency", x.PoliceAgency, 80, "text", ro)) + "</div>");
            sb.Append("<div class=\"ic-field\"><span>Photos (up to 8)</span><div class=\"ic-photos\" id=\"ic-photos\"></div>" + (edit ? "<div><button type=\"button\" class=\"ic-btn ic-btn-ghost ic-btn-sm\" onclick=\"AdminIncidentsJs.addPhoto()\">Add a photo</button></div>" : string.Empty) + Err("photos") + "</div>");
            if (isNew)
            {
                sb.Append("<label class=\"ic-check\" id=\"ic-needbox\"><input type=\"checkbox\" id=\"ic-f-need\" data-k=\"need\"> The car needs repair: open a repair and take it off the road</label><p class=\"ic-note\" id=\"ic-stolennote\" hidden>The car is marked stolen and leaves the fleet. A rental in progress ends at this time, future bookings move to other cars, and planned services and transfers are cancelled.</p>");
            }
            sb.Append("<label class=\"ic-field\" for=\"ic-f-notes\"><span>Notes</span><textarea id=\"ic-f-notes\" data-k=\"notes\" rows=\"2\" maxlength=\"400\"" + (Can(c, isNew ? 'A' : 'E') ? string.Empty : " readonly") + ">" + Ui.Esc(x.Notes) + "</textarea></label>");
            if (!isNew && x.Kind == "Stolen" && x.Status == "Open" && x.RecoveredAt == null && car?.Status == "Stolen" && Can(c, 'E'))
            {
                sb.Append("<div class=\"ic-panel ic-recover\"><p class=\"ic-sec\" style=\"border:0;padding:0;margin:0 0 10px\">Car recovered</p><div class=\"ic-grid2\">" + Field("rwhen", "Recovered at", "<input type=\"datetime-local\" id=\"ic-f-rwhen\" value=\"" + Rental.Iso(new DateTime(n0.Year, n0.Month, n0.Day, n0.Hour, n0.Minute, 0)) + "\">") + Field("rbranch", "Brought to", "<select id=\"ic-f-rbranch\">" + string.Concat(f.Branches.Where(b => b.Open && Access.BranchOk(c.U, b.Id)).Select(b => Opt(b.Id, b.Name, car.BranchId))) + "</select>") + "</div><p class=\"ic-hint\">The car goes to Repairs for a full check before it can be rented again.</p><div><button type=\"button\" class=\"ic-btn\" onclick=\"AdminIncidentsJs.recover()\">Mark recovered</button></div></div>");
            }
            sb.Append("<div class=\"ic-acts\">" + (Can(c, isNew ? 'A' : 'E') && (isNew || x.Status == "Open") ? "<button type=\"submit\" class=\"ic-btn\">" + (isNew ? "Report incident" : "Save") + "</button>" : string.Empty) + "<button type=\"button\" class=\"ic-btn ic-btn-ghost\" onclick=\"AdminIncidentsJs.closeModal()\">Close</button>" + (!isNew && Can(c, 'E') ? (x.Status == "Open" ? "<button type=\"button\" class=\"ic-btn ic-btn-ghost ic-push\" onclick=\"AdminIncidentsJs.closeIt('" + (x.Kind == "Stolen" && x.RecoveredAt == null ? "1" : "0") + "')\">Close incident</button>" : "<button type=\"button\" class=\"ic-btn ic-btn-ghost ic-push\" onclick=\"AdminIncidentsJs.reopen()\">Reopen</button>") : string.Empty) + "</div></form>");
            return sb.ToString();
        }

        private List<string> Photos(List<MediaItem> library, out string err)
        {
            List<string> photos = (GetDataValue("photos") ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Distinct().ToList();
            err = photos.Count > 8 ? "Up to 8 photos." : photos.Any(p => !library.Any(m => m.Path == p && m.Kind == "photo")) ? "A photo is no longer in the media library. Remove it and add it again." : string.Empty;
            return photos;
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
                return await Refuse(response, c, isNew ? "report incident" : "edit incident", id, "You do not have the right to " + (isNew ? "report" : "change") + " incidents.");
            }
            string place = Ui.Clip(GetDataValue("place"), 120);
            string desc = Ui.Clip((GetDataValue("desc") ?? string.Empty).Replace("\r", string.Empty), 800);
            string report = Ui.Clip(GetDataValue("report"), 40);
            string agency = Ui.Clip(GetDataValue("agency"), 80);
            string notes = Ui.Clip((GetDataValue("notes") ?? string.Empty).Replace("\r", string.Empty), 400);
            bool need = GetDataValue("need") == "1";
            List<string> photos = Photos(await Media.Load(), out string ePhotos);
            string extra = string.Empty;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                Avail.Settle(f);
                IncidentRec? x = f.Incidents.FirstOrDefault(k => k.Id == id);
                if (!isNew && (x == null || x.Status != "Open"))
                {
                    return await Refuse(response, c, "edit incident", id, "Only an open incident can be changed.");
                }
                string kind = isNew ? (Kinds.Contains(GetDataValue("kind")) ? GetDataValue("kind")! : "Other") : x!.Kind;
                CarRec? car = f.Cars.FirstOrDefault(k => k.Id == (isNew ? Ui.Clip(GetDataValue("car"), 60) : x!.CarId));
                DateTime when = x?.When ?? default;
                string eWhen = string.Empty;
                if (isNew && (!Rental.ParseLocal(GetDataValue("when"), out when) || when > DateTime.Now.AddMinutes(10) || when < DateTime.Now.AddYears(-2)))
                {
                    eWhen = "Enter when it happened. It cannot be in the future.";
                }
                Dictionary<string, string> e = new Dictionary<string, string>
                {
                    ["car"] = car == null || !Mine(c, car) || car.Status == "Sold" ? "Choose one of your cars." : isNew && kind == "Stolen" && car.Status == "Stolen" ? "This car is already marked stolen." : string.Empty,
                    ["when"] = eWhen,
                    ["desc"] = desc.Length < 5 ? "Describe what happened." : string.Empty,
                    ["report"] = kind == "Stolen" && report.Length < 3 ? "A stolen car needs a police report number." : string.Empty,
                    ["photos"] = ePhotos
                };
                foreach (KeyValuePair<string, string> kv in e)
                {
                    response.SetElementContents("ic-e-" + kv.Key, kv.Value);
                }
                if (e.Values.Any(v => v.Length > 0))
                {
                    return response;
                }
                CarRec t = car!;
                if (isNew)
                {
                    BookingRec? during = During(f, t.Id, when);
                    x = new IncidentRec { Id = Rental.NewId("n-"), CarId = t.Id, Kind = kind, When = when, BookingId = during?.Id ?? string.Empty, Status = "Open", By = c.U.Id };
                    f.Incidents.Add(x);
                    DateTime now = DateTime.Now;
                    if (kind == "Stolen")
                    {
                        if (during != null && Avail.Out(during))
                        {
                            during.Status = "Returned";
                            during.ReturnedAt = when;
                            during.AskEnd = null;
                            during.Note = (during.Note + " · Car reported stolen, police report " + report).Trim(' ', '·');
                            extra += " Rental " + during.Code + " ended.";
                        }
                        foreach (ServiceRec v in f.Services.Where(v => v.CarId == t.Id && (v.Status == "Planned" || v.Status == "In progress")))
                        {
                            v.Status = "Cancelled";
                        }
                        foreach (TransferRec tr in f.Transfers.Where(tr => tr.CarId == t.Id && tr.Status == "Planned"))
                        {
                            tr.Status = "Cancelled";
                        }
                        t.Status = "Stolen";
                        t.StatusNote = "Police report " + report;
                        t.BlockUntil = null;
                        t.StatusSince = now;
                        (string err, List<BookingRec> m) = Ops.Reflow(f, t, now, null, string.Empty);
                        extra += Ops.Moved(m) + (err.Length > 0 ? " Needs a car: " + err : string.Empty);
                    }
                    else if (need && kind != "Traffic ticket")
                    {
                        f.Repairs.Add(new RepairRec { Id = Rental.NewId("r-"), CarId = t.Id, IncidentId = x.Id, BookingId = during?.Id ?? string.Empty, Opened = now, Problem = kind + ": " + Ui.Clip(desc, 120), Status = "Open", By = c.U.Id });
                        if (t.Status == "On rent" || t.Status == "In transit")
                        {
                            extra += " A repair is open; the car goes to Repair when it is back.";
                        }
                        else if (!Avail.Gone.Contains(t.Status))
                        {
                            if (t.Status != "Repair")
                            {
                                t.StatusSince = now;
                            }
                            t.Status = "Repair";
                            t.StatusNote = kind;
                            t.BlockUntil = null;
                            (string err, List<BookingRec> m) = Ops.Reflow(f, t, now, null, string.Empty);
                            extra += " Car sent to repair." + Ops.Moved(m) + (err.Length > 0 ? " Needs a car: " + err : string.Empty);
                        }
                    }
                }
                IncidentRec w = x!;
                w.Place = place;
                w.Description = desc;
                w.PoliceReport = report;
                w.PoliceAgency = agency;
                w.Notes = notes;
                w.Photos = photos;
                Avail.Settle(f);
                await Ops.PersistAll(f);
                target = t.Plate + " · " + kind + (report.Length > 0 ? " · report " + report : string.Empty);
            }
            await Store.Log(c.U.Id, isNew ? "report incident" : "edit incident", target + extra, true);
            response.SetElementContents("ic-list", await ListNow(c));
            response.ExecuteScript("AdminIncidentsJs.saved();" + (extra.Length > 0 ? "AdminIncidentsJs.toast('" + Ui.Js("Saved." + extra) + "');" : string.Empty));
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
                return await Refuse(response, c, "update incident", id, "You do not have the right to change incidents.");
            }
            string notes = Ui.Clip((GetDataValue("notes") ?? string.Empty).Replace("\r", string.Empty), 400);
            string msg;
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                IncidentRec? x = f.Incidents.FirstOrDefault(k => k.Id == id);
                CarRec? car = x == null ? null : f.Cars.FirstOrDefault(k => k.Id == x.CarId);
                if (x == null || car == null || !Mine(c, car))
                {
                    return await Refuse(response, c, "update incident", id, "That incident was not found at your branches.");
                }
                target = car.Plate + " · " + x.Kind;
                DateTime now = DateTime.Now;
                if (what == "recover" && x.Kind == "Stolen" && x.Status == "Open" && car.Status == "Stolen")
                {
                    string branch = Ui.Clip(GetDataValue("rbranch"), 60);
                    if (!Rental.ParseLocal(GetDataValue("rwhen"), out DateTime rw) || rw < x.When || rw > now.AddMinutes(10))
                    {
                        response.SetElementContents("ic-e-rwhen", "Enter when the car was recovered, after it was stolen and not in the future.");
                        return response;
                    }
                    if (!f.Branches.Any(b => b.Id == branch && b.Open) || !Access.BranchOk(c.U, branch))
                    {
                        response.SetElementContents("ic-e-rbranch", "Choose one of your open branches.");
                        return response;
                    }
                    x.RecoveredAt = rw;
                    f.Repairs.Add(new RepairRec { Id = Rental.NewId("r-"), CarId = car.Id, IncidentId = x.Id, Opened = now, Problem = "Full check after recovery", Status = "Open", By = c.U.Id });
                    car.BranchId = branch;
                    car.Status = "Repair";
                    car.StatusNote = "Check after recovery";
                    car.BlockUntil = null;
                    car.StatusSince = now;
                    msg = car.Plate + " is recovered and goes to Repairs for a check.";
                }
                else if (what == "close" && x.Status == "Open")
                {
                    x.Status = "Closed";
                    x.ClosedAt = now;
                    msg = "Incident closed." + (x.Kind == "Stolen" && car.Status == "Stolen" ? " The car stays stolen and out of the fleet." : string.Empty);
                }
                else if (what == "reopen" && x.Status == "Closed")
                {
                    x.Status = "Open";
                    x.ClosedAt = null;
                    msg = "Incident reopened.";
                }
                else
                {
                    return await Refuse(response, c, "update incident", target, "That incident has already moved on. The list is refreshed.");
                }
                x.Notes = notes;
                Avail.Settle(f);
                await Ops.PersistAll(f);
            }
            await Store.Log(c.U.Id, what == "recover" ? "car recovered" : what == "close" ? "close incident" : "reopen incident", target, true);
            response.SetElementContents("ic-list", await ListNow(c));
            response.ExecuteScript("AdminIncidentsJs.clean();AdminIncidentsJs.shut();AdminIncidentsJs.toast('" + Ui.Js(msg) + "');");
            return response;
        }
    }
}
