using CarRental.Models;

namespace CarRental.Core
{
    public sealed class Area
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;
        public string Letters { get; init; } = "VAED";
        public bool Branched { get; init; }
    }

    public static class Access
    {
        public static readonly Area[] Areas =
        {
            new Area { Key = "desk", Name = "Desk", Page = "AdminDesk", Group = "Rentals", Letters = "VE", Branched = true },
            new Area { Key = "bookings", Name = "Bookings", Page = "AdminBookings", Group = "Rentals", Branched = true },
            new Area { Key = "calendar", Name = "Calendar", Page = "AdminCalendar", Group = "Rentals", Letters = "V", Branched = true },
            new Area { Key = "fleet", Name = "Fleet", Page = "AdminFleet", Group = "Fleet", Branched = true },
            new Area { Key = "service", Name = "Maintenance", Page = "AdminService", Group = "Fleet", Branched = true },
            new Area { Key = "repairs", Name = "Repairs", Page = "AdminRepairs", Group = "Fleet", Letters = "VAE", Branched = true },
            new Area { Key = "incidents", Name = "Incidents", Page = "AdminIncidents", Group = "Fleet", Letters = "VAE", Branched = true },
            new Area { Key = "transfers", Name = "Transfers", Page = "AdminTransfers", Group = "Fleet", Branched = true },
            new Area { Key = "sales", Name = "Car sales", Page = "AdminSales", Group = "Fleet", Branched = true },
            new Area { Key = "branches", Name = "Branches", Page = "AdminBranches", Group = "Fleet" },
            new Area { Key = "classes", Name = "Classes and rates", Page = "AdminClasses", Group = "Fleet", Letters = "VAED" },
            new Area { Key = "extras", Name = "Extras", Page = "AdminExtras", Group = "Fleet" },
            new Area { Key = "payments", Name = "Payments", Page = "AdminPayments", Group = "Money", Branched = true },
            new Area { Key = "insurers", Name = "Insurers", Page = "AdminInsurers", Group = "Money" },
            new Area { Key = "reports", Name = "Reports", Page = "AdminReports", Group = "Money", Letters = "V", Branched = true },
            new Area { Key = "pages", Name = "Pages", Page = "AdminPages", Group = "Website", Letters = "VE" },
            new Area { Key = "media", Name = "Media library", Page = "AdminMedia", Group = "Website" },
            new Area { Key = "messages", Name = "Messages", Page = "AdminMessages", Group = "Website", Letters = "VED" },
            new Area { Key = "staff", Name = "Staff", Page = "AdminStaff", Group = "Admin" },
            new Area { Key = "settings", Name = "Settings", Page = "AdminSettings", Group = "Admin", Letters = "VE" },
            new Area { Key = "activity", Name = "Activity log", Page = "AdminActivity", Group = "Admin", Letters = "V" }
        };

        public static readonly (char Letter, string Name)[] Rights = { ('V', "View"), ('A', "Add"), ('E', "Edit"), ('D', "Delete") };

        public static Area? Find(string key)
        {
            return Areas.FirstOrDefault(a => a.Key == key);
        }

        public static string Level(AdminUser u, string area)
        {
            Area? a = Find(area);
            if (a == null || !u.Active)
            {
                return string.Empty;
            }
            if (u.Manager)
            {
                return a.Letters;
            }
            return u.Perms.TryGetValue(area, out string? v) ? Clean(a, v) : string.Empty;
        }

        public static bool Can(AdminUser u, string area, char letter)
        {
            return Level(u, area).Contains(letter);
        }

        public static string Clean(Area a, string? v)
        {
            string s = new string((v ?? string.Empty).ToUpperInvariant().Where(ch => a.Letters.Contains(ch)).Distinct().ToArray());
            if (s.Length > 0 && !s.Contains('V'))
            {
                s = "V" + s;
            }
            return new string(Rights.Select(r => r.Letter).Where(ch => s.Contains(ch)).ToArray());
        }

        public static Dictionary<string, string> CleanAll(Dictionary<string, string> raw)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (Area a in Areas)
            {
                string v = Clean(a, raw.TryGetValue(a.Key, out string? x) ? x : string.Empty);
                if (v.Length > 0)
                {
                    d[a.Key] = v;
                }
            }
            return d;
        }

        public static bool BranchOk(AdminUser u, string branchId)
        {
            return u.Manager || u.AllBranches || u.Branches.Contains(branchId);
        }

        public static List<string> BranchIds(AdminUser u, List<BranchRec> all)
        {
            return all.Where(b => BranchOk(u, b.Id)).Select(b => b.Id).ToList();
        }

        public static string Describe(AdminUser u, List<BranchRec> branches)
        {
            if (u.Manager || u.AllBranches)
            {
                return "All branches";
            }
            List<string> names = branches.Where(b => u.Branches.Contains(b.Id)).Select(b => b.Name).ToList();
            return names.Count == 0 ? "No branch" : string.Join(", ", names);
        }
    }
}
