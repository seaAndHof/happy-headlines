-- The SubscriberDatabase: everyone who gets the daily newsletter.
CREATE TABLE subscribers (
    email         text PRIMARY KEY,
    subscribed_at timestamptz NOT NULL DEFAULT now()
);

INSERT INTO subscribers (email) VALUES ('ada@example.com'), ('linus@example.com'), ('grace@example.com');
