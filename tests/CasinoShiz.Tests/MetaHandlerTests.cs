using System.Reflection;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.UpdateHandling;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Meta;
using Games.Meta.Application.Quests;
using Games.Meta.Domain.Achievements;
using Games.Meta.Domain.Clans;
using Games.Meta.Domain.Quests;
using Games.Meta.Domain.Streaks;
using Games.Meta.Domain.Seasons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;
using System.Globalization;

namespace CasinoShiz.Tests;

public sealed class MetaHandlerTests
{
    [Fact]
    public async Task MetaHandler_RoutesCoreCommands()
    {
        var harness = CreateHarness();
        const long chatId = 100L;
        const long userId = 42L;
        var user = new User { Id = userId, FirstName = "Alice", Username = "alice" };
        const int messageId = 10;

        await InvokeMessageAsync(harness, "/season", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Текущий сезон", request.Text, StringComparison.Ordinal);
            Assert.Equal(chatId, request.ChatId.Identifier);
        });

        await InvokeMessageAsync(harness, "/profile", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Equal(ParseMode.Html, request.ParseMode);
            Assert.Contains("Профиль сезона", request.Text, StringComparison.Ordinal);
            Assert.Contains("Alice", request.Text, StringComparison.Ordinal);
        });

        await InvokeMessageAsync(harness, "/rank", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("Профиль сезона", request.Text, StringComparison.Ordinal));

        await InvokeMessageAsync(harness, "/topseason", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Сезонный топ", request.Text, StringComparison.Ordinal);
            Assert.Contains("Alice", request.Text, StringComparison.Ordinal);
        });

        await InvokeMessageAsync(harness, "/achievements", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Ачивки сезона", request.Text, StringComparison.Ordinal);
            Assert.Contains("1/2", request.Text, StringComparison.Ordinal);
        });

        await InvokeMessageAsync(harness, "/streaks", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Стрики по играм", request.Text, StringComparison.Ordinal);
            Assert.Contains("slots", request.Text, StringComparison.Ordinal);
        });

        await InvokeMessageAsync(harness, "/quests", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Квесты", request.Text, StringComparison.Ordinal);
            Assert.Contains("quest-1", request.Text, StringComparison.Ordinal);
        });

        await InvokeMessageAsync(harness, "/quest", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("Использование", request.Text, StringComparison.Ordinal));

        harness.Quests.ClaimResult = new QuestClaimResult("quest-1", "Quest", 125, 250, Claimed: true);
        await InvokeMessageAsync(harness, "/quest claim quest-1", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Забрана награда", request.Text, StringComparison.Ordinal);
            Assert.Contains("Quest", request.Text, StringComparison.Ordinal);
        });
        Assert.Equal(1, harness.Quests.ClaimCalls);

        await InvokeMessageAsync(harness, "/clan", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Кланы", request.Text, StringComparison.Ordinal);
            Assert.Contains("/clan top", request.Text, StringComparison.Ordinal);
        });

        harness.Clans.CreateResult = new ClanCreateResult(Created: true, "created", Clan());
        await InvokeMessageAsync(harness, "/clan create TAG Red Dragons", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("[TAG]", request.Text, StringComparison.Ordinal));

        harness.Clans.JoinResult = new ClanJoinResult(Joined: true, "joined", Clan());
        await InvokeMessageAsync(harness, "/clan join TAG", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("[TAG]", request.Text, StringComparison.Ordinal));

        harness.Clans.ClanByTag = Clan();
        await InvokeMessageAsync(harness, "/clan info TAG", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("[TAG]", request.Text, StringComparison.Ordinal);
            Assert.Contains("Season XP", request.Text, StringComparison.Ordinal);
        });

        harness.Clans.Members = [new ClanMemberInfo(7, userId, "Alice", "owner", DateTimeOffset.UtcNow)];
        await InvokeMessageAsync(harness, "/clan members TAG", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request => Assert.Contains("Alice", request.Text, StringComparison.Ordinal));

        harness.Clans.Top = [new ClanLeaderboardEntry(1, 7, "Red Dragons", "TAG", 3, 1_000, 200)];
        await InvokeMessageAsync(harness, "/clan top", user, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("Топ кланов", request.Text, StringComparison.Ordinal);
            Assert.Contains("Red Dragons", request.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task MetaMenuHandler_SendsHomeAndHandlesCallbacks()
    {
        var harness = CreateHarness();
        const long chatId = 100L;
        var owner = new User { Id = 42, FirstName = "Alice", Username = "alice" };
        const int messageId = 11;

        await InvokeMessageAsync(harness, "/menu", owner, chatId, messageId);
        AssertRequestCall<SendMessageRequest>(harness.Bot, request =>
        {
            Assert.Contains("CasinoShiz", request.Text, StringComparison.Ordinal);
            Assert.IsType<InlineKeyboardMarkup>(request.ReplyMarkup);
            var markup = (InlineKeyboardMarkup)request.ReplyMarkup!;
            Assert.Contains(markup.InlineKeyboard.SelectMany(x => x), button => button.Text.Contains("Профиль", StringComparison.Ordinal));
            Assert.Contains(markup.InlineKeyboard.SelectMany(x => x), button => button.Text.Contains("Забрать награды", StringComparison.Ordinal));
        });

        await InvokeCallbackAsync(harness, "bad", owner, chatId, messageId);
        AssertRequestCall<AnswerCallbackQueryRequest>(harness.Bot, request =>
        {
            Assert.Equal("1", request.CallbackQueryId);
            Assert.Equal("Кнопка устарела. Открой /menu заново.", request.Text);
            Assert.True(request.ShowAlert);
        });

        await InvokeCallbackAsync(harness, "mm:77:home", owner, chatId, messageId);
        AssertRequestCall<AnswerCallbackQueryRequest>(harness.Bot, request =>
        {
            Assert.Equal("Это меню другого игрока.", request.Text);
            Assert.True(request.ShowAlert);
        });

        var noMessageUpdate = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "1",
                Data = "mm:42:home",
                From = owner,
            },
        };
        var noMessageCtx = new UpdateContext(harness.BotClient, noMessageUpdate, null!, default);
        await harness.Handler.HandleAsync(noMessageCtx);
        AssertRequestCall<AnswerCallbackQueryRequest>(harness.Bot, request =>
        {
            Assert.Equal("1", request.CallbackQueryId);
            Assert.Null(request.Text);
        });

        await InvokeCallbackAsync(harness, "mm:42:profile", owner, chatId, messageId);
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request => Assert.Contains("Профиль сезона", request.Text, StringComparison.Ordinal));

        await InvokeCallbackAsync(harness, "mm:42:quests", owner, chatId, messageId);
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request => Assert.Contains("Квесты", request.Text, StringComparison.Ordinal));

        await InvokeCallbackAsync(harness, "mm:42:achievements", owner, chatId, messageId);
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request => Assert.Contains("Ачивки", request.Text, StringComparison.Ordinal));

        await InvokeCallbackAsync(harness, "mm:42:streaks", owner, chatId, messageId);
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request => Assert.Contains("Стрики по играм", request.Text, StringComparison.Ordinal));

        await InvokeCallbackAsync(harness, "mm:42:top", owner, chatId, messageId);
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request => Assert.Contains("Сезонный топ", request.Text, StringComparison.Ordinal));

        await InvokeCallbackAsync(harness, "mm:42:games", owner, chatId, messageId);
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request =>
        {
            Assert.Contains("Игры", request.Text, StringComparison.Ordinal);
            var markup = (InlineKeyboardMarkup)request.ReplyMarkup!;
            Assert.Contains(markup.InlineKeyboard.SelectMany(x => x), button => button.Text.Contains("Обновить", StringComparison.Ordinal));
        });

        harness.DailyBonus.Result = new DailyBonusClaimResult(DailyBonusClaimStatus.Claimed, 25, 125);
        await InvokeCallbackAsync(harness, "mm:42:daily", owner, chatId, messageId);
        AssertRequestCall<AnswerCallbackQueryRequest>(harness.Bot, request => Assert.Contains("+25", request.Text ?? string.Empty, StringComparison.Ordinal));
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request => Assert.Contains("CasinoShiz", request.Text, StringComparison.Ordinal));

        harness.Quests.ClaimResult = new QuestClaimResult("quest-1", "Quest", 10, 20, Claimed: true);
        await InvokeCallbackAsync(harness, "mm:42:claim:quest-1", owner, chatId, messageId);
        Assert.True(harness.Quests.ClaimCalls > 0);
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request => Assert.Contains("Квесты", request.Text, StringComparison.Ordinal));

        harness.Quests.GetQuestsResult = [
            new PlayerQuestView("quest-1", "Quest", "Desc", "daily", 1, 1, Completed: true, Claimed: false, 10, 20),
            new PlayerQuestView("quest-2", "Quest 2", "Desc", "daily", 1, 1, Completed: true, Claimed: false, 10, 20),
        ];
        await InvokeCallbackAsync(harness, "mm:42:claimall", owner, chatId, messageId);
        Assert.True(harness.Quests.GetQuestsCalls > 0);
        Assert.True(harness.Quests.ClaimCalls > 1);
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request => Assert.Contains("Квесты", request.Text, StringComparison.Ordinal));

        await InvokeCallbackAsync(harness, "mm:42:close", owner, chatId, messageId);
        AssertRequestCall<EditMessageTextRequest>(harness.Bot, request => Assert.Equal("Меню закрыто.", request.Text));
    }

    private static Harness CreateHarness()
    {
        var season = new MetaSeason(7, "Season 7", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}");
        var meta = new MetaServiceStub
        {
            Season = season,
            Profile = new SeasonProfile(
                season,
                new SeasonPlayer(7, 100, 42, "Alice", 1_500, 8, 1337, 12, 9, 3, 12_000, 18_000, DateTimeOffset.UtcNow),
                "gold",
                2_000,
                1_000),
            Top = [new SeasonLeaderboardEntry(1, 42, "Alice", 1_500, 8, 1337, 12, 9, 3)],
            Achievements = [
                new PlayerAchievementView("a1", "First", "Desc", "general", IsUnlocked: true, DateTimeOffset.UtcNow),
                new PlayerAchievementView("a2", "Second", "Desc", "general", IsUnlocked: false, UnlockedAt: null),
            ],
            Streaks = [
                new PlayerGameStreakView("dice", "slots", "/dice", 3, 7, 5, new DateOnly(2026, 6, 23)),
            ],
        };
        var quests = new QuestServiceStub
        {
            GetQuestsResult = [
                new PlayerQuestView("quest-1", "Quest", "Desc", "daily", 1, 2, Completed: true, Claimed: false, 10, 20),
            ],
        };
        var clans = new ClanServiceStub
        {
            ClanByTag = new ClanInfo(7, 100, "Red Dragons", "TAG", 42, DateTimeOffset.UtcNow, 5, 999, 222),
            UserClan = new ClanInfo(7, 100, "Red Dragons", "TAG", 42, DateTimeOffset.UtcNow, 5, 999, 222),
            Members = [new ClanMemberInfo(7, 42, "Alice", "owner", DateTimeOffset.UtcNow)],
            Top = [new ClanLeaderboardEntry(1, 7, "Red Dragons", "TAG", 5, 999, 222)],
            CreateResult = new ClanCreateResult(Created: false, "not created"),
            JoinResult = new ClanJoinResult(Joined: false, "not joined"),
        };
        var dailyBonus = new DailyBonusStub
        {
            Result = new DailyBonusClaimResult(DailyBonusClaimStatus.AlreadyClaimedToday, 0, 125),
        };
        var bot = new RecordingBotClient();
        var handler = new MetaMenuHandler(meta, quests, new EconomyStub(), dailyBonus, NullLogger<MetaMenuHandler>.Instance);
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new Harness(bot, handler, meta, quests, clans, dailyBonus, serviceProvider);
    }

    private static async Task InvokeMessageAsync(Harness harness, string text, User user, long chatId, int messageId)
    {
        harness.Bot.Clear();
        var update = new Update
        {
            Id = 1,
            Message = new Message
            {
                Id = messageId,
                Text = text,
                From = user,
                Chat = new Chat { Id = chatId, Type = ChatType.Private },
                Date = DateTime.UtcNow,
            },
        };

        if (string.Equals(text, "/menu", StringComparison.Ordinal))
        {
            await harness.Handler.HandleAsync(new UpdateContext(harness.BotClient, update, null!, default));
        }
        else
        {
            var metaHandler = new MetaHandler(harness.Meta, harness.Quests, harness.Clans);
            await metaHandler.HandleAsync(new UpdateContext(harness.BotClient, update, null!, default));
        }
    }

    private static async Task InvokeCallbackAsync(Harness harness, string data, User user, long chatId, int messageId)
    {
        harness.Bot.Clear();
        var update = new Update
        {
            Id = 1,
            CallbackQuery = new CallbackQuery
            {
                Id = "1",
                Data = data,
                From = user,
                Message = new Message
                {
                    Id = messageId,
                    Chat = new Chat { Id = chatId, Type = ChatType.Private },
                    Date = DateTime.UtcNow,
                },
            },
        };

        await harness.Handler.HandleAsync(new UpdateContext(harness.BotClient, update, null!, default));
    }

    private static ClanInfo Clan() => new(7, 100, "Red Dragons", "TAG", 42, DateTimeOffset.UtcNow, 5, 999, 222);

    private static void AssertRequestCall<TRequest>(RecordingBotClient bot, Action<TRequest> assert)
        where TRequest : class
    {
        var call = bot.Calls.LastOrDefault(x => string.Equals(x.MethodName, "SendRequest", StringComparison.Ordinal) && x.Args["request"] is TRequest)
            ?? throw new Xunit.Sdk.XunitException(
                $"Expected request {typeof(TRequest).Name}. Actual calls: {string.Join(", ", bot.Calls.Select(x => $"{x.MethodName}<{x.Args.GetValueOrDefault("request")?.GetType().Name}>"))}");

        assert((TRequest)call.Args["request"]!);
    }

    private sealed record Harness(
        RecordingBotClient Bot,
        MetaMenuHandler Handler,
        MetaServiceStub Meta,
        QuestServiceStub Quests,
        ClanServiceStub Clans,
        DailyBonusStub DailyBonus,
        IServiceProvider Services)
    {
        public ITelegramBotClient BotClient => Bot.Create();
    }

    private sealed class MetaServiceStub : IMetaService
    {
        public MetaSeason Season { get; set; } = null!;
        public SeasonProfile Profile { get; set; } = null!;
        public IReadOnlyList<SeasonLeaderboardEntry> Top { get; set; } = [];
        public IReadOnlyList<PlayerAchievementView> Achievements { get; set; } = [];
        public IReadOnlyList<PlayerGameStreakView> Streaks { get; set; } = [];

        public int ActiveSeasonCalls { get; private set; }
        public int ProfileCalls { get; private set; }
        public int TopCalls { get; private set; }
        public int AchievementsCalls { get; private set; }
        public int StreakCalls { get; private set; }

        public Task<MetaSeason> GetActiveSeasonAsync(CancellationToken ct)
        {
            ActiveSeasonCalls++;
            return Task.FromResult(Season);
        }

        public Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct)
        {
            ProfileCalls++;
            return Task.FromResult(Profile);
        }

        public Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct)
        {
            TopCalls++;
            return Task.FromResult(Top);
        }

        public Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct)
        {
            AchievementsCalls++;
            return Task.FromResult(Achievements);
        }

        public Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct)
        {
            StreakCalls++;
            return Task.FromResult(Streaks);
        }

        public Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct) => throw new NotImplementedException();
        public Task<SeasonPlayer> AddSeasonXpAsync(long seasonId, long chatId, long userId, string displayName, long xpDelta, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class QuestServiceStub : IQuestService
    {
        public IReadOnlyList<QuestProgressUpdate> ApplyResult { get; set; } = [];
        public IReadOnlyList<PlayerQuestView> GetQuestsResult { get; set; } = [];
        public QuestClaimResult? ClaimResult { get; set; }

        public int ApplyCalls { get; private set; }
        public int GetQuestsCalls { get; private set; }
        public int ClaimCalls { get; private set; }

        public Task<IReadOnlyList<QuestProgressUpdate>> ApplyGameCompletedAsync(GameCompletedMetaEvent ev, CancellationToken ct)
        {
            ApplyCalls++;
            return Task.FromResult(ApplyResult);
        }

        public Task<IReadOnlyList<PlayerQuestView>> GetQuestsAsync(long chatId, long userId, CancellationToken ct)
        {
            GetQuestsCalls++;
            return Task.FromResult(GetQuestsResult);
        }

        public Task<QuestClaimResult?> ClaimAsync(long chatId, long userId, string displayName, string questId, CancellationToken ct)
        {
            ClaimCalls++;
            if (ClaimResult is { } result && string.Equals(result.QuestId, questId, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<QuestClaimResult?>(result);
            return Task.FromResult<QuestClaimResult?>(ClaimResult);
        }
    }

    private sealed class ClanServiceStub : IClanService
    {
        public ClanCreateResult CreateResult { get; set; } = new(Created: false, "create");
        public ClanJoinResult JoinResult { get; set; } = new(Joined: false, "join");
        public ClanInfo? UserClan { get; set; }
        public ClanInfo? ClanByTag { get; set; }
        public IReadOnlyList<ClanMemberInfo> Members { get; set; } = [];
        public IReadOnlyList<ClanLeaderboardEntry> Top { get; set; } = [];

        public Task<ClanCreateResult> CreateAsync(long chatId, long userId, string displayName, string tag, string name, CancellationToken ct) => Task.FromResult(CreateResult);
        public Task<ClanJoinResult> JoinAsync(long chatId, long userId, string displayName, string tag, CancellationToken ct) => Task.FromResult(JoinResult);
        public Task<ClanInfo?> GetUserClanAsync(long chatId, long userId, CancellationToken ct) => Task.FromResult(UserClan);
        public Task<ClanInfo?> GetClanByTagAsync(long chatId, string tag, CancellationToken ct) => Task.FromResult(ClanByTag);
        public Task<IReadOnlyList<ClanMemberInfo>> GetMembersAsync(long clanId, CancellationToken ct) => Task.FromResult(Members);
        public Task<IReadOnlyList<ClanLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => Task.FromResult(Top);
        public Task ApplyGameCompletedAsync(GameCompletedMetaEvent ev, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class EconomyStub : IEconomicsService
    {
        public Task EnsureUserAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(long userId, long balanceScopeId, CancellationToken ct) => Task.FromResult(1_234);
        public Task<bool> TryDebitAsync(long userId, long balanceScopeId, int amount, string reason, CancellationToken ct) => Task.FromResult(false);
        public Task DebitAsync(long userId, long balanceScopeId, int amount, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task CreditAsync(long userId, long balanceScopeId, int amount, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task AdjustUncheckedAsync(long userId, long balanceScopeId, int delta, CancellationToken ct) => Task.CompletedTask;
        public Task<LedgerRevertResult> RevertLedgerEntryAsync(long economicsLedgerId, CancellationToken ct) => Task.FromResult(new LedgerRevertResult(LedgerRevertStatus.NoEffect, 0));
        public Task<PeerTransferResult> TryPeerTransferAsync(long fromUserId, long toUserId, long balanceScopeId, int debitFromSender, int creditToRecipient, string senderReason, string recipientReason, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class DailyBonusStub : IDailyBonusService
    {
        public DailyBonusClaimResult Result { get; set; }
        public Task<DailyBonusClaimResult> TryClaimAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) => Task.FromResult(Result);
        public Task<DailyBonusCatchUpStats> CatchUpMissedDaysAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private class RecordingBotClient : DispatchProxy
    {
        private static readonly AsyncLocal<List<RecordedCall>?> CurrentCalls = new();

        public List<RecordedCall> Calls { get; } = [];

        public ITelegramBotClient Create()
        {
            CurrentCalls.Value = Calls;
            return DispatchProxy.Create<ITelegramBotClient, RecordingBotClient>();
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException("Missing target method.");

            var parameters = targetMethod.GetParameters();
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < parameters.Length; i++)
                map[parameters[i].Name ?? string.Create(CultureInfo.InvariantCulture, $"arg{i}")] = args?[i];

            CurrentCalls.Value?.Add(new RecordedCall(targetMethod.Name, map));

            return CreateReturnValue(targetMethod.ReturnType);
        }

        public void Clear() => Calls.Clear();

        private static object? CreateReturnValue(Type returnType)
        {
            if (returnType == typeof(void))
                return null;

            if (returnType == typeof(Task))
                return Task.CompletedTask;

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var type = returnType.GetGenericArguments()[0];
                var method = typeof(RecordingBotClient).GetMethod(nameof(CreateTaskResult), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(type);
                return method.Invoke(null, parameters: null);
            }

            if (returnType.IsValueType)
                return Activator.CreateInstance(returnType);

            return null;
        }

        private static Task<T> CreateTaskResult<T>() => Task.FromResult(default(T)!);
    }

    private sealed record RecordedCall(string MethodName, IReadOnlyDictionary<string, object?> Args);

}
