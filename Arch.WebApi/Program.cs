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

app.MapPost("/notes", async (
    [FromBody] NoteBody body, 
    [FromServices] DataContext dataContext,
    CancellationToken ct) =>
{
    var now = DateTimeOffset.UtcNow;
    var note = new Note
    {
        Text = body.Text,
        CreatedAt = now,
        UpdatedAt = now
    };
    dataContext.Notes.Add(note);
    await dataContext.SaveChangesAsync(ct);

    return new NoteModel
    {
        Id = note.Id,
        Text = note.Text,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt
    };
});
app.MapGet("/notes/{id:int}", async Task<Results<NotFound, Ok<NoteModel>>> (
    [FromRoute] int id,
    [FromServices] DataContext dataContext,
    CancellationToken ct) =>
{
    var note = await dataContext.Notes.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (note is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Ok(new NoteModel
    {
        Id = note.Id,
        Text = note.Text,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt
    });
});
app.MapPut("/notes/{id:int}", async Task<Results<NotFound, Ok<NoteModel>>> (
    [FromRoute] int id,
    [FromBody] NoteBody body,
    [FromServices] DataContext dataContext,
    CancellationToken ct) =>
{
    var note = await dataContext.Notes.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (note is null)
    {
        return TypedResults.NotFound();
    }
    
    note.Text = body.Text;
    note.UpdatedAt = DateTimeOffset.UtcNow;
    await dataContext.SaveChangesAsync(ct);

    return TypedResults.Ok(new NoteModel
    {
        Id = note.Id,
        Text = note.Text,
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt
    });
});
app.MapDelete("/notes/{id:int}", async Task<Results<NotFound, NoContent>> (
    [FromRoute] int id,
    DataContext dataContext,
    CancellationToken ct) =>
{
    var note = await dataContext.Notes.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (note is null)
    {
        return TypedResults.NotFound();
    }
    
    dataContext.Notes.Remove(note);
    await dataContext.SaveChangesAsync(ct);
    
    return TypedResults.NoContent();
});
app.MapGet("/notes", async (
    [FromServices] DataContext dataContext,
    CancellationToken ct,
    [FromQuery] string? search = null) =>
{
    var query = dataContext.Notes.AsQueryable();
    if (string.IsNullOrEmpty(search) is false)
    {
        query = query.Where(x => EF.Functions.Like(
            x.Text.ToLower(), 
            $"%{search.ToLower()}%"));
    }

    return await query
        .Select(x => new NoteModel
        {
            Id = x.Id,
            Text = x.Text,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        })
        .OrderByDescending(x => x.Id)
        .ToListAsync(ct);

});

app.Run();

public record NoteBody
{
    public required string Text { get; set; }
}

public record NoteModel : NoteBody
{
    public required int Id { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
}