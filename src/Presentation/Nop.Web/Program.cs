using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Nop.Core.Configuration;
using Nop.Web.Framework.Infrastructure.Extensions;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Configuration.AddJsonFile(NopConfigurationDefaults.AppSettingsFilePath, true, true);
builder.Configuration.AddEnvironmentVariables();

// Configure Kestrel server limits for high concurrency (5000+ concurrent users)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 10000; // Allow 10k concurrent connections
    options.Limits.MaxConcurrentUpgradedConnections = 10000; // WebSocket connections
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB max request body
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2); // Keep connections alive for 2 minutes
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30); // 30 second header timeout
    
    // Configure HTTP/2 settings for better performance
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

// Configure thread pool for high concurrency
ThreadPool.SetMinThreads(200, 200); // Minimum threads for I/O and worker threads
ThreadPool.SetMaxThreads(10000, 10000); // Maximum threads for high concurrency

//Add services to the application and configure service provider
builder.Services.ConfigureApplicationServices(builder);

var app = builder.Build();

//Configure the application HTTP request pipeline
app.ConfigureRequestPipeline();
app.StartEngine();

app.Run();
