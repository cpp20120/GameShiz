# REST baseline игр

Дата замера: 2026-08-07 (UTC).

Это первый baseline для последовательного сравнения производительности REST
маршрутов. Замер выполнен в локальном k3d/k3s, через ingress
`api.casinoshiz.localhost`, с прямой маршрутизацией REST на соответствующие
game-поды.

Параметры каждого сценария:

- `wrk`, 3 секунды;
- 1 thread, 4 connections;
- dev bearer authentication;
- rate limit отключён только в dev-поде;
- сценарии запускались последовательно;
- приложение и runtime снимались через `dotnet-trace` и `dotnet-counters`.

Артефакты полного прогона находятся в
`.artifacts/perf-routing-baseline/<scenario>/<utc>/`. В каждом каталоге есть
`summary.txt`, `wrk.log`, `resources.tsv`, `counters.csv` и `trace.nettrace`.

## Результаты read-only маршрутов

`RPS` — итоговый `Requests/sec` из `wrk`. `CPU` — среднее потребление REST
процесса за время сценария; Linux показывает 100% как одно полностью занятое
логическое CPU.

| Игра/система | Маршрут | HTTP | RPS | Avg latency | App CPU avg | Классификация |
|---|---|---:|---:|---:|---:|---|
| Blackjack | `GET /blackjack/state` | 404 | 553.34 | 7.35 ms | 130.31% | state miss, не capacity baseline |
| Horse | `GET /horse/info` | 200 | 454.09 | 9.06 ms | 105.52% | valid baseline |
| Horse | `GET /horse/result` | 200 | 423.21 | 9.69 ms | 71.18% | valid baseline |
| Pick | `GET /pick/lottery` | 404 | 485.15 | 8.78 ms | 73.06% | state miss, не capacity baseline |
| Pick | `GET /pick/daily` | 404 | 503.50 | 8.45 ms | 70.56% | state miss, не capacity baseline |
| Pick | `GET /pick/daily/history` | 200 | 615.36 | 6.77 ms | 79.66% | valid baseline |
| Pick | `GET /pick/daily/schedule` | 200 | 838.88 | 116.05 ms | 102.16% | valid baseline, высокий latency tail |
| PixelBattle | `GET /pixelbattle/grid` | 200 | 52.31 | 75.90 ms | 143.56% | valid baseline, главный hot path |
| Poker | `GET /poker/tables/me` | 404 | 516.47 | 8.35 ms | 162.20% | state miss, не capacity baseline |
| Secret Hitler | `GET /secrethitler/game` | 404 | 568.94 | 9.74 ms | 130.21% | state miss, не capacity baseline |
| Leaderboard | `GET /leaderboard/` | 200 | 807.52 | 101.74 ms | 92.50% | valid system baseline |

Meta-маршруты также были включены в техническую матрицу, но в текущем
состоянии dev-стенда возвращают `500`. Их значения не являются baseline
производительности успешного запроса и нужны только как диагностическая
фиксация:

| Маршрут | HTTP | RPS | Avg latency | App CPU avg |
|---|---:|---:|---:|---:|
| `GET /meta/season` | 500 | 181.22 | 22.10 ms | 43.16% |
| `GET /meta/profile` | 500 | 166.87 | 24.00 ms | 38.19% |
| `GET /meta/top` | 500 | 181.16 | 22.13 ms | 38.72% |
| `GET /meta/achievements` | 500 | 190.51 | 20.95 ms | 45.58% |
| `GET /meta/streaks` | 500 | 196.50 | 20.26 ms | 64.80% |
| `GET /meta/quests` | 500 | 198.25 | 20.12 ms | 51.76% |
| `GET /meta/clan` | 500 | 188.95 | 21.23 ms | 64.86% |
| `GET /meta/clan/members` | 500 | 1844.26 | 2.54 ms | 99.69% |
| `GET /meta/clan/top` | 500 | 177.94 | 22.43 ms | 47.36% |
| `GET /meta/tournaments/open` | 500 | 208.24 | 19.15 ms | 65.64% |
| `GET /meta/risk` | 500 | 195.57 | 20.41 ms | 42.25% |

## Игры без безопасного read-only baseline

У этих игр в текущем route inventory нет безопасного GET-представления. Их
основные пути меняют состояние, зависят от payload, баланса, активной игры или
идентификатора раунда:

| Игра | Доступные измеряемые пути | Почему не вошла в read baseline |
|---|---|---|
| Dice | `POST /dice/roll` | stateful payload и игровой результат |
| DiceCube | `POST /dicecube/bet`, `/play`, `/roll` | ставка и pending bet |
| Darts | `POST /darts/bet`, `/play`, `/rounds/{roundId}/throw` | round state и payload |
| Football | `POST /football/bet`, `/play`, `/roll` | ставка и pending bet |
| Basketball | `POST /basketball/bet`, `/play`, `/roll` | ставка и pending bet |
| Bowling | `POST /bowling/bet`, `/play`, `/roll` | ставка и pending bet |
| Challenges | `POST /challenges`, `/{challengeId}/accept`, `/{challengeId}/decline` | stateful challenge lifecycle |

Это не означает, что игры медленные или быстрые — для них пока нет
репрезентативного измерения. Для следующего прохода нужен отдельный сценарий
на каждую игру: подготовка fixture, валидный payload, уникальный тестовый
пользователь/счёт и cleanup либо изолированный namespace базы. Stateful POST
нельзя добавлять в общий baseline без такой защиты.

## Как использовать baseline

Для сравнения после оптимизации сохраняем те же маршрут, длительность,
конфигурацию, число потоков и соединений. Сначала имеет смысл разбирать:

1. `PixelBattle /grid` — самый низкий успешный результат: 52.31 RPS и 143.56%
   CPU в initial-профиле; устойчивый high-load режим даёт 131.20 RPS при 32
   connections и уже 241.28 ms latency.
2. `Horse` — около 1.2k RPS при 128 connections и 105–109 ms latency.
3. `Leaderboard /` — 2005.54 RPS при 128 connections и 66.98 ms latency.
4. `Pick /daily/schedule` — 3855.30 RPS при 128 connections и 33.37 ms
   latency.

404 и 500 из таблиц не следует использовать как целевой throughput успешного
запроса. Перед оптимизацией Meta сначала нужно исправить ошибку, затем
переснять его baseline теми же параметрами.

## High-concurrency follow-up

После первого прогона с 1 thread / 4 connections был выполнен отдельный
scaling-тест Horse на полном k3s-пути. Он показывает, почему первый результат
нельзя считать пределом приложения:

| Threads | Connections | Duration | RPS | Avg latency | App CPU avg | Вывод |
|---:|---:|---:|---:|---:|---:|---|
| 1 | 4 | 3 s | 454.09 | 9.06 ms | 105.52% | ограничение concurrency |
| 2 | 32 | 10 s | 1250.38 | 25.89 ms | 108.12% | throughput растёт |
| 2 | 128 | 10 s | 1959.02 | 65.14 ms | 212.91% | максимум throughput в sweep |
| 2 | 256 | 10 s | 1140.34 | 222.29 ms | 219.01% | очередь/деградация |
| 2 | 512 | 10 s | 1090.91 | 457.23 ms | 95.20% | перегруз, RPS падает |

Таким образом, `128 connections` — рабочий high-load режим для сравнения
большинства REST-маршрутов в этом dev k3s стенде. Это не универсальный предел
сервиса: при изменении CPU, числа реплик, базы или сетевого пути sweep нужно
повторить.

## Игровые маршруты при 128 connections

Матрица выполнена последовательно, 2 threads / 128 connections / 10 секунд на
сценарий. Для 404 сохранена техническая пропускная способность state-miss, а
не throughput успешной игровой операции.

| Игра | Маршрут | HTTP | RPS | Avg latency | App CPU avg |
|---|---|---:|---:|---:|---:|
| Blackjack | `GET /blackjack/state` | 404 | 1766.44 | 72.76 ms | 117.10% |
| Horse | `GET /horse/info` | 200 | 1211.60 | 105.16 ms | 209.59% |
| Horse | `GET /horse/result` | 200 | 1172.08 | 108.54 ms | 198.06% |
| Pick | `GET /pick/lottery` | 404 | 1751.75 | 72.61 ms | 145.72% |
| Pick | `GET /pick/daily` | 404 | 1964.49 | 64.95 ms | 160.23% |
| Pick | `GET /pick/daily/history` | 200 | 1856.33 | 68.65 ms | 157.94% |
| Pick | `GET /pick/daily/schedule` | 200 | 3855.30 | 33.37 ms | 253.74% |
| PixelBattle | `GET /pixelbattle/grid` | 200 | 118.85 | 928.99 ms | 187.81% |
| Poker | `GET /poker/tables/me` | 404 | 1479.84 | 86.54 ms | 206.93% |
| Secret Hitler | `GET /secrethitler/game` | 404 | 1248.08 | 103.26 ms | 131.74% |
| Leaderboard | `GET /leaderboard/` | 200 | 2005.54 | 66.98 ms | 126.59% |

PixelBattle в этом режиме получил 62 socket timeout. Его устойчивый отдельный
результат — 131.20 RPS при 2 threads / 32 connections / 10 секунд без timeout;
при 64 connections RPS упал до 76.81, а latency выросла до 803.63 ms. Поэтому
для PixelBattle high-load baseline нужно использовать отдельный concurrency
профиль, а не общие 128 connections.

Артефакты scaling-теста находятся в `.artifacts/perf-scaling/`, а артефакты
high-concurrency матрицы — в `.artifacts/perf-high-concurrency/`.

## Post-fix Horse baseline

До этого прогона dev k3s использовал `1 CPU / 512 MiB` для REST и backend, а
`game-horse` продолжал работать из старого backend-образа. В нём не было
текущего кэширования read-only projection; кроме того, кластерная конфигурация
включала OTLP и ClickHouse, хотя актуальный Helm chart для этого профиля их не
включает. Поэтому прежний результат смешивал ограничение CPU, старый бинарник и
лишнюю dev-observability нагрузку.

Исправлено:

- добавлен `deploy/helm/cazinoshiz/values.dev.yaml` с `2 CPU / 1 GiB` для всех
  game backend и REST pod-ов;
- для dev включены `imagePullPolicy: Always` и Development auth, rate limit
  отключён;
- game-horse обновлён свежим образом с local/distributed cache;
- для чистого замера в pod отключены ClickHouse и OTEL SDK;
- Redis после cold-запроса содержит ключ
  `horse:race-info:v1:<date>:horses:<count>:scope:<scope>`, то есть кэш реально
  используется.

После фикса, через тот же ingress, 2 threads, 10 секунд:

| Target | Connections | RPS | Avg latency | Target CPU avg | Target RSS max | Result |
|---|---:|---:|---:|---:|---:|---|
| REST | 32 | 2704.70 | 12.00 ms | 260.02% | 204.52 MiB | устойчивый baseline |
| REST | 64 | 2510.38 | 26.20 ms | 165.87% | 183.02 MiB | устойчивый high-load |
| REST | 128 | 2032.82 | 63.63 ms | 252.52% | 204.52 MiB | перегруз, counters фиксируют 499 |
| game-horse | 64 | 2581.85 | 25.37 ms | 214.80% | 214.85 MiB | backend side profile |

Проценты CPU в этом harness — Linux process CPU; `100%` соответствует одному
полностью занятому CPU, а pod limit после фикса равен двум CPU. Для сравнения
используем c32 как устойчивый RPS baseline и c64 как high-load baseline. c128
оставляем диагностическим тестом очереди, а не целевой пропускной способностью:
там часть клиентов закрывает запросы раньше ответа, что отображается как HTTP
499.

Артефакты post-fix прогона находятся в
`.artifacts/perf-after-fix/horse-info-after-cache-c32/`,
`.artifacts/perf-after-fix/horse-info-after-cache-c64/` и
`.artifacts/perf-after-fix/horse-info-gamebackend-c64/`.
