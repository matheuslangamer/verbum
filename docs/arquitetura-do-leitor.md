# VERBUM — leitor bíblico para estudo e exegese

## Objetivo

Aplicativo Windows executável com leitura offline das Bíblias locais, área de estudo dividida em blocos redimensionáveis, abas dentro de cada bloco e restauração da sessão. Este documento registra a investigação inicial e a arquitetura proposta. A primeira versão funcional foi implementada em `src/Verbum` e publicada em `dist/VERBUM.exe`; o guia de uso e os limites atuais estão em `README.md`.

## O que foi observado na instalação do Logos

A inspeção foi estática, pelo REA 6.9.0, dos arquivos compilados em `C:\Users\mathe\AppData\Local\Logos`. Não foram consultados fontes da Microsoft ou da Faithlife.

- A varredura examinou 8.436 entradas e sondou 954 arquivos. Detectou 899 arquivos com cabeçalhos CLI declarados, compatíveis com assemblies .NET. Isso não comprova quais módulos são carregados em execução.
- `System/Logos.runtimeconfig.json` declara .NET 10.0 e os frameworks Microsoft.NETCore.App e Microsoft.WindowsDesktop.App, versão 10.0.12.
- Em `Libronix.DigitalLibrary.dll`, o REA encontrou `Libronix.DigitalLibrary.Layout` e `Libronix.DigitalLibrary.LayoutsManager`.
- O tipo Layout declara GUID, título, data, miniatura e XML. LayoutsManager declara operações como CreateNamedLayout, SaveAsLayout, RenameLayout e UpdateLayout.
- Literais de métodos desses tipos incluem `layouts.db` e consultas à tabela Layouts com campos Title, CreatedDate, Thumbnail e Xml. Isso sustenta que existem mecanismos compilados para persistir layouts. Não foi aberto o banco pessoal do usuário nem verificado o formato interno do XML.
- Foram encontrados TabControlUtility e utilitários de painéis em Libronix.Utility.Windows.dll. Esses nomes, isoladamente, não provam o mecanismo completo de encaixe e arraste da interface.

Evidência de membros selecionados: `ev_29e6ddd9992bf8f6827734dbca680dc8ab9366e9954ce694a69a2d51dd0772ae`.
SHA-256 do assembly: `c9ae8949a66ec8aaaf81b600f18a2757fc32c4377d74951f1ef2bc7bf2c2bfa3`.
MVID: `41d4f9ca-392c-4441-9465-86a7719b043a`.

A estrutura abaixo é proposta própria para o VERBUM. Não é uma afirmação de que o Logos tenha exatamente esse modelo interno.

## Modelo da área de estudo

Uma área de estudo contém uma árvore de divisões. Cada divisão organiza dois ou mais filhos na horizontal ou vertical, com proporções. Cada folha é um bloco com uma lista de abas e uma aba ativa.

Cada aba possui identificador próprio e tipo de conteúdo. Uma aba de Bíblia mantém versão, livro, capítulo, versículo ou intervalo, tamanho da fonte, posição de leitura e grupo de sincronização. Uma aba de notas mantém seu documento e sua referência bíblica, quando existir.

Mover uma aba deve mover seu estado, sem recriar ou perder a leitura. Remover o último item de um bloco deve simplificar a árvore. O layout deve ser validado ao restaurar, aceitando Bíblias que tenham sido removidas sem impedir a abertura do aplicativo.

Exemplo de uso:

```text
Área de estudo: Romanos 8
├── Bloco esquerdo: [ALM1911] [TB]
├── Bloco central: [BLIVRE] [Busca]
└── Bloco direito: [Notas] [Referências]
```

Blocos podem ser divididos, redimensionados e conter abas de tipos diferentes. Salvar a área registra também a aba ativa e a referência de cada leitor.

## Conteúdo bíblico

Foram encontrados 18 arquivos JSON e 18 variantes JS em `biblioteca/biblias`. Os JSONs têm uma lista de 66 livros. Cada livro contém abbrev, name e chapters; cada capítulo contém entradas de texto. O leitor deve interpretar os JSONs como dados, sem executar os arquivos JS.

O texto não precisa ficar incorporado ao EXE. O executável pode abrir os arquivos da biblioteca local. As notas, os índices de busca e os layouts são dados distintos dos textos bíblicos e devem ter armazenamento próprio.

O modelo de referência precisa usar identidade estável de livro, capítulo e versículo ou intervalo. Índices de arrays são coordenadas de armazenamento e não devem ser tratados automaticamente como uma referência bíblica válida.

Problemas identificados:

- NTLH.json: 1.203 capítulos no total; 2 Samuel contém 38 capítulos registrados, contra 24 em ALM1911.json. A causa ainda não foi identificada. O importador deve informar a divergência, sem renumerar o texto silenciosamente.
- MENS.json: 13.055 entradas; Gênesis 1 contém 12 entradas, e a primeira começa com o intervalo explícito `1-2`. Uma entrada não equivale necessariamente a um versículo.
- OL.json: 30.671 entradas; diferenças de contagem também exigem avaliação antes de alinhar traduções.
- As demais contagens de versículos variam. A validação realizada confirmou a estrutura básica e contou entradas, não autenticou edições, integridade textual, versificação ou licenças.

## Sincronização entre leitores

Abas podem pertencer a grupos A, B ou nenhum grupo. Ao navegar em uma aba do grupo A, o aplicativo publica uma referência bíblica semântica; as outras abas desse grupo resolvem essa referência em sua própria tradução. Abas sem grupo permanecem independentes.

A publicação deve carregar a origem para evitar ciclos. A tradução de destino pode não ter exatamente o mesmo versículo ou pode representá-lo como intervalo. Nesses casos, deve mostrar a correspondência disponível ou uma indicação de indisponibilidade, sem saltar para outro texto silenciosamente.

## Leitura, busca e exegese

A primeira versão deve cobrir biblioteca local, navegação por referência, capítulos legíveis, seleção e cópia de passagens, blocos com abas, comparação de traduções e notas locais. A busca deve indexar uma representação normalizada, preservando o texto original para exibição e citação.

JSONs com texto traduzido não fornecem automaticamente lemas, morfologia, números Strong, alinhamentos com grego/hebraico ou um interlinear. Essas capacidades exigem fontes adicionais e vínculos explícitos. O modelo deve permitir adicioná-las depois, sem inventar esses dados a partir de palavras portuguesas.

## Ordem de implementação

1. Criar o importador com relatório por versão, identificadores estáveis e tratamento de intervalos; começar com uma versão cujo mapeamento esteja validado.
2. Implementar um leitor com livro, capítulo e referência, preservando o texto original.
3. Implementar divisões redimensionáveis e abas independentes, incluindo mover abas entre blocos.
4. Persistir e restaurar layouts e estado de leitura de forma atômica, com recuperação da última sessão válida.
5. Adicionar grupos de sincronização e comparação, cobrindo versículos ausentes e intervalos.
6. Adicionar notas e busca local.
7. Gerar e testar o pacote executável Windows. Definir a tecnologia e os pré-requisitos de compilação antes dessa etapa.

## Critérios de aceite da primeira versão

- Abrir duas traduções em blocos diferentes e alternar abas sem perder a referência.
- Redimensionar blocos e mover uma aba entre eles.
- Salvar, fechar e restaurar o layout e as notas.
- Navegar por referência sem depender da posição visual ou do deslocamento de rolagem de outra tradução.
- Exibir um intervalo da MENS como intervalo, sem atribuir todos os seus textos a números consecutivos fictícios.
- Sinalizar a estrutura divergente de 2 Samuel na NTLH durante a importação.
- Funcionar offline com a biblioteca local selecionada.
