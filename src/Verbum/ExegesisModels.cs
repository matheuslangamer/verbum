namespace Verbum;

public sealed class ExegesisChapter
{
    public string Source { get; set; } = "";
    public string Revision { get; set; } = "";
    public string Edition { get; set; } = "";
    public int Book { get; set; }
    public int Chapter { get; set; }
    public List<ExegesisVerse> Verses { get; set; } = [];
}
public sealed class ExegesisVerse
{
    public int Verse { get; set; }
    public List<ExegesisWord> Words { get; set; } = [];
    public List<ExegesisGroup> Groups { get; set; } = [];
}
public sealed class ExegesisWord
{
    public string Id { get; set; } = "";
    public int Order { get; set; }
    public string Text { get; set; } = "";
    public string Lemma { get; set; } = "";
    public string English { get; set; } = "";
    public string Morphology { get; set; } = "";
    public string PartOfSpeech { get; set; } = "";
    public string Role { get; set; } = "";
    public string Strong { get; set; } = "";
    public Dictionary<string, string> Features { get; set; } = [];
}
public sealed class ExegesisGroup
{
    public string Sentence { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Role { get; set; } = "";
    public string Rule { get; set; } = "";
    public int Depth { get; set; }
    public List<string> Words { get; set; } = [];
}
public sealed class TextualComment
{
    public string Id { get; set; } = "";
    public List<TextualReference> References { get; set; } = [];
    public string Remark { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public List<TextualAlternative> Alternatives { get; set; } = [];
}
public sealed record TextualReference(int Book, int Chapter, int Verse);
public sealed class TextualAlternative
{
    public string Text { get; set; } = "";
    public string Rating { get; set; } = "";
    public string Literal { get; set; } = "";
    public List<string> Factors { get; set; } = [];
    public List<string> Versions { get; set; } = [];
}
