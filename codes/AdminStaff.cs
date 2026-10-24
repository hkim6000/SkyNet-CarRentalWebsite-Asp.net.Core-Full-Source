using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminStaff : WebPage
    {
        private const string AreaKey = "staff";
        private const int Size = 10;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Staff | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminStaffJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminStaffJs.denied();");
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
            response.SetElementContents("sf-brand", Ui.Esc(site.Brand));
            response.SetElementContents("sf-menu", await Auth.Menu("sf", "AdminStaff", c));
            response.SetElementContents("sf-who", Auth.Who("sf", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminStaffJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"sf-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"sf-btn sf-btn-ghost sf-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminStaffJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"sf-btn sf-btn-ghost sf-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminStaffJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"sf-pill sf-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"sf-err\" id=\"sf-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"sf-field\" for=\"sf-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"sf-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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
            SiteData site = await Store.LoadSite();
            await Shell(response, c, site);
            string tab = GetDataValue("tab") == "sets" ? "sets" : "staff";
            response.SetElementContents("sf-main", "<div class=\"sf-ph\"><div><h1>Staff and permissions</h1><p>Nobody has a fixed role. Each person gets View, Add, Edit and Delete rights per area and the branches they work at. Save common combinations as permission sets.</p></div><div id=\"sf-addbox\">" + AddButton(c, tab) + "</div></div><div class=\"sf-tabs\" role=\"tablist\"><button type=\"button\" class=\"sf-tab" + (tab == "staff" ? " sf-act" : string.Empty) + "\" id=\"sf-t-staff\" onclick=\"AdminStaffJs.tab('staff')\">Staff</button><button type=\"button\" class=\"sf-tab" + (tab == "sets" ? " sf-act" : string.Empty) + "\" id=\"sf-t-sets\" onclick=\"AdminStaffJs.tab('sets')\">Permission sets</button></div><div class=\"sf-tools\"><input type=\"search\" id=\"sf-q\" placeholder=\"Search\" maxlength=\"40\" oninput=\"AdminStaffJs.type()\" aria-label=\"Search\"></div><div id=\"sf-list\">" + await ListHtml(c, tab, string.Empty, 1) + "</div>");
            return response;
        }

        private static string AddButton(Ctx c, string tab)
        {
            return !Can(c, 'A') ? string.Empty : tab == "sets" ? "<button type=\"button\" class=\"sf-btn\" onclick=\"AdminStaffJs.addSet()\">Add a permission set</button>" : "<button type=\"button\" class=\"sf-btn\" onclick=\"AdminStaffJs.add()\">Add a staff member</button>";
        }

        private static string Summary(Dictionary<string, string> perms)
        {
            int n = Access.Areas.Count(a => perms.ContainsKey(a.Key));
            int w = Access.Areas.Count(a => perms.TryGetValue(a.Key, out string? v) && v.Length > 1);
            return Ui.Count(n, "area", "areas") + (w > 0 ? ", changes in " + w : ", view only");
        }

        private async Task<string> ListHtml(Ctx c, string tab, string term, int page)
        {
            List<AdminUser> staff = await Store.LoadStaff();
            List<PermSet> sets = await Store.Rows<PermSet>("permsets");
            List<BranchRec> branches = await Store.Rows<BranchRec>("branches");
            StringBuilder sb = new StringBuilder();
            if (tab == "sets")
            {
                List<PermSet> rows = sets.Where(s => term == string.Empty || Ui.Has(s.Name, term) || Ui.Has(s.Note, term)).ToList();
                if (rows.Count == 0)
                {
                    return "<div class=\"sf-panel sf-empty\">" + (sets.Count == 0 ? "No permission sets yet." : "No permission sets match.") + "</div>";
                }
                int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
                page = Math.Min(page, pages);
                sb.Append("<div class=\"sf-wrapt\"><table class=\"sf-table\"><thead><tr><th>Permission set</th><th>Rights</th><th>Used by</th></tr></thead><tbody>");
                foreach (PermSet s in rows.Skip((page - 1) * Size).Take(Size))
                {
                    int used = staff.Count(u => u.SetId == s.Id && !u.Manager);
                    sb.Append("<tr class=\"sf-row\" onclick=\"AdminStaffJs.editSet('" + Ui.Js(s.Id) + "')\"><td class=\"sf-first\"><b>" + Ui.Esc(s.Name) + "</b><small>" + Ui.Esc(s.Note) + "</small></td><td data-label=\"Rights\">" + Ui.Esc(Summary(s.Perms)) + "</td><td data-label=\"Used by\">" + Ui.Count(used, "person", "people") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "permission set", "permission sets"));
                return sb.ToString();
            }
            List<AdminUser> list = staff.Where(u => term == string.Empty || Ui.Has(u.Name, term) || Ui.Has(u.Login, term) || Ui.Has(u.Title, term) || Ui.Has(u.Email, term) || Ui.Has(u.Phone, term)).OrderByDescending(u => u.Active).ThenBy(u => u.Name).ToList();
            if (list.Count == 0)
            {
                return "<div class=\"sf-panel sf-empty\">No staff match.</div>";
            }
            int pg = Math.Max(1, (list.Count + Size - 1) / Size);
            page = Math.Min(page, pg);
            sb.Append("<div class=\"sf-wrapt\"><table class=\"sf-table\"><thead><tr><th>Name</th><th>Access</th><th>Branches</th><th>Status</th></tr></thead><tbody>");
            foreach (AdminUser u in list.Skip((page - 1) * Size).Take(Size))
            {
                string access = u.Manager ? "Manager, full access" : (sets.FirstOrDefault(s => s.Id == u.SetId)?.Name ?? "Custom") + " &middot; " + Ui.Esc(Summary(u.Perms));
                sb.Append("<tr class=\"sf-row\" onclick=\"AdminStaffJs.edit('" + Ui.Js(u.Id) + "')\"><td class=\"sf-first\"><b>" + Ui.Esc(u.Name) + (u.Id == c.U.Id ? " (you)" : string.Empty) + "</b><small>" + Ui.Esc(u.Login + (u.Title.Length > 0 ? " · " + u.Title : string.Empty)) + "</small>" + (u.Email.Length + u.Phone.Length > 0 ? "<small>" + Ui.Esc(string.Join(" · ", new[] { u.Email, u.Phone }.Where(x => x.Length > 0))) + "</small>" : string.Empty) + "</td><td data-label=\"Access\">" + (u.Manager ? Ui.Esc(access) : access) + "</td><td data-label=\"Branches\">" + Ui.Esc(Access.Describe(u, branches)) + "</td><td data-label=\"Status\">" + (!u.Active ? Pill("Inactive") : u.MustChange ? "<span class=\"sf-pill sf-pill-wait\">Must set password</span>" : Pill("Active")) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pg, list.Count, "person", "people"));
            return sb.ToString();
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string tab = GetDataValue("tab") == "sets" ? "sets" : "staff";
            response.SetElementContents("sf-list", await ListHtml(c, tab, Term(GetDataValue("term")), PageNo(GetDataValue("pg"))));
            response.SetElementContents("sf-addbox", AddButton(c, tab));
            return response;
        }

        private static string Grid(Dictionary<string, string> perms, bool edit, AdminUser editor)
        {
            StringBuilder sb = new StringBuilder("<div class=\"sf-wrapt sf-pgrid\"><table class=\"sf-table sf-ptable\"><thead><tr><th>Area</th>" + string.Concat(Access.Rights.Select(r => "<th>" + r.Name + "</th>")) + "</tr></thead><tbody>");
            string group = string.Empty;
            foreach (Area a in Access.Areas)
            {
                if (a.Group != group)
                {
                    group = a.Group;
                    sb.Append("<tr class=\"sf-pgroup\"><td colspan=\"5\">" + Ui.Esc(group) + "</td></tr>");
                }
                string have = perms.TryGetValue(a.Key, out string? v) ? v : string.Empty;
                string mine = Access.Level(editor, a.Key);
                sb.Append("<tr><td class=\"sf-first\"><b>" + Ui.Esc(a.Name) + "</b>" + (a.Branched ? "<small>Limited to the person's branches</small>" : string.Empty) + "</td>");
                foreach ((char letter, string name) in Access.Rights)
                {
                    if (!a.Letters.Contains(letter))
                    {
                        sb.Append("<td data-label=\"" + name + "\" class=\"sf-pna\">&mdash;</td>");
                        continue;
                    }
                    bool can = edit && mine.Contains(letter);
                    sb.Append("<td data-label=\"" + name + "\"><input type=\"checkbox\" class=\"sf-pm\" data-a=\"" + a.Key + "\" data-l=\"" + letter + "\" aria-label=\"" + Ui.Esc(a.Name + " " + name) + "\"" + (have.Contains(letter) ? " checked" : string.Empty) + (can ? string.Empty : " disabled") + " onchange=\"AdminStaffJs.perm(this)\"></td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        private static Dictionary<string, string> ReadPerms(string? raw, AdminUser editor, Dictionary<string, string> before)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (string part in (raw ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split(':');
                if (kv.Length == 2)
                {
                    d[kv[0]] = kv[1];
                }
            }
            d = Access.CleanAll(d);
            if (editor.Manager)
            {
                return d;
            }
            Dictionary<string, string> outp = new Dictionary<string, string>();
            foreach (Area a in Access.Areas)
            {
                string mine = Access.Level(editor, a.Key);
                string want = d.TryGetValue(a.Key, out string? w) ? w : string.Empty;
                string old = before.TryGetValue(a.Key, out string? o) ? o : string.Empty;
                string keep = new string(old.Where(ch => !mine.Contains(ch)).ToArray());
                string give = new string(want.Where(ch => mine.Contains(ch)).ToArray());
                string v = Access.Clean(a, keep + give);
                if (v.Length > 0)
                {
                    outp[a.Key] = v;
                }
            }
            return outp;
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
            List<AdminUser> staff = await Store.LoadStaff();
            AdminUser? u = staff.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && u == null)
            {
                return await Refuse(response, c, "open staff", id, "That person was not found.");
            }
            if (u == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add staff", string.Empty, "You can view staff but not add people.");
            }
            List<PermSet> sets = await Store.Rows<PermSet>("permsets");
            List<BranchRec> branches = await Store.Rows<BranchRec>("branches");
            response.SetElementContents("sf-mbody", FormHtml(c, u, sets, branches));
            response.ExecuteScript("AdminStaffJs.opened('" + Ui.Js(u == null ? "New staff member" : u.Name) + "');AdminStaffJs.scope();AdminStaffJs.boss();");
            return response;
        }

        private static string FormHtml(Ctx c, AdminUser? x, List<PermSet> sets, List<BranchRec> branches)
        {
            bool isNew = x == null;
            AdminUser u = x ?? new AdminUser { AllBranches = false, Active = true };
            bool edit = (isNew ? Can(c, 'A') : Can(c, 'E')) && (c.U.Manager || !u.Manager);
            bool self = u.Id == c.U.Id;
            string ro = edit ? string.Empty : " readonly";
            StringBuilder sb = new StringBuilder("<form class=\"sf-stack\" id=\"sf-form\" onsubmit=\"return AdminStaffJs.save()\" novalidate data-self=\"" + (self ? "1" : "0") + "\" data-boss=\"" + (u.Manager ? "1" : "0") + "\"><input type=\"hidden\" id=\"sf-f-id\" value=\"" + Ui.Esc(u.Id) + "\">");
            sb.Append("<div class=\"sf-grid3\">" + Field("name", "Full name", Text("name", u.Name, 60, "text", ro)) + Field("login", "User name", Text("login", u.Login, 30, "text", isNew && edit ? " autocomplete=\"off\"" : " readonly"), isNew ? "Small letters, numbers, dots or dashes." : "Cannot be changed.") + Field("title", "Job title", Text("title", u.Title, 40, "text", ro)) + "</div>");
            sb.Append("<div class=\"sf-grid2\">" + Field("email", "Email", Text("email", u.Email, 80, "email", ro), "Optional.") + Field("phone", "Phone", Text("phone", u.Phone, 25, "text", " inputmode=\"tel\"" + ro), "Optional.") + "</div>");
            sb.Append("<div class=\"sf-grid2\"><label class=\"sf-check\"><input type=\"checkbox\" id=\"sf-f-active\" data-k=\"active\"" + (u.Active ? " checked" : string.Empty) + (edit ? string.Empty : " disabled") + "> Can sign in</label>");
            sb.Append("<label class=\"sf-check\"><input type=\"checkbox\" id=\"sf-f-manager\" data-k=\"manager\" onchange=\"AdminStaffJs.boss()\"" + (u.Manager ? " checked" : string.Empty) + (edit && c.U.Manager ? string.Empty : " disabled") + "> Manager: full access to every area and branch</label></div>");
            sb.Append("<div id=\"sf-rights\"><p class=\"sf-sec\">Rights by area</p><div class=\"sf-grid2\" style=\"margin:12px 0\">" + Field("set", "Start from a permission set", "<select id=\"sf-f-set\" data-k=\"set\" onchange=\"AdminStaffJs.useSet()\"" + (edit ? string.Empty : " disabled") + ">" + Opt(string.Empty, "Custom", u.SetId) + string.Concat(sets.Select(s => "<option value=\"" + Ui.Esc(s.Id) + "\" data-p=\"" + Ui.Esc(string.Join(";", s.Perms.Select(kv => kv.Key + ":" + kv.Value))) + "\"" + (s.Id == u.SetId ? " selected" : string.Empty) + ">" + Ui.Esc(s.Name) + "</option>")) + "</select>", "Changing a box below makes it Custom. When a set is changed later, everyone using it gets the new rights.") + "</div>");
            sb.Append(Grid(u.Perms, edit, c.U) + Err("perms"));
            sb.Append("<p class=\"sf-sec\" style=\"margin-top:16px\">Branches</p><div class=\"sf-stack\" style=\"margin-top:10px\"><label class=\"sf-check\"><input type=\"radio\" name=\"sf-scope\" id=\"sf-f-all\" value=\"all\" onchange=\"AdminStaffJs.scope()\"" + (u.AllBranches ? " checked" : string.Empty) + (edit && (c.U.Manager || c.U.AllBranches) ? string.Empty : " disabled") + "> All branches, including new ones</label><label class=\"sf-check\"><input type=\"radio\" name=\"sf-scope\" id=\"sf-f-some\" value=\"some\" onchange=\"AdminStaffJs.scope()\"" + (u.AllBranches ? string.Empty : " checked") + (edit ? string.Empty : " disabled") + "> Only these branches</label><div class=\"sf-checks\" id=\"sf-blist\">");
            foreach (BranchRec b in branches)
            {
                bool mayGive = edit && Access.BranchOk(c.U, b.Id);
                sb.Append("<label class=\"sf-check\"><input type=\"checkbox\" class=\"sf-br\" data-v=\"" + Ui.Esc(b.Id) + "\"" + (u.Branches.Contains(b.Id) ? " checked" : string.Empty) + (mayGive ? string.Empty : " disabled") + "> " + Ui.Esc(b.Name) + (b.Open ? string.Empty : " (closed)") + "</label>");
            }
            sb.Append("</div>" + Err("branches") + "</div></div>");
            if (!edit && !isNew)
            {
                sb.Append("<p class=\"sf-hint\">" + (u.Manager && !c.U.Manager ? "Only a manager can change a manager's account." : "You can view this person but not change them.") + "</p>");
            }
            sb.Append("<div class=\"sf-acts\">" + (edit ? "<button type=\"submit\" class=\"sf-btn\">" + (isNew ? "Add and make a temporary password" : "Save") + "</button>" : string.Empty) + "<button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"AdminStaffJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>");
            if (!isNew && edit && !self)
            {
                sb.Append("<button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"AdminStaffJs.reset('" + Ui.Js(u.Id) + "','" + Ui.Js(u.Name) + "')\">Reset password</button>");
            }
            if (!isNew && !self && Can(c, 'D') && (c.U.Manager || !u.Manager))
            {
                sb.Append("<button type=\"button\" class=\"sf-btn sf-btn-danger sf-push\" onclick=\"AdminStaffJs.del('" + Ui.Js(u.Id) + "','" + Ui.Js(u.Name) + "')\">Delete</button>");
            }
            sb.Append("</div></form>");
            return sb.ToString();
        }

        private static string NewLoginError(string login, List<AdminUser> staff)
        {
            if (login.Length < 3 || login.Length > 30 || !login.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_'))
            {
                return "Use 3 to 30 small letters, numbers, dots, dashes or underscores.";
            }
            return staff.Any(u => u.Login == login) ? "Someone already uses this user name." : string.Empty;
        }

        private static string Shown(string name, string login, string pass)
        {
            return "<div class=\"sf-stack\"><p class=\"sf-ok\">Give " + Ui.Esc(name) + " this temporary password. It is shown only once. At the first sign in they choose their own.</p><dl class=\"sf-kv\"><dt>User name</dt><dd><b>" + Ui.Esc(login) + "</b></dd><dt>Temporary password</dt><dd><b class=\"sf-pass\">" + Ui.Esc(pass) + "</b></dd></dl><div class=\"sf-acts\"><button type=\"button\" class=\"sf-btn\" onclick=\"AdminStaffJs.shut()\">Done</button></div></div>";
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
                return await Refuse(response, c, isNew ? "add staff" : "edit staff", id, "You do not have the right to " + (isNew ? "add" : "change") + " staff.");
            }
            string name = Ui.Clip(GetDataValue("name"), 60);
            string login = Ui.Clip(GetDataValue("login"), 30).ToLowerInvariant();
            string title = Ui.Clip(GetDataValue("title"), 40);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 25);
            bool active = GetDataValue("active") == "1";
            bool manager = GetDataValue("manager") == "1";
            string setId = Ui.Clip(GetDataValue("set"), 60);
            bool all = GetDataValue("scope") == "all";
            List<string> picked = (GetDataValue("branches") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).Distinct().ToList();
            string pass = string.Empty;
            string target;
            bool selfOff = false;
            bool selfRole = false;
            using (await Store.Hold())
            {
                List<AdminUser> staff = await Store.LoadStaff();
                List<PermSet> sets = await Store.Rows<PermSet>("permsets");
                List<BranchRec> branches = await Store.Rows<BranchRec>("branches");
                AdminUser? u = staff.FirstOrDefault(x => x.Id == id);
                if (!isNew && u == null)
                {
                    return await Refuse(response, c, "edit staff", id, "That person was not found.");
                }
                if (u != null && u.Manager && !c.U.Manager)
                {
                    return await Refuse(response, c, "edit staff", u.Name, "Only a manager can change a manager's account.");
                }
                bool self = u != null && u.Id == c.U.Id;
                if (!c.U.Manager)
                {
                    manager = u?.Manager ?? false;
                }
                Dictionary<string, string> perms = ReadPerms(GetDataValue("perms"), c.U, u?.Perms ?? new Dictionary<string, string>());
                if (!c.U.Manager && !c.U.AllBranches)
                {
                    List<string> before = u?.Branches ?? new List<string>();
                    picked = before.Where(b => !c.U.Branches.Contains(b)).Concat(picked.Where(b => c.U.Branches.Contains(b))).Distinct().ToList();
                    all = u?.AllBranches ?? false;
                }
                picked = picked.Where(b => branches.Any(x => x.Id == b)).ToList();
                Dictionary<string, string> e = new Dictionary<string, string>
                {
                    ["name"] = name.Length < 2 ? "Please enter the full name." : string.Empty,
                    ["login"] = isNew ? NewLoginError(login, staff) : string.Empty,
                    ["title"] = string.Empty,
                    ["email"] = email.Length > 0 && !Ui.IsEmail(email) ? "Please enter a valid email address." : string.Empty,
                    ["phone"] = phone.Length > 0 && !Ui.IsPhone(phone) ? "Use 7 to 15 digits; spaces, brackets, dots, dashes or a plus sign are fine." : string.Empty,
                    ["perms"] = !manager && perms.Count == 0 ? "Tick at least one View box, or the person cannot open anything." : string.Empty,
                    ["branches"] = !manager && !all && picked.Count == 0 && Access.Areas.Any(a => a.Branched && perms.ContainsKey(a.Key)) ? "Choose at least one branch, or All branches." : string.Empty
                };
                foreach (KeyValuePair<string, string> kv in e)
                {
                    response.SetElementContents("sf-e-" + kv.Key, kv.Value);
                }
                if (e.Values.Any(v => v.Length > 0))
                {
                    return response;
                }
                if (u != null && u.Manager && (!manager || !active) && !staff.Any(x => x.Id != u.Id && x.Manager && x.Active))
                {
                    return await Refuse(response, c, "edit staff", u.Name, "At least one active manager must remain.");
                }
                PermSet? set = sets.FirstOrDefault(s => s.Id == setId);
                if (set != null && !Access.CleanAll(set.Perms).OrderBy(kv => kv.Key).SequenceEqual(perms.OrderBy(kv => kv.Key)))
                {
                    set = null;
                }
                if (isNew)
                {
                    u = new AdminUser { Id = Rental.NewId("u-"), Login = login, MustChange = true };
                    pass = Auth.NewPassword();
                    u.Hash = Auth.HashPassword(pass);
                    staff.Add(u);
                }
                AdminUser t = u!;
                selfOff = self && !active;
                selfRole = self && t.Manager != manager;
                t.Name = name;
                t.Title = title;
                t.Email = email;
                t.Phone = phone;
                t.Active = active;
                t.Manager = manager;
                t.Perms = manager ? new Dictionary<string, string>() : perms;
                t.SetId = manager ? string.Empty : set?.Id ?? string.Empty;
                t.AllBranches = manager || all;
                t.Branches = t.AllBranches ? new List<string>() : picked;
                target = t.Name;
                login = t.Login;
                await Store.SaveStaff(staff);
                if (!t.Active)
                {
                    await Auth.CloseAll(t.Id, string.Empty);
                }
            }
            await Store.Log(c.U.Id, isNew ? "add staff" : "edit staff", target, true);
            if (selfOff || selfRole)
            {
                response.ExecuteScript(selfOff ? "location.href='AdminLogin';" : "location.reload();");
                return response;
            }
            response.SetElementContents("sf-list", await ListHtml(c, "staff", Term(GetDataValue("term")), PageNo(GetDataValue("pg"))));
            if (isNew)
            {
                response.SetElementContents("sf-mbody", Shown(target, login, pass));
                response.ExecuteScript("AdminStaffJs.clean();");
            }
            else
            {
                response.ExecuteScript("AdminStaffJs.saved();");
            }
            return response;
        }

        public async Task<ApiResponse> Reset()
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
                return await Refuse(response, c, "reset password", id, "You do not have the right to change staff.");
            }
            if (Store.Demo())
            {
                return await Refuse(response, c, "reset password", id, "In demo mode the sample passwords stay demo1234.");
            }
            string pass = Auth.NewPassword();
            string name;
            string login;
            using (await Store.Hold())
            {
                List<AdminUser> staff = await Store.LoadStaff();
                AdminUser? u = staff.FirstOrDefault(x => x.Id == id);
                if (u == null || u.Id == c.U.Id)
                {
                    return await Refuse(response, c, "reset password", id, "Change your own password on My account.");
                }
                if (u.Manager && !c.U.Manager)
                {
                    return await Refuse(response, c, "reset password", u.Name, "Only a manager can reset a manager's password.");
                }
                u.Hash = Auth.HashPassword(pass);
                u.MustChange = true;
                name = u.Name;
                login = u.Login;
                await Store.SaveStaff(staff);
            }
            await Auth.CloseAll(id, string.Empty);
            await Store.Log(c.U.Id, "reset password", name, true);
            response.SetElementContents("sf-mbody", Shown(name, login, pass));
            response.SetElementContents("sf-list", await ListHtml(c, "staff", Term(GetDataValue("term")), PageNo(GetDataValue("pg"))));
            response.ExecuteScript("AdminStaffJs.clean();");
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
                return await Refuse(response, c, "delete staff", id, "You do not have the right to delete staff.");
            }
            string name;
            using (await Store.Hold())
            {
                List<AdminUser> staff = await Store.LoadStaff();
                AdminUser? u = staff.FirstOrDefault(x => x.Id == id);
                if (u == null || u.Id == c.U.Id)
                {
                    return await Refuse(response, c, "delete staff", id, "You cannot delete your own account.");
                }
                if (u.Manager && !c.U.Manager)
                {
                    return await Refuse(response, c, "delete staff", u.Name, "Only a manager can delete a manager.");
                }
                if (u.Manager && !staff.Any(x => x.Id != u.Id && x.Manager && x.Active))
                {
                    return await Refuse(response, c, "delete staff", u.Name, "At least one active manager must remain.");
                }
                name = u.Name;
                staff.Remove(u);
                await Store.SaveStaff(staff);
            }
            await Auth.CloseAll(id, string.Empty);
            await Store.Log(c.U.Id, "delete staff", name, true);
            response.SetElementContents("sf-list", await ListHtml(c, "staff", Term(GetDataValue("term")), PageNo(GetDataValue("pg"))));
            response.ExecuteScript("AdminStaffJs.deleted();");
            return response;
        }

        public async Task<ApiResponse> EditSet()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            List<PermSet> sets = await Store.Rows<PermSet>("permsets");
            PermSet? s = sets.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && s == null)
            {
                return await Refuse(response, c, "open permission set", id, "That permission set was not found.");
            }
            if (s == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add permission set", string.Empty, "You can view permission sets but not add them.");
            }
            bool isNew = s == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            PermSet r = s ?? new PermSet();
            string ro = edit ? string.Empty : " readonly";
            List<AdminUser> staff = await Store.LoadStaff();
            int used = staff.Count(u => u.SetId == r.Id && !u.Manager && r.Id.Length > 0);
            StringBuilder sb = new StringBuilder("<form class=\"sf-stack\" id=\"sf-form\" onsubmit=\"return AdminStaffJs.saveSet()\" novalidate><input type=\"hidden\" id=\"sf-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            sb.Append("<div class=\"sf-grid2\">" + Field("name", "Set name", Text("name", r.Name, 40, "text", ro)) + Field("note", "Who it is for", Text("note", r.Note, 120, "text", ro)) + "</div>");
            if (used > 0)
            {
                sb.Append("<p class=\"sf-note\">" + Ui.Count(used, "person uses", "people use") + " this set. Saving changes their rights too.</p>");
            }
            sb.Append(Grid(r.Perms, edit, c.U) + Err("perms"));
            sb.Append("<div class=\"sf-acts\">" + (edit ? "<button type=\"submit\" class=\"sf-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"AdminStaffJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"sf-btn sf-btn-danger sf-push\" onclick=\"AdminStaffJs.delSet('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
            response.SetElementContents("sf-mbody", sb.ToString());
            response.ExecuteScript("AdminStaffJs.opened('" + Ui.Js(isNew ? "New permission set" : r.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> SaveSet()
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
                return await Refuse(response, c, isNew ? "add permission set" : "edit permission set", id, "You do not have the right to change permission sets.");
            }
            string name = Ui.Clip(GetDataValue("name"), 40);
            string note = Ui.Clip(GetDataValue("note"), 120);
            int synced = 0;
            using (await Store.Hold())
            {
                List<PermSet> sets = await Store.Rows<PermSet>("permsets");
                PermSet? s = sets.FirstOrDefault(x => x.Id == id);
                if (!isNew && s == null)
                {
                    return await Refuse(response, c, "edit permission set", id, "That permission set was not found.");
                }
                Dictionary<string, string> perms = ReadPerms(GetDataValue("perms"), c.U, s?.Perms ?? new Dictionary<string, string>());
                string eName = name.Length < 2 ? "Please enter the set name." : sets.Any(x => x.Id != id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ? "Another set already has this name." : string.Empty;
                string ePerms = perms.Count == 0 ? "Tick at least one View box." : string.Empty;
                response.SetElementContents("sf-e-name", eName);
                response.SetElementContents("sf-e-perms", ePerms);
                if (eName + ePerms != string.Empty)
                {
                    return response;
                }
                if (s == null)
                {
                    s = new PermSet { Id = Rental.NewId("ps-") };
                    sets.Add(s);
                }
                s.Name = name;
                s.Note = note;
                s.Perms = perms;
                await Store.SaveRows("permsets", sets);
                List<AdminUser> staff = await Store.LoadStaff();
                foreach (AdminUser u in staff.Where(u => u.SetId == s.Id && !u.Manager))
                {
                    u.Perms = new Dictionary<string, string>(perms);
                    synced++;
                }
                await Store.SaveStaff(staff);
            }
            await Store.Log(c.U.Id, isNew ? "add permission set" : "edit permission set", name + (synced > 0 ? " · " + Ui.Count(synced, "person", "people") + " updated" : string.Empty), true);
            response.SetElementContents("sf-list", await ListHtml(c, "sets", Term(GetDataValue("term")), PageNo(GetDataValue("pg"))));
            response.ExecuteScript("AdminStaffJs.saved();" + (synced > 0 ? "AdminStaffJs.toast('Saved. " + Ui.Js(Ui.Count(synced, "person", "people")) + " updated.');" : string.Empty));
            return response;
        }

        public async Task<ApiResponse> DeleteSet()
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
                return await Refuse(response, c, "delete permission set", id, "You do not have the right to delete permission sets.");
            }
            string name;
            using (await Store.Hold())
            {
                List<PermSet> sets = await Store.Rows<PermSet>("permsets");
                PermSet? s = sets.FirstOrDefault(x => x.Id == id);
                if (s == null)
                {
                    return await Refuse(response, c, "delete permission set", id, "That permission set was not found.");
                }
                name = s.Name;
                sets.Remove(s);
                await Store.SaveRows("permsets", sets);
                List<AdminUser> staff = await Store.LoadStaff();
                foreach (AdminUser u in staff.Where(u => u.SetId == id))
                {
                    u.SetId = string.Empty;
                }
                await Store.SaveStaff(staff);
            }
            await Store.Log(c.U.Id, "delete permission set", name, true);
            response.SetElementContents("sf-list", await ListHtml(c, "sets", Term(GetDataValue("term")), PageNo(GetDataValue("pg"))));
            response.ExecuteScript("AdminStaffJs.deleted();");
            return response;
        }
    }
}
