using System.Reflection;
using BotFramework.Host.Composition.Builder;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Telegram.Infrastructure;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class TelegramModerationNotificationTests
{
    [Fact]
    public async Task FailureNotificationIsSentOnlyToConfiguredBotAdmin()
    {
        var recorder = new BotProxy();
        var bot = recorder.Create();
        var executor = new TelegramEffectExecutor(
            bot,
            Options.Create(new BotFrameworkOptions { Admins = [925337014] }));

        var effect = new NotifyAdministratorsEffect(
            new ChatId(-100),
            "moderation failed",
            "correlation",
            "cause");
        await executor.ExecuteAsync(
            new StoredModerationEffect(
                EffectId.New(),
                EffectTypeCatalog.NotifyAdministrators,
                effect,
                null,
                EffectImportance.BestEffort,
                1,
                3),
            CancellationToken.None);

        var request = Assert.IsType<SendMessageRequest>(Assert.Single(recorder.Requests));
        Assert.Equal(925337014, request.ChatId.Identifier);
        Assert.DoesNotContain(recorder.Methods, method => method == "GetChatAdministrators");
    }

    private class BotProxy : DispatchProxy
    {
        private static readonly AsyncLocal<BotProxy?> Current = new();

        public List<object> Requests { get; } = [];
        public List<string> Methods { get; } = [];

        public ITelegramBotClient Create()
        {
            Current.Value = this;
            return DispatchProxy.Create<ITelegramBotClient, BotProxy>();
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method is null)
                return null;

            Current.Value?.Methods.Add(method.Name);
            if (method.Name == "SendRequest" && args?[0] is object request)
                Current.Value?.Requests.Add(request);

            var returnType = method.ReturnType;
            if (returnType == typeof(Task))
                return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var helper = typeof(BotProxy)
                    .GetMethod(nameof(Result), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(returnType.GetGenericArguments()[0]);
                return helper.Invoke(null, null);
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }

        private static Task<T> Result<T>() => Task.FromResult(default(T)!);
    }
}
