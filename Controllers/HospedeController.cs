using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Só funcionários logados podem ver/gerenciar hóspedes (dados pessoais: CPF, telefone...).
    // EN: Only logged-in employees can view/manage guests (personal data: CPF, phone...).
    [RequerFuncionario]
    public class HospedeController : Controller
    {
        private readonly AppDbContext _context;

        public HospedeController(AppDbContext context) => _context = context;

        // GET: /Hospede?busca=texto
        public async Task<IActionResult> Index(string? busca)
        {
            var query = _context.Hospedes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                query = query.Where(h => h.Nome.Contains(busca) || h.Cpf.Contains(busca));
            }

            ViewData["Busca"] = busca;
            return View(await query.OrderBy(h => h.Nome).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.HospedeId == id);
            if (hospede == null) return NotFound();
            return View(hospede);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nome,Cpf,Telefone,DataNascimento")] Hospede hospede)
        {
            if (ModelState.IsValid)
            {
                _context.Add(hospede);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Hóspede cadastrado com sucesso.";
                return RedirectToAction(nameof(Index));
            }
            return View(hospede);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var hospede = await _context.Hospedes.FindAsync(id);
            if (hospede == null) return NotFound();

            // PT: Ver Hospede.EstaAnonimizado para o porquê. A recusa acontece já no GET para
            //     a recepção não perder tempo preenchendo um formulário que nunca salvaria.
            // EN: See Hospede.EstaAnonimizado for the reasoning. The refusal happens on the
            //     GET so reception doesn't waste time filling in a form that would never save.
            if (hospede.EstaAnonimizado)
            {
                TempData["Erro"] = "Esta ficha foi anonimizada a pedido do titular e não pode ser editada.";
                return RedirectToAction(nameof(Index));
            }

            return View(hospede);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("HospedeId,Nome,Cpf,Telefone,DataNascimento")] Hospede hospede)
        {
            if (id != hospede.HospedeId) return NotFound();

            // PT: O GET já recusa, mas o POST precisa recusar também — a checagem é contra a
            //     linha GRAVADA, não contra o que veio no formulário, senão bastaria enviar um
            //     CPF qualquer no corpo para repersonalizar uma ficha anonimizada.
            // EN: The GET already refuses, but the POST has to refuse too — and the check is
            //     against the STORED row, not against what arrived in the form, otherwise
            //     posting any CPF in the body would be enough to re-personalize an anonymized
            //     record.
            var gravado = await _context.Hospedes.AsNoTracking().FirstOrDefaultAsync(h => h.HospedeId == id);
            if (gravado == null) return NotFound();
            if (gravado.EstaAnonimizado)
            {
                TempData["Erro"] = "Esta ficha foi anonimizada a pedido do titular e não pode ser editada.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(hospede);
                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = "Hóspede atualizado com sucesso.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Hospedes.Any(h => h.HospedeId == id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(hospede);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.HospedeId == id);
            if (hospede == null) return NotFound();
            return View(hospede);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hospede = await _context.Hospedes.FindAsync(id);
            if (hospede == null) return RedirectToAction(nameof(Index));

            // PT: As FKs de Reserva e Hospedagem para Hospede são DeleteBehavior.Restrict
            //     (ver AppDbContext), então remover um hóspede com histórico faz o banco
            //     recusar o DELETE. Sem esta checagem, o SaveChangesAsync lançava
            //     DbUpdateException e a recepção via uma página de erro 500 — sem explicação
            //     nenhuma do que houve nem do que fazer.
            //     A checagem vem ANTES do Remove de propósito: assim a mensagem diz o motivo
            //     real, em vez de traduzir um erro de banco depois do fato.
            // EN: The Reserva and Hospedagem FKs to Hospede are DeleteBehavior.Restrict (see
            //     AppDbContext), so removing a guest with history makes the database refuse
            //     the DELETE. Without this check, SaveChangesAsync threw a DbUpdateException
            //     and reception got a 500 error page — with no explanation of what happened
            //     or what to do.
            //     The check comes BEFORE the Remove on purpose: that way the message states
            //     the real reason instead of translating a database error after the fact.
            var temReservas = await _context.Reservas.AnyAsync(r => r.HospedeId == id);
            var temHospedagens = await _context.Hospedagens.AnyAsync(h => h.HospedeId == id);

            if (temReservas || temHospedagens)
            {
                // PT: O próprio hóspede tem um caminho que funciona neste caso — a exclusão
                //     pela conta dele anonimiza em vez de apagar, preservando o histórico
                //     que a legislação fiscal exige (ver PrivacidadeService).
                // EN: The guest themselves has a path that works here — deleting from their
                //     own account anonymizes instead of erasing, preserving the history tax
                //     law requires (see PrivacidadeService).
                TempData["Erro"] = "Este hóspede tem reservas ou hospedagens registradas e não pode ser removido. " +
                                   "Para remover os dados pessoais preservando o histórico, o próprio hóspede pode " +
                                   "pedir a exclusão pela conta dele.";
                return RedirectToAction(nameof(Index));
            }

            _context.Hospedes.Remove(hospede);
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Hóspede removido.";
            return RedirectToAction(nameof(Index));
        }
    }
}
