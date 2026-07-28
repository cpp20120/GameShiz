using BotFramework.Host.Execution;

namespace Games.Pick.Application.Execution;

public abstract class QuickDescriptor<TCommand,TResult> : GameExecutionDescriptor<TCommand,QuickLotteryState,TResult>
{
    public override string GameId => "pick-lottery";
}
