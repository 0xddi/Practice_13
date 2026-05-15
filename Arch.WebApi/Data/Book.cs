namespace Arch.WebApi.Data;

public class Book
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateOnly? ReleaseDate { get; set; }
}