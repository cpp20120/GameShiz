using System.Security.Cryptography;
using System.Text;
using BotFramework.Rendering;
using Games.Horse.Infrastructure.Rendering.Generators;

namespace Games.Horse.Rendering;

public sealed record HorseRaceRenderSpec(int HorseCount, int Winner, int Variant);
