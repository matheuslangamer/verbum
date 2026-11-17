# Biblioteca e apostila de Hebraico IV

Versão 0.9.0. Leitor WPF com PDFium nativo x64, texto extraído e leitura contínua de documentos. Importação local; sem envio de documentos para serviços externos.

## Implementação

- `DocumentLibrary`: catálogo, originais por SHA-256, índices de páginas/seções, DOCX via OOXML, EPUB pela ordem do spine, texto/HTML inertes e DOC/RTF via Word local com macros desabilitadas. XML sem resolução externa; formatos não suportados e EPUB com recursos criptografados são recusados.
- `PdfDocumentReader`: PDFium 153.0.7999.0, binário já fornecido pelo runtime de documentos do ambiente. Acesso nativo serializado, abertura e liberação determinística de documento/página/texto/bitmap. Renderização de páginas e extração de texto; sem execução de JavaScript ou ações de formulário.
- `DocumentLibraryView` e `BookReaderView`: importação, catálogo, navegação, zoom/fonte, texto selecionável e realce. Abas `library` e `book` são aceitas na sessão. PDFs abertos por uma busca entram no texto extraído; a página original fica disponível no seletor.
- Pesquisa: correspondência textual Unicode usando a mesma normalização básica do VERBUM. Consultas com várias palavras exigem todos os termos na mesma página/seção; o trecho e os parágrafos realçam os termos presentes mesmo se a consulta se estende por vários parágrafos.

Limites: 512 MB por arquivo importado; sem OCR; DOC/RTF dependem do Word; leitura DOCX/EPUB preserva figuras comuns e tabelas, mas não todas as notas ou a diagramação completa. Os resultados globais mostram até 200 páginas/seções e pedem refinamento quando houver mais. Sem correspondência automática entre palavras dos documentos, Strong e tradução portuguesa.

## Apostila

19 capítulos, cerca de 7.200 palavras. Conteúdo organizado em `biblioteca/apostila-hebraico-iv.md`; EPUB e catálogo gerados por `tools/build_hebraico_reader.py`. Fontes: as 10 aulas DOCX e os 2 PDFs especificados pelo usuário, mais as tabelas da conversa. O arquivo extra preparação aula 8 foi apenas localizado e não entrou no acervo nem na apostila. Os originais são preservados. A síntese distingue observação morfológica, hipótese interpretativa e proposta de tradução, com 16 exercícios e respostas comentadas.

Mapa de títulos: `aula 09.docx` tem título interno 10 e trata de Daniel 8; `aula 10.docx` trata de coordenação e Jonas 2. A ausência da aula 05 foi explicitada. A apostila é um material de revisão pessoal, sem se apresentar como gabarito do professor.

## Verificação

186 testes passaram no executável nativo. Incluem renderização de uma página real de ESTUDO PROVA 1, extração de suas 52 páginas, leitura do EPUB com 19 capítulos na ordem correta, tabelas hebraicas no EPUB e no DOCX real, importação sem duplicação, consultas sem sinais e grafia exata, destaque parcial de trechos, restauração da posição de leitura e validação das novas abas.

Verificação visual: catálogo com 13 títulos; apostila em leitura contínua; tabela de pronomes; PDF original renderizado; pesquisa `identificacao` sem acento encontrando 12 páginas/seções; clique no resultado do PDF abrindo o texto e destacando `identificação` em negrito e realce. O EPUB final passou por revisão editorial de fontes e numeração após a prévia.

Licenças do mecanismo PDF em `native/licenses`. Origem do binário: `pypdfium2_raw` do runtime; avisos do fornecedor e terceiros da mesma versão em https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium/7999 e licença upstream em https://pdfium.googlesource.com/pdfium/+/refs/heads/chromium/7999/LICENSE.

As imagens das aulas foram conferidas visualmente: além do logotipo, há três quadros morfológicos nas aulas 04 e 07. Esses três quadros também foram incorporados como figuras no capítulo de fontes da apostila. O leitor permite ampliar cada figura em janela própria. Imagens não entram na busca sem OCR.

Também foi verificada na interface a figura de Joel 3 no capítulo 19, sua abertura em janela de ampliação e o retorno à leitura. A versão final 0.9.0 ficou aberta e a área foi salva.

## Complemento do leitor — exclusão, marcações e notas

- No catálogo, **Excluir da biblioteca…** retira o título e seus resultados de pesquisa após confirmação. O índice vai para `biblioteca/livros/removidos`; **Restaurar leituras excluídas** o recupera. Originais, figuras, notas e marcações são preservados. Uma aba já aberta pode continuar mostrando a leitura até ser fechada.
- Selecione texto e use **▰ Marcar** ou o botão direito para aplicar cinco cores ou remover a cor somente da seleção. Funciona em parágrafos e células de tabelas. Em PDF, ative **Texto pesquisável**; a imagem da página original não recebe anotações. Cores ficam em `%LOCALAPPDATA%/Verbum/document-markers.json`, com backup, separadas por documento, seção e texto exato.
- **+ Nota desta seção** cria uma nota com o título da leitura, a seção e o trecho selecionado. Reabra pelo menu **Minhas notas**. O editor das notas aceita Markdown, com botões para negrito, itálico e título, e alternância entre **Editar MD** e **Prévia**. A prévia suporta títulos, negrito, itálico, citações, listas simples e código em linha; não é um renderizador completo de CommonMark.
- O salvamento automático conserva o JSON anterior e também grava uma cópia `.md` de cada nota aberta em `%LOCALAPPDATA%/Verbum/notes`. O JSON continua sendo a fonte para reabrir as notas; alterações externas no `.md` não são importadas automaticamente. Notas antigas permanecem editáveis sem migração.

Verificação adicional: **191 testes passaram**, incluindo exclusão/restauração sem perda do original, persistência e remoção parcial das cores, isolamento por documento/seção e prévia de título, negrito e itálico. EXEs da raiz e de `dist` conferidos por SHA-256. O executável anterior foi preservado como `VERBUM-0.9.0-anterior.exe` porque estava em uso durante a atualização. Não houve alteração dos materiais, da apostila ou da sessão pessoal. Não foi realizada nova revisão visual da interface nesta etapa.

### Glossas em inglês no parsing

O tooltip e o painel da palavra agora mantêm a glossa portuguesa quando disponível e, na ausência dela, mostram o inglês do arquivo pessoal. Sem glossa pessoal em inglês, consultam o Strong incorporado e depois o BDB pelos códigos Strong relacionados. A interface identifica idioma e fonte; sentidos lexicais não são apresentados como tradução confirmada da ocorrência. A consulta preserva as ambiguidades e não usa aproximação de letras para escolher um verbete. Definições longas aparecem abreviadas no tooltip; os verbetes completos continuam nos léxicos. Testes adicionais cobrem preferência pelo português, inglês pessoal, Strong estendido, BDB e ausência de códigos compatíveis.

Verificação desta correção: 196 testes passaram, sem falhas. Executáveis da raiz e de dist conferidos por SHA-256. Executável anterior preservado em VERBUM-antes-glossas-ingles.exe; notas, sessão e materiais não foram alterados.

### Correção do tooltip inicial (Joel 1:1 e 1:3)

A definição Strong agora integra o conteúdo inicial do tooltip, antes do evento de abertura e sem depender de carregar o parsing pessoal ou os léxicos adicionais. O enriquecimento posterior mantém a preferência pelas glossas pessoais; a consulta ao BDB ocorre somente quando necessária. Se o carregamento complementar falhar, o tooltip preserva a definição Strong já disponível. Os exemplos das imagens foram acrescentados aos testes com os dados reais: H5608 (סַפֵּרוּ, Joel 1:3) e H3100 (יוֹאֵל, Joel 1:1). A ausência de parsing para uma forma não impede mostrar sua definição lexical; não se inventa uma análise morfológica.

Verificação: 199 testes passaram. Incluem a construção do tooltip WPF real em thread STA, comprovando que os dois exemplos já contêm a definição antes do evento Opened. Publicação e cópia para o EXE principal concluídas.
