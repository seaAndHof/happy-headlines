workspace "HappyHeadlines" "C4 model of the HappyHeadlines system (Development of Large Systems, E2026)." {

    model {
        Publisher = person "Publisher" "Journalist who writes articles for HappyHeadlines."
        Reader = person "Reader" "Reads, comments on and subscribes to positive news."

        HappyHeadlines = softwareSystem "HappyHeadlines" "Positive news from around the world, with comments and a daily newsletter." {
            Webapp = container "Webapp" "" "Where publishers write drafts and publish articles." "WebBrowser,NotBuilt"
            Website = container "Website" "" "Shows the most recent articles, with one highlighted." "WebBrowser,NotBuilt"

            DraftService = container "DraftService" "REST API" "Saves and loads drafts." "Service,NotBuilt"
            DraftDatabase = container "DraftDatabase" "" "Stores drafts." "Database,NotBuilt"
            PublisherService = container "PublisherService" "REST API" "Publishes finished articles." "Service,NotBuilt"
            ArticleQueue = container "ArticleQueue" "" "Published articles on their way to the ArticleDatabase." "Queue,NotBuilt"

            ArticleLoadBalancer = container "Article load balancer" "nginx" "X-axis: spreads requests round robin over the ArticleService replicas." "LoadBalancer"
            ArticleService = container "ArticleService ×3" ".NET REST API" "Article CRUD per continent. Three identical, stateless replicas." "Service" {
                ArticleEndpoints = component "Article endpoints" "ASP.NET Core minimal API" "REST CRUD under /articles/{continent}/{id}."
                Coordinator = component "ArticleDatabase Coordinator" "C#" "Z-axis coordination: hands out the database of the requested continent (one connection pool each)."
                CacheClient = component "ArticleCache client" "StackExchange.Redis" "Global reads look in the cache first; edits and deletes remove the cached copy."
                CacheMetrics = component "Cache metrics" "prometheus-net" "Counts hits and misses and their duration; serves /metrics."
                Observability = component "Observability" "Serilog, OpenTelemetry" "Sends logs to Seq and traces to Jaeger."
            }
            ArticleCache = container "ArticleCache" "Redis" "Global articles from the last 14 days." "Database"
            ArticleCacheWarmer = container "ArticleCacheWarmer" ".NET worker" "Offline process: copies the last 14 days of global articles into the ArticleCache every minute." "Service"
            GlobalArticleDatabase = container "ArticleDatabase (global)" "PostgreSQL" "News for the whole world. In North America." "Database"
            ContinentArticleDatabases = container "ArticleDatabase ×7 (continents)" "PostgreSQL" "One database per continent (z-axis split)." "Database"

            CommentService = container "CommentService ×2" ".NET REST API" "Reads and stores comments; new comments pass the ProfanityService." "Service"
            CommentCache = container "CommentCache" "Redis" "Comments of the 30 most recently read articles (LRU)." "Database"
            CommentDatabase = container "CommentDatabase" "PostgreSQL" "All comments." "Database"
            ProfanityService = container "ProfanityService" ".NET REST API" "Masks profanity in a text." "Service"
            ProfanityDatabase = container "ProfanityDatabase" "PostgreSQL" "The words to mask." "Database"

            SubscriberService = container "SubscriberService" ".NET REST API" "Subscribe and unsubscribe. Hidden behind a release toggle." "Service"
            SubscriberDatabase = container "SubscriberDatabase" "PostgreSQL" "All subscribers." "Database"
            SubscriberQueue = container "SubscriberQueue" "RabbitMQ" "New subscribers." "Queue"
            NewsletterService = container "NewsletterService" ".NET worker" "Sends the daily newsletter, and a welcome mail to new subscribers." "Service"

            Monitoring = container "Monitoring" "Seq, Jaeger, Prometheus, Grafana" "Central logs, distributed traces, metrics and dashboards." "Monitoring"
        }

        # Publishing
        Publisher -> Webapp "Writes and publishes articles"
        Webapp -> DraftService "Saves and loads drafts"
        DraftService -> DraftDatabase "Stores drafts"
        Webapp -> PublisherService "Publishes an article"
        PublisherService -> ProfanityService "Filters profanity"
        PublisherService -> ArticleQueue "Puts the published article"
        ArticleService -> ArticleQueue "Takes new articles"

        # Reading
        Reader -> Website "Reads, comments and subscribes"
        Website -> ArticleLoadBalancer "Gets articles" "HTTP"
        ArticleLoadBalancer -> ArticleService "Forwards, round robin" "HTTP"
        ArticleService -> ArticleCache "Reads global articles first"
        ArticleService -> GlobalArticleDatabase "Reads and writes global articles"
        ArticleService -> ContinentArticleDatabases "Reads and writes each continent's articles"
        ArticleCacheWarmer -> GlobalArticleDatabase "Reads the last 14 days"
        ArticleCacheWarmer -> ArticleCache "Fills, every minute"

        # Commenting
        Website -> CommentService "Posts and reads comments" "HTTP"
        CommentService -> CommentCache "Reads comments first"
        CommentService -> CommentDatabase "Stores and reads comments"
        CommentService -> ProfanityService "Filters profanity (circuit breaker)" "HTTP"
        ProfanityService -> ProfanityDatabase "Reads the words"

        # Subscribing
        Website -> SubscriberService "Subscribes and unsubscribes" "HTTP"
        SubscriberService -> SubscriberDatabase "Stores subscribers"
        SubscriberService -> SubscriberQueue "Puts new subscribers"
        NewsletterService -> SubscriberQueue "Takes new subscribers, sends a welcome mail" "AMQP"
        NewsletterService -> SubscriberService "Gets all subscribers (if the toggle is on)" "HTTP"
        NewsletterService -> ArticleLoadBalancer "Gets recent articles" "HTTP"

        # Inside ArticleService (level 3)
        ArticleLoadBalancer -> ArticleEndpoints "Forwards, round robin" "HTTP"
        ArticleEndpoints -> Coordinator "Asks for the continent's database"
        Coordinator -> GlobalArticleDatabase "global"
        Coordinator -> ContinentArticleDatabases "africa, antarctica, asia, europe, north-america, oceania, south-america"
        ArticleEndpoints -> CacheClient "Global articles: cache first"
        CacheClient -> ArticleCache "GET / DEL article:{id}"
        ArticleEndpoints -> CacheMetrics "Records hit or miss"
        ArticleEndpoints -> Observability "Logs and traces"
        Observability -> Monitoring "Logs to Seq, traces to Jaeger"
        CacheMetrics -> Monitoring "Scraped by Prometheus (/metrics)"
    }

    views {
        systemContext HappyHeadlines "Context" "Level 1: HappyHeadlines and its users." {
            include *
            autoLayout lr
        }

        container HappyHeadlines "Containers" "Level 2: the containers of HappyHeadlines. Dashed: designed, not built in this project." {
            include *
            exclude Monitoring
            autoLayout lr
        }

        component ArticleService "ArticleService-Components" "Level 3: inside ArticleService." {
            include *
            autoLayout lr
        }

        styles {
            element "Element" {
                color #ffffff
            }
            element "Person" {
                background #9b191f
                shape person
            }
            element "Software System" {
                background #ba1e25
            }
            element "Container" {
                background #d9232b
            }
            element "Component" {
                background #e66c5a
            }
            element "Service" {
                shape hexagon
            }
            element "Database" {
                shape cylinder
            }
            element "Queue" {
                shape pipe
            }
            element "WebBrowser" {
                shape WebBrowser
            }
            element "LoadBalancer" {
                background #b0464b
            }
            element "Monitoring" {
                background #6b6b6b
            }
            element "NotBuilt" {
                background #c9a3a5
                border dashed
            }
        }
    }
}
