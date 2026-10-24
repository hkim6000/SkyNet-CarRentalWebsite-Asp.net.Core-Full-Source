var ContactJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Contact/' + method, JSON.stringify(list || []));
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function toast(msg) {
        var t = el('ct-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ct-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ct-open');
        }, 3600);
    }

    function openNav() {
        el('ct-nav').classList.add('ct-open');
        el('ct-scrim').classList.add('ct-open');
    }

    function closeNav() {
        el('ct-nav').classList.remove('ct-open');
        el('ct-scrim').classList.remove('ct-open');
    }

    function find(form) {
        var s = form.querySelector('[name="s"]').value, e = form.querySelector('[name="e"]').value;
        var msg = form.querySelector('.ct-serr');
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
        var box = el('ct-slides');
        return box ? box.querySelectorAll('.ct-slide') : [];
    }

    function slideTo(i) {
        var list = slides(), dots = document.querySelectorAll('#ct-slides .ct-sdot');
        if (!list.length) {
            return;
        }
        sIdx = (i + list.length) % list.length;
        for (var k = 0; k < list.length; k++) {
            var on = k === sIdx;
            list[k].classList.toggle('ct-on', on);
            list[k].setAttribute('aria-hidden', on ? 'false' : 'true');
            if (dots[k]) {
                dots[k].classList.toggle('ct-on', on);
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
        var box = el('ct-slides');
        if (!box) {
            return;
        }
        var ctl = box.querySelector('.ct-sctl');
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

    function send() {
        el('ct-send').disabled = true;
        call('Submit', [
            { key: 'name', vlu: val('ct-name') },
            { key: 'email', vlu: val('ct-email') },
            { key: 'phone', vlu: val('ct-phone') },
            { key: 'branch', vlu: el('ct-branch').value },
            { key: 'code', vlu: val('ct-code') },
            { key: 'msg', vlu: val('ct-msg') },
            { key: 'web', vlu: val('ct-web') }
        ]);
        return false;
    }

    function failed() {
        el('ct-send').disabled = false;
        var bad = document.querySelector('.ct-err:not(:empty)');
        if (bad) {
            var f = bad.parentNode.querySelector('input,textarea,select');
            if (f) {
                f.focus();
            }
        }
    }

    function sent() {
        el('ct-send').disabled = false;
        el('ct-form').reset();
        el('ct-sent').scrollIntoView({ behavior: 'auto', block: 'center' });
    }

    function start() {
    }

    var own = { send: send, failed: failed, sent: sent };

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
    document.addEventListener('DOMContentLoaded', ContactJs.setup);
} else {
    ContactJs.setup();
}
