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

// Configure Kestrel server limits for high concurrency (10000+ concurrent users)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 20000; // Allow 20k concurrent connections (supports burst traffic)
    options.Limits.MaxConcurrentUpgradedConnections = 20000; // WebSocket connections
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB max request body
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2); // Keep connections alive for 2 minutes
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30); // 30 second header timeout
    options.Limits.MaxRequestBufferSize = 1024 * 1024; // 1 MB request buffer
    options.Limits.MaxResponseBufferSize = 64 * 1024; // 64 KB response buffer
    
    // Configure HTTP/2 settings for better performance
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
    
    // Optimize HTTP/2 settings for high concurrency
    options.Limits.Http2.MaxStreamsPerConnection = 100; // Allow 100 streams per HTTP/2 connection
    options.Limits.Http2.HeaderTableSize = 4096; // Header table size
    options.Limits.Http2.MaxFrameSize = 16384; // Max frame size
});

// Configure thread pool for high concurrency (10000+ users)
ThreadPool.SetMinThreads(500, 500); // Increased minimum threads for faster ramp-up
ThreadPool.SetMaxThreads(20000, 20000); // Maximum threads for 10000+ concurrent users

//Add services to the application and configure service provider
builder.Services.ConfigureApplicationServices(builder);

var app = builder.Build();

//Configure the application HTTP request pipeline
app.ConfigureRequestPipeline();
app.StartEngine();

app.Run();
