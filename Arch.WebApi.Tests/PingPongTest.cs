namespace Arch.WebApi.Tests;

public class PingPongTest(ApiFixture fixture)
{
    [Fact]
    public async Task GetPing_ReceivePong()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.Api.CreateClient();
        
        var res = await client.GetAsync("/ping", ct);
        
        Assert.True(res.IsSuccessStatusCode);
    }
}