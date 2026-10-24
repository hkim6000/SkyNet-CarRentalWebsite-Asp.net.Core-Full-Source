var AdminLoginJs = (function () {

    var changing = false;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? e.value : '';
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function call(method, list) {
        $ApiRequest('AdminLogin/' + method, JSON.stringify(list || []));
    }

    function busy(on) {
        el('lg-btn').disabled = on;
    }

    function message(text) {
        el('lg-e').textContent = text;
        busy(false);
    }

    function submit() {
        el('lg-e').textContent = '';
        busy(true);
        var user = val('lg-user').trim();
        if (changing) {
            call('SetPassword', [{ key: 'user', vlu: user }, { key: 'pass', vlu: val('lg-pass') }, { key: 'next', vlu: val('lg-new') }, { key: 'again', vlu: val('lg-again') }]);
        } else {
            call('SignIn', [{ key: 'user', vlu: user }, { key: 'pass', vlu: val('lg-pass') }]);
        }
        return false;
    }

    function choose() {
        changing = true;
        el('lg-change').hidden = false;
        el('lg-btn').textContent = 'Save and sign in';
        el('lg-passlabel').textContent = 'Current password';
        message('');
        el('lg-new').focus();
    }

    function ok(token) {
        document.cookie = 'br_tok=' + token + '; path=' + base() + '; max-age=28800; samesite=lax';
        location.href = 'Admin';
    }

    function reveal() {
        var u = el('lg-user');
        if (u && !u.value) {
            u.focus();
        }
    }

    return {
        reveal: reveal,
        submit: submit,
        choose: choose,
        message: message,
        ok: ok
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminLoginJs.reveal);
} else {
    AdminLoginJs.reveal();
}
