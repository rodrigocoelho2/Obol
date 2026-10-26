using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Obol.Library.Enums;
using Obol.Library.Validation;
using Obol.WebApi.Extensions.Mvc;
using Obol.WebApi.Extensions.Services;
using Obol.WebApi.Features.Insights.Models.DTOs;
using Obol.WebApi.Features.Insights.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Obol.WebApi.Features.Insights.Controllers;

/// <summary>The Insights screens: performance, exposure, realized gains, goals and the AI coach.</summary>
/// <param name="insightsService">The insights service.</param>
/// <param name="coachService">The AI coach.</param>
[Authorize]
[Route("insights")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class InsightsController(IInsightsService insightsService, ICoachService coachService) : InternalControllerBase
{
    /// <summary>Money- and time-weighted returns, against MSCI World and the S&amp;P 500.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="range">The range.</param>
    /// <returns>The performance.</returns>
    [HttpGet("performance")]
    [ProducesResponseType<PerformanceDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "InsightsPerformance", OperationId = "InsightsPerformance")]
    public async Task<ActionResult<PerformanceDTO>> InsightsPerformanceAsync(string userId, ChartRange range = ChartRange.Year) =>
        (await insightsService.PerformanceAsync(userId, range)).ToActionResult();

    /// <summary>Currencies (with the exchange rate's effect), sectors, countries and concentration.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The exposure.</returns>
    [HttpGet("exposure")]
    [ProducesResponseType<ExposureDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "InsightsExposure", OperationId = "InsightsExposure")]
    public async Task<ActionResult<ExposureDTO>> InsightsExposureAsync(string userId) => (await insightsService.ExposureAsync(userId)).ToActionResult();

    /// <summary>What the sales of a year made.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="year">The year (this one when empty).</param>
    /// <returns>The realized gains.</returns>
    [HttpGet("realized")]
    [ProducesResponseType<RealizedDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "InsightsRealized", OperationId = "InsightsRealized")]
    public async Task<ActionResult<RealizedDTO>> InsightsRealizedAsync(string userId, int? year = null) =>
        (await insightsService.RealizedAsync(userId, year)).ToActionResult();

    /// <summary>Where the portfolio is now, for the projection.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The goals' starting point.</returns>
    [HttpGet("goals")]
    [ProducesResponseType<GoalsDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "InsightsGoals", OperationId = "InsightsGoals")]
    public async Task<ActionResult<GoalsDTO>> InsightsGoalsAsync(string userId) => (await insightsService.GoalsAsync(userId)).ToActionResult();

    /// <summary>The AI coach: set up or not, running, and the last review.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The coach.</returns>
    [HttpGet("coach")]
    [ProducesResponseType<CoachDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "InsightsCoach", OperationId = "InsightsCoach")]
    public async Task<ActionResult<CoachDTO>> InsightsCoachAsync(string userId) => (await coachService.GetAsync(userId)).ToActionResult();

    /// <summary>Starts a new review in the background; poll GET insights/coach.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The coach, running.</returns>
    [HttpPost("coach")]
    [ProducesResponseType<CoachDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "InsightsCoachAnalyse", OperationId = "InsightsCoachAnalyse")]
    public async Task<ActionResult<CoachDTO>> InsightsCoachAnalyseAsync(string userId) => (await coachService.AnalyseAsync(userId)).ToActionResult();
}
