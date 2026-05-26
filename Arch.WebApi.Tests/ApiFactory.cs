using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Arch.WebApi.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // по идее cache-shared только мешает изоляции тестов, но здесь наоборот
                // мы каждый раз вручную чистим таблицы через ApiFixture.InitializeAsync()
                // таким образом "изолируя" все эти тесты; без этого все тесты упадут
                ["ConnectionStrings:SQLite"] = "Data Source=file::memory:?cache=shared"
            });
        });
        base.ConfigureWebHost(builder);
    }
}