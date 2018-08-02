namespace Microsoft.Diagnostics.Runtime.ICorDebug
{
  public enum CorDebugExceptionCallbackType
  {
    // Fields
    DEBUG_EXCEPTION_CATCH_HANDLER_FOUND = 3,
    DEBUG_EXCEPTION_FIRST_CHANCE = 1,
    DEBUG_EXCEPTION_UNHANDLED = 4,
    DEBUG_EXCEPTION_USER_FIRST_CHANCE = 2
  }
}