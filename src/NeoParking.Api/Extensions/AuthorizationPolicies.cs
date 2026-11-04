namespace NeoParking.Api.Extensions;

using Microsoft.AspNetCore.Authorization;
using NeoParking.Management.Domain;

/// <summary>
/// Policies da matriz de autorização (decisão D2 em docs/specs/technical-debt-remediation.md).
/// </summary>
public static class AuthorizationPolicies
{
    public const string AnyOperator = nameof(AnyOperator);
    public const string OwnerOrAdmin = nameof(OwnerOrAdmin);
    public const string Owner = nameof(Owner);

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(AnyOperator, policy => policy.RequireRole(
            nameof(OperatorRole.Operator), nameof(OperatorRole.Admin), nameof(OperatorRole.Owner)));
        options.AddPolicy(OwnerOrAdmin, policy => policy.RequireRole(
            nameof(OperatorRole.Admin), nameof(OperatorRole.Owner)));
        options.AddPolicy(Owner, policy => policy.RequireRole(nameof(OperatorRole.Owner)));
    }
}
