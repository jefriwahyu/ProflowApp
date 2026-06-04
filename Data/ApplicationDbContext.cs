using Microsoft.EntityFrameworkCore;
using ProflowApp.Models;

namespace ProflowApp.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Barang> Barang { get; set; }
    public DbSet<Pengajuan> Pengajuan { get; set; }
    public DbSet<Pesanan> Pesanan { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
}
