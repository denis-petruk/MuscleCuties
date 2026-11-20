using System.Net;
using System.Text;
using System.Text.Json;
using MuscleCuties.Server.Nutrition;
using MuscleCuties.UseCases.Features.Nutrition.CatalogSearch;
using Xunit;

namespace MuscleCuties.Server.Tests;

public sealed class FdcExternalFoodSourceTests
{
    [Fact]
    public void FdcExternalFoodSource_Parse_ParsesNutrientsCorrectly()
    {
        var json = """
        {
            "foods": [
                {
                    "fdcId": 173904,
                    "description": "Oats, rolled, dry",
                    "dataType": "Foundation",
                    "foodNutrients": [
                        { "nutrientId": 1008, "value": 379.0 },
                        { "nutrientId": 1003, "value": 13.15 },
                        { "nutrientId": 1004, "value": 6.52 },
                        { "nutrientId": 1005, "value": 67.70 }
                    ]
                },
                {
                    "fdcId": 999999,
                    "description": "Incomplete Food (Missing Carbs)",
                    "foodNutrients": [
                        { "nutrientId": 1008, "value": 100.0 },
                        { "nutrientId": 1003, "value": 10.0 },
                        { "nutrientId": 1004, "value": 5.0 }
                    ]
                }
            ]
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var items = FdcExternalFoodSource.Parse(doc.RootElement);

        Assert.Single(items);
        var item = items[0];
        Assert.Equal(173904, item.FdcId);
        Assert.Equal("Oats, rolled, dry", item.Name);
        Assert.Equal(379f, item.CaloriesPer100Grams);
        Assert.Equal(13.15f, item.ProteinPer100Grams);
        Assert.Equal(6.52f, item.FatsPer100Grams);
        Assert.Equal(67.70f, item.CarbsPer100Grams);
        Assert.Equal("Foundation", item.DataType);
    }

    [Fact]
    public void FdcExternalFoodSource_Parse_EmptyOrInvalidPayloadReturnsEmptyList()
    {
        using var emptyDoc = JsonDocument.Parse("{}");
        Assert.Empty(FdcExternalFoodSource.Parse(emptyDoc.RootElement));

        using var nonObjectDoc = JsonDocument.Parse("[]");
        Assert.Empty(FdcExternalFoodSource.Parse(nonObjectDoc.RootElement));
    }

    [Fact]
    public async Task FdcExternalFoodSource_SearchAsync_UsesPostWithJsonDataTypeArrayAndHeaderApiKey()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "foods": [
                {
                  "fdcId": 174272,
                  "description": "Tempeh",
                  "dataType": "SR Legacy",
                  "foodNutrients": [
                    { "nutrientId": 1008, "value": 192.0 },
                    { "nutrientId": 1003, "value": 20.3 },
                    { "nutrientId": 1004, "value": 10.8 },
                    { "nutrientId": 1005, "value": 7.64 }
                  ]
                }
              ]
            }
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = FdcExternalFoodSource.BaseUri };
        var source = new FdcExternalFoodSource(httpClient, "synthetic-fdc-key");

        var items = await source.SearchAsync("tempeh");

        Assert.Single(items);
        Assert.Equal(174272, items[0].FdcId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("https://api.nal.usda.gov/fdc/v1/foods/search", handler.LastUri?.ToString());
        Assert.Equal("synthetic-fdc-key", handler.LastApiKeyHeader);
        Assert.NotNull(handler.LastRequestBody);
        using var requestDoc = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("tempeh", requestDoc.RootElement.GetProperty("query").GetString());
        var dataTypes = requestDoc.RootElement.GetProperty("dataType")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(["Foundation", "SR Legacy", "Survey (FNDDS)"], dataTypes);
        Assert.Equal(15, requestDoc.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, requestDoc.RootElement.GetProperty("pageNumber").GetInt32());
    }

    [Fact]
    public async Task FdcExternalFoodSource_SearchAsync_ReturnsEmptyWhenUpstreamReturnsBadRequest()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, "<html>400 Bad Request</html>");
        using var httpClient = new HttpClient(handler) { BaseAddress = FdcExternalFoodSource.BaseUri };
        var source = new FdcExternalFoodSource(httpClient, "synthetic-fdc-key");

        var items = await source.SearchAsync("tempeh");

        Assert.Empty(items);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastUri { get; private set; }
        public string? LastApiKeyHeader { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastUri = request.RequestUri;
            LastApiKeyHeader = request.Headers.TryGetValues("X-Api-Key", out var values)
                ? values.FirstOrDefault()
                : null;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
