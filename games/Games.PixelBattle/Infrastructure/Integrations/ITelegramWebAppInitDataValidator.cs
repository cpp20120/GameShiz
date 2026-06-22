namespace Games.PixelBattle.Infrastructure.Integrations;

public interface ITelegramWebAppInitDataValidator
{
    bool TryValidate(string? initData, out TelegramWebAppAuth auth);
}
