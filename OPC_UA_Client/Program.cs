using Opc.Ua;
using OPC_UA_Client.Components;
using OPC_UA_Client.Services;
using Serilog;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Serilog logger.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // Add Serilog to the logging pipeline
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var userTokenType = Enum.Parse<UserTokenType>(configuration["OpcUaServerSettings:UserTokenType"]!);
            return new OpcUaSessionProvider(configuration, userTokenType);
        });

        builder.Services.AddTransient<OpcUaPollingService>();
        builder.Services.AddTransient<OpcUaSubscriptionService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}