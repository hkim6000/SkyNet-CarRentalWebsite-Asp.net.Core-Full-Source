using System.Globalization;
using System.Security.Cryptography;
using CarRental.Models;

namespace CarRental.Core
{
    public sealed class Quote
    {
        public int Days { get; set; }
        public decimal Rate { get; set; }
        public decimal Extras { get; set; }
        public decimal Charges { get; set; }
        public decimal Subtotal => Rate + Extras + Charges;
        public decimal Tax { get; set; }
        public decimal Total => Subtotal + Tax;
    }

    public static class Rental
    {
        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public static readonly string[] Panels = { "Front", "Rear", "Left side", "Right side", "Roof", "Windscreen", "Wheels", "Interior" };
        public static readonly string[] Marks = { "OK", "Scratch", "Dent", "Crack", "Stain", "Missing" };
        public static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        public static string Money(decimal v)
        {
            return v.ToString("C2", Us);
        }

        public static string When(DateTime d)
        {
            return d.ToString("ddd, MMM d, h:mm tt", Us);
        }

        public static string Day(DateTime d)
        {
            return d.ToString("MMM d, yyyy", Us);
        }

        public static int Days(DateTime s, DateTime e, int grace)
        {
            double minutes = (e - s).TotalMinutes - Math.Max(0, grace);
            return Math.Max(1, (int)Math.Ceiling(minutes / 1440.0));
        }

        public static decimal Rate(ClassRec c, DateTime s, int days)
        {
            int weeks = days / 7;
            decimal total = weeks * c.Weekly;
            for (int i = weeks * 7; i < days; i++)
            {
                DayOfWeek w = s.Date.AddDays(i).DayOfWeek;
                total += w == DayOfWeek.Friday || w == DayOfWeek.Saturday || w == DayOfWeek.Sunday ? c.Weekend : c.Daily;
            }
            return total;
        }

        public static decimal ExtrasCost(List<ExtraRec> extras, List<BookedExtra> picked, int days)
        {
            decimal t = 0;
            foreach (BookedExtra p in picked)
            {
                ExtraRec? x = extras.FirstOrDefault(e => e.Id == p.Id);
                if (x != null)
                {
                    t += x.PerDay * p.Qty * days;
                }
            }
            return t;
        }

        public static Quote Price(SiteData site, ClassRec c, List<ExtraRec> extras, DateTime s, DateTime e, List<BookedExtra> picked, decimal charges)
        {
            Quote q = new Quote { Days = Days(s, e, site.GraceMinutes) };
            q.Rate = Rate(c, s, q.Days);
            q.Extras = ExtrasCost(extras, picked, q.Days);
            q.Charges = charges;
            q.Tax = Math.Round(q.Subtotal * site.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
            return q;
        }

        public static Quote Of(SiteData site, BookingRec b)
        {
            Quote q = new Quote { Days = Days(b.Start, b.End, site.GraceMinutes), Rate = b.RateTotal, Extras = b.ExtrasTotal, Charges = b.Charges.Sum(x => x.Amount) };
            q.Tax = Math.Round(q.Subtotal * site.TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
            return q;
        }

        public static decimal Balance(SiteData site, BookingRec b)
        {
            return Of(site, b).Total - b.Paid;
        }

        public static string NewCode(List<BookingRec> all)
        {
            for (int n = 0; n < 50; n++)
            {
                string code = "BR-" + new string(Enumerable.Range(0, 6).Select(_ => Letters[RandomNumberGenerator.GetInt32(Letters.Length)]).ToArray());
                if (!all.Any(b => b.Code == code))
                {
                    return code;
                }
            }
            return "BR-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }

        public static string NewId(string prefix)
        {
            return prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
        }

        public static int Age(DateTime birth, DateTime at)
        {
            int age = at.Year - birth.Year;
            if (birth.Date > at.Date.AddYears(-age))
            {
                age--;
            }
            return age;
        }

        public static bool ParseLocal(string? v, out DateTime d)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        }

        public static bool ParseDate(string? v, out DateTime d)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        }

        public static string Iso(DateTime d)
        {
            return d.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        }

        public static string Name(BookingRec b)
        {
            return (b.First + " " + b.Last).Trim();
        }

        public static string FuelText(int eighths)
        {
            eighths = Math.Clamp(eighths, 0, 8);
            return eighths switch { 0 => "Empty", 2 => "1/4", 4 => "1/2", 6 => "3/4", 8 => "Full", _ => eighths + "/8" };
        }

        public static List<ChargeRec> ReturnCharges(SiteData site, ClassRec c, BookingRec b, InspectionRec outI, int mileage, int fuel, DateTime returned, bool cleaning, decimal damage, string damageNote)
        {
            List<ChargeRec> list = new List<ChargeRec>();
            int days = Days(b.Start, b.End, site.GraceMinutes);
            if (fuel < outI.Fuel)
            {
                list.Add(new ChargeRec { Kind = "fuel", Label = "Fuel refill (" + (outI.Fuel - fuel) + "/8 tank)", Amount = (outI.Fuel - fuel) * site.FuelPerEighth });
            }
            if (site.DailyMiles > 0)
            {
                int allowed = site.DailyMiles * days;
                int driven = Math.Max(0, mileage - outI.Mileage);
                if (driven > allowed)
                {
                    list.Add(new ChargeRec { Kind = "miles", Label = (driven - allowed).ToString("N0", Us) + " miles over " + allowed.ToString("N0", Us), Amount = Math.Round((driven - allowed) * site.MileRate, 2) });
                }
            }
            if (returned > b.End.AddMinutes(Math.Max(0, site.GraceMinutes)))
            {
                int late = Days(b.Start, returned, site.GraceMinutes) - days;
                if (late > 0)
                {
                    list.Add(new ChargeRec { Kind = "late", Label = "Late return (" + late + (late == 1 ? " day" : " days") + ")", Amount = late * c.Daily });
                }
            }
            if (cleaning)
            {
                list.Add(new ChargeRec { Kind = "cleaning", Label = "Cleaning", Amount = site.CleaningFee });
            }
            if (damage > 0)
            {
                list.Add(new ChargeRec { Kind = "damage", Label = "Damage" + (damageNote.Length > 0 ? ": " + damageNote : string.Empty), Amount = damage });
            }
            return list;
        }
    }
}
