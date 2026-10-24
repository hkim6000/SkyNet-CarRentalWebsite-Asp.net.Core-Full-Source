using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminPayments : WebPage
    {
        private const string AreaKey = "payments";
        private const int Size = 15;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Payments | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminPaymentsJs.denied();");
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
            response.SetElementContents("py-brand", Ui.Esc(site.Brand));
            response.SetElementContents("py-menu", await Auth.Menu("py", "AdminPayments", c));
            response.SetElementContents("py-who", Auth.Who("py", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminPaymentsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"py-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminPaymentsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminPaymentsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"py-pill py-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"py-err\" id=\"py-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"py-field\" for=\"py-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"py-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static DateTime? Day(string? v)
        {
            return Rental.ParseDate(v, out DateTime d) ? d : null;
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
            DateTime d0 = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            StringBuilder sb = new StringBuilder("<div class=\"py-ph\"><div><h1>Payments</h1><p>Every payment and refund, from customers and insurers. Desk payments at check-out and check-in appear here on their own; record anything else, such as an insurer's bank transfer or a refund, here.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"py-btn\" onclick=\"AdminPaymentsJs.add()\">Record a payment</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"py-tools\"><input type=\"search\" id=\"py-q\" placeholder=\"Booking code, name or reference\" maxlength=\"40\" oninput=\"AdminPaymentsJs.type()\" aria-label=\"Search\"><label class=\"py-dlab\">From <input type=\"date\" id=\"py-f0\" value=\"" + d0.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\" onchange=\"AdminPaymentsJs.list(true)\"></label><label class=\"py-dlab\">to <input type=\"date\" id=\"py-f1\" value=\"" + DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\" onchange=\"AdminPaymentsJs.list(true)\"></label><select id=\"py-fp\" onchange=\"AdminPaymentsJs.list(true)\" aria-label=\"Payer\">" + Opt(string.Empty, "Customers and insurers", string.Empty) + Opt("Customer", "Customers", string.Empty) + Opt("Insurer", "Insurers", string.Empty) + "</select><select id=\"py-fm\" onchange=\"AdminPaymentsJs.list(true)\" aria-label=\"Method\">" + Opt(string.Empty, "Any method", string.Empty) + string.Concat(Bill.Methods.Select(m => Opt(m, m, string.Empty))) + "</select><select id=\"py-fb\" onchange=\"AdminPaymentsJs.list(true)\" aria-label=\"Branch\">" + Opt(string.Empty, "All my branches", string.Empty) + string.Concat(f.Branches.Where(b => Access.BranchOk(c.U, b.Id)).Select(b => Opt(b.Id, b.Name, string.Empty))) + "</select></div>");
            sb.Append("<div id=\"py-list\">" + ListHtml(c, f, string.Empty, d0, DateTime.Today, string.Empty, string.Empty, string.Empty, 1) + "</div>");
            response.SetElementContents("py-main", sb.ToString());
            string code = Ui.Clip(GetDataValue("code"), 12).ToUpperInvariant();
            if (code.Length > 0 && Can(c, 'A'))
            {
                response.SetElementContents("py-mbody", FormHtml(f, code));
                response.ExecuteScript("AdminPaymentsJs.opened('Record a payment');AdminPaymentsJs.find();");
            }
            return response;
        }

        private static string ListHtml(Ctx c, FleetData f, string term, DateTime? d0, DateTime? d1, string payer, string method, string branch, int page)
        {
            List<(PaymentRec P, BookingRec B)> rows = f.Payments.Select(p => (P: p, B: f.Bookings.FirstOrDefault(b => b.Id == p.BookingId))).Where(x => x.B != null).Select(x => (x.P, x.B!)).Where(x =>
                Access.BranchOk(c.U, x.Item2.BranchId) && (branch == string.Empty || x.Item2.BranchId == branch)
                && (d0 == null || x.P.When.Date >= d0.Value) && (d1 == null || x.P.When.Date <= d1.Value)
                && (payer == string.Empty || x.P.Payer == payer) && (method == string.Empty || x.P.Method == method)
                && (term == string.Empty || Ui.Has(x.Item2.Code, term) || Ui.Has(Rental.Name(x.Item2), term) || Ui.Has(x.P.Reference, term))).OrderByDescending(x => x.P.When).ToList();
            decimal inSum = rows.Where(x => x.P.Amount > 0).Sum(x => x.P.Amount);
            decimal outSum = rows.Where(x => x.P.Amount < 0).Sum(x => x.P.Amount);
            StringBuilder sb = new StringBuilder("<div class=\"py-tiles\"><div class=\"py-tile\"><b>" + Rental.Money(inSum) + "</b><span>received</span></div><div class=\"py-tile\"><b>" + Rental.Money(-outSum) + "</b><span>refunded</span></div><div class=\"py-tile\"><b>" + Rental.Money(inSum + outSum) + "</b><span>net</span></div><div class=\"py-tile\"><b>" + rows.Count + "</b><span>payments</span></div></div>");
            if (rows.Count == 0)
            {
                return sb + "<div class=\"py-panel py-empty\">No payments match.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            sb.Append("<div class=\"py-wrapt\"><table class=\"py-table\"><thead><tr><th>When</th><th>Booking</th><th>Payer</th><th>Method</th><th class=\"py-num\">Amount</th><th></th></tr></thead><tbody>");
            foreach ((PaymentRec p, BookingRec b) in rows.Skip((page - 1) * Size).Take(Size))
            {
                string who = p.Payer == "Insurer" ? f.Insurers.FirstOrDefault(i => i.Id == p.InsurerId)?.Name ?? "Insurer" : "Customer";
                sb.Append("<tr><td class=\"py-first\"><b>" + Ui.Esc(Rental.When(p.When)) + "</b><small>" + Ui.Esc(p.Note) + "</small></td><td data-label=\"Booking\"><a class=\"py-lnk\" href=\"AdminBookings?code=" + Ui.Esc(b.Code) + "&amp;st=all\">" + Ui.Esc(b.Code) + "</a><small>" + Ui.Esc(Rental.Name(b) + " · " + Sheet.BranchName(f, b.BranchId)) + "</small></td><td data-label=\"Payer\">" + Ui.Esc(who) + "</td><td data-label=\"Method\">" + Ui.Esc(p.Method) + (p.Reference.Length > 0 ? "<small>" + Ui.Esc(p.Reference) + "</small>" : string.Empty) + "</td><td data-label=\"Amount\" class=\"py-num\"><span class=\"" + (p.Amount < 0 ? "py-neg" : string.Empty) + "\">" + Rental.Money(p.Amount) + "</span></td><td data-label=\"\">" + (Can(c, 'D') ? "<button type=\"button\" class=\"py-btn py-btn-danger py-btn-sm\" onclick=\"AdminPaymentsJs.del('" + Ui.Js(p.Id) + "','" + Ui.Js(Rental.Money(p.Amount) + " on " + b.Code) + "')\">Delete</button>" : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "payment", "payments"));
            return sb.ToString();
        }

        private async Task<string> ListNow(Ctx c)
        {
            FleetData f = await Avail.LoadSettled();
            return ListHtml(c, f, Term(GetDataValue("term")), Day(GetDataValue("f0")), Day(GetDataValue("f1")), Ui.Clip(GetDataValue("fp"), 10), Ui.Clip(GetDataValue("fm"), 20), Ui.Clip(GetDataValue("fb"), 60), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("py-list", await ListNow(c));
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
                return await Refuse(response, c, "record payment", string.Empty, "You can view payments but not record them.");
            }
            FleetData f = await Avail.Load();
            response.SetElementContents("py-mbody", FormHtml(f, string.Empty));
            response.ExecuteScript("AdminPaymentsJs.opened('Record a payment');");
            return response;
        }

        private static string FormHtml(FleetData f, string code)
        {
            DateTime n = DateTime.Now;
            StringBuilder sb = new StringBuilder("<form class=\"py-stack\" id=\"py-form\" onsubmit=\"return AdminPaymentsJs.save()\" novalidate>");
            sb.Append("<div class=\"py-grid2\">" + Field("code", "Booking code", Text("code", code, 12, "text", " placeholder=\"BR-XXXXXX\" autocapitalize=\"characters\" onchange=\"AdminPaymentsJs.find()\"")) + "<div class=\"py-field\"><span>&nbsp;</span><button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"AdminPaymentsJs.find()\">Find the booking</button></div></div>");
            sb.Append("<div id=\"py-bk\"></div>");
            sb.Append("<div class=\"py-grid3\">" + Field("payer", "Paid by", "<select id=\"py-f-payer\" data-k=\"payer\" onchange=\"AdminPaymentsJs.payerChange()\">" + Opt("Customer", "Customer", "Customer") + Opt("Insurer", "Insurer", "Customer") + "</select>") + Field("method", "Method", "<select id=\"py-f-method\" data-k=\"method\">" + string.Concat(Bill.Methods.Select(m => Opt(m, m, "Card"))) + "</select>") + Field("amount", "Amount (US dollars)", Text("amount", string.Empty, 10, "text", " inputmode=\"decimal\"")) + "</div>");
            sb.Append("<label class=\"py-check\"><input type=\"checkbox\" id=\"py-f-refund\" data-k=\"refund\"> This is a refund (money paid back)</label>");
            sb.Append("<div class=\"py-grid3\">" + Field("when", "Date and time", "<input type=\"datetime-local\" id=\"py-f-when\" data-k=\"when\" value=\"" + Rental.Iso(new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, 0)) + "\">") + Field("ref", "Reference (receipt, transfer or check number)", Text("ref", string.Empty, 60)) + Field("note", "Note", Text("note", string.Empty, 120)) + "</div>");
            sb.Append("<div class=\"py-acts\"><button type=\"submit\" class=\"py-btn\">Record</button><button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"AdminPaymentsJs.closeModal()\">Cancel</button></div></form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Find()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            FleetData f = await Avail.LoadSettled();
            string code = Ui.Clip(GetDataValue("code"), 12).ToUpperInvariant();
            BookingRec? b = f.Bookings.FirstOrDefault(x => x.Code == code && Access.BranchOk(c.U, x.BranchId));
            if (b == null)
            {
                response.SetElementContents("py-e-code", code.Length == 0 ? string.Empty : "No booking with this code at your branches.");
                response.SetElementContents("py-bk", string.Empty);
                return response;
            }
            response.SetElementContents("py-e-code", string.Empty);
            Split sp = Bill.Of(f, b, null);
            response.SetElementContents("py-bk", "<div class=\"py-bar\"><div><b>" + Ui.Esc(b.Code + " · " + Rental.Name(b)) + "</b> <span class=\"py-mini\">" + Ui.Esc(b.Status + " · " + Rental.When(b.Start) + " to " + Rental.When(b.End)) + "</span></div></div>" + Sheet.Money("py", f, b, null));
            response.ExecuteScript("AdminPaymentsJs.found(" + (sp.Insurer > 0 ? "true" : "false") + ",'" + Num(Math.Max(0, sp.DueCustomer)) + "','" + Num(Math.Max(0, sp.DueInsurer)) + "');");
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
                return await Refuse(response, c, "record payment", string.Empty, "You do not have the right to record payments.");
            }
            string code = Ui.Clip(GetDataValue("code"), 12).ToUpperInvariant();
            string payer = GetDataValue("payer") == "Insurer" ? "Insurer" : "Customer";
            string method = Bill.Methods.Contains(GetDataValue("method")) ? GetDataValue("method")! : "Other";
            decimal amount = Dec(GetDataValue("amount"));
            bool refund = GetDataValue("refund") == "1";
            string reference = Ui.Clip(GetDataValue("ref"), 60);
            string note = Ui.Clip(GetDataValue("note"), 120);
            bool okW = Rental.ParseLocal(GetDataValue("when"), out DateTime when);
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                BookingRec? b = f.Bookings.FirstOrDefault(x => x.Code == code && Access.BranchOk(c.U, x.BranchId));
                Split? sp = b == null ? null : Bill.Of(f, b, null);
                Dictionary<string, string> e = new Dictionary<string, string>
                {
                    ["code"] = b == null ? "Enter the code of a booking at your branches." : !Bill.Charged(b) && !refund ? "This booking is " + b.Status.ToLowerInvariant() + "; only a refund can be recorded." : string.Empty,
                    ["payer"] = sp != null && payer == "Insurer" && b!.PayerKind != "insurer" ? "No insurer pays this booking." : string.Empty,
                    ["amount"] = amount <= 0 || amount > 100000 ? "Enter the amount." : refund && sp != null && amount > (payer == "Insurer" ? sp.PaidInsurer : sp.PaidCustomer) ? "A refund cannot be more than was paid." : string.Empty,
                    ["when"] = !okW || when > DateTime.Now.AddMinutes(10) ? "Enter when it was paid, not in the future." : string.Empty
                };
                foreach (KeyValuePair<string, string> kv in e)
                {
                    response.SetElementContents("py-e-" + kv.Key, kv.Value);
                }
                if (e.Values.Any(v => v.Length > 0))
                {
                    return response;
                }
                BookingRec t = b!;
                Bill.Add(f, t, payer, method, refund ? -amount : amount, when, reference, note.Length > 0 ? note : refund ? "Refund" : string.Empty, c.U.Id);
                await Store.SaveRows("payments", f.Payments);
                await Store.SaveRows("bookings", f.Bookings);
                target = t.Code + " · " + payer + " · " + method + " · " + Rental.Money(refund ? -amount : amount);
            }
            await Store.Log(c.U.Id, refund ? "record refund" : "record payment", target, true);
            response.SetElementContents("py-list", await ListNow(c));
            response.ExecuteScript("AdminPaymentsJs.saved();");
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
                return await Refuse(response, c, "delete payment", id, "You do not have the right to delete payments.");
            }
            string target;
            using (await Store.Hold())
            {
                FleetData f = await Avail.Load();
                PaymentRec? p = f.Payments.FirstOrDefault(x => x.Id == id);
                BookingRec? b = p == null ? null : f.Bookings.FirstOrDefault(x => x.Id == p.BookingId);
                if (p == null || b == null || !Access.BranchOk(c.U, b.BranchId))
                {
                    return await Refuse(response, c, "delete payment", id, "That payment was not found.");
                }
                f.Payments.Remove(p);
                Bill.Sync(f, b);
                await Store.SaveRows("payments", f.Payments);
                await Store.SaveRows("bookings", f.Bookings);
                target = b.Code + " · " + Rental.Money(p.Amount);
            }
            await Store.Log(c.U.Id, "delete payment", target, true);
            response.SetElementContents("py-list", await ListNow(c));
            response.ExecuteScript("AdminPaymentsJs.toast('Payment deleted');");
            return response;
        }
    }
}
