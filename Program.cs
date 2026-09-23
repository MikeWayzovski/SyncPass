using System.Net.Http.Headers;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using TrimbleConnector;
using TrimbleConnector.Config;
using TrimbleConnector.Endpoints;
using TrimbleConnector.Services;

var builder = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .UseSystemd();

if (WindowsServiceHelpers.IsWindowsService())
{
    // A Windows Service starts with cwd System32. Keep wwwroot, data, and appsettings next to the exe.
    builder.UseContentRoot(AppContext.BaseDirectory);
}

IHost host = builder
    .ConfigureServices((context, services) =>
    {
        services.AddWindowsService(options =>
        {
            options.ServiceName = ProductInfo.ServiceName;
        });

        var listenUrl = DashboardListen.Resolve(context.Configuration);
        var listenPort = DashboardListen.PortOf(listenUrl);
        services.Configure<TrimbleConnectOptions>(
            context.Configuration.GetSection(TrimbleConnectOptions.SectionName));
        services.PostConfigure<TrimbleConnectOptions>(options =>
        {
            options.Port = listenPort;
        });

        services.AddHttpClient("TrimbleIdentity", client =>
        {
            client.BaseAddress = new Uri(TrimbleConnectOptions.DefaultIdentityUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddHttpClient("TrimbleConnect", (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<TrimbleConnectOptions>>().Value;
            var baseUrl = options.EffectiveApiBaseUrl;
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(15);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{ProductInfo.ServiceName}/{ProductInfo.Version}");
        });

        services.AddHttpClient("TrimbleConnectV20", (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<TrimbleConnectOptions>>().Value;
            var baseUrl = options.EffectiveApiBaseUrlV20;
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(15);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{ProductInfo.ServiceName}/{ProductInfo.Version}");
        });

        services.AddHttpClient("Transfer", client =>
        {
            client.Timeout = TimeSpan.FromHours(2);
        });

        services.AddSingleton<ITrimbleAuthService, TrimbleAuthService>();
        services.AddSingleton<AuthSetupHelper>();
        services.AddSingleton<ITrimbleApiClient, TrimbleApiClient>();
        services.AddSingleton<ILocalFileWatcher, LocalFileWatcher>();
        services.AddSingleton<SyncJobStore>();
        services.AddSingleton<SyncLogBuffer>();
        services.AddSingleton<SyncStateRepository>();
        services.AddSingleton<ProjectProvisioningService>();
        services.AddSingleton<SyncInventoryService>();
        services.AddSingleton<SyncEngine>();
        services.AddSingleton<SetupApi>();
        services.AddSingleton<SystemEndpoints>();
        services.AddSingleton(new DashboardListenState
        {
            RequestedUrl = listenUrl,
            Port = listenPort
        });
        // HttpListener binds the resolved port on all adapters (http://*:port) unless the Kestrel URL names a host.
        services.AddHostedService<SetupWebServer>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
