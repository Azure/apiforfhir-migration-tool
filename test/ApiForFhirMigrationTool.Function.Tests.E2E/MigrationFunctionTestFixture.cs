// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace ApiForFhirMigrationTool.Function.Tests.E2E;

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using Xunit;

public class MigrationFunctionTestFixture : IAsyncLifetime
{
    private readonly HttpClient _httpClient;

    private readonly int _port = 7071;

    private readonly string _functionProjectDir;

    private Process? _application = null;

    public MigrationFunctionTestFixture()
    {
        _functionProjectDir = TestConfigurationHelpers.GetProjectPath("src", typeof(ApiForFhirMigrationTool.Function.Program));
        TestConfigurationHelpers.SetTestConfiguration();

        _httpClient = new()
        {
            BaseAddress = new Uri($"http://localhost:{_port}"),
        };
    }

    public HttpClient FunctionClient => _httpClient;

    public string? TestMigrationFunctionUrl { get; set; }

    private Process StartApplication(string projectDirectory, int port, string buildConfiguration = "Debug", string targetFramework = "net7.0")
    {
        var appInfo = new ProcessStartInfo("func", $"start --port 7071 --prefix bin/Debug/net7.0")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = projectDirectory,
        };

        var app = new Process { StartInfo = appInfo };
        app.Start();
        return app;
    }

    private async Task<bool> IsFunctionAppImmediatelyAvailable(string endpoint)
    {
        try
        {
            var result = await _httpClient.GetAsync(endpoint);
            return result.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task WaitUntilTriggerIsAvailableAsync(string endpoint)
    {
        AsyncRetryPolicy retryPolicy =
                Policy.Handle<Exception>()
                      .WaitAndRetryForeverAsync(index => TimeSpan.FromMilliseconds(500));

        PolicyResult<HttpResponseMessage> result =
            await Policy.TimeoutAsync(TimeSpan.FromSeconds(30))
                        .WrapAsync(retryPolicy)
                        .ExecuteAndCaptureAsync(() => _httpClient.GetAsync(endpoint));

        if (result.Outcome == OutcomeType.Failure)
        {
            throw new InvalidOperationException(
                "The Azure Functions project doesn't seem to be running, "
                + "please check any build or runtime errors that could occur during startup");
        }
    }

    public async Task InitializeAsync()
    {
        TestMigrationFunctionUrl = Environment.GetEnvironmentVariable("TestMigrationFunctionUrl");

        // If the endpoint is not set, we will use the local version of our function.
        if (string.IsNullOrEmpty(TestMigrationFunctionUrl))
        {
            TestMigrationFunctionUrl = $"http://localhost:7071/admin/host/status";

            // If the function app isn't already started in the debugger.
            if (!await IsFunctionAppImmediatelyAvailable(TestMigrationFunctionUrl))
            {
                _application = StartApplication(_functionProjectDir, _port);
                await WaitUntilTriggerIsAvailableAsync(TestMigrationFunctionUrl);
                await OnInitializedAsync();
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
        {
            if (!_application.HasExited)
            {
                _application.Kill(entireProcessTree: true);
            }

            _application?.Dispose();
        }

        _httpClient.Dispose();

        await OnDisposedAsync();
    }

    protected virtual Task OnInitializedAsync() => Task.CompletedTask;

    protected virtual Task OnDisposedAsync() => Task.CompletedTask;
}
