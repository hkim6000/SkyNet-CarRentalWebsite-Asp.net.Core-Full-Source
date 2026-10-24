using System.Security.Cryptography;
using System.Text;
using CarRental.Models;

namespace CarRental.Core
{
    public sealed class Ctx
    {
        public AdminUser U { get; init; } = new AdminUser();
        public string Token { get; init; } = string.Empty;
    }

    public static class Auth
    {
        private static readonly SemaphoreSlim SessGate = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, List<DateTime>> Misses = new Dictionary<string, List<DateTime>>();
        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        public static string NewPassword()
        {
            StringBuilder sb = new StringBuilder();
            for (int g = 0; g < 3; g++)
            {
                if (g > 0)
                {
                    sb.Append('-');
                }
                for (int i = 0; i < 4; i++)
                {
                    sb.Append(Letters[RandomNumberGenerator.GetInt32(Letters.Length)]);
                }
            }
            return sb.ToString();
        }

        public static string HashPassword(string pass)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(pass, salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToHexString(salt) + "$" + Convert.ToHexString(hash);
        }

        public static bool VerifyPassword(string pass, string stored)
        {
            string[] p = stored.Split('$');
            if (p.Length != 4 || p[0] != "pbkdf2" || !int.TryParse(p[1], out int rounds))
            {
                return false;
            }
            try
            {
                byte[] hash = Rfc2898DeriveBytes.Pbkdf2(pass, Convert.FromHexString(p[2]), rounds, HashAlgorithmName.SHA256, 32);
                return CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(p[3]));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool Paused(string login)
        {
            lock (Misses)
            {
                if (!Misses.TryGetValue(login, out List<DateTime>? list))
                {
                    return false;
                }
                list.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));
                return list.Count >= 6;
            }
        }

        public static void Failed(string login)
        {
            lock (Misses)
            {
                if (!Misses.TryGetValue(login, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Misses[login] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        private static async Task<List<SessionRec>> LoadSessions()
        {
            List<SessionRec> list = await Store.Read(Store.SessionFile, () => new List<SessionRec>());
            list.RemoveAll(s => s.Expires < DateTime.UtcNow);
            return list;
        }

        private static async Task<string> Open(string userId)
        {
            await SessGate.WaitAsync();
            try
            {
                List<SessionRec> list = await LoadSessions();
                string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                list.Add(new SessionRec { Token = token, UserId = userId, Expires = DateTime.UtcNow.AddHours(8) });
                await Store.Write(Store.SessionFile, list);
                return token;
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task CloseAll(string userId, string keep)
        {
            await SessGate.WaitAsync();
            try
            {
                List<SessionRec> list = await LoadSessions();
                list.RemoveAll(s => s.UserId == userId && s.Token != keep);
                await Store.Write(Store.SessionFile, list);
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task SignOut(string? tok)
        {
            await SessGate.WaitAsync();
            try
            {
                List<SessionRec> list = await LoadSessions();
                list.RemoveAll(s => s.Token == (tok ?? string.Empty));
                await Store.Write(Store.SessionFile, list);
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task<Ctx?> Check(string? tok)
        {
            string t = (tok ?? string.Empty).Trim();
            if (t.Length < 16 || t.Length > 64)
            {
                return null;
            }
            SessionRec? s;
            await SessGate.WaitAsync();
            try
            {
                List<SessionRec> list = await LoadSessions();
                s = list.FirstOrDefault(x => x.Token == t);
                if (s != null && s.Expires < DateTime.UtcNow.AddHours(7))
                {
                    s.Expires = DateTime.UtcNow.AddHours(8);
                    await Store.Write(Store.SessionFile, list);
                }
            }
            finally
            {
                SessGate.Release();
            }
            if (s == null)
            {
                return null;
            }
            List<AdminUser> staff = await Store.LoadStaff();
            AdminUser? u = staff.FirstOrDefault(x => x.Id == s.UserId && !x.MustChange && x.Active);
            return u == null ? null : new Ctx { U = u, Token = t };
        }

        public static async Task<(string State, string Token)> SignIn(string login, string pass)
        {
            string name = login.Trim().ToLowerInvariant();
            if (Paused(name))
            {
                return ("paused", string.Empty);
            }
            List<AdminUser> staff = await Store.LoadStaff();
            AdminUser? u = staff.FirstOrDefault(x => x.Login == name && x.Active);
            if (u != null && Store.Demo())
            {
                if (u.MustChange)
                {
                    using (await Store.Hold())
                    {
                        List<AdminUser> all = await Store.LoadStaff();
                        AdminUser? m = all.FirstOrDefault(x => x.Id == u.Id);
                        if (m != null)
                        {
                            m.MustChange = false;
                            await Store.SaveStaff(all);
                        }
                    }
                }
                return ("ok", await Open(u.Id));
            }
            if (u != null && u.Hash != string.Empty && VerifyPassword(pass, u.Hash))
            {
                if (u.MustChange)
                {
                    return ("change", string.Empty);
                }
                return ("ok", await Open(u.Id));
            }
            Failed(name);
            return ("bad", string.Empty);
        }

        public static async Task<(string Error, string Token)> SetFirstPassword(string login, string current, string next, string again)
        {
            string name = login.Trim().ToLowerInvariant();
            if (Paused(name))
            {
                return ("Too many tries. Wait a few minutes and try again.", string.Empty);
            }
            using (await Store.Hold())
            {
                List<AdminUser> staff = await Store.LoadStaff();
                AdminUser? u = staff.FirstOrDefault(x => x.Login == name && x.MustChange);
                if (u == null || !VerifyPassword(current, u.Hash))
                {
                    Failed(name);
                    return ("The user name or password is not right.", string.Empty);
                }
                string e = Weak(next, current, again);
                if (e.Length > 0)
                {
                    return (e, string.Empty);
                }
                u.Hash = HashPassword(next);
                u.MustChange = false;
                await Store.SaveStaff(staff);
                if (File.Exists(Store.InitialFile))
                {
                    File.Delete(Store.InitialFile);
                }
                await CloseAll(u.Id, string.Empty);
                string token = await Open(u.Id);
                return (string.Empty, token);
            }
        }

        public static string Weak(string next, string current, string again)
        {
            if (next.Length < 8)
            {
                return "Use at least 8 characters for the new password.";
            }
            if (next == current)
            {
                return "Choose a password different from the current one.";
            }
            if (next != again)
            {
                return "The two new passwords are not the same.";
            }
            return string.Empty;
        }

        public static async Task<string> ChangeOwn(Ctx c, string current, string next, string again)
        {
            using (await Store.Hold())
            {
                List<AdminUser> staff = await Store.LoadStaff();
                AdminUser? u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                if (u == null || !VerifyPassword(current, u.Hash))
                {
                    return "The current password is not right.";
                }
                string e = Weak(next, current, again);
                if (e.Length > 0)
                {
                    return e;
                }
                u.Hash = HashPassword(next);
                await Store.SaveStaff(staff);
            }
            await CloseAll(c.U.Id, c.Token);
            return string.Empty;
        }

        public static async Task<string> Menu(string p, string current, Ctx c)
        {
            List<InquiryRec> msgs = await Store.Rows<InquiryRec>("messages");
            int fresh = msgs.Count(m => m.Status == "New");
            StringBuilder sb = new StringBuilder();
            sb.Append("<a class=\"" + p + "-mi" + (current == "Admin" ? " " + p + "-on" : string.Empty) + "\" href=\"Admin\">Dashboard</a>");
            string group = string.Empty;
            foreach (Area a in Access.Areas.Where(x => x.Page.Length > 0 && Access.Can(c.U, x.Key, 'V')))
            {
                if (a.Group != group)
                {
                    group = a.Group;
                    sb.Append("<span class=\"" + p + "-mg\">" + Ui.Esc(group) + "</span>");
                }
                sb.Append("<a class=\"" + p + "-mi" + (a.Page == current ? " " + p + "-on" : string.Empty) + "\" href=\"" + a.Page + "\">" + Ui.Esc(a.Name) + (a.Key == "messages" && fresh > 0 ? "<i class=\"" + p + "-badge\">" + fresh + "</i>" : string.Empty) + "</a>");
            }
            sb.Append("<a class=\"" + p + "-mi" + (current == "AdminAccount" ? " " + p + "-on" : string.Empty) + "\" href=\"AdminAccount\">My account</a>");
            return sb.ToString();
        }

        public static string Who(string p, Ctx c)
        {
            return "<b>" + Ui.Esc(c.U.Name) + "</b><small>" + Ui.Esc(c.U.Title.Length > 0 ? c.U.Title : c.U.Manager ? "Manager" : "Staff") + "</small>";
        }
    }
}
