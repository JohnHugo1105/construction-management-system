using Cms.Api.Startup;

var builder = WebApplication.CreateBuilder(args);

// CMS_DATABASE_CONNECTION is the documented way to supply the connection string (see .env.example); the standard
// ConnectionStrings__Cms setting works too. No connection string with a password is ever committed.
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Cms")) &&
    Environment.GetEnvironmentVariable("CMS_DATABASE_CONNECTION") is { Length: > 0 } fromEnvironment)
    builder.Configuration["ConnectionStrings:Cms"] = fromEnvironment;

builder.Services.AddCmsApi(builder.Configuration, builder.Environment);

var app = builder.Build();
if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(app.Configuration["DataProtection:KeysPath"]))
    app.Logger.LogWarning("DataProtection:KeysPath is not set, so session keys are lost on every restart and everyone is signed out. " +
        "Point it at a persistent folder (docs/operations/deployment.md).");
app.UseCmsPipeline();
app.MapCmsEndpoints();
await app.MigrateIfRequestedAsync();
app.Run();

/// <summary>Made public so integration tests can start the application with WebApplicationFactory.</summary>
public partial class Program;
