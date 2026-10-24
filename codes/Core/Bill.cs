using CarRental.Models;

namespace CarRental.Core
{
    public sealed class Split
    {
        public decimal Total { get; set; }
        public decimal Insurer { get; set; }
        public decimal Customer => Total - Insurer;
        public decimal PaidInsurer { get; set; }
        public decimal PaidCustomer { get; set; }
        public decimal DueInsurer => Insurer - PaidInsurer;
        public decimal DueCustomer => Customer - PaidCustomer;
        public decimal Due => Total - PaidInsurer - PaidCustomer;
        public string InsurerName { get; set; } = string.Empty;
    }

    public static class Bill
    {
        public static readonly string[] Methods = { "Card", "Cash", "Bank transfer", "Check", "Other" };

        public static bool Charged(BookingRec b)
        {
            return b.Status != "Cancelled" && b.Status != "No-show";
        }

        public static Split Of(FleetData f, BookingRec b, List<ChargeRec>? charges)
        {
            List<ChargeRec> list = charges ?? b.Charges;
            Split s = new Split();
            if (Charged(b))
            {
                Quote q = new Quote { Rate = b.RateTotal, Extras = b.ExtrasTotal, Charges = list.Sum(x => x.Amount) };
                q.Tax = Math.Round(q.Subtotal * f.Site.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
                s.Total = q.Total;
                InsurerRec? ins = b.PayerKind == "insurer" ? f.Insurers.FirstOrDefault(x => x.Id == b.InsurerId) : null;
                if (ins != null)
                {
                    int days = Rental.Days(b.Start, b.End, f.Site.GraceMinutes);
                    decimal rental = ins.DailyCap > 0 ? Math.Min(b.RateTotal, ins.DailyCap * days) : b.RateTotal;
                    decimal covered = rental + (b.CoverExtras ? b.ExtrasTotal : 0);
                    s.Insurer = Math.Min(s.Total, Math.Round(covered * (1 + f.Site.TaxRate / 100m), 2, MidpointRounding.AwayFromZero));
                    s.InsurerName = ins.Name;
                }
            }
            List<PaymentRec> pays = f.Payments.Where(p => p.BookingId == b.Id).ToList();
            s.PaidInsurer = pays.Where(p => p.Payer == "Insurer").Sum(p => p.Amount);
            s.PaidCustomer = pays.Where(p => p.Payer != "Insurer").Sum(p => p.Amount);
            return s;
        }

        public static PaymentRec Add(FleetData f, BookingRec b, string payer, string method, decimal amount, DateTime when, string reference, string note, string by)
        {
            PaymentRec p = new PaymentRec { Id = Rental.NewId("p-"), BookingId = b.Id, When = when, Payer = payer == "Insurer" ? "Insurer" : "Customer", InsurerId = payer == "Insurer" ? b.InsurerId : string.Empty, Method = Methods.Contains(method) ? method : "Other", Amount = amount, Reference = reference, Note = note, By = by };
            f.Payments.Add(p);
            Sync(f, b);
            return p;
        }

        public static void Sync(FleetData f, BookingRec b)
        {
            List<PaymentRec> pays = f.Payments.Where(p => p.BookingId == b.Id).OrderBy(p => p.When).ToList();
            b.Paid = pays.Sum(p => p.Amount);
            b.PaidBy = pays.LastOrDefault()?.Method ?? string.Empty;
        }

        public static decimal Owed(FleetData f, string insurerId)
        {
            return f.Bookings.Where(b => b.PayerKind == "insurer" && b.InsurerId == insurerId && Charged(b) && b.Status != "Reserved").Sum(b => Math.Max(0, Of(f, b, null).DueInsurer));
        }
    }
}
