namespace NTSW_CENOPS.Components.Common.Notices;

/// <summary>An expected rule/validation failure, whose Persian message is safe for users.</summary>
public sealed class AppActionRejectedException(string message) : Exception(message);
