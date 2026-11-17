# Logos instalado — regras do Guia Exegético

Inspeção em 13/11/2026 com REA. Leitura estática dos assemblies instalados em `C:\Users\mathe\AppData\Local\Logos\System`. Não foram executados assemblies, abertos bancos pessoais, modificadas preferências ou alterado o VERBUM. As observações abaixo descrevem esta compilação; não autenticam a configuração efetiva da interface aberta.

## Conclusão

O Guia Exegético tem um modelo de seções que recebe uma referência bíblica e coordena consultas independentes. O caminho de palavra por palavra utiliza dados estruturados de palavras, lemas, morfologia e, quando disponível, alinhamento interlinear reverso. As glossas resumidas têm uma consulta própria, distinta da consulta a verbetes de léxicos. Essas características são sustentadas pelo modelo XML embutido e pelas chamadas CIL examinadas; não equivalem a observar uma sessão em execução.

## 1. Entrada e seções padrão

O recurso `Libronix.DigitalLibrary.Guides.Templates.ExegeticalGuide.lbxguide` declara `kind="biblereference"`. Seu conteúdo extraído está em [logos-exegetical-template.lbxguide](logos-exegetical-template.lbxguide). SHA-256 do conteúdo: `08f037a25d04e537cb703ddb6d94d6cbd817901e83ea4bc07b3b21f1487ac198`, idêntico ao digest do recurso informado pelo REA.

Ordem declarada no modelo:

| Identificador compilado | Finalidade indicada pelo nome e contexto |
|---|---|
| MyContent | Conteúdo pessoal |
| TextualVariants | Variantes textuais |
| WordByWord | Palavra por palavra |
| GrammaticalConstructions | Construções gramaticais |
| ImportantWords2 | Palavras importantes |
| LemmasInPassage | Lemas na passagem |
| ImportantPassages | Passagens importantes |
| AncientLiterature | Literatura antiga |
| Commentaries | Comentários |
| Journals | Periódicos |
| Grammars | Gramáticas |
| Visualizations | Visualizações |

A presença e a ordem são fatos do modelo padrão. As descrições em português acima são explicativas, não nomes da interface portuguesa autenticados em execução. Um modelo personalizado, recursos indisponíveis e preferências podem produzir outra apresentação.

`GuideTemplate` contém operações para adicionar, inserir, remover, mover e substituir seções, criar modelos de usuário e carregar/salvar XML. `GuideSection` declara suporte à chave de entrada, disponibilidade por família do aplicativo, atualização, cancelamento, expansão, notas e salvamento de estado. Não se deve confundir a existência dessas operações com a confirmação de todos os seus caminhos em execução.

## 2. Regras de Palavra por palavra

O modelo padrão declara:

- `ShowAllWords=true`.
- `LemmaOccurrenceCountThreshold=30`.
- `ShowAllPartsOfSpeech=false`.
- Classes selecionadas: verbo, substantivo, adjetivo e advérbio.

Os campos constantes de `WordByWordSection` confirmam esses padrões e uma paginação interna de 10. O código também possui controles para pronome, preposição e conjunção. O limiar 30 é configurável; não representa quantidade máxima de palavras na passagem.

O fluxo `DoCreateOriginalWordAsync.MoveNext` consulta `ShowAllWords`, frequência do lema e classe morfológica. Com o filtro por frequência ativo, compara a contagem ao limiar. O caminho simples observado admite `contagem <= limiar`; outro caminho, que envolve múltiplas análises, tem comparação `contagem >= limiar` para pular um candidato. Portanto, não é correto afirmar uma única regra universal de inclusão exatamente no valor 30 sem revisar todos os casos de agrupamento. A regra geral comprovada é a filtragem configurável por frequência e classe, com tratamento próprio para palavras compostas/múltiplas análises.

`GetLemmaOccurrenceCounts` constrói uma consulta pelo lema, usa `SingleResourceRange` e lê `SearchResultSummary.HitCount`. Isso sustenta contagem de resultados de busca no recurso escolhido; não é contagem de grafias idênticas apenas dentro do trecho. Não foi autenticada a equivalência entre cada hit e uma palavra gráfica em todos os formatos de recurso.

## 3. Texto original, tradução e parsing

Há caminhos separados para recurso padrão e interlinear reverso:

- `DoUpdateTextStandardResourceAsync` abre o recurso selecionado, extrai unidades estruturadas e procura um interlinear reverso associado ao texto e referência.
- `DoUpdateTextReverseInterlinearAsync` converte a referência ao sistema local e chama `GetTextAlignment`.
- `JoinWords` utiliza forma, sufixo, lemas, códigos morfológicos e número da palavra das unidades originais.
- `WordByWordVerseModel` mantém intervalos original e traduzido, alinhamento e palavras originais.
- `OriginalLanguageWordModel` mantém lema, glossa, transliteração, comparação morfológica, sentido, vínculos a recursos e ao estudo da palavra.

Conclusão técnica: o guia aproveita anotações e alinhamentos fornecidos pelos recursos. A análise examinada não demonstra que ele deduza o parsing ou a tradução de uma palavra simplesmente pela semelhança de letras. Uma tradução alinhada na passagem, uma glossa do lema e uma definição de dicionário são dados distintos.

## 4. De onde vem a glossa e como o idioma entra

O caminho comprovado é:

`WordByWordVerseModel.GetGlosses`
→ `Culture.Current.LanguageName`
→ `LemmaUtility.GetGlossesFromMostPreferredAnalysisGroup`
→ `ResourceLists.OpenLemmasResource`
→ `LemmasResource`
→ `LemmasResourceCore`.

O recurso de lemas usa `lemmas.db`. Os literais SQL examinados consultam `Gloss`, relações como `LemmaGloss`, identidade do lema, idioma e grupo de análise. Há também equivalências entre lemas, grupos com análise/idioma/corpus/variante/prioridade e o identificador `userAnalysisGroupPriorities`.

`LemmasResourceCore.GetBestLanguage` chama `Culture.Create(...).FindBestMatch(...)` sobre os idiomas suportados. Portanto, o idioma solicitado é negociado com os idiomas do recurso; não há nesta evidência uma regra universal comprovada de “sempre português, depois inglês”. O ramo de grupo preferido ordena grupos, escolhe um deles e filtra as glossas relacionadas; a implementação exata da chave de ordenação não foi examinada por inteiro.

Separadamente, `OriginalLanguageWordModel.GetGlossText` chama `LexiconGlossUtility.GetGlosses`, converte o texto rico para texto simples e elimina duplicatas. Isso explica a existência de um caminho para glossas extraídas de léxicos além da glossa do banco de lemas. Não demonstra uma tentativa automática de todo dicionário da biblioteca, nem tradução automática do inglês.

Consequência prática inferida: um verbete existir em um léxico não garante, sozinho, uma glossa resumida em qualquer idioma e em toda ocorrência. Identidade do lema, grupo de análise, idioma suportado, recurso e disponibilidade do dado influenciam o resultado.

## 5. Gramáticas e construções

`GrammarCrossReferencesSection` usa os identificadores `feature:grammar-xrefs` e `text.monograph.grammar`. Isso indica seleção por metadados de gramática e suporte a referências cruzadas, não simplesmente por um arquivo conter uma palavra do versículo. O algoritmo completo de ordenação dos livros não foi confirmado.

`GrammaticalConstructionsSection` contém agrupamento por lema da preposição e caso/lema do objeto, além de `LabelPatternRenderer.CreateSearchQuery`. As construções geram consultas e referências estruturadas. Essa seção é diferente de uma lista de referências em gramáticas e de uma simples expansão do código morfológico.

## 6. Variantes textuais

Os classificadores compilados incluem:

- Comentários textuais: `textual-variants:textual-commentary`.
- Edições modernas: `textual-variants:modern-edition`.
- Transcrições: `textual-variants:transcription`.
- Versões antigas: `textual-variants:ancient-version`.
- Aparatos: `text.monograph.critical-apparatus.bible`.

`CreateModels` cria grupos diferentes conforme os tipos/classificadores disponíveis. O ramo de manuscritos consulta suporte à referência, estado online e `IsFeatureUnlocked` com `FEATURE:ONLINE-MANUSCRIPTS` antes de construir seu modelo. Essa exigência pertence ao ramo examinado; não significa que todo o Guia Exegético exija internet ou essa licença.

## 7. O que não ficou determinado

- Fórmula de classificação de Palavras importantes e Passagens importantes.
- Ordem integral de priorização de todos os léxicos, comentários e gramáticas.
- Desempates entre grupos de análise e a sequência exata de idiomas de reserva.
- Quais recursos/licenças da conta estão ativos e quais seções aparecem na sessão atual.
- Comportamento observado em execução para uma passagem específica.

Não foram necessárias alterações no Logos, acesso a credenciais ou contorno de licenças. Nenhuma regra desta investigação foi incorporada ao VERBUM nesta etapa.

## Evidências e limites

Os registros selecionados do REA, com corpos CIL, chamadas, constantes e SQL, estão em [logos-guia-exegetico-evidencias.json](logos-guia-exegetico-evidencias.json). A extração do XML foi feita por leitura dos bytes PE/CLI, sem carregar o assembly; o hash corresponde ao recurso inventariado pelo REA.

| Artefato | Identidade |
|---|---|
| Libronix.DigitalLibrary.Guides.dll | Assembly 54.3.0.5; SHA-256 `8dac60178cd228ea2cf1836ee891127c4739cdb7c172fdcf3bbfbd3d02bd5f18`; MVID `b7e289bd-c352-49de-8a20-09e819060c3c` |
| Libronix.DigitalLibrary.ResourceDrivers.dll | Assembly 77.2.2.0; SHA-256 `397ece93b19344d7267e99d2f5caa72402bc851e109960b9da59302c1733c923`; MVID `3b36940d-4308-45ff-90e1-db11cf36973e` |

Evidências principais de membros selecionados: `ev_cbbadd282d069d5c2eb07d3f6380124d436620a0540b2bd552dadd3506c110d8` (modelo/seção); `ev_3e00602a54efc29e6ffce55b2511b4afc10a86ff6fe68c621aa10f7c2f0ea348` (modelos de palavras); `ev_4d4683719fe16685063817f1c0f70246d6fa03daa7d4fb1bbe5c2ce3093564d0` (gramática/variantes); `ev_b67d3a7c87c40e8bfa1f7402fda37bb8c92fc44724f536a2b31d9184c7494ee3` (núcleo de lemas). Os demais pais e tokens estão nos registros salvos.

A tentativa inicial de inventário integral de membros do assembly de guias excedeu a capacidade de representação do REA. Foram então selecionados tipos menores, com cobertura completa dentro das seleções informadas. Isso não constitui cobertura integral de todos os subsistemas do Logos. Tokens são locais à compilação e precisam ser interpretados com SHA-256/MVID. Chamadas e nomes compilados não constituem observação de execução nem recuperação do código-fonte original.
