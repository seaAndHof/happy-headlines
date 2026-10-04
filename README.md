# HappyHeadlines: caching

Compulsory assignment 1 in Development of Large Systems: an ArticleCache and a CommentCache, with a dashboard showing each cache's hit ratio.

Everything runs in Docker:

```sh
docker compose up --build -d
```

| What | Where |
|---|---|
| Dashboard (Grafana) | http://localhost:3030 |
| Prometheus | http://localhost:9091 |
| RabbitMQ (guest / guest) | http://localhost:15673 |
| Transatlantic link control panel | http://localhost:8475 |
| Database viewer (DbGate) | http://localhost:8085 |
| Transatlantic link (Toxiproxy API) | http://localhost:8474/proxies |

Stop with `docker compose down`.

## Containers

Every container carries `happyheadlines.c4` and `happyheadlines.description` labels (`docker inspect`, or the Docker Desktop UI).

| Container | C4 container | What it does |
|---|---|---|
| `article-db` | ArticleDatabase | PostgreSQL: the global article database, in North America |
| `article-cache` | ArticleCache | Redis: articles from the last 14 days |
| `article-cache-warmer` | ArticleCache (offline process) | Copies the last 14 days of articles into the ArticleCache, on start and then every minute |
| `article-service` ×3 | ArticleService | Answers `GetArticle`: from the cache, or from the database on a miss |
| `comment-db` | CommentDatabase | PostgreSQL: all comments, in North America |
| `comment-cache` | CommentCache | Redis: comments of the 30 most recently read articles |
| `comment-service` ×2 | CommentService | Answers `GetComments` (cache first, fills it on a miss); stores `PostComment` |
| `transatlantic-link` | | Toxiproxy: the network between Europe and North America. Database traffic passes through it and gets 100 ms of latency |
| `transatlantic-link-setup` | | One-shot: creates the two links (`article-db`, `comment-db`), then exits |
| `database-viewer` | | DbGate: browse both databases and both caches in the browser, read-only |
| `transatlantic-control` | | Control panel: a big slider per link (all the way right cuts it) and live hit and miss times |
| `rabbitmq` | | Message broker for MassTransit |
| `reader-simulator` | Website (simulated) | 5 simulated readers: open an article, read its comments, sometimes post a comment |
| `prometheus`, `grafana` | | Scrape the cache metrics; show the dashboard |

The services talk only through MassTransit messages: `GetArticle` and `GetComments` are request/response, and `PostComment` is fire-and-forget. The replicas of a service share one queue, so each message is handled by one of them.

**Seed data:** 300 articles; article *n* was published *n* × 5 hours ago, so articles 1–67 are from the last 14 days. Each article has 3 comments.

**Distance to North America:** both databases are in North America and the services are in Europe. The services reach the databases through `transatlantic-link`, a [Toxiproxy](https://github.com/Shopify/toxiproxy) container that delays every database response by 100 ms. The code knows nothing about it: the delay is in the network, where it belongs. The Redis caches are in Europe, so they're reached directly.

## How the brief is covered

| Requirement | Where |
|---|---|
| ArticleCache between ArticleService and the global ArticleDatabase | [`GetArticleConsumer`](src/ArticleService/GetArticleConsumer.cs) |
| Filled by a periodic offline process with articles from the last 14 days | [`ArticleCacheRefresher`](src/ArticleCacheWarmer/ArticleCacheRefresher.cs) |
| CommentCache between CommentService and the CommentDatabase, filled on a cache miss | [`GetCommentsConsumer`](src/CommentService/GetCommentsConsumer.cs) |
| Holds the comments of the 30 most recently accessed articles; least recently used evicted when full | [`CommentCache`](src/CommentService/CommentCache.cs) |
| Dashboard with the hit ratio of both caches | [`caching.json`](monitoring/grafana/dashboards/caching.json); metrics in [`CacheMetrics`](src/HappyHeadlines.Shared/CacheMetrics.cs) |

## Design decisions and trade-offs

### Distributed cache (Redis), not IMemoryCache or HybridCache

- **ArticleCache:** a separate offline process fills it, and 3 ArticleService replicas read it. With an in-memory cache, every replica would hold its own copy and run its own refresh. That means three times the reads against the database in North America, which is the thing the cache exists to protect, and a new replica would start empty. With Redis there is one copy, filled once, and every replica shares it.
- **CommentCache:** "the 30 most recently read articles" is one list for the whole service. In memory, each replica would keep its own 30, in a different order.
- **HybridCache** (in-memory in front of Redis) would make hits slightly faster, but each replica's in-memory copy can go stale. It also has no limit by number of articles. A Redis hit already takes under 1 ms, against more than 100 ms for a miss.
- **The cost:** one more container per cache, and a network hop on every lookup. If Redis goes down, lookups fail. The next step would be to fall back to the database.

### ArticleCache

- **Never filled on a miss:** a read that misses (an article older than 14 days, or anything before the first refresh) goes to the database every time. Only the offline process fills the cache.
- **Refresh interval:** 1 minute for the demo; in production it might be 15 minutes. Each entry lives for two refresh intervals. Every refresh renews the articles still within 14 days, so an article that ages out, or is deleted, disappears on its own.
- **Stale data:** an edited article can be stale for up to one refresh interval. That is the accepted trade-off of filling a cache offline.

### CommentCache

- **Per-article entries:** one key per article (`comments:{id}`) holds all its comments. A sorted set (`comments:last-read`) records when each article was last read.
- **On a miss:** read the database, store the comments, mark the article as read, and if more than 30 articles are cached, remove the ones read longest ago. Redis can only evict by memory use, not by count, so this rule is written in the service.
- **New comments:** a new comment removes its article from the cache, so the next read loads the fresh list.
- **Two replicas:** they can interleave these steps, so the worst case is one extra eviction. That's acceptable for a cache.

## Demo script

1. **Dashboard:** open http://localhost:3030.
   - The ArticleCache hit ratio sits around **80%**: readers ask for last-14-days articles 80% of the time, and older ones are always misses.
   - The CommentCache hit ratio sits around **25–30%**: 30 slots for readers spread over 300 articles. The evictions panel shows the LRU at work.
2. **Hit versus miss:** the bar panel shows under 1 ms for a hit against about 100 ms for a miss.
3. **Move North America further away:** on the control panel (http://localhost:8475), drag the Articles slider to 1000 ms. Misses slow down; hits don't change.
4. **Cut the transatlantic link:** drag the Articles slider all the way right, until it says **cut**. Articles from the last 14 days are still served from the ArticleCache; every miss fails. That's the brief's availability problem, solved for recent articles. After 2 minutes the refresh has failed twice, the entries expire, and everything fails. Drag it back to bring the link back.

   Every change on the panel also puts a marker on the Grafana graphs. Without the panel, the same changes can be made with `curl`:
   ```sh
   curl -X POST localhost:8474/proxies/article-db/toxics/distance -d '{"attributes": {"latency": 1000}}'
   curl -X POST localhost:8474/proxies/article-db -d '{"enabled": false}'
   curl -X POST localhost:8474/proxies/article-db -d '{"enabled": true}'
   ```
5. **Cold ArticleCache:**
   ```sh
   docker compose exec article-cache redis-cli FLUSHALL   # article hit ratio drops to 0
   docker compose restart article-cache-warmer            # refills immediately; hit ratio recovers
   ```
6. **The offline process stops:** `docker compose stop article-cache-warmer`. After 2 minutes the entries expire and every read misses. Bring it back with `docker compose start article-cache-warmer`.
7. **The LRU list:**
   ```sh
   docker compose exec comment-cache redis-cli ZCARD comments:last-read                 # 30
   docker compose exec comment-cache redis-cli ZRANGE comments:last-read 0 -1 WITHSCORES # least recently read first
   ```
8. **Scaling out:** `docker compose up -d --scale article-service=5`. The new replicas share the already-filled cache and hit straight away.

## Not included

This is the slice the caching assignment needs. Earlier weekly requirements are not part of it:
- the REST CRUD API (W36; this solution uses messaging),
- the continent databases (W36),
- ProfanityService and the circuit breaker (W37),
- PublisherService and the ArticleQueue,
- logging and tracing (W38–W39).
