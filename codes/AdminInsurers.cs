using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminInsurers : WebPage
    {
        private const string AreaKey = "insurers";
        private const int Size = 12;
        private static readonly string[] InsMethods = { "Bank transfer", "Check", "Card", "Other" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Insurers | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminInsurersJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminInsurersJs.denied();");
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
            response.SetElementContents("is-brand", Ui.Esc(site.Brand));
            response.SetElementContents("is-menu", await Auth.Menu("is", "AdminInsurers", c));
            response.SetElementContents("is-who", Auth.Who("is", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminInsurersJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"is-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"is-btn is-btn-ghost is-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminInsurersJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"is-btn is-btn-ghost is-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminInsurersJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"is-pill is-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"is-err\" id=\"is-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"is-field\" for=\"is-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"is-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static List<BookingRec> Rentals(FleetData f, string id)
        {
            return f.Bookings.Where(b => b.PayerKind == "insurer" && b.InsurerId == id && Bill.Charged(b)).ToList();
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
            response.SetElementContents("is-main", "<div class=\"is-ph\"><div><h1>Insurers</h1><p>Insurance companies that pay for replacement rentals. Choose one on a booking under Who pays: it pays the rental, up to its daily limit, and the customer pays the rest. Open an insurer for its statement and to record what it paid.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"is-btn\" onclick=\"AdminInsurersJs.add()\">Add an insurer</button>" : string.Empty) + "</div><div class=\"is-tools\"><input type=\"search\" id=\"is-q\" placeholder=\"Search insurers\" maxlength=\"40\" oninput=\"AdminInsurersJs.type()\" aria-label=\"Search insurers\"></div><div id=\"is-list\">" + ListHtml(f, string.Empty, 1) + "</div>");
            return response;
        }

        private static string ListHtml(FleetData f, string term, int page)
        {
            List<InsurerRec> rows = f.Insurers.Where(i => term == string.Empty || Ui.Has(i.Name, term) || Ui.Has(i.Contact, term)).OrderByDescending(i => i.Active).ThenBy(i => i.Name).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"is-panel is-empty\">" + (f.Insurers.Count == 0 ? "No insurers yet. Add the first one." : "No insurers match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"is-wrapt\"><table class=\"is-table\"><thead><tr><th>Insurer</th><th>Pays</th><th>Terms</th><th>Rentals</th><th class=\"is-num\">Owed now</th><th>Status</th></tr></thead><tbody>");
            foreach (InsurerRec i in rows.Skip((page - 1) * Size).Take(Size))
            {
                List<BookingRec> rs = Rentals(f, i.Id);
                decimal owed = Bill.Owed(f, i.Id);
                sb.Append("<tr class=\"is-row\" onclick=\"AdminInsurersJs.edit('" + Ui.Js(i.Id) + "')\"><td class=\"is-first\"><b>" + Ui.Esc(i.Name) + "</b><small>" + Ui.Esc(i.Contact + (i.Phone.Length > 0 ? " · " + i.Phone : string.Empty)) + "</small></td><td data-label=\"Pays\">Rental" + (i.DailyCap > 0 ? " up to " + Rental.Money(i.DailyCap) + " a day" : string.Empty) + (i.CoverExtras ? " and extras" : string.Empty) + "</td><td data-label=\"Terms\">" + i.TermsDays + " days</td><td data-label=\"Rentals\">" + rs.Count + "<small>" + rs.Count(b => Avail.Live(b)) + " active</small></td><td data-label=\"Owed now\" class=\"is-num\"><b>" + Rental.Money(owed) + "</b></td><td data-label=\"Status\"><span class=\"is-pill " + (i.Active ? "is-pill-go\">Active" : "is-pill-off\">Inactive") + "</span></td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "insurer", "insurers"));
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
            response.SetElementContents("is-list", await ListNow());
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
            InsurerRec? i = f.Insurers.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && i == null)
            {
                return await Refuse(response, c, "open insurer", id, "That insurer was not found.");
            }
            if (i == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add insurer", string.Empty, "You can view insurers but not add them.");
            }
            response.SetElementContents("is-mbody", FormHtml(c, f, i));
            response.ExecuteScript("AdminInsurersJs.opened('" + Ui.Js(i == null ? "New insurer" : i.Name) + "');");
            return response;
        }

        private static string Statement(Ctx c, FleetData f, InsurerRec i)
        {
            List<BookingRec> rs = Rentals(f, i.Id).Where(b => Access.BranchOk(c.U, b.BranchId)).OrderBy(b => b.Start).ToList();
            StringBuilder sb = new StringBuilder("<p class=\"is-sec\">Statement</p>");
            if (rs.Count == 0)
            {
                return sb + "<p class=\"is-hint\">No rentals paid by this insurer yet.</p>";
            }
            decimal tShare = 0, tPaid = 0;
            sb.Append("<div class=\"is-wrapt\"><table class=\"is-table is-stmt\"><thead><tr><th>Booking</th><th>Claim</th><th>Rental</th><th>Status</th><th class=\"is-num\">Insurer share</th><th class=\"is-num\">Paid</th><th class=\"is-num\">Owed</th></tr></thead><tbody>");
            foreach (BookingRec b in rs)
            {
                Split sp = Bill.Of(f, b, null);
                bool billable = b.Status != "Reserved";
                tShare += billable ? sp.Insurer : 0;
                tPaid += sp.PaidInsurer;
                DateTime due = (b.ReturnedAt ?? b.End).Date.AddDays(i.TermsDays);
                bool late = billable && sp.DueInsurer > 0.005m && b.Status == "Returned" && due < DateTime.Today;
                sb.Append("<tr><td class=\"is-first\"><b>" + Ui.Esc(b.Code) + "</b><small>" + Ui.Esc(Rental.Name(b)) + "</small></td><td data-label=\"Claim\">" + Ui.Esc(b.ClaimNo) + "</td><td data-label=\"Rental\">" + Ui.Esc(Rental.Day(b.Start)) + "<small>to " + Ui.Esc(Rental.Day(b.ReturnedAt ?? b.End)) + "</small></td><td data-label=\"Status\">" + Pill(b.Status) + (late ? "<small class=\"is-late\">Due " + Ui.Esc(Rental.Day(due)) + "</small>" : billable && b.Status == "Returned" && sp.DueInsurer > 0.005m ? "<small>Due " + Ui.Esc(Rental.Day(due)) + "</small>" : string.Empty) + "</td><td data-label=\"Insurer share\" class=\"is-num\">" + (billable ? Rental.Money(sp.Insurer) : "<small>When picked up</small>") + "</td><td data-label=\"Paid\" class=\"is-num\">" + Rental.Money(sp.PaidInsurer) + "</td><td data-label=\"Owed\" class=\"is-num\"><b>" + (billable ? Rental.Money(sp.DueInsurer) : string.Empty) + "</b></td></tr>");
            }
            sb.Append("</tbody><tfoot><tr><td class=\"is-first\"><b>Total</b></td><td></td><td></td><td></td><td class=\"is-num\" data-label=\"Insurer share\"><b>" + Rental.Money(tShare) + "</b></td><td class=\"is-num\" data-label=\"Paid\"><b>" + Rental.Money(tPaid) + "</b></td><td class=\"is-num\" data-label=\"Owed\"><b>" + Rental.Money(tShare - tPaid) + "</b></td></tr></tfoot></table></div>");
            sb.Append("<div class=\"is-acts-row\" style=\"justify-content:flex-start;margin-top:10px\"><button type=\"button\" class=\"is-btn is-btn-ghost is-btn-sm\" onclick=\"AdminInsurersJs.printIt()\">Print the statement</button></div>");
            if (Access.Can(c.U, "payments", 'A') && tShare - tPaid > 0.005m)
            {
                sb.Append("<div class=\"is-panel is-paybox\" id=\"is-paybox\"><p class=\"is-sec\" style=\"border:0;padding:0;margin:0 0 10px\">Record a payment from " + Ui.Esc(i.Name) + "</p><div class=\"is-grid3\">" + Field("pamount", "Amount (US dollars)", Text("pamount", Num(tShare - tPaid), 10, "text", " inputmode=\"decimal\"")) + Field("pmethod", "Method", "<select id=\"is-f-pmethod\">" + string.Concat(InsMethods.Select(m => Opt(m, m, "Bank transfer"))) + "</select>") + Field("pref", "Reference", Text("pref", string.Empty, 60)) + "</div><p class=\"is-hint\">The amount pays the oldest open rentals first.</p><div><button type=\"button\" class=\"is-btn\" onclick=\"AdminInsurersJs.pay('" + Ui.Js(i.Id) + "')\">Record payment</button></div></div>");
            }
            return sb.ToString();
        }

        private static string FormHtml(Ctx c, FleetData f, InsurerRec? x)
        {
            bool isNew = x == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            InsurerRec i = x ?? new InsurerRec();
            string ro = edit ? string.Empty : " readonly";
            StringBuilder sb = new StringBuilder("<form class=\"is-stack\" id=\"is-form\" onsubmit=\"return AdminInsurersJs.save()\" novalidate><input type=\"hidden\" id=\"is-f-id\" value=\"" + Ui.Esc(i.Id) + "\">");
            sb.Append("<div class=\"is-grid2\">" + Field("name", "Company name", Text("name", i.Name, 80, "text", ro)) + Field("contact", "Contact person", Text("contact", i.Contact, 60, "text", ro)) + Field("phone", "Phone", Text("phone", i.Phone, 30, "text", ro)) + Field("email", "Email for statements", Text("email", i.Email, 80, "email", ro)) + "</div>");
            sb.Append(Field("address", "Address", Text("address", i.Address, 160, "text", ro)));
            sb.Append("<div class=\"is-grid3\">" + Field("terms", "Pays within (days)", Text("terms", i.TermsDays.ToString(CultureInfo.InvariantCulture), 3, "number", ro + " min=\"0\" max=\"180\"")) + Field("cap", "Daily limit for the rental (0 = no limit)", Text("cap", Num(i.DailyCap), 8, "text", ro + " inputmode=\"decimal\"")) + "<label class=\"is-check\" style=\"align-self:end;min-height:42px\"><input type=\"checkbox\" id=\"is-f-extras\" data-k=\"extras\"" + (i.CoverExtras ? " checked" : string.Empty) + (edit ? string.Empty : " disabled") + "> Usually pays the extras too</label></div>");
            sb.Append("<label class=\"is-field\" for=\"is-f-note\"><span>Note</span><textarea id=\"is-f-note\" data-k=\"note\" rows=\"2\" maxlength=\"300\"" + ro + ">" + Ui.Esc(i.Note) + "</textarea></label>");
            sb.Append("<label class=\"is-check\"><input type=\"checkbox\" id=\"is-f-active\" data-k=\"active\"" + (i.Active ? " checked" : string.Empty) + (edit ? string.Empty : " disabled") + "> Active: can be chosen on new bookings</label>");
            sb.Append("<div class=\"is-acts\">" + (edit ? "<button type=\"submit\" class=\"is-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"is-btn is-btn-ghost\" onclick=\"AdminInsurersJs.closeModal()\">Close</button>" + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"is-btn is-btn-danger is-push\" onclick=\"AdminInsurersJs.del('" + Ui.Js(i.Id) + "','" + Ui.Js(i.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
            if (!isNew)
            {
                sb.Append("<div id=\"is-stmt\">" + Statement(c, f, i) + "</div>");
            }
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
                return await Refuse(response, c, isNew ? "add insurer" : "edit insurer", id, "You do not have the right to " + (isNew ? "add" : "change") + " insurers.");
            }
            string name = Ui.Clip(GetDataValue("name"), 80);
            string contact = Ui.Clip(GetDataValue("contact"), 60);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string address = Ui.Clip(GetDataValue("address"), 160);
            int terms = Int(GetDataValue("terms"), -1);
            decimal cap = Dec(GetDataValue("cap"));
            bool extras = GetDataValue("extras") == "1";
            bool active = GetDataValue("active") == "1";
            string note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 300);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Please enter the company name." : string.Empty,
                ["contact"] = string.Empty,
                ["phone"] = phone.Length > 0 && !Ui.IsPhone(phone) ? "Please enter a valid phone number, or leave it empty." : string.Empty,
                ["email"] = email.Length > 0 && !Ui.IsEmail(email) ? "Please enter a valid email address, or leave it empty." : string.Empty,
                ["terms"] = terms < 0 || terms > 180 ? "From 0 to 180 days." : string.Empty,
                ["cap"] = cap < 0 || cap > 5000 ? "Enter the daily limit, or 0 for no limit." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("is-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<InsurerRec> all = await Store.Rows<InsurerRec>("insurers");
                if (all.Any(x => x.Id != id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    response.SetElementContents("is-e-name", "Another insurer already has this name.");
                    return response;
                }
                InsurerRec? i = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    i = new InsurerRec { Id = Rental.NewId("ins-") };
                    all.Add(i);
                }
                if (i == null)
                {
                    return await Refuse(response, c, "edit insurer", id, "That insurer was not found.");
                }
                i.Name = name;
                i.Contact = contact;
                i.Phone = phone;
                i.Email = email;
                i.Address = address;
                i.TermsDays = terms;
                i.DailyCap = cap;
                i.CoverExtras = extras;
                i.Active = active;
                i.Note = note;
                await Store.SaveRows("insurers", all);
            }
            await Store.Log(c.U.Id, isNew ? "add insurer" : "edit insurer", name, true);
            response.SetElementContents("is-list", await ListNow());
            response.ExecuteScript("AdminInsurersJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            if (!Access.Can(c.U, "payments", 'A'))
            {
                return await Refuse(response, c, "insurer payment", id, "You do not have the right to record payments.");
            }
            decimal amount = Dec(GetDataValue("pamount"));
            string method = InsMethods.Contains(GetDataValue("pmethod")) ? GetDataValue("pmethod")! : "Bank transfer";
            string reference = Ui.Clip(GetDataValue("pref"), 60);
            string target;
            int n = 0;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                InsurerRec? i = f.Insurers.FirstOrDefault(x => x.Id == id);
                if (i == null)
                {
                    return await Refuse(response, c, "insurer payment", id, "That insurer was not found.");
                }
                List<(BookingRec B, decimal Due)> open = Rentals(f, i.Id).Where(b => b.Status != "Reserved" && Access.BranchOk(c.U, b.BranchId)).Select(b => (B: b, Due: Bill.Of(f, b, null).DueInsurer)).Where(x => x.Due > 0.005m).OrderBy(x => x.B.ReturnedAt ?? x.B.End).ToList();
                decimal owed = open.Sum(x => x.Due);
                if (amount <= 0 || amount > owed + 0.005m)
                {
                    response.SetElementContents("is-e-pamount", "Enter an amount up to " + Rental.Money(owed) + ".");
                    return response;
                }
                decimal left = amount;
                DateTime now = DateTime.Now;
                foreach ((BookingRec b, decimal due) in open)
                {
                    if (left <= 0.005m)
                    {
                        break;
                    }
                    decimal take = Math.Min(left, due);
                    Bill.Add(f, b, "Insurer", method, take, now, reference, "Statement payment", c.U.Id);
                    left -= take;
                    n++;
                }
                await Store.SaveRows("payments", f.Payments);
                await Store.SaveRows("bookings", f.Bookings);
                target = i.Name + " · " + Rental.Money(amount) + " · " + Ui.Count(n, "rental", "rentals");
                response.SetElementContents("is-stmt", Statement(c, f, i));
            }
            await Store.Log(c.U.Id, "insurer payment", target, true);
            response.SetElementContents("is-list", await ListNow());
            response.ExecuteScript("AdminInsurersJs.clean();AdminInsurersJs.toast('" + Ui.Js("Payment recorded on " + Ui.Count(n, "rental", "rentals") + ".") + "');");
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
                return await Refuse(response, c, "delete insurer", id, "You do not have the right to delete insurers.");
            }
            string name;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                InsurerRec? i = f.Insurers.FirstOrDefault(x => x.Id == id);
                if (i == null)
                {
                    return await Refuse(response, c, "delete insurer", id, "That insurer was not found.");
                }
                if (f.Bookings.Any(b => b.InsurerId == id) || f.Payments.Any(p => p.InsurerId == id))
                {
                    return await Refuse(response, c, "delete insurer", i.Name, i.Name + " has bookings or payments in its records, so it cannot be deleted. Mark it inactive instead.");
                }
                name = i.Name;
                f.Insurers.Remove(i);
                await Store.SaveRows("insurers", f.Insurers);
            }
            await Store.Log(c.U.Id, "delete insurer", name, true);
            response.SetElementContents("is-list", await ListNow());
            response.ExecuteScript("AdminInsurersJs.deleted();");
            return response;
        }
    }
}
