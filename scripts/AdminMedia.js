var AdminMediaJs = (function () {

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
        $ApiRequest('AdminMedia/' + method, JSON.stringify(list));
    }

    function expired() {
        document.cookie = 'br_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function denied() {
        location.href = 'Admin';
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
        var t = el('ml-toast');
        t.textContent = msg;
        t.classList.add('ml-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ml-open');
        }, 3400);
    }

    function openMenu() {
        el('ml-rail').classList.add('ml-open');
        el('ml-scrim').classList.add('ml-open');
    }

    function closeMenu() {
        el('ml-rail').classList.remove('ml-open');
        el('ml-scrim').classList.remove('ml-open');
    }

    function openModal(title) {
        var m = el('ml-modal');
        el('ml-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('ml-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('ml-modal');
        if (m) {
            m.classList.remove('ml-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('ml-modal');
        if (!m || !m.classList.contains('ml-open')) {
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
        if (!askBox || !askBox.classList.contains('ml-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('ml-open');
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
        askBox.className = 'ml-ask';
        askBox.innerHTML = '<div class="ml-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="ml-err" role="alert"></em><div class="ml-askacts"><button type="button" class="ml-btn ml-btn-ghost"></button><button type="button" class="ml-btn"></button></div></div>';
        document.getElementById('ml-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        on(window, 'keydown', function (e) {
            if (!askBox.classList.contains('ml-open')) {
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
        x.b[1].className = 'ml-btn' + (o.danger ? ' ml-askdel' : '');
        askBox.firstChild.classList.toggle('ml-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('ml-open');
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
            } else if (n.type === 'password') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.value });
            } else {
                list.push({ key: n.getAttribute('data-k'), vlu: (n.value || '').trim() });
            }
        });
        return list;
    }

    var mk = '';
    var mp = 1;
    var mtimer = null;

    function filters() {
        return [{ key: 'mk', vlu: mk }, { key: 'mq', vlu: val('ml-mq') }, { key: 'mp', vlu: String(mp) }];
    }

    function lib() {
        api('Lib', filters());
    }

    function libKind(k) {
        mk = k;
        mp = 1;
        document.querySelectorAll('#ml-chips .ml-chip').forEach(function (b) {
            b.classList.toggle('ml-act', b.getAttribute('data-mk') === k);
        });
        lib();
    }

    function libType() {
        clearTimeout(mtimer);
        mp = 1;
        mtimer = setTimeout(lib, 250);
    }

    function go(n) {
        mp = n;
        lib();
    }

    function upload() {
        mp = 1;
        var msg = el('ml-upmsg');
        if (msg) {
            msg.textContent = 'Uploading. Large videos can take a minute.';
        }
        api('Upload', filters());
    }

    function uploaded() {
        document.querySelectorAll('#ml-site-root input[type="file"]').forEach(function (f) {
            f.value = '';
        });
    }

    function replace(id) {
        var e = el('ml-e-rep');
        if (e) {
            e.textContent = 'Uploading.';
        }
        api('Replace', filters().concat([{ key: 'id', vlu: id }]));
    }

    function replaced(ok, msg) {
        document.querySelectorAll('#ml-site-root input[type="file"]').forEach(function (f) {
            f.value = '';
        });
        if (ok) {
            clean();
            shut();
            toast(msg);
            return;
        }
        var e = el('ml-e-rep');
        if (e) {
            e.textContent = msg;
        }
    }

    function opened(title) {
        openModal(title);
    }

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function save(id) {
        var list = collect('ml-form', [{ key: 'id', vlu: id }]);
        list.push.apply(list, filters());
        api('Save', list);
        return false;
    }

    function saved() {
        clean();
        shut();
        toast('Saved');
    }

    function drop(id, name, used) {
        if (used > 0) {
            toast(name + ' is used on the website. Replace it there before deleting it.');
            return;
        }
        ask({ title: 'Delete ' + name + '?', text: 'The file is removed from the library and the server for good.', ok: 'Delete file', no: 'Keep it', danger: true }, function () {
            var list = filters();
            list.push({ key: 'id', vlu: id });
            api('Drop', list);
        });
    }

    function dropped() {
        clean();
        shut();
        toast('File deleted');
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
                return c.indexOf('ml-') === 0 ? np + '-' + c.slice(3) : c;
            }).join(' ');
        });
    }

    function carry(id, doc, np, page) {
        var from = el('ml-' + id), to = doc.getElementById(np + '-' + id);
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
            var rail = el('ml-rail');
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
            [].forEach.call(el('ml-menu').querySelectorAll('a'), function (n) {
                n.classList.toggle('ml-on', n === a);
            });
            navTo(href, true);
        });
    }

    function navInit() {
        var m = el('ml-menu');
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
                if (askBox && askBox.classList.contains('ml-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('ml-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
        api('Load');
    }

    return {
        reveal: reveal,
        expired: expired,
        denied: denied,
        signout: signout,
        toast: toast,
        openMenu: openMenu,
        closeMenu: closeMenu,
        openModal: openModal,
        closeModal: closeModal,
        shut: shut,
        clean: clean,
        libKind: libKind,
        libType: libType,
        go: go,
        upload: upload,
        uploaded: uploaded,
        replace: replace,
        replaced: replaced,
        opened: opened,
        edit: edit,
        save: save,
        saved: saved,
        drop: drop,
        dropped: dropped
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminMediaJs.reveal);
} else {
    AdminMediaJs.reveal();
}
