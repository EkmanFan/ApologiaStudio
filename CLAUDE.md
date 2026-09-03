# ApologiaStudio — guide agent

Plateforme .NET/Blazor de formation de responsables chrétiens : apologétique,
leadership biblique, posture d'aide pastorale. Sert aussi de projet réel
d'apprentissage en ingénierie d'agents IA (RAG, MCP, évaluations).

Dépôt : https://github.com/EkmanFan/ApologiaStudio

## Architecture

Architecture en couches, **vérifiée par des tests** (`tests/ApologiaStudio.ArchitectureTests`) :

| Projet | Rôle | Règle imposée |
|---|---|---|
| `src/ApologiaStudio.Domain` | entités, invariants | ne dépend d'aucune couche externe |
| `src/ApologiaStudio.Application` | handlers, abstractions | ne dépend que vers l'intérieur |
| `src/ApologiaStudio.Infrastructure` | EF Core, PostgreSQL, dépôts | pas de dépendance vers AgentRuntime ni Web |
| `src/ApologiaStudio.AgentRuntime` | orchestration LLM | pas de dépendance vers Infrastructure ni Web |
| `src/ApologiaStudio.Web` | Blazor Server + endpoints | composition root |
| `src/ApologiaStudio.Mcp.KnowledgeServer` | serveur MCP | — |

`CompositionRootTests` vérifie aussi les **durées de vie DI** des services critiques
et que le conteneur se construit avec validation de scope. Une régression de
layering ou de lifetime casse la suite : ne pas contourner ces tests.

## Commandes

```bash
dotnet build                      # solution complète

eval "$(direnv export bash)"      # OBLIGATOIRE avant les tests d'intégration
dotnet test                       # suite complète

./scripts/run-apologia-dev.sh     # lance BDD + migrations + app sur :5090
./scripts/ef.sh migrations list --context ApologiaStudioDbContext
./scripts/commit-apologia.sh "message" [type]
```

L'app tourne **toujours sur le port 5090**, jamais sur un port de repli. Si le
port est occupé par une instance précédente, la tuer puis relancer.

### Pièges vérifiés

- **`dotnet test` sans direnv** : les 21 tests d'intégration échouent tous sur
  `APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured`. Ce n'est pas une panne,
  c'est `.envrc` non chargé. Rider charge direnv automatiquement, pas un shell nu.
- **`./scripts/ef.sh`** ne passe pas `--context` : sans lui, EF s'arrête sur
  « More than one DbContext was found » (il y a `ApologiaStudioDbContext` et
  `KnowledgeDbContext`).
- **Deux PostgreSQL** : `apologia_studio` sur 54329, `apologia_knowledge` (pgvector)
  sur 54330. Les mots de passe viennent de `.env.apologia.local` (jamais commité).
- **Base de test séparée** : `apologia_studio_test`, distincte de la base de dev.

## Convention de commit

**Messages en anglais**, et commit **via `scripts/commit-apologia.sh`** :

```bash
eval "$(direnv export bash)"                       # sinon les tests bloquent
./scripts/commit-apologia.sh "add dark palette" feat
```

Toute exception doit être motivée et validée par le propriétaire au préalable.

Le script n'est pas qu'un formateur de message : il enchaîne build, suite de
tests complète, refus des fichiers secrets stagés, contrôle d'espaces, commit
puis push, et **refuse de committer si les tests échouent**. Un `git commit`
direct perd ce filet.

Types acceptés : `feat, fix, refactor, chore, test, docs, perf`.

> Note historique : les commits `Feat : message` (français, capitale, espace
> avant le deux-points) présents dans le journal ont été faits à la main hors
> script. C'est une dérive, pas la règle — ne pas la reproduire.

## Documentation de décision

Les décisions structurantes vont dans `docs/adr/` :

- `0001-canonical-bible-corpus-model.md` — USFM canonique, `SIL.Machine`, VPL comme oracle
- `0002-knowledge-store-and-rag-architecture.md` — Knowledge Store pgvector, projections, citations

Autres : `docs/bible-corpus-provenance.md` (manifestes + hachages),
`docs/knowledge-ingestion/`, `docs/security/`, `docs/ux-*.md`.

## Style de code observé

Le dépôt n'a **pas** de `.editorconfig` : les conventions sont implicites et
tenues à la main. Les respecter en imitant le fichier voisin.

- un paramètre par ligne dès que la signature dépasse ~80 colonnes ;
- constructeurs privés + fabriques statiques `Create(...)` surchargées en
  cascade sur le domaine, invariants validés dans des `Set*` privés ;
- `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentNullException.ThrowIfNull` ;
- identifiants fortement typés (`UserId`) avec convertisseurs EF dédiés ;
- colonnes en `snake_case`, contraintes `CHECK` nommées `ck_<table>_<colonne>` ;
- commentaires rares, réservés au *pourquoi* non déductible du code ;
- textes d'IHM bilingues via un helper local `Text("fr", "en")`.

## Thème et apparence

Le thème est piloté par variables CSS sur `:root`, définies dans
`wwwroot/app.css` et surchargées à l'exécution par `wwwroot/app.js`
(`apologiaStudio.applyTheme`). Les `.razor.css` consomment ces variables —
ne jamais réintroduire une couleur en dur dans un composant.

Le mode sombre expose deux fonds configurables et **neutres** (niveaux de gris
imposés côté domaine) : fond d'écran et fond des zones de présentation, bornés
`#101010`–`#585858` par `UserPreferences.MinimumDarkShade`/`MaximumDarkShade`.
Les autres jetons (surfaces intermédiaires, bordures) en sont dérivés en JS.
La couleur d'accent reste libre et est ajustée pour tenir 4.5:1 sur les deux fonds.

Règle d'IHM : aperçu immédiat, `Save` pour conserver, croix pour annuler.

## Attentes de travail

Le propriétaire est un ancien développeur/architecte C# expérimenté. Ne pas
réexpliquer les fondamentaux du génie logiciel ; se concentrer sur le
spécifique IA et le relier à ce qu'il connaît déjà.

Principes qu'il applique et attend (issus de sa « Engineering Constitution ») :
simplicité par défaut, l'ingénierie avant les frameworks, les preuves avant la
popularité, le déterministe avant l'agent, un agent avant plusieurs, les
sorties de modèle sont non fiables, les prompts ne sont pas une frontière de
sécurité. Contester une solution faible, non sûre ou inutilement complexe est
attendu, pas mal vu.

Une fonctionnalité n'est pas finie parce que le chemin nominal marche :
validation, sécurité, tests, journalisation, documentation font partie du
« done ». Terminer par ce qui est établi, ce qui reste ouvert, et l'étape
suivante recommandée.
