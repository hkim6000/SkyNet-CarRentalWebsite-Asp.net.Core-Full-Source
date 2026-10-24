var BookJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Book/' + method, JSON.stringify(list || []));
    }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function toast(msg) {
        var t = el('bk-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('bk-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bk-open');
        }, 3600);
    }

    function openNav() {
        el('bk-nav').classList.add('bk-open');
        el('bk-scrim').classList.add('bk-open');
    }

    function closeNav() {
        el('bk-nav').classList.remove('bk-open');
        el('bk-scrim').classList.remove('bk-open');
    }

    function find(form) {
        var s = form.querySelector('[name="s"]').value, e = form.querySelector('[name="e"]').value;
        var msg = form.querySelector('.bk-serr');
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
        var box = el('bk-slides');
        return box ? box.querySelectorAll('.bk-slide') : [];
    }

    function slideTo(i) {
        var list = slides(), dots = document.querySelectorAll('#bk-slides .bk-sdot');
        if (!list.length) {
            return;
        }
        sIdx = (i + list.length) % list.length;
        for (var k = 0; k < list.length; k++) {
            var on = k === sIdx;
            list[k].classList.toggle('bk-on', on);
            list[k].setAttribute('aria-hidden', on ? 'false' : 'true');
            if (dots[k]) {
                dots[k].classList.toggle('bk-on', on);
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
        var box = el('bk-slides');
        if (!box) {
            return;
        }
        var ctl = box.querySelector('.bk-sctl');
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

    var qtimer = null;
    var busy = false;

    function extras() {
        var x = [];
        document.querySelectorAll('#bk-form .bk-xq').forEach(function (s) {
            if (s.value !== '0') {
                x.push(s.getAttribute('data-x') + ':' + s.value);
            }
        });
        var c = document.querySelector('#bk-form input[name="bk-cover"]:checked');
        if (c && c.value) {
            x.push(c.value + ':1');
        }
        return x.join(',');
    }

    function fields() {
        var list = [{ key: 'extras', vlu: extras() }];
        document.querySelectorAll('#bk-form [data-k]').forEach(function (n) {
            list.push({ key: n.getAttribute('data-k'), vlu: n.type === 'checkbox' ? (n.checked ? '1' : '0') : (n.value || '').trim() });
        });
        return list;
    }

    function quote() {
        clearTimeout(qtimer);
        qtimer = setTimeout(function () {
            call('Quote', fields());
        }, 200);
    }

    function place() {
        if (busy) {
            return false;
        }
        busy = true;
        el('bk-go').disabled = true;
        el('bk-e-trip').textContent = '';
        call('Place', fields());
        return false;
    }

    function done() {
        busy = false;
        var b = el('bk-go');
        if (b) {
            b.disabled = false;
        }
    }

    function toFirstError() {
        var e = Array.prototype.find.call(document.querySelectorAll('#bk-form .bk-err'), function (n) { return n.textContent.trim().length > 0; });
        if (e) {
            var f = e.parentNode.querySelector('input, select');
            (f || e).scrollIntoView({ behavior: 'smooth', block: 'center' });
            if (f) {
                setTimeout(function () { f.focus({ preventScroll: true }); }, 300);
            }
        }
    }

    function top() {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function start() {
        document.querySelectorAll('#bk-form [data-k]').forEach(function (n) {
            n.addEventListener('input', function () {
                var e = el('bk-e-' + n.getAttribute('data-k'));
                if (e) {
                    e.textContent = '';
                }
            });
        });
    }

    var own = { quote: quote, place: place, done: done, toFirstError: toFirstError, top: top };

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
    document.addEventListener('DOMContentLoaded', BookJs.setup);
} else {
    BookJs.setup();
}
