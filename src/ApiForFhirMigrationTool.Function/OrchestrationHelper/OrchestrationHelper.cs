// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Azure.Storage.Blobs;
using System;
using ApiForFhirMigrationTool.Function.Configuration;
using Azure.Identity;
using ApiForFhirMigrationTool.Function.Models;

namespace ApiForFhirMigrationTool.Function.OrchestrationHelper
{
    public class OrchestrationHelper : IOrchestrationHelper
    {
        private readonly MigrationOptions _options;
        private readonly IAzureBlobClientFactory _azureBlobClientFactory;
        public OrchestrationHelper(MigrationOptions options, IAzureBlobClientFactory azureBlobClientFactory)
        {
            _options = options;
            _azureBlobClientFactory = azureBlobClientFactory;
        }

        public int CreateImportRequest(string content, string importMode, string statusUrl)
        {
            int fileCount = _options.FileCount;
            string statusId = GetProcessId(statusUrl);
            int importPayloadCount = 1;
            try
            {
                JObject objResponse = JObject.Parse(content);
                var objOutput = objResponse["output"];
                JObject importRequest = new JObject();
                importRequest.Add("resourceType", "Parameters");
                JArray paramArray = new JArray();
                JObject inputFormat = new JObject();
                inputFormat.Add("name", "inputFormat");
                inputFormat.Add("valueString", "application/fhir+ndjson");
                JObject mode = new JObject();
                mode.Add("name", "mode");
                mode.Add("valueString", importMode);

                paramArray.Add(inputFormat);
                paramArray.Add(mode);
                int counter = 0;


                if (objOutput != null)
                {
                    foreach (var item in objOutput)
                    {
                        if (item?["type"]?.ToString() != "SearchParameter")
                        {
                            if (counter == fileCount)
                            {
                                importRequest.Add("parameter", paramArray);
                                SaveImportRequestToFile(importRequest, importPayloadCount, statusId);
                                importRequest = new JObject();
                                importRequest.Add("resourceType", "Parameters");
                                paramArray = new JArray();
                                paramArray.Add(inputFormat);
                                paramArray.Add(mode);

                                counter = 0;
                                importPayloadCount++;
                            }
                            JObject input = new JObject();
                            input.Add("name", "input");
                            JArray partArray = new JArray();
                            JObject type = new JObject();
                            type.Add("name", "type");
                            type.Add("valueString", item["type"]);
                            partArray.Add(type);
                            JObject url = new JObject();
                            url.Add("name", "url");
                            url.Add("valueString", item["url"]);
                            partArray.Add(url);

                            input.Add("part", partArray);
                            paramArray.Add(input);
                            counter++;
                        }
                    }
                    importRequest.Add("parameter", paramArray);
                    SaveImportRequestToFile(importRequest, importPayloadCount, statusId);
                }

            }
            catch
            {
                throw;
            }
            return importPayloadCount;
        }

        public void SaveImportRequestToFile(JObject importRequest, int importPayloadCount, string statusId)
        {
            try
            {
                string importRequestJson = importRequest.ToString();
                string containerName = $"import-{statusId}";
                BlobContainerClient containerClient = _azureBlobClientFactory.GetBlobContainerClient(containerName);

                if (!containerClient.Exists())
                {
                    containerClient = _azureBlobClientFactory.Create(containerName);
                }
                string fileName = $"import_payload_{importPayloadCount}.json";
                BlobClient blobClient = containerClient.GetBlobClient(fileName);
                using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(importRequestJson)))
                {
                    blobClient.Upload(ms, true);
                }

                Console.WriteLine($"Creation of Import body {fileName} is completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while saving the import request to Azure Blob Storage: {ex.Message}");
            }
        }

        public string GetProcessId(string statusUrl)
        {
            var array = statusUrl.Split('/');
            return array.Last();
        }

        public ulong CalculateSumOfResources(JArray output)
        {
            ulong sum = 0;
            foreach (JObject obj in output)
            {
                var countToken = obj["count"];
                if (countToken != null)
                {
                    sum += countToken.Value<ulong>();
                }
            }

            return sum;
        }
    }
}

