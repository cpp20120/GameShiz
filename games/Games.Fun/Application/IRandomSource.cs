namespace Games.Fun.Application;

public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
}
