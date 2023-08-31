// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function.Tests.E2E;

public static class TestConfigurationHelpers
{
    public static void SetTestConfiguration()
    {
        var functionStartypType = typeof(ApiForFhirMigrationTool.Function.Program);
        var functionProjectDir = GetProjectPath("src", functionStartypType);
        var testConfigPath = Path.GetFullPath("testconfiguration.json");

        // Setup test specific configuration.
        Dictionary<string, string?> configuration = GetLaunchSettings(functionProjectDir);

        foreach (var p in GetTestConfiguration(testConfigPath))
        {
            if (p.Value is not null)
            {
                configuration.Remove(p.Key);
                configuration.Add(p.Key, p.Value);
            }
        }

        // Only set if not in environment already. This allows users to override built in test config.
        foreach (var kvp in configuration)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("kvp.Key")))
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }
    }

    internal static Dictionary<string, string?> GetLaunchSettings(string projectDir)
    {
        Dictionary<string, string?> config = new();
        string launchSettingsPath = Path.Combine(projectDir, "Properties", "launchSettings.json");

        if (File.Exists(launchSettingsPath))
        {
            var launchSettings = JObject.Parse(File.ReadAllText(launchSettingsPath));

            // Assuming all of these are here since the file is part of this project.
            config = launchSettings["profiles"]!["FhirMigrationTool"]!["environmentVariables"]!
                .Cast<JProperty>().ToDictionary(p => p.Name, p => p.Value is null ? null : p.Value.ToString());
        }

        return config;
    }

    internal static Dictionary<string, string?> GetTestConfiguration(string testConfigFilePath)
    {
        if (File.Exists(testConfigFilePath))
        {
            string jsonContent = File.ReadAllText(testConfigFilePath);
            return JsonConvert.DeserializeObject<Dictionary<string, string?>>(jsonContent)!;
        }
        else
        {
            throw new FileNotFoundException($"Configuration file '{testConfigFilePath}' not found.");
        }
    }

    /// <summary>
    /// Gets the full path to the target project that we wish to test
    /// </summary>
    /// <param name="projectRelativePath">
    /// The parent directory of the target project.
    /// e.g. src, samples, test, or test/Websites
    /// </param>
    /// <param name="type">The startup type</param>
    /// <returns>The full path to the target project.</returns>
    internal static string GetProjectPath(string projectRelativePath, Type type)
    {
        // Get name of the target project which we want to test
        var projectName = type.GetTypeInfo().Assembly.GetName().Name;

        // Get currently executing test project path
        var applicationBasePath = AppContext.BaseDirectory;

        // Find the path to the target project
        var directoryInfo = new DirectoryInfo(applicationBasePath);

        do
        {
            directoryInfo = directoryInfo.Parent;

            var projectDirectoryInfo = new DirectoryInfo(Path.Combine(directoryInfo!.FullName, projectRelativePath));
            if (projectDirectoryInfo.Exists)
            {
                var projectFileInfo = new FileInfo(Path.Combine(projectDirectoryInfo.FullName, projectName!, $"{projectName}.csproj"));
                if (projectFileInfo.Exists)
                {
                    return Path.Combine(projectDirectoryInfo.FullName, projectName!);
                }
            }
        }
        while (directoryInfo.Parent != null);

        throw new InvalidOperationException($"Project root could not be located for startup type {type.FullName}");
    }
}
