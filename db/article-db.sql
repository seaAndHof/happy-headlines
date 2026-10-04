-- The global ArticleDatabase.
-- 300 articles; article n was published n × 5 hours ago, so articles 1–67 are from the last 14 days.
CREATE TABLE articles (
    id           int PRIMARY KEY,
    title        text NOT NULL,
    content      text NOT NULL,
    published_at timestamptz NOT NULL
);

INSERT INTO articles (id, title, content, published_at)
SELECT n,
       'Good news #' || n,
       'A positive story from somewhere in the world.',
       now() - n * interval '5 hours'
FROM generate_series(1, 300) AS n;
