using Opc.Ua;
using OPC_UA_Client.Components;
using OPC_UA_Client.Models;
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
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // Add Serilog to the logging pipeline
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.Configure<OpcUaSettings>(builder.Configuration.GetSection("OpcUaSettings"));

        builder.Services.AddScoped<OpcUaSessionProvider>();

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