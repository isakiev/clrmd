// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopAppDomain : ClrAppDomain
  {
    public override ClrRuntime Runtime => _runtime;

    /// <summary>
    ///   ulong of the AppDomain.
    /// </summary>
    public override ulong Address => _address;

    /// <summary>
    ///   The AppDomain's ID.
    /// </summary>
    public override int Id => _id;

    /// <summary>
    ///   The name of the AppDomain, as specified when the domain was created.
    /// </summary>
    public override string Name => _name;
    public override IList<ClrModule> Modules => _modules;

    internal int InternalId => _internalId;

    public override string ConfigurationFile => _runtime.GetConfigFile(_address);

    public override string ApplicationBase
    {
      get
      {
        var appBase = _runtime.GetAppBase(_address);
        if (string.IsNullOrEmpty(appBase))
          return null;

        var uri = new Uri(appBase);
        try
        {
          return uri.AbsolutePath.Replace('/', '\\');
        }
        catch (InvalidOperationException)
        {
          return appBase;
        }
      }
    }

    internal DesktopAppDomain(DesktopRuntimeBase runtime, IAppDomainData data, string name)
    {
      _address = data.Address;
      _id = data.Id;
      _name = name;
      _internalId = s_internalId++;
      _runtime = runtime;
    }

    internal void AddModule(ClrModule module)
    {
      _modules.Add(module);
    }

    #region Private
    private readonly ulong _address;
    private readonly string _name;
    private readonly int _id;
    private readonly int _internalId;
    private readonly List<ClrModule> _modules = new List<ClrModule>();
    private readonly DesktopRuntimeBase _runtime;

    private static int s_internalId;
    #endregion
  }
}