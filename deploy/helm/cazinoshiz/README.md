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

The three PostgreSQL StatefulSets are separate physical ownership boundaries.
Redis is the default CAP/event transport; Kafka/Redpanda can be selected with
`Messaging:Transport=Kafka` and broker settings. Replicas of one logical service share its
consumer group, while different game services receive their own group. The
PostgreSQL outboxes use leases and advisory-lock-protected migrations, so
restart and horizontal scaling are safe. For production, replace the local
PostgreSQL/Redis templates with managed services by overriding the service
addresses and database secrets. Example values:

```yaml
messaging:
  transport: Kafka
  kafka:
    servers: redpanda.messaging.svc:9092
```
