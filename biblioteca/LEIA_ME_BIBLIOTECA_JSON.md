# Biblioteca SOMA (Σῶμα) — Onde Colocar Bíblias e Documentos (.JSON)

O SOMA separa sua biblioteca em dois diretórios dedicados para organização igual ao **Logos Bible Software**:

1. **`biblioteca/biblias/`** — Coloque aqui seus arquivos `.json` de **Bíblias e Traduções** (ex.: `acf.json`, `ara.json`, `nvi.json`, `naa.json`, `kjv.json`, etc.).
2. **`biblioteca/documentos/`** — Coloque aqui seus arquivos `.json` de **Documentos, Livros, Comentários Exegéticos, Confissões, Patrística e Notas** (ex.: `comentario_joao.json`, `teologia_sistematica.json`, etc.).

Você também pode importar quantos arquivos `.json` quiser diretamente pela interface do SOMA clicando na aba superior **"Biblioteca (Bíblias & Docs)"** nos botões:
- **`[+ Importar Bíblia(s) .JSON]`**
- **`[+ Importar Documento(s) / Livro(s) .JSON]`**

---

## Formatos `.JSON` de Bíblias Reconhecidos Automaticamente

O importador inteligente do SOMA reconhece automaticamente todos os principais padrões de Bíblias em JSON da internet/GitHub:

### Formato A — Padrão GitHub Brasileiro (`[{ abbrev, name, chapters: [[...]] }]`)
```json
[
  {
    "abbrev": "gn",
    "name": "Gênesis",
    "chapters": [
      [
        "No princípio criou Deus os céus e a terra.",
        "E a terra era sem forma e vazia..."
      ]
    ]
  }
]
```

### Formato B — Mapa Direto de Referências (`{ sigla, nome, versos: { "GN 1:1": "..." } }`)
```json
{
  "sigla": "ACF",
  "nome": "Almeida Corrigida Fiel",
  "idioma": "PT-BR",
  "versos": {
    "GN 1:1": "No princípio criou Deus os céus e a terra.",
    "JO 1:1": "No princípio era o Verbo, e o Verbo estava com Deus, e o Verbo era Deus."
  }
}
```

### Formato C — Lista de Versículos (`[{ book, chapter, verse, text }]`)
```json
[
  { "book": "GN", "chapter": 1, "verse": 1, "text": "No princípio criou Deus os céus e a terra." }
]
```

---

## Formatos `.JSON` de Documentos / Livros Reconhecidos Automaticamente

```json
{
  "id": "MEU-LIVRO-01",
  "titulo": "Comentário Exegético de Romanos",
  "autor": "Autor / Seminário",
  "categoria": "Comentário Exegético",
  "descricao": "Estudo versículo por versículo.",
  "capitulos": [
    {
      "titulo": "Capítulo 1 — Justificação pela Fé",
      "ref": "RM 3:23",
      "conteudo": "Texto completo do capítulo ou comentário..."
    }
  ]
}
```
*(Também aceita listas simples de artigos `[{ "titulo": "...", "conteudo": "...", "ref": "..." }]` ou mapas de comentário `{ "JO 1:1": "Comentário sobre Jo 1:1..." }`.)*

