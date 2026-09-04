using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using WorkforceIntegrationGateway.Api.Contracts;
using WorkforceIntegrationGateway.Api.Requests;
using WorkforceIntegrationGateway.Application.VerificationRequests;

namespace WorkforceIntegrationGateway.Api.Controllers;

[ApiController]
[Route("api/v1/verification-requests")]
public sealed class VerificationRequestsController(
    VerificationRequestService service,
    VerificationRequestRequestReader requestReader) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<VerificationRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiValidationProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!HasApplicationJsonContentType(Request))
        {
            return ApiProblems.UnsupportedMediaType();
        }

        var readResult = await requestReader.ReadAsync(Request, cancellationToken);

        if (readResult.Errors.Count > 0)
        {
            return ApiProblems.Validation(readResult.Errors);
        }

        var result = await service.CreateAsync(readResult.Command!, cancellationToken);

        if (!result.IsSuccess)
        {
            return ApiProblems.Validation(result.Errors);
        }

        var response = VerificationRequestResponse.FromDomain(result.Request!);
        var location = $"/api/v1/verification-requests/{response.Id}";
        return Created(location, response);
    }

    [HttpGet("{id}")]
    [ProducesResponseType<VerificationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiValidationProblem>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Find(string id, CancellationToken cancellationToken)
    {
        if (id.Length != 36 || !Guid.TryParseExact(id, "D", out var parsedId))
        {
            return ApiProblems.Validation(
                [new ApiValidationError("id", "invalid_format", "The identifier must be a hyphenated UUID.")]);
        }

        var request = await service.FindAsync(parsedId, cancellationToken);
        return request is null
            ? ApiProblems.NotFound()
            : Ok(VerificationRequestResponse.FromDomain(request));
    }

    private static bool HasApplicationJsonContentType(HttpRequest request) =>
        MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType)
        && mediaType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase);
}
