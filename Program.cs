using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using TrimbleConnector;
using TrimbleConnector.Config;
using TrimbleConnector.Endpoints;
using TrimbleConnector.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .UseSystemd()
    .ConfigureServices((context, services) =>
    {
        services.AddWindowsService(options =>
        {
            options.ServiceName = ProductInfo.ServiceName;
        });

        services.Configure<TrimbleConnectOptions>(
            context.Configuration.GetSection(TrimbleConnectOptions.SectionName));

        services.AddHttpClient("TrimbleIdentity", client =>
        {
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
        services.AddSingleton<SyncEngine>();
        services.AddSingleton<SetupApi>();
        services.AddHostedService<SetupWebServer>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
