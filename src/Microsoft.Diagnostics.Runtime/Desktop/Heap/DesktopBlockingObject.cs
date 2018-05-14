using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
  internal class DesktopBlockingObject : BlockingObject
  {
    private readonly ulong _obj;
    private bool _locked;
    private readonly int _recursion;
    private IList<ClrThread> _waiters;
    private BlockingReason _reason;
    private ClrThread[] _owners;

    private static readonly ClrThread[] s_emptyWaiters = new ClrThread[0];

    internal void SetOwners(ClrThread[] owners)
    {
      _owners = owners;
    }

    internal void SetOwner(ClrThread owner)
    {
      _owners = new ClrThread[0];
      _owners[0] = owner;
    }

    public DesktopBlockingObject(ulong obj, bool locked, int recursion, ClrThread owner, BlockingReason reason)
    {
      _obj = obj;
      _locked = locked;
      _recursion = recursion;
      _reason = reason;
      _owners = new ClrThread[1];
      _owners[0] = owner;
    }

    public DesktopBlockingObject(ulong obj, bool locked, int recursion, BlockingReason reason, ClrThread[] owners)
    {
      _obj = obj;
      _locked = locked;
      _recursion = recursion;
      _reason = reason;
      _owners = owners;
    }

    public DesktopBlockingObject(ulong obj, bool locked, int recursion, BlockingReason reason)
    {
      _obj = obj;
      _locked = locked;
      _recursion = recursion;
      _reason = reason;
    }

    public override ulong Object => _obj;

    public override bool Taken => _locked;

    public void SetTaken(bool status)
    {
      _locked = status;
    }

    public override int RecursionCount => _recursion;

    public override IList<ClrThread> Waiters
    {
      get
      {
        if (_waiters == null)
          return s_emptyWaiters;

        return _waiters;
      }
    }

    internal void AddWaiter(ClrThread thread)
    {
      if (thread == null)
        return;

      if (_waiters == null)
        _waiters = new List<ClrThread>();

      _waiters.Add(thread);
      _locked = true;
    }

    public override BlockingReason Reason
    {
      get => _reason;
      internal set => _reason = value;
    }

    public override ClrThread Owner
    {
      get
      {
        if (!HasSingleOwner)
          throw new InvalidOperationException("BlockingObject has more than one owner.");

        return _owners[0];
      }
    }

    public override bool HasSingleOwner => _owners.Length == 1;

    public override IList<ClrThread> Owners => _owners ?? new ClrThread[0];
  }
}