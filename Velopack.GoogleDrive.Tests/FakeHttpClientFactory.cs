using Google.Apis.Http;
using System.Net.Http;

public class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _httpMessageHandler;

    public FakeHttpClientFactory(HttpMessageHandler httpMessageHandler)
    {
        _httpMessageHandler = httpMessageHandler;
    }

    public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args)
    {
        // Tworzymy ConfigurableHttpClient na podstawie HttpMessageHandler
        return new ConfigurableHttpClient(new ConfigurableMessageHandler(_httpMessageHandler)); // Use ConfigurableMessageHandler instead of HttpClientHandlerAdapter
    }
}
