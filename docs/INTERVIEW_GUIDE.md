# Guia de entrevista — BIM Change Radar

> Use este guia como roteiro, não como texto decorado. Adapte para suas palavras e seja honesto sobre o que você fez, o que usou de IA e o que ainda não sabe.

## 1. Pitch de ~2 minutos

**Problema.** "Em projetos BIM o modelo muda toda hora, e descobrir o que mudou entre duas versões costuma ser manual: abrir os dois modelos, comparar tabelas, conferir parâmetros. É lento, dá erro, e nem toda mudança tem o mesmo peso."

**Solução.** "Criei o BIM Change Radar, um app desktop em C# que compara dois snapshots de um modelo BIM e mostra o que foi adicionado, removido, modificado, e exatamente quais propriedades mudaram. Além disso, classifica cada alteração por impacto estimado, de baixo a crítico, e explica o motivo."

**Como funciona.** "Cada versão é um JSON com os elementos. O motor casa os elementos pelo identificador único, que é estável, e compara propriedade por propriedade. Depois um analisador aplica regras independentes: renomear é impacto baixo, trocar material é médio, mudar dimensão é alto, e remover ou alterar um elemento estrutural, como pilar, é crítico. Quando há dimensões, ele estima a variação de área, por exemplo +4,48 m² numa parede, sempre identificando como estimativa."

**Tecnologias.** "C# com .NET 8, interface em Avalonia com MVVM — uso Avalonia porque desenvolvi no Mac e WPF só roda no Windows, mas o modelo é o mesmo —, System.Text.Json, LINQ e testes com xUnit. Tem exportação CSV e HTML."

**Diferencial.** "Não é só um 'diff': o foco é priorizar revisão com regras transparentes. Cada regra é uma classe separada, então é fácil de explicar e evoluir."

**Futuro.** "Hoje usa snapshots JSON e não tem integração com Revit. A arquitetura separa o carregamento de dados do motor, então o próximo passo seria um adaptador com a Revit API que gere o mesmo snapshot. Também pretendo estudar IFC e explicações de mudança com IA."

## 2. Explicação técnica

- **Arquitetura:** três camadas. `Core` (modelos, comparação, regras, sem UI/IO), `Infrastructure` (JSON e exportação), `App` (Avalonia + MVVM). Dependência sempre em direção ao Core. Interfaces (`ISnapshotLoader`, `IReportExporter`...) permitem trocar implementações e testar.
- **MVVM:** View (XAML) só faz binding; ViewModel (`MainViewModel`) guarda estado e comandos (`CommunityToolkit.Mvvm`: `[ObservableProperty]`, `[RelayCommand]`); Model é o Core. Diálogos de arquivo ficam atrás de `IDialogService` para o ViewModel não depender da UI.
- **C#/OOP:** interfaces, composição (o `ImpactAnalyzer` recebe uma lista de regras), `record` para dados imutáveis, nullable reference types.
- **LINQ:** agrupamentos (por categoria), filtros da tabela, `Max` de impacto, união das chaves de parâmetros.
- **JSON:** `System.Text.Json` com opções case-insensitive e um `JsonConverter` customizado para `parameters` aceitar string/número/booleano. Validação com mensagens de erro claras.
- **Motor de comparação:** dois dicionários por `UniqueId` (O(n+m)). Está só na atual → Added; só na anterior → Removed; nas duas → `PropertyDiffer` compara cada propriedade; sem diferenças → Unchanged. Números comparados com tolerância (1e-6), texto com `Trim`, `null` e vazio equivalentes — isso evita falsos positivos.
- **Regras de impacto:** padrão estratégia — `IPropertyImpactRule` e `IElementImpactRule`, uma classe por regra, cada uma devolve nível + motivo. O elemento fica com o maior nível (abordagem conservadora).
- **Testes:** 25 testes xUnit: adicionados/removidos/modificados/inalterados, parâmetros, falsos positivos, área, impacto, JSON (inclusive inválido), CSV (inclusive injection) e HTML (escape).
- **Separação de responsabilidades:** comparar ≠ classificar impacto ≠ estimar quantidade ≠ exportar ≠ exibir. Cada um é uma classe/camada.

## 3. Perguntas e respostas

1. **Por que escolheu esse projeto?** Queria um problema real de BIM, não um CRUD. Comparar versões é uma dor concreta e permite mostrar lógica, arquitetura, testes e UX num escopo que cabia no prazo.
2. **Qual problema resolve?** Reduz a comparação manual entre versões do modelo e ajuda a priorizar o que revisar por impacto.
3. **Como funciona o algoritmo de comparação?** Indexo os elementos de cada versão por `UniqueId` em dicionários. Quem só existe na nova é adicionado, só na antiga é removido; quem existe nas duas passa por um diff propriedade a propriedade. Custo linear.
4. **Como identifica elementos modificados?** Comparo cada propriedade (nome, material, dimensões, nível...) e cada parâmetro. Se pelo menos uma diferir, é Modified e guardo nome, valor anterior e atual de cada uma.
5. **Por que C#?** É a linguagem da API do Revit (.NET), então o que aprendi aqui se aplica direto a plugins. Também é fortemente tipada, com bom suporte a LINQ, testes e UI desktop.
6. **O que é BIM?** Building Information Modeling: um modelo 3D de edificação em que cada elemento carrega informação (categoria, material, dimensões, parâmetros), usado para projeto, quantitativos e coordenação entre disciplinas.
7. **Como integraria com o Revit?** Faria um add-in com a Revit API que percorre os elementos (`FilteredElementCollector`), usa o `UniqueId` do Revit e exporta/gera um `ModelSnapshot`. O motor não muda. *(Honesto: ainda não implementei; conheço o conceito, não a API em profundidade.)*
8. **O que é uma API?** Uma interface que expõe funcionalidades de um software para outros programas usarem. A Revit API permite que um plugin leia e modifique o modelo.
9. **Como testou?** Testes unitários com xUnit para motor, regras, estimativa, JSON e exportação, mais um teste de integração que compara os dois JSONs de demonstração com contagens esperadas. Também executei o app e conferi a interface.
10. **Como evitaria falsos positivos?** Tolerância numérica para ponto flutuante, `Trim` em textos, `null` = vazio, comparação case-insensitive das chaves de parâmetro. Tem testes para isso. Um próximo passo seria ignorar parâmetros voláteis configuráveis.
11. **Como evoluiria o sistema?** Adaptador Revit, suporte a IFC, comparação visual 3D, regras configuráveis, análise de dependências e relatório PDF.
12. **Como usou IA no desenvolvimento?** Usei o Claude Code para acelerar a implementação, a partir de um escopo e requisitos que eu defini. Revisei a arquitetura, rodei e li os testes e consigo explicar cada parte. *(Ajuste para refletir exatamente o quanto você revisou e modificou.)*
13. **O que foi mais difícil?** Decidir regras de impacto razoáveis e explicáveis, e evitar falsos positivos. Também adaptar a UI: WPF não roda no Mac, então usei Avalonia.
14. **Qual parte você desenvolveu?** Seja específico e verdadeiro: ex. "Defini o escopo, as regras de impacto e os dados de demonstração, acompanhei e revisei o código e aprendi a explicá-lo". Não diga que escreveu algo que não escreveu.
15. **O que faria diferente?** Regras de impacto configuráveis desde o início, testes de UI automatizados, e validar as regras com um profissional de BIM em vez de depender só de heurísticas minhas.
16. **Por que Avalonia e não WPF?** Desenvolvi no macOS, onde WPF não roda. Avalonia tem o mesmo modelo (XAML, binding, MVVM), então a lógica é idêntica e a migração de Views seria simples.
17. **Por que separar em Core/Infrastructure/App?** Para o motor não depender de UI nem de arquivos: fica testável e permite trocar a fonte de dados (JSON → Revit) sem mexer nas regras.
18. **Os impactos são "verdade"?** Não: são estimativas por heurística, e a interface diz isso. Servem para priorizar revisão, não para substituir a avaliação de um engenheiro.

## 4. Demo sugerida (1 minuto)
1. Abrir o app → **Carregar projeto demo**.
2. Mostrar o dashboard (12/8/21/81) e a distribuição de impacto.
3. Aba Alterações → filtrar impacto `Crítico` → abrir **C-105** (coluna mais alta) e **W-104** (3 mudanças + estimativa +4,48 m²).
4. Exportar CSV e abrir.
5. Mostrar uma regra (`Rules/PropertyRules.cs`) e rodar `dotnet test`.

## 5. Cuidados
- Não afirme integração com Revit: **não existe**.
- Seja claro sobre quanto da implementação foi assistida por IA e sobre o que você entende em profundidade.
- Se não souber algo, diga "ainda não vi isso, mas pensaria assim…".
