# Continuidade do VERBUM — 14/11/2026

## Comece aqui

Workspace: `C:\Users\mathe\OneDrive\2026\SISTEMA\VERBUM`.
Aplicativo local Windows, C# WPF, .NET 10. Versão atual verificada: **0.17.0** (269 testes na 0.17.0). Executáveis principal `VERBUM.exe` e `dist/VERBUM.exe` foram comparados por SHA-256 após a publicação. Último relatório: **262 testes aprovados, zero falhas**, em `docs/verbum-test-results.json`.

Leia README.md e a documentação correspondente à tarefa antes de editar. Este arquivo é uma passagem de contexto, não uma autorização para executar todas as sugestões abaixo.

## Prioridades e preferências do usuário

- Preservar notas pessoais, biblioteca, materiais de Hebraico IV, marcações, referências e divisões/abas do layout.
- Interface compacta: várias áreas de estudo simultâneas. Não esconder controles frequentes para ganhar espaço.
- Pesquisa ao lado do nome VERBUM. Edição, livro, capítulo e sincronização sempre visíveis no leitor; seis botões de ações diretas. Os grupos mudam de linha conforme a largura.
- Fontes hebraicas iguais às do leitor: SBL Hebrew, Ezra SIL, Times New Roman.
- Notas devem continuar leves, armazenadas em Markdown. O usuário quer amadurecer o editor, com edição visual e MD, vínculos, grafo e gestão das notas.
- Fazer cópias de segurança antes de mudanças amplas, sem acumular indiscriminadamente executáveis e bibliotecas duplicados.
- Continuar o trabalho autorizado sem pedir confirmações repetidas para decisões rotineiras.

## Implementado

- Biblioteca de Bíblias, livros, PDFs, EPUB e DOCX; apostila de Hebraico IV e pesquisa offline.
- Parsing e glossas com fallback inglês quando português indisponível. Dados abertos mantidos separados do executável.
- Guia exegético com MACULA hebraico/grego e comentários HOTTP, fontes e limites explícitos.
- Confirmação ao sair: Sim salva posição, Não retém checkpoint anterior preservando notas, Cancelar continua aberto.
- Destaque de fundo e cor de letra independentes, inclusive hexadecimal personalizado.
- Temas claro/escuro, preferência em appearance.txt, ícones Lucide locais e licenciados.
- Botão Notas na lateral; catálogo pesquisável e reabertura sem duplicar identidades.
- Vínculos Markdown internos para Bíblia (edição e referência), outras notas/comentários e página/seção de livros; links http/https abrem somente ao clicar.
- 0.13.0: modo **Normal**, editável e formatado em RichTextBox; **Editar MD**, Prévia e Lado a lado. Conteúdo continua em Markdown. Blocos não editados preservam a sintaxe original.
- Exclusão separada de fechar aba: confirmação, lixeira e restauração preservando identidade e vínculos. Sessões/checkpoints antigos não devem ressuscitar notas na lixeira.
- 0.14.0: notas ancoradas à ocorrência de uma palavra ou expressão, tag Markdown com hiperlink, marcador clicável e botão de visibilidade por aba. O grafo usa atração e repulsão entre nós e inclui hashtags compartilhadas. A lixeira permite exclusão definitiva com confirmação. Guarda apenas um identificador vazio (.deleted) para bloquear a recuperação acidental por checkpoints antigos.

- 0.17.0: cartão de hover por segmentos (`WordTip.cs`), ligação BHS↔OSHB tolerante a holam-waw e ketiv/qere (`MorphologyLinks.cs`), alinhamento automático tradução↔hebraico (`Alignment.cs`, `AlignmentBuilder.cs`, `biblioteca/alinhamento/ARA.json.gz`). Método, números e limites em `docs/alinhamento-0.17.0.md`. As 18 traduções portuguesas foram alinhadas; não há alinhamento grego. Versículos hebraicos que compartilham uma entrada da tradução (intervalos da MENS, versículos unidos ou divididos) são alinhados como uma só passagem. A MENS, por ser paráfrase, liga cerca de metade das palavras.
- 0.17.1: `MainWindow.RenderWorkspace` mantém as telas cuja aba não mudou (`Reuse`, `Fingerprint`, `KeptView`); antes recriava tudo a cada clique. Quem alterar o estado de uma aba por fora e chamar `RenderWorkspace` só verá a tela refeita se o campo alterado estiver em `Fingerprint`. Notas, catálogo, biblioteca e análise continuam sendo recriados. Cartões de hover sob demanda; `--perf-report` mede as cargas. A carga da BHS (≈4 s) e o alinhamento por tradução (≈2,7 s, ≈140 MB) seguem sendo os pontos lentos.

## Arquivos importantes

- `src/Verbum/MainWindow.cs`: composição, layout, navegação, salvamento, integração de notas.
- `ReaderView.cs`, `ReaderToolbarLayout.cs`, `ReaderToolbarTests.cs`: leitor e barra adaptável.
- `Theme.cs`, `App.xaml`, `Icons.cs`: paleta, estilos e vetores.
- `MarkdownNoteView.cs`, `VisualMarkdown.cs`: editor MD/visual e conversão que preserva blocos não editados.
- `NoteLinks.cs`, `NoteLinkDialog.cs`: destinos e criação dos vínculos.
- `NotesCatalogView.cs`, `NoteStore.cs`, `NoteGraphView.cs`: lista, lixeira, grafo.
- `Models.cs`, `SessionExit.cs`: estado de abas, validação de sessão e checkpoint.
- `SelfTests.cs`, `NoteTests.cs`, `VisualNoteTests.cs`, `ThemeTests.cs`: verificações.
- `WorkspacePreview.cs`: prévias com dados sintéticos e guarda contra salvamento pessoal.

Documentação: `docs/biblioteca-0.9.0.md`, `docs/barra-leitura-0.10.2.md`, `docs/cores-da-letra-0.10.3.md`, `docs/layout-0.11.0.md`, `docs/notas-0.12.0.md`, **`docs/notas-0.13.0.md`**.

## Limites atuais a respeitar

- O editor visual cobre formatos principais; não é um equivalente completo a Obsidian ou Word. Tabelas, imagens e sintaxes avançadas podem exigir MD. Colagem no modo visual entra como texto simples.
- O modo MD conserva a seleção. A posição visual não é convertida para offsets MD.
- Grafo: vínculos entre notas e hashtags do Markdown, até 120 nós por filtro. As âncoras de palavras usam verbum://word/ID; WordAnnotation.cs valida edição, entrada, texto e posição.
- Editor.md e Foam foram analisados como referências; não foram incorporados. Editor.md é um editor web de fonte MD com prévia; Foam se integra ao VS Code. As escolhas e links estão na documentação 0.13.0.
- REA foi usado para análise estática de componentes do Logos. Evidências e limitações em `docs/logos-barra-leitura-rea-evidencias.json` e `docs/barra-leitura-0.10.2.md`. Não importar código ou dados proprietários do Logos.

## Dados pessoais e backups

Dados reais: `C:\Users\mathe\AppData\Local\Verbum`. Contém sessão, notes/*.json e *.md, notes/lixeira, marcações e appearance.txt. Não usar esses arquivos como fixtures de testes. O ambiente de execução pode redirecionar LOCALAPPDATA; conferir o caminho real antes de copiar.

Backups recentes:
- `.tools/backups/layout-before-redesign-20261113-214152`: inclui biblioteca completa e dados pessoais anteriores ao redesenho.
- `.tools/backups/before-notes-editor-20261113-222841`.
- `.tools/backups/before-visual-notes-20261113-224845`: código, executável, README e dados pessoais anteriores à 0.13.0.

## Compilar e conferir

SDK local: `.tools/dotnet/dotnet.exe`. `build.ps1 -Test` publica, testa e copia para a raiz. Em sessões anteriores, restore esbarrou em NuGet.Config do perfil e rede indisponível. A publicação com dependências já restauradas funcionou com:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tools/dotnet-home'
& ./.tools/dotnet/dotnet.exe publish src/Verbum/Verbum.csproj --no-restore -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o dist --nologo
```

Testes: iniciar `dist/VERBUM.exe --self-test --library "CAMINHO_ABSOLUTO/biblioteca/biblias" --report "CAMINHO_ABSOLUTO/docs/verbum-test-results.json"` por Start-Process -WindowStyle Hidden -Wait -PassThru. Conferir ExitCode e relatório. Verificações WPF executam no dispatcher da aplicação.

Prévias: `--workspace-preview CAMINHO_PNG --width 1440 --panes 3 --light` ou `--dark`; acrescentar `--notes-preview` para notas ou `--notes-graph-preview` para grafo. Usar --library explícito. Prévias não salvam dados pessoais. Conferir PNGs com view_image.

O executável principal pode estar aberto e bloquear cópia. Não encerrar a sessão do usuário à força. Em publicações anteriores foi possível renomear o EXE carregado para um backup em `.tools/backups` e copiar o novo para a raiz, verificando versão e hashes. Não criar muitos executáveis antigos visíveis na raiz.

## Último assunto: tamanho em disco

Usuário perguntou por que a pasta parecia ter 12 GB. Medição feita neste chat encontrou cerca de 6,2 GiB lógicos, dominados por cópias e desenvolvimento:
- `.tools/backups`: ~3,12 GiB;
- SDK, caches e ZIP do SDK: ~1,34 GiB;
- biblioteca: ~714 MiB;
- executável atual: ~134 MiB;
- pacote essencial (EXE + native + biblioteca): ~854 MiB.

Nada foi apagado. Foi sugerido separar uso, desenvolvimento e backups, mantendo poucas versões. **Essa limpeza ainda não foi autorizada nem executada.** Medir novamente antes de qualquer ação e preservar materiais e cópias de recuperação.

## Retomada

Na versão 0.16.0, o editor alterna MD <> e Normal, preserva a seleção nas paletas e oferece seis níveis de título, parágrafo, listas, link e citação colorida. NoteInfoView.cs implementa o painel + com datas opcionais, passagens e etiquetas. O REA verificou app.css no pacote do Obsidian 1.15.1; o estilo de citação usa borda lateral de 2px, recuo e fundo transparente. Na versão 0.15.0, NoteListView.cs atende as páginas Notas e Lixeira, com navegação compartilhada. NoteColors.cs e o editor implementam marcador e cor da letra em Markdown. A identidade visual da nota é salva em NoteColor. O REA identificou o pacote local obsidian-1.15.1.asar (SHA-256 42bf7f34782752f159d63c17ac6bac4ad01dd55709578f9879f76f8295abb2eb). A inspeção estática complementar de app.css confirmou .cm-highlight, --text-highlight-bg, .markdown-source-view, .markdown-reading-view e --graph-node-tag. A entrega 0.14.1 redesenha a lixeira com busca própria, prévia legível e ações compactas. Prévia em docs/previews/lixeira-0141.png. A entrega 0.14.0 ampliou o arquivo de notas. A prévia sintética do grafo está em docs/previews/notas-014-grafo.png.
