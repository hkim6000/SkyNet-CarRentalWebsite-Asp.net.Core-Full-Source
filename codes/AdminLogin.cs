using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminLogin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Staff sign in");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            await Init.OnStart();
            await Store.EnsureFirstManager();
            SiteData site = await Store.LoadSite();
            bool demo = Store.Demo();
            string first = string.Empty;
            string pass = Local.Request() ? Store.InitialPassword() : string.Empty;
            if (!demo && pass.Length > 0)
            {
                first = "<div class=\"lg-first\"><b>First sign-in</b>User name <code>admin</code><br>Password <code>" + Ui.Esc(pass) + "</code><small>Shown only on this computer, until you choose your own password. Also in appConfig/initial-admin.txt.</small></div>";
            }
            string note = string.Empty;
            if (demo)
            {
                List<AdminUser> staff = await Store.LoadStaff();
                note = "<div class=\"lg-demo\"><b>Demo</b><p>The owner account is filled in. Each login sees only what its permissions allow. In demo any password signs in, also for staff you add:</p><ul>" + string.Concat(staff.Where(s => s.Active).Select(s => "<li><code>" + Ui.Esc(s.Login) + "</code> " + Ui.Esc(s.Title) + "</li>")) + "</ul></div>";
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_first}", first)
                .Replace("{plhd_demo}", note)
                .Replace("{plhd_user}", demo ? "owner" : string.Empty)
                .Replace("{plhd_pass}", demo ? "demo1234" : string.Empty);
        }

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string user = Ui.Clip(GetDataValue("user"), 60);
            string pass = GetDataValue("pass") ?? string.Empty;
            if (user.Length == 0 || pass.Length == 0)
            {
                response.ExecuteScript("AdminLoginJs.message('Please enter your user name and password.');");
                return response;
            }
            (string state, string token) = await Auth.SignIn(user, pass);
            switch (state)
            {
                case "ok":
                    Ctx? c = await Auth.Check(token);
                    await Store.Log(c?.U.Id ?? "system", "sign in", c?.U.Name ?? user, true);
                    response.ExecuteScript("AdminLoginJs.ok('" + token + "');");
                    break;
                case "change":
                    response.ExecuteScript("AdminLoginJs.choose();");
                    break;
                case "paused":
                    response.ExecuteScript("AdminLoginJs.message('Too many wrong passwords. Wait a few minutes and try again.');");
                    break;
                default:
                    await Store.Log("visitor", "sign in refused", user, false);
                    response.ExecuteScript("AdminLoginJs.message('The user name or password is not right.');");
                    break;
            }
            return response;
        }

        public async Task<ApiResponse> SetPassword()
        {
            ApiResponse response = new ApiResponse();
            string user = Ui.Clip(GetDataValue("user"), 60);
            (string error, string token) = await Auth.SetFirstPassword(user, GetDataValue("pass") ?? string.Empty, GetDataValue("next") ?? string.Empty, GetDataValue("again") ?? string.Empty);
            if (error.Length > 0)
            {
                response.ExecuteScript("AdminLoginJs.message('" + Ui.Js(error) + "');");
                return response;
            }
            Ctx? c = await Auth.Check(token);
            await Store.Log(c?.U.Id ?? "system", "first password chosen", c?.U.Name ?? user, true);
            response.ExecuteScript("AdminLoginJs.ok('" + token + "');");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.SignOut(GetDataValue("tok"));
            response.ExecuteScript("location.href='AdminLogin';");
            return response;
        }
    }
}
