using System.Globalization;
using System.Text;
using System.Text.Json;
using CarRental.Core;
using CarRental.Models;
using SkyNet;

namespace CarRental.codes
{
    public class AdminPages : WebPage
    {
                public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Pages | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private const string AreaKey = "pages";

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPagesJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminPagesJs.denied();");
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
            response.ExecuteScript("AdminPagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminPagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("pe-brand", Ui.Esc(site.Brand));
            response.SetElementContents("pe-menu", await Auth.Menu("pe", "AdminPages", c));
            response.SetElementContents("pe-who", Auth.Who("pe", c));
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
            response.SetElementContents("pe-main", MainHtml(c));
            response.ExecuteScript("AdminPagesJs.loaded();");
            return response;
        }

        private static string Opts(IEnumerable<(string Key, string Name)> list, string cur)
        {
            return string.Concat(list.Select(x => "<option value=\"" + Ui.Esc(x.Key) + "\"" + (x.Key == cur ? " selected" : string.Empty) + ">" + Ui.Esc(x.Name) + "</option>"));
        }

        private static string CE(string f, string value, string ph, string cls, bool ro, bool multi = false)
        {
            return "<div class=\"" + cls + (multi ? " pe-ml" : string.Empty) + "\" data-f=\"" + f + "\" contenteditable=\"" + (ro ? "false" : "plaintext-only") + "\" data-ph=\"" + Ui.Esc(ph) + "\"" + (multi ? " data-ml=\"1\"" : string.Empty) + ">" + Ui.Esc(value) + "</div>";
        }

        private static string CI(string f, string value, string ph, bool ro, bool multi)
        {
            return "<div data-i=\"" + f + "\" contenteditable=\"" + (ro ? "false" : "plaintext-only") + "\" data-ph=\"" + Ui.Esc(ph) + "\"" + (multi ? " data-ml=\"1\"" : string.Empty) + ">" + Ui.Esc(value) + "</div>";
        }

        private static string RichEd(string html, string ph, bool ro)
        {
            return "<div class=\"pe-er\" data-f=\"text\" contenteditable=\"" + (ro ? "false" : "true") + "\" data-ph=\"" + Ui.Esc(ph) + "\">" + html + "</div>";
        }

        private static string MediaEd(List<MediaItem> media, string src, string allow, string ph, bool ro)
        {
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            bool vid = Blocks.IsVideo(src);
            string prev = src == string.Empty ? string.Empty : vid ? "<video src=\"" + Ui.Esc(src) + "#t=0.1\" muted preload=\"metadata\" playsinline></video>" : "<img src=\"" + Ui.Esc(m != null && m.Thumb.Length > 0 ? m.Thumb : src) + "\" alt=\"\">";
            return "<div class=\"pe-em\" data-f=\"media\" data-src=\"" + Ui.Esc(src) + "\" data-allow=\"" + allow + "\" data-ph=\"" + Ui.Esc(ph) + "\"><button type=\"button\" class=\"pe-emb\"" + (ro ? " disabled" : " onclick=\"AdminPagesJs.media(this)\"") + " aria-label=\"" + Ui.Esc(ph) + "\">" + (prev != string.Empty ? prev + (vid ? "<span class=\"pe-emv\">Video</span>" : string.Empty) + (ro ? string.Empty : "<span class=\"pe-emc\">Change</span>") : "<span>" + Ui.Esc(ph) + "</span>") + "</button>" + (ro ? string.Empty : "<button type=\"button\" class=\"pe-emx\" onclick=\"AdminPagesJs.noMedia(this)\"" + (prev == string.Empty ? " hidden" : string.Empty) + ">Remove</button>") + "</div>";
        }

        private static string BtnEd(string n, string label, string link, string title, bool ro)
        {
            string r = ro ? " readonly" : string.Empty;
            return "<div class=\"pe-ebtn\"><span>" + title + "</span><input type=\"text\" data-f=\"label" + n + "\" maxlength=\"40\" value=\"" + Ui.Esc(label) + "\" placeholder=\"Button text\"" + r + "><input type=\"text\" data-f=\"link" + n + "\" maxlength=\"300\" value=\"" + Ui.Esc(link) + "\" placeholder=\"Link: Shop, Contact or https://\"" + r + "></div>";
        }

        private static string ItemEd(List<MediaItem> media, Block b, PartSpec spec, BlockItem it, bool ro)
        {
            bool icon = spec.ItemFields.Contains("icon");
            bool link = spec.ItemFields.Contains("link");
            bool pic = spec.ItemFields.Contains("media");
            (string tp, string xp) = b.Type switch
            {
                "steps" => ("Step", "What happens in this step"),
                "stats" => ("Number, like 14", "What it counts"),
                "people" => ("Name", "Role or title"),
                                "faq" => ("Question", "Answer"),
                "cards" => ("Card title", "Short text"),
                _ => ("Title", "Text")
            };
            StringBuilder sb = new StringBuilder("<div class=\"pe-eit\">");
            if (icon)
            {
                sb.Append("<select data-i=\"icon\" aria-label=\"Icon\"" + (ro ? " disabled" : string.Empty) + "><option value=\"\">No icon</option>" + Opts(Blocks.Icons, it.Icon) + "</select>");
            }
            if (pic)
            {
                sb.Append(MediaEd(media, it.Media, "photo", "Add a photo", ro).Replace("data-f=\"media\"", "data-i=\"media\"").Replace("class=\"pe-em\"", "class=\"pe-em pe-emi\""));
            }
            if (b.Type == "hero")
            {
                tp = "Caption, for example Library";
            }
            sb.Append(CI("title", it.Title, tp, ro, false) + (spec.ItemFields.Contains("text") ? CI("text", it.Text, xp, ro, true) : string.Empty));
            if (link)
            {
                sb.Append("<div class=\"pe-elink\"><input type=\"text\" data-i=\"label\" maxlength=\"40\" value=\"" + Ui.Esc(it.Label) + "\" placeholder=\"Link text\"" + (ro ? " readonly" : string.Empty) + "><input type=\"text\" data-i=\"link\" maxlength=\"300\" value=\"" + Ui.Esc(it.Link) + "\" placeholder=\"Link: Shop or https://\"" + (ro ? " readonly" : string.Empty) + "></div>");
            }
            if (!ro)
            {
                sb.Append("<div class=\"pe-ictl\"><button type=\"button\" aria-label=\"Move earlier\" onclick=\"AdminPagesJs.itemMove(this,-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" aria-label=\"Move later\" onclick=\"AdminPagesJs.itemMove(this,1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button><button type=\"button\" aria-label=\"Remove\" onclick=\"AdminPagesJs.itemDrop(this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/></svg></button></div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Ed(List<MediaItem> lib, Block b, bool ro, string page)
        {
            PartSpec? spec = Blocks.Spec(b);
            if (spec == null)
            {
                return string.Empty;
            }
            bool data = b.Type == "data";
            string x = data ? "data" : b.Type;
            StringBuilder sb = new StringBuilder("<div class=\"pe-blk pe-x-" + x + (b.Hidden ? " pe-hid" : string.Empty) + "\" data-id=\"" + Ui.Esc(b.Id) + "\" data-type=\"" + b.Type + "\" data-src=\"" + Ui.Esc(b.Source) + "\" data-hidden=\"" + (b.Hidden ? "1" : "0") + "\"><header class=\"pe-bh\">");
            if (!ro)
            {
                sb.Append("<button type=\"button\" class=\"pe-grip\" aria-label=\"Drag to move\" title=\"Drag to move\" onpointerdown=\"AdminPagesJs.grab(event,this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></button>");
            }
            sb.Append("<b>" + Ui.Esc(spec.Name) + "</b>" + (data ? "<span class=\"pe-src\">From your data</span>" : string.Empty) + "<span class=\"pe-bsp\"></span>");
            if (!ro)
            {
                sb.Append("<button type=\"button\" class=\"pe-hb\" aria-pressed=\"" + (b.Hidden ? "true" : "false") + "\" onclick=\"AdminPagesJs.hide(this)\" title=\"Hide on the website without deleting\"><span>" + (b.Hidden ? "Show" : "Hide") + "</span></button><button type=\"button\" class=\"pe-hb\" aria-label=\"Move up\" title=\"Move up\" onclick=\"AdminPagesJs.move(this,-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" class=\"pe-hb\" aria-label=\"Move down\" title=\"Move down\" onclick=\"AdminPagesJs.move(this,1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button>" + (data ? string.Empty : "<button type=\"button\" class=\"pe-hb pe-del\" aria-label=\"Remove block\" title=\"Remove block\" onclick=\"AdminPagesJs.drop(this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/></svg></button>"));
            }
            sb.Append("</header><div class=\"pe-bb\">");
            if (data)
            {
                sb.Append("<div class=\"pe-dinfo\">" + Ui.Esc(spec.Desc) + "</div>");
            }
            string kick = Blocks.Has(spec, "kicker") ? CE("kicker", b.Kicker, "Small label above the title", "pe-ek", ro) : string.Empty;
            string title = Blocks.Has(spec, "title") ? CE("title", b.Title, "Title", "pe-et", ro) : string.Empty;
            string text = Blocks.Has(spec, "text") ? RichEd(b.Text, "Write here", ro) : string.Empty;
            string media = Blocks.Has(spec, "media") ? MediaEd(lib, b.Media, b.Type == "hero" ? "photo" : string.Empty, b.Type == "hero" ? "Add a photo" : "Add a picture or video", ro) : string.Empty;
            string btns = Blocks.Has(spec, "buttons") ? "<div class=\"pe-ebtns\">" + BtnEd(string.Empty, b.Label, b.Link, "Main button", ro) + BtnEd("2", b.Label2, b.Link2, "Second button", ro) + "</div>" : Blocks.Has(spec, "button") ? "<div class=\"pe-ebtns\">" + BtnEd(string.Empty, b.Label, b.Link, "Button", ro) + "</div>" : string.Empty;
            string extra = string.Empty;
            if (Blocks.Has(spec, "flip"))
            {
                extra = "<label class=\"pe-chk\"><input type=\"checkbox\" data-f=\"flip\"" + (b.Flip ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + " onchange=\"this.closest('.pe-bb').querySelector('.pe-two').classList.toggle('pe-flip',this.checked)\"> Picture on the right</label>";
            }
            switch (b.Type)
            {
                case "hero":
                    sb.Append("<div class=\"pe-two\"><div class=\"pe-col\">" + kick + title + text + btns + "</div>" + media + "</div>");
                    break;
                case "split":
                    sb.Append("<div class=\"pe-two" + (b.Flip ? " pe-flip" : string.Empty) + "\">" + media + "<div class=\"pe-col\">" + kick + title + text + btns + "</div></div>" + extra);
                    break;
                case "media":
                    sb.Append(media + CE("text", b.Text, "Caption (optional)", "pe-hint2", ro));
                    break;
                default:
                    sb.Append(kick + title + text + btns);
                    break;
            }
            if (Blocks.Has(spec, "items") && (b.Type != "hero" || page == "home"))
            {
                sb.Append(b.Type == "hero" ? "<div class=\"pe-slides\"><b>Slides</b><p class=\"pe-hint2\">The picture above is the first slide. Add more pictures and they change every 7 seconds. The caption shows in the corner of each picture.</p><div class=\"pe-eitems pe-sitems\" data-f=\"items\">" : "<div><div class=\"pe-eitems\" data-f=\"items\">");
                foreach (BlockItem it in b.Items)
                {
                    sb.Append(ItemEd(lib, b, spec, it, ro));
                }
                sb.Append("</div>" + (ro ? string.Empty : "<button type=\"button\" class=\"pe-eadd\" onclick=\"AdminPagesJs.addItem(this)\">+ Add " + (b.Type switch { "steps" => "a step", "faq" => "a question", "cards" => "a card", "stats" => "a number", "people" => "a person", "hero" => "a slide", _ => "a line" }) + "</button><template>" + ItemEd(lib, b, spec, new BlockItem(), false) + "</template>") + "</div>" + (b.Type == "hero" ? "</div>" : string.Empty));
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static string Tabs(string cur)
        {
            return string.Concat(Blocks.Pages.Select(p => "<button type=\"button\" class=\"pe-chip" + (p.Key == cur ? " pe-act" : string.Empty) + "\" aria-pressed=\"" + (p.Key == cur ? "true" : "false") + "\" onclick=\"AdminPagesJs.tab('" + p.Key + "')\">" + Ui.Esc(p.Name) + "</button>"));
        }

        private static string MainHtml(Ctx c)
        {
            bool canE = Can(c, 'E');
            return "<div class=\"pe-ph\"><div><h1>Pages</h1><p>" + (canE ? "Click any text to change it. Drag a block by its handle, or use the arrows, to move it. Use {name} to show the company name from Settings. Changes go live when you save." : "You can look at the pages but not change them.") + "</p></div></div><div class=\"pe-ptools\"><div class=\"pe-ptabs\" id=\"pe-ptabs\">" + Tabs("home") + "</div><div class=\"pe-pacts\"><span class=\"pe-dirty\" id=\"pe-dirty\"></span><button type=\"button\" class=\"pe-btn pe-btn-sm pe-btn-ghost\" onclick=\"AdminPagesJs.versions()\">Versions</button><button type=\"button\" class=\"pe-btn pe-btn-sm pe-btn-ghost\" id=\"pe-viewlink\" data-href=\"Home\" onclick=\"AdminPagesJs.view()\">View page</button>" + (canE ? "<button type=\"button\" class=\"pe-btn pe-btn-sm\" id=\"pe-savebtn\" onclick=\"AdminPagesJs.save()\">Save page</button>" : string.Empty) + "</div></div><div class=\"pe-canvas\" id=\"pe-canvas\"></div><div id=\"pe-holder\" hidden></div><div class=\"pe-rt\" id=\"pe-rt\" role=\"toolbar\" aria-label=\"Text style\"><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('bold')\" title=\"Bold\"><b>B</b></button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('italic')\" title=\"Italic\"><i>I</i></button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('link')\" title=\"Link\">Link</button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('insertUnorderedList')\" title=\"List\">&bull; List</button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"AdminPagesJs.fmt('clear')\" title=\"Clear formatting\">Clear</button></div>";
        }

        private static string Canvas(SiteData site, List<MediaItem> media, PageSpec p, bool canE)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Block b in site.Pages[p.Key].Blocks)
            {
                sb.Append(Ed(media, b, !canE, p.Key));
            }
            if (canE)
            {
                sb.Append("<div class=\"pe-addp\" id=\"pe-addp\"><b>Add a block</b><div class=\"pe-addl\">" + string.Concat(Blocks.Types.Select(t => "<button type=\"button\" onclick=\"AdminPagesJs.addBlock('" + t.Key + "')\"><b>" + Ui.Esc(t.Name) + "</b><small>" + Ui.Esc(t.Desc) + "</small></button>")) + "</div><p class=\"pe-hint2\">Blocks marked From your data (search box, classes, branches, forms) cannot be removed, but you can move them, change their titles or hide them.</p></div>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Show()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            PageSpec p = Blocks.PageOf(GetDataValue("page")) ?? Blocks.Pages[0];
            bool canE = Can(c, 'E');
            response.SetElementContents("pe-canvas", Canvas(site, media, p, canE));
            response.SetElementContents("pe-ptabs", Tabs(p.Key));
            response.ExecuteScript("AdminPagesJs.shown('" + p.Key + "','" + p.Href + "');");
            return response;
        }

        public async Task<ApiResponse> AddBlock()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Deny(response, c, "add block", "You can look at the pages but not change them.");
            }
            PartSpec? t = Blocks.Types.FirstOrDefault(x => x.Key == GetDataValue("btype"));
            if (t == null)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            Block b = new Block { Id = Blocks.NewId(), Type = t.Key };
            if (t.Key is "cards" or "steps" or "faq" or "stats" or "people")
            {
                b.Items.Add(new BlockItem());
                b.Items.Add(new BlockItem());
                b.Items.Add(new BlockItem());
            }
            response.SetElementContents("pe-holder", Ed(media, b, false, Blocks.PageOf(GetDataValue("page"))?.Key ?? "home"));
            response.ExecuteScript("AdminPagesJs.added();");
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
                return await Deny(response, c, "save page", "You can look at the pages but not change them.");
            }
            PageSpec? p = Blocks.PageOf(GetDataValue("page"));
            if (p == null)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            string err = string.Empty;
            SiteData? done = null;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                (List<Block>? list, string e) = Blocks.Clean(media, p, GetDataValue("blocks"));
                if (list == null)
                {
                    err = e;
                }
                else
                {
                    PageDoc doc = site.Pages[p.Key];
                    doc.Versions.Insert(0, new PageVersion { When = doc.Saved == default ? DateTime.UtcNow.AddMinutes(-1) : doc.Saved, By = doc.By, Blocks = doc.Blocks });
                    if (doc.Versions.Count > 5)
                    {
                        doc.Versions.RemoveRange(5, doc.Versions.Count - 5);
                    }
                    doc.Blocks = list;
                    doc.Saved = DateTime.UtcNow;
                    doc.By = c.U.Id;
                    await Store.SaveSite(site);
                    done = site;
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminPagesJs.fail('" + Ui.Js(err) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "save page", p.Name, true);
            response.SetElementContents("pe-canvas", Canvas(done, media, p, true));
            response.ExecuteScript("AdminPagesJs.shown('" + p.Key + "','" + p.Href + "');AdminPagesJs.saved('" + Ui.Js(p.Name + " saved. It is live on the website.") + "');");
            return response;
        }

        public async Task<ApiResponse> Versions()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            PageSpec? p = Blocks.PageOf(GetDataValue("page"));
            if (p == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            PageDoc doc = site.Pages[p.Key];
            string byName = doc.By == string.Empty ? string.Empty : await Store.Who(doc.By);
            Dictionary<string, string> names = new Dictionary<string, string>();
            foreach (string id in doc.Versions.Select(v => v.By).Where(x => x != string.Empty).Distinct())
            {
                names[id] = await Store.Who(id);
            }
            string Who(string id) => id == doc.By ? byName : names.TryGetValue(id, out string? n) ? n : id;
            bool canE = true;
            StringBuilder sb = new StringBuilder("<p class=\"pe-hint\">The last five saved versions are kept. Restoring one makes it live right away.</p><div class=\"pe-vlist\"><div class=\"pe-vrow\"><div><b>Live now</b><small>" + (doc.Saved != default ? "Saved " + Ui.Esc(Ui.When(doc.Saved)) + (doc.By != string.Empty ? " by " + Ui.Esc(Who(doc.By)) : string.Empty) : "Sample content") + " &middot; " + Ui.Count(doc.Blocks.Count, "block", "blocks") + "</small></div></div>");
            if (doc.Versions.Count == 0)
            {
                sb.Append("<p class=\"pe-hint\">No earlier versions yet. They appear here after you save.</p>");
            }
            for (int i = 0; i < doc.Versions.Count; i++)
            {
                PageVersion v = doc.Versions[i];
                string when = Ui.When(v.When);
                sb.Append("<div class=\"pe-vrow\"><div><b>" + Ui.Esc(when) + "</b><small>" + (v.By != string.Empty ? Ui.Esc(Who(v.By)) + " &middot; " : string.Empty) + Ui.Count(v.Blocks.Count, "block", "blocks") + "</small></div>" + (canE ? "<button type=\"button\" class=\"pe-btn pe-btn-sm pe-btn-ghost\" onclick=\"AdminPagesJs.restore(" + i + ",'" + Ui.Js(when) + "')\">Restore</button>" : string.Empty) + "</div>");
            }
            sb.Append("</div><div class=\"pe-acts\"><button type=\"button\" class=\"pe-btn pe-btn-ghost\" onclick=\"AdminPagesJs.closeModal()\">Close</button></div>");
            response.SetElementContents("pe-mbody", sb.ToString());
            response.ExecuteScript("AdminPagesJs.opened('" + Ui.Js(p.Name + ": versions") + "');");
            return response;
        }

        public async Task<ApiResponse> Restore()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Deny(response, c, "restore page", "You can look at the pages but not change them.");
            }
            PageSpec? p = Blocks.PageOf(GetDataValue("page"));
            int i = int.TryParse(GetDataValue("vi"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv) ? iv : -1;
            SiteData? done = null;
            if (p != null)
            {
                using (await Store.Hold())
                {
                    SiteData site = await Store.LoadSite();
                    PageDoc doc = site.Pages[p.Key];
                    if (i >= 0 && i < doc.Versions.Count)
                    {
                        PageVersion v = doc.Versions[i];
                        doc.Versions.RemoveAt(i);
                        doc.Versions.Insert(0, new PageVersion { When = doc.Saved == default ? DateTime.UtcNow.AddMinutes(-1) : doc.Saved, By = doc.By, Blocks = doc.Blocks });
                        if (doc.Versions.Count > 5)
                        {
                            doc.Versions.RemoveRange(5, doc.Versions.Count - 5);
                        }
                        doc.Blocks = v.Blocks;
                        doc.Saved = DateTime.UtcNow;
                        doc.By = c.U.Id;
                        Blocks.Ensure(site);
                        await Store.SaveSite(site);
                        done = site;
                    }
                }
            }
            if (p == null || done == null)
            {
                return response;
            }
            await Store.Log(c.U.Id, "restore page", p.Name, true);
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("pe-canvas", Canvas(done, media, p, true));
            response.ExecuteScript("AdminPagesJs.shown('" + p.Key + "','" + p.Href + "');AdminPagesJs.saved('Version restored. It is live on the website.');");
            return response;
        }

        private static string PickItem(MediaItem m)
        {
            bool vid = m.Kind == "video";
            string thumb = m.Thumb.Length > 0 ? m.Thumb : m.Path;
            return "<button type=\"button\" class=\"pe-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(thumb) + "\" data-kind=\"" + m.Kind + "\" data-title=\"" + Ui.Esc(m.Name) + "\" onclick=\"AdminPagesJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\">" + (vid ? "<video src=\"" + Ui.Esc(m.Path) + "#t=0.1\" muted preload=\"metadata\" playsinline></video><b class=\"pe-vbadge\">Video</b>" : "<img src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\">") + "<span>" + Ui.Esc(m.Name) + "</span></button>";
        }

        private static string PickGrid(List<MediaItem> media, string allow, string kind, string term, int page)
        {
            string k = allow != string.Empty ? allow : kind;
            List<MediaItem> rows = media.Where(m => (k == string.Empty || m.Kind == k) && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"pe-none\">Nothing matches. Upload a photo or video above, or add them in the Media library.</p>";
            }
            int pages = (rows.Count + PickSize - 1) / PickSize;
            page = Math.Min(Math.Max(page, 1), pages);
            string pager = pages <= 1 ? string.Empty : "<div class=\"pe-pkpager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(rows.Count, "file", "files") + "</span><div><button type=\"button\" class=\"pe-btn pe-btn-ghost pe-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminPagesJs.pkList(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"pe-btn pe-btn-ghost pe-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminPagesJs.pkList(" + (page + 1) + ")\">Next</button></div></div>";
            return string.Concat(rows.Skip((page - 1) * PickSize).Take(PickSize).Select(PickItem)) + pager;
        }

        private const int PickSize = 12;

        private static int PickPage(string? v)
        {
            return int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            string allow = GetDataValue("pallow") == "photo" ? "photo" : string.Empty;
            string kind = GetDataValue("pkind") is "photo" or "video" ? GetDataValue("pkind") : string.Empty;
            response.SetElementContents("pe-pkgrid", PickGrid(media, allow, kind, Ui.Clip(GetDataValue("pterm"), 40), PickPage(GetDataValue("ppg"))));
            return response;
        }

        public async Task<ApiResponse> MediaUp()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E') || !Access.Can(c.U, "media", 'A'))
            {
                response.ExecuteScript("AdminPagesJs.pkUploaded(false,'You do not have the right to add files to the media library.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.ExecuteScript("AdminPagesJs.pkUploaded(false,'Choose a photo or video first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files, site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            string allow = GetDataValue("pallow") == "photo" ? "photo" : string.Empty;
            response.SetElementContents("pe-pkgrid", PickGrid(media, allow, string.Empty, string.Empty, 1));
            response.ExecuteScript("AdminPagesJs.pkUploaded(" + (added > 0 ? "true" : "false") + ",'" + Ui.Js(((added > 0 ? Ui.Count(added, "file", "files") + " added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
            return response;
        }
    }
}
