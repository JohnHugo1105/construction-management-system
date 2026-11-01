using Cms.Api.Startup;

var builder = WebApplication.CreateBuilder(args);

// CMS_DATABASE_CONNECTION is the documented way to supply the connection string (see .env.example); the standard
// ConnectionStrings__Cms setting works too. No connection string with a password is ever committed.
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Cms")) &&
    Environment.GetEnvironmentVariable("CMS_DATABASE_CONNECTION") is { Length: > 0 } fromEnvironment)
    builder.Configuration["ConnectionStrings:Cms"] = fromEnvironment;

builder.Services.AddCmsApi(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseCmsPipeline();
app.MapCmsEndpoints();
await app.MigrateIfRequestedAsync();
app.Run();

/// <summary>Made public so integration tests can start the application with WebApplicationFactory.</summary>
public partial class Program;
