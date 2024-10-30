namespace Berg.Api.Models.V1;

public enum SubmitFlagResult
{
    Correct,
    Incorrect,
    RateLimited,
    AlreadySolved,
    CtfNotStarted,
    CtfHasEnded,
    MustJoinTeam
}