using System.Globalization;
using System.Text;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminMedia : WebPage
    {
        private const int Size = 12;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Media library | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private const string AreaKey = "media";

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMediaJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminMediaJs.denied();");
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
            response.ExecuteScript("AdminMediaJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ml-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ml-menu", await Auth.Menu("ml", "AdminMedia", c));
            response.SetElementContents("ml-who", Auth.Who("ml", c));
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
            List<MediaItem> media = await Media.Load();
            Links links = await LoadLinks();
            response.SetElementContents("ml-main", PageHtml(site, media, links));
            return response;
        }

        private sealed class Links
        {
            public List<BranchRec> Branches { get; init; } = new List<BranchRec>();
            public List<ClassRec> Classes { get; init; } = new List<ClassRec>();
            public List<InspectionRec> Inspections { get; init; } = new List<InspectionRec>();
            public List<IncidentRec> Incidents { get; init; } = new List<IncidentRec>();
        }

        private static async Task<Links> LoadLinks()
        {
            return new Links { Branches = await Store.Rows<BranchRec>("branches"), Classes = await Store.Rows<ClassRec>("classes"), Inspections = await Store.Rows<InspectionRec>("inspections"), Incidents = await Store.Rows<IncidentRec>("incidents") };
        }

        private static List<string> UsedOf(SiteData site, Links l, string path)
        {
            return Media.UsedBy(site, path, l.Branches, l.Classes, l.Inspections, l.Incidents);
        }

        private async Task<string> LibNow(SiteData site)
        {
            List<MediaItem> media = await Media.Load();
            Links links = await LoadLinks();
            return LibHtml(site, media, links, Kind(GetDataValue("mk")), Ui.Clip(GetDataValue("mq"), 40), PageNo(GetDataValue("mp")));
        }

        public async Task<ApiResponse> Lib()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            response.SetElementContents("ml-lib", await LibNow(site));
            return response;
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'A'))
            {
                response.ExecuteScript("AdminMediaJs.uploaded();");
                return await Deny(response, c, "upload media", "You can view the media library but not add files.");
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.SetElementContents("ml-upmsg", "Choose one or more photos or videos first.");
                response.ExecuteScript("AdminMediaJs.uploaded();");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files, site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            string msg = (added > 0 ? Ui.Count(added, "file", "files") + " added to the library. " : string.Empty) + string.Join(" ", errors);
            response.SetElementContents("ml-lib", await LibNow(site));
            response.SetElementContents("ml-upmsg", Ui.Esc(msg.Trim()));
            response.ExecuteScript("AdminMediaJs.uploaded();");
            return response;
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            List<MediaItem> media = await Media.Load();
            MediaItem? m = media.FirstOrDefault(x => x.Id == id);
            if (m == null)
            {
                response.ExecuteScript("AdminMediaJs.toast('That file was not found.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            Links links = await LoadLinks();
            response.SetElementContents("ml-mbody", FormHtml(site, m, UsedOf(site, links, m.Path)));
            response.ExecuteScript("AdminMediaJs.opened('" + Ui.Js(m.Name) + "');");
            return response;
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
                return await Deny(response, c, "edit media", "You can view the media library but not change files.");
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            string name = Media.CleanName(GetDataValue("name") ?? string.Empty);
            string alt = (GetDataValue("alt") ?? string.Empty).Trim();
            string caption = (GetDataValue("caption") ?? string.Empty).Trim();
            string eName = name.Length == 0 ? "Please enter a file name." : name.Length > 80 ? "Keep the name under 80 characters." : string.Empty;
            string eAlt = alt.Length > 160 ? "Keep it under 160 characters." : string.Empty;
            string eCap = caption.Length > 200 ? "Keep it under 200 characters." : string.Empty;
            response.SetElementContents("ml-e-name", eName);
            response.SetElementContents("ml-e-alt", eAlt);
            response.SetElementContents("ml-e-caption", eCap);
            if (eName + eAlt + eCap != string.Empty)
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<MediaItem> list = await Media.Load();
                MediaItem? m = list.FirstOrDefault(x => x.Id == id);
                if (m == null)
                {
                    response.ExecuteScript("AdminMediaJs.toast('That file was not found.');");
                    return response;
                }
                string ext = Path.GetExtension(m.Path);
                if (Path.GetExtension(name).ToLowerInvariant() != ext.ToLowerInvariant())
                {
                    name = Path.GetFileNameWithoutExtension(name) + ext;
                }
                m.Name = name;
                m.Alt = alt;
                m.Caption = caption;
                await Media.Save(list);
            }
            await Store.Log(c.U.Id, "edit media", name, true);
            SiteData site = await Store.LoadSite();
            response.SetElementContents("ml-lib", await LibNow(site));
            response.ExecuteScript("AdminMediaJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Replace()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                response.ExecuteScript("AdminMediaJs.replaced(false,'You can view the media library but not change files.');");
                return await Deny(response, c, "replace media", "You can view the media library but not change files.");
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.ExecuteScript("AdminMediaJs.replaced(false,'Choose the new file first.');");
                return response;
            }
            string error = await Media.Replace(id, files[0], site.MaxVideoMb, c.U.Name);
            if (error.Length > 0)
            {
                await Store.Log(c.U.Id, "replace media", id, false);
                response.ExecuteScript("AdminMediaJs.replaced(false,'" + Ui.Js(error) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "replace media", id, true);
            response.SetElementContents("ml-lib", await LibNow(site));
            response.ExecuteScript("AdminMediaJs.replaced(true,'File replaced everywhere it is used.');");
            return response;
        }

        public async Task<ApiResponse> Drop()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'D'))
            {
                return await Deny(response, c, "delete media", "You can view the media library but not delete files.");
            }
            string id = Ui.Clip(GetDataValue("id"), 20);
            string name = string.Empty;
            List<string> used = new List<string>();
            SiteData site = await Store.LoadSite();
            Links links = await LoadLinks();
            using (await Store.Hold())
            {
                List<MediaItem> list = await Media.Load();
                MediaItem? m = list.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    name = m.Name;
                    used = UsedOf(site, links, m.Path);
                    if (used.Count == 0)
                    {
                        list.Remove(m);
                        await Media.Save(list);
                        Media.Erase(m);
                    }
                }
            }
            if (name.Length == 0)
            {
                response.ExecuteScript("AdminMediaJs.toast('That file was not found.');");
                return response;
            }
            if (used.Count > 0)
            {
                await Store.Log(c.U.Id, "delete media", name, false);
                response.ExecuteScript("AdminMediaJs.toast('" + Ui.Js(name + " is still used: " + string.Join(", ", used.Take(3)) + (used.Count > 3 ? " and more" : string.Empty) + ". Replace it there first.") + "');");
                return response;
            }
            await Store.Log(c.U.Id, "delete media", name, true);
            response.SetElementContents("ml-lib", await LibNow(site));
            response.ExecuteScript("AdminMediaJs.dropped();");
            return response;
        }

        private static int PageNo(string? v)
        {
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        private static string Kind(string? v)
        {
            string k = (v ?? string.Empty).Trim();
            return k == "photo" || k == "video" ? k : string.Empty;
        }

        private static string PageHtml(SiteData site, List<MediaItem> media, Links links)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ml-ph\"><div><h1>Media library</h1><p>Every picture and video on the website comes from here. Pages, branches, vehicle classes, inspections and incidents all pick from this library.</p></div></div>");
            sb.Append("<section class=\"ml-panel ml-uppanel\"><label class=\"ml-btn ml-upbtn\"><input type=\"file\" id=\"ml-upfile\" multiple accept=\"" + Media.Accept + "\" onchange=\"AdminMediaJs.upload()\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 16V4M6 10l6-6 6 6M4 20h16\"/></svg>Upload photos or videos</label>");
            sb.Append("<p class=\"ml-hint\">Photos: JPG, PNG, WebP or GIF up to " + Media.PhotoMb + " MB. Videos: MP4 up to " + site.MaxVideoMb + " MB (change this in Settings). Up to 20 files at a time.</p><p class=\"ml-upmsg\" id=\"ml-upmsg\" role=\"status\"></p></section>");
            sb.Append("<div class=\"ml-tools\"><div class=\"ml-chips\" id=\"ml-chips\"><button type=\"button\" class=\"ml-chip ml-act\" data-mk=\"\" onclick=\"AdminMediaJs.libKind('')\">All</button><button type=\"button\" class=\"ml-chip\" data-mk=\"photo\" onclick=\"AdminMediaJs.libKind('photo')\">Photos</button><button type=\"button\" class=\"ml-chip\" data-mk=\"video\" onclick=\"AdminMediaJs.libKind('video')\">Videos</button></div>");
            sb.Append("<input type=\"search\" id=\"ml-mq\" placeholder=\"Search by name or description\" maxlength=\"40\" oninput=\"AdminMediaJs.libType()\" aria-label=\"Search media\"></div>");
            sb.Append("<div id=\"ml-lib\">" + LibHtml(site, media, links, string.Empty, string.Empty, 1) + "</div>");
            return sb.ToString();
        }

        private static string LibHtml(SiteData site, List<MediaItem> media, Links links, string kind, string term, int pn)
        {
            List<MediaItem> rows = media.Where(m => (kind == string.Empty || m.Kind == kind) && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).ToList();
            int photos = media.Count(m => m.Kind == "photo");
            int videos = media.Count(m => m.Kind == "video");
            StringBuilder sb = new StringBuilder();
            sb.Append("<p class=\"ml-hint ml-libcount\">" + Ui.Count(photos, "photo", "photos") + " &middot; " + Ui.Count(videos, "video", "videos") + (rows.Count != media.Count ? " &middot; showing " + rows.Count : string.Empty) + "</p>");
            if (rows.Count == 0)
            {
                return sb.Append("<div class=\"ml-panel ml-empty\">" + (media.Count == 0 ? "The library is empty. Upload your first photo or video." : "Nothing matches.") + "</div>").ToString();
            }
            int pages = (rows.Count + Size - 1) / Size;
            pn = Math.Min(pn, pages);
            sb.Append("<div class=\"ml-grid\">");
            foreach (MediaItem m in rows.Skip((pn - 1) * Size).Take(Size))
            {
                int used = UsedOf(site, links, m.Path).Count;
                string id = Ui.Js(m.Id);
                sb.Append("<article class=\"ml-md\"><button type=\"button\" class=\"ml-md-pv\" onclick=\"AdminMediaJs.edit('" + id + "')\" aria-label=\"Open " + Ui.Esc(m.Name) + "\">");
                sb.Append(m.Kind == "video" ? "<video src=\"" + Ui.Esc(m.Path) + "#t=0.1\" muted preload=\"metadata\" playsinline></video><i class=\"ml-md-play\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"currentColor\"><path d=\"M8 5v14l11-7z\"/></svg></i>" : "<img src=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" alt=\"\" loading=\"lazy\">");
                sb.Append("</button><div class=\"ml-md-t\"><b title=\"" + Ui.Esc(m.Name) + "\">" + Ui.Esc(m.Name) + "</b><small>" + (m.Kind == "video" ? "Video" : "Photo") + " &middot; " + Media.Size(m.Bytes) + (used > 0 ? " &middot; used " + (used == 1 ? "once" : used + " times") : string.Empty) + "</small></div><div class=\"ml-md-a\">");
                sb.Append("<button type=\"button\" class=\"ml-btn ml-btn-ghost ml-btn-sm\" onclick=\"AdminMediaJs.edit('" + id + "')\">Edit</button>");
                sb.Append("<button type=\"button\" class=\"ml-btn ml-btn-danger ml-btn-sm\" onclick=\"AdminMediaJs.drop('" + id + "','" + Ui.Js(m.Name) + "'," + used + ")\">Delete</button>");
                sb.Append("</div></article>");
            }
            sb.Append("</div><div class=\"ml-pager\"><span>Page " + pn + " of " + pages + " &middot; " + Ui.Count(rows.Count, "file", "files") + "</span><div class=\"ml-pgs\"><button type=\"button\" class=\"ml-btn ml-btn-ghost ml-btn-sm\"" + (pn <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminMediaJs.go(" + (pn - 1) + ")\">Previous</button>");
            foreach (int n in Nums(pn, pages))
            {
                sb.Append(n == 0 ? "<i class=\"ml-pgdots\">&hellip;</i>" : "<button type=\"button\" class=\"ml-pgn" + (n == pn ? " ml-pgon" : string.Empty) + "\"" + (n == pn ? " aria-current=\"page\"" : string.Empty) + " onclick=\"AdminMediaJs.go(" + n + ")\">" + n + "</button>");
            }
            sb.Append("<button type=\"button\" class=\"ml-btn ml-btn-ghost ml-btn-sm\"" + (pn >= pages ? " disabled" : string.Empty) + " onclick=\"AdminMediaJs.go(" + (pn + 1) + ")\">Next</button></div></div>");
            return sb.ToString();
        }

        private static List<int> Nums(int pn, int pages)
        {
            List<int> list = new List<int>();
            for (int n = 1; n <= pages; n++)
            {
                if (n == 1 || n == pages || Math.Abs(n - pn) <= 1)
                {
                    list.Add(n);
                }
                else if (list.Count > 0 && list[^1] != 0)
                {
                    list.Add(0);
                }
            }
            return list;
        }

        private static string FormHtml(SiteData site, MediaItem m, List<string> used)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<form class=\"ml-stack\" id=\"ml-form\" onsubmit=\"return AdminMediaJs.save('" + Ui.Js(m.Id) + "')\" novalidate><div class=\"ml-view\">");
            sb.Append(m.Kind == "video" ? "<video src=\"" + Ui.Esc(m.Path) + "\" controls preload=\"metadata\" playsinline></video>" : "<img src=\"" + Ui.Esc(m.Path) + "\" alt=\"" + Ui.Esc(m.Alt) + "\">");
            sb.Append("</div><dl class=\"ml-kv\"><dt>File</dt><dd>" + Ui.Esc(m.Path) + "</dd><dt>Size</dt><dd>" + Media.Size(m.Bytes) + "</dd><dt>Added</dt><dd>" + Ui.Esc(Ui.When(m.Added)) + (m.By.Length > 0 ? " by " + Ui.Esc(m.By) : string.Empty) + "</dd><dt>Used in</dt><dd>" + (used.Count == 0 ? "Not used yet" : string.Join("<br>", used.Select(Ui.Esc))) + "</dd></dl>");
            sb.Append("<label class=\"ml-field\" for=\"ml-f-name\"><span>Name</span><input type=\"text\" id=\"ml-f-name\" data-k=\"name\" maxlength=\"80\" value=\"" + Ui.Esc(m.Name) + "\"><em class=\"ml-err\" id=\"ml-e-name\"></em></label>");
            sb.Append("<label class=\"ml-field\" for=\"ml-f-alt\"><span>Description for screen readers (alt text)</span><input type=\"text\" id=\"ml-f-alt\" data-k=\"alt\" maxlength=\"160\" value=\"" + Ui.Esc(m.Alt) + "\"><small>Say what the picture shows, in a few words.</small><em class=\"ml-err\" id=\"ml-e-alt\"></em></label>");
            sb.Append("<label class=\"ml-field\" for=\"ml-f-caption\"><span>Caption (optional)</span><input type=\"text\" id=\"ml-f-caption\" data-k=\"caption\" maxlength=\"200\" value=\"" + Ui.Esc(m.Caption) + "\"><small>Shown in the corner when this picture is the first slide of the Home slideshow.</small><em class=\"ml-err\" id=\"ml-e-caption\"></em></label>");
            sb.Append("<div class=\"ml-field\"><span>Replace the file</span><label class=\"ml-btn ml-btn-ghost ml-btn-sm ml-upbtn ml-repbtn\"><input type=\"file\" id=\"ml-repfile\" accept=\"" + (m.Kind == "video" ? "video/mp4" : "image/jpeg,image/png,image/webp,image/gif") + "\" onchange=\"AdminMediaJs.replace('" + Ui.Js(m.Id) + "')\">Choose a new " + m.Kind + "</label><small>The new file takes the place of this one on every page, branch, class and inspection that uses it.</small><em class=\"ml-err\" id=\"ml-e-rep\"></em></div>");
            sb.Append("<div class=\"ml-acts\"><button type=\"submit\" class=\"ml-btn\">Save</button><button type=\"button\" class=\"ml-btn ml-btn-ghost\" onclick=\"AdminMediaJs.closeModal()\">Cancel</button>");
            sb.Append("<button type=\"button\" class=\"ml-btn ml-btn-danger ml-push\" onclick=\"AdminMediaJs.drop('" + Ui.Js(m.Id) + "','" + Ui.Js(m.Name) + "'," + used.Count + ")\">Delete file</button>");
            sb.Append("</div></form>");
            return sb.ToString();
        }
    }
}
