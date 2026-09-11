// PT: Arquivo externo, e não <script> inline na View, porque o CSP definido no
//     Program.cs é "script-src 'self' https://cdn.jsdelivr.net", sem 'unsafe-inline'.
//     Um script embutido no HTML seria bloqueado pelo navegador sem erro visível na
//     página — a barra simplesmente nunca fecharia.
// EN: An external file rather than an inline <script> in the View, because the CSP set in
//     Program.cs is "script-src 'self' https://cdn.jsdelivr.net", with no 'unsafe-inline'.
//     A script embedded in the HTML would be blocked by the browser with no visible error
//     on the page — the bar would simply never close.
(function () {
    var barra = document.getElementById('aviso-cookies');
    if (!barra) return;

    // PT: A dispensa é guardada em localStorage, não em cookie. Gravar um cookie só para
    //     lembrar que o usuário leu o aviso sobre cookies seria irônico, e acrescentaria
    //     ao site exatamente o tipo de coisa que o aviso diz não existir.
    // EN: The dismissal is stored in localStorage, not a cookie. Writing a cookie just to
    //     remember the user read the notice about cookies would be ironic, and would add to
    //     the site the very kind of thing the notice says isn't there.
    var CHAVE = 'sishospedar:aviso-cookies-lido';

    // PT: localStorage lança em janela anônima com dados de site bloqueados; nesse caso o
    //     aviso apenas reaparece, que é degradação aceitável.
    // EN: localStorage throws in a private window with site data blocked; in that case the
    //     notice simply reappears, which is acceptable degradation.
    function lido() {
        try { return localStorage.getItem(CHAVE) === '1'; } catch (e) { return false; }
    }

    function marcarLido() {
        try { localStorage.setItem(CHAVE, '1'); } catch (e) { /* ignora */ }
    }

    if (lido()) return;

    barra.hidden = false;

    var botao = document.getElementById('aviso-cookies-fechar');
    if (botao) {
        botao.addEventListener('click', function () {
            barra.hidden = true;
            marcarLido();
        });
    }
})();
