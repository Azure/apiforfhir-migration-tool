// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

/*using Microsoft.Health.Fhir.Tests.Common.FixtureParameters;
using Microsoft.Health.Fhir.Tests.E2E.Common;
using Microsoft.Health.Fhir.Tests.E2E.Rest;
using Xunit;

namespace ApiForFhirMigrationTool.Function.Tests.E2E
{
    [HttpIntegrationFixtureArgumentSets(DataStore.CosmosDb, Format.Json)]
    public class MigrationTests : IClassFixture<MigrationTestFixture>
    {
        private readonly TestFhirClient _client;

        public MigrationTests(MigrationTestFixture fixture)
        {
            _client = fixture.TestFhirClient;
        }

        // [Fact]
        public async Task GivenATestServer_WhenMigrationOperation()
        {

            var migrationResponse = await _client.GetAsync("/api/start-migration");
            migrationResponse.EnsureSuccessStatusCode();
            var migrationContent = await migrationResponse.Content.ReadAsStringAsync();

            var checkResponse = await _client.GetAsync("/api/run-check-operation");
            checkResponse.EnsureSuccessStatusCode();
            var checkContent = await checkResponse.Content.ReadAsStringAsync();


            var test = await _client.SearchAsync(Hl7.Fhir.Model.ResourceType.Patient);

            Assert.NotNull(test);
        }
    }
}
*/
