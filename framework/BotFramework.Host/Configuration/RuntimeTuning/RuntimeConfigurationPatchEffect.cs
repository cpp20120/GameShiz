using BotFramework.Host.Admin.Execution;
using BotFramework.Sdk.Admin.Execution;

namespace BotFramework.Host.Configuration.RuntimeTuning;

public sealed record RuntimeConfigurationPatchEffect(string NormalizedPatchJson) : IAdminEffect;
