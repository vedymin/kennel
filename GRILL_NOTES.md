# GRILL_NOTES — Hotel dla psów (`kennel`)

Notatki z sesji `/grill-me` z dnia 2026-05-02. Zapisane jako pełen, niesprasowany kontekst do wejścia w `/to-prd` w kolejnej sesji. **Nie streszczać przy czytaniu** — wszystko tu jest po coś.

---

## 0. Cel meta tego eksperymentu

User (`michalskimlm@gmail.com`) chce **przetestować workflow agentowy Matta Pococka** opisany w artykule https://www.aihero.dev/5-agent-skills-i-use-every-day, oraz, w drugiej fazie, **ralph-loop**.

Aplikacja `kennel` (hotel dla psów) to **pretekst** — celowo wybrana jako mała i nudna, żeby skupić uwagę na samym procesie, nie na domenie.

**Decyzja zatwierdzona w trakcie grilla:** ralph-loop **odłożony**. Najpierw przechodzimy pełen workflow Matta (`/grill-me` → `/to-prd` → `/to-issues` → `/tdd` → `/improve-codebase-architecture`) i oceniamy, czy jest sensowny. Dopiero potem wracamy do ralpha. Wszystkie wcześniejsze ustalenia o ralph-loop (worktree per issue, stop conditions, `/work-issue` skill itd.) są **odłożone na później**, nie są teraz częścią PRD.

Pierwotnie planowana była hybryda „Matt = planowanie, ralph-loop = wykonanie per-issue", ale user się z tego wycofał: chce najpierw zobaczyć sam workflow Matta w czystej postaci.

---

## 1. Środowisko, narzędzia, ścieżki

- **Working dir:** `C:\Users\micha\Programowanie\ralph-loop-tests\kennel`
- **OS:** Windows 10, shell bash (Unix syntax), PowerShell też dostępny
- **Git:** folder NIE jest jeszcze repo gita (na moment grilla). Trzeba `git init` + utworzyć remote.
- **.NET:** zainstalowane `10.0.203` (`/c/Program Files/dotnet/dotnet`)
- **Node:** `v24.11.1`, npm `11.6.2`
- **Dziś:** 2026-05-02

### Skille zainstalowane globalnie w `C:\Users\micha\.claude\skills\`

Po sesji grilla zostały dociągnięte. Stan na koniec grilla:

- `find-skills` (było wcześniej)
- `grill-me` (było wcześniej, używany w tej sesji)
- `to-prd` (mattpocock/skills@to-prd, świeżo zainstalowany, **wymaga restartu Claude Code by się załadował do dostępnych skilli**)
- `to-issues` (mattpocock/skills@to-issues, świeżo, restart wymagany)
- `tdd` (mattpocock/skills@tdd, świeżo, restart wymagany)
- `improve-codebase-architecture` (mattpocock/skills@improve-codebase-architecture, świeżo, restart wymagany)

Uwaga: w wynikach `npx skills find` pojawił się też alternatywny `mattpocock/skills@prd-to-issues` (8.7K installs vs `to-issues` 23.5K). Zainstalowano `to-issues` zgodnie z artykułem. Jeśli okaże się przestarzały, można rozważyć podmianę na `prd-to-issues`.

### Plan na start kolejnej sesji

1. Zrestartować Claude Code w katalogu `kennel` żeby skille się załadowały.
2. Otworzyć ten plik (`GRILL_NOTES.md`) i upewnić się, że Claude go przeczytał.
3. Odpalić `/to-prd` używając tych notatek jako wejścia.
4. PRD powinien zostać zsubmitowany jako GitHub issue (zgodnie z workflow Matta) — ale przedtem trzeba mieć repo na GitHubie (sekcja 3).

---

## 2. Filozofia testowania workflow Matta — co konkretnie sprawdzamy

Z artykułu kluczowe cytaty / zasady, które chcemy zweryfikować w praktyce:

- **„You have access to a fleet of middling to good engineers that you can deploy at any time. But these engineers have a critical flaw: they have no memory."** Stąd: cały proces musi być w skillach, nie w pamięci sesji.
- **`/grill-me`:** „Interview me relentlessly until shared understanding." Ta sesja była tym etapem.
- **`/to-prd`:** generuje formalny PRD z user stories, eksploruje repo żeby zweryfikować założenia, robi krótki interview (może wywołać grill-me), szkicuje moduły, **submituje PRD jako GitHub issue**.
- **`/to-issues`:** rozbija PRD na taski, ale w **vertical slices**, nie horizontal layers. „Tracer bullet" — cienkie, ale end-to-end. Ustanawia blocking relationships między issue.
- **`/tdd`:** red-green-refactor. „Doing really good TDD has been the most consistent way to improve agent outputs." Confirm interfaces → write tests first → implement → refactor.
- **`/improve-codebase-architecture`:** uruchamiać raz w tygodniu albo po surge developmentu. Wykrywa unclear test boundaries, tightly coupled modules, shallow module design.

**Co konkretnie chcemy zaobserwować podczas testu:**
- Czy `/to-prd` faktycznie wyciągnie z tych notatek dobry PRD bez kolejnej rundy pytań (user nie chce drugiego grilla).
- Czy `/to-issues` rozbije PRD na sensowne vertical slices, czy zrobi horizontal („setup .NET project", „setup Svelte project" — to byłoby horizontal i niedobre wg Matta).
- Czy `/tdd` daje się prowadzić sensownie dla aplikacji o 2 endpointach.
- Czy `/improve-codebase-architecture` ma w ogóle co znaleźć w tak małej apce, czy jest no-op dla tej skali.

---

## 3. Repo i workflow githubowy

**Decyzje (wszystkie zatwierdzone „rekomendacją"):**

- **Repo:** prywatne, na GitHubie usera `michalskimlm@gmail.com` (musi sprawdzić jakie ma konto github gh CLI).
- **Nazwa:** `kennel`.
- **Struktura:** monorepo z `backend/` i `frontend/` jako podkatalogami. Jeden `README.md`, jeden `Makefile` na root.
- **Branch strategy:** `main` chroniony (jeśli plan GitHub na to pozwala dla prywatnego — jeśli nie, zasadą społeczną), każde issue → branch `issue-N-slug`, PR → squash merge.
- **PR per issue.** Człowiek (user) jest jedynym mergerem. To **świadomy circuit breaker** — bez przeglądu mergowane są tylko rzeczy, które user zatwierdził. Krytyczne dla wartości testu.
- **PRD = GitHub issue** (zgodnie z workflow Matta). `/to-prd` powinien go utworzyć przez `gh issue create`.
- **Issues z `/to-issues`** też przez `gh`, z `Closes #PRD_number` w opisie albo blocking relations.

**Czego brakuje na moment startu nowej sesji:**
- `git init` + `.gitignore` (.NET + Node + IDE)
- `gh repo create kennel --private --source . --remote origin --push` (lub równoważnik)
- Pierwszy commit (pusty scaffold + ten plik notatek)

Decyzja niezamknięta: czy to zrobić **przed** `/to-prd`, czy zrobić to jako pierwszy issue/task po `/to-issues`. Sugestia: **przed**, bo bez repo `/to-prd` nie ma gdzie wrzucić issue. Ale to można potraktować jako pierwszy ruch w nowej sesji, niezależny od PRD.

---

## 4. Stack techniczny

Wszystkie poniższe zatwierdzone „rekomendacją" przez usera. Przy każdej decyzji jest też uzasadnienie — żeby `/to-prd` mógł je przepisać do PRD bez wymyślania na nowo.

### Backend
- **.NET 10 Minimal API** (jeden `Program.cs`, bez kontrolerów). Powód: mała apka, mniej ceremonii = mniej powierzchni do błędów dla agenta.
- **EF Core** z providerem **SQLite**. Powód: migracje wbudowane (`dotnet ef migrations add`), agent sobie z nimi poradzi.
- **OpenAPI/Swagger** (`Microsoft.AspNetCore.OpenApi`). Bez codegenu do frontu — tylko do debugowania w developmencie.
- **Plik bazy:** lokalny SQLite, np. `backend/kennel.db`. Dodać do `.gitignore`. W dev na razie tylko jedna baza, bez profili.

### Frontend
- **SvelteKit** + **Svelte 5 z runes**.
- `adapter-static`, `ssr=false`, **SPA mode**. Powód: backend i tak osobno, SSR by tylko komplikował.
- **Tailwind 4** (z `@import "tailwindcss"` w `app.css`). Bez biblioteki komponentów (shadcn, skeleton, itd.) — overkill na 1 ekranie i zwiększyłoby surface area.
- Zero CSS pisanego ręcznie poza `app.css`.

### Komunikacja
- REST/JSON, ręczny `fetch` w froncie. Bez OpenAPI codegen.
- Dev: backend `localhost:5174`, frontend `localhost:5173`. **CORS otwarte na dev** (origin `http://localhost:5173`). Nie zostawiać `*` w prod, ale prod nas nie obchodzi w v1.

### Testy (kluczowe dla `/tdd` Matta)
- **Backend:** xUnit + `WebApplicationFactory<TProgram>` dla integration tests. Baza per test: in-memory SQLite (connection per test, `Mode=Memory;Cache=Shared` lub `:memory:` z trzymaniem connection żywego). EF Core `EnsureCreated` lub `Migrate` — do ustalenia w trakcie /tdd.
- **Frontend unit:** Vitest (defaultowo z SvelteKit).
- **Frontend e2e:** Playwright, **1-2 happy path testy** (dodanie rezerwacji + wyświetlenie listy). Świadomie minimalne, ale obecne — sanity check „feature naprawdę działa", nie tylko „testy zielone".

### Brak / non-tech
- **Brak Dockera, brak docker-compose.** „Wszystko lokalnie" → `dotnet run` + `npm run dev`.
- **Brak CI** w v1 (pomyślimy później).
- **Brak deploymentu.**

---

## 5. Domena — model rezerwacji (v1)

Najważniejsza sekcja dla `/to-prd`. Wszystko **świadomie zminimalizowane**, żeby `/to-issues` miało materiał na 5-8 vertical slices, a nie 30.

### Encja
```
Reservation {
  id: int (autoincrement, PK)
  dogName: string (wymagane, niepuste po trim, max 50 znaków)
  startDate: date (tylko data, bez czasu)
  endDate: date (tylko data, bez czasu)
  createdAt: timestamp (UTC, ustawiane przez serwer)
}
```

### Walidacja (server-side autoritative; klient TYLKO UX)
- `dogName`: trim, niepuste, długość 1..50
- `endDate > startDate` (minimum 1 noc — równość niedozwolona)
- `startDate >= today` (lokalna data systemu, nie pozwalamy rezerwować wstecz)

### Strefa czasowa
- Lokalna systemowa, **świadomie ignorujemy UTC**. Apka jest zabawką, nie produkcyjnym systemem.

### Endpointy
- `GET /api/reservations` → lista wszystkich, posortowana `startDate ASC`. Zwraca też przeszłe (nie filtrujemy w API).
- `POST /api/reservations` → tworzy. Sukces: `201 Created` + `Location: /api/reservations/{id}` + body utworzonego obiektu. Walidacja zła: `400 Bad Request` z polem `errors` mapującym pole→wiadomość.

### Świadome NON-GOALS w v1 (zapisać explicit w PRD jako sekcja „Out of scope")
- Brak edycji rezerwacji (`PUT/PATCH`)
- Brak usuwania / anulowania (`DELETE`)
- **Brak detekcji konfliktów / capacity hotelu** (zakładamy nieskończony hotel; potencjalny v2)
- Brak właściciela, telefonu, rasy, uwag, ceny, numeru kojca, statusu (potwierdzona/anulowana)
- Brak auth, multi-tenant, ról
- Brak paginacji (zakładamy < 1000 rezerwacji w lifetime apki)
- Brak filtrowania ani wyszukiwania w API
- Brak `GET /api/reservations/{id}` (lista wystarczy)

User świadomie zaakceptował, że bez delete użytkownik nie poprawi literówki w imieniu — to jest świadomy trade-off na rzecz skupienia eksperymentu.

---

## 6. UX / UI (v1)

### Layout
- **Jeden route** `/` w SvelteKit (`src/routes/+page.svelte`).
- Bez routera nawigacyjnego, bez `/new`, bez modali, bez tabsów.
- Pionowo z góry na dół:
  1. Tytuł `Hotel dla psów — rezerwacje`
  2. Form inline (zawsze widoczny): pola `[Imię psa] [Od] [Do]` + button `[Dodaj]`
  3. Lista pod spodem — najprostsza tabela albo lista kart (szczegół wybiera implementacja w `/tdd`)

### 6 stanów do zaimplementowania (każdy może być oddzielnym kryterium akceptacji w issue)
1. **Empty:** „Brak rezerwacji. Dodaj pierwszą powyżej."
2. **Loading (initial fetch):** prosty spinner / „Ładowanie…"
3. **Fetch error:** „Nie udało się pobrać rezerwacji." + przycisk `[Spróbuj ponownie]`
4. **Submit error (walidacja serwera):** komunikat inline pod formem, np. „Data zakończenia musi być po dacie rozpoczęcia". `aria-live="polite"` żeby SR czytał.
5. **Submitting:** przycisk `[Dodawanie…]`, disabled.
6. **Submit success:** form czyści inputy, lista refetch, **bez toasta** (overkill — odświeżenie listy = wystarczający feedback).

### Wizualne wyróżnienie przeszłych rezerwacji
- `opacity-50` + tag „zakończona" obok daty.
- Bez filtra do ukrywania ich (świadomy non-goal).

### Walidacja klienta
- **Minimum:** disable submit gdy któreś pole jest puste.
- **Reszta walidacji idzie z serwera** (żeby ralph / agent nie pisał duplikatu logiki w dwóch miejscach — to jeden z gotchasów Matta z artykułu o garbage codebase = garbage AI output).

### Accessibility (v1, minimalne)
- `<label>` dla każdego inputa (powiązane przez `for`/`id`).
- `aria-live="polite"` na komunikatach błędów.
- `<button type="submit">` z sensownym tekstem.
- **Bez** dalszego a11y w v1 (skip links, focus management, role-y itd.).

### Świadome NON-GOALS UX
- Bez dark mode
- Bez responsive ponad to, co Tailwind daje za darmo (czyli na mobile nadal ma się wyświetlać sensownie, ale nie projektujemy mobile-first)
- Bez animacji
- Bez biblioteki komponentów
- Bez toastów / notyfikacji
- Bez sortowania/filtrowania UI (server zwraca posortowane, koniec)

---

## 7. Format daty na wejściu / wyjściu

- API: ISO `YYYY-MM-DD` w JSON (string). Backend parsuje do `DateOnly` (.NET 6+).
- Frontend: native `<input type="date">` w przeglądarce, który zwraca `YYYY-MM-DD`. Kompatybilność z API natywna, bez parserów.
- `createdAt`: ISO 8601 z `Z`, jako string w JSON, parsowany do `DateTime` UTC po stronie serwera.

---

## 8. Open questions / rzeczy odłożone

- **Seed data dla deva** — przy `startDate >= today` walidacji, jak naładować baze testowymi danymi? Sugestia z grilla: skrypt seedingowy który wpisuje wprost przez DbContext, omija API. Decyzja: zrobić dopiero gdy będzie potrzebne, NIE issue w v1.
- **CI/lint/format:** poza scope v1.
- **Wybór `to-issues` vs `prd-to-issues`** (Matt ma oba): używamy `to-issues` (więcej installs, zgodnie z artykułem).
- **Czy `/to-prd` ma wywołać kolejny `/grill-me`?** User nie chce drugiej rundy grilla. PRD ma być wygenerowany z tych notatek bez powtarzania pytań. Jeśli `/to-prd` próbuje grillować — zatrzymać go i pokazać te notatki jako wystarczające wejście.
- **Komendy dev:** `Makefile` na root z `make dev-backend`, `make dev-frontend`, `make test-backend`, `make test-frontend`, `make test-e2e`, `make test`. To pojawiło się w odłożonej sekcji ralph-loop, ale i bez ralpha jest sensowne — zostawić jako pomysł, niech `/to-issues` zdecyduje czy to issue.

---

## 9. Stan rozmowy: gdzie jesteśmy w procesie Matta

```
[x] /grill-me          ← właśnie skończony, ten plik to jego output
[ ] /to-prd            ← następny krok po restarcie sesji
[ ] /to-issues
[ ] /tdd               ← per issue
[ ] /improve-codebase-architecture  ← na końcu / okresowo
```

Po restarcie nowa sesja powinna:
1. Przeczytać ten plik w całości.
2. Potwierdzić z userem, że nic się nie zmieniło od grilla.
3. Sprawdzić czy istnieje repo gita lokalnie i remote — jeśli nie, zaproponować `git init` + `gh repo create` jako pierwszy krok przed `/to-prd` (`/to-prd` musi mieć dokąd submit'ować issue).
4. Odpalić `/to-prd` z tym plikiem jako kontekstem, instruując by **nie** uruchamiał kolejnego grill-me.

---

## 10. Rzeczy odłożone na fazę po teście Matta (NIE wchodzą do PRD)

- ralph-loop w ogóle
- worktree per issue
- skill `/work-issue` (autorski wrapper na ralpha)
- stop conditions, max iterations, równoległe odpalanie ralphów
- automat `gh pr merge` przez agenta — wszystkie merge robi user ręcznie
- v2: capacity hotelu i konflikty rezerwacji
- v2: edycja / delete rezerwacji
- v2: dane właściciela, kojec, cena, status

Te rzeczy świadomie odłożone — wracamy do nich, gdy workflow Matta zostanie pozytywnie zwalidowany na tej apce.
