-- The CommentDatabase. Three comments on each of the 300 articles.
CREATE TABLE comments (
    id         serial PRIMARY KEY,
    article_id int NOT NULL,
    author     text NOT NULL,
    body       text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX comments_article_id ON comments (article_id);

INSERT INTO comments (article_id, author, body)
SELECT a, 'Reader ' || c, 'Comment ' || c || ' on article ' || a
FROM generate_series(1, 300) AS a, generate_series(1, 3) AS c;
