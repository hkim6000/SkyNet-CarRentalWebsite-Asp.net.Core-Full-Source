using CarRental.Models;

namespace CarRental.Core
{
    public sealed class FleetData
    {
        public SiteData Site { get; set; } = new SiteData();
        public List<BranchRec> Branches { get; set; } = new List<BranchRec>();
        public List<ClassRec> Classes { get; set; } = new List<ClassRec>();
        public List<CarRec> Cars { get; set; } = new List<CarRec>();
        public List<BookingRec> Bookings { get; set; } = new List<BookingRec>();
        public List<TransferRec> Transfers { get; set; } = new List<TransferRec>();
        public List<ExtraRec> Extras { get; set; } = new List<ExtraRec>();
        public List<ServiceRec> Services { get; set; } = new List<ServiceRec>();
        public List<RepairRec> Repairs { get; set; } = new List<RepairRec>();
        public List<IncidentRec> Incidents { get; set; } = new List<IncidentRec>();
        public List<SaleRec> Sales { get; set; } = new List<SaleRec>();
        public List<InsurerRec> Insurers { get; set; } = new List<InsurerRec>();
        public List<PaymentRec> Payments { get; set; } = new List<PaymentRec>();
    }

    public static class Avail
    {
        public static readonly string[] Statuses = { "Available", "Reserved", "On rent", "Cleaning", "Maintenance", "Repair", "In transfer", "Hold", "Stolen", "Sold" };
        public static readonly string[] Blocking = { "Maintenance", "Repair", "Hold" };
        public static readonly string[] Gone = { "Stolen", "Sold" };
        public static readonly string[] BookingStatuses = { "Reserved", "Picked up", "Extended", "Returned", "Cancelled", "No-show" };

        public static async Task<FleetData> Load()
        {
            return new FleetData
            {
                Site = await Store.LoadSite(),
                Branches = await Store.Rows<BranchRec>("branches"),
                Classes = await Store.Rows<ClassRec>("classes"),
                Cars = await Store.Rows<CarRec>("cars"),
                Bookings = await Store.Rows<BookingRec>("bookings"),
                Transfers = await Store.Rows<TransferRec>("transfers"),
                Extras = await Store.Rows<ExtraRec>("extras"),
                Services = await Store.Rows<ServiceRec>("services"),
                Repairs = await Store.Rows<RepairRec>("repairs"),
                Incidents = await Store.Rows<IncidentRec>("incidents"),
                Sales = await Store.Rows<SaleRec>("sales"),
                Insurers = await Store.Rows<InsurerRec>("insurers"),
                Payments = await Store.Rows<PaymentRec>("payments")
            };
        }

        public static bool Out(BookingRec b)
        {
            return b.Status == "Picked up" || b.Status == "Extended";
        }

        public static bool Live(BookingRec b)
        {
            return b.Status == "Reserved" || Out(b);
        }

        public static bool Overdue(BookingRec b, DateTime now)
        {
            return Out(b) && b.End < now;
        }

        public static DateTime EndOf(BookingRec b, DateTime now)
        {
            return Out(b) && b.End < now ? now.AddHours(1) : b.End;
        }

        public static bool Overlap(DateTime a0, DateTime a1, DateTime b0, DateTime b1)
        {
            return a0 < b1 && b0 < a1;
        }

        public static bool AtBranch(CarRec c, string branch, DateTime s, DateTime e, List<TransferRec> transfers)
        {
            List<TransferRec> open = transfers.Where(t => t.CarId == c.Id && (t.Status == "Planned" || t.Status == "In transit")).OrderBy(t => t.Depart).ToList();
            if (open.Count == 0)
            {
                return c.BranchId == branch;
            }
            string loc = c.BranchId;
            DateTime from = DateTime.MinValue;
            foreach (TransferRec t in open)
            {
                if (loc == branch && s >= from && e <= t.Depart)
                {
                    return true;
                }
                loc = t.To;
                from = t.Arrive;
            }
            return loc == branch && s >= from;
        }

        public static bool Blocked(CarRec c, DateTime s, DateTime e)
        {
            if (Gone.Contains(c.Status))
            {
                return true;
            }
            if (Blocking.Contains(c.Status))
            {
                if (c.BlockUntil == null)
                {
                    return true;
                }
                DateTime now = DateTime.Now;
                return s < (c.BlockUntil.Value < now && c.Status != "Hold" ? now.AddHours(1) : c.BlockUntil.Value);
            }
            return false;
        }

        public static bool Serviced(FleetData f, CarRec c, DateTime s, DateTime e, string ignore)
        {
            TimeSpan buf = TimeSpan.FromHours(Math.Max(0, f.Site.BufferHours));
            return f.Services.Any(v => v.CarId == c.Id && v.Id != ignore && v.Status == "Planned" && Overlap(v.Start - buf, v.End + buf, s, e));
        }

        public static bool CarFree(FleetData f, CarRec c, string branch, DateTime s, DateTime e, string ignore)
        {
            DateTime now = DateTime.Now;
            TimeSpan buf = TimeSpan.FromHours(Math.Max(0, f.Site.BufferHours));
            if (Blocked(c, s, e) || !AtBranch(c, branch, s, e, f.Transfers) || Serviced(f, c, s, e, string.Empty))
            {
                return false;
            }
            if (c.Status == "On rent" && !f.Bookings.Any(b => b.CarId == c.Id && Out(b)))
            {
                return false;
            }
            return !f.Bookings.Any(b => b.CarId == c.Id && b.Id != ignore && Live(b) && Overlap(b.Start - buf, EndOf(b, now) + buf, s, e));
        }

        public static List<CarRec> FreeCars(FleetData f, string branch, string cls, DateTime s, DateTime e, string ignore)
        {
            return f.Cars.Where(c => c.ClassId == cls && CarFree(f, c, branch, s, e, ignore)).ToList();
        }

        public static CarRec Best(FleetData f, List<CarRec> free, DateTime s)
        {
            return free.OrderBy(c =>
            {
                DateTime last = f.Bookings.Where(b => b.CarId == c.Id && Live(b) && b.End <= s).Select(b => b.End).DefaultIfEmpty(DateTime.MinValue).Max();
                return last == DateTime.MinValue ? double.MaxValue : (s - last).TotalHours;
            }).ThenBy(c => c.Mileage).First();
        }

        public static (string CarId, List<BookingRec> Moved) Assign(FleetData f, string branch, string cls, DateTime s, DateTime e, string ignore)
        {
            List<CarRec> free = FreeCars(f, branch, cls, s, e, ignore);
            if (free.Count > 0)
            {
                return (Best(f, free, s).Id, new List<BookingRec>());
            }
            DateTime now = DateTime.Now;
            TimeSpan buf = TimeSpan.FromHours(Math.Max(0, f.Site.BufferHours));
            foreach (CarRec c in f.Cars.Where(x => x.ClassId == cls && !Blocked(x, s, e) && AtBranch(x, branch, s, e, f.Transfers) && !Serviced(f, x, s, e, string.Empty)))
            {
                List<BookingRec> clash = f.Bookings.Where(b => b.CarId == c.Id && b.Id != ignore && Live(b) && Overlap(b.Start - buf, EndOf(b, now) + buf, s, e)).ToList();
                if (clash.Count != 1 || clash[0].Status != "Reserved")
                {
                    continue;
                }
                BookingRec other = clash[0];
                string keep = other.CarId;
                other.CarId = string.Empty;
                List<CarRec> alt = FreeCars(f, other.BranchId, other.ClassId, other.Start, other.End, other.Id).Where(x => x.Id != c.Id).ToList();
                if (alt.Count > 0)
                {
                    other.CarId = Best(f, alt, other.Start).Id;
                    return (c.Id, new List<BookingRec> { other });
                }
                other.CarId = keep;
            }
            return (string.Empty, new List<BookingRec>());
        }

        public static int FreeCount(FleetData f, string branch, string cls, DateTime s, DateTime e)
        {
            return FreeCars(f, branch, cls, s, e, string.Empty).Count;
        }

        public static int ExtraLeft(FleetData f, string branch, string extraId, DateTime s, DateTime e, string ignore)
        {
            ExtraRec? x = f.Extras.FirstOrDefault(y => y.Id == extraId);
            if (x == null)
            {
                return 0;
            }
            if (x.Kind != "item")
            {
                return x.MaxQty;
            }
            int stock = x.Stock.TryGetValue(branch, out int n) ? n : 0;
            DateTime now = DateTime.Now;
            int used = f.Bookings.Where(b => b.BranchId == branch && b.Id != ignore && Live(b) && Overlap(b.Start, EndOf(b, now), s, e)).Sum(b => b.Extras.Where(q => q.Id == extraId).Sum(q => q.Qty));
            return Math.Max(0, stock - used);
        }

        public static string Location(FleetData f, CarRec c)
        {
            TransferRec? t = f.Transfers.FirstOrDefault(x => x.CarId == c.Id && x.Status == "In transit");
            string name(string id) => f.Branches.FirstOrDefault(b => b.Id == id)?.Name ?? id;
            return t != null ? name(t.From) + " to " + name(t.To) : name(c.BranchId);
        }

        public static bool Settle(FleetData f)
        {
            DateTime now = DateTime.Now;
            bool changed = false;
            foreach (CarRec c in f.Cars)
            {
                if (c.Status == "Cleaning" && c.StatusSince.AddHours(Math.Max(0, f.Site.BufferHours)) <= now)
                {
                    c.Status = "Available";
                    c.StatusSince = now;
                    changed = true;
                }
                if (c.Status == "Hold" && c.BlockUntil != null && c.BlockUntil.Value <= now)
                {
                    c.Status = "Available";
                    c.StatusNote = string.Empty;
                    c.BlockUntil = null;
                    c.StatusSince = now;
                    changed = true;
                }
                bool soon = f.Bookings.Any(b => b.CarId == c.Id && b.Status == "Reserved" && b.Start <= now.AddHours(24) && b.End > now);
                if (c.Status == "Available" && soon)
                {
                    c.Status = "Reserved";
                    c.StatusSince = now;
                    changed = true;
                }
                else if (c.Status == "Reserved" && !soon)
                {
                    c.Status = "Available";
                    c.StatusSince = now;
                    changed = true;
                }
            }
            foreach (ServiceRec v in f.Services.Where(x => x.Status == "Planned" && x.Start <= now))
            {
                CarRec? c = f.Cars.FirstOrDefault(x => x.Id == v.CarId);
                if (c != null && (c.Status == "Available" || c.Status == "Reserved" || c.Status == "Cleaning"))
                {
                    v.Status = "In progress";
                    c.Status = "Maintenance";
                    c.StatusNote = v.Kind;
                    c.BlockUntil = v.End;
                    c.StatusSince = now;
                    changed = true;
                }
            }
            foreach (TransferRec t in f.Transfers.Where(x => x.Status == "In transit" && x.Arrive <= now))
            {
                CarRec? c = f.Cars.FirstOrDefault(x => x.Id == t.CarId);
                t.Status = "Arrived";
                if (c != null)
                {
                    c.BranchId = t.To;
                    if (c.Status == "In transit")
                    {
                        c.Status = "Available";
                        c.StatusSince = now;
                    }
                }
                changed = true;
            }
            foreach (TransferRec t in f.Transfers.Where(x => x.Status == "Planned" && x.Depart <= now))
            {
                CarRec? c = f.Cars.FirstOrDefault(x => x.Id == t.CarId);
                if (c != null && (c.Status == "Available" || c.Status == "Reserved" || c.Status == "Cleaning"))
                {
                    t.Status = "In transit";
                    c.Status = "In transit";
                    c.StatusSince = now;
                    changed = true;
                }
            }
            return changed;
        }

        public static async Task<FleetData> LoadSettled()
        {
            await Seed.Keep();
            FleetData f = await Load();
            if (Settle(f))
            {
                using (await Store.Hold())
                {
                    FleetData g = await Load();
                    if (Settle(g))
                    {
                        await Store.SaveRows("cars", g.Cars);
                        await Store.SaveRows("transfers", g.Transfers);
                        await Store.SaveRows("services", g.Services);
                    }
                    f = g;
                }
            }
            return f;
        }
    }
}
