using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminAccount : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("My account | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
            }
            return c;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("my-brand", Ui.Esc(site.Brand));
            response.SetElementContents("my-menu", await Auth.Menu("my", "AdminAccount", c));
            response.SetElementContents("my-who", Auth.Who("my", c));
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
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"my-ph\"><div><h1>My account</h1><p>Your own staff account. A manager sets what you can see and change on Staff.</p></div></div>");
            sb.Append(ProfileHtml(c.U));
            sb.Append("<section class=\"my-panel my-stack\"><h2>Your access</h2><dl class=\"my-kv\"><dt>User name</dt><dd>" + Ui.Esc(c.U.Login) + "</dd><dt>Branches</dt><dd>" + Ui.Esc(Access.Describe(c.U, await Store.Rows<BranchRec>("branches"))) + "</dd><dt>Access</dt><dd>" + (c.U.Manager ? "Manager, full access to every area" : string.Join("<br>", Access.Areas.Where(a => Access.Can(c.U, a.Key, 'V')).Select(a => Ui.Esc(a.Name) + ": " + string.Join(", ", Access.Rights.Where(r => Access.Can(c.U, a.Key, r.Letter)).Select(r => r.Name.ToLowerInvariant()))))) + "</dd></dl></section>");
            sb.Append("<form class=\"my-panel my-stack\" id=\"my-form\" onsubmit=\"return AdminAccountJs.change()\" novalidate><h2>Change password</h2>");
            sb.Append("<label class=\"my-field\" for=\"my-f-cur\"><span>Current password</span><input type=\"password\" id=\"my-f-cur\" data-k=\"cur\" autocomplete=\"current-password\" maxlength=\"100\"><em class=\"my-err\" id=\"my-e-cur\"></em></label>");
            sb.Append("<label class=\"my-field\" for=\"my-f-new\"><span>New password</span><input type=\"password\" id=\"my-f-new\" data-k=\"next\" autocomplete=\"new-password\" maxlength=\"100\"><small>At least 8 characters.</small><em class=\"my-err\" id=\"my-e-new\"></em></label>");
            sb.Append("<label class=\"my-field\" for=\"my-f-again\"><span>New password again</span><input type=\"password\" id=\"my-f-again\" data-k=\"again\" autocomplete=\"new-password\" maxlength=\"100\"><em class=\"my-err\" id=\"my-e-again\"></em></label>");
            sb.Append("<div class=\"my-acts\"><button type=\"submit\" class=\"my-btn\">Change password</button></div><p class=\"my-hint\">Changing the password signs you out of other browsers.</p></form>");
            response.SetElementContents("my-main", sb.ToString());
            return response;
        }

        private static string ProfileField(string key, string label, string value, int max, string type, string extra, string hint)
        {
            return "<label class=\"my-field\" for=\"my-f-" + key + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"my-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">" + (hint.Length > 0 ? "<small>" + hint + "</small>" : string.Empty) + "<em class=\"my-err\" id=\"my-e-" + key + "\"></em></label>";
        }

        private static string ProfileHtml(AdminUser u)
        {
            StringBuilder sb = new StringBuilder("<form class=\"my-panel my-stack\" id=\"my-pform\" onsubmit=\"return AdminAccountJs.profile()\" novalidate><h2>Profile</h2>");
            sb.Append("<div class=\"my-grid2\">" + ProfileField("name", "Full name", u.Name, 60, "text", " autocomplete=\"name\"", string.Empty) + ProfileField("title", "Job title", u.Title, 40, "text", string.Empty, "Shown under your name at the top of every staff page.") + "</div>");
            sb.Append("<div class=\"my-grid2\">" + ProfileField("email", "Email", u.Email, 80, "email", " autocomplete=\"email\"", "Optional.") + ProfileField("phone", "Phone", u.Phone, 25, "text", " inputmode=\"tel\" autocomplete=\"tel\"", "Optional.") + "</div>");
            sb.Append("<div class=\"my-acts\"><button type=\"submit\" class=\"my-btn\">Save profile</button></div><p class=\"my-hint\">Your user name, rights and branches are set by a manager on Staff.</p></form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Profile()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string name = Ui.Clip(GetDataValue("name"), 60);
            string title = Ui.Clip(GetDataValue("title"), 40);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 25);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Please enter your full name." : string.Empty,
                ["title"] = string.Empty,
                ["email"] = email.Length > 0 && !Ui.IsEmail(email) ? "Please enter a valid email address." : string.Empty,
                ["phone"] = phone.Length > 0 && !Ui.IsPhone(phone) ? "Use 7 to 15 digits; spaces, brackets, dots, dashes or a plus sign are fine." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("my-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            AdminUser? u;
            using (await Store.Hold())
            {
                List<AdminUser> staff = await Store.LoadStaff();
                u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                if (u == null)
                {
                    response.ExecuteScript("AdminAccountJs.expired();");
                    return response;
                }
                u.Name = name;
                u.Title = title;
                u.Email = email;
                u.Phone = phone;
                await Store.SaveStaff(staff);
            }
            await Store.Log(c.U.Id, "edit own profile", name, true);
            response.SetElementContents("my-who", Auth.Who("my", new Ctx { U = u, Token = c.Token }));
            response.ExecuteScript("AdminAccountJs.profiled();");
            return response;
        }

        public async Task<ApiResponse> Change()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (Store.Demo())
            {
                response.SetElementContents("my-e-cur", "In demo mode the sample passwords stay demo1234, so everyone can try the site.");
                return response;
            }
            string cur = GetDataValue("cur") ?? string.Empty;
            string next = GetDataValue("next") ?? string.Empty;
            string again = GetDataValue("again") ?? string.Empty;
            string eCur = cur.Length == 0 ? "Please enter your current password." : string.Empty;
            string eNew = next.Length == 0 ? "Please enter a new password." : string.Empty;
            string eAgain = again.Length == 0 ? "Please repeat the new password." : string.Empty;
            response.SetElementContents("my-e-cur", eCur);
            response.SetElementContents("my-e-new", eNew);
            response.SetElementContents("my-e-again", eAgain);
            if (eCur + eNew + eAgain != string.Empty)
            {
                return response;
            }
            string error = await Auth.ChangeOwn(c, cur, next, again);
            if (error.Length > 0)
            {
                await Store.Log(c.U.Id, "change password", "refused", false);
                bool wrongCurrent = error.StartsWith("The current", StringComparison.Ordinal);
                response.SetElementContents(wrongCurrent ? "my-e-cur" : "my-e-new", error);
                return response;
            }
            await Store.Log(c.U.Id, "change password", c.U.Login, true);
            response.ExecuteScript("AdminAccountJs.changed();");
            return response;
        }
    }
}
