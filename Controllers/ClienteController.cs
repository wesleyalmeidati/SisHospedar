using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Authentication;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;
using SisHospedar.Services;

namespace SisHospedar.Controllers
{
    // PT: Portal de autoatendimento do hóspede — cookie de autenticação próprio
    //     (esquema Esquemas.Hospede), independente do cookie de funcionário. Login é
    //     um dos dois pontos de entrada do login único (o outro é HomeController.Login)
    //     — ver IAutenticacaoService.
    // EN: Guest self-service portal — its own authentication cookie (Esquemas.Hospede
    //     scheme), independent from the employee cookie. Login is one of the two
    //     entry points for the unified login (the other is HomeController.Login) —
    //     see IAutenticacaoService.
    public class ClienteController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Hospede> _userManager;
        private readonly IAutenticacaoService _autenticacao;
        private readonly IPrivacidadeService _privacidade;

        public ClienteController(
            AppDbContext context,
            UserManager<Hospede> userManager,
            IAutenticacaoService autenticacao,
            IPrivacidadeService privacidade)
        {
            _context = context;
            _userManager = userManager;
            _autenticacao = autenticacao;
            _privacidade = privacidade;
        }

        // PT: returnUrl atravessa o cadastro inteiro para que quem chegou aqui a partir de
        //     uma reserva volte para ela depois de criar a conta, em vez de ser largado em
        //     "Minha Conta" sem entender onde foi parar a escolha que tinha feito.
        // EN: returnUrl travels through the whole sign-up so that someone who arrived here
        //     from a booking goes back to it after creating their account, instead of being
        //     dropped in "My Account" wondering where the choice they made went.
        public IActionResult Registrar(string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // PT: Cria a conta do hóspede via UserManager<Hospede> (hash e persistência
        //     ficam por conta do Identity, através de EntityUserStore) e já assina o
        //     cookie dele, encaminhando direto para criar a primeira reserva.
        // EN: Creates the guest account via UserManager<Hospede> (hashing and
        //     persistence are handled by Identity, through EntityUserStore) and
        //     immediately signs in their cookie, sending them straight to create
        //     their first booking.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("CadastroPublico")]
        public async Task<IActionResult> Registrar(Hospede hospede, string senha, string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ModelState.Remove(nameof(Hospede.SenhaHash));

            // PT: Email e Login são [Required] só aqui, não no model (Hospede) — a
            //     mesma classe também representa hóspedes cadastrados pela recepção
            //     no balcão (HospedeController.Create), sem login/e-mail próprios, e
            //     um [Required] no model quebraria esse fluxo. Autocadastro é o único
            //     lugar onde os dois são obrigatórios: sem login/senha não tem como
            //     essa conta entrar depois, e sem e-mail não tem como redefinir senha
            //     se esquecer (ver ContaController.EsqueciSenha).
            // EN: Email and Login are [Required] only here, not on the model
            //     (Hospede) — the same class also represents guests registered by
            //     reception at the counter (HospedeController.Create), with no
            //     login/email of their own, and a model-level [Required] would break
            //     that flow. Self-registration is the only place where both are
            //     mandatory: with no login/password there's no way back into this
            //     account, and with no email there's no way to reset the password if
            //     forgotten (see ContaController.EsqueciSenha).
            if (string.IsNullOrWhiteSpace(hospede.Login))
                ModelState.AddModelError(nameof(Hospede.Login), "O login é obrigatório.");
            if (string.IsNullOrWhiteSpace(hospede.Email))
                ModelState.AddModelError(nameof(Hospede.Email), "O e-mail é obrigatório.");

            if (!string.IsNullOrWhiteSpace(hospede.Login))
            {
                if (await _context.Hospedes.AnyAsync(h => h.Login == hospede.Login))
                    ModelState.AddModelError(nameof(Hospede.Login), "Este login já está em uso.");

                // PT: Login também não pode colidir com o de um funcionário — as duas
                //     identidades são independentes, mas compartilham o mesmo "namespace" de login.
                // EN: Login also can't collide with an employee's — the two identities are
                //     independent, but share the same login "namespace".
                if (await _context.Funcionarios.AnyAsync(f => f.Login == hospede.Login))
                    ModelState.AddModelError(nameof(Hospede.Login), "Este login já está em uso.");
            }

            // PT: E-mail tem índice único no banco (ver AppDbContext) porque também é
            //     a chave de busca do "esqueci minha senha" — sem checar aqui antes,
            //     um e-mail repetido só falharia lá na frente com uma exceção crua do
            //     SQL Server ao salvar, em vez de um erro de formulário legível.
            // EN: Email has a unique index in the database (see AppDbContext) since
            //     it's also the lookup key for "forgot my password" — without
            //     checking here first, a repeated email would only fail later with a
            //     raw SQL Server exception on save, instead of a readable form error.
            if (!string.IsNullOrWhiteSpace(hospede.Email) && await _context.Hospedes.AnyAsync(h => h.Email == hospede.Email))
                ModelState.AddModelError(nameof(Hospede.Email), "Já existe uma conta com este e-mail.");

            if (await _context.Hospedes.AnyAsync(h => h.Cpf == hospede.Cpf))
                ModelState.AddModelError(nameof(Hospede.Cpf), "Já existe uma conta com este CPF.");

            if (ModelState.IsValid)
            {
                var resultado = await _userManager.CreateAsync(hospede, senha);
                if (resultado.Succeeded)
                {
                    await AssinarCookieAsync(hospede);

                    TempData["Sucesso"] = "Conta criada com sucesso! Bem-vindo(a) ao SisHospedar.";
                    return RedirecionarPara(returnUrl) ?? RedirectToAction("Create", "Reserva");
                }
                foreach (var erro in resultado.Errors)
                    ModelState.AddModelError("senha", erro.Description);
            }
            return View(hospede);
        }

        public IActionResult Login(string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // PT: Só aceita caminho local. returnUrl vem da URL, ou seja, é entrada do usuário:
        //     sem esta checagem, um link malicioso poderia usar a tela de login do site
        //     para jogar a pessoa em outro domínio logo após ela digitar a senha — o
        //     clássico open redirect usado em phishing.
        // EN: Local paths only. returnUrl comes from the URL, i.e. user input: without this
        //     check, a malicious link could use the site's own login screen to send someone
        //     to another domain right after they type their password — the classic open
        //     redirect used in phishing.
        private IActionResult? RedirecionarPara(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : null;

        // PT: Mesmo login único de HomeController.Login — quem digitar credenciais de
        //     funcionário aqui também entra normalmente, só que como funcionário.
        // EN: The same unified login as HomeController.Login — someone typing an
        //     employee's credentials here also gets signed in fine, just as an employee.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("AutenticacaoPublica")]
        public async Task<IActionResult> Login(string login, string senha, string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            var (resultado, tipo) = await _autenticacao.AutenticarAsync(HttpContext, login, senha);

            if (tipo == TipoIdentidadeLogada.Hospede)
                return RedirecionarPara(returnUrl) ?? RedirectToAction(nameof(MinhaConta));

            // PT: Funcionário ignora o returnUrl de propósito. Se ele entrar por um fluxo de
            //     reserva de hóspede, mandá-lo para lá seria sem sentido — a tela pertence à
            //     outra identidade, e ele cairia num formulário que não pode concluir.
            // EN: An employee deliberately ignores returnUrl. If they log in through a guest
            //     booking flow, sending them there would make no sense — the screen belongs
            //     to the other identity, and they'd land on a form they can't complete.
            if (tipo == TipoIdentidadeLogada.Funcionario) return RedirectToAction("Index", "Painel");

            ModelState.AddModelError("", resultado == ResultadoLogin.ContaBloqueada
                ? "Conta temporariamente bloqueada após várias tentativas inválidas. Tente novamente mais tarde ou redefina sua senha."
                : "Login ou senha inválidos.");
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(Esquemas.Hospede);
            return RedirectToAction("Index", "Home");
        }

        // PT: Área do hóspede — mostra a hospedagem ativa (com consumo) e as reservas
        //     pendentes de confirmação pela recepção.
        // EN: The guest's account area — shows the active stay (with consumption) and
        //     any bookings still pending reception's confirmation.
        [RequerHospede]
        public async Task<IActionResult> MinhaConta()
        {
            var hospedeId = ObterHospedeId();

            var hospedagemAtiva = await _context.Hospedagens
                .Include(h => h.Apartamento)
                .Include(h => h.Consumos).ThenInclude(c => c.Produto)
                .Where(h => h.HospedeId == hospedeId && !h.Finalizada)
                .FirstOrDefaultAsync();

            var reservasPendentes = await _context.Reservas
                .Include(r => r.Apartamento)
                .Where(r => r.HospedeId == hospedeId && r.Status == StatusReserva.Pendente)
                .ToListAsync();

            ViewData["Reservas"] = reservasPendentes;
            return View(hospedagemAtiva);
        }

        // PT: Permite ao hóspede estender a própria estadia, desde que ela pertença
        //     a ele, ainda esteja ativa e a nova data seja depois da entrada.
        // EN: Lets the guest extend their own stay, provided it belongs to them,
        //     is still active, and the new date is after check-in.
        [RequerHospede]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Prorrogar(int hospedagemId, DateTime novaDataSaida)
        {
            var hospedeId = ObterHospedeId();

            var hospedagem = await _context.Hospedagens
                .FirstOrDefaultAsync(h => h.HospedagemId == hospedagemId && h.HospedeId == hospedeId);

            if (hospedagem == null || hospedagem.Finalizada)
                return RedirectToAction(nameof(MinhaConta));

            // PT: A única checagem aqui era "depois da data de entrada", o que deixava passar
            //     duas coisas absurdas numa estadia EM ANDAMENTO: prorrogar para uma data já
            //     no passado (a estadia ficava com saída anterior a hoje e ainda aberta, com
            //     o apartamento Ocupado) e prorrogar para 2060. O nome da ação é prorrogar —
            //     a data tem que ser futura, e o teto existe pelo mesmo motivo do limite nas
            //     reservas: pegar ano digitado errado.
            // EN: The only check here was "after the check-in date", which let two absurd
            //     things through on an ONGOING stay: extending to a date already in the past
            //     (the stay ended up with a checkout before today and still open, with the
            //     room Ocupado) and extending to 2060. The action is called "extend" — the
            //     date has to be in the future, and the ceiling exists for the same reason as
            //     the booking limit: catching a mistyped year.
            if (novaDataSaida.Date <= DateTime.Today)
            {
                TempData["Erro"] = "A nova data de saída precisa ser depois de hoje.";
                return RedirectToAction(nameof(MinhaConta));
            }

            if (novaDataSaida.Date > DateTime.Today.AddYears(1))
            {
                TempData["Erro"] = "A nova data de saída está muito distante. Confira o ano informado.";
                return RedirectToAction(nameof(MinhaConta));
            }

            hospedagem.DataSaida = novaDataSaida;
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Estadia prorrogada com sucesso.";
            return RedirectToAction(nameof(MinhaConta));
        }

        // PT: O hóspede só sinaliza o pedido (SolicitouEncerramento = true); quem de
        //     fato finaliza a hospedagem é a recepção, em HospedagemController.
        // EN: The guest only flags the request (SolicitouEncerramento = true); reception
        //     is the one who actually finalizes the stay, in HospedagemController.
        [RequerHospede]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarEncerramento(int hospedagemId)
        {
            var hospedeId = ObterHospedeId();

            var hospedagem = await _context.Hospedagens
                .FirstOrDefaultAsync(h => h.HospedagemId == hospedagemId && h.HospedeId == hospedeId);

            if (hospedagem != null && !hospedagem.Finalizada)
            {
                hospedagem.SolicitouEncerramento = true;
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Pedido de encerramento enviado à recepção.";
            }
            return RedirectToAction(nameof(MinhaConta));
        }

        // PT: Direito de acesso (LGPD Art. 18, II) — mostra ao titular exatamente o que o
        //     sistema guarda sobre ele, inclusive o histórico de reservas e consumo.
        // EN: Right of access (LGPD Art. 18, II) — shows the data subject exactly what the
        //     system holds about them, including booking and consumption history.
        [RequerHospede]
        public async Task<IActionResult> MeusDados()
        {
            var dados = await _privacidade.ReunirDadosAsync(ObterHospedeId());
            if (dados == null) return NotFound();
            return View(dados);
        }

        // PT: Direito de portabilidade (LGPD Art. 18, V) — a lei pede formato
        //     interoperável, por isso JSON e não PDF: o titular consegue levar o arquivo
        //     para outro sistema, que é o objetivo do direito.
        // EN: Right to portability (LGPD Art. 18, V) — the law asks for an interoperable
        //     format, hence JSON rather than PDF: the data subject can take the file to
        //     another system, which is the point of the right.
        [RequerHospede]
        public async Task<IActionResult> ExportarDados()
        {
            var dados = await _privacidade.ReunirDadosAsync(ObterHospedeId());
            if (dados == null) return NotFound();

            var json = JsonSerializer.Serialize(dados, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json",
                $"meus-dados-sishospedar-{DateTime.Today:yyyy-MM-dd}.json");
        }

        [RequerHospede]
        public IActionResult ExcluirConta() => View();

        // PT: Direito de eliminação (LGPD Art. 18, VI). O serviço decide entre exclusão
        //     real e anonimização — ver IPrivacidadeService, que explica por que nem toda
        //     exclusão pode apagar a linha. Em qualquer um dos dois casos a sessão cai,
        //     porque a conta deixou de existir ou perdeu o login.
        // EN: Right to erasure (LGPD Art. 18, VI). The service decides between a real
        //     delete and anonymization — see IPrivacidadeService, which explains why not
        //     every erasure can drop the row. Either way the session ends, because the
        //     account is gone or has lost its login.
        [RequerHospede]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirContaConfirmado()
        {
            var resultado = await _privacidade.ExcluirContaAsync(ObterHospedeId());

            switch (resultado)
            {
                case ResultadoExclusao.BloqueadoEstadiaAtiva:
                    TempData["Erro"] = "Você tem uma estadia em aberto. Peça o encerramento na recepção antes de excluir a conta.";
                    return RedirectToAction(nameof(MeusDados));

                case ResultadoExclusao.BloqueadoReservaPendente:
                    TempData["Erro"] = "Você tem uma reserva pendente. Cancele-a com a recepção antes de excluir a conta.";
                    return RedirectToAction(nameof(MeusDados));

                case ResultadoExclusao.NaoEncontrado:
                    return NotFound();
            }

            await HttpContext.SignOutAsync(Esquemas.Hospede);
            TempData["Sucesso"] = resultado == ResultadoExclusao.Excluido
                ? "Sua conta e seus dados foram excluídos."
                : "Seus dados pessoais foram removidos. O registro das hospedagens já realizadas foi mantido de forma anonimizada, como exige a legislação fiscal.";
            return RedirectToAction("Index", "Home");
        }

        private async Task AssinarCookieAsync(Hospede hospede)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, hospede.HospedeId.ToString()),
                new(ClaimTypes.Name, hospede.Nome)
            };
            var identidade = new ClaimsIdentity(claims, Esquemas.Hospede);
            await HttpContext.SignInAsync(Esquemas.Hospede, new ClaimsPrincipal(identidade));
        }

        // PT: [RequerHospede] já garante que a claim existe e é um int válido.
        // EN: [RequerHospede] already guarantees the claim exists and is a valid int.
        private int ObterHospedeId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
