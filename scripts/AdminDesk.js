var AdminDeskJs = (function () {

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
        $ApiRequest('AdminDesk/' + method, JSON.stringify(list));
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
        var t = el('dk-toast');
        t.textContent = msg;
        t.classList.add('dk-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('dk-open');
        }, 3400);
    }

    function openMenu() {
        el('dk-rail').classList.add('dk-open');
        el('dk-scrim').classList.add('dk-open');
    }

    function closeMenu() {
        el('dk-rail').classList.remove('dk-open');
        el('dk-scrim').classList.remove('dk-open');
    }

    function openModal(title) {
        var m = el('dk-modal');
        el('dk-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('dk-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('dk-modal');
        if (m) {
            m.classList.remove('dk-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('dk-modal');
        if (!m || !m.classList.contains('dk-open')) {
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
        if (!askBox || !askBox.classList.contains('dk-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('dk-open');
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
        askBox.className = 'dk-ask';
        askBox.innerHTML = '<div class="dk-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off"><em class="dk-err" role="alert"></em><div class="dk-askacts"><button type="button" class="dk-btn dk-btn-ghost"></button><button type="button" class="dk-btn"></button></div></div>';
        document.getElementById('dk-site-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        on(window, 'keydown', function (e) {
            if (!askBox.classList.contains('dk-open')) {
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
        x.b[1].className = 'dk-btn' + (o.danger ? ' dk-askdel' : '');
        askBox.firstChild.classList.toggle('dk-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('dk-open');
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

    var pkPg = 1;

    function pkList(n) {
        pkPg = typeof n === 'number' ? n : 1;
        api('MediaPick', [{ key: 'ppg', vlu: String(pkPg) }, { key: 'pkind', vlu: 'photo' }, { key: 'pterm', vlu: val('dk-pkq') }]);
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(function () {
            pkList();
        }, 300);
    }

    function pkOpen() {
        var d = el('dk-pk');
        return d && d.classList.contains('dk-open');
    }

    function pkClose() {
        var d = el('dk-pk');
        if (d) {
            d.classList.remove('dk-open');
            d.setAttribute('aria-hidden', 'true');
        }
        el('dk-pkfile').value = '';
        pkDone = null;
    }

    function pickMedia(done) {
        pkDone = done;
        el('dk-pkq').value = '';
        el('dk-pkmsg').textContent = '';
        var d = el('dk-pk');
        d.classList.add('dk-open');
        d.setAttribute('aria-hidden', 'false');
        pkList();
        setTimeout(function () {
            el('dk-pkq').focus();
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
        el('dk-pkmsg').textContent = 'Uploading.';
        api('MediaUp');
    }

    function pkUploaded(ok, msg) {
        el('dk-pkfile').value = '';
        el('dk-pkmsg').textContent = msg;
        if (ok) {
            el('dk-pkq').value = '';
        }
    }

    function paintPic(box, it) {
        var b = box.querySelector('.dk-picb');
        box.setAttribute('data-src', it ? it.src : '');
        b.innerHTML = it ? '<img src="' + esc(it.thumb) + '" alt="">' : '<span>' + esc(box.getAttribute('data-ph') || 'Choose a picture') + '</span>';
        var x = box.querySelector('.dk-picx');
        if (x) {
            x.hidden = !it;
        }
    }

    function pic(btn) {
        var box = btn.closest('.dk-pic');
        pickMedia(function (it) {
            paintPic(box, it);
            markDirty();
        });
    }

    function picClear(btn) {
        paintPic(btn.closest('.dk-pic'), null);
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

    var tabNow = 'out';

    function qs(k) {
        var m = location.search.match(new RegExp('[?&]' + k + '=([^&]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function extra() {
        return [{ key: 'b', vlu: val('dk-b') }, { key: 'd', vlu: val('dk-d') }, { key: 't', vlu: tabNow }];
    }

    function first() {
        return [{ key: 'b', vlu: qs('b') }];
    }

    function tab(t) {
        tabNow = t;
        list(true);
    }

    function today() {
        var d = new Date();
        el('dk-d').value = d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2);
        list(true);
    }

    function one(method, id) {
        api(method, [{ key: 'id', vlu: id }]);
    }

    function outForm(id) {
        one('OutForm', id);
    }

    function inForm(id) {
        one('InForm', id);
    }

    function extForm(id) {
        one('ExtForm', id);
    }

    function carPick() {
        var s = el('dk-f-car');
        var o = s.options[s.selectedIndex];
        if (o) {
            el('dk-f-mileage').value = o.getAttribute('data-m');
            el('dk-f-fuel').value = o.getAttribute('data-f');
        }
    }

    var shots = [];

    function paintShots() {
        var box = el('dk-photos');
        if (!box) {
            return;
        }
        box.innerHTML = shots.map(function (s, i) {
            return '<span class="dk-shot"><img src="' + esc(s.thumb) + '" alt=""><button type="button" aria-label="Remove photo" onclick="AdminDeskJs.dropPhoto(' + i + ')">&times;</button></span>';
        }).join('');
    }

    function photosReset() {
        shots = [];
        paintShots();
    }

    function addPhoto() {
        if (shots.length >= 8) {
            toast('Up to 8 photos.');
            return;
        }
        pickMedia(function (it) {
            if (!shots.some(function (s) { return s.src === it.src; })) {
                shots.push(it);
                paintShots();
                markDirty();
            }
        });
    }

    function dropPhoto(i) {
        shots.splice(i, 1);
        paintShots();
        markDirty();
    }

    function inspection() {
        var p = [];
        document.querySelectorAll('#dk-form .dk-pnl').forEach(function (s) {
            p.push(s.getAttribute('data-p') + '=' + s.value);
        });
        return [{ key: 'panels', vlu: p.join(';') }, { key: 'photos', vlu: shots.map(function (s) { return s.src; }).join('|') }];
    }

    function outSave() {
        api('OutSave', collect('dk-form', [{ key: 'id', vlu: val('dk-f-id') }].concat(inspection())).concat(filters()));
        return false;
    }

    function inPreview() {
        api('InPreview', collect('dk-form', [{ key: 'id', vlu: val('dk-f-id') }]));
    }

    function due(v) {
        var p = el('dk-f-paid');
        if (p) {
            p.value = v;
        }
    }

    function inSave() {
        api('InSave', collect('dk-form', [{ key: 'id', vlu: val('dk-f-id') }].concat(inspection())).concat(filters()));
        return false;
    }

    function extCheck() {
        api('ExtCheck', collect('dk-form', [{ key: 'id', vlu: val('dk-f-id') }]));
        return false;
    }

    function extSave() {
        api('ExtSave', collect('dk-form', [{ key: 'id', vlu: val('dk-f-id') }, { key: 'sign', vlu: val('dk-f-sign') }]).concat(filters()));
    }

    function decline(id, code) {
        ask({ title: 'Decline the request on ' + code + '?', text: 'The return time stays as booked. The customer gets an email if email is set up.', ok: 'Decline', no: 'Back', danger: true }, function () {
            api('Decline', [{ key: 'id', vlu: id }].concat(filters()));
        });
    }

    function noShow(id, code) {
        ask({ title: 'Mark ' + code + ' as a no-show?', text: 'The booking ends and its car becomes free for other customers.', ok: 'Mark no-show', no: 'Back', danger: true }, function () {
            api('NoShow', [{ key: 'id', vlu: id }].concat(filters()));
        });
    }

    var own = { tab: tab, today: today, outForm: outForm, inForm: inForm, extForm: extForm, carPick: carPick, photosReset: photosReset, addPhoto: addPhoto, dropPhoto: dropPhoto, outSave: outSave, inPreview: inPreview, due: due, inSave: inSave, extCheck: extCheck, extSave: extSave, decline: decline, noShow: noShow };

    var pg = 1;
    var mtimer = null;

    function filters() {
        return [{ key: 'term', vlu: val('dk-q') }, { key: 'pg', vlu: String(pg) }].concat(extra());
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
                return c.indexOf('dk-') === 0 ? np + '-' + c.slice(3) : c;
            }).join(' ');
        });
    }

    function carry(id, doc, np, page) {
        var from = el('dk-' + id), to = doc.getElementById(np + '-' + id);
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
            var rail = el('dk-rail');
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
            [].forEach.call(el('dk-menu').querySelectorAll('a'), function (n) {
                n.classList.toggle('dk-on', n === a);
            });
            navTo(href, true);
        });
    }

    function navInit() {
        var m = el('dk-menu');
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
                if (askBox && askBox.classList.contains('dk-open')) {
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
        var m = el('dk-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
        var d = el('dk-pk');
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
    document.addEventListener('DOMContentLoaded', AdminDeskJs.reveal);
} else {
    AdminDeskJs.reveal();
}
