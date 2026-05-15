using Microsoft.EntityFrameworkCore;

namespace Arch.WebApi.Data;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
    
    public DbSet<Book> Books => Set<Book>();
}