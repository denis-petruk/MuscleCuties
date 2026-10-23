using Amazon.Lambda.AspNetCoreServer.Hosting;
using MuscleCuties.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
var app = ApiApplication.Build(builder);
await app.RunAsync();

public partial class Program;
