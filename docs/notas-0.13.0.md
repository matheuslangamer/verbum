# Notas — edição visual, lixeira e grafo (0.13.0)

## Projetos analisados

- [Editor.md](https://github.com/pandao/editor.md): componente web baseado em CodeMirror, jQuery e Marked, com edição de Markdown e prévia. O README informa que não oferece conversão de HTML para Markdown. Seus recursos orientaram a comparação de comandos e modos de edição.
- [Foam](https://github.com/foambubble/foam): sistema de conhecimento integrado ao VS Code, com grafo, referências recebidas e navegação por vínculos. Orientou a organização das conexões.

A implementação desta versão utiliza os controles nativos WPF do VERBUM e mantém Markdown como armazenamento. Não incorpora o código desses projetos nem instala VS Code ou um navegador embarcado. O grafo desta versão é uma visualização circular de vínculos existentes, sem motor físico ou equivalência a todas as funcionalidades do Foam/Obsidian.

## Edição

**Normal** usa RichTextBox para editar diretamente o texto formatado. Os comandos de negrito, itálico, título, lista, numeração, citação, vínculo e desfazer/refazer atuam sobre o documento visual. **Texto** retorna o parágrafo ao estilo comum. Novas notas iniciam no modo Normal; notas anteriores conservam o modo salvo, podendo alternar a qualquer momento para **Editar MD**. Prévia e código lado a lado continuam disponíveis.

O documento visual mantém uma cópia da sintaxe e uma assinatura estrutural de cada bloco. Sem edição, a conversão de ida e volta devolve o Markdown original exatamente. Ao editar, blocos não modificados preservam sua sintaxe, incluindo quebras de linha e cercas de código. Os blocos editados são serializados em Markdown. Texto colado no modo Normal entra como texto simples, para evitar introduzir formatação de outros programas que não possa ser representada no formato da nota.

Este editor ainda não é um processador de documentos completo: tabelas, imagens e extensões avançadas de Markdown podem exigir o modo MD. O modo visual cobre os formatos descritos acima; não oferece paginação ou diagramação de DOCX. Links internos usam Ctrl+clique no modo Normal e clique simples na Prévia. O modo MD conserva a seleção; a posição de edição visual não é convertida para offsets MD.

## Exclusão e restauração

Excluir é uma ação separada de fechar aba. A confirmação move o conteúdo mais recente e a identidade da nota para `notes/lixeira`, em JSON e Markdown. A nota sai da lista ativa e dos blocos abertos. O carregamento e o salvamento ignoram identidades na lixeira, impedindo que uma sessão/checkpoint antigo ressuscite a nota. **Notas → Lixeira → Restaurar** recupera a identidade original, fazendo os vínculos existentes voltarem a funcionar.

Não foi adicionada uma ação de destruição permanente. A exclusão de uma nota não apaga links escritos em outras notas. Ao abrir um link cujo destino esteja na lixeira, o aplicativo informa que a nota não está disponível.

## Grafo e referências recebidas

O grafo deriva arestas direcionadas de links Markdown `verbum://note/...`, desduplica as arestas e inclui somente notas ativas existentes. Ignora código em linha e blocos cercados. Links de Bíblia e livros continuam funcionando, mas não viram nós neste grafo. Não interpreta wikilinks `[[...]]` nesta versão.

Há filtro por título, inclusão de vizinhos diretos, zoom e navegação pelos nós. A visualização limita a 120 notas e informa quando é necessário refinar o filtro. As referências recebidas também aparecem no rodapé da nota como botões para as notas de origem.

## Proteção e validação

Backup anterior em `.tools/backups/before-visual-notes-20261113-224845`, contendo código, README, executável e dados pessoais gravados em disco. Testes de exclusão e restauração usam uma pasta temporária; as notas pessoais não são excluídas nos testes.

**246 testes aprovados, nenhuma falha.** As verificações cobrem conversão sem alterações, preservação de blocos não editados, texto literal, formatação visual, inserção de vínculo, alternância Normal/MD, lixeira, restauração, filtragem de sessão e grafo derivado de vínculos reais. O resultado está em `verbum-test-results.json`.

Prévias inspecionadas usam somente notas de demonstração:

- [Edição Normal — claro](previews/notas-013-light.png)
- [Edição Normal — escuro](previews/notas-013-dark.png)
- [Grafo de exemplo](previews/notas-013-graph.png)

A publicação usa dependências já restauradas. A indisponibilidade da auditoria NuGet online gera NU1900, sem impedir a compilação.
