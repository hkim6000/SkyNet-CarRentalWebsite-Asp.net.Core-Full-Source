using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminSettings : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Settings | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private const string AreaKey = "settings";

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminSettingsJs.denied();");
                return null;
            }
            return c;
        }

        private static bool Can(Ctx c, char letter)
        {
            return Access.Can(c.U, AreaKey, letter);
        }

        private static async Task<ApiResponse> Deny(ApiResponse response, Ctx c, string action, string message)
        {
            await Store.Log(c.U.Id, action, "no right", false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("se-brand", Ui.Esc(site.Brand));
            response.SetElementContents("se-menu", await Auth.Menu("se", "AdminSettings", c));
            response.SetElementContents("se-who", Auth.Who("se", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
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
            response.SetElementContents("se-main", FormHtml(site, Can(c, 'E'), c.U.Manager));
            return response;
        }

        private static string Field(string id, string label, string value, int max, string hint = "", string type = "text")
        {
            return "<label class=\"se-field\" for=\"se-f-" + id + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"se-f-" + id + "\" data-k=\"" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\">" + (hint.Length > 0 ? "<small>" + hint + "</small>" : string.Empty) + "<em class=\"se-err\" id=\"se-e-" + id + "\"></em></label>";
        }

        private static string Area(string id, string label, string value, int max, int rows, string hint = "")
        {
            return "<label class=\"se-field\" for=\"se-f-" + id + "\"><span>" + label + "</span><textarea id=\"se-f-" + id + "\" data-k=\"" + id + "\" rows=\"" + rows + "\" maxlength=\"" + max + "\">" + Ui.Esc(value) + "</textarea>" + (hint.Length > 0 ? "<small>" + hint + "</small>" : string.Empty) + "<em class=\"se-err\" id=\"se-e-" + id + "\"></em></label>";
        }

        private static string Inv(decimal v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormHtml(SiteData s, bool edit, bool manager)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"se-ph\"><div><h1>Settings</h1><p>Company facts shown on the website and in emails, and the rules every booking and return follows.</p></div></div>");
            sb.Append("<form class=\"se-panel se-stack\" id=\"se-form\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<h2>The company</h2><div class=\"se-grid2\">" + Field("brand", "Short name", s.Brand, 40, "Used in the header and in titles.") + Field("legal", "Full name", s.Legal, 80, "Used in the footer and in emails.") + "</div>");
            sb.Append(Field("tagline", "Tagline", s.Tagline, 140));
            sb.Append("<div class=\"se-grid2\">" + Field("phone", "Main phone", s.Phone, 30) + Field("email", "Main email", s.Email, 80, string.Empty, "email") + "</div>");
            sb.Append("<h2 class=\"se-sec\">Booking rules</h2><div class=\"se-grid3\">" + Field("minage", "Youngest driver (years)", s.MinAge.ToString(CultureInfo.InvariantCulture), 2, "Checked against the date of birth at pick-up.", "number") + Field("cancel", "Free cancellation until (hours before pick-up)", s.CancelHours.ToString(CultureInfo.InvariantCulture), 3, string.Empty, "number") + Field("buffer", "Cleaning time between rentals (hours)", s.BufferHours.ToString(CultureInfo.InvariantCulture), 2, "A car is not offered again until this long after its last return.", "number") + "</div>");
            sb.Append("<div class=\"se-grid3\">" + Field("grace", "Grace time before an extra day (minutes)", s.GraceMinutes.ToString(CultureInfo.InvariantCulture), 3, "Rental days are counted in 24 hours. A return within this time does not add a day.", "number") + Field("tax", "Sales tax (%)", Inv(s.TaxRate), 6) + Field("video", "Largest video upload (MB)", s.MaxVideoMb.ToString(CultureInfo.InvariantCulture), 3, "Between 1 and 100.", "number") + "</div>");
            sb.Append("<h2 class=\"se-sec\">Return charges</h2><div class=\"se-grid2\">" + Field("fuel", "Fuel refill, per eighth of a tank (US dollars)", Inv(s.FuelPerEighth), 8) + Field("clean", "Extra cleaning (US dollars)", Inv(s.CleaningFee), 8) + Field("miles", "Miles included per day (0 for unlimited)", s.DailyMiles.ToString(CultureInfo.InvariantCulture), 5, string.Empty, "number") + Field("milerate", "Each extra mile (US dollars)", Inv(s.MileRate), 6) + "</div>");
            sb.Append("<h2 class=\"se-sec\">Maintenance</h2><div class=\"se-grid3\">" + Field("every", "Miles between scheduled services", s.ServiceEvery.ToString(CultureInfo.InvariantCulture), 6, "Sets the next service when a scheduled service or oil change is done.", "number") + "</div>");
            sb.Append("<h2 class=\"se-sec\">Messages</h2>" + Field("notify", "Send a copy of each new booking and message to", s.NotifyEmail, 80, Mailer.Ready ? "Mail is set up, so a copy is emailed here." : "Mail is not set up yet (see below), so nothing is emailed.", "email"));
            sb.Append(edit ? "<div class=\"se-acts\"><button type=\"submit\" class=\"se-btn\">Save settings</button></div>" : "<p class=\"se-hint\">You can view the settings but not change them.</p>");
            sb.Append("</form>");
            sb.Append("<section class=\"se-panel se-stack\"><h2>Email</h2><dl class=\"se-kv\"><dt>Mail server</dt><dd>" + Ui.Esc(Mailer.Server.Length > 0 ? Mailer.Server : "Not set") + "</dd><dt>Sender</dt><dd>" + Ui.Esc(Mailer.From.Length > 0 ? Mailer.From : "Not set") + "</dd><dt>Status</dt><dd>" + (Mailer.Ready ? "Ready. Customers get booking emails." : "Not set up. The sample values in appConfig/application.cfg are placeholders.") + "</dd></dl><p class=\"se-hint\">The mail server, sender and password live in the app.mail lines of appConfig/application.cfg. Edit that file and restart the site. The password is never shown here.</p></section>");
            sb.Append("<section class=\"se-panel se-stack\"><h2>Run mode</h2><p class=\"se-hint\">Run mode is set in appConfig/application.cfg.</p></section>");
            if (Store.Demo() && manager)
            {
                sb.Append("<section class=\"se-panel se-stack se-danger\"><h2>Initialize data</h2><p class=\"se-hint\">Removes every booking, inspection, extension, transfer, car, service, repair, incident, sale, payment, message and the activity log. Branches, classes, extras, pages, settings, staff and the media library stay.</p><div><button type=\"button\" class=\"se-btn se-btn-danger\" onclick=\"AdminSettingsJs.initialize()\">Initialize data</button></div></section>");
            }
            return sb.ToString();
        }

        private static int Whole(string? v)
        {
            return int.TryParse((v ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1;
        }

        private static decimal Money(string? v)
        {
            return decimal.TryParse((v ?? string.Empty).Trim().TrimStart('$'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? Math.Round(d, 2) : -1;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Deny(response, c, "save settings", "You can view the settings but not change them.");
            }
            string brand = Ui.Clip(GetDataValue("brand"), 40);
            string legal = Ui.Clip(GetDataValue("legal"), 80);
            string tagline = Ui.Clip(GetDataValue("tagline"), 140);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string notify = Ui.Clip(GetDataValue("notify"), 80);
            int minAge = Whole(GetDataValue("minage"));
            int cancel = Whole(GetDataValue("cancel"));
            int buffer = Whole(GetDataValue("buffer"));
            int grace = Whole(GetDataValue("grace"));
            int video = Whole(GetDataValue("video"));
            int miles = Whole(GetDataValue("miles"));
            decimal tax = Money(GetDataValue("tax"));
            decimal fuel = Money(GetDataValue("fuel"));
            decimal clean = Money(GetDataValue("clean"));
            decimal mileRate = Money(GetDataValue("milerate"));
            int every = Whole(GetDataValue("every"));
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "brand", brand.Length < 2 ? "Please enter the short name." : string.Empty },
                { "legal", legal.Length < 2 ? "Please enter the full name." : string.Empty },
                { "tagline", string.Empty },
                { "phone", !Ui.IsPhone(phone) ? "Please enter a valid phone number." : string.Empty },
                { "email", !Ui.IsEmail(email) ? "Please enter a valid email address." : string.Empty },
                { "notify", notify.Length > 0 && !Ui.IsEmail(notify) ? "Please enter a valid email address, or leave it empty." : string.Empty },
                { "minage", minAge < 16 || minAge > 30 ? "From 16 to 30." : string.Empty },
                { "cancel", cancel < 0 || cancel > 168 ? "From 0 to 168 hours." : string.Empty },
                { "buffer", buffer < 0 || buffer > 24 ? "From 0 to 24 hours." : string.Empty },
                { "grace", grace < 0 || grace > 180 ? "From 0 to 180 minutes." : string.Empty },
                { "video", video < 1 || video > 100 ? "Enter a whole number from 1 to 100." : string.Empty },
                { "miles", miles < 0 || miles > 5000 ? "From 0 to 5,000." : string.Empty },
                { "tax", tax < 0 || tax > 30 ? "From 0 to 30." : string.Empty },
                { "fuel", fuel < 0 || fuel > 100 ? "From 0 to 100." : string.Empty },
                { "clean", clean < 0 || clean > 1000 ? "From 0 to 1,000." : string.Empty },
                { "milerate", mileRate < 0 || mileRate > 10 ? "From 0 to 10." : string.Empty },
                { "every", every < 1000 || every > 50000 ? "From 1,000 to 50,000." : string.Empty }
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("se-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                site.Brand = brand;
                site.Legal = legal;
                site.Tagline = tagline;
                site.Phone = phone;
                site.Email = email;
                site.NotifyEmail = notify;
                site.MinAge = minAge;
                site.CancelHours = cancel;
                site.BufferHours = buffer;
                site.GraceMinutes = grace;
                site.MaxVideoMb = video;
                site.DailyMiles = miles;
                site.TaxRate = tax;
                site.FuelPerEighth = fuel;
                site.CleaningFee = clean;
                site.MileRate = mileRate;
                site.ServiceEvery = every;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "save settings", brand, true);
            SiteData fresh = await Store.LoadSite();
            response.SetElementContents("se-brand", Ui.Esc(fresh.Brand));
            response.ExecuteScript("AdminSettingsJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Initialize()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.U.Manager)
            {
                return await Deny(response, c, "initialize data", "Only a manager can initialize the data.");
            }
            if (!Store.Demo())
            {
                return await Refuse(response, c, "initialize data", "settings", "In Production the data is initialized once, on the first start. Edit records in their own pages instead.");
            }
            if ((GetDataValue("word") ?? string.Empty).Trim() != "INITIALIZE")
            {
                response.ExecuteScript("AdminSettingsJs.toast('Type INITIALIZE to confirm.');");
                return response;
            }
            await Store.Log(c.U.Id, "initialize data", "requested from Settings", true);
            int removed = await Init.Run("Settings, by " + c.U.Name, false, true);
            response.ExecuteScript("AdminSettingsJs.initialized(" + removed + ");");
            return response;
        }
    }
}
