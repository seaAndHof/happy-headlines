workspace "HappyHeadlines" "C4 model of the HappyHeadlines system (Development of Large Systems, E2026)." {

    model {
        Publisher = person "Publisher" "Journalist who writes articles."
        Reader = person "Reader" "Reads, comments and subscribes."

        HappyHeadlines = softwareSystem "HappyHeadlines" "Positive news from around the world, with comments and a daily newsletter." {
            Website = container "Website" "Simulated by reader-simulator" "Recent articles, comments and newsletter sign-up." "WebBrowser,Frontend"

            # Each group is a swimlane: its own services and its own data, nothing shared.
            group "Subscribers" {
                SubscriberService = container "SubscriberService" ".NET REST API" "Subscribe and unsubscribe. Behind a release toggle." "Service,Subscribers"
                SubscriberDatabase = container "SubscriberDatabase" "PostgreSQL" "All subscribers." "Database,Subscribers"
                SubscriberQueue = container "SubscriberQueue" "RabbitMQ" "New subscribers." "Queue,Subscribers"
            }

            group "Newsletter" {
                NewsletterService = container "NewsletterService" ".NET worker" "Daily newsletter and welcome mails." "Service,Newsletter"
            }

            group "Articles" {
                ArticleLoadBalancer = container "Load balancer" "Caddy" "Round robin over the ArticleService replicas (x-axis)." "LoadBalancer,Articles"
                ArticleService = container "ArticleService ×3" ".NET REST API" "Article CRUD per continent." "Service,Articles" {
                    ArticleEndpoints = component "Article endpoints" "ASP.NET Core minimal API" "REST CRUD under /articles/{continent}/{id}."
                    Coordinator = component "ArticleDatabase Coordinator" "C#" "Z-axis coordination: hands out the database of the requested continent (one connection pool each)."
                    CacheClient = component "ArticleCache client" "StackExchange.Redis" "Global reads look in the cache first; edits and deletes remove the cached copy."
                    CacheMetrics = component "Cache metrics" "prometheus-net" "Counts hits and misses and their duration; serves /metrics."
                    Observability = component "Observability" "Serilog, OpenTelemetry" "Sends logs to Seq and traces to Jaeger."
                }
                ArticleCache = container "ArticleCache" "Redis" "Global articles from the last 14 days." "Database,Articles"
                ArticleCacheWarmer = container "ArticleCacheWarmer" ".NET worker" "Refills the ArticleCache every minute." "Service,Articles"
                ArticleDatabases = container "ArticleDatabase ×8" "PostgreSQL" "7 continents + 1 global (z-axis split)." "Database,Articles"
            }

            group "Comments" {
                CommentService = container "CommentService ×2" ".NET REST API" "Reads and stores comments." "Service,Comments"
                CommentCache = container "CommentCache" "Redis" "Comments of the 30 most recently read articles (LRU)." "Database,Comments"
                CommentDatabase = container "CommentDatabase" "PostgreSQL" "All comments." "Database,Comments"
            }

            group "Profanity" {
                ProfanityService = container "ProfanityService" ".NET REST API" "Masks profanity in a text." "Service,Profanity"
                ProfanityDatabase = container "ProfanityDatabase" "PostgreSQL" "The words to mask." "Database,Profanity"
            }

            group "Publishing (designed, not built)" {
                Webapp = container "Webapp" "" "Write drafts and publish articles." "WebBrowser,NotBuilt"
                DraftService = container "DraftService" "REST API" "Saves and loads drafts." "Service,NotBuilt"
                DraftDatabase = container "DraftDatabase" "" "Drafts." "Database,NotBuilt"
                PublisherService = container "PublisherService" "REST API" "Publishes finished articles." "Service,NotBuilt"
                ArticleQueue = container "ArticleQueue" "" "Newly published articles." "Queue,NotBuilt"
            }

            Monitoring = container "Monitoring" "Seq, Jaeger, Prometheus, Grafana" "Central logs, distributed traces, metrics and dashboards." "Monitoring"
        }

        # Publishing
        Publisher -> Webapp "Writes articles"
        Webapp -> DraftService "Saves drafts"
        DraftService -> DraftDatabase "Stores"
        Webapp -> PublisherService "Publishes"
        PublisherService -> ProfanityService "Checks profanity"
        PublisherService -> ArticleQueue "Queues article"
        ArticleQueue -> ArticleService "New articles"

        # Reading
        Reader -> Website "Uses"
        Website -> ArticleLoadBalancer "Reads articles"
        ArticleLoadBalancer -> ArticleService "Round robin"
        ArticleService -> ArticleCache "Global: cache first"
        ArticleService -> ArticleDatabases "Reads and writes"
        ArticleCacheWarmer -> ArticleDatabases "Last 14 days"
        ArticleCacheWarmer -> ArticleCache "Fills"

        # Commenting
        Website -> CommentService "Reads and posts comments"
        CommentService -> CommentCache "Cache first"
        CommentService -> CommentDatabase "Reads and writes"
        CommentService -> ProfanityService "Filters (circuit breaker)"
        ProfanityService -> ProfanityDatabase "Reads words"

        # Subscribing
        Website -> SubscriberService "Subscribes"
        SubscriberService -> SubscriberDatabase "Stores"
        SubscriberService -> SubscriberQueue "New subscriber"
        SubscriberQueue -> NewsletterService "Welcome mail"
        NewsletterService -> SubscriberService "Gets subscribers (toggle)"
        NewsletterService -> ArticleLoadBalancer "Recent articles"

        # Inside ArticleService (level 3)
        ArticleLoadBalancer -> ArticleEndpoints "Round robin" "HTTP"
        ArticleEndpoints -> Coordinator "Asks for the continent's database"
        Coordinator -> ArticleDatabases "One connection pool per database"
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

        container HappyHeadlines "Containers" "Level 2: HappyHeadlines, one colour per swimlane." {
            include *
            exclude Monitoring
            autoLayout tb 200 120
        }


        component ArticleService "ArticleService-Components" "Level 3: inside ArticleService." {
            include *
            autoLayout lr
        }

        styles {
            element "Element" {
                color #ffffff
                stroke #ffffff
                strokeWidth 4
            }
            element "Person" {
                background #37474f
                shape person
            }
            element "Software System" {
                background #37474f
            }
            element "Component" {
                background #e57373
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
            element "Frontend" {
                background #607d8b
            }

            element "Articles" {
                background #c62828
            }
            element "Comments" {
                background #ef6c00
            }
            element "Profanity" {
                background #ad1457
            }
            element "Subscribers" {
                background #6a1b9a
            }
            element "Newsletter" {
                background #1565c0
            }
            element "LoadBalancer" {
                background #8e1c1c
            }
            element "NotBuilt" {
                background #bdbdbd
                border dashed
            }
            element "Monitoring" {
                background #546e7a
            }

            element "Group:Articles" {
                color #c62828
            }
            element "Group:Comments" {
                color #ef6c00
            }
            element "Group:Profanity" {
                color #ad1457
            }
            element "Group:Subscribers" {
                color #6a1b9a
            }
            element "Group:Newsletter" {
                color #1565c0
            }
            element "Group:Publishing (designed, not built)" {
                color #9e9e9e
            }

            relationship "Relationship" {
                color #616161
                thickness 3
                style solid
                fontSize 26
            }
        }
    }
}
