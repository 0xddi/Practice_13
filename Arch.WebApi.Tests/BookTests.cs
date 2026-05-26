using System.Net;
using System.Net.Http.Json;
using Arch.WebApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Arch.WebApi.Tests;

public class BooksCrudTest : IAsyncLifetime
{
    private readonly ApiFixture _fixture;
    private readonly HttpClient _client;
    private readonly DataContext _dataContext;

    public BooksCrudTest(ApiFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.Api.CreateClient();
        
        var scope = _fixture.Api.Services.CreateScope();
        _dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
    }

    // очищаем таблицу Books перед каждым тестом
    public async ValueTask InitializeAsync()
    {
        await _dataContext.Books.ExecuteDeleteAsync();
    }

    // ничего не делаем, т. к. при InitializeAsync очищаем таблицы
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    
    
    [Fact]
    public async Task PostBook_ValidData_ReturnsBookWithId()
    {
        var ct = TestContext.Current.CancellationToken;
        var newBook = new
        {
            Name = "Война и мир",
            Author = "Лев Толстой",
            ReleaseDate = "1888-08-08" 
        };

        var response = await _client.PostAsJsonAsync("/books", newBook, ct);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var book = await response.Content.ReadFromJsonAsync<BookModel>(ct);
        Assert.NotNull(book);
        Assert.True(book.Id > 0);
        Assert.Equal("Война и мир", book.Name);
        Assert.Equal("Лев Толстой", book.Author);
        Assert.Equal("1888-08-08", book.ReleaseDate?.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task PostBook_WithoutReleaseDate_ReturnsBookWithNullReleaseDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var newBook = new
        {
            Name = "Мастер и Маргарита",
            Author = "Михаил Булгаков",
            ReleaseDate = (string?)null
        };

        var response = await _client.PostAsJsonAsync("/books", newBook, ct);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var book = await response.Content.ReadFromJsonAsync<BookModel>(ct);
        Assert.NotNull(book);
        Assert.Null(book.ReleaseDate);
    }

    
    
    [Fact]
    public async Task GetBookById_ExistingId_ReturnsBook()
    {
        var ct = TestContext.Current.CancellationToken;
        
        var createdBook = await CreateTestBookAsync(ct);
        
        var response = await _client.GetAsync($"/books/{createdBook.Id}", ct);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var book = await response.Content.ReadFromJsonAsync<BookModel>(ct);
        Assert.NotNull(book);
        Assert.Equal(createdBook.Id, book.Id);
        Assert.Equal(createdBook.Name, book.Name);
        Assert.Equal(createdBook.Author, book.Author);
    }

    [Fact]
    public async Task GetBookById_NonExistingId_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        
        var response = await _client.GetAsync("/books/99999", ct);
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

 
    [Fact]
    public async Task GetAllBooks_ReturnsAllBooks()
    {
        var ct = TestContext.Current.CancellationToken;
        
        var book1 = await CreateTestBookAsync(ct, "Книга 1", "Автор 1");
        var book2 = await CreateTestBookAsync(ct, "Книга 2", "Автор 2");
        
        var response = await _client.GetAsync("/books", ct);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var books = await response.Content.ReadFromJsonAsync<List<BookModel>>(ct);
        Assert.NotNull(books);
        Assert.Contains(books, b => b.Id == book1.Id);
        Assert.Contains(books, b => b.Id == book2.Id);
    }

    [Fact]
    public async Task GetAllBooks_WithSearch_ReturnsFilteredBooks()
    {
        var ct = TestContext.Current.CancellationToken;
        
        await CreateTestBookAsync(ct, "Война и мир", "Толстой");
        await CreateTestBookAsync(ct, "Норма", "Сорокин");
        
        var response = await _client.GetAsync("/books?search=Война", ct);
        
        var books = await response.Content.ReadFromJsonAsync<List<BookModel>>(ct);
        Assert.NotNull(books);
        Assert.All(books, b => Assert.Contains("Война", b.Name));
    }

    [Fact]
    public async Task GetAllBooks_WithEmptySearch_ReturnsAllBooks()
    {
        var ct = TestContext.Current.CancellationToken;
        
        await CreateTestBookAsync(ct, "Книга А", "Автор А");
        await CreateTestBookAsync(ct, "Книга Б", "Автор Б");
        
        var response = await _client.GetAsync("/books?search=", ct);
        
        var books = await response.Content.ReadFromJsonAsync<List<BookModel>>(ct);
        Assert.NotNull(books);
        Assert.Equal(2, books.Count);
    }

    
    [Fact]
    public async Task PutBook_ExistingId_UpdatesBook()
    {
        var ct = TestContext.Current.CancellationToken;
        
        var createdBook = await CreateTestBookAsync(ct);
        
        var updatedData = new
        {
            Name = "Обновлённое название",
            Author = "Новый автор",
            ReleaseDate = "2026-05-15"
        };
        
        var response = await _client.PutAsJsonAsync($"/books/{createdBook.Id}", updatedData, ct);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var updatedBook = await response.Content.ReadFromJsonAsync<BookModel>(ct);
        Assert.NotNull(updatedBook);
        Assert.Equal(createdBook.Id, updatedBook.Id);
        Assert.Equal("Обновлённое название", updatedBook.Name);
        Assert.Equal("Новый автор", updatedBook.Author);
        Assert.Equal("2026-05-15", updatedBook.ReleaseDate?.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task PutBook_NonExistingId_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var updatedData = new
        {
            Name = "Любое имя",
            Author = "Любой автор",
            ReleaseDate = "2026-05-15"
        };
        
        var response = await _client.PutAsJsonAsync("/books/99999", updatedData, ct);
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    
    
    [Fact]
    public async Task DeleteBook_ExistingId_RemovesBook()
    {
        var ct = TestContext.Current.CancellationToken;
        
        var createdBook = await CreateTestBookAsync(ct);
        
        var deleteResponse = await _client.DeleteAsync($"/books/{createdBook.Id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        
        var getResponse = await _client.GetAsync($"/books/{createdBook.Id}", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteBook_NonExistingId_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        
        var response = await _client.DeleteAsync("/books/99999", ct);
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    
    private async Task<BookModel> CreateTestBookAsync(
        CancellationToken ct,
        string name = "Тестовая книга",
        string author = "Тестовый автор")
    {
        var newBook = new
        {
            Name = name,
            Author = author,
            ReleaseDate = "2026-05-15"
        };
        
        var response = await _client.PostAsJsonAsync("/books", newBook, ct);
        response.EnsureSuccessStatusCode();
        
        var book = await response.Content.ReadFromJsonAsync<BookModel>(ct);
        Assert.NotNull(book);
        return book;
    }
}