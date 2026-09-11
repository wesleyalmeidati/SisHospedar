using System.Collections.Concurrent;

namespace SisHospedar.Services
{
    // PT: Acumulador em memória, registrado como singleton. Existe para que contar uma
    //     visita NÃO custe um ida-e-volta ao banco em cada requisição: o Azure SQL do
    //     projeto é tier Basic (5 DTU) e a home já foi otimizada justamente para não bater
    //     nele a cada acesso — gravar visita sincronamente desfaria esse ganho.
    //     O middleware só incrementa um contador na memória (microssegundos) e o
    //     FlushVisitasService grava o acumulado de tempos em tempos.
    //     Contrapartida assumida: se o contêiner morrer entre duas descargas, perde-se a
    //     contagem daquele intervalo. Para medição de audiência isso é aceitável — o número
    //     é uma estimativa, não um registro fiscal. Seria inaceitável para cobrança.
    // EN: In-memory accumulator, registered as a singleton. It exists so that counting a
    //     visit does NOT cost a database round-trip on every request: the project's Azure
    //     SQL is Basic tier (5 DTU) and the home page was optimized precisely to avoid
    //     hitting it on every visit — writing a visit synchronously would undo that gain.
    //     The middleware only increments an in-memory counter (microseconds) and
    //     FlushVisitasService persists the accumulated totals periodically.
    //     Accepted trade-off: if the container dies between two flushes, that interval's
    //     counts are lost. For audience measurement that's acceptable — the number is an
    //     estimate, not a financial record. It would be unacceptable for billing.
    public class BufferVisitas
    {
        private class Contador
        {
            public int Total;
            public int Recorrentes;
        }

        private ConcurrentDictionary<string, Contador> _contadores = new();

        public void Registrar(string caminho, bool recorrente)
        {
            var contador = _contadores.GetOrAdd(caminho, _ => new Contador());
            Interlocked.Increment(ref contador.Total);
            if (recorrente) Interlocked.Increment(ref contador.Recorrentes);
        }

        // PT: Troca o dicionário inteiro por um novo, de forma atômica, e devolve o antigo.
        //     Assim nenhum incremento é perdido por estar "no meio" de uma remoção item a
        //     item: quem ainda estiver escrevendo escreve no dicionário antigo, que é
        //     justamente o que está sendo lido aqui.
        // EN: Atomically swaps the whole dictionary for a fresh one and returns the old one.
        //     This way no increment is lost by being "in the middle" of an item-by-item
        //     removal: anyone still writing writes into the old dictionary, which is exactly
        //     the one being read here.
        public IReadOnlyList<(string Caminho, int Total, int Recorrentes)> Drenar()
        {
            var anterior = Interlocked.Exchange(ref _contadores, new ConcurrentDictionary<string, Contador>());
            return anterior
                .Select(par => (par.Key, par.Value.Total, par.Value.Recorrentes))
                .Where(x => x.Total > 0)
                .ToList();
        }
    }
}
