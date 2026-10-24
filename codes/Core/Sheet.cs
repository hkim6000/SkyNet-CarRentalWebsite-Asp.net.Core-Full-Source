using System.Text;
using CarRental.Models;

namespace CarRental.Core
{
    public static class Sheet
    {
        public static string BranchName(FleetData f, string id)
        {
            return f.Branches.FirstOrDefault(b => b.Id == id)?.Name ?? id;
        }

        public static string ClassName(FleetData f, string id)
        {
            return f.Classes.FirstOrDefault(c => c.Id == id)?.Name ?? id;
        }

        public static string ExtrasText(FleetData f, BookingRec b)
        {
            List<string> parts = b.Extras.Select(x =>
            {
                ExtraRec? e = f.Extras.FirstOrDefault(y => y.Id == x.Id);
                string n = e?.Name ?? x.Id;
                return x.Qty > 1 ? n + " × " + x.Qty : n;
            }).ToList();
            if (!b.Extras.Any(x => f.Extras.Any(e => e.Id == x.Id && e.Kind == "cover")))
            {
                parts.Add("Basic coverage");
            }
            return string.Join(", ", parts);
        }

        public static string Summary(string p, FleetData f, BookingRec b)
        {
            CarRec? car = f.Cars.FirstOrDefault(x => x.Id == b.CarId);
            StringBuilder sb = new StringBuilder("<dl class=\"" + p + "-kv\">");
            void Row(string k, string v)
            {
                sb.Append("<dt>" + Ui.Esc(k) + "</dt><dd>" + v + "</dd>");
            }
            Row("Customer", "<b>" + Ui.Esc(Rental.Name(b)) + "</b>");
            Row("Contact", Ui.Esc(b.Email) + "<br>" + Ui.Esc(b.Phone));
            Row("Licence", Ui.Esc(b.License + " · " + b.LicenseState));
            Row("Age at pick-up", b.Birth == default ? string.Empty : Rental.Age(b.Birth, b.Start).ToString(Rental.Us));
            Row("Branch", Ui.Esc(BranchName(f, b.BranchId)));
            Row("Class", Ui.Esc(ClassName(f, b.ClassId)));
            Row("Car", Ui.Esc(car == null ? "Not assigned yet" : car.Plate + " · " + car.Year + " " + car.Make + " " + car.Model + " · " + car.Color));
            Row("Pick-up", Ui.Esc(Rental.When(b.Start)) + (b.PickedUpAt != null ? "<br><small>Picked up " + Ui.Esc(Rental.When(b.PickedUpAt.Value)) + "</small>" : string.Empty));
            Row("Return", Ui.Esc(Rental.When(b.End)) + (b.ReturnedAt != null ? "<br><small>Returned " + Ui.Esc(Rental.When(b.ReturnedAt.Value)) + "</small>" : string.Empty));
            Row("Extras", Ui.Esc(ExtrasText(f, b)));
            Row("Booked", Ui.Esc(Rental.When(b.Created) + " · " + (b.Source == "desk" ? "by staff" : "on the website")));
            InsurerRec? ins = b.PayerKind == "insurer" ? f.Insurers.FirstOrDefault(x => x.Id == b.InsurerId) : null;
            Row("Paid by", ins == null ? "The customer" : "<b>" + Ui.Esc(ins.Name) + "</b>" + (b.ClaimNo.Length > 0 ? "<br><small>Claim " + Ui.Esc(b.ClaimNo) + "</small>" : string.Empty) + "<br><small>Rental" + (ins.DailyCap > 0 ? " up to " + Rental.Money(ins.DailyCap) + " a day" : string.Empty) + (b.CoverExtras ? " and extras" : string.Empty) + "; the customer pays the rest</small>");
            if (b.AskEnd != null)
            {
                Row("Asked to extend", "<b>" + Ui.Esc(Rental.When(b.AskEnd.Value)) + "</b>" + (b.AskNote.Length > 0 ? "<br><small>" + Ui.Esc(b.AskNote) + "</small>" : string.Empty));
            }
            if (b.Note.Length > 0)
            {
                Row("Note", Ui.Esc(b.Note));
            }
            sb.Append("</dl>");
            return sb.ToString();
        }

        public static string Money(string p, FleetData f, BookingRec b, List<ChargeRec>? charges)
        {
            List<ChargeRec> list = charges ?? b.Charges;
            Quote q = new Quote { Days = Rental.Days(b.Start, b.End, f.Site.GraceMinutes), Rate = b.RateTotal, Extras = b.ExtrasTotal, Charges = list.Sum(x => x.Amount) };
            q.Tax = Math.Round(q.Subtotal * f.Site.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
            ClassRec? c = f.Classes.FirstOrDefault(x => x.Id == b.ClassId);
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-sum\">");
            sb.Append("<div><span>Rental, " + Ui.Count(q.Days, "day", "days") + "</span><span>" + Rental.Money(q.Rate) + "</span></div>");
            if (q.Extras > 0)
            {
                sb.Append("<div><span>Extras</span><span>" + Rental.Money(q.Extras) + "</span></div>");
            }
            foreach (ChargeRec x in list)
            {
                sb.Append("<div><span>" + Ui.Esc(x.Label) + "</span><span>" + Rental.Money(x.Amount) + "</span></div>");
            }
            Split sp = Bill.Of(f, b, list);
            sb.Append("<div><span>Tax " + f.Site.TaxRate.ToString("0.##", Rental.Us) + "%</span><span>" + Rental.Money(q.Tax) + "</span></div>");
            sb.Append("<div class=\"" + p + "-tot\"><span>Total</span><span>" + Rental.Money(q.Total) + "</span></div>");
            if (sp.Insurer > 0)
            {
                sb.Append("<div><span>" + Ui.Esc(sp.InsurerName) + " pays</span><span>" + Rental.Money(sp.Insurer) + "</span></div>");
                sb.Append("<div><span>Customer pays</span><span>" + Rental.Money(sp.Customer) + "</span></div>");
                sb.Append("<div><span>Paid by " + Ui.Esc(sp.InsurerName) + "</span><span>" + Rental.Money(sp.PaidInsurer) + "</span></div>");
            }
            sb.Append("<div><span>Paid by the customer</span><span>" + Rental.Money(sp.PaidCustomer) + "</span></div>");
            if (sp.Insurer > 0)
            {
                sb.Append("<div class=\"" + p + "-tot\"><span>Insurer balance</span><span>" + Rental.Money(sp.DueInsurer) + "</span></div>");
            }
            sb.Append("<div class=\"" + p + "-tot\"><span>" + (sp.Insurer > 0 ? "Customer balance" : "Balance") + "</span><span>" + Rental.Money(sp.DueCustomer) + "</span></div>");
            if (c != null && c.Deposit > 0 && (b.Status == "Reserved" || Avail.Out(b)))
            {
                sb.Append("<div class=\"" + p + "-mini\"><span>Deposit held at pick-up, released at return</span><span>" + Rental.Money(c.Deposit) + "</span></div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public static decimal Total(SiteData site, BookingRec b, List<ChargeRec> charges)
        {
            Quote q = new Quote { Rate = b.RateTotal, Extras = b.ExtrasTotal, Charges = charges.Sum(x => x.Amount) };
            q.Tax = Math.Round(q.Subtotal * site.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
            return q.Total;
        }

        private static string Thumb(List<MediaItem> media, string src)
        {
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            string t = m != null && m.Thumb.Length > 0 ? m.Thumb : src;
            return "<a href=\"" + Ui.Esc(src) + "\" target=\"_blank\" rel=\"noopener\"><img src=\"" + Ui.Esc(t) + "\" alt=\"" + Ui.Esc(m?.Alt ?? "Inspection photo") + "\" loading=\"lazy\"></a>";
        }

        public static string Compare(string p, InspectionRec? o, InspectionRec? i, List<MediaItem> media)
        {
            if (o == null && i == null)
            {
                return "<p class=\"" + p + "-hint\">No inspections yet. They are recorded at check-out and check-in.</p>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-cmp\"><table class=\"" + p + "-ctab\"><thead><tr><th></th><th>Check-out</th><th>Check-in</th></tr></thead><tbody>");
            string W(InspectionRec? x)
            {
                return x == null ? "&mdash;" : Ui.Esc(Rental.When(x.When));
            }
            sb.Append("<tr><th>When</th><td>" + W(o) + "</td><td>" + W(i) + "</td></tr>");
            sb.Append("<tr><th>Mileage</th><td>" + (o == null ? "&mdash;" : o.Mileage.ToString("N0", Rental.Us)) + "</td><td>" + (i == null ? "&mdash;" : i.Mileage.ToString("N0", Rental.Us) + (o != null ? " <small>(" + (i.Mileage - o.Mileage).ToString("N0", Rental.Us) + " driven)</small>" : string.Empty)) + "</td></tr>");
            sb.Append("<tr><th>Fuel</th><td>" + (o == null ? "&mdash;" : Ui.Esc(Rental.FuelText(o.Fuel))) + "</td><td" + (o != null && i != null && i.Fuel < o.Fuel ? " class=\"" + p + "-diff\"" : string.Empty) + ">" + (i == null ? "&mdash;" : Ui.Esc(Rental.FuelText(i.Fuel))) + "</td></tr>");
            foreach (string panel in Rental.Panels)
            {
                string a = o != null && o.Panels.TryGetValue(panel, out string? va) ? va : "OK";
                string b = i != null && i.Panels.TryGetValue(panel, out string? vb) ? vb : "OK";
                bool diff = i != null && b != a && b != "OK";
                sb.Append("<tr><th>" + Ui.Esc(panel) + "</th><td>" + (o == null ? "&mdash;" : Ui.Esc(a)) + "</td><td" + (diff ? " class=\"" + p + "-diff\"" : string.Empty) + ">" + (i == null ? "&mdash;" : Ui.Esc(b)) + "</td></tr>");
            }
            sb.Append("<tr><th>Notes</th><td>" + Ui.Esc(o?.Notes ?? string.Empty) + "</td><td>" + Ui.Esc(i?.Notes ?? string.Empty) + "</td></tr>");
            sb.Append("<tr><th>Photos</th><td><div class=\"" + p + "-shots\">" + (o == null ? string.Empty : string.Concat(o.Photos.Select(x => Thumb(media, x)))) + "</div></td><td><div class=\"" + p + "-shots\">" + (i == null ? string.Empty : string.Concat(i.Photos.Select(x => Thumb(media, x)))) + "</div></td></tr>");
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        public static string ConfirmText(FleetData f, BookingRec b)
        {
            BranchRec? br = f.Branches.FirstOrDefault(x => x.Id == b.BranchId);
            return "Hello " + b.First + "," + Environment.NewLine + Environment.NewLine
                + "Your booking with " + f.Site.Legal + " is confirmed." + Environment.NewLine + Environment.NewLine
                + "Booking code: " + b.Code + Environment.NewLine
                + "Car class: " + ClassName(f, b.ClassId) + Environment.NewLine
                + "Pick-up: " + Rental.When(b.Start) + " at " + (br?.Name ?? string.Empty) + ", " + (br == null ? string.Empty : br.Street + ", " + br.City) + Environment.NewLine
                + "Return: " + Rental.When(b.End) + " at the same branch" + Environment.NewLine
                + "Estimated total: " + Rental.Money(Rental.Of(f.Site, b).Total) + ", paid at pick-up" + Environment.NewLine + Environment.NewLine
                + "Bring the driver's licence and a payment card in the driver's name. You can view or cancel the booking on Manage booking with your code and this email address. Free cancellation until " + f.Site.CancelHours + " hours before pick-up." + Environment.NewLine + Environment.NewLine
                + f.Site.Legal + " · " + f.Site.Phone;
        }

        public static async Task Tell(string to, string subject, string text)
        {
            if (Mailer.Ready && Ui.IsEmail(to))
            {
                await Mailer.Send(to, subject, text);
            }
        }
    }
}
