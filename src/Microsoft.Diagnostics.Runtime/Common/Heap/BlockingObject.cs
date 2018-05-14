﻿using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   Represents a managed lock within the runtime.
  /// </summary>
  public abstract class BlockingObject
  {
    /// <summary>
    ///   The object associated with the lock.
    /// </summary>
    public abstract ulong Object { get; }

    /// <summary>
    ///   Whether or not the object is currently locked.
    /// </summary>
    public abstract bool Taken { get; }

    /// <summary>
    ///   The recursion count of the lock (only valid if Locked is true).
    /// </summary>
    public abstract int RecursionCount { get; }

    /// <summary>
    ///   The thread which currently owns the lock.  This is only valid if Taken is true and
    ///   only valid if HasSingleOwner is true.
    /// </summary>
    public abstract ClrThread Owner { get; }

    /// <summary>
    ///   Returns true if this lock has only one owner.  Returns false if this lock
    ///   may have multiple owners (for example, readers on a RW lock).
    /// </summary>
    public abstract bool HasSingleOwner { get; }

    /// <summary>
    ///   Returns the list of owners for this object.
    /// </summary>
    public abstract IList<ClrThread> Owners { get; }

    /// <summary>
    ///   Returns the list of threads waiting on this object.
    /// </summary>
    public abstract IList<ClrThread> Waiters { get; }

    /// <summary>
    ///   The reason why it's blocking.
    /// </summary>
    public abstract BlockingReason Reason { get; internal set; }
  }
}