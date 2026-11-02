#!/bin/sh
# Sets up the transatlantic link (Toxiproxy): the services in Europe reach the databases in North America through it,
# and every response from a database is delayed by 100 ms. Runs at startup, and again on every `docker compose up`,
# so a link that already exists is left as it is.
set -e
API=http://transatlantic-link:8474

until curl -sf "$API/version" > /dev/null; do sleep 1; done

link() { # link <name> <listen port> <upstream>
  if curl -sf "$API/proxies/$1" > /dev/null; then
    echo "$1: already set up"
    return
  fi
  curl -sf -X POST "$API/proxies" \
    -d "{\"name\": \"$1\", \"listen\": \"0.0.0.0:$2\", \"upstream\": \"$3\"}" > /dev/null
  curl -sf -X POST "$API/proxies/$1/toxics" \
    -d '{"name": "distance", "type": "latency", "stream": "downstream", "attributes": {"latency": 100, "jitter": 10}}' > /dev/null
  echo "$1: transatlantic-link:$2 -> $3, +100 ms"
}

link article-db 5432 article-db:5432
link comment-db 5433 comment-db:5432
