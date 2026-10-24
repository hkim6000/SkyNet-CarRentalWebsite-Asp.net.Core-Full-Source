var AdminRepairsJs = (function () {

    var ttimer = null;
    var askBox = null, askDone = null, askCheck = null, askBack = null;
    var dirty = false;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function getCookie(name) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('br_tok') });
        $ApiRequest('AdminRepairs/' + method, JSON.stringify(list));
    }

    function expired() {
        document.cookie = 'br_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function signout() {
        var t = getCookie('br_tok');
        document.cookie = 'br_tok=; path=' + base() + '; max-age=0';
        setTimeout(function () {
            location.href = 'AdminLogin';
        }, 1500);
        $ApiRequest('AdminLogin/SignOut', JSON.stringify([{ key: 'tok', vlu: t }]));
    }

    function toast(msg) {
        var t = el('rp-toast');
        t.textContent = msg;
        t.classList.add('rp-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rp-open');
        }, 3400);
    }

    function openMenu() {
        el('rp-rail').classList.add('rp-open');
        el('rp-scrim').classList.add('rp-open');
    }

    function closeMenu() {
        el('rp-rail').classList.remove('rp-open');
        el('rp-scrim').classList.remove('rp-open');
    }

    function openModal(title) {
        var m = el('rp-modal');
        el('rp-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('rp-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('rp-modal');
        if (m) {
            m.classList.remove('rp-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('rp-modal');
        if (!m || !m.classList.contains('rp-open')) {
            return;
        }
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, shut);
            return;
        }
        shut();
    }

    function markDirty() {
        dirty = true;
    }

    function clean() {
        dirty = false;
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('rp-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('rp-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        var m = askCheck ? askCheck(x.i.value.trim()) : '';
        if (m) {
            x.e.textContent = m;
            x.i.focus();
            return;
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'rp-ask';
        askBox.innerHTML = '<div class="rp-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="rp-err" role="alert"></em><div class="rp-askacts"><button type="button" class="rp-btn rp-btn-ghost"></button><button type="button" class="rp-btn"></button></div></div>';
        document.getElementById('rp-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        on(window, 'keydown', function (e) {
            if (!askBox.classList.contains('rp-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) { return n.offsetParent !== null; });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts(), input = typeof o.value === 'string';
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'rp-btn' + (o.danger ? ' rp-askdel' : '');
        askBox.firstChild.classList.toggle('rp-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('rp-open');
        if (input) {
            x.i.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function collect(root, extra) {
        var list = extra || [];
        document.querySelectorAll('#' + root + ' [data-k]').forEach(function (n) {
            if (n.type === 'checkbox') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.checked ? '1' : '0' });
            } else if (n.hasAttribute('data-src')) {
                list.push({ key: n.getAttribute('data-k'), vlu: n.getAttribute('data-src') || '' });
            } else if (n.type === 'password') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.value });
            } else {
                list.push({ key: n.getAttribute('data-k'), vlu: (n.value || '').trim() });
            }
        });
        return list;
    }


    var pkDone = null;
    var pkTimer = null;

    function pkList() {
        api('MediaPick', [{ key: 'pkind', vlu: 'photo' }, { key: 'pterm', vlu: val('rp-pkq') }]);
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(pkList, 300);
    }

    function pkOpen() {
        var d = el('rp-pk');
        return d && d.classList.contains('rp-open');
    }

    function pkClose() {
        var d = el('rp-pk');
        if (d) {
            d.classList.remove('rp-open');
            d.setAttribute('aria-hidden', 'true');
        }
        el('rp-pkfile').value = '';
        pkDone = null;
    }

    function pickMedia(done) {
        pkDone = done;
        el('rp-pkq').value = '';
        el('rp-pkmsg').textContent = '';
        var d = el('rp-pk');
        d.classList.add('rp-open');
        d.setAttribute('aria-hidden', 'false');
        pkList();
        setTimeout(function () {
            el('rp-pkq').focus();
        }, 30);
    }

    function pkPick(b) {
        var f = pkDone;
        var it = { src: b.getAttribute('data-src'), thumb: b.getAttribute('data-thumb') };
        pkClose();
        if (f) {
            f(it);
        }
    }

    function pkUpload() {
        el('rp-pkmsg').textContent = 'Uploading.';
        api('MediaUp');
    }

    function pkUploaded(ok, msg) {
        el('rp-pkfile').value = '';
        el('rp-pkmsg').textContent = msg;
        if (ok) {
            el('rp-pkq').value = '';
        }
    }

    function paintPic(box, it) {
        var b = box.querySelector('.rp-picb');
        box.setAttribute('data-src', it ? it.src : '');
        b.innerHTML = it ? '<img src="' + esc(it.thumb) + '" alt="">' : '<span>' + esc(box.getAttribute('data-ph') || 'Choose a picture') + '</span>';
        var x = box.querySelector('.rp-picx');
        if (x) {
            x.hidden = !it;
        }
    }

    function pic(btn) {
        var box = btn.closest('.rp-pic');
        pickMedia(function (it) {
            paintPic(box, it);
            markDirty();
        });
    }

    function picClear(btn) {
        paintPic(btn.closest('.rp-pic'), null);
        markDirty();
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }



    function denied() {
        location.href = 'Admin';
    }

    function qs(k) {
        var m = location.search.match(new RegExp('[?&]' + k + '=([^&]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function extra() {
        return [{ key: 'fs', vlu: val('rp-fs') }, { key: 'fb', vlu: val('rp-fb') }];
    }

    function first() {
        return [{ key: 'car', vlu: qs('car') }];
    }


    function save() {
        api('Save', collect('rp-form', [{ key: 'id', vlu: val('rp-f-id') }]).concat(filters()));
        return false;
    }

    function act(what) {
        api('Act', collect('rp-form', [{ key: 'id', vlu: val('rp-f-id') }, { key: 'what', vlu: what }]).concat(filters()));
    }

    function cancelIt() {
        ask({ title: 'Cancel this repair?', text: 'The repair is closed without a cost. The car goes back to available unless it has another open repair.', ok: 'Cancel repair', no: 'Keep it', danger: true }, function () {
            act('cancel');
        });
    }

    var own = { save: save, act: act, cancelIt: cancelIt };

    var pg = 1;
    var mtimer = null;

    function filters() {
        return [{ key: 'term', vlu: val('rp-q') }, { key: 'pg', vlu: String(pg) }].concat(extra());
    }

    function list(reset) {
        if (reset) {
            pg = 1;
        }
        api('List', filters());
    }

    function type() {
        clearTimeout(mtimer);
        mtimer = setTimeout(function () {
            list(true);
        }, 250);
    }

    function go(n) {
        pg = n;
        list(false);
    }

    function opened(title) {
        openModal(title);
    }

    function saved() {
        clean();
        shut();
        toast('Saved');
    }

    function add() {
        api('Edit', [{ key: 'id', vlu: '' }]);
    }

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function deleted() {
        clean();
        shut();
        toast('Deleted');
    }

    var offs = [];
    var here = '';
    var navBusy = false;

    function on(t, type, fn, opt) {
        t.addEventListener(type, fn, opt);
        offs.push([t, type, fn, opt]);
    }

    function leave() {
        offs.forEach(function (x) {
            x[0].removeEventListener(x[1], x[2], x[3]);
        });
        offs = [];
        clearTimeout(ttimer);
        if (typeof mtimer !== 'undefined') {
            clearTimeout(mtimer);
        }
        if (typeof pkTimer !== 'undefined') {
            clearTimeout(pkTimer);
        }
    }

    function leaveOk(go) {
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Stay', danger: true }, function () {
                dirty = false;
                go();
            });
            return;
        }
        go();
    }

    function abs(u) {
        return new URL(u, location.href).href;
    }

    function swapClass(root, np) {
        [].forEach.call(root.querySelectorAll('[class]'), function (n) {
            n.className = n.className.split(/\s+/).map(function (c) {
                return c.indexOf('rp-') === 0 ? np + '-' + c.slice(3) : c;
            }).join(' ');
        });
    }

    function carry(id, doc, np, page) {
        var from = el('rp-' + id), to = doc.getElementById(np + '-' + id);
        if (!from || !to || !from.innerHTML) {
            return;
        }
        to.innerHTML = from.innerHTML;
        swapClass(to, np);
        if (page) {
            [].forEach.call(to.querySelectorAll('a'), function (a) {
                a.classList.toggle(np + '-on', a.getAttribute('href') === page);
            });
        }
    }

    function navTo(href, push) {
        if (navBusy) {
            return;
        }
        navBusy = true;
        var page = href.split('?')[0];
        fetch(href, { credentials: 'same-origin' }).then(function (r) {
            if (!r.ok || /AdminLogin/.test(r.url)) {
                throw new Error('page');
            }
            return r.text();
        }).then(function (html) {
            var doc = new DOMParser().parseFromString(html, 'text/html');
            var root = doc.querySelector('[id$="-site-root"]');
            if (!root) {
                throw new Error('page');
            }
            var np = root.id.replace(/-site-root$/, '');
            var have = [].map.call(document.querySelectorAll('link[rel="stylesheet"]'), function (l) {
                return abs(l.getAttribute('href'));
            });
            var want = [].map.call(doc.querySelectorAll('link[rel="stylesheet"]'), function (l) {
                return l.getAttribute('href');
            });
            var loads = want.filter(function (h) {
                return have.indexOf(abs(h)) < 0;
            }).map(function (h) {
                return new Promise(function (ok) {
                    var n = document.createElement('link');
                    n.rel = 'stylesheet';
                    n.href = h;
                    n.onload = n.onerror = function () {
                        ok();
                    };
                    setTimeout(ok, 4000);
                    document.head.appendChild(n);
                });
            });
            return Promise.all(loads).then(function () {
                return { doc: doc, np: np, want: want.map(abs) };
            });
        }).then(function (x) {
            var doc = x.doc, np = x.np;
            var srcs = [].map.call(doc.querySelectorAll('script[src]'), function (n) {
                return n.getAttribute('src');
            });
            var rail = el('rp-rail');
            var top = rail ? rail.scrollTop : 0;
            carry('menu', doc, np, page);
            carry('who', doc, np, '');
            carry('brand', doc, np, '');
            leave();
            closeMenu();
            if (push) {
                history.pushState({ nav: 1 }, '', href);
            }
            document.title = doc.title;
            document.body.className = doc.body.className;
            [].forEach.call(doc.body.querySelectorAll('script'), function (s) {
                s.parentNode.removeChild(s);
            });
            document.body.innerHTML = doc.body.innerHTML;
            var r2 = document.getElementById(np + '-rail');
            if (r2) {
                r2.scrollTop = top;
            }
            window.scrollTo(0, 0);
            [].forEach.call(document.querySelectorAll('link[rel="stylesheet"]'), function (l) {
                if (x.want.indexOf(abs(l.getAttribute('href'))) < 0) {
                    l.parentNode.removeChild(l);
                }
            });
            var loaded = [].map.call(document.querySelectorAll('script[src]'), function (s) {
                return abs(s.getAttribute('src'));
            });
            srcs.forEach(function (src) {
                var mine = /(^|\/)scripts\/Admin[A-Za-z]*\.js/.test(src);
                if (!mine && loaded.indexOf(abs(src)) >= 0) {
                    return;
                }
                if (mine) {
                    [].forEach.call(document.querySelectorAll('script[src]'), function (o) {
                        if (/(^|\/)scripts\/Admin[A-Za-z]*\.js/.test(o.getAttribute('src'))) {
                            o.parentNode.removeChild(o);
                        }
                    });
                }
                var n = document.createElement('script');
                n.src = src;
                document.head.appendChild(n);
            });
        }).catch(function () {
            location.href = href;
        });
    }

    function menuClick(e) {
        var a = e.target.closest ? e.target.closest('a') : null;
        if (!a || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey || a.target) {
            return;
        }
        var href = a.getAttribute('href') || '';
        if (!/^Admin[A-Za-z]*$/.test(href) || !window.fetch || !window.DOMParser || !history.pushState) {
            return;
        }
        e.preventDefault();
        leaveOk(function () {
            [].forEach.call(el('rp-menu').querySelectorAll('a'), function (n) {
                n.classList.toggle('rp-on', n === a);
            });
            navTo(href, true);
        });
    }

    function navInit() {
        var m = el('rp-menu');
        if (m) {
            m.addEventListener('click', menuClick);
        }
        here = location.pathname + location.search;
        if (!history.state || !history.state.nav) {
            history.replaceState({ nav: 1 }, '', location.href);
        }
        on(window, 'popstate', function () {
            if (location.pathname + location.search === here) {
                return;
            }
            navTo(location.pathname.replace(/^.*\//, '') + location.search, false);
        });
    }

    function reveal() {
        navInit();
        on(document, 'keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('rp-open')) {
                    return;
                }
                if (pkOpen()) {
                    pkClose();
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('rp-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
        var d = el('rp-pk');
        if (d) {
            d.addEventListener('mousedown', function (e) {
                if (e.target === d) {
                    pkClose();
                }
            });
        }
        api('Load', first());
    }

    var base0 = {
        reveal: reveal,
        expired: expired,
        signout: signout,
        denied: denied,
        toast: toast,
        openMenu: openMenu,
        closeMenu: closeMenu,
        openModal: openModal,
        closeModal: closeModal,
        shut: shut,
        clean: clean,
        pic: pic,
        picClear: picClear,
        pkList: pkList,
        pkSearch: pkSearch,
        pkPick: pkPick,
        pkClose: pkClose,
        pkUpload: pkUpload,
        pkUploaded: pkUploaded,
        list: list,
        type: type,
        go: go,
        opened: opened,
        saved: saved,
        add: add,
        edit: edit,
        deleted: deleted
    };
    Object.keys(own).forEach(function (k) {
        base0[k] = own[k];
    });
    return base0;
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminRepairsJs.reveal);
} else {
    AdminRepairsJs.reveal();
}
