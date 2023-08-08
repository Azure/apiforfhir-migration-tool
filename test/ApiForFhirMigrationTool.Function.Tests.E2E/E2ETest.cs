// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Xunit;

namespace FhirMigrationToolE2E.E2E
{
    public class E2ETest
    {
        private readonly HttpClient _client;

        public E2ETest(FunctionTestFixture fixture)
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:7071"),
            };
        }

        [Fact]
        public async Task GivenATestServer_WhenMigrationOperation()
        {
            var migrationResponse = await _client.GetAsync("/api/start-migration");
            migrationResponse.EnsureSuccessStatusCode();
            var migrationContent = await migrationResponse.Content.ReadAsStringAsync();

            var checkResponse = await _client.GetAsync("/api/run-check-operation");
            checkResponse.EnsureSuccessStatusCode();
            var checkContent = await checkResponse.Content.ReadAsStringAsync();

            // Assert that the content is what you expect
            Assert.Equal("Expected Result", migrationContent);
            Assert.Equal("Expected Result", checkContent);
        }
    }
}
