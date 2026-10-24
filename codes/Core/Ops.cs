using System.Globalization;
using CarRental.Models;

namespace CarRental.Core
{
    public sealed class Driver
    {
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string BirthText { get; set; } = string.Empty;
    }

    public sealed class ExtPlan
    {
        public string Error { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public List<(string BookingId, string CarId)> Moves { get; set; } = new List<(string, string)>();
        public int Days { get; set; }
        public decimal Rate { get; set; }
        public decimal Extras { get; set; }
        public decimal Diff { get; set; }
        public string Sign => CarId + "|" + string.Join(",", Moves.Select(m => m.BookingId + ">" + m.CarId)) + "|" + (Rate + Extras).ToString(CultureInfo.InvariantCulture);
    }

    public static class Ops
    {
        public static string TripError(FleetData f, string branch, string cls, DateTime s, DateTime e, bool staff)
        {
            BranchRec? b = f.Branches.FirstOrDefault(x => x.Id == branch);
            if (b == null || !b.Open)
            {
                return "Choose a pick-up branch.";
            }
            if (cls.Length > 0)
            {
                ClassRec? c = f.Classes.FirstOrDefault(x => x.Id == cls);
                if (c == null || (!staff && !c.Show))
                {
                    return "That vehicle class is not offered.";
                }
            }
            DateTime now = DateTime.Now;
            if (s == default || e == default)
            {
                return "Choose the pick-up and return date and time.";
            }
            if (s < (staff ? now.AddMinutes(-30) : now.AddHours(1)))
            {
                return staff ? "Pick-up cannot be in the past." : "Pick-up must be at least one hour from now.";
            }
            if (s > now.AddDays(365))
            {
                return "Bookings open up to one year ahead.";
            }
            if (e <= s.AddHours(1))
            {
                return "Return must be at least one hour after pick-up.";
            }
            if ((e - s).TotalDays > 60)
            {
                return "Rentals can be up to 60 days. Call us for a longer rental.";
            }
            return string.Empty;
        }

        public static Dictionary<string, string> DriverErrors(SiteData site, Driver d, DateTime start, out DateTime birth)
        {
            Dictionary<string, string> e = new Dictionary<string, string>();
            birth = default;
            e["first"] = d.First.Length < 1 ? "Please enter the first name." : string.Empty;
            e["last"] = d.Last.Length < 1 ? "Please enter the last name." : string.Empty;
            e["email"] = !Ui.IsEmail(d.Email) ? "Please enter a valid email address." : string.Empty;
            e["phone"] = !Ui.IsPhone(d.Phone) ? "Please enter a phone number we can call." : string.Empty;
            e["license"] = d.License.Length < 4 || !d.License.All(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == ' ') ? "Please enter the driver's licence number (letters, numbers and dashes)." : string.Empty;
            e["lstate"] = d.State.Length < 2 ? "Please enter the state or country that issued the licence." : string.Empty;
            if (!Rental.ParseDate(d.BirthText, out birth))
            {
                e["birth"] = "Please enter the date of birth.";
            }
            else if (birth.Year < 1900 || birth > DateTime.Today)
            {
                e["birth"] = "Please check the date of birth.";
            }
            else if (Rental.Age(birth, start) < site.MinAge)
            {
                e["birth"] = "The driver must be at least " + site.MinAge + " years old at pick-up.";
            }
            else
            {
                e["birth"] = string.Empty;
            }
            return e;
        }

        public static List<BookedExtra> ParseExtras(string? raw)
        {
            List<BookedExtra> list = new List<BookedExtra>();
            foreach (string part in (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int q) && q > 0 && kv[0].Trim().Length > 0 && !list.Any(x => x.Id == kv[0].Trim()))
                {
                    list.Add(new BookedExtra { Id = kv[0].Trim(), Qty = Math.Min(q, 9) });
                }
            }
            return list;
        }

        public static string ExtrasError(FleetData f, string branch, DateTime s, DateTime e, string ignore, List<BookedExtra> picked, bool staff)
        {
            int covers = 0;
            foreach (BookedExtra p in picked)
            {
                ExtraRec? x = f.Extras.FirstOrDefault(y => y.Id == p.Id);
                if (x == null || (!staff && !x.Show))
                {
                    return "One of the chosen extras is no longer offered. Please choose again.";
                }
                if (x.Kind == "cover")
                {
                    covers++;
                    p.Qty = 1;
                }
                if (p.Qty > x.MaxQty)
                {
                    return x.Name + ": up to " + x.MaxQty + " per rental.";
                }
                int left = Avail.ExtraLeft(f, branch, x.Id, s, e, ignore);
                if (x.Kind == "item" && p.Qty > left)
                {
                    return left == 0 ? x.Name + " is not available at this branch for these dates." : x.Name + ": only " + left + " left at this branch for these dates.";
                }
            }
            return covers > 1 ? "Choose one coverage option." : string.Empty;
        }

        public static async Task Persist(FleetData f, bool bookings, bool cars, bool transfers)
        {
            if (bookings)
            {
                await Store.SaveRows("bookings", f.Bookings);
            }
            if (cars)
            {
                await Store.SaveRows("cars", f.Cars);
            }
            if (transfers)
            {
                await Store.SaveRows("transfers", f.Transfers);
            }
        }

        public static string Plate(FleetData f, string carId)
        {
            CarRec? c = f.Cars.FirstOrDefault(x => x.Id == carId);
            return c == null ? "No car yet" : c.Plate + " · " + c.Make + " " + c.Model;
        }

        public static (BookingRec? Booking, string Error) Place(FleetData f, BookingRec draft, bool staff)
        {
            string err = TripError(f, draft.BranchId, draft.ClassId, draft.Start, draft.End, staff);
            if (err.Length > 0)
            {
                return (null, err);
            }
            err = ExtrasError(f, draft.BranchId, draft.Start, draft.End, draft.Id, draft.Extras, staff);
            if (err.Length > 0)
            {
                return (null, err);
            }
            ClassRec c = f.Classes.First(x => x.Id == draft.ClassId);
            CarRec? now = f.Cars.FirstOrDefault(x => x.Id == draft.CarId && x.ClassId == draft.ClassId);
            string carId = now != null && Avail.CarFree(f, now, draft.BranchId, draft.Start, draft.End, draft.Id) ? now.Id : Avail.Assign(f, draft.BranchId, draft.ClassId, draft.Start, draft.End, draft.Id).CarId;
            if (carId.Length == 0)
            {
                return (null, "Sorry, no " + c.Name + " car is free at this branch for these dates.");
            }
            Quote q = Rental.Price(f.Site, c, f.Extras, draft.Start, draft.End, draft.Extras, 0);
            draft.CarId = carId;
            draft.RateTotal = q.Rate;
            draft.ExtrasTotal = q.Extras;
            if (draft.Id.Length == 0)
            {
                draft.Id = Rental.NewId("b-");
                draft.Code = Rental.NewCode(f.Bookings);
                draft.Created = DateTime.Now;
                draft.Status = "Reserved";
                f.Bookings.Add(draft);
            }
            Avail.Settle(f);
            return (draft, string.Empty);
        }

        public static (string Error, List<BookingRec> Moved) Reflow(FleetData f, CarRec car, DateTime from, DateTime? until, string except)
        {
            DateTime now = DateTime.Now;
            TimeSpan buf = TimeSpan.FromHours(Math.Max(0, f.Site.BufferHours));
            DateTime to = until ?? DateTime.MaxValue.AddDays(-30);
            List<BookingRec> clash = f.Bookings.Where(b => b.CarId == car.Id && b.Id != except && Avail.Live(b) && Avail.Overlap(b.Start - buf, Avail.EndOf(b, now) + buf, from, to)).OrderBy(b => b.Start).ToList();
            List<BookingRec> moved = new List<BookingRec>();
            List<string> stuck = new List<string>();
            foreach (BookingRec b in clash)
            {
                if (Avail.Out(b))
                {
                    stuck.Add(b.Code + " (on rent)");
                    continue;
                }
                List<CarRec> alt = Avail.FreeCars(f, b.BranchId, b.ClassId, b.Start, b.End, b.Id).Where(x => x.Id != car.Id).ToList();
                if (alt.Count == 0)
                {
                    stuck.Add(b.Code);
                    continue;
                }
                b.CarId = Avail.Best(f, alt, b.Start).Id;
                moved.Add(b);
            }
            return (stuck.Count == 0 ? string.Empty : "Booked in that period with no other free car: " + string.Join(", ", stuck) + ".", moved);
        }

        public static ExtPlan PlanExtend(FleetData f, BookingRec b, DateTime newEnd)
        {
            ExtPlan p = new ExtPlan();
            DateTime now = DateTime.Now;
            ClassRec? c = f.Classes.FirstOrDefault(x => x.Id == b.ClassId);
            if (c == null || !Avail.Live(b))
            {
                p.Error = "Only reserved or picked-up bookings can be extended.";
                return p;
            }
            if (newEnd <= b.End || newEnd <= now.AddMinutes(30))
            {
                p.Error = "Choose a return time later than the current one.";
                return p;
            }
            if ((newEnd - b.Start).TotalDays > 60)
            {
                p.Error = "Rentals can be up to 60 days.";
                return p;
            }
            DateTime s0 = Avail.Out(b) ? now : b.Start;
            List<BookedExtra> items = b.Extras.ToList();
            string xe = ExtrasError(f, b.BranchId, b.End, newEnd, b.Id, items, true);
            if (xe.Length > 0)
            {
                p.Error = xe;
                return p;
            }
            CarRec? car = f.Cars.FirstOrDefault(x => x.Id == b.CarId);
            if (car != null && Avail.CarFree(f, car, b.BranchId, s0, newEnd, b.Id))
            {
                p.CarId = car.Id;
            }
            else if (car != null && !Avail.Blocked(car, s0, newEnd))
            {
                Dictionary<string, string> keep = f.Bookings.ToDictionary(x => x.Id, x => x.CarId);
                (string err, List<BookingRec> moved) = Reflow(f, car, b.End, newEnd, b.Id);
                if (err.Length == 0 && Avail.CarFree(f, car, b.BranchId, s0, newEnd, b.Id))
                {
                    p.CarId = car.Id;
                    p.Moves = moved.Select(m => (m.Id, m.CarId)).ToList();
                }
                foreach (BookingRec x in f.Bookings)
                {
                    x.CarId = keep[x.Id];
                }
            }
            if (p.CarId.Length == 0 && b.Status == "Reserved")
            {
                List<CarRec> alt = Avail.FreeCars(f, b.BranchId, b.ClassId, b.Start, newEnd, b.Id);
                if (alt.Count > 0)
                {
                    p.CarId = Avail.Best(f, alt, b.Start).Id;
                }
            }
            if (p.CarId.Length == 0)
            {
                p.Error = "No " + c.Name + " car is free until " + Rental.When(newEnd) + ". The extension cannot be confirmed.";
                return p;
            }
            p.Days = Rental.Days(b.Start, newEnd, f.Site.GraceMinutes);
            p.Rate = Rental.Rate(c, b.Start, p.Days);
            p.Extras = Rental.ExtrasCost(f.Extras, b.Extras, p.Days);
            p.Diff = p.Rate + p.Extras - b.RateTotal - b.ExtrasTotal;
            return p;
        }

        public static ExtensionRec ApplyExtend(FleetData f, BookingRec b, ExtPlan p, DateTime newEnd, string by, string note)
        {
            ExtensionRec x = new ExtensionRec { Id = Rental.NewId("x-"), BookingId = b.Id, When = DateTime.Now, By = by, OldEnd = b.End, NewEnd = newEnd, OldCarId = b.CarId, NewCarId = p.CarId, Amount = p.Diff, Note = note };
            foreach ((string id, string carId) in p.Moves)
            {
                BookingRec? o = f.Bookings.FirstOrDefault(y => y.Id == id);
                if (o != null)
                {
                    o.CarId = carId;
                }
            }
            b.End = newEnd;
            b.CarId = p.CarId;
            b.RateTotal = p.Rate;
            b.ExtrasTotal = p.Extras;
            b.Extensions++;
            b.AskEnd = null;
            b.AskNote = string.Empty;
            if (Avail.Out(b))
            {
                b.Status = "Extended";
            }
            Avail.Settle(f);
            return x;
        }

        public static string ShortCar(CarRec? c)
        {
            return c == null ? "No car" : c.Plate + " (" + c.Make + " " + c.Model + ")";
        }

        public static List<string> Flags(FleetData f, CarRec c)
        {
            List<string> list = new List<string>();
            DateTime now = DateTime.Now;
            if (f.Bookings.Any(b => b.CarId == c.Id && Avail.Overdue(b, now)))
            {
                list.Add("Overdue");
            }
            if (c.ServiceMiles > 0 && c.Mileage >= c.ServiceMiles - 300)
            {
                list.Add("Service due");
            }
            if (c.RegistrationDue != default && c.RegistrationDue <= DateTime.Today.AddDays(30) && !Avail.Gone.Contains(c.Status))
            {
                list.Add("Registration due");
            }
            if (f.Incidents.Any(x => x.CarId == c.Id && x.Status == "Open"))
            {
                list.Add("Open incident");
            }
            return list;
        }

        public static readonly string[] FlagNames = { "Overdue", "Service due", "Registration due", "Open incident" };

        public static async Task PersistAll(FleetData f)
        {
            await Store.SaveRows("bookings", f.Bookings);
            await Store.SaveRows("cars", f.Cars);
            await Store.SaveRows("transfers", f.Transfers);
            await Store.SaveRows("services", f.Services);
            await Store.SaveRows("repairs", f.Repairs);
            await Store.SaveRows("incidents", f.Incidents);
            await Store.SaveRows("sales", f.Sales);
            await Store.SaveRows("payments", f.Payments);
        }

        public static string Moved(List<BookingRec> m)
        {
            return m.Count > 0 ? " " + Ui.Count(m.Count, "booking moved", "bookings moved") + " to other cars: " + string.Join(", ", m.Select(b => b.Code)) + "." : string.Empty;
        }

        public static void Release(FleetData f, CarRec car, string from)
        {
            if (car.Status != from)
            {
                return;
            }
            bool repair = f.Repairs.Any(r => r.CarId == car.Id && (r.Status == "Open" || r.Status == "In shop"));
            bool service = f.Services.Any(v => v.CarId == car.Id && v.Status == "In progress");
            if (repair)
            {
                car.Status = "Repair";
                car.BlockUntil = f.Repairs.Where(r => r.CarId == car.Id && (r.Status == "Open" || r.Status == "In shop")).Select(r => r.ReadyBy).Max();
            }
            else if (service)
            {
                ServiceRec v = f.Services.First(x => x.CarId == car.Id && x.Status == "In progress");
                car.Status = "Maintenance";
                car.StatusNote = v.Kind;
                car.BlockUntil = v.End;
            }
            else
            {
                car.Status = "Available";
                car.StatusNote = string.Empty;
                car.BlockUntil = null;
            }
            car.StatusSince = DateTime.Now;
            Avail.Settle(f);
        }
    }
}
