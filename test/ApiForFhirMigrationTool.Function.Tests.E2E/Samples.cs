// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using Hl7.Fhir.Model;
using Microsoft.Health.Fhir.Core.Models;
using Newtonsoft.Json;

namespace ApiForFhirMigrationTool.Function.Tests.E2E;

public class Samples
{
    private const string EmbeddedResourceSubNamespace = "TestFiles";

    /// <summary>
    /// Gets back a resource from a json sample file.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="fileName">The JSON filename, omit the extension</param>
    public static T? GetJsonSample<T>(string fileName)
    {
        var json = GetJson(fileName);
        if (typeof(Resource).IsAssignableFrom(typeof(T)))
        {
            var parser = new Hl7.Fhir.Serialization.FhirJsonParser();
            return (T)(object)parser.Parse(json, typeof(T));
        }

        return JsonConvert.DeserializeObject<T>(json);
    }

    /// <summary>
    /// Gets back a the string from a sample file
    /// </summary>
    /// <param name="fileName">The JSON filename, omit the extension</param>
    public static string GetJson(string fileName)
    {
        return Samples.GetStringContent(fileName, "json");
    }

    /// <summary>
    /// Gets the string content of an embedded resource.
    /// </summary>
    /// <param name="fileName">Filename of the file (without folder structure or extension.</param>
    /// <param name="extension">Extension of the file</param>
    /// <returns>String content of the resource.</returns>
    public static string GetStringContent(string fileName, string extension)
    {
        string resourceName = $"{typeof(Samples).Namespace}.{EmbeddedResourceSubNamespace}.{fileName}.{extension}";

        var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

        if (resourceStream is null)
        {
            throw new ArgumentException($"Cannot find stream from resource {resourceName}.");
        }

        using (Stream stream = resourceStream)
        {
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
