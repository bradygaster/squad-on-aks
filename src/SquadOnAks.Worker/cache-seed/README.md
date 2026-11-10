# Worker package cache seed

Package references only, with no app code. The worker image restores these so that
generated Aspire apps (AppHost + Api + PostgreSQL + Vite/React frontend) can
restore offline inside build jobs. When a generated app needs another package,
add it here and rebuild the worker image; do not add online restore to the worker.
