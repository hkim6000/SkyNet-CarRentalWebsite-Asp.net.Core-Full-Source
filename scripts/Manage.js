var ManageJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Manage/' + method, JSON.stringify(list || []));
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function toast(msg) {
        var t = el('mg-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('mg-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('mg-open');
        }, 3600);
    }

    function openNav() {
        el('mg-nav').classList.add('mg-open');
        el('mg-scrim').classList.add('mg-open');
    }

    function closeNav() {
        el('mg-nav').classList.remove('mg-open');
        el('mg-scrim').classList.remove('mg-open');
    }

    function find(form) {
        var s = form.querySelector('[name="s"]').value, e = form.querySelector('[name="e"]').value;
        var msg = form.querySelector('.mg-serr');
        msg.textContent = '';
        if (!s || !e) {
            msg.textContent = 'Choose the pick-up and return date and time.';
            return false;
        }
        if (e <= s) {
            msg.textContent = 'The return must be after the pick-up.';
            return false;
        }
        return true;
    }

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            closeNav();
        }
    });

    var sIdx = 0, sTimer = null, sHold = false, sX = null;

    function slides() {
        var box = el('mg-slides');
        return box ? box.querySelectorAll('.mg-slide') : [];
    }

    function slideTo(i) {
        var list = slides(), dots = document.querySelectorAll('#mg-slides .mg-sdot');
        if (!list.length) {
            return;
        }
        sIdx = (i + list.length) % list.length;
        for (var k = 0; k < list.length; k++) {
            var on = k === sIdx;
            list[k].classList.toggle('mg-on', on);
            list[k].setAttribute('aria-hidden', on ? 'false' : 'true');
            if (dots[k]) {
                dots[k].classList.toggle('mg-on', on);
                dots[k].setAttribute('aria-pressed', on ? 'true' : 'false');
            }
        }
        plan();
    }

    function slide(step) {
        slideTo(sIdx + step);
    }

    function still() {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function plan() {
        clearTimeout(sTimer);
        sTimer = null;
        if (sHold || document.hidden || still() || slides().length < 2) {
            return;
        }
        sTimer = setTimeout(function () { slideTo(sIdx + 1); }, 7000);
    }

    function hold(on) {
        sHold = on;
        plan();
    }

    function slideSetup() {
        var box = el('mg-slides');
        if (!box) {
            return;
        }
        var ctl = box.querySelector('.mg-sctl');
        ctl.addEventListener('mouseenter', function () { hold(true); });
        ctl.addEventListener('mouseleave', function () { hold(false); });
        ctl.addEventListener('focusin', function () { hold(true); });
        ctl.addEventListener('focusout', function () { hold(false); });
        box.addEventListener('touchstart', function (e) {
            sX = e.touches[0].clientX;
            hold(true);
        }, { passive: true });
        box.addEventListener('touchend', function (e) {
            var dx = sX === null ? 0 : e.changedTouches[0].clientX - sX;
            sX = null;
            sHold = false;
            if (Math.abs(dx) > 40) {
                slide(dx < 0 ? 1 : -1);
            } else {
                plan();
            }
        });
        document.addEventListener('visibilitychange', plan);
        plan();
    }

    function who() {
        return [{ key: 'bcode', vlu: val('mg-f-code') }, { key: 'email', vlu: val('mg-f-email') }];
    }

    function look() {
        el('mg-e-find').textContent = '';
        call('Look', who());
        return false;
    }

    function sure() {
        el('mg-sure').hidden = false;
    }

    function keep() {
        el('mg-sure').hidden = true;
    }

    function cancel() {
        call('Cancel', who());
    }

    function ask() {
        el('mg-e-end').textContent = '';
        call('Ask', who().concat([{ key: 'end', vlu: val('mg-f-end') }, { key: 'note', vlu: val('mg-f-note') }]));
    }

    function start() {
        var c = el('mg-f-code');
        if (c && c.value) {
            el('mg-f-email').focus();
        }
    }

    var own = { look: look, sure: sure, keep: keep, cancel: cancel, ask: ask };

    function setup() {
        slideSetup();
        start();
    }

    var base0 = {
        setup: setup,
        openNav: openNav,
        closeNav: closeNav,
        toast: toast,
        find: find,
        slide: slide,
        slideTo: slideTo
    };
    Object.keys(own).forEach(function (k) {
        base0[k] = own[k];
    });
    return base0;

})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ManageJs.setup);
} else {
    ManageJs.setup();
}
