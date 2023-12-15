// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using ApiForFhirMigrationTool.Function.Configuration;
using ApiForFhirMigrationTool.Function.DeepCheck;
using ApiForFhirMigrationTool.Function.FhirOperation;
using ApiForFhirMigrationTool.Function.Models;
using ApiForFhirMigrationTool.Function.OrchestrationHelper;
using ApiForFhirMigrationTool.Function.Processors;
using ApiForFhirMigrationTool.Function.SearchParameterOperation;
using ApiForFhirMigrationTool.Function.Security;
using ApiForFhirMigrationTool.Function.SurfaceCheck;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Net.Http.Headers;
using Polly;
using Polly.Extensions.Http;

public class Program
{
    private static void Main(string[] args)
    {
        MigrationOptions config = new();

        var host = new HostBuilder()
            .ConfigureAppConfiguration((hostingContext, configuration) =>
            {
                configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .AddEnvironmentVariables("AZURE_");

                IConfigurationRoot configurationRoot = configuration.Build();
                configurationRoot.Bind(config);
            })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        var credential = new DefaultAzureCredential();
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

        // services.AddTransient<IExportProcessor, ExportProcessor>();
        // services.AddTransient<IImportProcessor, ImportProcessor>();

        services.AddTransient<ISurfaceCheck, SurfaceCheck>();
        services.AddTransient<IDeepCheck, DeepCheck>();

        services.AddTransient<IAzureTableClientFactory, AzureTableClientFactory>();
        services.AddTransient<IMetadataStore, AzureTableMetadataStore>();

        TableClientOptions opts = new TableClientOptions(TableClientOptions.ServiceVersion.V2019_02_02);
        opts.Retry.Delay = TimeSpan.FromSeconds(5);
        opts.Retry.Mode = RetryMode.Fixed;
        opts.Retry.MaxRetries = 3;

        services.AddSingleton<TableServiceClient>(new TableServiceClient(new Uri(config.StagingStorageUri), credential, opts));

        services.AddTransient<IFhirProcessor, FhirProcessor>();

        services.AddScoped<IFhirClient, FhirClient>();

        services.AddTransient<ISearchParameterOperation, SearchParameterOperation>();
        services.AddSingleton(config);

        var baseUri = config.SourceUri;
        var desUri = config.DestinationUri;
        string[]? scopes = default;

#pragma warning disable CS8604 // Possible null reference argument.
        services.AddHttpClient(config.SourceHttpClient, httpClient =>
        {
            httpClient.DefaultRequestHeaders.Add(HeaderNames.UserAgent, config.UserAgent);
            httpClient.BaseAddress = baseUri;
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddHttpMessageHandler(x => new BearerTokenHandler(credential, baseUri, scopes));

#pragma warning restore CS8604 // Possible null reference argument.

#pragma warning disable CS8604 // Possible null reference argument.
        services.AddHttpClient(config.DestinationHttpClient, client =>
        {
            client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, config.UserAgent);
            client.BaseAddress = desUri;
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddHttpMessageHandler(x => new BearerTokenHandler(credential, desUri, scopes));
#pragma warning restore CS8604 // Possible null reference argument.

    })
    .Build();

        host.Run();
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
