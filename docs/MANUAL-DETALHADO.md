# VERBUM

Leitor bíblico Windows para estudo offline, com blocos redimensionáveis, abas e pesquisa sensível ao texto original.

## Abrir

Execute **VERBUM.exe**, na pasta principal. Há uma cópia idêntica em **dist/VERBUM.exe**. É uma publicação Windows x64 com o runtime .NET incluído; o usuário do aplicativo não precisa instalar o SDK.

A partir da versão 0.9.0, mantenha também a pasta **native** junto do EXE: ela contém o mecanismo de leitura PDF e seus avisos de licença. Os textos e materiais continuam na pasta **biblioteca**.

## Biblioteca de livros e apostilas

O botão **Biblioteca** abre o acervo de documentos; **Bíblias ▾** conserva o menu de traduções. Use **Adicionar livros e documentos** para importar PDF, EPUB sem criptografia, DOCX, DOC, RTF, TXT, Markdown e HTML. DOC/RTF usam o Microsoft Word instalado, com macros desabilitadas; os demais formatos não dependem de Word. Os originais são copiados para `biblioteca/livros/originais`, e os índices ficam em `biblioteca/livros`. Arquivos idênticos não são duplicados.

PDFs abrem dentro do VERBUM com página original, seletor de página, anterior/próxima e zoom. **Texto pesquisável** alterna para o texto extraído, com seleção e destaque da consulta. Não há OCR automático: páginas digitalizadas sem texto permanecem legíveis como imagem, mas não entram na pesquisa. A qualidade da pesquisa depende da camada de texto do PDF.

DOCX e EPUB abrem em leitura contínua, com seções/capítulos, ajuste de fonte e tabelas. A conversão conserva texto, tabelas e imagens comuns (PNG/JPEG e outros formatos aceitos pelo WPF); clique numa figura para abrir a ampliação. Notas e a diagramação integral desses formatos não são reproduzidas. EPUBs com recursos criptografados são recusados. A página ou capítulo aberto fica salvo na aba, que pode ser movida entre blocos como as demais.

Marque **Pesquisar também nos PDFs e livros** para incluir os documentos na pesquisa inteligente. Resultados adicionais aparecem na seção **PDFs e livros**, com título, página/seção e trecho destacado. Clique para abrir a seção correspondente; PDFs abrem no modo de texto para mostrar a palavra em negrito e realce, e podem voltar à página original. No modo inteligente, a busca ignora acentos e sinais hebraicos; exige todos os termos na mesma página/seção. Prefixo, trecho e grafia exata também são aplicados. Strong e lema, que pertencem aos textos bíblicos anotados, são tratados como texto comum nos documentos. Não há busca semântica por conceitos ou OCR em segundo plano.

A coleção inicial de Hebraico IV reúne os 12 arquivos fornecidos e **Hebraico IV — apostila de estudo**, em 19 capítulos, com cerca de 7.200 palavras, tabelas, leituras guiadas, 16 exercícios, respostas comentadas e mapa de fontes. O EPUB independente está em `biblioteca/livros/Hebraico IV — apostila de estudo.epub`, e sua fonte editável em `biblioteca/apostila-hebraico-iv.md`. Os arquivos de aula têm numeração interna nem sempre igual ao nome: `aula 09.docx` trata de Daniel 8 e `aula 10.docx` de coordenação e Jonas 2. A apostila mantém essa distinção e não inventa uma aula 05 ausente.

O programa procura `biblioteca/biblias` ao lado do projeto ou do executável. Se não encontrar, use **Pasta da biblioteca**. O formato aceito nesta versão é uma lista JSON de livros:

```json
[{"abbrev":"Gn","name":"Gênesis","chapters":[["Texto do primeiro versículo."]]}]
```

Os arquivos JS não são executados. Os textos existentes permanecem separados do EXE. Antes de redistribuir uma tradução, confira a autorização específica da edição; o nome de uma tradução e a existência de um JSON não estabelecem sua licença.

## Telas mais rápidas (0.17.1)

Abrir uma análise, criar uma aba, dividir ou fechar um bloco reconstruía todas as abas de todos os blocos, que ficavam em branco enquanto carregavam de novo. Agora as telas cuja aba não mudou são mantidas. Os cartões de hover são montados só quando o mouse para sobre a palavra. A BHS abre em cerca de 4 s em vez de 7,5 s (leitura em paralelo com o WLC-OSHB), e o leitor mostra "Abrindo…" durante a carga. Medição: `dist\VERBUM.exe --perf-report ARQUIVO.txt --library …`.

## Parsing no hover e termo original da tradução (0.17.0)

Passe o mouse em uma palavra hebraica: o cartão mostra a palavra inteira, com uma linha para cada prefixo, para a palavra principal e para cada sufixo, além de lema, Strong, glossa e a expressão da ARA ligada a ela. Na BHS, o parsing por ocorrência cobre agora 417.961 dos 426.581 segmentos (antes, 363.289).

Passe o mouse em uma palavra da **ARA** no Antigo Testamento para ver o termo hebraico ligado, com parsing e referência MT. Clique na palavra e escolha **Analisar o termo original (hebraico)…** para abrir a análise no WLC-OSHB.

A ligação português ↔ hebraico é um **alinhamento estatístico automático**, calculado dos textos locais e gravado em `biblioteca/alinhamento/ARA.json.gz`. Cada ligação é marcada como provável, plausível ou incerta; não é um interlinear reverso revisado. Para outra tradução: `dist\VERBUM.exe --build-alignment NAA --library CAMINHO\biblioteca\biblias`. Detalhes, método e limites em [docs/alinhamento-0.17.0.md](docs/alinhamento-0.17.0.md).

## Editor e informações da nota (0.16.0)

No editor visual, **MD <>** abre o código Markdown e passa a se chamar **Normal** para retornar. **Lado a lado** alterna a edição com prévia. Um clique direito nesse botão oferece a prévia isolada. **Título** tem seis níveis, **¶** aplica o parágrafo comum e o botão de aspas permite escolher a cor da borda da citação. Os botões de lista e vínculo têm ícones.

Selecione o trecho e escolha a cor em **Marcador** ou **Letra**. O editor recupera o trecho selecionado ao aplicar a cor. O **+** ao lado de **Cor da nota** mostra datas, tipo, links, passagens e etiquetas. Links e passagens adicionados pelo painel são anexados à nota. Etiquetas adicionadas pelo painel ficam nos metadados e participam da busca e do grafo. Datas ausentes em notas antigas aparecem como “sem registro”.

## Catálogo e cores das notas (0.15.0)

**Notas**, **Grafo** e **Lixeira** compartilham o menu superior. As listas de notas ativas e excluídas têm busca, prévia e ações por linha. **Cor da nota** identifica a nota no título do editor, nas listas e no nó do grafo.

Na barra de edição, selecione o trecho e escolha **Marcador** para o fundo ou **Letra** para a cor do texto. As duas cores podem ser combinadas. O menu **Título** oferece três níveis. No modo MD, `==texto==` representa o realce padrão. Cores específicas ficam em elementos HTML inline no Markdown.

## Notas nas palavras e grafo (0.14.0)

Clique em uma palavra na tradução e escolha **Criar nota nesta palavra ou seleção…**. Para anotar uma expressão, selecione-a em um único versículo e use o botão direito. Nos textos originais, o botão direito oferece a anotação e o clique simples abre a análise lexical. O interlinear também permite anotar pelo menu da palavra.

O sinal **✎** após a palavra ou expressão abre a nota. O botão **✎** da barra do leitor mostra ou oculta esses sinais por aba. Na nota, a tag inicial (por exemplo, **#luz**) leva de volta à ocorrência com realce. Use Ctrl+clique no modo Normal ou clique na Prévia. Se o texto da edição mudar, a âncora deixa de ser exibida para evitar apontar para outra ocorrência.

Em **Notas → Grafo**, notas e hashtags aparecem em uma rede distribuída pelas conexões. Clique em uma nota para abrir ou em uma tag para filtrar as notas ligadas a ela. Inclua outras hashtags diretamente no texto. Em **Notas → Lixeira**, cada linha reúne a prévia da nota, **Restaurar** e o ícone de exclusão. O ícone pede confirmação e remove o JSON e o Markdown daquela nota. Depois disso, a restauração pela lixeira fica indisponível.

## Editor de notas

O botão **Notas**, na lateral esquerda (ícone de caderno com lápis), abre **Suas notas e comentários**, com pesquisa por título e conteúdo. A lista reúne notas abertas e salvas, sem duplicar a mesma nota. **+ Nova nota** cria uma anotação; clicar numa nota existente a reabre. O menu anterior **Minhas notas** também abre essa lista.

O botão **Normal** oferece edição visual do texto formatado, com negrito, itálico, títulos, listas, citações, vínculos e desfazer/refazer. **Editar MD** mostra o código Markdown. **Texto** retorna o parágrafo ao estilo comum. As notas continuam armazenadas em Markdown; abrir o modo visual sem editar não regrava a sintaxe existente. **Prévia** oferece leitura e links clicáveis; **Lado a lado** mostra código e prévia nos blocos largos. Atalhos: **Ctrl+B**, **Ctrl+I**, **Ctrl+K**, **Ctrl+Z** e **Ctrl+Y**. No modo Normal, use **Ctrl+clique** para abrir os vínculos.

**Excluir…**, dentro da nota, ou **Excluir**, na lista, move a nota para **Notas → Lixeira**, mediante confirmação. **Restaurar** recupera o título, o conteúdo e a identidade original, mantendo os vínculos. Fechar a aba continua sendo diferente de excluir a nota.

**Notas → Grafo** mostra as conexões criadas por links entre notas, com pesquisa, zoom e abertura ao clicar nos nós. Na aba de uma nota também aparecem referências recebidas de outras notas. O grafo considera os vínculos reais, sem inferir conexões por semelhança de palavras. Links em blocos de código não entram no grafo.

Selecione um trecho e clique em **Link…** (ou Ctrl+K). Escolha **Passagem bíblica**, **Nota / comentário pessoal**, **Livro / comentário da biblioteca** ou **Página web**. O vínculo bíblico guarda a edição e a referência, aceitando intervalos do mesmo capítulo. O vínculo de livro guarda a página/seção. Na **Prévia**, clique no texto sublinhado para abrir o destino. Passagens abrem no início do intervalo; notas já abertas são reutilizadas. Comentários podem ser suas próprias notas ou livros importados; nenhum comentário externo é incluído automaticamente.

As notas continuam salvas em JSON e Markdown na pasta pessoal. O modo do editor é incluído na sessão; o modo MD também conserva sua seleção. Tabelas e sintaxes avançadas ainda podem exigir edição MD. A revisão preserva blocos não editados em sua forma original. Veja [edição visual e lixeira](docs/notas-0.13.0.md) e o [histórico dos vínculos](docs/notas-0.12.0.md).

## Aparência clara e escura (0.11.0)

O botão de **lua/sol**, no rodapé da lateral, alterna entre os modos claro e escuro. A preferência é salva localmente em `appearance.txt`, separada da sessão de estudo. A troca atualiza as cores sem recriar blocos, fechar abas ou deslocar a leitura. Textos originais mantêm suas fontes. Cores pessoais de letra e destaque são preservadas exatamente como escolhidas.

A interface usa superfícies neutras, acentos azulados, seletores e barras de rolagem adaptados ao tema e ícones vetoriais Lucide incorporados ao executável. Nenhum ícone é baixado ao abrir o aplicativo. A licença está em `native/licenses/lucide-LICENSE.txt`. As páginas originais dos PDFs e as imagens dos livros conservam suas cores.

## Interface compacta (0.10.4)

A pesquisa fica ao lado do nome VERBUM, na mesma linha dos comandos da área de estudo. As opções de modo e escopo abrem abaixo apenas quando solicitadas.

A lateral começa recolhida; **☰** alterna entre ícones e nomes das ferramentas. Passe o mouse sobre um ícone para identificar a função. O botão ao lado da pesquisa mostra o modo atual (**Inteligente**, **Strong**, **Lema** etc.) e abre modo, escopo e documentos. **Ctrl+F** e Enter continuam funcionando.

No leitor bíblico, edição, livro, capítulo e sincronização ficam visíveis, junto aos botões **A−**, **A+**, **≡**, **V**, **▰** e **ⓘ**. Os grupos mudam de linha conforme a largura do bloco, sem esconder os comandos. A referência aceita navegação direta por Enter. Nos livros, **⋯ → Pesquisar / modo de texto…** revela a busca e o seletor do PDF. Fontes, filtros e ajustes do guia ficam em **⋯**. A lateral e os painéis de opções guardam seu estado na sessão; suas abas e divisões são mantidas. Veja [revisão da barra de leitura](docs/barra-leitura-0.10.2.md).

## Cor da letra (0.10.3)

Selecione um trecho e abra **▰ → Cor da letra** (ou o menu do botão direito). Escolha azul **#0000FF**, rosa **#FF0066**, vermelho **#FF0000**, verde **#00FF00**, preto, roxo ou laranja. **Outra cor…** aceita qualquer código **#RRGGBB**, com prévia e validação antes de aplicar. **Restaurar cor da letra** remove somente a cor do texto selecionado; **Remover destaque da seleção** remove somente o fundo. As duas marcações podem coexistir e são salvas automaticamente.

Também disponível em **Marcar seleção… → Cor da letra** nos livros e no modo de texto pesquisável dos PDFs. A página original do PDF permanece intacta. No interlinear, a cor salva aparece na palavra; para selecionar um novo trecho, use o modo de leitura. As marcações anteriores continuam compatíveis.

## Área de estudo

- **Biblioteca ▾** reúne as Bíblias em um menu; escolher uma abre uma aba no bloco ativo. A lista fixa foi retirada da lateral. O menu também reúne a escolha da pasta, as notas salvas e a ajuda da pesquisa.
- **Estudo da palavra** abre uma aba com pesquisa por Strong, lema ou forma, seleção do texto original e tradução, gráfico de rosca e distribuição por livro. Ao abrir após selecionar uma palavra, aproveita seu Strong quando disponível. Fatias, legendas e barras abrem as ocorrências na tabela.
- **Léxico hebraico**, o segundo botão da lateral, tem um seletor de fontes: **Strong**, **BDB integral (inglês)**, **UBS (português)** e **Holladay (PDF pessoal)**. Pesquise lema com ou sem sinais, forma completa flexionada, Strong (`H7225` ou `7225`) ou palavras da definição. A aba guarda fonte, busca e verbete selecionado. BDB e UBS mantêm IDs próprios e códigos relacionados; homônimos e sentidos não são fundidos por Strong. O botão do verbete abre o estudo de seu primeiro código relacionado. No PDF, pesquise palavras do OCR ou digite o número da página do arquivo; **Abrir PDF pessoal** abre o original para conferência. O número da página do arquivo pode diferir do número impresso. Páginas sem texto OCR são identificadas; não há ligação automática do PDF a Strong ou parsing. Os recursos ficam em `biblioteca/lexicos`, ao lado da pasta `biblias`, separados do EXE.
- O gráfico conta **versículos na fonte**, sem duplicá-los, e distingue traduções marcadas manualmente dos versículos ainda sem marcação (cinza). A cobertura é parcial; não representa um alinhamento automático completo nem uma contagem de todas as palavras em cada versículo. Refaça o estudo com **Estudar** após novas marcações para atualizar o gráfico. As opções e a aba são salvas na sessão.
- A partir da versão 0.6.1, o estudo também mostra automaticamente **Glossas possíveis · português**, extraídas do arquivo pessoal de parsing pelos códigos Strong das palavras encontradas, mesmo sem vínculos manuais. São glossas automáticas provisórias, sem atribuição à ARA ou a outra tradução. Cada botão consulta se a expressão aparece literalmente nos versículos paralelos comparáveis; o clique filtra esses paralelos e destaca a expressão portuguesa. Essas contagens representam coocorrências, podem se sobrepor e não comprovam que a expressão traduza aquele lema. O gráfico de traduções confirmadas continua separado. Quando não há glossa portuguesa, a definição inglesa disponível é exibida.
- **+ Bloco** ou **↔** divide lado a lado; **↕** divide acima e abaixo. As barras entre blocos são arrastáveis. Até oito blocos.
- Arraste o título de uma aba: solte no **centro** de um bloco para integrá-la como aba; nas bordas **esquerda, direita, superior ou inferior** para criar outro bloco. Uma prévia indica o destino. O bloco de origem vazio é recolhido. Esc cancela. Há também opções no menu de contexto do título.
- **+ Notas** cria uma nota; **Minhas notas** permite reabrir notas salvas.
- Selecione livro e capítulo, ou digite uma referência como `João 3:16` e pressione Enter.
- Os grupos **A**, **B** e **C** sincronizam a navegação entre abas pelo **mapa de referências**, nos dois sentidos. **Livre** mantém a aba independente. Clique no número de um versículo para acompanhar sua correspondência no grupo; a barra inferior mostra a ligação usada. Cada Bíblia conserva sua numeração nativa.
- O leitor mostra edição, livro, capítulo, sincronização e a referência digitável. Os botões **A− / A+**, **≡**, **V**, **▰** e **ⓘ** dão acesso direto a fonte, interlinear, variantes, marcações e informações da edição. O texto pode ser selecionado e copiado.
- Layout, abas, referências, fontes e notas são salvos automaticamente. **Ctrl+S** salva imediatamente.

Os dados pessoais ficam em `%LOCALAPPDATA%\Verbum`: `session.json`, backup da sessão e a pasta `notes`. Fechar a aba de uma nota não apaga sua cópia salva.

Ao fechar o VERBUM, **Salvar onde parou?** oferece **Sim**, **Não** e **Cancelar**. **Sim** salva a posição e a área atuais para a próxima abertura. **Não** mantém a posição/área do início desta sessão ou do último **Salvar área/Ctrl+S**. As notas e marcações são preservadas nas duas opções. **Cancelar** mantém o programa aberto. Se houver erro ao salvar, a saída é cancelada para permitir nova tentativa. O salvamento automático durante o uso continua funcionando como recuperação.

### Marcadores coloridos

Selecione um trecho no leitor e use **▰**, ou o botão direito, para escolher amarelo, verde, azul, rosa ou laranja. **Remover cor da seleção** limpa somente o trecho selecionado. As cores são salvas em `%LOCALAPPDATA%\Verbum\text-markers.json`, com backup; não alteram o JSON bíblico. As marcações pertencem à edição e ao texto exatos. No interlinear, as cores já salvas são exibidas; volte à leitura normal com **≡** para selecionar novos trechos.

## Guia Exegético

**Guia exegético**, na lateral, reúne a passagem original e seu paralelo, parsing e glossas por palavra, consulta aos léxicos, estrutura sintática MACULA, variantes/notas da fonte e comentários UBS HOTTP. Use WLC-OSHB para hebraico/aramaico (numeração MT) ou SBLGNT para grego; aceite capítulos ou intervalos como `Joel 1:1-3`. No menu **⋯** do guia, **Notas da passagem** cria ou reabre uma nota Markdown vinculada ao intervalo e **Copiar resumo Markdown** copia a página atual com suas fontes. **Fontes e filtros…** abre os ajustes quando necessários.

As anotações abertas cobrem 929 capítulos hebraicos e 260 gregos; HOTTP oferece 4.515 comentários textuais em inglês. Os índices comprimidos ficam em `biblioteca/exegese`, separados das Bíblias. A ligação às palavras exige correspondência textual; glossas e sintaxe não são alinhamento automático com o português. Consulte [uso, fontes e limites](docs/guia-exegetico-0.10.0.md).

## Pesquisa inteligente

**Ctrl+F** foca a pesquisa. Digite palavras e pressione Enter. Selecione a aba ativa, uma versão específica ou toda a biblioteca.

- **Inteligente · sem sinais:** procura palavras inteiras ignorando marcas combinantes, acentos, sinais vocálicos e cantilação. `coracao` encontra `coração`; `שלום` encontra as formas apontadas correspondentes quando presentes no acervo. O texto exibido nunca é substituído pela chave normalizada.
- Nos originais, a busca inteligente também considera os segmentos explicitamente fornecidos pela fonte. Assim, `אהלות` encontra `אֲהָלֹות` mesmo ligado ao prefixo waw em `וַאֲהָלוֹת`: Salmos MT 45:9 e Cântico dos Cânticos 4:14. A segmentação vem das palavras BHS e das barras de morfemas OSHB, não de remoção arbitrária de letras. O modo exato continua respeitando a palavra gráfica e seus sinais.
- **Exata · com sinais:** exige a grafia com seus sinais. Formas Unicode canonicamente equivalentes são comparadas como iguais. Maiúsculas e minúsculas são equivalentes.
- **Começa com:** encontra prefixos de palavras, sem sinais.
- **Trecho de palavra:** encontra uma sequência dentro de uma palavra, sem sinais.
- Várias palavras usam **E** na mesma entrada, em qualquer ordem; não é busca de frase na sequência exata.
- Correspondências exatas aparecem primeiro. Os botões de grafias mostram a contagem de palavras encontradas, que pode ser maior que a contagem de entradas/versículos.
- Se uma busca de uma só palavra não retornar ocorrências, o programa sugere grafias do acervo com até uma inserção, remoção ou substituição. São aproximações ortográficas, não sinônimos.
- Clique em um resultado para abrir o contexto e destacar os termos. Os resultados são apresentados em lotes de 100; **Mostrar mais** continua a lista.
- Nas ocorrências, os termos encontrados aparecem em **negrito e realce**, preservando sinais e acentos. O versículo completo evita esconder correspondências após o antigo limite do resumo.
- A coluna portuguesa também usa **negrito e realce**: prioriza o vínculo manual e a expressão filtrada. Na ausência deles, procura sugestões pelas glossas do Strong e algumas flexões verbais portuguesas (por exemplo, colocar → colocou; pôr → pôs). O tooltip identifica esses realces como **sugeridos**, pois coocorrência e flexão não comprovam alinhamento. Eles não são gravados como vínculos nem entram no gráfico de traduções confirmadas. Sem evidência nas glossas, o trecho fica sem sugestão; você pode selecionar a expressão correta e confirmar pelo botão direito.
- O filtro **Todos os livros** restringe por livro ou testamento sem repetir a consulta; a contagem informa a seleção e o total.

### Paralelos e formas de tradução

O seletor ao lado do filtro de livros escolhe a tradução paralela (ARA por padrão). As ocorrências são uma tabela com cabeçalho fixo: referência, texto pesquisado e tradução lado a lado. As linhas têm separadores e fundo alternado. A orientação sobre versificação fica na dica do cabeçalho da tradução; a numeração editorial dos JSONs portugueses não foi autenticada. Grafias e contagens de traduções ficam em seções recolhidas para reduzir a poluição visual. A BHS mantém a numeração MT na leitura e utiliza o mapa KJV original somente para o paralelo, quando a correspondência é única; a referência portuguesa diferente é mostrada na célula. No WLC, sem esse mapa, informe manualmente a referência portuguesa. Intervalos como MENS permanecem intervalos da fonte.

Não há base de alinhamento entre Strong e as traduções portuguesas. Por isso, o programa não infere automaticamente que uma expressão traduz determinado lema nem produz contagens editoriais completas. Você pode selecionar uma expressão no paralelo e usar o botão direito e escolher **Vincular seleção à pesquisa**. Ela fica em negrito e as **traduções marcadas por você** mostram quantos versículos receberam cada expressão, para aquela pesquisa, tradução e filtro de livro. Prefira pesquisar por Strong/lema para uma investigação lexical.

As contagens são parciais e não incluem passagens ainda não marcadas. Clique numa expressão para filtrar suas marcações; **Todas as ocorrências** restaura o conjunto do filtro. Repetir uma marcação substitui a anterior; **Desfazer vínculo** remove a associação. As marcações ficam em `%LOCALAPPDATA%\Verbum\translation-links.json`, com backup. Os textos bíblicos não são alterados. Mudanças no texto original ou paralelo invalidam marcações antigas, evitando reaproveitamento silencioso.

O índice é preparado em memória na primeira pesquisa de cada versão e reutilizado enquanto o programa estiver aberto. A busca e os textos não são enviados à internet.

### Hebraico e grego

A busca implementa normalização Unicode e remoção de marcas para a comparação. Consoantes diferentes, letras hebraicas finais, matres lectionis e diferenças de palavras permanecem distintas. A remoção de pontos pode reunir palavras de sentidos diferentes, inclusive distinções marcadas pelos pontos de shin/sin. O modo exato está disponível para restringir a grafia.

O menu **Originais** abre os recursos incorporados na versão 0.2.0. A normalização também funciona nos textos reais. Para encontrar uma palavra dentro de uma forma com prefixos hebraicos, use **Trecho de palavra**; a busca inteligente padrão exige a palavra inteira.

## Línguas originais e Strong

| Recurso | Conteúdo incorporado | Referências |
|---|---|---|
| BHS-A-Strong | 39 livros, 23.213 versículos; texto BHSA segmentado com Strong e códigos estendidos da fonte | MT nativas, recuperadas por alinhamento com ETCBC BHSA c |
| WLC-OSHB | 39 livros, 23.213 versículos; Strong, códigos morfológicos e 1.388 notas de leitura/qere | Numeração MT original |
| SBLGNT | 27 livros, 7.939 versículos; grego e 6.934 notas do aparato de comparação de edições | Referências SBLGNT |

- Clique em uma palavra original para abrir sua análise em outra aba/bloco. São exibidos os dados que a fonte fornece: lema, Strong, morfologia, identificador e origem.
- Clique em **≡** para alternar o interlinear lexical: palavra, lema, Strong e morfologia disponíveis. Não há alinhamento artificial com uma tradução portuguesa.
- Clique no código Strong no painel para pesquisar suas ocorrências; também há os modos **Código Strong** e **Lema da fonte** na pesquisa. O lema vem dos dados importados, sem inferência automática para palavras portuguesas.
- O léxico Strong hebraico incluído fornece definições em **inglês**, com transliteração quando disponível. Códigos estendidos de prefixos e subdivisões de Strong mantêm a convenção da fonte.
- Clique em **V** para consultar variantes/notas da referência. No WLC são notas da fonte, incluindo qere; no SBLGNT, comparação entre edições. Não é o aparato manuscrito da BHS, NA28 ou UBS.
- O SBLGNT importado não fornece Strong nem morfologia; o programa informa essa ausência.
- Passe o mouse numa palavra original para consultar os dados disponíveis. No WLC, os códigos morfológicos OSHB da ocorrência são expandidos em português (classe, binyan, conjugação, pessoa/gênero/número/estado quando presentes), mantendo o código integral. Não se inventam flexões ausentes.
- O tooltip agora é compacto: palavra, parsing e até duas glossas provisórias. O código integral, o léxico e as análises alternativas continuam no painel aberto ao clicar. Na BHS, quando o arquivo pessoal fornece uma única análise compatível, ela aparece identificada como sugestão; ambiguidades não são resolvidas por suposição.
- O arquivo pessoal de parsing foi copiado para `biblioteca/parsing/hebraico-usuario.md`. Suas análises e glossas provisórias aparecem no tooltip e no painel da palavra como **sugestões**, cruzando forma com vogais, Strong, livro e capítulo. Cantilação e pontuação são desconsideradas nesse cruzamento; vogais não são descartadas. O arquivo não fornece versículos: posição ordinal não é tratada como número de versículo. Ambiguidades são preservadas e a cobertura é parcial. As glossas automáticas em português não são traduções revisadas.
- **ⓘ** mostra créditos, licença, revisão e sistema de referências. Recursos MT não sincronizam automaticamente com as traduções portuguesas/KJV, para evitar falsas correspondências.

Uso preparado para estudo pessoal. Fontes, licenças e revisões estão em `biblioteca/fontes`, nos metadados de cada recurso e em `docs/original-resources-import.json`. Texto WLC: domínio público; anotações OSHB e SBLGNT desta revisão: CC BY 4.0; mapeamentos BHSA/BHS-A: condições CC BY-NC 4.0 e créditos próprios. O PDF BHS da SBL não foi importado. Consulte `docs/fontes-linguas-originais.md` antes de qualquer distribuição.

O conversor reproduzível está em `tools/ImportSources`. Os textos das traduções portuguesas existentes não foram alterados.

### Correção da numeração BHS na versão 0.4.0

A importação anterior agrupava o texto pelos números KJV presentes no CSV, alterando a divisão hebraica. Agora preserva os números MT nativos recuperados do corpus ETCBC BHSA c: Joel tem **20, 27, 5 e 21 versículos em seus quatro capítulos**. Joel MT 3:1 corresponde a KJV 2:28; MT 4:1 corresponde a KJV 3:1. O mapa KJV permanece como metadado para os paralelos e não muda os capítulos exibidos.

Os 426.581 segmentos BHS foram alinhados, na ordem do corpus, às 426.584 posições BHSA c, verificando formas consonantais e leituras qere e permitindo segmentos compostos dentro da mesma referência. Nenhuma posição nativa foi pulada. O texto e Strong do CSV não foram substituídos. Fontes e revisão estão em `biblioteca/fontes/BHSA-4c`; auditoria em `docs/bhs-native-alignment.json`. Abas de leitura antigas da BHS são migradas pelo mapa quando disponível; marcações de tradução só reaparecem quando referência e texto ainda correspondem.

## Numeração dos textos

- MENS: reconhece números e intervalos explícitos como `1-2`. Entradas sem referência explícita ficam como `§` e não são vinculadas a um número inventado.
- Livros com contagem de capítulos divergente mostram aviso e não participam da sincronização automática. Foi encontrado esse caso em 2 Samuel da NTLH.
- Outras traduções usam a posição no array como número de versículo. Isso permite leitura, mas não comprova a versificação editorial de cada arquivo. Diferenças de omissão ou combinação não declaradas no JSON ainda precisam de revisão textual.
- A validação estrutural não autentica a edição nem a precisão textual da Bíblia.

### Ligações BHS/WLC ↔ ARA

A versão 0.7.0 combina o mapa numérico original com as notas explícitas `KJV:` do XML OSHB. Elas servem de ponte para as traduções locais com essa versificação; não constituem alinhamento palavra a palavra. O índice inverso exclui superscrições sem texto correspondente na tradução.

| BHS/WLC · numeração hebraica | ARA · corpo do texto |
|---|---|
| Joel 3:1–5 | Joel 2:28–32 |
| Joel 4:1–21 | Joel 3:1–21 |
| Cânticos 7:1 | Cânticos 6:13 |
| Cânticos 7:2–14 | Cânticos 7:1–13 |
| Salmos 3:2 | Salmos 3:1 |
| Salmos 51:3 | Salmos 51:1 |
| Salmos 13:6 | Salmos 13:5 e 13:6 |

As superscrições antigas ficam em itálico no hebraico, inclusive quando ocupam apenas o início de um versículo que também contém o corpo do salmo. Não são confundidas com títulos temáticos editoriais. Os textos de superscrição não estão nos JSONs portugueses atuais: nesses casos a tabela informa a ausência e a navegação mantém a posição portuguesa, sem associar o título ao v. 1 indevidamente. Salmos sem superscrição não recebem deslocamento. Correspondências em mais de um versículo são preservadas e mostradas na tabela; a navegação destaca os alvos do capítulo aberto e lista as referências na barra inferior. Marcas de parte de versículo (`!a`, `!b`) são preservadas no mapa. Auditoria: `docs/versification-map-audit.json`.

## Desenvolvimento e publicação

Código C# e WPF em `src/Verbum`, sem pacotes de interface de terceiros. SDK oficial instalado localmente em `.tools/dotnet` para este desenvolvimento. A implementação foi escrita para o VERBUM; não incorpora código do Logos.

Execute `build.ps1` no PowerShell para gerar o EXE. O script usa o SDK local ou um SDK .NET 10 disponível no PATH. A primeira publicação pode baixar os componentes de runtime pelo NuGet.

Testes independentes de pacotes: o aplicativo aceita `--self-test --library CAMINHO --report ARQUIVO.json`. O conjunto cobre normalização, destaques, importação das Bíblias reais, Strong, lema, variantes, limites de sincronização MT, encaixe de abas, filtros, parsing pessoal e vínculos manuais de tradução. Fixtures sintéticas são separadas dos corpora reais. Relatório em `docs/verbum-test-results.json`.

## Referências de implementação

- [Normalização Unicode](https://www.unicode.org/faq/normalization.html): equivalência canônica e formas NFC/NFD. A remoção de marcas é uma escolha de busca do VERBUM, não uma equivalência de significado.
- [Publicação em arquivo único do .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).

Versão atual: **0.17.1**. O inventário e a análise anterior do Logos estão em `docs/arquitetura-do-leitor.md`; eles são separados do código do aplicativo.



### Anotações dos livros (0.9.0)

A Biblioteca permite **Excluir da biblioteca…** e **Restaurar leituras excluídas**, preservando originais e notas. No leitor, selecione texto e use **▰ Marcar** (em PDF, no modo **Texto pesquisável**). **+ Nota desta seção** inclui o trecho selecionado em uma nota com editor e prévia de Markdown. As notas são salvas automaticamente também como `.md`; podem ser reabertas em **Minhas notas**. Detalhes e limites em `docs/biblioteca-0.9.0.md`.
