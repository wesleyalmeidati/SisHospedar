namespace SisHospedar.Services
{
    // PT: Descarrega o BufferVisitas no banco periodicamente. Como é um serviço singleton
    //     de longa duração e o AnalyticsService/AppDbContext são scoped, cada rodada abre o
    //     próprio escopo — resolver um serviço scoped direto no construtor de um
    //     BackgroundService é o erro clássico aqui: o DbContext viveria pela aplicação
    //     inteira, acumulando entidades rastreadas e vazando memória.
    // EN: Periodically drains BufferVisitas into the database. Since this is a long-lived
    //     singleton service while AnalyticsService/AppDbContext are scoped, each round opens
    //     its own scope — resolving a scoped service directly in a BackgroundService's
    //     constructor is the classic mistake here: the DbContext would live for the entire
    //     application lifetime, piling up tracked entities and leaking memory.
    public class FlushVisitasService : BackgroundService
    {
        private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(60);

        private readonly BufferVisitas _buffer;
        private readonly IServiceScopeFactory _escopos;
        private readonly ILogger<FlushVisitasService> _logger;

        public FlushVisitasService(
            BufferVisitas buffer,
            IServiceScopeFactory escopos,
            ILogger<FlushVisitasService> logger)
        {
            _buffer = buffer;
            _escopos = escopos;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Intervalo);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                    await DescarregarAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // PT: Falha ao gravar audiência não pode derrubar a aplicação nem parar
                    //     o laço — medição é acessória. Registra e tenta de novo no próximo
                    //     ciclo.
                    // EN: A failure writing audience data must not take the application down
                    //     or stop the loop — measurement is ancillary. Log it and try again
                    //     on the next cycle.
                    _logger.LogError(ex, "Falha ao gravar contagem de visitas; tentando no próximo ciclo.");
                }
            }

            // PT: Última descarga no desligamento, para não perder o intervalo em curso
            //     quando o contêiner é substituído num deploy.
            // EN: A final flush on shutdown, so the in-flight interval isn't lost when the
            //     container is replaced during a deploy.
            try { await DescarregarAsync(); } catch { /* encerrando, nada a fazer */ }
        }

        private async Task DescarregarAsync()
        {
            var pendentes = _buffer.Drenar();
            if (pendentes.Count == 0) return;

            using var escopo = _escopos.CreateScope();
            var analytics = escopo.ServiceProvider.GetRequiredService<IAnalyticsService>();

            foreach (var (caminho, total, recorrentes) in pendentes)
                await analytics.RegistrarVisitasAsync(caminho, total, recorrentes);
        }
    }
}
