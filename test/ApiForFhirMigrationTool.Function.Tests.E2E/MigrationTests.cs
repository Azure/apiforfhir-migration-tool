// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Health.Fhir.Tests.E2E.Common;
using Xunit;
using FhirModel = Hl7.Fhir.Model;

namespace ApiForFhirMigrationTool.Function.Tests.E2E;

public class MigrationTests : IClassFixture<FhirCosmosFixture>, IClassFixture<FhirSqlFixture>, IClassFixture<MigrationFunctionTestFixture>, IAsyncLifetime
{
    private readonly TestFhirClient _fhirCosmosClient;
    private readonly TestFhirClient _fhirSqlClient;
    private readonly HttpClient _functionClient;

    public MigrationTests(FhirCosmosFixture fhirCosmosFixture, FhirSqlFixture fhirSqlFixture, MigrationFunctionTestFixture migrationFixture)
    {
        _fhirCosmosClient = fhirCosmosFixture.TestFhirClient;
        _fhirSqlClient = fhirSqlFixture.TestFhirClient;
        _functionClient = migrationFixture.FunctionClient;
    }

    // Runs before every test.
    public async Task InitializeAsync()
    {
        var createBundle = Samples.GetJsonSample<FhirModel.Bundle>("Bundle_Create_353_Resources");
        await _fhirCosmosClient.PostBundleAsync(createBundle);
    }

    // Runs after every test.
    public async Task DisposeAsync()
    {
        var deleteBundle = Samples.GetJsonSample<FhirModel.Bundle>("Bundle_Delete_353_Resources");
        await _fhirCosmosClient.PostBundleAsync(deleteBundle);
    }

    [Fact]
    public async Task GivenATestEnvironment_WhenTestFixturesAreInitialized_ClientsShouldOperateCorrectly()
    {
        // We should get a response from the function host.
        var functionHttpTest = await _functionClient.GetAsync("/admin/host/status");
        Assert.True(functionHttpTest.IsSuccessStatusCode);

        // The source server should have all 353 resources. Check by searching root of server.
        var cosmosFhirTestResourceCount = await _fhirCosmosClient.SearchAsync("/?_summary=count");
        Assert.Equal(353, cosmosFhirTestResourceCount.Resource.Total);

        // The destination server should be empty
        var sqlFhirTestResourceCount = await _fhirSqlClient.SearchAsync("/?_summary=count");
        Assert.Equal(0, sqlFhirTestResourceCount.Resource.Total);
    }
}
