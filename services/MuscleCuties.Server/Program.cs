using Amazon.Lambda.AspNetCoreServer.Hosting;
using MuscleCuties.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
var app = ApiApplication.Build(builder);
await app.RunAsync();

/// <summary>The host's entry point, exposed as a partial type so the integration tests can start the real app.</summary>
public partial class Program;
