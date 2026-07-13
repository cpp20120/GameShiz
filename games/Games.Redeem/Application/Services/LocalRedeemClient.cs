using Games.Redeem.Contracts;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Services;

public sealed class LocalRedeemClient(IRedeemService service, IOptions<RedeemOptions> options)
    : IRedeemClient
{
    public Task<Guid> IssueAdminCodeAsync(long userId, string? freeSpinGameId, CancellationToken ct) =>
        service.IssueAdminCodeAsync(userId, ct, freeSpinGameId);

    public async Task<BeginRedeemResponse> BeginAsync(
        long userId, long balanceScopeId, string displayName, string codeText, CancellationToken ct)
    {
        var result = await service.BeginRedeemAsync(userId, balanceScopeId, displayName, codeText, ct);
        var captcha = result.Captcha is null
            ? null
            : new RedeemCaptchaChallenge(result.Captcha.Pattern,
                result.Captcha.Items.Select(x => new RedeemCaptchaItem(x.Text, x.Data)).ToArray());
        return new BeginRedeemResponse((RedeemClientError)result.Error, result.CodeGuid, captcha);
    }

    public Task<bool> VerifyCaptchaAsync(long userId, Guid codeGuid, int chosenId, CancellationToken ct)
    {
        var expected = CaptchaService.CreateCaptcha(codeGuid.ToString(), options.Value.CaptchaItems);
        var passed = chosenId == expected.TargetId;
        service.ReportCaptcha(userId, codeGuid.ToString(), expected.Pattern, passed);
        return Task.FromResult(passed);
    }

    public async Task<CompleteRedeemResponse> CompleteAsync(
        long userId, long balanceScopeId, Guid codeGuid, CancellationToken ct)
    {
        var result = await service.CompleteRedeemAsync(userId, balanceScopeId, codeGuid, ct);
        return new CompleteRedeemResponse((RedeemClientError)result.Error, result.FreeSpinGameId);
    }
}
