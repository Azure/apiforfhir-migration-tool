// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;

public class FunctionTestFixture : IDisposable
{
    public FunctionTestFixture()
    {
        Host = new HostBuilder()
            .ConfigureDefaultTestHost<Program>(b =>
            {
                b.AddAzureStorageBlobs();

                // Add other extensions or configuration as needed
            })
            .ConfigureServices(services =>
            {
                // Register any additional services needed for testing
            })
            .Build();

        Host.StartAsync().GetAwaiter().GetResult();
    }

    public IHost Host { get; private set; }

    public void Dispose()
    {
        Host.StopAsync().GetAwaiter().GetResult();
        Host.Dispose();
    }
}
