using System;

namespace Microsoft.Diagnostics.Runtime
{
  /// <summary>
  ///   Indicates that the value of the marked element could be <c>null</c> sometimes,
  ///   so the check for <c>null</c> is necessary before its usage.
  /// </summary>
  [AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property |
    AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Event |
    AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.GenericParameter)]
  internal sealed class CanBeNullAttribute : Attribute
  {
  }

  /// <summary>
  ///   Indicates that the value of the marked element could never be <c>null</c>.
  /// </summary>
  [AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property |
    AttributeTargets.Delegate | AttributeTargets.Field | AttributeTargets.Event |
    AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.GenericParameter)]
  internal sealed class NotNullAttribute : Attribute
  {
  }
}