using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Obol.Library.Enums;
using Obol.Library.Validation;
using Obol.WebApi.Extensions.Mvc;
using Obol.WebApi.Extensions.Services;
using Obol.WebApi.Features.Portfolio.Models.DTOs;
using Obol.WebApi.Features.Portfolio.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Obol.WebApi.Features.Portfolio.Controllers;

/// <summary>The portfolio screens: Overview, Holdings, a position, the activity and Income.</summary>
/// <param name="portfolioService">The portfolio service.</param>
[Authorize]
[Route("portfolio")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class PortfolioController(IPortfolioService portfolioService) : InternalControllerBase
{
    /// <summary>The Overview: value, chart, allocation, movers and income.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="range">The chart range.</param>
    /// <returns>The overview.</returns>
    [HttpGet("overview")]
    [ProducesResponseType<OverviewDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PortfolioOverview", OperationId = "PortfolioOverview")]
    public async Task<ActionResult<OverviewDTO>> PortfolioOverviewAsync(string userId, ChartRange range = ChartRange.Year) =>
        (await portfolioService.OverviewAsync(userId, range)).ToActionResult();

    /// <summary>The open positions.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The holdings.</returns>
    [HttpGet("holdings")]
    [ProducesResponseType<HoldingsDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PortfolioHoldings", OperationId = "PortfolioHoldings")]
    public async Task<ActionResult<HoldingsDTO>> PortfolioHoldingsAsync(string userId) => (await portfolioService.HoldingsAsync(userId)).ToActionResult();

    /// <summary>One position, with its chart and activity.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="key">The instrument key (ISIN or T212:ticker).</param>
    /// <param name="range">The chart range.</param>
    /// <returns>The position.</returns>
    [HttpGet("position")]
    [ProducesResponseType<PositionDetailDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PortfolioPosition", OperationId = "PortfolioPosition")]
    public async Task<ActionResult<PositionDetailDTO>> PortfolioPositionAsync(string userId, string key, ChartRange range = ChartRange.Year) =>
        (await portfolioService.PositionAsync(userId, key, range)).ToActionResult();

    /// <summary>A page of the activity.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="kind">Only one kind.</param>
    /// <param name="skip">Lines to skip.</param>
    /// <param name="take">Lines to return.</param>
    /// <returns>The page.</returns>
    [HttpGet("activity")]
    [ProducesResponseType<ActivityPageDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PortfolioActivity", OperationId = "PortfolioActivity")]
    public async Task<ActionResult<ActivityPageDTO>> PortfolioActivityAsync(string userId, ActivityKind? kind = null, int skip = 0, int take = 50) =>
        (await portfolioService.ActivityAsync(userId, kind, skip, take)).ToActionResult();

    /// <summary>The Income screen of a year.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="year">The year (this one when empty).</param>
    /// <returns>The income.</returns>
    [HttpGet("income")]
    [ProducesResponseType<IncomeDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PortfolioIncome", OperationId = "PortfolioIncome")]
    public async Task<ActionResult<IncomeDTO>> PortfolioIncomeAsync(string userId, int? year = null) =>
        (await portfolioService.IncomeAsync(userId, year)).ToActionResult();
}
