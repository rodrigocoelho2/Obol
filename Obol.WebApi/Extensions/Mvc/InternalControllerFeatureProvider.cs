using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Obol.WebApi.Extensions.Mvc;

/// <summary>
/// ASP.NET Core only discovers public controllers by default; this also accepts the internal ones
/// deriving from <see cref="InternalControllerBase"/>.
/// </summary>
internal sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    /// <summary>
    /// Decides whether a type is a controller.
    /// </summary>
    /// <param name="typeInfo">The candidate type.</param>
    /// <returns>True for concrete <see cref="InternalControllerBase"/> types named "…Controller".</returns>
    protected override bool IsController(TypeInfo typeInfo) =>
        typeInfo.IsClass
        && !typeInfo.IsAbstract
        && !typeInfo.ContainsGenericParameters
        && typeof(InternalControllerBase).IsAssignableFrom(typeInfo)
        && typeInfo.Name.EndsWith("Controller", StringComparison.Ordinal);
}
