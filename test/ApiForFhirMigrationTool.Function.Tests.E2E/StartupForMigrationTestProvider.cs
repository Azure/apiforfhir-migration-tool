﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

/*using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Api.Configs;
using Microsoft.Health.Fhir.Tests.E2E;

namespace ApiForFhirMigrationTool.Function.Tests.E2E
{
    public class StartupForMigrationTestProvider : StartupBaseForCustomProviders
    {
        public StartupForMigrationTestProvider(IConfiguration configuration)
            : base(configuration)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            FeatureConfiguration configuration = new FeatureConfiguration()
            {
                SupportsAnonymizedExport = true,
            };
            IOptions<FeatureConfiguration> options = Options.Create<FeatureConfiguration>(configuration);
            services.Replace(new ServiceDescriptor(typeof(IOptions<FeatureConfiguration>), options));

            // services.Add<MetricHandler>()
            //         .Singleton()
            //         .AsService<INotificationHandler<ExportTaskMetricsNotification>>();
        }
    }
}
*/
