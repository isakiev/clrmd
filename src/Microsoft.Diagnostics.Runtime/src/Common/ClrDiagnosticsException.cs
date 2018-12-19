﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// Exception thrown by Microsoft.Diagnostics.Runtime unless there is a more appropriate
    /// exception subclass.
    /// </summary>
    [Serializable]
    public class ClrDiagnosticsException : Exception
    {
        internal ClrDiagnosticsException(string message, ClrDiagnosticsExceptionKind kind = ClrDiagnosticsExceptionKind.Unknown)
            : base(message)
        {
            Kind = kind;
        }

        protected ClrDiagnosticsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public ClrDiagnosticsExceptionKind Kind { get; }
    }
}