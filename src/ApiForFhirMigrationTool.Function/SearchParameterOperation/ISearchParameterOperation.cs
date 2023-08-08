// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Newtonsoft.Json.Linq;

namespace ApiForFhirMigrationTool.Function.SearchParameterOperation
{
    public interface ISearchParameterOperation
    {
        Task<JObject> GetSearchParameters();

        string TransformObject(JObject searchParameterObject);

        Task PostSearchParameters(string requestContent);
    }
}
