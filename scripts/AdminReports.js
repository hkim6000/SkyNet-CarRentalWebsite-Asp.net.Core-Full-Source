var AdminReportsJs = (function () {

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
        $ApiRequest('AdminReports/' + method, JSON.stringify(list));
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
        var t = el('rt-toast');
        t.textContent = msg;
        t.classList.add('rt-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rt-open');
        }, 3400);
    }

    function openMenu() {
        el('rt-rail').classList.add('rt-open');
        el('rt-scrim').classList.add('rt-open');
    }

    function closeMenu() {
        el('rt-rail').classList.remove('rt-open');
        el('rt-scrim').classList.remove('rt-open');
    }

    function openModal(title) {
        var m = el('rt-modal');
        el('rt-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('rt-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('rt-modal');
        if (m) {
            m.classList.remove('rt-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('rt-modal');
        if (!m || !m.classList.contains('rt-open')) {
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
        if (!askBox || !askBox.classList.contains('rt-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('rt-open');
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
        askBox.className = 'rt-ask';
        askBox.innerHTML = '<div class="rt-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="rt-err" role="alert"></em><div class="rt-askacts"><button type="button" class="rt-btn rt-btn-ghost"></button><button type="button" class="rt-btn"></button></div></div>';
        document.getElementById('rt-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        on(window, 'keydown', function (e) {
            if (!askBox.classList.contains('rt-open')) {
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
        x.b[1].className = 'rt-btn' + (o.danger ? ' rt-askdel' : '');
        askBox.firstChild.classList.toggle('rt-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('rt-open');
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
        api('MediaPick', [{ key: 'pkind', vlu: 'photo' }, { key: 'pterm', vlu: val('rt-pkq') }]);
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(pkList, 300);
    }

    function pkOpen() {
        var d = el('rt-pk');
        return d && d.classList.contains('rt-open');
    }

    function pkClose() {
        var d = el('rt-pk');
        if (d) {
            d.classList.remove('rt-open');
            d.setAttribute('aria-hidden', 'true');
        }
        el('rt-pkfile').value = '';
        pkDone = null;
    }

    function pickMedia(done) {
        pkDone = done;
        el('rt-pkq').value = '';
        el('rt-pkmsg').textContent = '';
        var d = el('rt-pk');
        d.classList.add('rt-open');
        d.setAttribute('aria-hidden', 'false');
        pkList();
        setTimeout(function () {
            el('rt-pkq').focus();
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
        el('rt-pkmsg').textContent = 'Uploading.';
        api('MediaUp');
    }

    function pkUploaded(ok, msg) {
        el('rt-pkfile').value = '';
        el('rt-pkmsg').textContent = msg;
        if (ok) {
            el('rt-pkq').value = '';
        }
    }

    function paintPic(box, it) {
        var b = box.querySelector('.rt-picb');
        box.setAttribute('data-src', it ? it.src : '');
        b.innerHTML = it ? '<img src="' + esc(it.thumb) + '" alt="">' : '<span>' + esc(box.getAttribute('data-ph') || 'Choose a picture') + '</span>';
        var x = box.querySelector('.rt-picx');
        if (x) {
            x.hidden = !it;
        }
    }

    function pic(btn) {
        var box = btn.closest('.rt-pic');
        pickMedia(function (it) {
            paintPic(box, it);
            markDirty();
        });
    }

    function picClear(btn) {
        paintPic(btn.closest('.rt-pic'), null);
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

    function extra() {
        return [];
    }

    function first() {
        return [];
    }

    function run() {
        api('Run', [{ key: 'f0', vlu: val('rt-f0') }, { key: 'f1', vlu: val('rt-f1') }, { key: 'fb', vlu: val('rt-fb') }]);
    }

    function iso(d) {
        return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2);
    }

    function span(k) {
        var t = new Date(), a, b = t;
        if (k === 'month') {
            a = new Date(t.getFullYear(), t.getMonth(), 1);
        } else if (k === 'last') {
            a = new Date(t.getFullYear(), t.getMonth() - 1, 1);
            b = new Date(t.getFullYear(), t.getMonth(), 0);
        } else {
            a = new Date(t.getFullYear(), 0, 1);
        }
        el('rt-f0').value = iso(a);
        el('rt-f1').value = iso(b);
        run();
    }

    function cell(n) {
        var parts = [];
        n.childNodes.forEach(function (x) {
            var s = (x.textContent || '').replace(/\s+/g, ' ').trim();
            if (s) {
                parts.push(s);
            }
        });
        var v = parts.join(' · ').replace(/"/g, '""');
        return /[",\n]/.test(v) ? '"' + v + '"' : v;
    }

    function csv(id, name) {
        var t = el(id);
        if (!t) {
            return;
        }
        var lines = [];
        t.querySelectorAll('tr').forEach(function (r) {
            var cells = [];
            r.querySelectorAll('th,td').forEach(function (c) {
                cells.push(cell(c));
            });
            lines.push(cells.join(','));
        });
        var blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = name.replace(/[^A-Za-z0-9]+/g, '-').toLowerCase() + '-' + val('rt-f0') + '-to-' + val('rt-f1') + '.csv';
        document.body.appendChild(a);
        a.click();
        setTimeout(function () {
            URL.revokeObjectURL(a.href);
            a.remove();
        }, 500);
    }

    var own = { run: run, span: span, csv: csv };

    var pg = 1;
    var mtimer = null;

    function filters() {
        return [{ key: 'term', vlu: val('rt-q') }, { key: 'pg', vlu: String(pg) }].concat(extra());
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
                return c.indexOf('rt-') === 0 ? np + '-' + c.slice(3) : c;
            }).join(' ');
        });
    }

    function carry(id, doc, np, page) {
        var from = el('rt-' + id), to = doc.getElementById(np + '-' + id);
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
            var rail = el('rt-rail');
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
            [].forEach.call(el('rt-menu').querySelectorAll('a'), function (n) {
                n.classList.toggle('rt-on', n === a);
            });
            navTo(href, true);
        });
    }

    function navInit() {
        var m = el('rt-menu');
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
                if (askBox && askBox.classList.contains('rt-open')) {
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
        var m = el('rt-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
        var d = el('rt-pk');
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
    document.addEventListener('DOMContentLoaded', AdminReportsJs.reveal);
} else {
    AdminReportsJs.reveal();
}
