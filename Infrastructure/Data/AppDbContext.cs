using Microsoft.EntityFrameworkCore;
using MinhaApi.Domain.Entities;

namespace MinhaApi.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Produto> Produtos => Set<Produto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurações explícitas do Fluent API (Boa prática de DDD/Clean Architecture)
        modelBuilder.Entity<Produto>(builder =>
        {
            builder.HasKey(p => p.Id);
            
            builder.Property(p => p.Nome)
                   .IsRequired()
                   .HasMaxLength(150);

            builder.Property(p => p.Preco)
                   .HasConversion<double>() // Necessário para compatibilidade de decimal estável no SQLite
                   .IsRequired();
        });
    }
}