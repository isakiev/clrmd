using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal struct DomainContainer
  {
    public List<ClrAppDomain> Domains;
    public DesktopAppDomain System;
    public DesktopAppDomain Shared;
  }
}