// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

// placeholder file for now
using System.Reflection;
using Azure.Identity;
using FhirMigrationTool.Configuration;
using FhirMigrationTool.DeepCheck;
using FhirMigrationTool.ExportProcess;
using FhirMigrationTool.FhirOperation;
using FhirMigrationTool.ImportProcess;
using FhirMigrationTool.OrchestrationHelper;
using FhirMigrationTool.Security;
using FhirMigrationTool.SurfaceCheck;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Net.Http.Headers;

internal class Program
{
    private static void Main(string[] args)
    {
        MigrationOptions config = new();

        var host = new HostBuilder()
            .ConfigureAppConfiguration((hostingContext, configuration) =>
            {
                configuration.Sources.Clear();
                configuration.AddJsonFile("local.settings.json", true, true)
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .AddEnvironmentVariables();

                IConfigurationRoot configurationRoot = configuration.Build();
                configurationRoot.Bind(config);
            })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        if (config.AppInsightConnectionstring != null)
        {
            services.AddLogging(builder =>
            {
                builder.AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, config.Debug ? LogLevel.Debug : LogLevel.Information);
                builder.AddApplicationInsights(op => op.ConnectionString = config.AppInsightConnectionstring, op => op.FlushOnDispose = true);
            });

            services.Configure<TelemetryConfiguration>(options =>
            {
                options.ConnectionString = config.AppInsightConnectionstring;
            });
            services.AddTransient<TelemetryClient>();
        }

        services.AddTransient<IOrchestrationHelper, OrchestrationHelper>();

        services.AddTransient<IExportProcessor, ExportProcessor>();

        services.AddTransient<IImportProcessor, ImportProcessor>();

        services.AddScoped<IFhirClient, FhirClient>();

        services.AddTransient<ISurfaceCheck, SurfaceCheck>();

        services.AddTransient<IDeepCheck, DeepCheck>();
        services.AddSingleton(config);

        var credential = new DefaultAzureCredential();
        var baseUri = config.SourceFhirUri;
        var desUri = config.DestinationFhirUri;
        string[]? scopes = default;

#pragma warning disable CS8604 // Possible null reference argument.
        services.AddHttpClient(config.SourceHttpClient, httpClient =>
        {
            httpClient.DefaultRequestHeaders.Add(HeaderNames.UserAgent, config.UserAgent);
            httpClient.BaseAddress = baseUri;
        })
        .AddHttpMessageHandler(x => new BearerTokenHandler(credential, baseUri, scopes));

#pragma warning restore CS8604 // Possible null reference argument.

#pragma warning disable CS8604 // Possible null reference argument.
        services.AddHttpClient(config.DestinationHttpClient, client =>
        {
            client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, config.UserAgent);
            client.BaseAddress = desUri;
        })
        .AddHttpMessageHandler(x => new BearerTokenHandler(credential, desUri, scopes));
#pragma warning restore CS8604 // Possible null reference argument.

    })
    .Build();

        host.Run();
    }
}
