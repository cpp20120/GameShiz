namespace BotFramework.Scheduling.Abstractions;

/// <summary>A command whose cadence is declared by its owning module.</summary>
public interface IRecurringScheduledCommand : IScheduledCommand
{
    ScheduleDescriptor Schedule { get; }
}
