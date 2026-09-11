using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Authentication;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;
using SisHospedar.Services;

namespace SisHospedar.Controllers
{
    // PT: Controller com duas metades: hóspede cria o pedido de reserva; recepção
    //     confirma (o que gera o check-in) ou cancela. Por isso o filtro de sessão de
    //     funcionário é aplicado por método (Index/Confirmar/Cancelar), não na classe.
    //     As regras de negócio vivem em IReservaService — o controller só cuida de
    //     sessão/ModelState/View.
    // EN: A controller with two halves: the guest creates the booking request;
    //     reception confirms it (which triggers check-in) or cancels it. That's why the
    //     employee session filter is applied per-method (Index/Confirmar/Cancelar), not
    //     on the class. The business rules live in IReservaService — the controller
    //     only handles session/ModelState/View concerns.
    public class ReservaController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IReservaService _reservaService;

        public ReservaController(AppDbContext context, IReservaService reservaService)
        {
            _context = context;
            _reservaService = reservaService;
        }

        // ---- Área do CLIENTE: criar pedido de reserva ----

        // PT: Sem [RequerHospede] de propósito. Antes, o visitante que clicava "Reservar
        //     Agora" na home batia no filtro e era jogado direto no login, sem nunca ver
        //     o que estava reservando — pedir conta antes de mostrar valor é a parede de
        //     atrito clássica. Agora ele escolhe primeiro; a autenticação entra só na
        //     hora de efetivar, que é a ação irreversível.
        //     Os parâmetros opcionais existem para o retorno pós-login: é assim que a
        //     escolha atravessa a autenticação, sem guardar estado no servidor.
        // EN: Deliberately no [RequerHospede]. Before, a visitor clicking "Book now" on
        //     the home page hit the filter and was dumped straight into login, never
        //     seeing what they were booking — asking for an account before showing value
        //     is the classic friction wall. Now they choose first; authentication only
        //     comes in at the point of committing, which is the irreversible action.
        //     The optional parameters exist for the post-login return: that's how the
        //     choice survives authentication, with no server-side state.
        public async Task<IActionResult> Create(int? apartamentoId, DateTime? entrada, DateTime? saida)
        {
            ViewData["Autenticado"] = await EstaAutenticadoComoHospedeAsync();
            CarregarApartamentos(apartamentoId);

            return View(new Reserva
            {
                ApartamentoId = apartamentoId ?? 0,
                DataEntrada = entrada ?? DateTime.Today,
                DataSaidaPrevista = saida ?? DateTime.Today.AddDays(1)
            });
        }

        // PT: Cria a reserva como Pendente. A validação de datas e a revalidação de
        //     disponibilidade do apartamento acontecem dentro do service.
        // EN: Creates the booking as Pendente. Date validation and room availability
        //     re-validation happen inside the service.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ApartamentoId,DataEntrada,DataSaidaPrevista")] Reserva reserva)
        {
            var identidade = await HttpContext.AuthenticateAsync(Esquemas.Hospede);

            // PT: Erro de preenchimento é resolvido antes de mandar para o login. Fazer o
            //     visitante criar uma conta para só então descobrir que a data estava
            //     errada seria cruel — e ele provavelmente desistiria no meio.
            // EN: Form errors are settled before sending anyone to login. Making a visitor
            //     create an account only to then discover the date was wrong would be
            //     cruel — and they'd probably abandon halfway.
            if (!ModelState.IsValid)
                return RecarregarFormulario(reserva, identidade.Succeeded);

            if (!identidade.Succeeded)
            {
                // PT: Checagem barata antes de exigir cadastro: se o apartamento já não
                //     está disponível, avisa agora. Sem isso, a pessoa faria todo o
                //     cadastro para receber "indisponível" no fim.
                // EN: A cheap check before demanding registration: if the room is already
                //     unavailable, say so now. Without this, the person would complete the
                //     whole sign-up only to be told "unavailable" at the end.
                var disponivel = await _context.Apartamentos
                    .AnyAsync(a => a.ApartamentoId == reserva.ApartamentoId && a.Status == StatusApartamento.Disponivel);
                if (!disponivel)
                {
                    ModelState.AddModelError("", "Este apartamento não está mais disponível. Escolha outro.");
                    return RecarregarFormulario(reserva, autenticado: false);
                }

                // PT: A escolha viaja na URL de retorno — nada é gravado no servidor, então
                //     não há rascunho para expirar nem limpar, e o fluxo sobrevive a
                //     reinício de contêiner. Só número de apartamento e datas trafegam:
                //     nenhum dado pessoal.
                // EN: The choice travels in the return URL — nothing is stored server-side,
                //     so there's no draft to expire or clean up, and the flow survives a
                //     container restart. Only a room number and dates travel: no personal
                //     data.
                var retorno = Url.Action(nameof(Create), "Reserva", new
                {
                    apartamentoId = reserva.ApartamentoId,
                    entrada = reserva.DataEntrada.ToString("yyyy-MM-dd"),
                    saida = reserva.DataSaidaPrevista.ToString("yyyy-MM-dd")
                })!;

                return RedirectToAction("Login", "Cliente", new { returnUrl = retorno });
            }

            reserva.HospedeId = int.Parse(identidade.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var (sucesso, erro) = await _reservaService.CriarPedidoAsync(reserva);
            if (sucesso)
            {
                TempData["Sucesso"] = "Reserva enviada! A recepção vai confirmar em breve.";
                return RedirectToAction("MinhaConta", "Cliente");
            }

            ModelState.AddModelError("", erro!);
            return RecarregarFormulario(reserva, autenticado: true);
        }

        // PT: Sem esquema padrão de autenticação neste projeto, User não é preenchido
        //     sozinho fora de uma ação protegida — daí o AuthenticateAsync explícito.
        //     Mesmo motivo pelo qual o _Layout faz isso para montar a navbar.
        // EN: With no default authentication scheme in this project, User isn't populated
        //     automatically outside a guarded action — hence the explicit AuthenticateAsync.
        //     Same reason _Layout does it to render the navbar.
        private async Task<bool> EstaAutenticadoComoHospedeAsync() =>
            (await HttpContext.AuthenticateAsync(Esquemas.Hospede)).Succeeded;

        private IActionResult RecarregarFormulario(Reserva reserva, bool autenticado)
        {
            ViewData["Autenticado"] = autenticado;
            CarregarApartamentos(reserva.ApartamentoId);
            return View(reserva);
        }

        // PT: ToList() antes do SelectList é necessário: DescricaoParaSelecao é [NotMapped]
        //     e composta em C#, então o EF não consegue traduzi-la para SQL. Materializar
        //     primeiro faz a composição acontecer em memória, sobre as linhas já trazidas.
        // EN: ToList() before SelectList is required: DescricaoParaSelecao is [NotMapped] and
        //     composed in C#, so EF can't translate it to SQL. Materializing first makes the
        //     composition happen in memory, over the rows already fetched.
        private void CarregarApartamentos(int? selecionado) =>
            ViewData["ApartamentoId"] = new SelectList(
                _context.Apartamentos
                    .Where(a => a.Status == StatusApartamento.Disponivel)
                    .OrderBy(a => a.Numero)
                    .ToList(),
                "ApartamentoId", nameof(Apartamento.DescricaoParaSelecao), selecionado);

        // ---- Área do FUNCIONÁRIO (recepção): confirmar/cancelar reservas ----
        [RequerFuncionario]
        public async Task<IActionResult> Index()
        {
            var reservas = await _context.Reservas
                .Include(r => r.Hospede)
                .Include(r => r.Apartamento)
                .OrderBy(r => r.Status)
                .ThenBy(r => r.DataEntrada)
                .ToListAsync();

            return View(reservas);
        }

        // PT: Ponto de check-in: confirmar a reserva cria a Hospedagem de verdade e
        //     ocupa o apartamento (ver IReservaService.ConfirmarAsync).
        // EN: The check-in point: confirming the booking creates the actual Hospedagem
        //     and occupies the room (see IReservaService.ConfirmarAsync).
        [RequerFuncionario]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmar(int id)
        {
            var mensagem = await _reservaService.ConfirmarAsync(id);
            if (mensagem != null) TempData["Sucesso"] = mensagem;
            return RedirectToAction(nameof(Index));
        }

        [RequerFuncionario]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            if (await _reservaService.CancelarAsync(id))
                TempData["Sucesso"] = "Reserva cancelada.";
            return RedirectToAction(nameof(Index));
        }
    }
}
