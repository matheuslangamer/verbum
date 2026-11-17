using System.IO;
using System.Text.Json;

namespace Verbum;

internal static class SelfTests
{
    public static int Run(string[] args)
    {
        var checks = new List<object>();
        int failures = 0;
        void Check(string name, bool result)
        {
            checks.Add(new { name, passed = result }); if (!result) failures++;
        }
        string temp = Path.Combine(Path.GetTempPath(), "verbum-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var glossWord = new OriginalWord { Strongs = ["H123a"] };
            var glossResource = new OriginalResource { Lexicon = new() { ["H123"] = new LexiconEntry { Definition = "English lexical meaning" } } };
            var glossRow = new UserParsing("forma", "tradução", "parsing", "código", "H123", "personal meaning", "referência");
            Check("Tooltip gloss prefers Portuguese over English", WordGlosses.Summary([glossRow], glossWord, glossResource, []).StartsWith("tradução (glossa PT"));
            Check("Tooltip gloss falls back to personal English", WordGlosses.Summary([glossRow with { Portuguese = "—" }], glossWord, glossResource, []).StartsWith("personal meaning (inglês"));
            Check("Tooltip gloss rejects empty placeholders and resolves extended Strong", WordGlosses.Summary([glossRow with { Portuguese = " ", English = "-" }], glossWord, glossResource, []).StartsWith("English lexical meaning (inglês · Strong"));
            Check("Tooltip gloss does not use unrelated lexical entries", WordGlosses.Summary([], new OriginalWord { Strongs = ["H999"] }, glossResource, []) == "");
            var bdbGloss = new LexicalResource { Id = "BDB", Entries = [new() { Codes = ["H123"], Definition = "BDB English meaning" }] };
            Check("Tooltip gloss can fall back to BDB by exact Strong", WordGlosses.Summary([], glossWord, null, [bdbGloss]).StartsWith("BDB English meaning (inglês · BDB"));
            var exitInitial = new StudySession { Layout = new() { Tabs = [new() { Id = "reader", Book = 28, Chapter = 1, Verse = 3 }, new() { Id = "note", Kind = "notes", Note = "texto inicial" }] } };
            var exitPrevious = SessionExit.Clone(exitInitial);
            exitInitial.Layout.Tabs[0].Chapter = 4;
            exitInitial.Layout.Tabs[1].Note = "**nota atual**";
            Check("Exit checkpoint is independent of live reading and notes", exitPrevious.Layout.Tabs[0].Chapter == 1 && exitPrevious.Layout.Tabs[1].Note == "texto inicial");
            string exitPath = Path.Combine(temp, "exit-session.json"); int currentSaves = 0, previousSaves = 0;
            bool SaveCurrentExit() { currentSaves++; File.WriteAllText(exitPath, JsonSerializer.Serialize(exitInitial)); return true; }
            bool SavePreviousExit() { previousSaves++; SessionExit.PreserveNotes(exitPrevious, exitInitial.Layout.Tabs); File.WriteAllText(exitPath, JsonSerializer.Serialize(exitPrevious)); return true; }
            Check("Cancel exit leaves window open and does not write a session", !SessionExit.Complete(System.Windows.MessageBoxResult.Cancel, SaveCurrentExit, SavePreviousExit) && currentSaves == 0 && previousSaves == 0 && !File.Exists(exitPath));
            Check("Yes exit saves current reading position", SessionExit.Complete(System.Windows.MessageBoxResult.Yes, SaveCurrentExit, SavePreviousExit) && JsonSerializer.Deserialize<StudySession>(File.ReadAllText(exitPath))!.Layout.Tabs[0].Chapter == 4 && currentSaves == 1 && previousSaves == 0);
            Check("No exit restores previous position while preserving latest note", SessionExit.Complete(System.Windows.MessageBoxResult.No, SaveCurrentExit, SavePreviousExit) && JsonSerializer.Deserialize<StudySession>(File.ReadAllText(exitPath))!.Layout.Tabs is [{ Chapter: 1 }, { Note: "**nota atual**" }] && currentSaves == 1 && previousSaves == 1);
            Check("Failed save on Yes or No prevents exit", !SessionExit.Complete(System.Windows.MessageBoxResult.Yes, () => false, () => true) && !SessionExit.Complete(System.Windows.MessageBoxResult.No, () => true, () => false));
            var compactSession = new StudySession { SidebarCollapsed = false, Layout = new() { Tabs = [new() { ReaderOptionsExpanded = true }, new() { Kind = "exegesis", GuideOptionsExpanded = true }, new() { Kind = "wordstudy", StudyOptionsExpanded = true }] } };
            var compactRestored = SessionExit.Clone(compactSession);
            Check("Compact interface restores sidebar and per-tab option visibility", !compactRestored.SidebarCollapsed && compactRestored.Layout.Tabs[0].ReaderOptionsExpanded && compactRestored.Layout.Tabs[1].GuideOptionsExpanded && compactRestored.Layout.Tabs[2].StudyOptionsExpanded);
            var legacyCompact = JsonSerializer.Deserialize<StudySession>("{\"Schema\":1,\"Layout\":{\"Tabs\":[{\"Kind\":\"bible\",\"Chapter\":3}]}}")!;
            Check("Legacy session retains reading position with compact defaults", legacyCompact.SidebarCollapsed && legacyCompact.Layout.Tabs[0].Chapter == 3 && !legacyCompact.Layout.Tabs[0].ReaderOptionsExpanded && SessionStore.Validate(legacyCompact.Layout, 0));
            Check("Document search ignores Portuguese accents", DocumentLibrary.Matches("Identificação dos verbos", "identificacao", SearchMode.Intelligent).Count == 1);
            Check("Document search ignores Hebrew points without changing source", DocumentLibrary.Matches("שָׁלוֹם", "שלום", SearchMode.Intelligent) is [{ Original: "שָׁלוֹם" }]);
            Check("Document exact search retains Hebrew points", DocumentLibrary.Matches("שָׁלוֹם", "שלום", SearchMode.Exact).Count == 0);
            Check("Document search requires all terms in one section", DocumentLibrary.Matches("um verbo", "verbo gutural", SearchMode.Intelligent).Count == 0);
            Check("Document excerpt can highlight one query term", DocumentLibrary.Matches("um verbo", "verbo gutural", SearchMode.Intelligent, false).Count == 1);
            var documentFolder = Path.Combine(temp, "study", "biblias"); Directory.CreateDirectory(documentFolder); var documentCatalog = new DocumentLibrary(documentFolder);
            string textDocument = Path.Combine(temp, "Aula.txt"); File.WriteAllText(textDocument, "Identificação verbal: שָׁלוֹם.");
            var importedText = documentCatalog.Import(textDocument);
            Check("Document import retains original and indexed text", File.Exists(documentCatalog.OriginalPath(importedText)) && importedText.Sections.Single().PlainText.Contains("שָׁלוֹם"));
            Check("Duplicate document import does not duplicate catalog", documentCatalog.Import(textDocument).Id == importedText.Id && documentCatalog.Load().Count == 1);
            documentCatalog.Remove(importedText.Id);
            Check("Removing document hides catalog and search but preserves original", documentCatalog.Load().Count == 0 && documentCatalog.Search("verbal", SearchMode.Intelligent, CancellationToken.None).Count == 0 && File.Exists(documentCatalog.OriginalPath(importedText)));
            documentCatalog.RestoreRemoved();
            Check("Removed document can be restored with same identity", documentCatalog.Find(importedText.Id)?.Sections.Single().PlainText == importedText.Sections.Single().PlainText);
            var documentMarks = new TextMarkers(Path.Combine(temp, "document-markers.json"));
            var markedEntry = new VerseEntry("document:" + importedText.Id, 0, "", "", 0, 0, null, null, "texto de estudo", true);
            documentMarks.Apply([(markedEntry, 0, 8)], TextMarkers.Colors["Verde"]);
            documentMarks.Apply([(markedEntry, 3, 2)], null);
            Check("Document markers survive reload and partial erasure", new TextMarkers(Path.Combine(temp, "document-markers.json")).For(markedEntry).Select(m => (m.Start, m.Length)).SequenceEqual(new[] { (0, 3), (5, 3) }));
            Check("Document markers do not leak across documents or sections", documentMarks.For(markedEntry with { Edition = "document:other" }).Count == 0 && documentMarks.For(markedEntry with { Chapter = 1 }).Count == 0);
            var markdownPreview = MarkdownNoteView.Render("# Título\n**forte** e *ênfase*");
            Check("Markdown note renders heading bold and italic", markdownPreview.Blocks.FirstBlock is System.Windows.Documents.Paragraph { FontSize: 28 } && markdownPreview.Blocks.OfType<System.Windows.Documents.Paragraph>().Last().Inlines.OfType<System.Windows.Documents.Bold>().Any() && markdownPreview.Blocks.OfType<System.Windows.Documents.Paragraph>().Last().Inlines.OfType<System.Windows.Documents.Italic>().Any());
            Check("Document result opens matching section", documentCatalog.Search("identificacao", SearchMode.Intelligent, CancellationToken.None) is [{ Section: 0 }]);
            Check("Document tab retains reading position", JsonSerializer.Deserialize<TabState>(JsonSerializer.Serialize(new TabState { Kind = "book", DocumentId = "fixture", DocumentSection = 7, DocumentQuery = "שלום" })) is { Kind: "book", DocumentId: "fixture", DocumentSection: 7, DocumentQuery: "שלום" });
            Check("Session validation accepts library and document tabs", SessionStore.Validate(new LayoutNode { Tabs = [new() { Kind = "library" }, new() { Kind = "book", DocumentSection = 7 }] }, 0));
            Check("Session validation still rejects unknown tools", !SessionStore.Validate(new LayoutNode { Tabs = [new() { Kind = "unknown" }] }, 0));
            Check("UBS compact reference decodes book chapter verse", HebrewLexiconView.ReferenceLabel("00102902000004") == "Gênesis 29:20");
            Check("UBS reference preserves Psalm verse zero", HebrewLexiconView.ReferenceLabel("01900300000002") == "Salmos 3:0");
            Check("Unknown UBS reference is not assigned a guessed book", HebrewLexiconView.ReferenceLabel("09900100100002").StartsWith("Referência não reconhecida"));
            Check("Portuguese accents and case", SearchText.Normalize("CORAÇÃO") == "coracao");
            Check("Canonical composed/decomposed equivalence", SearchText.Normalize("café") == SearchText.Normalize("cafe\u0301"));
            Check("Hebrew vowels and shin dot", SearchText.Normalize("שָׁלוֹם") == "שלום");
            Check("Hebrew cantillation", SearchText.Normalize("בְּרֵאשִׁ֖ית") == "בראשית");
            Check("Different order of Hebrew marks", SearchText.Normalize("ש\u05B8\u05C1לוֹם") == SearchText.Normalize("ש\u05C1\u05B8לוֹם"));
            Check("Hebrew combining grapheme joiner", SearchText.Normalize("ש\u034Fָׁלוֹם") == "שלום");
            Check("Ignore direction formatting", SearchText.Normalize("\u200Fשלום\u200E") == "שלום");
            Check("Ignore direction formatting inside token", SearchText.Tokenize("ש\u200Fלום").Single().Normalized == "שלום");
            Check("Greek accents", SearchText.Normalize("λόγος") == "λογοσ");
            Check("Greek final sigma and uppercase", SearchText.Normalize("ΛΌΓΟΣ") == SearchText.Normalize("λόγος"));
            Check("Preserve distinct Hebrew consonants", SearchText.Normalize("שלום") != SearchText.Normalize("שלם"));
            Check("Preserve final Hebrew letters", SearchText.Normalize("מלך") != SearchText.Normalize("מלכ"));
            Check("Maqaf is a boundary", SearchText.QueryTokens("בני־ישראל").Length == 2);
            Check("Whitespace-only query", SearchText.QueryTokens("  ").Length == 0);
            Check("Whole words do not match substrings", !SearchText.WordMatches("amoroso", "amor", SearchMode.Intelligent));
            Check("Explicit prefix search", SearchText.WordMatches("amoroso", "amor", SearchMode.Prefix));
            Check("Suggestions use bounded edit distance", SearchText.IsNear("coracao", "coraca") && !SearchText.IsNear("coracao", "casa"));
            Check("Standard chapter counts", BibleLibrary.StandardChapterCounts.Length == 66 && BibleLibrary.StandardChapterCounts.Sum() == 1189);
            Check("OSHB adjective retains part of speech after language prefix", OriginalResource.MorphologySummary("HAamsa").Contains("adjetivo"));
            Check("Portuguese finite Hebrew verb parsing", OshbPortuguese.Describe("HVqp3ms").Contains("Qal, perfeito (qatal), 3ª pessoa, masculino, singular"));
            Check("Portuguese construct noun with prefix", OshbPortuguese.Describe("HR/Ncfsc").Contains("preposição + substantivo, comum, feminino, singular, estado construto"));
            Check("Infinitives do not invent person gender number", OshbPortuguese.Describe("HVqc").Contains("infinitivo construto") && !OshbPortuguese.Describe("HVqc").Contains("pessoa"));
            Check("Aramaic stem has separate decoding", OshbPortuguese.Describe("AVqp3ms").Contains("Aramaico · verbo, Peal"));
            Check("Participles decode state without person", OshbPortuguese.Describe("HVqrmsa").Contains("particípio ativo, masculino, singular, estado absoluto"));
            Check("Unknown morphology remains visible", OshbPortuguese.Describe("HZ9").Contains("código Z9"));
            Check("Pointed form key ignores cantillation not vowels", PortugueseParsing.FormKey("בָּרָ֣א׃") == PortugueseParsing.FormKey("בָּרָא") && PortugueseParsing.FormKey("בָּרָא") != PortugueseParsing.FormKey("בְּרֹא"));
            BibleEdition MorphFixture(string id, string text, string code, int chapter = 1)
            {
                var fixtureEdition = new BibleEdition { Info = new(id, ""), Books = [] };
                var entry = new VerseEntry(id, 0, "Gênesis", "Gn", chapter, 0, 1, 1, text, true) { Words = [new OriginalWord { Text = text, Morphology = code, Strongs = ["H1254"] }] };
                fixtureEdition.Entries.Add(entry); fixtureEdition.Chapters[(0, chapter)] = [entry]; return fixtureEdition;
            }
            Check("Morphology alignment rejects same Strong with different vowels", MorphologyLinks.Enrich(MorphFixture("BHS", "בְּרֹא", ""), MorphFixture("OSHB", "בָּרָא", "HVqp3ms")) == 0);
            Check("Morphology alignment rejects different verse context", MorphologyLinks.Enrich(MorphFixture("BHS", "בָּרָא", ""), MorphFixture("OSHB", "בָּרָא", "HVqp3ms", 2)) == 0);
            Check("Morphology alignment leaves unavailable source morphology empty", MorphologyLinks.Enrich(MorphFixture("BHS", "בָּרָא", ""), MorphFixture("OSHB", "בָּרָא", "")) == 0);
            Check("Morphology alignment accepts cantillation-only difference", MorphologyLinks.Enrich(MorphFixture("BHS", "בָּרָ֣א", ""), MorphFixture("OSHB", "בָּרָא", "HVqp3ms")) == 1);
            Check("Morphology alignment accepts holam written before or on waw", MorphologyLinks.PointKey("יֹּ֔ום") == MorphologyLinks.PointKey("יּ֔וֹם") && MorphologyLinks.Enrich(MorphFixture("BHS", "אֹ֑ור", ""), MorphFixture("OSHB", "א֑וֹר", "HNcbsa")) == 1);
            Check("Consonant map skips only the differing word", MorphologyLinks.Map("אבגדה", "אבזדה") is [0, 1, -1, 3, 4]);
            {
                var whole = new OriginalWord { Text = "בְּרֵאשִׁית", Morphology = "HR/Ncfsa", Segments = [new("בְּ", 0), new("רֵאשִׁית", 3)] };
                var lines = WordTip.Segments(new OriginalWord { Text = "בְּ", Morphology = "HR", Whole = whole, WholeSegment = 0, WholeSegmentCount = 1 });
                Check("Hover lists every segment of the graphic word and marks the hovered one", lines.Count == 2 && lines[0] is { Active: true, Description: "preposição" } && !lines[1].Active && lines[1].Description.StartsWith("substantivo, comum, feminino, singular"));
                Check("Alignment units separate prefix, stem and suffix", AlignmentBuilder.Units(new OriginalWord { Text = "וּבֵיתוֹ", Morphology = "HC/Ncmsc/Sp3ms", Strongs = ["H1004b"], Segments = [new("וּ", 0), new("בֵית", 2), new("וֹ", 6)] }) is [(0, "P:C"), (1, "H1004b"), (2, "S:Sp3ms")]);
                Check("Portuguese stem joins inflected forms without touching short words", AlignmentBuilder.Stem("criou") == AlignmentBuilder.Stem("criaram") && AlignmentBuilder.Stem("deus") == "deus");
                BibleEdition Tiny(string id, bool original, params string[] verses)
                {
                    var tiny = new BibleEdition { Info = new(id, ""), Books = [], Resource = original ? new OriginalResource { ReferenceSystem = "" } : null };
                    for (int v = 0; v < verses.Length; v++)
                    {
                        var words = new List<OriginalWord>(); int at = 0;
                        foreach (string part in original ? verses[v].Split(' ') : []) { string[] pair = part.Split('='); words.Add(new() { Text = pair[0], Start = at, Strongs = [pair[1]], Morphology = "HNcmsa" }); at += pair[0].Length + 1; }
                        var entry = new VerseEntry(id, 0, "Gênesis", "Gn", 1, v, v + 1, v + 1, original ? string.Join(" ", words.Select(w => w.Text)) : verses[v], true) { Words = words };
                        tiny.Entries.Add(entry); if (!tiny.Chapters.TryGetValue((0, 1), out var list)) tiny.Chapters[(0, 1)] = list = []; list.Add(entry);
                    }
                    return tiny;
                }
                var tinySource = Tiny("SRC", true, "אור=H216 טוב=H2896", "אור=H216 יום=H3117", "יום=H3117 טוב=H2896", "לילה=H3915 טוב=H2896", "אור=H216 לילה=H3915");
                var tinyTarget = Tiny("TGT", false, "luz boa", "luz dia", "dia bom", "noite boa", "luz noite");
                var tinyAlignment = new TranslationAlignment(AlignmentBuilder.Build(tinySource, tinyTarget));
                var tinyTerm = tinyAlignment.Originals(tinyTarget.Entries[1], 4, 3).FirstOrDefault();
                Check("Alignment links a translation word to its original term", tinyTerm?.Word.S == "H3117" && tinyAlignment.Originals(tinyTarget.Entries[1], 0, 3).FirstOrDefault()?.Word.S == "H216");
                Check("Alignment answers from the original word to the translation", tinyAlignment.Translations(tinySource.Entries[4], tinySource.Entries[4].Words[1], tinyTarget).FirstOrDefault()?.Text == "noite");
            }

            foreach (var region in new[] { DockRegion.Left, DockRegion.Right, DockRegion.Top, DockRegion.Bottom, DockRegion.Center })
            {
                var tab = new TabState { Kind = "notes", Note = "Anotação preservada" }; var staying = new TabState(); var targetTab = new TabState();
                var source = new LayoutNode { Tabs = [tab, staying], ActiveTab = tab.Id }; var target = new LayoutNode { Tabs = [targetTab], ActiveTab = targetTab.Id };
                var dockTree = new LayoutNode { Kind = "split", Children = [source, target] };
                bool docked = StudyLayout.Dock(ref dockTree, tab.Id, target.Id, region, out string active);
                var leaves = dockTree.Leaves().ToList(); var moved = leaves.Single(l => l.Tabs.Any(t => t.Id == tab.Id));
                Check("Dock " + region + " preserves tabs notes and active selection", docked && moved.Id == active && moved.ActiveTab == tab.Id && leaves.SelectMany(l => l.Tabs).Count() == 3 && moved.Tabs.Single(t => t.Id == tab.Id).Note == "Anotação preservada" && leaves.Count == (region == DockRegion.Center ? 2 : 3));
                if (region != DockRegion.Center) Check("Dock " + region + " inserts in expected side", target.Direction == (region is DockRegion.Left or DockRegion.Right ? "horizontal" : "vertical") && target.Children[region is DockRegion.Left or DockRegion.Top ? 0 : 1].Id == active);
            }
            var lone = new TabState(); var sourceOnly = new LayoutNode { Tabs = [lone] }; var other = new LayoutNode { Tabs = [new()] };
            var pruning = new LayoutNode { Kind = "split", Children = [sourceOnly, other] };
            Check("Dock closes emptied source without duplicate tabs", StudyLayout.Dock(ref pruning, lone.Id, other.Id, DockRegion.Center, out _) && pruning.Kind == "pane" && pruning.Tabs.Count == 2);
            Check("Same pane single tab edge is safe no-op", !StudyLayout.Dock(ref pruning, pruning.Tabs[0].Id, pruning.Id, DockRegion.Center, out _) && pruning.Tabs.Count == 2);
            Check("Dock pointer regions", StudyLayout.Region(2, 250, 1000, 500) == DockRegion.Left && StudyLayout.Region(990, 250, 1000, 500) == DockRegion.Right && StudyLayout.Region(500, 5, 1000, 500) == DockRegion.Top && StudyLayout.Region(500, 495, 1000, 500) == DockRegion.Bottom && StudyLayout.Region(500, 250, 1000, 500) == DockRegion.Center);
            var linkPath = Path.Combine(temp, "links.json"); var maps = new TranslationLinks(linkPath);
            var sourceVerse = new VerseEntry("HE", 0, "Gênesis", "Gn", 1, 0, 1, 1, "בָּרָא", true);
            var targetVerse = new VerseEntry("ARA", 0, "Gênesis", "Gn", 1, 0, 1, 1, "Deus criou os céus.", true);
            string queryKey = TranslationLinks.Key("H1254", SearchMode.Strong);
            maps.Save(queryKey, sourceVerse, targetVerse, "criou", 5);
            var markedLink = new TranslationLinks(linkPath).Find(queryKey, sourceVerse, "ARA")!;
            Check("Manual translation link persists exact source target and phrase", markedLink.Phrase == "criou" && TranslationLinks.Valid(markedLink, targetVerse));
            Check("Manual translation link never crosses translations or searches", maps.Find(queryKey, sourceVerse, "TB") == null && maps.Find(TranslationLinks.Key("H430", SearchMode.Strong), sourceVerse, "ARA") == null);
            Check("Changed source or target text invalidates previous annotation", maps.Find(queryKey, sourceVerse with { Text = "changed" }, "ARA") == null && !TranslationLinks.Valid(markedLink, targetVerse with { Text = "Outra redação." }));
            bool invalidLink = false;
            try { maps.Save(queryKey, sourceVerse, targetVerse, "inventado", 5); } catch (ArgumentException) { invalidLink = true; }
            Check("Translation link cannot fabricate a phrase absent from verse", invalidLink && maps.Find(queryKey, sourceVerse, "ARA")?.Phrase == "criou");
            maps.Save(queryKey, sourceVerse, targetVerse, "os céus", 11);
            Check("Editing a translation link replaces rather than duplicates it", maps.Find(queryKey, sourceVerse, "ARA")?.Phrase == "os céus" && JsonSerializer.Deserialize<List<TranslationLink>>(File.ReadAllText(linkPath))?.Count == 1);
            maps.Remove(queryKey, sourceVerse, "ARA");
            Check("Undo translation link preserves backup and removes annotation", new TranslationLinks(linkPath).Find(queryKey, sourceVerse, "ARA") == null && File.Exists(linkPath + ".backup"));
            var studyLinks = new TranslationLinks(Path.Combine(temp, "study-links.json"));
            studyLinks.Save(queryKey, sourceVerse, targetVerse, "criou", 5);
            var studyTarget = new BibleEdition { Info = new("ARA", ""), Books = [] }; studyTarget.Chapters[(0, 1)] = [targetVerse];
            var markedHit = new SearchHit(sourceVerse, "Strong H1254", 0, []);
            var unmarkedHit = new SearchHit(sourceVerse with { EntryIndex = 1, VerseStart = 2, VerseEnd = 2, Text = "Outra forma" }, "Strong H1254", 0, []);
            var studyGroups = WordStudyData.Group([markedHit, markedHit, unmarkedHit], queryKey, studyTarget, studyLinks);
            Check("Word study partitions marked and unknown verses without double count", studyGroups.Sum(s => s.Hits.Count) == 2 && studyGroups.Single(s => s.Phrase == "criou").Hits.Count == 1 && studyGroups.Single(s => s.Unmarked).Hits.Count == 1);
            Check("Word study does not invent translations for unannotated query", WordStudyData.Group([markedHit], "different", studyTarget, studyLinks) is [{ Unmarked: true }]);
            studyTarget.Chapters[(0, 1)] = [targetVerse with { Text = "Redação alterada" }];
            Check("Word study excludes stale target annotations from chart", WordStudyData.Group([markedHit], queryKey, studyTarget, studyLinks) is [{ Unmarked: true }]);
            Check("Empty word study has no fabricated segments", WordStudyData.Group([], queryKey, studyTarget, studyLinks).Count == 0);
            Check("Parallel gloss lookup respects whole words and accents", GlossParallels.ContainsPhrase("O coração do rei", "coracao") && !GlossParallels.ContainsPhrase("Reinado de reis", "rei"));
            Check("Parallel gloss highlighting preserves original accented word", GlossParallels.Highlights("O coração do rei", "coracao") is [{ Original: "coração", Start: 2, Length: 7 }]);
            var verbGlosses = new[] { new PortugueseGloss("e pôr / colocar", "and place", ["H5414"], true), new PortugueseGloss("livrou / entregou", "delivered", ["H5414"], true), new PortugueseGloss("deu", "gave", ["H5414"], true) };
            Check("Suggested parallel highlight recognizes supplied verb alternatives and inflections", ParallelHighlighting.Suggest("Ele colocou, entregou, pôs e deu.", verbGlosses).Select(t => t.Original).SequenceEqual(new[] { "colocou", "entregou", "pôs", "deu" }));
            Check("Suggested highlight preserves accented source offsets", ParallelHighlighting.Suggest("Ele pôs ali.", verbGlosses) is [{ Original: "pôs", Start: 4, Length: 3 }]);
            Check("Suggestion does not highlight unrelated words or function-only gloss", ParallelHighlighting.Suggest("O reinado de reis", [new("rei", "king", []), new("e / a / para", "", [])]).Count == 0);
            Check("Inflection suggestions require a verbal source analysis", ParallelHighlighting.Suggest("colocou", [new("colocar", "", [], false)]).Count == 0);
            Check("Infinitive por does not highlight the preposition por", ParallelHighlighting.Suggest("Abraão, por sua vez, pôs ali.", [new("pôr", "put", [], true)]).Select(t => t.Original).SequenceEqual(new[] { "pôs" }));
            var comparison = GlossParallels.Compare([markedHit], new BibleEdition { Info = new("HE", ""), Books = [] }, studyTarget, [new("Redação", "", []), new("alterada", "", [])]);
            Check("Gloss cooccurrence categories may overlap without creating lexical links", comparison.Items.All(i => i.Hits.Count == 1) && comparison.Compared == 1 && WordStudyData.Group([markedHit], queryKey, studyTarget, studyLinks).Single().Unmarked);
            Check("Word study settings survive session serialization", JsonSerializer.Deserialize<TabState>(JsonSerializer.Serialize(new TabState { Kind = "wordstudy", StudyTerm = "H1254", StudyMode = SearchMode.Strong, StudyTranslation = "ARA" })) is { Kind: "wordstudy", StudyTerm: "H1254", StudyTranslation: "ARA", StudyMode: SearchMode.Strong });
            var legacyPath = Path.Combine(temp, "legacy-markers.json");
            File.WriteAllText(legacyPath, System.Text.Json.JsonSerializer.Serialize(new[] { new { targetVerse.Edition, targetVerse.Book, targetVerse.Chapter, Entry = targetVerse.EntryIndex, targetVerse.Text, Start = 0, Length = 3, Color = TextMarkers.Colors["Azul"] } }));
            Check("Existing markers without kind remain background highlights", new TextMarkers(legacyPath).For(targetVerse) is [{ Kind: "background", Length: 3 }]);
            var inkPath = Path.Combine(temp, "font-colors.json"); var inks = new TextMarkers(inkPath);
            inks.Apply([(targetVerse, 0, 12)], TextMarkers.Colors["Amarelo"]);
            inks.Apply([(targetVerse, 2, 8)], "#0000FF", "foreground");
            inks.Apply([(targetVerse, 4, 2)], "#FF0066", "foreground");
            var reloadedInks = new TextMarkers(inkPath).For(targetVerse);
            Check("Font color persists separately from background and splits partial recoloring", reloadedInks.Count == 4 && reloadedInks.Count(m => m.Kind == "foreground") == 3 && reloadedInks.Single(m => m.Kind == "background").Length == 12 && reloadedInks.Single(m => m.Color == "#FF0066") is { Start: 4, Length: 2 });
            inks.Apply([(targetVerse, 3, 5)], null, "foreground");
            Check("Removing font color preserves background and unselected font spans", inks.For(targetVerse).Count == 3 && inks.For(targetVerse).Single(m => m.Kind == "background").Length == 12 && inks.For(targetVerse).Where(m => m.Kind == "foreground").Select(m => (m.Start, m.Length)).SequenceEqual(new[] { (2, 1), (8, 2) }));
            inks.Apply([(targetVerse, 2, 8)], "#12abEF", "foreground");
            inks.Apply([(targetVerse, 0, 12)], null);
            Check("Custom RGB survives reload and removal of background", new TextMarkers(inkPath).For(targetVerse) is [{ Kind: "foreground", Color: "#12abEF" }]);
            bool invalidInk = false; try { inks.Apply([(targetVerse, 0, 1)], "red", "foreground"); } catch (ArgumentException) { invalidInk = true; }
            Check("Font colors reject malformed values and accept requested palette", invalidInk && !TextMarkers.ValidFontColor("#12345678") && TextMarkers.FontColors.Values.All(TextMarkers.ValidFontColor));
            string markerPath = Path.Combine(temp, "markers.json"); var markers = new TextMarkers(markerPath);
            markers.Apply([(targetVerse, 5, 5)], TextMarkers.Colors["Amarelo"]);
            Check("Colored marker persists exact selected source text", new TextMarkers(markerPath).For(targetVerse) is [{ Start: 5, Length: 5 }]);
            markers.Apply([(targetVerse, 7, 2)], TextMarkers.Colors["Azul"]);
            Check("Changing marker color preserves both unselected sides", markers.For(targetVerse).Count == 3 && markers.For(targetVerse).Single(m => m.Color == TextMarkers.Colors["Azul"]) is { Start: 7, Length: 2 });
            markers.Apply([(targetVerse, 6, 3)], null);
            Check("Removing a marker clears only selected offsets", markers.For(targetVerse).Count == 2 && markers.For(targetVerse).All(m => m.Start + m.Length <= 6 || m.Start >= 9));
            Check("Markers do not transfer to changed text or another edition", markers.For(targetVerse with { Text = "Texto alterado" }).Count == 0 && markers.For(targetVerse with { Edition = "TB" }).Count == 0);
            var hebrewVerse = sourceVerse with { Text = "בָּרָא" }; markers.Apply([(hebrewVerse, 0, hebrewVerse.Text.Length)], TextMarkers.Colors["Rosa"]);
            Check("Hebrew markers preserve vowel and consonant spans", new TextMarkers(markerPath).For(hebrewVerse).Single().Length == hebrewVerse.Text.Length);

            // Synthetic lexical fixtures test Unicode matching, not supplied Bible content.
            string fixture = Path.Combine(temp, "TEST.json");
            var book = new BibleBook { Name = "Texto de teste", Abbrev = "T", Chapters = [["שָׁלוֹם", "שלום", "שָׁלֹום", "coração amor", "coracao AMOR", "amoroso", "λόγος", "λογος"]] };
            File.WriteAllText(fixture, JsonSerializer.Serialize(new[] { book }));
            var lib = new BibleLibrary(temp); var engine = new SearchEngine(lib);
            SearchResponse Search(string q, SearchMode mode = SearchMode.Intelligent) => engine.SearchAsync(q, ["TEST"], mode, CancellationToken.None).GetAwaiter().GetResult();
            var hebrew = Search("שלום");
            Check("Unpointed Hebrew finds all original variants", hebrew.Total == 3 && hebrew.Spellings.Count == 3);
            Check("Hebrew exact hits ranked first", hebrew.Hits[0].Verse.Text == "שלום" && hebrew.Hits[0].Rank == 0);
            Check("Pointed query finds unpointed text", Search("שָׁלוֹם").Total == 3);
            Check("Exact mode preserves Hebrew points", Search("שלום", SearchMode.Exact).Total == 1);
            Check("Exact mode canonical ordering", Search("ש\u05C1\u05B8לוֹם", SearchMode.Exact).Total == 1);
            Check("Portuguese accent-insensitive matching", Search("coracao").Total == 2);
            Check("Portuguese exact mode", Search("coração", SearchMode.Exact).Total == 1);
            Check("Multiple words use same-entry AND", Search("amor coração").Total == 2);
            Check("Whole-word default", Search("amor").Total == 2);
            Check("Prefix mode includes derivations", Search("amor", SearchMode.Prefix).Total == 3);
            Check("Greek query without accent", Search("λογος").Total == 2);
            Check("Search never changes original text", hebrew.Hits.Any(h => h.Verse.Text == "שָׁלוֹם"));
            Check("Highlight spans preserve accented original", Search("coracao").Hits.SelectMany(h => h.Highlights).Any(t => t.Original == "coração" && t.Length == 7));
            var canceled = new CancellationToken(true);
            bool cancellation = false;
            try { engine.SearchAsync("amor", ["TEST"], SearchMode.Intelligent, canceled).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancellation = true; }
            Check("Cancellation does not return stale results", cancellation);

            var library = new BibleLibrary(App.FindLibrary(args));
            ExegesisTests.Run(library, Check);
            ReaderToolbarTests.Run(library, Check);
            ThemeTests.Run(Check);
            NoteTests.Run(Check);
            var actualDocuments = new DocumentLibrary(library.DirectoryPath).Load();
            if (actualDocuments.Any(d => d.Title == "ESTUDO PROVA 1"))
            {
                var pdfMaterial = actualDocuments.Single(d => d.Title == "ESTUDO PROVA 1"); var actualCatalog = new DocumentLibrary(library.DirectoryPath);
                var pdfPages = PdfDocumentReader.Extract(actualCatalog.OriginalPath(pdfMaterial));
                Check("Native PDF reader extracts 52 real source pages", pdfPages.Count == 52 && pdfPages.Any(p => SearchText.Normalize(p.PlainText).Contains("joel")));
                var bitmap = PdfDocumentReader.RenderPage(actualCatalog.OriginalPath(pdfMaterial), 2, 700);
                Check("Native PDF page renders with expected dimensions", bitmap.PixelWidth == 700 && bitmap.PixelHeight > 700 && bitmap.IsFrozen);
                var apostila = actualDocuments.Single(d => d.Title == "Hebraico IV — apostila de estudo");
                var epubSections = DocumentLibrary.ReadEpub(actualCatalog.OriginalPath(apostila));
                Check("Study EPUB preserves 19 chapters in spine order", epubSections.Count == 19 && epubSections[0].Title.StartsWith("1 Como") && epubSections[^1].Title.StartsWith("19 Fontes"));
                Check("Study EPUB preserves Hebrew tables", epubSections.SelectMany(s => s.Blocks).Any(b => b.Kind == "table" && b.Rows.Any(r => r.Any(c => c.Contains("אֲנִי")))));
                Check("Study booklet is searchable without Portuguese accents", actualCatalog.Search("identificacao", SearchMode.Intelligent, CancellationToken.None).Any(h => h.Document.Id == apostila.Id));
                Check("All twelve supplied sources and booklet are indexed", actualDocuments.Count >= 13 && actualDocuments.Count(d => d.Format == "PDF") >= 2 && actualDocuments.Count(d => d.Format == "DOCX") >= 10);
                var sourceDocx = actualDocuments.First(d => d.Title == "AULA 01"); var wordSections = DocumentLibrary.ReadDocx(actualCatalog.OriginalPath(sourceDocx));
                Check("Word reader preserves real Hebrew tables", wordSections.SelectMany(s => s.Blocks).Any(b => b.Kind == "table" && b.Rows.Any(r => r.Any(c => SearchText.HasHebrew(c)))));
                var epubWithImages = DocumentLibrary.ReadEpub(actualCatalog.OriginalPath(apostila), temp, "epub-images");
                Check("Study EPUB includes three readable source illustrations", epubWithImages.SelectMany(s => s.Blocks).Count(b => b.Kind == "image" && File.Exists(Path.Combine(temp, b.Image))) == 3);
                var sourceWithFigures = actualDocuments.Single(d => d.Title == "aula 04");
                var wordWithImages = DocumentLibrary.ReadDocx(actualCatalog.OriginalPath(sourceWithFigures), temp, "word-images");
                Check("DOCX reader extracts embedded images", wordWithImages.SelectMany(s => s.Blocks).Any(b => b.Kind == "image" && File.Exists(Path.Combine(temp, b.Image))));
            }
            if (library.Editions.Count > 0)
            {
                var alm = library.LoadAsync("ALM1911").GetAwaiter().GetResult();
                Check("Actual ALM1911 import", alm.Books.Count == 66 && alm.Chapters.Count == 1189);
                Check("John 3:16 reference parsing", BibleLibrary.TryReference(alm, "João 3:16", out int b, out int c, out int? v) && b == 42 && c == 3 && v == 16);
                Check("Reference lookup preserves actual verse", BibleLibrary.ResolveVerse(alm, 42, 3, 16)?.Text.Contains("Deus", StringComparison.OrdinalIgnoreCase) == true);
                Check("Missing reference is not invented", !BibleLibrary.TryReference(alm, "João 3:999", out _, out _, out _));
                var mens = library.LoadAsync("MENS").GetAwaiter().GetResult();
                var combined = BibleLibrary.ResolveVerse(mens, 0, 1, 2);
                Check("MENS range 1–2 covers verse 2", combined is { VerseStart: 1, VerseEnd: 2, EntryIndex: 0 });
                var ntlh = library.LoadAsync("NTLH").GetAwaiter().GetResult();
                Check("NTLH 2 Samuel discrepancy detected", ntlh.Warnings.Any(w => w.Contains("2 Samuel")));
                Check("NTLH disputed book never links by fake verse number", BibleLibrary.ResolveVerse(ntlh, 9, 1, 1) == null);
                var actual = new SearchEngine(library).SearchAsync("principio", ["ALM1911"], SearchMode.Intelligent, CancellationToken.None).GetAwaiter().GetResult();
                Check("Actual Bible accent-insensitive search", actual.Hits.Any(h => h.Verse.Book == 0 && h.Verse.Chapter == 1 && h.Verse.VerseStart == 1));
                var tree = LayoutNode.Default(library.Editions);
                var roundTrip = JsonSerializer.Deserialize<LayoutNode>(JsonSerializer.Serialize(tree));
                Check("Layout and per-tab state round trip", roundTrip?.Leaves().Count() == 2 && roundTrip.Leaves().SelectMany(p => p.Tabs).Count() == 3);
                if (library.Editions.Any(e => e.Id == "BHS-A-Strong"))
                {
                    var bhs = library.LoadAsync("BHS-A-Strong").GetAwaiter().GetResult();
                    var wlc = library.LoadAsync("WLC-OSHB").GetAwaiter().GetResult();
                    var greek = library.LoadAsync("SBLGNT").GetAwaiter().GetResult();
                    Check("BHS resource covers 39 books in native versification", bhs.Books.Count(b => b.Chapters.Count > 0) == 39 && bhs.Entries.Count == 23213);
                    var first = BibleLibrary.ResolveVerse(bhs, 0, 1, 1)!;
                    Check("BHS first lexical segment retains prefix and no inserted space", first.Words[0].Strongs.Contains("H9003") && first.Words[0].Suffix == "" && first.Text.StartsWith("בְּרֵאשִׁ֖ית"));
                    Check("BHS Genesis 1:1 Strong H7225", first.Words.Any(w => w.Strongs.Contains("H7225") && w.Lemma.Length > 0));
                    Check("Strong English lexicon present", bhs.Resource!.Lexicon.TryGetValue("H7225", out var beginning) && beginning.Transliteration.Length > 0 && beginning.Definition.Length > 0);
                    var lexiconItems = HebrewLexiconData.Build([bhs, wlc]);
                    var extraLexicons = library.Lexicons.GetAwaiter().GetResult();
                    Check("BDB keeps distinct source entries", extraLexicons.Single(r => r.Id == "BDB").Entries is { Count: 10022 } bdbEntries && bdbEntries.Select(e => e.Id).Distinct().Count() == 10022);
                    var ubsPt = extraLexicons.Single(r => r.Id == "UBS-PT");
                    Check("UBS Portuguese definitions and attribution present", ubsPt.Entries.Count == 7943 && ubsPt.License.Contains("CC BY-SA") && ubsPt.Entries.Any(e => e.Codes.Contains("H3") && e.Definition.Contains("flor")));
                    Check("Multiple UBS senses are preserved", ubsPt.Entries.First(e => e.Codes.Contains("H3")).Definition.Contains("fruto"));
                    Check("Additional lexicon finds unpointed Hebrew and alternate Strong codes", HebrewLexiconData.Find([new(new LexiconEntry { Code = "H3", Lemma = "אֵב", Definition = "flor" }, [], false, "UBS-PT", "1", ["H3", "H4"])], "אב").Count == 1 && HebrewLexiconData.Find([new(new LexiconEntry { Code = "H3", Lemma = "אֵב" }, [], false, "UBS-PT", "1", ["H3", "H4"])], "H4").Count == 1);
                    Check("Lexicon source and record selection persist", JsonSerializer.Deserialize<TabState>(JsonSerializer.Serialize(new TabState { Kind = "lexicon", LexiconSource = "BDB", LexiconCode = "BDB1" })) is { LexiconSource: "BDB", LexiconCode: "BDB1" });
                    Check("BHS bara receives occurrence morphology from OSHB", first.Words.Any(w => w.Strongs.Contains("H1254") && w.Morphology == "HVqp3ms" && w.MorphologySource.Contains("OSHB")));
                    Check("BHS prefix receives separate segment morphology", first.Words[0].Morphology == "HR");
                    var enrichedCount = bhs.Entries.Sum(v => v.Words.Count(w => w.MorphologySource.Length > 0));
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(library.DirectoryPath))!, "..", "docs", "morphology-coverage.json"), JsonSerializer.Serialize(new { bhsWords = bhs.Entries.Sum(v => v.Words.Count), alignedOshbWords = enrichedCount, remainingWithoutOccurrenceParsing = bhs.Entries.Sum(v => v.Words.Count(w => w.Morphology.Length == 0)), emptyElidedSegments = bhs.Entries.Sum(v => v.Words.Count(w => w.Text.Length == 0)), rule = "same MT verse + same consonants at the aligned position (ketiv/qere differences skipped) + same vowel points (cantillation and holam-waw placement ignored); unmatched forms left empty" }, new JsonSerializerOptions { WriteIndented = true }));
                    Check("BHS occurrence enrichment covers substantial corpus", enrichedCount > 100000);
                    Check("Hebrew lexicon includes every supplied entry once", lexiconItems.Count >= bhs.Resource.Lexicon.Count && lexiconItems.Select(i => i.Entry.Code).Distinct().Count() == lexiconItems.Count && bhs.Resource.Lexicon.Keys.All(code => lexiconItems.Any(i => i.Entry.Code == code)));
                    Check("Hebrew lexicon numeric and suffixed Strong resolve same base entry", HebrewLexiconData.Find(lexiconItems, "1254").Single().Entry.Code == "H1254" && HebrewLexiconData.Find(lexiconItems, "h1254a").Single().Entry.Code == "H1254");
                    Check("Hebrew lexicon ignores vowels when searching real lemma", HebrewLexiconData.Find(lexiconItems, "ראשית").Any(i => i.Entry.Code == "H7225"));
                    Check("Hebrew lexicon indexes inflected forms from actual corpus", HebrewLexiconData.Find(lexiconItems, "ברא").Any(i => i.Entry.Code == "H1254"));
                    Check("Hebrew lexicon separates extended codes without invented definitions", lexiconItems.Any(i => i.Entry.Code == "H9003" && i.Extended));
                    Check("Hebrew lexicon English definition lookup", HebrewLexiconData.Find(lexiconItems, "beginning").Any(i => i.Entry.Code == "H7225"));
                    Check("Hebrew lexicon full empty filter and absent code", HebrewLexiconData.Find(lexiconItems, "").Count == lexiconItems.Count && HebrewLexiconData.Find(lexiconItems, "H99999").Count == 0);
                    Check("Lexicon tab query and selection persist", JsonSerializer.Deserialize<TabState>(JsonSerializer.Serialize(new TabState { Kind = "lexicon", LexiconQuery = "ראשית", LexiconCode = "H7225" })) is { Kind: "lexicon", LexiconQuery: "ראשית", LexiconCode: "H7225" });
                    Check("Source revision and attribution retained", bhs.Resource.Revision.Length == 40 && bhs.Resource.Attribution.Contains("Eliran Wong"));
                    Check("WLC native MT stays distinct", wlc.Resource!.ReferenceSystem == "MT" && !BibleLibrary.CanSynchronize(wlc, alm) && BibleLibrary.CanSynchronize(wlc, wlc));
                    Check("BHS native MT never synchronizes by identical Portuguese reference", bhs.Resource.ReferenceSystem == "MT" && !BibleLibrary.CanSynchronize(bhs, alm));
                    Check("Joel preserves all four Hebrew chapters", bhs.Books[28].Chapters.Count == 4 && new[] { 20, 27, 5, 21 }.SequenceEqual(bhs.Books[28].Chapters.Select(ch => ch.Count)));
                    var joelThree = BibleLibrary.ResolveVerse(bhs, 28, 3, 1)!;
                    var joelFour = BibleLibrary.ResolveVerse(bhs, 28, 4, 1)!;
                    Check("Joel 3:1 maps to Portuguese 2:28 without relabeling Hebrew", joelThree.KjvReferences.Single() == new SourceReference(28, 2, 28) && BibleLibrary.ResolveParallel(bhs, joelThree, alm)?.VerseStart == 28 && joelThree.Reference == "Joel 3:1");
                    Check("Joel 4:1 maps to Portuguese chapter three", BibleLibrary.ResolveParallel(bhs, joelFour, alm) is { Chapter: 3, VerseStart: 1 });
                    Check("Psalms retain native verse one titles", bhs.Books[18].Chapters[2].Count == 9 && BibleLibrary.ResolveVerse(bhs, 18, 3, 1) != null);
                    var araLinks = library.LoadAsync("ARA").GetAwaiter().GetResult();
                    VerseEntry B(int book, int chapter, int verse) => BibleLibrary.ResolveVerse(bhs, book, chapter, verse)!;
                    VerseEntry A(int book, int chapter, int verse) => BibleLibrary.ResolveVerse(araLinks, book, chapter, verse)!;
                    bool Forward(int book, int sc, int sv, int tc, int tv) => BibleLibrary.ResolveLinks(bhs, B(book, sc, sv), araLinks).Targets is [var mapped] && mapped.Chapter == tc && mapped.VerseStart == tv;
                    bool Reverse(int book, int sc, int sv, int tc, int tv) => BibleLibrary.ResolveLinks(araLinks, A(book, sc, sv), bhs).Targets is [var mapped] && mapped.Chapter == tc && mapped.VerseStart == tv;
                    Check("Joel forward and reverse map across all chapter boundaries", Forward(28, 3, 1, 2, 28) && Reverse(28, 2, 28, 3, 1) && Forward(28, 3, 5, 2, 32) && Reverse(28, 2, 32, 3, 5) && Forward(28, 4, 1, 3, 1) && Reverse(28, 3, 1, 4, 1));
                    Check("Canticles 7:1 links to ARA 6:13 in both directions", Forward(21, 7, 1, 6, 13) && Reverse(21, 6, 13, 7, 1));
                    Check("Canticles chapter seven offset and chapter eight identity", Forward(21, 7, 2, 7, 1) && Reverse(21, 7, 1, 7, 2) && Forward(21, 7, 14, 7, 13) && Forward(21, 8, 1, 8, 1));
                    Check("Psalm three heading never points to Portuguese verse one", B(18, 3, 1).IsSuperscription && B(18, 3, 1).KjvReferences.Count == 0 && BibleLibrary.ResolveLinks(bhs, B(18, 3, 1), araLinks).Targets.Count == 0);
                    Check("Psalm three body verse maps past heading both directions", Forward(18, 3, 2, 3, 1) && Reverse(18, 3, 1, 3, 2));
                    Check("Psalm fifty one retains two separate heading verses", B(18, 51, 1).IsSuperscription && B(18, 51, 2).IsSuperscription && BibleLibrary.ResolveLinks(bhs, B(18, 51, 1), araLinks).Targets.Count == 0 && BibleLibrary.ResolveLinks(bhs, B(18, 51, 2), araLinks).Targets.Count == 0);
                    Check("Psalm fifty one body verse maps with offset two both directions", Forward(18, 51, 3, 51, 1) && Reverse(18, 51, 1, 51, 3) && Forward(18, 51, 4, 51, 2));
                    Check("Psalms without headings are never shifted by generic rule", !B(18, 1, 1).IsSuperscription && Forward(18, 1, 1, 1, 1) && !B(18, 119, 1).IsSuperscription && Forward(18, 119, 1, 119, 1));
                    Check("Partial heading Psalm fifty does not hide body verse", !B(18, 50, 1).IsSuperscription && B(18, 50, 1).SuperscriptionPrefixLength > 0 && Forward(18, 50, 1, 50, 1));
                    var mixedHeading = B(18, 50, 1); var firstHeadingWord = mixedHeading.Words[0];
                    Check("A partial heading word hit is identified as absent Portuguese superscription", BibleLibrary.IsSuperscriptionHit(new(mixedHeading, "", 0, [new(firstHeadingWord.Text, "", firstHeadingWord.Start, firstHeadingWord.Text.Length)])) && !BibleLibrary.IsSuperscriptionHit(new(mixedHeading, "", 0, [new(mixedHeading.Words[^1].Text, "", mixedHeading.Words[^1].Start, mixedHeading.Words[^1].Text.Length)])));
                    Check("Psalm thirteen one-to-many linkage keeps both Portuguese verses", BibleLibrary.ResolveLinks(bhs, B(18, 13, 6), araLinks).Targets.Select(v => v.VerseStart).SequenceEqual(new int?[] { 5, 6 }) && BibleLibrary.ResolveParallel(bhs, B(18, 13, 6), araLinks) == null);
                    Check("Psalm thirteen reverse map accepts either part", Reverse(18, 13, 5, 13, 6) && Reverse(18, 13, 6, 13, 6));
                    Check("WLC links use the same explicit native reference map", BibleLibrary.ResolveLinks(wlc, BibleLibrary.ResolveVerse(wlc, 28, 4, 1)!, araLinks).Targets is [{ Chapter: 3, VerseStart: 1 }] && BibleLibrary.ResolveLinks(araLinks, A(18, 51, 1), wlc).Targets is [{ Chapter: 51, VerseStart: 3 }]);
                    Check("All superscription-only verses have no foreign numeric mapping", bhs.Entries.Where(v => v.IsSuperscription).All(v => v.KjvReferences.Count == 0) && wlc.Entries.Where(v => v.IsSuperscription).All(v => v.KjvReferences.Count == 0));
                    Check("OSHB word-level morphology retained", wlc.Entries[0].Words.Any(w => w.Morphology == "HR/Ncfsa"));
                    Check("OSHB qere notes retained", wlc.Entries.Any(e => e.Apparatus.Any(a => a.StartsWith("x-qere"))));
                    Check("NT canonical book index preserved", greek.Books.Count(b => b.Chapters.Count > 0) == 27 && greek.Entries.First().Book == 39 && greek.Entries.Any(e => e.Book == 42 && e.Chapter == 1));
                    Check("SBLGNT verse numbers preserved", BibleLibrary.ResolveVerse(greek, 42, 1, 1)?.Text.Contains("λόγος") == true);
                    Check("SBLGNT apparatus tied to John 1:18", BibleLibrary.ResolveVerse(greek, 42, 1, 18)?.Apparatus.Any(a => a.Contains("μονογενὴς")) == true);
                    Check("Greek Strong not fabricated", greek.Entries.All(e => e.Words.All(w => w.Strongs.Count == 0 && w.Morphology == "")));
                    Check("All original word offsets match immutable displayed text", new[] { bhs, wlc, greek }.All(ed => ed.Entries.All(e => e.Words.All(w => e.Text.Substring(w.Start, w.Text.Length) == w.Text))));
                    var linguisticSearch = new SearchEngine(library);
                    var aloesBhs = linguisticSearch.SearchAsync("אהלות", ["BHS-A-Strong"], SearchMode.Intelligent, CancellationToken.None).GetAwaiter().GetResult();
                    var aloesWlc = linguisticSearch.SearchAsync("אהלות", ["WLC-OSHB"], SearchMode.Intelligent, CancellationToken.None).GetAwaiter().GetResult();
                    Check("Unpointed aloes finds both BHS segments after waw prefix", aloesBhs.Total == 2 && aloesBhs.Hits.Any(h => h.Verse.Book == 18 && h.Verse.Chapter == 45 && h.Verse.VerseStart == 9) && aloesBhs.Hits.Any(h => h.Verse.Book == 21 && h.Verse.Chapter == 4 && h.Verse.VerseStart == 14));
                    Check("Unpointed aloes finds native OSHB segments", aloesWlc.Total == 2 && aloesWlc.Hits.All(h => h.Highlights.Any(t => t.Normalized == "אהלות")));
                    Check("Segment search highlights actual pointed source without changing text", aloesBhs.Hits.Concat(aloesWlc.Hits).All(h => h.Highlights.All(t => h.Verse.Text.Substring(t.Start, t.Length) == t.Original)));
                    Check("Exact search still preserves signs and graphic word boundaries", linguisticSearch.SearchAsync("אהלות", ["BHS-A-Strong"], SearchMode.Exact, CancellationToken.None).GetAwaiter().GetResult().Total == 0);
                    Check("Pointed and unpointed segmented queries have same intelligent result", linguisticSearch.SearchAsync("אֲהָלוֹת", ["BHS-A-Strong"], SearchMode.Intelligent, CancellationToken.None).GetAwaiter().GetResult().Total == aloesBhs.Total);
                    Check("Source OSHB segmentation spans remain exact", aloesWlc.Hits.All(h => h.Verse.Words.All(w => w.Segments.All(s => w.Text.Substring(s.Start, s.Text.Length) == s.Text))));
                    Check("Hebrew lexicon finds inflected aloes without accents", HebrewLexiconData.Find(lexiconItems, "אהלות").Any(i => i.Entry.Code == "H174"));
                    var strongHits = linguisticSearch.SearchAsync("H7225", ["BHS-A-Strong"], SearchMode.Strong, CancellationToken.None).GetAwaiter().GetResult();
                    Check("Search Strong across original corpus", strongHits.Total > 1 && strongHits.Hits.Any(h => h.Verse.Book == 0 && h.Verse.Chapter == 1 && h.Verse.VerseStart == 1));
                    var splitStrong = linguisticSearch.SearchAsync("H1254", ["WLC-OSHB"], SearchMode.Strong, CancellationToken.None).GetAwaiter().GetResult();
                    Check("Conventional Strong includes suffixed lemma subdivisions", splitStrong.Hits.Any(h => h.Verse.Book == 0 && h.Verse.Chapter == 1 && h.Verse.VerseStart == 1));
                    var lemmaHits = linguisticSearch.SearchAsync(first.Words.First(w => w.Strongs.Contains("H7225")).Lemma, ["BHS-A-Strong"], SearchMode.Lemma, CancellationToken.None).GetAwaiter().GetResult();
                    Check("Search lemma uses provided annotations", lemmaHits.Hits.Any(h => h.Verse.Book == 0 && h.Verse.Chapter == 1 && h.Verse.VerseStart == 1));
                    var greekHits = linguisticSearch.SearchAsync("λογος", ["SBLGNT"], SearchMode.Intelligent, CancellationToken.None).GetAwaiter().GetResult();
                    Check("Real unaccented Greek finds John 1:1", greekHits.Hits.Any(h => h.Verse.Book == 42 && h.Verse.Chapter == 1 && h.Verse.VerseStart == 1));
                    var hebrewHits = linguisticSearch.SearchAsync("ראשית", ["BHS-A-Strong"], SearchMode.Contains, CancellationToken.None).GetAwaiter().GetResult();
                    Check("Real unpointed Hebrew finds pointed Genesis 1:1", hebrewHits.Hits.Any(h => h.Verse.Book == 0 && h.Verse.Chapter == 1 && h.Verse.VerseStart == 1));
                    Check("Lexical tab state roundtrip", JsonSerializer.Deserialize<TabState>(JsonSerializer.Serialize(new TabState { Kind = "analysis", SelectedWord = first.Words[1] }))?.SelectedWord?.Strongs.Contains("H7225") == true);
                    var parsing = library.Parsing.GetAwaiter().GetResult();
                    var araHighlightEdition = library.LoadAsync("ARA").GetAwaiter().GetResult();
                    var givingGlosses = parsing.Glosses(["H5414"]);
                    Check("Screenshot Portuguese parallels receive candidate highlights from source glosses", new[] { (1, 17, "colocou"), (14, 20, "deu"), (15, 10, "pos"), (18, 7, "deu") }.All(example => ParallelHighlighting.Suggest(BibleLibrary.ResolveVerse(araHighlightEdition, 0, example.Item1, example.Item2)!.Text, givingGlosses).Any(t => t.Normalized == example.Item3)));
                    var givingVerse = BibleLibrary.ResolveVerse(bhs, 0, 14, 20)!;
                    var givingHit = new SearchHit(givingVerse, "", 0, SearchText.SourceTokens(givingVerse).Where(t => t.Normalized == "ויתן").ToList());
                    var contextualGiving = ParallelHighlighting.Suggest(BibleLibrary.ResolveVerse(araHighlightEdition, 0, 14, 20)!.Text, ParallelHighlighting.GlossesForHit(givingHit, parsing));
                    Check("Source contextual gloss disambiguates gave from delivered in Genesis 14:20", contextualGiving.Any(t => t.Normalized == "deu") && !contextualGiving.Any(t => t.Normalized == "entregou"));
                    Check("Contextual parallel renderer covers all four screenshot examples", new[] { (1, 17, "colocou"), (14, 20, "deu"), (15, 10, "pos"), (18, 7, "deu") }.All(example =>
                    {
                        var verse = BibleLibrary.ResolveVerse(bhs, 0, example.Item1, example.Item2)!;
                        var hit = new SearchHit(verse, "", 0, SearchText.SourceTokens(verse).Where(t => t.Normalized == "ויתן").ToList());
                        return ParallelHighlighting.SuggestForHit(BibleLibrary.ResolveVerse(araHighlightEdition, 0, example.Item1, example.Item2)!.Text, hit, parsing).Any(t => t.Normalized == example.Item3);
                    }));
                    Check("Portuguese glosses available without manual translation links", parsing.Glosses(["H4428"]).Any(g => SearchText.Normalize(g.Text) == "rei") && parsing.Glosses(["H99999"]).Count == 0);
                    Check("Personal parsing uses source chapter map for native Joel four", joelFour.Words.SelectMany(w => parsing.Find(joelFour, w)).Any(p => p.Reference.StartsWith("Joel 3 ")));
                    var joelTell = BibleLibrary.ResolveVerse(bhs, 28, 1, 3)!.Words.Single(w => w.Strongs.Contains("H5608"));
                    var joelName = BibleLibrary.ResolveVerse(bhs, 28, 1, 1)!.Words.Single(w => w.Strongs.Contains("H3100"));
                    Check("Screenshot Joel 1:3 has English gloss without personal parsing or external lexicons", WordGlosses.Summary([], joelTell, bhs.Resource, []).Contains("to recount"));
                    Check("Screenshot Joel 1:1 name has English gloss without parsing", WordGlosses.Summary([], joelName, bhs.Resource, []).Contains("Joel"));
                    bool tooltipHasTell = false, tooltipHasJoel = false; Exception? tooltipError = null;
                    Action tooltipCheck = () =>
                    {
                        try
                        {
                            var reader = new ReaderView(library, new TabState(), () => { }, _ => { }, _ => { }, (_, _) => { }, _ => { });
                            typeof(ReaderView).GetField("edition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(reader, bhs);
                            var method = typeof(ReaderView).GetMethod("ParsingTip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                            string Tooltip(VerseEntry verse, OriginalWord word) => WordTip.PlainText(method.Invoke(reader, [verse, word]));
                            tooltipHasTell = Tooltip(BibleLibrary.ResolveVerse(bhs, 28, 1, 3)!, joelTell).Contains("to recount");
                            tooltipHasJoel = Tooltip(BibleLibrary.ResolveVerse(bhs, 28, 1, 1)!, joelName).Contains("Joel");
                        }
                        catch (Exception ex) { tooltipError = ex; }
                    };
                    System.Windows.Application.Current.Dispatcher.Invoke(tooltipCheck);
                    Check("Actual tooltip includes both screenshot glosses before Opened event", tooltipError == null && tooltipHasTell && tooltipHasJoel);
                    var wlcFirst = BibleLibrary.ResolveVerse(wlc, 0, 1, 1)!;
                    var created = wlcFirst.Words.First(w => w.Strongs.Any(c => OriginalResource.LexiconKey(c) == "H1254"));
                    Check("User parsing imports large real corpus", parsing.Count > 100000);
                    Check("User parsing matches pointed form and Strong", parsing.Find(wlcFirst, created).Any(p => p.Code.Contains("Qal") && p.Parsing.Contains("3ª pessoa")));
                    Check("User parsing does not map chapter ordinal to verse", parsing.Describe(wlcFirst, created).Contains("sem vínculo confirmado ao versículo") && parsing.Describe(wlcFirst, created).Contains("provisórias"));
                    Check("User parsing rejects wrong Strong", parsing.Find(wlcFirst, new OriginalWord { Text = created.Text, Strongs = ["H99999"] }).Count == 0);
                    Check("Every actual OSHB code decodes without throwing", wlc.Entries.SelectMany(e => e.Words).Select(w => w.Morphology).Distinct().All(c => OshbPortuguese.Describe(c).Contains(c)));
                    var spans = SearchPresentation.Spans(greekHits.Hits.First(h => h.Verse.Book == 42 && h.Verse.Chapter == 1 && h.Verse.VerseStart == 1));
                    Check("Result spans preserve full accented original", string.Concat(spans.Select(s => s.Text)) == greekHits.Hits.First(h => h.Verse.Book == 42 && h.Verse.Chapter == 1 && h.Verse.VerseStart == 1).Verse.Text && spans.Any(s => s.Match && s.Text == "λόγος"));
                    var longHit = new SearchHit(new VerseEntry("TEST", 0, "Gênesis", "Gn", 1, 0, 1, 1, new string('a', 350) + " coração", true), "", 0, [new("coração", "coracao", 351, 7)]);
                    Check("Late match beyond old excerpt limit remains boldable", SearchPresentation.Spans(longHit).Any(s => s.Match && s.Text == "coração"));
                    Check("Book filtering retains only requested canonical book", greekHits.Hits.Where(new BookFilter("João", 42).Matches).All(h => h.Verse.Book == 42) && greekHits.Hits.Count(new BookFilter("Antigo", -2).Matches) == 0 && greekHits.Hits.Count(new BookFilter("Novo", -3).Matches) == greekHits.Hits.Count);
                }
            }
        }
        catch (Exception ex) { checks.Add(new { name = "Unhandled test failure", passed = false, error = ex.ToString() }); failures++; }
        int reportIndex = Array.IndexOf(args, "--report");
        string report = reportIndex >= 0 && reportIndex + 1 < args.Length ? args[reportIndex + 1] : Path.Combine(Environment.CurrentDirectory, "verbum-test-results.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new { passed = failures == 0, total = checks.Count, failures, checks }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
