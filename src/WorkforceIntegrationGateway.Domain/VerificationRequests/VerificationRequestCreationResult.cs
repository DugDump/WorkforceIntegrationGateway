using System.Collections.ObjectModel;

namespace WorkforceIntegrationGateway.Domain.VerificationRequests;

public sealed class VerificationRequestCreationResult
{
    private VerificationRequestCreationResult(
        VerificationRequest? request,
        IReadOnlyList<VerificationRequestValidationError> errors)
    {
        Request = request;
        Errors = errors;
    }

    public bool IsSuccess => Request is not null;

    public VerificationRequest? Request { get; }

    public IReadOnlyList<VerificationRequestValidationError> Errors { get; }

    internal static VerificationRequestCreationResult Success(VerificationRequest request) =>
        new(request, Array.Empty<VerificationRequestValidationError>());

    internal static VerificationRequestCreationResult Failure(
        IEnumerable<VerificationRequestValidationError> errors) =>
        new(null, new ReadOnlyCollection<VerificationRequestValidationError>(errors.ToArray()));
}
