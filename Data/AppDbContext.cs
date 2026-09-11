using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
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
    public class AppDbContext : DbContext, IDataProtectionKeyContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // PT: Chaveiro do Data Protection (assina o cookie de autenticação e o token de
        //     antiforgery). Por padrão as chaves são gravadas em disco DENTRO do contêiner
        //     (/root/.aspnet/DataProtection-Keys), que é descartado a cada deploy — o que
        //     invalidava todo cookie e todo formulário aberto. Os logs de produção mostravam
        //     exatamente isso: uma chave nova por inicialização de contêiner e
        //     AntiforgeryValidationException ("key not found in the key ring") todos os dias.
        //     Guardar o chaveiro aqui resolve usando o banco que o projeto já tem, sem
        //     depender de Blob Storage ou Key Vault (que teriam custo).
        // EN: Data Protection key ring (signs the auth cookie and the antiforgery token).
        //     By default keys are written to disk INSIDE the container
        //     (/root/.aspnet/DataProtection-Keys), which is discarded on every deploy —
        //     invalidating every cookie and every open form. Production logs showed exactly
        //     that: a new key per container start and a daily AntiforgeryValidationException
        //     ("key not found in the key ring"). Keeping the key ring here solves it using
        //     the database the project already has, with no Blob Storage or Key Vault
        //     (which would cost money).
        public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

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
            modelBuilder.Entity<Hospede>().HasIndex(h => h.Email).IsUnique();
            modelBuilder.Entity<Funcionario>().HasIndex(f => f.Login).IsUnique();
            // PT: Únicos entre si (não entre as duas tabelas) — ver ContaController.EsqueciSenha,
            //     que busca primeiro em Funcionario e só depois em Hospede caso o e-mail
            //     também exista repetido nas duas identidades.
            // EN: Unique within each table (not across the two) — see
            //     ContaController.EsqueciSenha, which checks Funcionario first and only
            //     falls back to Hospede if the same email also happens to exist in both.
            modelBuilder.Entity<Funcionario>().HasIndex(f => f.Email).IsUnique();
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
