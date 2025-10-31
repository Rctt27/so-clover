namespace SoClover.Domain;

public sealed class WordsPool
{
    private readonly IWordDictionary _wordDictionary;
    private readonly List<string> _availableWords;
    private readonly object _lock = new();

    public GameId GameId { get; }
    public string Language { get; }

    private WordsPool(GameId gameId, string language, IWordDictionary wordDictionary)
    {
        GameId = gameId;
        Language = language;
        _wordDictionary = wordDictionary;
        _availableWords = new List<string>();
    }

    public static async Task<WordsPool> CreateAsync(
        GameId gameId,
        string language,
        IWordDictionary wordDictionary,
        CancellationToken ct = default)
    {
        var pool = new WordsPool(gameId, language, wordDictionary);
        await pool.InitializeAsync(ct);
        return pool;
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        // Charger tous les mots du dictionnaire pour la langue
        var allWords = await _wordDictionary.GetAllWordsAsync(Language, ct);

        lock (_lock)
        {
            _availableWords.AddRange(allWords);
        }
    }

    public List<string> DrawWords(int count)
    {
        lock (_lock)
        {
            if (_availableWords.Count < count)
                throw new InvalidOperationException(
                    $"Not enough words available. Requested: {count}, Available: {_availableWords.Count}");

            var drawnWords = new List<string>(count);
            var random = new Random();

            for (int i = 0; i < count; i++)
            {
                var index = random.Next(_availableWords.Count);
                drawnWords.Add(_availableWords[index]);
                _availableWords.RemoveAt(index);
            }

            return drawnWords;
        }
    }

    public int RemainingWordsCount
    {
        get
        {
            lock (_lock)
            {
                return _availableWords.Count;
            }
        }
    }
}
