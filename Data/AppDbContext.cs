using Microsoft.EntityFrameworkCore;
using SisHospedar.Models;

namespace SisHospedar.Data
{
    // PT: Substitui o antigo ConexaoBanco.cs / DAOs manuais do projeto original.
    //     O EF Core gera e executa as queries parametrizadas automaticamente, e a
    //     string de conexão NUNCA fica no código-fonte (ver Program.cs / appsettings).
    // EN: Replaces the original project's ConexaoBanco.cs / manual DAOs. EF Core
    //     generates and runs parameterized queries automatically, and the connection
    //     string NEVER lives in source code (see Program.cs / appsettings).
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Hospede> Hospedes => Set<Hospede>();
        public DbSet<Apartamento> Apartamentos => Set<Apartamento>();
        public DbSet<Funcionario> Funcionarios => Set<Funcionario>();
        public DbSet<Produto> Produtos => Set<Produto>();
        public DbSet<Hospedagem> Hospedagens => Set<Hospedagem>();
        public DbSet<Consumo> Consumos => Set<Consumo>();
        public DbSet<Reserva> Reservas => Set<Reserva>();

        // PT: Configura índices únicos e as regras de exclusão (cascade/restrict) entre
        //     as entidades — ver CLAUDE.md para o resumo dessas regras.
        // EN: Configures unique indexes and delete rules (cascade/restrict) between
        //     entities — see CLAUDE.md for a summary of these rules.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Hospede>().HasIndex(h => h.Cpf).IsUnique();
            modelBuilder.Entity<Hospede>().HasIndex(h => h.Login).IsUnique();
            modelBuilder.Entity<Funcionario>().HasIndex(f => f.Login).IsUnique();
            modelBuilder.Entity<Apartamento>().HasIndex(a => a.Numero).IsUnique();

            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Hospede)
                .WithMany(h => h.Reservas)
                .HasForeignKey(r => r.HospedeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Apartamento)
                .WithMany()
                .HasForeignKey(r => r.ApartamentoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Hospedagem>()
                .HasOne(h => h.Hospede)
                .WithMany(h => h.Hospedagens)
                .HasForeignKey(h => h.HospedeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Hospedagem>()
                .HasOne(h => h.Apartamento)
                .WithMany(a => a.Hospedagens)
                .HasForeignKey(h => h.ApartamentoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Consumo>()
                .HasOne(c => c.Hospedagem)
                .WithMany(h => h.Consumos)
                .HasForeignKey(c => c.HospedagemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Consumo>()
                .HasOne(c => c.Produto)
                .WithMany(p => p.Consumos)
                .HasForeignKey(c => c.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);

            // PT: Vínculo opcional (FK nullable) — identidades continuam independentes.
            //     Se o cadastro de hóspede for excluído, o funcionário não é afetado,
            //     apenas perde a referência (SetNull em vez de Cascade/Restrict).
            // EN: Optional link (nullable FK) — identities remain independent. If the
            //     guest record is deleted, the employee is unaffected, it just loses
            //     the reference (SetNull instead of Cascade/Restrict).
            modelBuilder.Entity<Funcionario>()
                .HasOne(f => f.Hospede)
                .WithMany()
                .HasForeignKey(f => f.HospedeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Apartamento>()
                .Property(a => a.ValorDiaria).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Produto>()
                .Property(p => p.Preco).HasColumnType("decimal(10,2)");
        }
    }
}
