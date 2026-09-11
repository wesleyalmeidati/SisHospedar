using System.Text.RegularExpressions;
using SisHospedar.Services;

namespace SisHospedar.Middleware
{
    // PT: Conta acessos a páginas. Roda para todo mundo, com ou sem consentimento — e isso
    //     é legítimo porque a contagem é agregada e anônima (ver VisitaPagina): não há
    //     identificador, então não há dado pessoal, então não há consentimento a pedir.
    //     O consentimento entra só na distinção entre visitante novo e recorrente, que
    //     exige gravar um identificador no navegador.
    // EN: Counts page hits. It runs for everyone, with or without consent — and that is
    //     legitimate because the count is aggregated and anonymous (see VisitaPagina): there
    //     is no identifier, so no personal data, so no consent to ask for.
    //     Consent only comes into play for telling a new visitor from a returning one, which
    //     requires writing an identifier to the browser.
    public class RegistroVisitaMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly BufferVisitas _buffer;

        public RegistroVisitaMiddleware(RequestDelegate next, BufferVisitas buffer)
        {
            _next = next;
            _buffer = buffer;
        }

        // PT: Troca segmentos numéricos por "{id}". Sem isto, /Hospede/Edit/5,
        //     /Hospede/Edit/6... virariam uma linha cada, e a tabela cresceria com os dados
        //     em vez de com as páginas — exatamente o que o agregado existe para evitar.
        // EN: Replaces numeric segments with "{id}". Without this, /Hospede/Edit/5,
        //     /Hospede/Edit/6... would each become their own row, and the table would grow
        //     with the data instead of with the pages — exactly what the aggregate exists to
        //     avoid.
        private static readonly Regex SegmentoNumerico = new(@"/\d+", RegexOptions.Compiled);

        public async Task InvokeAsync(HttpContext context, IAnalyticsService _)
        {
            if (DeveContar(context))
            {
                var caminho = Normalizar(context.Request.Path);
                var recorrente = ResolverRecorrencia(context);
                _buffer.Registrar(caminho, recorrente);
            }

            await _next(context);
        }

        private static bool DeveContar(HttpContext context)
        {
            if (!HttpMethods.IsGet(context.Request.Method)) return false;

            var caminho = context.Request.Path.Value ?? "/";

            // PT: Fora da contagem: sonda de saúde (ruído constante que distorceria tudo) e
            //     arquivos estáticos, identificados pela extensão — medir audiência de
            //     página é diferente de medir requisição de CSS.
            // EN: Excluded: the health probe (constant noise that would skew everything) and
            //     static files, identified by extension — measuring page audience is not the
            //     same as measuring CSS requests.
            if (caminho.StartsWith("/health", StringComparison.OrdinalIgnoreCase)) return false;
            if (Path.HasExtension(caminho)) return false;

            return true;
        }

        private static string Normalizar(PathString path)
        {
            var caminho = path.Value;
            if (string.IsNullOrEmpty(caminho) || caminho == "/") return "/";

            caminho = SegmentoNumerico.Replace(caminho.TrimEnd('/'), "/{id}");
            return caminho.Length > 200 ? caminho[..200] : caminho;
        }

        // PT: Só há como saber que alguém já esteve aqui se existir um identificador no
        //     navegador — e ele só pode ser gravado com consentimento. Sem aceite, todo
        //     acesso conta como novo, que é a degradação correta: a medição fica menos
        //     precisa, e o site continua funcionando igual.
        // EN: The only way to know someone has been here before is an identifier in the
        //     browser — and it may only be written with consent. Without acceptance every
        //     hit counts as new, which is the correct degradation: the measurement gets less
        //     precise and the site keeps working exactly the same.
        private static bool ResolverRecorrencia(HttpContext context)
        {
            var decisao = context.Request.Cookies[Medicao.CookieConsentimento];
            if (decisao != "aceito") return false;

            if (context.Request.Cookies.ContainsKey(Medicao.CookieVisitante)) return true;

            // PT: Primeira visita com consentimento: grava o identificador agora, e esta
            //     visita ainda conta como nova. HttpOnly porque nenhum script precisa lê-lo.
            //     Secure segue o esquema da requisição — graças ao UseForwardedHeaders, em
            //     produção Request.IsHttps é verdadeiro mesmo o Kestrel recebendo HTTP puro
            //     do ingress, então o cookie sai com a flag.
            // EN: First visit with consent: write the identifier now, and this visit still
            //     counts as new. HttpOnly because no script needs to read it. Secure follows
            //     the request scheme — thanks to UseForwardedHeaders, in production
            //     Request.IsHttps is true even though Kestrel receives plain HTTP from the
            //     ingress, so the cookie ships with the flag.
            context.Response.Cookies.Append(
                Medicao.CookieVisitante,
                Guid.NewGuid().ToString("N"),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = false,
                    SameSite = SameSiteMode.Lax,
                    Secure = context.Request.IsHttps,
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                });

            return false;
        }
    }
}
