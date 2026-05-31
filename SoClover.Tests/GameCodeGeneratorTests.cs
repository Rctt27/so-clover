using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SoClover.Domain;
using SoClover.UseCases.GameLogics;
using Xunit;

namespace SoClover.Tests;

public class GameCodeGeneratorTests
{
    private sealed class FakeDictionary : IWordDictionary
    {
        private readonly IReadOnlyList<string> _words;
        public FakeDictionary(params string[] words) => _words = words;

        public Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(_words.Take(count).ToList());

        public Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default)
            => Task.FromResult(_words);
    }

    [Fact]
    public async Task Generates_four_lowercase_slug_segments()
    {
        var dict = new FakeDictionary("Water Bottle", "Fir Tree", "Lamp", "Sheep");
        var gen = new GameCodeGenerator(dict);

        var code = await gen.GenerateAsync();

        Assert.Equal("waterbottle-firtree-lamp-sheep", code);
        Assert.Matches(new Regex("^[a-z0-9]+(-[a-z0-9]+){3}$"), code);
    }
}
