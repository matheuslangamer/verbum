# Léxicos e parsing — 0.8.0

## Fontes incorporadas

- BDB integral: 10.022 registros; Brown–Driver–Briggs, edição digital de Eliran Wong / Marvel Bible, dados de Tim Morton e John Isett. Repositório declara documento de domínio público. Revisão `6e8326c6884f8ef1399c7a365d660da9bb9e2d01`. Conversão de HTML em texto inerte; IDs e relações Strong preservados, inclusive registros sem Strong. Fonte: https://github.com/eliranwong/unabridged-BDB-Hebrew-lexicon
- UBS Dictionary of Biblical Hebrew: 7.943 registros da edição portuguesa 0.9.4, © UBS 2023, adaptada do SDBH © 2000–2023 UBS. CC BY-SA 4.0. Revisão `a15a96cfaa4ca18a6c0339016645ba1e534db3c8`. Extração dos campos lexicais, sem tradução adicional; avisos de IA/revisão humana da fonte são mantidos. Códigos aramaicos A da fonte normalizados a H para consulta Strong. Referências compactas permanecem rotuladas no formato original, sem assumir equivalência à ARA. Fonte: https://github.com/ubsicap/ubs-open-license/tree/main/dictionaries/hebrew
- Holladay: índice local de 656 páginas do PDF fornecido pelo usuário, sem redistribuição como recurso aberto. OCR não revisado, sem extração automática de lemas ou associação a Strong. Algumas páginas não possuem texto. PDF original permanece em Downloads; mover o arquivo impede o botão de abertura até atualizar o índice. O índice pessoal foi excluído do versionamento.

Fontes brutas e licença UBS em `biblioteca/fontes/BDB` e `biblioteca/fontes/UBS`. Reconstrução: `tools/import_lexicons.py`, opcionalmente passando o caminho do PDF pessoal. Os dados externos não executam HTML/JavaScript.

Os links ENG-B-LEB2012-cc-USFM e Hebrew-Bible-JSON-with-Nikkud são textos bíblicos, não dicionários de parsing. Não foram adicionados como léxicos nem substituíram BHS/WLC.

## Morfologia da BHS

A BHS importada originalmente não possui código morfológico. Ao carregar essa edição, o VERBUM consulta a ocorrência WLC/OSHB da mesma referência MT. O empréstimo exige igualdade da sequência consonantal de todo o versículo, limites completos de segmentos OSHB e igualdade da forma vocalizada, ignorando cantilação. Não faz inferência pelo Strong. Análises existentes não são sobrescritas. Diferenças de texto, vogais ou segmentação ficam sem empréstimo. Formas ainda ausentes podem ter sugestões do arquivo pessoal, identificadas separadamente.

O tooltip marca OSHB e o painel detalhado mostra referência e identificadores usados. O texto bíblico, offsets de seleção, numeração e notas não são modificados.

No corpus atual: 426.581 unidades BHS; 363.289 com morfologia alinhada; 63.292 sem morfologia de ocorrência por esse procedimento. São unidades de palavra/segmento da fonte, não um número de verbos. Detalhes em `morphology-coverage.json`. O parsing permanece parcial.

O arquivo `biblioteca/fontes/OSHB/.../parsing/HebrewMorphologyCodes.html` define a decodificação portuguesa usada. Texto WLC em domínio público; anotações OSHB CC BY 4.0, atribuição já presente no recurso local.

## Ajustes 0.8.2
Referências compactas UBS são exibidas como livro, capítulo e versículo na numeração da fonte; elementos do mesmo versículo são reunidos. Os códigos completos ficam na dica de consulta. Listas expandidas têm rolagem própria e o cabeçalho alterna entre mostrar/ocultar. Formas adicionais do Strong são expansíveis desde 0.8.1, e a troca de verbete reinicia a rolagem.

## Leitura hebraica 0.8.4
O controle de leitura e o documento usam a mesma direção RTL para BHS e WLC. Isso evita o espelhamento de alinhamento entre o documento hebraico e seu controle hospedeiro WPF. Edições portuguesas continuam LTR.
