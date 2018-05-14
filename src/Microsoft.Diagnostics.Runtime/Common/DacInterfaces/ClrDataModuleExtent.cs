// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable 649

namespace Microsoft.Diagnostics.Runtime
{
  internal struct CLRDATA_MODULE_EXTENT
  {
    public ulong baseAddress;
    public uint length;
    public ModuleExtentType type;
  }
}