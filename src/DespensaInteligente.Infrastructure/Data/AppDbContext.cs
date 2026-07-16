using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Domain.Entities;
using DespensaInteligente.Application.Common.Interfaces;

using DespensaInteligente.Application.Services;

namespace DespensaInteligente.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<IdentityUser<Guid>, IdentityRole<Guid>, Guid>, AppDbContextPlaceholder, IApplicationDbContext
    {
        private readonly ICurrentUserService _currentUserService;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            ICurrentUserService currentUserService) : base(options)
        {
            _currentUserService = currentUserService;
        }

        public DbSet<Categoria> Categorias => Set<Categoria>();
        public DbSet<Item> Itens => Set<Item>();
        public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();
        public DbSet<Compra> Compras => Set<Compra>();
        public DbSet<CompraItem> CompraItens => Set<CompraItem>();
        public DbSet<Lote> Lotes => Set<Lote>();
        public DbSet<ListaManual> ListaManuais => Set<ListaManual>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure ASP.NET Identity tables
            builder.Entity<IdentityUser<Guid>>(entity =>
            {
                entity.ToTable("users");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.NormalizedEmail).HasColumnName("normalized_email");
                entity.Property(e => e.UserName).HasColumnName("username");
                entity.Property(e => e.NormalizedUserName).HasColumnName("normalized_username");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.SecurityStamp).HasColumnName("security_stamp");
                entity.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp");
                entity.Property(e => e.PhoneNumber).HasColumnName("phone_number");
                entity.Property(e => e.EmailConfirmed).HasColumnName("email_confirmed");
                entity.Property(e => e.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
                entity.Property(e => e.TwoFactorEnabled).HasColumnName("two_factor_enabled");
                entity.Property(e => e.LockoutEnd).HasColumnName("lockout_end");
                entity.Property(e => e.LockoutEnabled).HasColumnName("lockout_enabled");
                entity.Property(e => e.AccessFailedCount).HasColumnName("access_failed_count");
            });

            builder.Entity<IdentityRole<Guid>>().ToTable("roles").Property(r => r.Id).HasColumnName("id");
            builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
            builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
            builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
            builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
            builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

            // Global Query Filters (only applies if user is authenticated)
            builder.Entity<Categoria>().HasQueryFilter(x => x.UserId == _currentUserService.UserId);
            builder.Entity<Item>().HasQueryFilter(x => x.UserId == _currentUserService.UserId);
            builder.Entity<NotaFiscal>().HasQueryFilter(x => x.UserId == _currentUserService.UserId);
            builder.Entity<Compra>().HasQueryFilter(x => x.UserId == _currentUserService.UserId);
            builder.Entity<CompraItem>().HasQueryFilter(x => x.UserId == _currentUserService.UserId);
            builder.Entity<Lote>().HasQueryFilter(x => x.UserId == _currentUserService.UserId);
            builder.Entity<ListaManual>().HasQueryFilter(x => x.UserId == _currentUserService.UserId);

            // Categorias configurations
            builder.Entity<Categoria>(entity =>
            {
                entity.ToTable("categorias");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.Nome).HasColumnName("nome").IsRequired();
                entity.Property(e => e.Cor).HasColumnName("cor").HasDefaultValue("#94a3b8");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            });

            // Itens configurations
            builder.Entity<Item>(entity =>
            {
                entity.ToTable("itens");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.Nome).HasColumnName("nome").IsRequired();
                entity.Property(e => e.Apelido).HasColumnName("apelido");
                entity.Property(e => e.Marca).HasColumnName("marca");
                entity.Property(e => e.CategoriaId).HasColumnName("categoria_id");
                entity.Property(e => e.Unidade).HasColumnName("unidade").HasDefaultValue("un");
                entity.Property(e => e.DuracaoDias).HasColumnName("duracao_dias");
                entity.Property(e => e.EstoqueMinimo).HasColumnName("estoque_minimo").HasColumnType("numeric").HasDefaultValue(0m);
                entity.Property(e => e.Observacoes).HasColumnName("observacoes");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

                entity.HasOne(e => e.Categoria)
                    .WithMany(c => c.Itens)
                    .HasForeignKey(e => e.CategoriaId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // NotasFiscais configurations
            builder.Entity<NotaFiscal>(entity =>
            {
                entity.ToTable("notas_fiscais");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.ChaveAcesso).HasColumnName("chave_acesso").HasMaxLength(44).IsRequired();
                entity.Property(e => e.Mercado).HasColumnName("mercado").IsRequired();
                entity.Property(e => e.DataCompra).HasColumnName("data_compra").HasColumnType("date");
                entity.Property(e => e.ValorTotal).HasColumnName("valor_total").HasColumnType("numeric");
                entity.Property(e => e.ArquivoNome).HasColumnName("arquivo_nome");
                entity.Property(e => e.ArquivoPath).HasColumnName("arquivo_path");
                entity.Property(e => e.RawExtracao).HasColumnName("raw_extracao").HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            });

            // Compras configurations
            builder.Entity<Compra>(entity =>
            {
                entity.ToTable("compras");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.Mercado).HasColumnName("mercado").IsRequired();
                entity.Property(e => e.DataCompra).HasColumnName("data_compra").HasColumnType("date");
                entity.Property(e => e.ValorTotal).HasColumnName("valor_total").HasColumnType("numeric").HasDefaultValue(0m);
                entity.Property(e => e.NotaFiscalId).HasColumnName("nota_fiscal_id");
                entity.Property(e => e.Observacoes).HasColumnName("observacoes");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");

                entity.HasOne(e => e.NotaFiscal)
                    .WithMany()
                    .HasForeignKey(e => e.NotaFiscalId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // CompraItens configurations
            builder.Entity<CompraItem>(entity =>
            {
                entity.ToTable("compra_itens");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.CompraId).HasColumnName("compra_id").IsRequired();
                entity.Property(e => e.ItemId).HasColumnName("item_id");
                entity.Property(e => e.NomeOriginal).HasColumnName("nome_original").IsRequired();
                entity.Property(e => e.Quantidade).HasColumnName("quantidade").HasColumnType("numeric").HasDefaultValue(1m);
                entity.Property(e => e.Unidade).HasColumnName("unidade").HasDefaultValue("un");
                entity.Property(e => e.PrecoUnitario).HasColumnName("preco_unitario").HasColumnType("numeric");
                entity.Property(e => e.PrecoTotal).HasColumnName("preco_total").HasColumnType("numeric");

                entity.HasOne(e => e.Compra)
                    .WithMany(c => c.Itens)
                    .HasForeignKey(e => e.CompraId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Item)
                    .WithMany(i => i.CompraItens)
                    .HasForeignKey(e => e.ItemId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Lotes configurations
            builder.Entity<Lote>(entity =>
            {
                entity.ToTable("lotes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.ItemId).HasColumnName("item_id").IsRequired();
                entity.Property(e => e.CompraId).HasColumnName("compra_id");
                entity.Property(e => e.Quantidade).HasColumnName("quantidade").HasColumnType("numeric").IsRequired();
                entity.Property(e => e.Validade).HasColumnName("validade").HasColumnType("date");
                entity.Property(e => e.Apelido).HasColumnName("apelido");
                entity.Property(e => e.Marca).HasColumnName("marca");
                entity.Property(e => e.Local).HasColumnName("local").HasDefaultValue("despensa");
                entity.Property(e => e.AbertoEm).HasColumnName("aberto_em").HasColumnType("date");
                entity.Property(e => e.Consumido).HasColumnName("consumido").HasDefaultValue(false);
                entity.Property(e => e.ConsumidoEm).HasColumnName("consumido_em").HasColumnType("date");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");

                entity.HasOne(e => e.Item)
                    .WithMany(i => i.Lotes)
                    .HasForeignKey(e => e.ItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Compra)
                    .WithMany(c => c.Lotes)
                    .HasForeignKey(e => e.CompraId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ListaManual configurations
            builder.Entity<ListaManual>(entity =>
            {
                entity.ToTable("lista_manual");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.Nome).HasColumnName("nome").IsRequired();
                entity.Property(e => e.Unidade).HasColumnName("unidade").HasDefaultValue("un");
                entity.Property(e => e.QuantidadePlanejada).HasColumnName("quantidade_planejada").HasColumnType("numeric").HasDefaultValue(1m);
                entity.Property(e => e.QuantidadeReal).HasColumnName("quantidade_real").HasColumnType("numeric");
                entity.Property(e => e.Pego).HasColumnName("pego").HasDefaultValue(false);
                entity.Property(e => e.PrecoUnitario).HasColumnName("preco_unitario").HasColumnType("numeric");
                entity.Property(e => e.Preco).HasColumnName("preco").HasColumnType("numeric");
                entity.Property(e => e.Observacoes).HasColumnName("observacoes");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
            });
        }
    }
}
