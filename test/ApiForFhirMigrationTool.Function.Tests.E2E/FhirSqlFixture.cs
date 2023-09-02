// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Health.Fhir.Tests.Common.FixtureParameters;
using Microsoft.Health.Fhir.Tests.E2E.Rest;
using Microsoft.Health.Fhir.Web;

namespace ApiForFhirMigrationTool.Function.Tests.E2E
{
    public class FhirSqlFixture : HttpIntegrationTestFixture<Startup>
    {
        public FhirSqlFixture(TestFhirServerFactory testFhirServerFactory)
            : base(DataStore.SqlServer, Format.Json, testFhirServerFactory)
        {
            TestConfigurationHelpers.SetTestConfiguration();
        }
    }
}
