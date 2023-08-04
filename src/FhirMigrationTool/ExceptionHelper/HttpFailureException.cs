// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace FhirMigrationTool.ExceptionHelper
{
    internal class HttpFailureException : Exception
    {
        public HttpFailureException()
        {
        }

        public HttpFailureException(string message)
            : base(message)
        {
        }

        public HttpFailureException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
