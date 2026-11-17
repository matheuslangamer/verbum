# Guia Exegético — VERBUM 0.10.0

O botão **Guia exegético**, na lateral, abre uma aba comum no bloco ativo. As divisões e abas existentes são mantidas. Quando a aba ativa é uma Bíblia, a passagem é convertida ao original pelo mapa de referências existente; se não há correspondência explícita, o programa informa a ausência. Sem uma leitura bíblica ativa, o exemplo inicial é **Joel 1:1–3**.

## Uso

1. Selecione **WLC-OSHB** para hebraico/aramaico ou **SBLGNT** para grego.
2. Digite uma referência ou intervalo no mesmo capítulo: `Joel 1:1-3`, `Joel 4` ou `João 1:1-5`. Pressione Enter ou **Gerar guia**.
3. Escolha a tradução portuguesa para o paralelo. O original conserva sua numeração; WLC usa MT. Joel MT 4 continua sendo Joel 4 no guia.
4. Abra as seções de texto, palavras, consulta lexical, sintaxe, variantes e fontes. Capítulos longos são paginados em dez versículos; os botões de próxima/anterior percorrem o intervalo solicitado.
5. Para filtrar lemas frequentes, desmarque **Todas as frequências**, ajuste o limite e use **Aplicar filtros**. O padrão é 30. O filtro de classe gramatical é independente. A frequência conta palavras anotadas por lema na edição; quando o texto nativo não fornece lema, uma correspondência MACULA única permite usar a contagem MACULA, explicitamente identificada. Palavras sem contagem comparável ficam fora do filtro de frequência.
6. Clique numa palavra para consultar os verbetes relacionados. Strong, UBS-PT e BDB continuam separados, com os identificadores e sentidos próprios. No grego pode haver glossa MACULA sem dicionário grego instalado. O estudo de ocorrências já existente é oferecido quando os dados nativos suportam a consulta.
7. **Notas da passagem** cria ou reabre a mesma nota para edição original, livro, capítulo e intervalo. As notas usam o editor Markdown existente e o salvamento JSON/MD em `%LOCALAPPDATA%/Verbum/notes`. Fechar sua aba não apaga a nota; reabrir o mesmo intervalo pelo guia recupera a nota salva. Intervalos distintos mantêm notas distintas.
8. **Copiar resumo MD** copia texto, paralelo, palavras exibidas, comentários textuais e fontes da página atual. A árvore sintática visual não é incluída nesse resumo.

## Fontes incorporadas

| Fonte | Revisão fixada | Índice importado |
|---|---|---|
| MACULA Hebrew, WLC | `47db250bd55d0d8577f2a94fba114ef16c35b23c` | 929 capítulos, 475.911 palavras/segmentos e 340.101 grupos sintáticos |
| MACULA Greek, SBLGNT | `8423afe47b9e8f24b7772e808af45c7159a6fe7e` | 260 capítulos, 137.741 palavras e 107.706 grupos sintáticos |
| UBS HOTTP | `a15a96cfaa4ca18a6c0339016645ba1e534db3c8` | 4.515 comentários de crítica textual em inglês |

Origens: https://github.com/Clear-Bible/macula-hebrew ; https://github.com/Clear-Bible/macula-greek ; https://github.com/ubsicap/ubs-open-license/tree/main/HOTTP .

MACULA Hebrew Linguistic Datasets e MACULA Greek Linguistic Datasets © 2022–2024 Biblica, Inc.; as camadas selecionadas estão sob CC BY 4.0, com atribuições dos componentes. Sintaxe hebraica Westminster/Groves Center, morfologia Open Scriptures e glossas Cherith conservam seus créditos. O grego usa a árvore MACULA sobre SBLGNT e o atributo `english` das glossas Cherith. Não foram importados os campos semânticos MARBLE/SDBH identificados como usados sob permissão (`sdbh`, `ln`, domínios e equivalentes); os dicionários UBS já existentes permanecem recursos distintos.

UBS HOTTP © United Bible Societies 1973–1980, 2003–2023: CC BY-SA 4.0. A extração para o índice mantém o inglês, referências por versículo, propostas, alternativas, avaliações A–D, fatores e exemplos de versões; remove apenas marcação HTML para exibição inerte. A posição de palavra nos identificadores UBS não é usada como alinhamento ao WLC. O XML original foi preservado. Os critérios A–D e os fatores originais abrem em uma janela de texto local, sem executar HTML.

Licenças em `biblioteca/fontes/MACULA-Hebrew`, `biblioteca/fontes/MACULA-Greek` e `biblioteca/fontes/UBS-HOTTP`. Revisões, hashes dos arquivos de origem e contagens estão em `biblioteca/exegese/manifest.json`.

## Regras e limites

- O texto das Bíblias não é substituído. As anotações MACULA são uma camada separada, lida por capítulo em JSON comprimido. Os índices somam aproximadamente 20 MB, mais o XML HOTTP original de aproximadamente 8,6 MB e licenças. Os arquivos ZIP usados na importação são temporários e não são necessários para executar o aplicativo.
- Uma anotação é atribuída à palavra apenas quando a sequência consonantal completa do versículo corresponde e a forma vocalizada correspondente é igual, desconsiderando cantilação e pontuação. Prefixos/sufixos podem ser agrupados, preservando os identificadores MACULA. Formas incompatíveis não são alinhadas por aproximação ou somente por Strong.
- A contagem de cobertura aparece na seção de palavras. Ter um índice para o capítulo não significa correspondência exata de todas as palavras em todas as edições/revisões.
- As glossas portuguesas pessoais continuam identificadas como automáticas/provisórias. Sem português, o guia prefere a glossa inglesa MACULA da ocorrência; sem correspondência, recorre ao arquivo pessoal e aos léxicos existentes. Essa informação não é uma tradução alinhada à ARA/TB.
- A morfologia nativa é preservada e tem preferência. No grego, a interface apresenta as características MACULA em português e mantém o código original. Os códigos e lemas gregos não são gravados no JSON SBLGNT nem atribuídos à fonte nativa.
- A sintaxe é uma análise editorial MACULA, não consenso obrigatório nem interpretação gerada pelo VERBUM. Classes/alguns papéis recebem rótulos portugueses; regras e códigos não mapeados são preservados. Grupos que atravessam versículos são exibidos somente na parte presente e só quando todos os membros dessa parte têm correspondência admitida.
- HOTTP é comentário de crítica textual; não é o aparato completo BHS/NA28. As avaliações pertencem à comissão. O SBLGNT continua mostrando suas variantes entre edições, e WLC suas notas/qere. Ausência de uma nota nos dados carregados não prova ausência de variantes.
- Não há classificação automática de palavras/passagens importantes, busca automática em gramáticas, alinhamento português novo nem tradução automática dos relatórios ingleses.
- Toda a consulta funciona offline depois da instalação dos dados. A sessão guarda referência, edição, paralelo, filtros, página, seções abertas e rolagem. O esquema anterior de sessão continua aceito.

## Reprodução e verificação

Execute `tools/import-exegesis.ps1` para obter as revisões públicas fixadas e gerar os índices. O importador .NET usa apenas bibliotecas padrão e lê XML com DTD proibido e resolução externa desativada. Os ZIPs são lidos sem executar conteúdo e sem extração de caminhos arbitrários. O arquivo de origem de cada capítulo determina o índice; referências desconhecidas, IDs repetidos e capítulos duplicados no mesmo corpus interrompem a importação.

`build.ps1 -Test` compila, publica e executa o conjunto de testes. Esta versão passou **219 testes**, incluindo intervalos MT, erros de referência, divisão de prefixos/sufixos, rejeição de formas incompatíveis, glossas reais de Joel, parsing grego de João, comentários HOTTP e validação estrutural dos 1.189 capítulos. Testes anteriores de Biblioteca, notas, apostila, pesquisas e sincronização permanecem no conjunto.

O modo `--guide-preview ARQUIVO.png --reference "Joel 1:1-3" --library CAMINHO` renderiza a própria interface WPF e salva o resumo MD sem abrir/salvar a sessão pessoal. `--edition SBLGNT` seleciona grego; `--section syntax` ou `--section variants` focaliza essas seções. O modo também exercita a regeneração do guia. Prévia não equivale a teste manual de todos os cliques.

Foram renderizadas e inspecionadas as prévias [palavras em Joel](previews/guia-joel.png), [estrutura sintática](previews/guia-sintaxe.png), [grego de João](previews/guia-grego.png) e [comentário HOTTP](previews/guia-hottp.png). Os quatro processos concluíram sem erro, inclusive após regenerar a mesma aba. Joel 1:1–3 apresentou 29/29 palavras com correspondência MACULA; João 1:1–3 apresentou 24/36, com as divergências mantidas sem alinhamento forçado. A publicação final é 0.10.0; o SHA-256 dos executáveis da raiz e de `dist` foi conferido e coincide. Os dois arquivos ZIP temporários baixados para a importação foram removidos; podem ser obtidos novamente pelo script de reprodução.

Ajuste de tipografia: ocorrências da pesquisa e hebraico do guia compartilham a mesma família de fontes do leitor (SBL Hebrew, Ezra SIL, Times New Roman). O padrão também foi aplicado a lemas e grupos sintáticos do guia, preservando os tamanhos existentes. Compilação e prévia WPF de Joel 1:3 conferidas; executáveis da raiz e dist com SHA-256 idênticos.
