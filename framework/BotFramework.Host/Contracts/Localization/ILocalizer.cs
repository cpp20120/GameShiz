namespace BotFramework.Host.Contracts.Localization;

public interface ILocalizer
{
    string Get(string moduleId, string key, string cultureCode = "ru");
    string GetPlural(string moduleId, string key, int count, string cultureCode = "ru");
}
