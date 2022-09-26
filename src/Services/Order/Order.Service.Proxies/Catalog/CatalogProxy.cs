using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Order.Service.Proxies.Catalog.Commands;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Order.Service.Proxies.Catalog
{
    public interface ICatalogProxy
    {
        Task UpdateStockAsync(ProductInStockUpdateStockCommand command);
    }

    public class CatalogProxy : ICatalogProxy
    {
        private readonly ApiUrls _apiUrls;
        private readonly IHttpClientFactory _httpClientFactory;


        private static readonly Random Jitterer = new Random();
        private static readonly AsyncRetryPolicy<HttpResponseMessage> TransientErrorRetryPolicy =
            Policy.HandleResult<HttpResponseMessage>(
                message => ((int)message.StatusCode) == 429 || (int)message.StatusCode >= 500)
            .WaitAndRetryAsync(2, sleepDurationProvider: retryAttemp =>
            {
                Console.WriteLine($"Reintentando: {retryAttemp}");
                return TimeSpan.FromSeconds(Math.Pow(2, retryAttemp)) + TimeSpan.FromMilliseconds(Jitterer.Next(0, 1000));
            });


        private static readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> CircuitBreakerPolicy =
            Policy.HandleResult<HttpResponseMessage>(message => ((int)message.StatusCode) == 429 || (int)message.StatusCode >= 500)
            .CircuitBreakerAsync(2, TimeSpan.FromSeconds(15));
            
        public CatalogProxy(
            IHttpClientFactory httpClientFactory,
            IOptions<ApiUrls> apiUrls,
            IHttpContextAccessor httpContextAccessor)
        {


            _httpClientFactory = httpClientFactory;
            _apiUrls = apiUrls.Value;
        }

        public async Task UpdateStockAsync(ProductInStockUpdateStockCommand command)
        {
            if (CircuitBreakerPolicy.CircuitState == CircuitState.Open)
            {
                throw new Exception("Circuito abierto");
            }


            var content = new StringContent(
                JsonSerializer.Serialize(command),
                Encoding.UTF8,
                "application/json"
            );
            var httpClient = _httpClientFactory.CreateClient();
            var response = await CircuitBreakerPolicy.ExecuteAsync(() =>

                    TransientErrorRetryPolicy.ExecuteAsync(() =>
                    httpClient.PutAsync(_apiUrls.CatalogUrl + "v1/stocks", content))
                );



            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Fallo UpdateStockAsync");
            }

        }
    }
}
