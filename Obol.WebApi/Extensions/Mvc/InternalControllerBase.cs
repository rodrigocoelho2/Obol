using Microsoft.AspNetCore.Mvc;

namespace Obol.WebApi.Extensions.Mvc;

/// <summary>
/// Base class of every WebApi controller. Controllers are internal: they are discovered by
/// <see cref="InternalControllerFeatureProvider"/>, not exposed as public types.
/// </summary>
internal abstract class InternalControllerBase : ControllerBase
{
}
