// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.AI;

namespace Microsoft.SemanticKernel.Connectors.AI.KoboldCpp.TextCompletion;

#pragma warning disable RCS1194 // Implement exception constructors.
internal sealed class KoboldCppInvalidResponseException<T> : AIException
{
    public T? ResponseData { get; }

    public KoboldCppInvalidResponseException(T? responseData, string? message = null) : base(ErrorCodes.InvalidResponseContent, message)
    {
        this.ResponseData = responseData;
    }
}
