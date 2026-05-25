using Xunit;

namespace SoClover.Tests;

public class DictionaryIntegrityTests
{
    private static string DictionariesPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "Infrastructure", "Dictionaries"));

    [Fact]
    public async Task Every_dictionary_file_has_no_duplicate_words()
    {
        Assert.True(Directory.Exists(DictionariesPath), $"Dictionaries directory not found: {DictionariesPath}");

        var dictionaryFiles = Directory.GetFiles(DictionariesPath, "*.txt");
        Assert.NotEmpty(dictionaryFiles);

        foreach (var file in dictionaryFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var words = (await File.ReadAllLinesAsync(file))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .ToList();

            var duplicates = words
                .GroupBy(w => w)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(duplicates.Count == 0, $"Dictionary '{fileName}' contains duplicate word(s): {string.Join(", ", duplicates)}");
        }
    }
}
