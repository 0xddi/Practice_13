using Arch.WebApi.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<DataContext>((sp, ef) =>
{
    var connStr = sp.GetRequiredService<IConfiguration>().GetConnectionString("SQLite");
    ef.UseSqlite(connStr);
});

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DataContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/ping", () => TypedResults.Text("pong"));

app.MapGet("/books/{id:int}", async Task<Results<NotFound, Ok<BookModel>>> (
    [FromRoute] int id,
    [FromServices] DataContext dataContext,
    CancellationToken ct) =>
{
    var book = await dataContext.Books.FirstOrDefaultAsync(book => book.Id == id, ct);
    if (book == null) return TypedResults.NotFound();
    return TypedResults.Ok(new BookModel
    {
        Id = book.Id,
        Name = book.Name,
        Author = book.Author,
        ReleaseDate = book.ReleaseDate
    });
});

app.MapPost("/books", async (
    [FromBody] BookBody body,
    [FromServices] DataContext dataContext,
    CancellationToken ct) =>
{
    var book = new Book
    {
        Name = body.Name,
        Author = body.Author,
        ReleaseDate = body.ReleaseDate
    };
    dataContext.Books.Add(book);
    await dataContext.SaveChangesAsync(ct);

    return new BookModel
    {
        Id =  book.Id,
        Name = book.Name,
        Author = book.Author,
        ReleaseDate = book.ReleaseDate
    };
});

app.MapGet("/books", async ([FromServices] DataContext dataContext, CancellationToken ct,
    [FromQuery] string? search = null) =>
{
    var query = dataContext.Books.AsQueryable();
    if (string.IsNullOrEmpty(search) is false)
    {
        query = query.Where(book => 
            EF.Functions.Like(book.Name, $"%{search}%") ||
            EF.Functions.Like(book.Author, $"%{search}%"));
    }

    return await query.Select(book => new BookModel
    {
        Id = book.Id,
        Name = book.Name,
        Author = book.Author,
        ReleaseDate = book.ReleaseDate
    }).OrderByDescending(book => book.Id).ToListAsync(ct);
});

app.MapPut("/books/{id:int}", async Task<Results<NotFound, Ok<BookModel>>> (
    [FromRoute] int id,
    [FromBody] BookBody body,
    [FromServices] DataContext dataContext,
    CancellationToken ct) =>
{
    var book = await dataContext.Books.FirstOrDefaultAsync(book => book.Id == id, ct);
    if (book == null) return TypedResults.NotFound();
    book.Name = body.Name;
    book.Author = body.Author;
    book.ReleaseDate = body.ReleaseDate;
    await dataContext.SaveChangesAsync(ct);

    return TypedResults.Ok(new BookModel
    {
        Id = book.Id,
        Name = book.Name,
        Author = book.Author,
        ReleaseDate = book.ReleaseDate
    });
});

app.MapDelete("/books/{id:int}", async Task<Results<NotFound, NoContent>> (
        [FromRoute] int id,
        [FromServices] DataContext dataContext,
        CancellationToken ct) =>
    {
        var book = await dataContext.Books.FirstOrDefaultAsync(book => book.Id == id, ct);
        if (book == null) return TypedResults.NotFound();
        dataContext.Books.Remove(book);
        await dataContext.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
);

app.Run();

public record BookBody
{
    public required string Name { get; set; }
    public required string Author { get; set; }
    public required DateOnly? ReleaseDate { get; set; }
}

public record BookModel : BookBody
{
    public required int Id { get; set; }
}

public partial class Program { }