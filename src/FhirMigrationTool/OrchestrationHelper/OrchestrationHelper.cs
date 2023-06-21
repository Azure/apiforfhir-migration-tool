// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FhirMigrationTool.OrchestrationHelper
{
    public class OrchestrationHelper : IOrchestrationHelper
    {
        public OrchestrationHelper()
        {
        }

        public string CreateImportRequest(HttpResponseMessage exportStatusResponse, string importMode)
        {
            string importRequestJson;
            try
            {
                JObject objResponse = JObject.Parse(exportStatusResponse.Content.ReadAsStringAsync().Result);
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

                if (objOutput != null)
                {
                    foreach (var item in objOutput)
                    {
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
                    }
                }

                importRequest.Add("parameter", paramArray);

                importRequestJson = importRequest.ToString(Formatting.None);
            }
            catch
            {
                throw;
            }

            return importRequestJson;
        }
    }
}
