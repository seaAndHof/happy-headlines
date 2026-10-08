-- The ProfanityDatabase: the words the ProfanityService masks.
CREATE TABLE profanities (
    word text PRIMARY KEY
);

INSERT INTO profanities (word) VALUES
    ('damn'), ('crap'), ('idiot'), ('stupid'), ('bloody'), ('heck'), ('moron'), ('dumb');
