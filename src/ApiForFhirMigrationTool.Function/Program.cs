// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using Azure.Data.Tables;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Polly;
using Polly.Extensions.Http;

namespace ApiForFhirMigrationTool.Function
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new Configuration.MigrationOptions()
            {
                SourceFhirUri = null,
                TargetFhirUri = null,
            };

            var host = new HostBuilder()
                .ConfigureAppConfiguration((hostingContext, configuration) =>
                {
                    configuration.Sources.Clear();

                    IConfigurationRoot configRoot = configuration
                        .AddJsonFile("local.settings.json", true, true)
                        .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                        .AddEnvironmentVariables()
                        .Build();

                    // #TODO - services that require configuration should get these from DI not from here.
                    config = configRoot.GetRequiredSection("MigrationOptions").Get<Configuration.MigrationOptions>();
                })
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(services =>
            {
                SetupLogging(services, config);

                // #TODO - would it make sense to split out this large options class into smaller, logical classes?
                // Add our configuration as an IOptions. Validate.
                services
                    .AddOptions<Configuration.MigrationOptions>(nameof(Configuration.MigrationOptions))
                    .ValidateDataAnnotations()
                    .Validate(config =>
                    {
                        return config.ComplexValidate();
                    });

                services.AddTransient<OrchestrationHelper.IOrchestrationHelper, OrchestrationHelper.OrchestrationHelper>();

                // services.AddTransient<IExportProcessor, ExportProcessor>();
                // services.AddTransient<IImportProcessor, ImportProcessor>();
                services.AddTransient<Models.IMetadataStore, Models.AzureTableMetadataStore>();

                SetupMigrationStorageAccount(services, config);

                services.AddTransient<Processors.IFhirProcessor, Processors.FhirProcessor>();

                services.AddScoped<FhirOperation.IFhirClient, FhirOperation.FhirClient>();

                services.AddTransient<SearchParameterOperation.ISearchParameterOperation, SearchParameterOperation.SearchParameterOperation>();

                SetupFhirHttpClients(services, config);
            })
            .Build();

            host.Run();
        }

        internal static void SetupFhirHttpClients(IServiceCollection services, Configuration.MigrationOptions config)
        {
            var credential = config.TokenCredential;
            var sourceUri = config.SourceFhirUri!;
            var desUri = config.TargetFhirUri!;
            string[]? scopes = default;

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            var sourceFhirHttpBuilder = services.AddHttpClient(config.SourceHttpClient, httpClient =>
            {
                httpClient.DefaultRequestHeaders.Add(HeaderNames.UserAgent, config.UserAgent);
                httpClient.BaseAddress = sourceUri!;
            })
            .AddPolicyHandler(retryPolicy);

            var targetFhirHttpBuilder = services.AddHttpClient(config.DestinationHttpClient, client =>
            {
                client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, config.UserAgent);
                client.BaseAddress = desUri;
            })
            .AddPolicyHandler(retryPolicy);

            if (credential is not null)
            {
                sourceFhirHttpBuilder.AddHttpMessageHandler(x => new Security.BearerTokenHandler(credential, sourceUri!, scopes));
                targetFhirHttpBuilder.AddHttpMessageHandler(x => new Security.BearerTokenHandler(credential, desUri!, scopes));
            }
        }

        internal static void SetupMigrationStorageAccount(IServiceCollection services, Configuration.MigrationOptions config)
        {
            if (config.StagingStorageUri is not null)
            {
                services.AddSingleton(new TableServiceClient(config.StagingStorageUri, config.TokenCredential));
            }
            else if (config.StagingStorageConnectionString is not null)
            {
                services.AddSingleton(new TableClient(config.StagingStorageConnectionString, config.ExportTableName));
            }
            else
            {
                throw new ArgumentException("Either StagingStorageUri or StagingStorageConnectionString must be configured.");
            }

            services.AddTransient<Models.IAzureTableClientFactory, Models.AzureTableClientFactory>();
        }

        internal static void SetupLogging(IServiceCollection services, Configuration.MigrationOptions config)
        {
            if (!string.IsNullOrEmpty(config.AppInsightConnectionString))
            {
                services.AddLogging(builder =>
                {
                    builder.AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, config.Debug ? LogLevel.Debug : LogLevel.Information);
                    builder.AddApplicationInsights(op => op.ConnectionString = config.AppInsightConnectionString, op => op.FlushOnDispose = true);
                });

                services.Configure<TelemetryConfiguration>(options =>
                {
                    options.ConnectionString = config.AppInsightConnectionString;
                });
                services.AddTransient<TelemetryClient>();
            }
        }
    }
}
