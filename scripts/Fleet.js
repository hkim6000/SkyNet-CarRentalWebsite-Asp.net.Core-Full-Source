var FleetJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Fleet/' + method, JSON.stringify(list || []));
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function toast(msg) {
        var t = el('fl-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('fl-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('fl-open');
        }, 3600);
    }

    function openNav() {
        el('fl-nav').classList.add('fl-open');
        el('fl-scrim').classList.add('fl-open');
    }

    function closeNav() {
        el('fl-nav').classList.remove('fl-open');
        el('fl-scrim').classList.remove('fl-open');
    }

    function find(form) {
        var s = form.querySelector('[name="s"]').value, e = form.querySelector('[name="e"]').value;
        var msg = form.querySelector('.fl-serr');
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
        var box = el('fl-slides');
        return box ? box.querySelectorAll('.fl-slide') : [];
    }

    function slideTo(i) {
        var list = slides(), dots = document.querySelectorAll('#fl-slides .fl-sdot');
        if (!list.length) {
            return;
        }
        sIdx = (i + list.length) % list.length;
        for (var k = 0; k < list.length; k++) {
            var on = k === sIdx;
            list[k].classList.toggle('fl-on', on);
            list[k].setAttribute('aria-hidden', on ? 'false' : 'true');
            if (dots[k]) {
                dots[k].classList.toggle('fl-on', on);
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
        var box = el('fl-slides');
        if (!box) {
            return;
        }
        var ctl = box.querySelector('.fl-sctl');
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

    function pick(b) {
        var g = b.getAttribute('data-g');
        document.querySelectorAll('.fl-chip[data-g="' + g + '"]').forEach(function (x) {
            var on = x === b;
            x.classList.toggle('fl-act', on);
            x.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        var v = function (name) {
            var a = document.querySelector('.fl-chip.fl-act[data-g="' + name + '"]');
            return a ? a.getAttribute('data-v') : '';
        };
        call('Filter', [{ key: 'seats', vlu: v('Seats') }, { key: 'gear', vlu: v('Transmission') }, { key: 'max', vlu: v('Price a day') }]);
    }

    function start() {
    }

    var own = { pick: pick };

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
    document.addEventListener('DOMContentLoaded', FleetJs.setup);
} else {
    FleetJs.setup();
}
