# Parsing no hover e ligação português ↔ hebraico (0.17.0)

## O que mudou

### 1. Parsing ao passar o mouse no hebraico

- O cartão mostra a **palavra gráfica inteira**, com uma linha por segmento: prefixos (conjunção, preposição, artigo), a palavra principal e os sufixos pronominais. Na BHS, que divide a palavra em segmentos, o segmento sob o mouse fica em negrito e os demais em cinza.
- Abaixo vêm lema, Strong, glossa e a expressão da tradução ligada àquela palavra (ARA, quando o alinhamento existe).
- Cobertura do parsing por ocorrência na BHS: de 363.289 para **417.961** dos 426.581 segmentos. Dos 8.620 restantes, 6.448 são artigos elididos sem texto (nada a exibir) e cerca de 2.170 são diferenças reais entre BHS e WLC (ketiv/qere e grafias), deixadas sem parsing.

Causas corrigidas em `MorphologyLinks.cs`:

| Causa | Segmentos | Correção |
|---|---|---|
| Holam escrito antes do waw (BHSA `אֹור`) ou sobre ele (WLC `אוֹר`) | 32.212 | Os pontos vocálicos são comparados como conjunto; a posição do holam é diferença de codificação |
| Uma palavra diferente no versículo (ketiv/qere) invalidava o versículo inteiro | 24.882 | As consoantes dos dois textos são alinhadas (maior subsequência comum); só a palavra divergente fica sem parsing |

A regra continua conservadora: mesmo versículo MT, mesmas consoantes na posição alinhada, limites completos dos segmentos OSHB e mesmos pontos vocálicos. Cantilação e meteg são ignorados.

### 2. Do português para o hebraico

Passe o mouse em uma palavra da ARA no Antigo Testamento: o cartão mostra o termo hebraico ligado, seus segmentos e parsing, lema, Strong, glossas e a referência MT. Clique na palavra (ou botão direito) e escolha **Analisar o termo original (hebraico)…** para abrir a análise completa no WLC-OSHB.

Cada ligação traz um grau: **provável**, **plausível** ou **incerta**. O hover do hebraico só mostra ligações incertas quando não há outra melhor.

## Como o Logos faz e o que foi aproveitado

Inspeção estática com o REA de `Libronix.DigitalLibrary.ResourceDrivers.dll` (SHA-256 `397ece93…c923`, evidência `ev_ee33cbf0…d4ba`) e inventário de nomes de `Libronix.DigitalLibrary.dll`. Os nomes de tipos e membros indicam:

- o interlinear reverso é um **recurso separado, associado a cada tradução** (`ReverseInterlinearResourceDriver`, `GetAssociatedReverseInterlinearResource`);
- cada palavra original tem um **número de palavra** estável; a tradução é ligada a ele por tabelas (`c_strSqlGetTranslationWordsForWordNumber`, `GetWordNumbersIntersectingTextRange`, `GetTextRangesForWordNumber`);
- lema e morfologia são consultados pelo número da palavra (`GetLemmaForWordNumber`), não pela palavra traduzida;
- os dados têm colunas de palavra original e palavra traduzida (`TextAlignmentOriginalWord`, `TextAlignmentTranslationWord`).

São inferências a partir de nomes em metadados; nenhum método foi descompilado e nenhuma execução foi observada. **Nenhum dado ou código do Logos foi usado.** O alinhamento ARA do Logos é um trabalho editorial proprietário; o do VERBUM é calculado localmente.

O VERBUM adota a mesma arquitetura: um arquivo de alinhamento por tradução (`biblioteca/alinhamento/ARA.json.gz`), com as palavras do WLC-OSHB identificadas por versículo MT e posição, e ligações (trecho da tradução → palavra e segmento).

## Como o alinhamento é calculado

`AlignmentBuilder.cs`, a partir do WLC-OSHB e do JSON da tradução:

1. Cada palavra hebraica vira unidades: prefixos, palavra principal (pelo Strong) e sufixos pronominais.
2. As palavras portuguesas recebem um radical simples, para reunir flexões (criou, criaram).
3. Um modelo estatístico (IBM Modelo 1) é treinado nos dois sentidos sobre os 23.146 versículos pareados pelo mapa de versificação, com preferência por posições relativas próximas.
4. Uma ligação exige concordância dos dois sentidos. As glossas portuguesas do arquivo pessoal de parsing reforçam a ligação quando coincidem.
5. Artigos e preposições sem ligação própria acompanham a palavra seguinte, quando o melhor candidato é um segmento dela.

6. Versículos hebraicos que caem na mesma entrada da tradução (intervalos da MENS, versículos unidos ou divididos) são alinhados juntos, como uma só passagem.

Resultado na ARA: 534.816 palavras; 457.204 ligadas (281.300 prováveis, 99.863 plausíveis, 76.041 incertas). Amostra legível em `biblioteca/alinhamento/ARA.amostra.txt`.

As 18 traduções portuguesas foram alinhadas. Ligações prováveis: 53–55% das palavras nas formais (ACF, ALM1911, ARC, KJF, BLIVRE, JFAA, TB, ARA), 47–51% em AS21, NAA e NVI, 33–42% nas dinâmicas (KJA, NVT, VFL, NBV, NTLH, OL) e 26% na MENS, que é paráfrase e cobre 18.914 dos 23.146 versículos hebraicos.

## Limites

- É um alinhamento **automático e não revisado**. Não houve medição de acerto contra um padrão revisado; a amostra foi conferida manualmente em 13 versículos. Erros típicos: verbos irregulares raros (`vou`, `vens`) ligados a outra palavra do versículo; palavras sem equivalente direto.
- Só o Antigo Testamento. O SBLGNT local não tem lema, Strong nem morfologia, então não há alinhamento grego.
- O arquivo vale para o JSON exato da tradução (tamanho conferido ao abrir). Se o texto mudar, gere de novo.
- Superscrições dos Salmos não existem nos JSONs portugueses e ficam sem ligação.

## Gerar para outra tradução

```powershell
dist\VERBUM.exe --build-alignment ARA,NAA --library "CAMINHO\biblioteca\biblias"
```

Leva cerca de dois minutos e meio por tradução e grava `biblioteca/alinhamento/<ID>.json.gz` e `<ID>.amostra.txt`. Em caso de erro, veja `biblioteca/alinhamento/erro.txt`. O hover do hebraico usa a ARA quando existe; senão, a primeira tradução alinhada.

Prévia dos cartões: `dist\VERBUM.exe --workspace-preview docs\previews\hover-017.png --tips-preview --light --library …`.
