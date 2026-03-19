public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connStr = builder.Configuration.GetConnectionString("Default") ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? "Host=postgres;Port=5432;Database=rag_demo;Username=postgres;Password=postgres";
        builder.Services.AddControllers();

        builder.Services.AddSingleton(new DatabaseOptions(connStr));
        builder.Services.AddSingleton<IDocumentRepository, PostgresDocumentRepository>();
        builder.Services.AddSingleton<AzureFoundryClient>();

        var app = builder.Build();

        var repository = app.Services.GetRequiredService<IDocumentRepository>();
        await repository.EnsureDatabaseReadyAsync();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapControllers();

        await app.RunAsync();
    }
}
