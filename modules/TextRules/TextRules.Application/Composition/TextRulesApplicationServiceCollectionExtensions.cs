using BotFramework.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TextRules.Application.Analysis;
using TextRules.Application.Compilation;
using TextRules.Application.Matching;

namespace TextRules.Application.Composition;

public static class TextRulesApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddTextRulesCore(
        this IServiceCollection services,
        TextRuleCompilerOptions? compilerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(compilerOptions ?? new TextRuleCompilerOptions());
        services.TryAddSingleton<ITextRuleCompiler, TextRuleCompiler>();
        services.TryAddSingleton<ITextRuleMatcher, TextRuleMatcher>();
        services.TryAddSingleton<ITextRuleScopeResolver, DefaultTextRuleScopeResolver>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<ITextAnalyzer, TextRuleAnalyzer>());
        return services;
    }
}
