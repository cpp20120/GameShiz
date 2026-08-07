# CasinoShiz Kubernetes deployment

This chart uses the same `CasinoShiz.Backend` binary for every game. The
`Backend:Modules` value is supplied as `Backend__Modules`, so a game can be
scaled or rolled out independently without changing application code.

Create the secret referenced by `values.yaml` first, then install:

```bash
kubectl create secret generic cazinoshiz-secrets \
  --from-literal=postgres-password='...' \
  --from-literal=redis-password='...' \
  --from-literal=telegram-token='...' \
  --from-literal=discord-token='...' \
  --from-literal=operations-api-key='...' \
  --from-literal=admin-superadmin-token='...'

helm upgrade --install cazinoshiz ./deploy/helm/cazinoshiz
kubectl scale deployment game-poker --replicas=3
```

For the local k3d load-test profile, layer the dev values on top of the normal
chart. It gives game backends and REST bounded `2 CPU / 1 GiB` limits, disables
the REST rate limiter, uses the Development-only bearer scheme, and pulls the
mutable local tags on every rollout:

```bash
helm upgrade --install cazinoshiz ./deploy/helm/cazinoshiz \
  -f ./deploy/helm/cazinoshiz/values.dev.yaml
```

The dev overlay contains no credentials. The existing secret must still contain
the key configured by `rest.developmentAuthentication.tokenSecretKey`.

The three PostgreSQL StatefulSets are separate physical ownership boundaries.
Redis is the default CAP/event transport and the shared tenant-context cache
for every `game-*` backend replica. Tenant provisioning still uses PostgreSQL
as its source of truth; Redis only stores a short-lived provisioning marker.
Kafka/Redpanda can be selected with
`Messaging:Transport=Kafka` and broker settings. Replicas of one logical service share its
consumer group, while different game services receive their own group. The
PostgreSQL outboxes use leases and advisory-lock-protected migrations, so
restart and horizontal scaling are safe. For production, replace the local
PostgreSQL/Redis templates with managed services by overriding the service
addresses and database secrets. Example values:

For a local profiling cluster only, the REST API also has a dev-only bearer
scheme. Set `rest.environment: Development`,
`rest.developmentAuthentication.enabled: true`, and add the configured token
key to the existing secret. The application refuses to start if this flag is
enabled outside the Development environment. Keep it disabled in production.

```yaml
messaging:
  transport: Kafka
  kafka:
    servers: redpanda.messaging.svc:9092
```
