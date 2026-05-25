# Architectuurdiagrammen — OpenMRS Communicatiemodule

Alle diagrammen zijn geschreven in [Mermaid](https://mermaid.js.org/) en renderen direct op GitHub.
Naast de markdown-bron zijn er ook **PNG-exports** beschikbaar in [images/](images/) — handig voor PDF/slide-export, offline gebruik of inclusie in andere documenten.

## C4-model

Het C4-model beschrijft de architectuur op vier zoom-niveaus.

| Diagram | Beschrijving | PNG |
|---|---|---|
| [Level 1 — Context](01-context.md) | Het systeem in zijn bredere omgeving (zorgmedewerker, patiënt, OpenMRS, messaging providers) | [01-context.png](images/01-context.png) |
| [Level 2 — Containers](02-containers.md) | De technische bouwstenen: frontend, backend API, database, message bus | [02-containers.png](images/02-containers.png) |
| [Level 3 — Componenten](03-components.md) | Interne structuur van de backend per architectuurlaag | [03-components.png](images/03-components.png) |
| [Procesdiagram — Herinneringen](04-reminder-process.md) | Sequence van webhook → consumer → patiënt, inclusief handmatige trigger en data-retentie | [1](images/04-reminder-process-1.png) · [2](images/04-reminder-process-2.png) · [3](images/04-reminder-process-3.png) |
| [Klassediagram](05-class-diagram.md) | UML class diagrams van Domain en Application layer | [1](images/05-class-diagram-1.png) · [2](images/05-class-diagram-2.png) · [3](images/05-class-diagram-3.png) · [4](images/05-class-diagram-4.png) |

## Aanvullende diagrammen

| Diagram | Beschrijving | PNG |
|---|---|---|
| [Use case](06-use-case.md) | Actors en use cases voor zorgmedewerker, patiënt, ontwikkelaar en externe systemen | [06-use-case.png](images/06-use-case.png) |
| [User flow](07-user-flow.md) | Globale navigatie + 4 detail-flows: bericht versturen, patiënt zoeken, geschiedenis, login | [globaal](images/07-user-flow-1.png) · [bericht](images/07-user-flow-2.png) · [patient](images/07-user-flow-3.png) · [historie](images/07-user-flow-4.png) · [login](images/07-user-flow-5.png) |
| [ER-diagram](08-er-diagram.md) | PostgreSQL-schema: entiteiten, kolommen, encryptie, relaties, indices | [08-er-diagram.png](images/08-er-diagram.png) |
| [Deployment](09-deployment.md) | Docker Compose layout (dev) en productie target-architectuur met security-eisen | [dev](images/09-deployment-1.png) · [prod](images/09-deployment-2.png) |

## PNG-export regenereren

De PNG's worden lokaal gegenereerd uit de mermaid-blocks in de markdown-bron.

```bash
# 1. Install mermaid-cli (eenmalig)
npm install -g @mermaid-js/mermaid-cli

# 2. Extract mermaid blocks naar .mmd files
cd docs/c4
python3 -c "
import re
from pathlib import Path
for md in sorted(Path('.').glob('0*.md')):
    blocks = re.findall(r'\`\`\`mermaid\n(.*?)\n\`\`\`', md.read_text(), re.DOTALL)
    for i, b in enumerate(blocks, 1):
        suffix = '' if len(blocks) == 1 else f'-{i}'
        Path('diagrams', f'{md.stem}{suffix}.mmd').write_text(b)
"

# 3. Render naar PNG
for f in diagrams/*.mmd; do
    mmdc -i \"\$f\" -o \"images/\$(basename \${f%.mmd}).png\" -w 2400 -t default -b white
done
```

> **Tip:** Houd de mermaid-bron in de markdown bestanden als single source of truth. PNG's zijn afgeleid en moeten na elke wijziging opnieuw worden gegenereerd.
